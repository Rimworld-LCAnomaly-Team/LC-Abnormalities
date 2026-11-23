using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Misc;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.JudgementBird
{
    /// <summary>审判鸟与正义裁决者专属 GLSL 450 着色器与解包特效材质缓存。</summary>
    [StaticConstructorOnStartup]
    public static class JudgementBirdMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropAccentColor = Shader.PropertyToID("_AccentColor");
        public static readonly int PropPaleColor = Shader.PropertyToID("_PaleColor");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropEdgeColor = Shader.PropertyToID("_EdgeColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");
        public static readonly int PropPulse = Shader.PropertyToID("_Pulse");
        public static readonly int PropArcSpan = Shader.PropertyToID("_ArcSpan");

        public static readonly Material PaleFieldMat;
        public static readonly Material ScaleGlowMat;
        public static readonly Material JustitiaSlashMat;

        public static readonly Material GallowsTopMat;
        public static readonly Material GallowsPostMat;
        public static readonly Material GallowsBaseMat;
        public static readonly Material GallowsChainMat;
        public static readonly Material BandageWispMat;

        static JudgementBirdMaterials()
        {
            Shader paleFieldShader = ShaderDatabase.LoadShader("Map/LC_JudgementPaleField") ?? ShaderDatabase.MoteGlow;
            Shader scaleGlowShader = ShaderDatabase.LoadShader("Map/LC_JudgementScaleGlow") ?? ShaderDatabase.MoteGlow;
            Shader slashShader = ShaderDatabase.LoadShader("Map/LC_JustitiaPaleSlash") ?? ShaderDatabase.MoteGlow;

            Texture2D scaleTex = ContentFinder<Texture2D>.Get("Things/VFX/JudgementBird/Unbalanced_Scale", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D gallowsTopTex = ContentFinder<Texture2D>.Get("Things/VFX/JudgementBird/Gallows_Top", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D gallowsPostTex = ContentFinder<Texture2D>.Get("Things/VFX/JudgementBird/Gallows_Post", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D gallowsBaseTex = ContentFinder<Texture2D>.Get("Things/VFX/JudgementBird/Gallows_Base", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D gallowsChainTex = ContentFinder<Texture2D>.Get("Things/VFX/JudgementBird/Gallows_Chain", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D bandageTex = ContentFinder<Texture2D>.Get("Things/VFX/JudgementBird/Bandage_Wisp", reportFailure: false) ?? BaseContent.WhiteTex;

            PaleFieldMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = paleFieldShader,
                color = new Color(0.60f, 0.94f, 1.0f, 1.0f)
            });

            ScaleGlowMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = scaleTex,
                shader = scaleGlowShader,
                color = new Color(1.0f, 0.92f, 0.58f, 1.0f)
            });

            JustitiaSlashMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = slashShader,
                color = new Color(0.56f, 0.94f, 1.0f, 1.0f)
            });

            GallowsTopMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = gallowsTopTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });

            GallowsPostMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = gallowsPostTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });

            GallowsBaseMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = gallowsBaseTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });

            GallowsChainMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = gallowsChainTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });

            BandageWispMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = bandageTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });
        }
    }

    /// <summary>
    /// 审判鸟蓄力审判领域与发光不公天平印记（参考脑叶解包 Unbalanced_Scale + ScaleEffect）：
    /// 地面铺开 8.5 格苍白蓝伤审判法阵并随读条向边界收束，头顶浮现摇曳倾斜的发光黄金天平与飘散白绷带。
    /// </summary>
    public class Thing_JudgementCastAura : Thing
    {
        private Pawn caster;
        private Vector3 anchorPos;
        private float radius = 8.5f;
        private int totalCastTicks = 275;
        private int ageTicks;
        private bool finished;
        private int fadeOutTicks;
        private const int FadeDuration = 18;
        private bool initialized;

        private MaterialPropertyBlock fieldProps;
        private MaterialPropertyBlock scaleProps;

        public void Initialize(Pawn casterPawn, float judgeRadius, int castDurationTicks)
        {
            caster = casterPawn;
            anchorPos = casterPawn != null ? casterPawn.DrawPos.Yto0() : Position.ToVector3Shifted().Yto0();
            radius = Mathf.Max(2f, judgeRadius);
            totalCastTicks = Mathf.Max(30, castDurationTicks);
            ageTicks = 0;
            finished = false;
            fadeOutTicks = 0;
            initialized = true;
        }

        public void NotifyVerdictTriggered()
        {
            finished = true;
            fadeOutTicks = 0;
        }

        public void CancelEarly()
        {
            finished = true;
            fadeOutTicks = FadeDuration / 2;
        }

        protected override void Tick()
        {
            if (!initialized)
            {
                Destroy();
                return;
            }

            ageTicks++;

            if (!finished)
            {
                if (caster == null || caster.Dead || caster.Destroyed || !caster.Spawned)
                {
                    finished = true;
                }
                else
                {
                    CompJudgementBird comp = caster.TryGetComp<CompJudgementBird>();
                    if (comp == null || !comp.Escaped || !comp.IsJudging)
                    {
                        finished = true;
                    }
                    else
                    {
                        anchorPos = caster.DrawPos.Yto0();
                        // Periodically spawn drifting white bandages around Judgement Bird while weighing souls
                        if (ageTicks % 20 == 0 && Map != null)
                        {
                            JudgementBirdVfxUtility.SpawnBandageWisp(
                                Map,
                                anchorPos + new Vector3(Rand.Range(-1.1f, 1.1f), 0f, Rand.Range(-0.2f, 1.2f)),
                                new Vector3(Rand.Range(-0.014f, 0.014f), 0f, Rand.Range(0.018f, 0.036f)),
                                Rand.Range(0.55f, 0.85f),
                                Rand.Range(42, 68));
                        }
                    }
                }

                if (ageTicks > totalCastTicks + 40)
                {
                    finished = true;
                }
            }
            else
            {
                fadeOutTicks++;
                if (fadeOutTicks >= FadeDuration)
                {
                    Destroy();
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized)
            {
                return;
            }

            float progress = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, totalCastTicks));
            float openAlpha = Mathf.Clamp01(ageTicks / 24f);
            float closeAlpha = finished ? Mathf.Clamp01(1f - fadeOutTicks / (float)FadeDuration) : 1f;
            float alpha = openAlpha * closeAlpha;
            if (alpha <= 0.01f)
            {
                return;
            }

            float ageSecs = ageTicks / 60f;
            float fieldAlt = AltitudeLayer.Blueprint.AltitudeFor() + 0.02f;
            float overheadAlt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;

            // 1. Ground Pale Soul Judgement Domain Ring (8.5-cell exact radius)
            if (JudgementBirdMaterials.PaleFieldMat != null)
            {
                fieldProps ??= new MaterialPropertyBlock();
                fieldProps.SetColor(JudgementBirdMaterials.PropColor, new Color(0.58f, 0.94f, 1.0f, 1.0f));
                fieldProps.SetColor(JudgementBirdMaterials.PropAccentColor, new Color(1.0f, 0.90f, 0.56f, 1.0f));
                fieldProps.SetFloat(JudgementBirdMaterials.PropProgress, progress);
                fieldProps.SetFloat(JudgementBirdMaterials.PropAgeSecs, ageSecs);
                fieldProps.SetFloat(JudgementBirdMaterials.PropAlpha, alpha * 0.90f);
                fieldProps.SetFloat(JudgementBirdMaterials.PropMode, 0f);

                float diameter = radius * 2.08f;
                Matrix4x4 fieldMat = Matrix4x4.TRS(
                    new Vector3(anchorPos.x, fieldAlt, anchorPos.z),
                    Quaternion.identity,
                    new Vector3(diameter, 1f, diameter));
                Graphics.DrawMesh(MeshPool.plane10, fieldMat, JudgementBirdMaterials.PaleFieldMat, 0, null, 0, fieldProps);
            }

            // 2. Floating Luminous Unbalanced Scale Sigil overhead (appears prominently once BigChoo manifests)
            if (JudgementBirdMaterials.ScaleGlowMat != null)
            {
                float sigilReveal = Mathf.Clamp01((ageTicks - 20f) / 36f);
                float sigilAlpha = sigilReveal * closeAlpha;
                if (sigilAlpha > 0.01f)
                {
                    // Gentle sway early, then snaps to a steep unbalanced verdict tilt after 55% cast progress
                    float tiltDeg = progress < 0.52f
                        ? Mathf.Sin(ageSecs * 2.4f) * 8f - 6f
                        : Mathf.Lerp(-8f, 19f, Mathf.SmoothStep(0f, 1f, (progress - 0.52f) / 0.22f))
                          + Mathf.Sin(ageSecs * 9f) * 1.6f;

                    float scalePulse = 1.18f + 0.35f * Mathf.SmoothStep(0.65f, 1.0f, progress);
                    float sz = Mathf.Lerp(1.65f, 2.35f, sigilReveal) * (1f + 0.06f * Mathf.Sin(ageSecs * 4.5f));

                    scaleProps ??= new MaterialPropertyBlock();
                    scaleProps.SetColor(JudgementBirdMaterials.PropColor, new Color(1.0f, 0.91f, 0.54f, 1.0f));
                    scaleProps.SetColor(JudgementBirdMaterials.PropPaleColor, new Color(0.66f, 0.96f, 1.0f, 1.0f));
                    scaleProps.SetFloat(JudgementBirdMaterials.PropProgress, progress);
                    scaleProps.SetFloat(JudgementBirdMaterials.PropAgeSecs, ageSecs);
                    scaleProps.SetFloat(JudgementBirdMaterials.PropAlpha, sigilAlpha * 0.95f);
                    scaleProps.SetFloat(JudgementBirdMaterials.PropPulse, scalePulse);

                    Vector3 sigilPos = new Vector3(
                        anchorPos.x,
                        overheadAlt,
                        anchorPos.z + 1.55f + Mathf.Sin(ageSecs * 2.1f) * 0.08f);
                    Matrix4x4 sigilMat = Matrix4x4.TRS(
                        sigilPos,
                        Quaternion.AngleAxis(tiltDeg, Vector3.up),
                        new Vector3(sz, 1f, sz));
                    Graphics.DrawMesh(MeshPool.plane10, sigilMat, JudgementBirdMaterials.ScaleGlowMat, 0, null, 0, scaleProps);
                }
            }
        }
    }

    /// <summary>审判鸟天平落定瞬间的全范围苍白灵魂冲击波与天平强光爆发。</summary>
    public class Thing_JudgementPaleWave : Thing
    {
        private Vector3 centerPos;
        private float radius = 8.5f;
        private int ageTicks;
        private const int DurationTicks = 38;
        private bool initialized;

        private MaterialPropertyBlock waveProps;
        private MaterialPropertyBlock flashScaleProps;

        public void Initialize(Vector3 center, float waveRadius)
        {
            centerPos = center.Yto0();
            radius = Mathf.Max(2f, waveRadius);
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || ++ageTicks >= DurationTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)DurationTicks);
            float ageSecs = ageTicks / 60f;
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f;

            if (JudgementBirdMaterials.PaleFieldMat != null)
            {
                waveProps ??= new MaterialPropertyBlock();
                waveProps.SetColor(JudgementBirdMaterials.PropColor, new Color(0.62f, 0.96f, 1.0f, 1.0f));
                waveProps.SetColor(JudgementBirdMaterials.PropAccentColor, new Color(1.0f, 0.93f, 0.62f, 1.0f));
                waveProps.SetFloat(JudgementBirdMaterials.PropProgress, t);
                waveProps.SetFloat(JudgementBirdMaterials.PropAgeSecs, ageSecs);
                waveProps.SetFloat(JudgementBirdMaterials.PropAlpha, 1.0f);
                waveProps.SetFloat(JudgementBirdMaterials.PropMode, 1f);

                float diameter = radius * 2.22f;
                Matrix4x4 mat = Matrix4x4.TRS(
                    new Vector3(centerPos.x, alt, centerPos.z),
                    Quaternion.identity,
                    new Vector3(diameter, 1f, diameter));
                Graphics.DrawMesh(MeshPool.plane10, mat, JudgementBirdMaterials.PaleFieldMat, 0, null, 0, waveProps);
            }

            // Blinding expanding Unbalanced Scale verdict flash at the epicentre
            if (t < 0.72f && JudgementBirdMaterials.ScaleGlowMat != null)
            {
                float ft = t / 0.72f;
                float flashAlpha = Mathf.Pow(1f - ft, 1.55f);
                float sz = Mathf.Lerp(2.2f, 4.1f, 1f - Mathf.Pow(1f - ft, 2f));

                flashScaleProps ??= new MaterialPropertyBlock();
                flashScaleProps.SetColor(JudgementBirdMaterials.PropColor, new Color(0.82f, 0.98f, 1.0f, 1.0f));
                flashScaleProps.SetColor(JudgementBirdMaterials.PropPaleColor, Color.white);
                flashScaleProps.SetFloat(JudgementBirdMaterials.PropProgress, 1f);
                flashScaleProps.SetFloat(JudgementBirdMaterials.PropAgeSecs, ageSecs);
                flashScaleProps.SetFloat(JudgementBirdMaterials.PropAlpha, flashAlpha);
                flashScaleProps.SetFloat(JudgementBirdMaterials.PropPulse, 2.0f);

                Matrix4x4 smat = Matrix4x4.TRS(
                    new Vector3(centerPos.x, alt + 0.03f, centerPos.z + 1.35f),
                    Quaternion.AngleAxis(22f, Vector3.up),
                    new Vector3(sz, 1f, sz));
                Graphics.DrawMesh(MeshPool.plane10, smat, JudgementBirdMaterials.ScaleGlowMat, 0, null, 0, flashScaleProps);
            }
        }
    }

    /// <summary>
    /// 审判鸟处决绞刑架演出（还原脑叶解包 LongBirdScaffold / 교수형1~3 + 교수대끈）：
    /// 当人类被审判鸟苍白审判处决致死时，三爪黑羽支架破土而出，竖起红白羽绞刑梁并垂下摇曳的黄金吊链。
    /// </summary>
    public class Thing_JudgementGallows : Thing
    {
        private Vector3 exactPos;
        private int ageTicks;
        private const int TotalTicks = 270; // 4.5s solemn execution lingering
        private bool hangFollowupPlayed;
        private bool initialized;

        private readonly MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

        public void Initialize(Vector3 worldPos)
        {
            exactPos = worldPos.Yto0();
            ageTicks = 0;
            hangFollowupPlayed = false;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized)
            {
                Destroy();
                return;
            }

            ageTicks++;

            if (!hangFollowupPlayed && ageTicks >= 16)
            {
                hangFollowupPlayed = true;
                if (Map != null && Position.InBounds(Map))
                {
                    LCAbnormalitiesHelper.PlaySoundAt(JudgementBirdDefOf.LC_JudgementBird_Hanging, this);
                    for (int i = 0; i < 4; i++)
                    {
                        JudgementBirdVfxUtility.SpawnBandageWisp(
                            Map,
                            exactPos + new Vector3(Rand.Range(-0.55f, 0.55f), 0f, Rand.Range(0.2f, 1.5f)),
                            new Vector3(Rand.Range(-0.018f, 0.018f), 0f, Rand.Range(0.015f, 0.035f)),
                            Rand.Range(0.55f, 0.85f),
                            Rand.Range(48, 76));
                    }
                }
            }

            if (ageTicks >= TotalTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized)
            {
                return;
            }

            float ageSecs = ageTicks / 60f;
            float riseBase = Mathf.Clamp01(ageTicks / 10f);
            float risePost = Mathf.Clamp01((ageTicks - 4f) / 14f);
            float riseTop = Mathf.Clamp01((ageTicks - 12f) / 14f);
            float dropChain = Mathf.Clamp01((ageTicks - 18f) / 16f);
            float fadeOut = ageTicks > TotalTicks - 45
                ? Mathf.Clamp01((TotalTicks - ageTicks) / 45f)
                : 1f;

            if (fadeOut <= 0.01f)
            {
                return;
            }

            float baseAlt = AltitudeLayer.MoteOverhead.AltitudeFor() - 0.02f;
            float postEase = 1f - Mathf.Pow(1f - risePost, 3f);
            float topEase = 1f - Mathf.Pow(1f - riseTop, 3f);
            float chainEase = 1f - Mathf.Pow(1f - dropChain, 2.5f);

            // 1. Three-taloned feathered base claw (Gallows_Base)
            DrawPart(
                JudgementBirdMaterials.GallowsBaseMat,
                new Vector3(exactPos.x + 0.42f, baseAlt, exactPos.z - 0.12f),
                0f,
                1.45f * riseBase,
                0.78f * riseBase,
                fadeOut);

            // 2. Tall black feathered vertical pillar (Gallows_Post)
            if (postEase > 0.02f)
            {
                float postH = 2.15f * postEase;
                DrawPart(
                    JudgementBirdMaterials.GallowsPostMat,
                    new Vector3(exactPos.x + 0.42f, baseAlt + 0.01f, exactPos.z - 0.12f + postH * 0.48f),
                    0f,
                    0.46f,
                    postH,
                    fadeOut);
            }

            // 3. Feathered crossbar with iconic red-tipped white feather (Gallows_Top)
            if (topEase > 0.02f)
            {
                DrawPart(
                    JudgementBirdMaterials.GallowsTopMat,
                    new Vector3(exactPos.x + 0.10f, baseAlt + 0.02f, exactPos.z + 1.86f),
                    Mathf.Lerp(-12f, 0f, topEase),
                    1.68f * topEase,
                    0.86f * topEase,
                    fadeOut);
            }

            // 4. Gently swaying golden gallows chain hanging over the condemned victim (Gallows_Chain)
            if (chainEase > 0.02f)
            {
                float sway = Mathf.Sin(ageSecs * 2.2f) * 6.5f;
                float chainLen = 1.15f * chainEase;
                DrawPart(
                    JudgementBirdMaterials.GallowsChainMat,
                    new Vector3(exactPos.x - 0.34f + Mathf.Sin(sway * Mathf.Deg2Rad) * 0.12f, baseAlt + 0.03f, exactPos.z + 1.78f - chainLen * 0.48f),
                    sway,
                    0.22f,
                    chainLen,
                    fadeOut);
            }
        }

        private void DrawPart(Material mat, Vector3 pos, float angleDeg, float sizeX, float sizeZ, float alpha)
        {
            if (mat == null || alpha <= 0.01f || sizeX <= 0.01f || sizeZ <= 0.01f)
            {
                return;
            }

            propBlock.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, alpha));
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angleDeg, Vector3.up), new Vector3(sizeX, 1f, sizeZ));
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0, null, 0, propBlock);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref exactPos, "jbGallowsPos");
            Scribe_Values.Look(ref ageTicks, "jbGallowsAge", 0);
            Scribe_Values.Look(ref hangFollowupPlayed, "jbGallowsSound", false);
            Scribe_Values.Look(ref initialized, "jbGallowsInit", false);
        }
    }

    /// <summary>审判鸟与正义裁决者专属飘散绷带残片特效（使用解包 Bandage.png）。</summary>
    public class Thing_JudgementBandageWisp : Thing
    {
        private Vector3 exactPos;
        private Vector3 velocity;
        private float baseScale = 0.65f;
        private float angleDeg;
        private float spinSpeed;
        private int ageTicks;
        private int lifetimeTicks = 54;
        private float phaseSeed;
        private bool initialized;

        private readonly MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

        public void Initialize(Vector3 pos, Vector3 vel, float scale, int life)
        {
            exactPos = pos.Yto0();
            velocity = vel.Yto0();
            baseScale = Mathf.Max(0.2f, scale);
            lifetimeTicks = Mathf.Max(16, life);
            angleDeg = Rand.Range(-35f, 35f);
            spinSpeed = Rand.Range(-1.8f, 1.8f);
            phaseSeed = Rand.Range(0f, 6.28f);
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized)
            {
                Destroy();
                return;
            }

            ageTicks++;
            exactPos += velocity;
            exactPos.x += Mathf.Sin(ageTicks * 0.16f + phaseSeed) * 0.012f;
            angleDeg += spinSpeed;
            velocity *= 0.975f;

            if (ageTicks >= lifetimeTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || JudgementBirdMaterials.BandageWispMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, lifetimeTicks));
            float alpha = t < 0.18f
                ? t / 0.18f
                : (t > 0.60f ? Mathf.Lerp(1f, 0f, (t - 0.60f) / 0.40f) : 1f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float sz = baseScale * Mathf.Lerp(0.85f, 1.25f, t);
            Vector3 pos = new Vector3(exactPos.x, AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f, exactPos.z);
            propBlock.SetColor(ShaderPropertyIDs.Color, new Color(0.88f, 0.97f, 1.0f, alpha * 0.88f));
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(angleDeg, Vector3.up), new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, matrix, JudgementBirdMaterials.BandageWispMat, 0, null, 0, propBlock);
        }
    }

    /// <summary>正义裁决者（Justitia）与审判鸟专属苍白月牙斩击（由自定义 GLSL 450 着色器 LC_JustitiaPaleSlash 渲染）。</summary>
    public class Thing_JustitiaSlash : Thing
    {
        private Vector3 exactPos;
        private float angleDeg;
        private float scale = 1.6f;
        private float arcSpan = 2.45f;
        private bool mirrorSweep;
        private bool goldenVerdict;
        private int ageTicks;
        private int durationTicks = 16;
        private bool initialized;

        private MaterialPropertyBlock slashProps;

        public void Initialize(Vector3 pos, float yawAngleDeg, float slashScale, bool flipSweep, bool isGoldenVerdict = false, int lifeTicks = 16, float spanRad = 2.45f)
        {
            exactPos = pos.Yto0();
            angleDeg = yawAngleDeg;
            scale = Mathf.Max(0.6f, slashScale);
            mirrorSweep = flipSweep;
            goldenVerdict = isGoldenVerdict;
            durationTicks = Mathf.Clamp(lifeTicks, 10, 32);
            arcSpan = Mathf.Clamp(spanRad, 1.4f, 3.6f);
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || ++ageTicks >= durationTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || JudgementBirdMaterials.JustitiaSlashMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, durationTicks));
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.06f;

            Color mainCol = goldenVerdict
                ? new Color(0.76f, 0.96f, 1.0f, 1.0f)
                : new Color(0.54f, 0.92f, 1.0f, 1.0f);
            Color coreCol = goldenVerdict
                ? new Color(1.0f, 0.97f, 0.84f, 1.0f)
                : new Color(0.95f, 0.99f, 1.0f, 1.0f);
            Color edgeCol = goldenVerdict
                ? new Color(1.0f, 0.86f, 0.44f, 1.0f)
                : new Color(0.74f, 0.60f, 1.0f, 1.0f);

            slashProps ??= new MaterialPropertyBlock();
            slashProps.SetColor(JudgementBirdMaterials.PropColor, mainCol);
            slashProps.SetColor(JudgementBirdMaterials.PropCoreColor, coreCol);
            slashProps.SetColor(JudgementBirdMaterials.PropEdgeColor, edgeCol);
            slashProps.SetFloat(JudgementBirdMaterials.PropProgress, t);
            slashProps.SetFloat(JudgementBirdMaterials.PropAgeSecs, ageTicks / 60f);
            slashProps.SetFloat(JudgementBirdMaterials.PropAlpha, 1.0f);
            slashProps.SetFloat(JudgementBirdMaterials.PropArcSpan, arcSpan);

            float grow = Mathf.Lerp(0.88f, 1.12f, t);
            float sx = scale * grow;
            float sz = scale * grow;
            Mesh mesh = mirrorSweep ? MeshPool.plane10Flip : MeshPool.plane10;
            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.AngleAxis(angleDeg, Vector3.up),
                new Vector3(sx, 1f, sz));
            Graphics.DrawMesh(mesh, matrix, JudgementBirdMaterials.JustitiaSlashMat, 0, null, 0, slashProps);
        }
    }

    /// <summary>
    /// 正义裁决者（Justitia）多段连斩演出驱动器（还原脑叶解包 LongBirdWeapon Attack_JudeSword_01 5连斩 / Attack_JudgeSword_02 扩展裁决连斩）：
    /// 按错落节奏释放多道苍白月牙刀光、音效与黄金天平裁决闪影。
    /// </summary>
    public class Thing_JustitiaComboRunner : Thing
    {
        private Vector3 targetPos;
        private float baseAngleDeg;
        private bool extendedCombo;
        private int ageTicks;
        private int totalTicks = 24;
        private int nextStepIndex;
        private bool initialized;

        private MaterialPropertyBlock scaleProps;

        // Tick timings matching Attack_JudeSword_01 (5 rapid hits) vs Attack_JudgeSword_02 (heavy triple + 5 rapid flurry)
        private static readonly int[] StandardTicks = { 0, 4, 8, 12, 16 };
        private static readonly int[] ExtendedTicks = { 0, 6, 12, 18, 21, 24, 27, 30 };

        public void Initialize(Vector3 worldPos, float attackYawDeg, bool isExtendedVerdict)
        {
            targetPos = worldPos.Yto0();
            baseAngleDeg = attackYawDeg;
            extendedCombo = isExtendedVerdict;
            ageTicks = 0;
            nextStepIndex = 0;
            totalTicks = isExtendedVerdict ? 42 : 24;
            initialized = true;
            TriggerPendingSteps();
        }

        protected override void Tick()
        {
            if (!initialized)
            {
                Destroy();
                return;
            }

            ageTicks++;
            TriggerPendingSteps();

            if (ageTicks >= totalTicks)
            {
                Destroy();
            }
        }

        private void TriggerPendingSteps()
        {
            if (Map == null)
            {
                return;
            }

            int[] schedule = extendedCombo ? ExtendedTicks : StandardTicks;
            while (nextStepIndex < schedule.Length && ageTicks >= schedule[nextStepIndex])
            {
                int idx = nextStepIndex++;
                bool isHeavy = extendedCombo && idx < 3;
                float angleJitter = isHeavy
                    ? (idx - 1) * 48f + Rand.Range(-10f, 10f)
                    : Rand.Range(-165f, 165f);
                Vector3 posJitter = new Vector3(Rand.Range(-0.24f, 0.24f), 0f, Rand.Range(-0.24f, 0.24f));
                float slashScale = isHeavy ? Rand.Range(2.15f, 2.55f) : Rand.Range(1.35f, 1.85f);
                float span = isHeavy ? 2.85f : 2.35f;
                int life = isHeavy ? 18 : 13;

                JudgementBirdVfxUtility.SpawnPaleSlash(
                    Map,
                    targetPos + posJitter,
                    baseAngleDeg + angleJitter,
                    slashScale,
                    flipSweep: (idx % 2) == 1,
                    isGoldenVerdict: extendedCombo && (isHeavy || idx == schedule.Length - 1),
                    lifeTicks: life,
                    spanRad: span);

                // Play authentic Lobotomy Corp Justitia blade SFX on key beats
                if (idx == 0)
                {
                    SoundDef s = extendedCombo
                        ? (JudgementBirdDefOf.LC_Justitia_Slash4 ?? JudgementBirdDefOf.LC_Justitia_Slash1)
                        : JudgementBirdDefOf.LC_Justitia_Slash1;
                    LCAbnormalitiesHelper.PlaySoundAt(s, this);
                }
                else if (idx == 2)
                {
                    LCAbnormalitiesHelper.PlaySoundAt(JudgementBirdDefOf.LC_Justitia_Slash2, this);
                }
                else if (extendedCombo && idx == 4)
                {
                    LCAbnormalitiesHelper.PlaySoundAt(JudgementBirdDefOf.LC_Justitia_Slash3 ?? JudgementBirdDefOf.LC_Justitia_Slash1, this);
                }

                if (extendedCombo && (idx == 0 || idx == 3))
                {
                    JudgementBirdVfxUtility.SpawnBandageWisp(
                        Map,
                        targetPos + posJitter,
                        new Vector3(Rand.Range(-0.025f, 0.025f), 0f, Rand.Range(0.02f, 0.045f)),
                        Rand.Range(0.45f, 0.70f),
                        Rand.Range(28, 44));
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // During the 40% extended Judgement Verdict combo, manifest a crisp golden Unbalanced Scale flash above the judged foe!
            if (!initialized || !extendedCombo || JudgementBirdMaterials.ScaleGlowMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, totalTicks));
            float fadeIn = Mathf.Clamp01(t / 0.14f);
            float fadeOut = t > 0.55f ? Mathf.Clamp01(1f - (t - 0.55f) / 0.45f) : 1f;
            float alpha = fadeIn * fadeOut;
            if (alpha <= 0.01f)
            {
                return;
            }

            float tilt = Mathf.Lerp(-12f, 20f, Mathf.SmoothStep(0f, 1f, t));
            float sz = Mathf.Lerp(1.15f, 1.65f, 1f - Mathf.Pow(1f - t, 2f));
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.07f;

            scaleProps ??= new MaterialPropertyBlock();
            scaleProps.SetColor(JudgementBirdMaterials.PropColor, new Color(1.0f, 0.92f, 0.56f, 1.0f));
            scaleProps.SetColor(JudgementBirdMaterials.PropPaleColor, new Color(0.70f, 0.96f, 1.0f, 1.0f));
            scaleProps.SetFloat(JudgementBirdMaterials.PropProgress, t);
            scaleProps.SetFloat(JudgementBirdMaterials.PropAgeSecs, ageTicks / 60f);
            scaleProps.SetFloat(JudgementBirdMaterials.PropAlpha, alpha * 0.92f);
            scaleProps.SetFloat(JudgementBirdMaterials.PropPulse, 1.45f);

            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(targetPos.x, alt, targetPos.z + 0.95f),
                Quaternion.AngleAxis(tilt, Vector3.up),
                new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, matrix, JudgementBirdMaterials.ScaleGlowMat, 0, null, 0, scaleProps);
        }
    }

    /// <summary>审判鸟镇压消散演出：失衡天平逐渐黯淡、苍白绷带随风飘散，最终凝结为审判鸟收容蛋。</summary>
    public class DyingJudgementBird : LC_FX_Standard
    {
        private const int TotalDurationTicks = 90;
        private int startTick;
        private MaterialPropertyBlock scaleProps;

        public override void InitWith(Pawn targetPawn)
        {
            CompAbnormality comp = targetPawn?.TryGetComp<CompAbnormality>();
            if (comp != null)
            {
                bioSignature = comp.biosignature;
            }

            startTick = Find.TickManager?.TicksGame ?? TickSpawned;
            completeTick = startTick + TotalDurationTicks;
            hasInited = true;

            if (Map != null && Position.InBounds(Map))
            {
                LCAbnormalitiesHelper.PlaySoundAt(JudgementBirdDefOf.LC_JudgementBird_Stun, this);
                Vector3 center = Position.ToVector3Shifted();
                FleckMaker.ThrowDustPuffThick(center, Map, 2.2f, new Color(0.68f, 0.92f, 1.0f));
                for (int i = 0; i < 6; i++)
                {
                    JudgementBirdVfxUtility.SpawnBandageWisp(
                        Map,
                        center + new Vector3(Rand.Range(-0.7f, 0.7f), 0f, Rand.Range(-0.3f, 0.9f)),
                        new Vector3(Rand.Range(-0.02f, 0.02f), 0f, Rand.Range(0.02f, 0.045f)),
                        Rand.Range(0.55f, 0.95f),
                        Rand.Range(55, 85));
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!hasInited || JudgementBirdMaterials.ScaleGlowMat == null)
            {
                return;
            }

            int elapsed = Mathf.Clamp((Find.TickManager?.TicksGame ?? startTick) - startTick, 0, TotalDurationTicks);
            float t = elapsed / (float)TotalDurationTicks;
            float alpha = Mathf.Pow(1f - t, 1.4f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float sz = Mathf.Lerp(2.1f, 0.65f, t);
            float tilt = Mathf.Lerp(18f, -26f, t);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.02f;

            scaleProps ??= new MaterialPropertyBlock();
            scaleProps.SetColor(JudgementBirdMaterials.PropColor, new Color(1.0f, 0.90f, 0.56f, 1.0f));
            scaleProps.SetColor(JudgementBirdMaterials.PropPaleColor, new Color(0.68f, 0.94f, 1.0f, 1.0f));
            scaleProps.SetFloat(JudgementBirdMaterials.PropProgress, 1f - t);
            scaleProps.SetFloat(JudgementBirdMaterials.PropAgeSecs, elapsed / 60f);
            scaleProps.SetFloat(JudgementBirdMaterials.PropAlpha, alpha);
            scaleProps.SetFloat(JudgementBirdMaterials.PropPulse, 1.25f);

            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(drawLoc.x, alt, drawLoc.z + Mathf.Lerp(0.85f, 0.1f, t)),
                Quaternion.AngleAxis(tilt, Vector3.up),
                new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, matrix, JudgementBirdMaterials.ScaleGlowMat, 0, null, 0, scaleProps);
        }

        public override void Complete()
        {
            if (!hasInited)
            {
                Destroy();
                return;
            }

            EggReturnExtension ext = def.GetModExtension<EggReturnExtension>();
            if (ext?.eggDef != null && MapHeld != null && PositionHeld.InBounds(MapHeld))
            {
                Thing egg = ThingMaker.MakeThing(ext.eggDef);
                AbnormalityUtility.TransferSignature(egg, bioSignature);
                GenSpawn.Spawn(egg, PositionHeld, MapHeld);
            }

            Destroy();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startTick, "jbDeadStartTick", 0);
        }
    }

    /// <summary>审判鸟与正义裁决者 VFX 生成辅助工具。</summary>
    public static class JudgementBirdVfxUtility
    {
        public static Thing_JudgementCastAura SpawnCastAura(Pawn caster, float radius, int castDurationTicks)
        {
            if (caster == null || caster.MapHeld == null || JudgementBirdDefOf.LC_JudgementCastAuraFX == null)
            {
                return null;
            }

            IntVec3 cell = caster.PositionHeld.ClampInsideMap(caster.MapHeld);
            if (!cell.InBounds(caster.MapHeld))
            {
                return null;
            }

            if (GenSpawn.Spawn(JudgementBirdDefOf.LC_JudgementCastAuraFX, cell, caster.MapHeld) is Thing_JudgementCastAura aura)
            {
                aura.Initialize(caster, radius, castDurationTicks);
                return aura;
            }

            return null;
        }

        public static void SpawnPaleWave(Map map, Vector3 center, float radius)
        {
            if (map == null || JudgementBirdDefOf.LC_JudgementPaleWaveFX == null)
            {
                return;
            }

            IntVec3 cell = center.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(JudgementBirdDefOf.LC_JudgementPaleWaveFX, cell, map) is Thing_JudgementPaleWave wave)
            {
                wave.Initialize(center, radius);
            }
        }

        public static void SpawnGallows(Map map, Vector3 victimPos)
        {
            if (map == null || JudgementBirdDefOf.LC_JudgementGallowsFX == null)
            {
                return;
            }

            IntVec3 cell = victimPos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(JudgementBirdDefOf.LC_JudgementGallowsFX, cell, map) is Thing_JudgementGallows gallows)
            {
                gallows.Initialize(victimPos);
                LCAbnormalitiesHelper.PlaySoundAt(JudgementBirdDefOf.LC_JudgementBird_Hang, gallows);
            }
        }

        public static void SpawnBandageWisp(Map map, Vector3 pos, Vector3 velocity, float scale, int lifeTicks)
        {
            if (map == null || JudgementBirdDefOf.LC_JudgementBandageFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(JudgementBirdDefOf.LC_JudgementBandageFX, cell, map) is Thing_JudgementBandageWisp wisp)
            {
                wisp.Initialize(pos, velocity, scale, lifeTicks);
            }
        }

        public static void SpawnPaleSlash(Map map, Vector3 pos, float yawAngleDeg, float scale, bool flipSweep, bool isGoldenVerdict = false, int lifeTicks = 16, float spanRad = 2.45f)
        {
            if (map == null || JudgementBirdDefOf.LC_JustitiaSlashFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(JudgementBirdDefOf.LC_JustitiaSlashFX, cell, map) is Thing_JustitiaSlash slash)
            {
                slash.Initialize(pos, yawAngleDeg, scale, flipSweep, isGoldenVerdict, lifeTicks, spanRad);
            }
        }

        public static void SpawnJustitiaCombo(Map map, Vector3 targetPos, float attackYawDeg, bool extendedVerdict)
        {
            if (map == null || JudgementBirdDefOf.LC_JustitiaComboFX == null)
            {
                return;
            }

            IntVec3 cell = targetPos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(JudgementBirdDefOf.LC_JustitiaComboFX, cell, map) is Thing_JustitiaComboRunner runner)
            {
                runner.Initialize(targetPos, attackYawDeg, extendedVerdict);
            }
        }
    }
}
