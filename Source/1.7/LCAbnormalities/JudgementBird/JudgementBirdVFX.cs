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
        public static readonly Material GroundShadowMat;

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
            Texture2D shadowTex = ContentFinder<Texture2D>.Get("Things/Skyfaller/SkyfallerShadowCircle", reportFailure: false) ?? BaseContent.WhiteTex;

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

            GroundShadowMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = shadowTex,
                shader = ShaderDatabase.Transparent,
                color = new Color(0f, 0f, 0f, 1f)
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
    /// 审判鸟处决绞刑架演出（还原参考视频与脑叶解包 LongBirdScaffold / longBirdAgentDead）：
    /// 捕获被审判鸟击杀单位的尸体并将其立于原地，依次从地表长出三爪黑羽底座、黑羽立柱与红白羽横梁，
    /// 逐节垂下黄金吊链并在吊起瞬间触发短暂挣扎与断颈歪头，随吊链做两个完整周期的钟摆摇曳后缓缓落地释放尸体、消散绞刑架。
    /// </summary>
    public class Thing_JudgementGallows : Thing, IThingHolderWithDrawnPawn
    {
        private const int BaseStartTick = 0;
        private const int BaseEndTick = 30;
        private const int PostStartTick = 30;
        private const int PostEndTick = 50;
        private const int TopStartTick = 50;
        private const int TopEndTick = 70;
        private const int HoistStartTick = 90;
        private const int HoistEndTick = 120;
        private const int NeckSnapFxTick = 128;
        private const int SwayStartTick = 160;
        private const int SwayLoopTicks = 190;
        private const int ReleaseStartTick = 540; // 160 + 190 * 2
        private const int DropEndTick = 554;
        private const int TotalTicks = 590;

        private const float Ppu = 390f;
        private const float BaseW = 333f / Ppu;
        private const float BaseH = 219f / Ppu;
        private const float PostW = 85f / Ppu;
        private const float PostH = 522f / Ppu;
        private const float TopW = 492f / Ppu;
        private const float TopH = 255f / Ppu;
        private const float ArmReach = (0.684f - 0.085f) * TopW;
        private const float HoistLift = 0.60f;
        private const float ChainSegW = 0.115f;
        private const float ChainSegH = 0.355f;
        private const float ChainSegStep = 0.315f;

        private static readonly int[] ChainSegAppearTicks = { 70, 78, 86, 92, 100 };

        private ThingOwner<Thing> innerContainer;
        private Vector3 exactPos;
        private int ageTicks;
        private float visualScale = 1f;
        private float neckOffsetZ = 0.20f;
        private float targetDownedAngle = 65f;
        private int victimPawnId = -1;
        private bool hangLoopSoundPlayed;
        private bool neckSnapFxSpawned;
        private bool corpseReleased;
        private bool initialized;

        [Unsaved(false)] private float cachedBaseAlt;
        [Unsaved(false)] private float cachedBodyAngle;
        [Unsaved(false)] private float cachedChainAngleDeg;
        [Unsaved(false)] private float cachedLift;
        [Unsaved(false)] private float cachedStruggleJitterX;
        [Unsaved(false)] private Vector3 cachedVictimDrawPos;

        private readonly MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

        public Thing_JudgementGallows()
        {
            innerContainer = new ThingOwner<Thing>(this, oneStackOnly: true, LookMode.Deep);
        }

        public Corpse HeldCorpse => innerContainer != null && innerContainer.Count > 0 ? innerContainer[0] as Corpse : null;

        public int VictimPawnId => victimPawnId;

        public float HeldPawnDrawPos_Y => cachedBaseAlt + 0.014f;

        public float HeldPawnBodyAngle => cachedBodyAngle;

        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundNormal;

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public void Initialize(Vector3 worldPos, Corpse victimCorpse = null, Pawn victimPawn = null)
        {
            innerContainer ??= new ThingOwner<Thing>(this, oneStackOnly: true, LookMode.Deep);
            exactPos = worldPos.Yto0();
            ageTicks = 0;
            hangLoopSoundPlayed = false;
            neckSnapFxSpawned = false;
            corpseReleased = false;
            initialized = true;

            Pawn targetPawn = victimCorpse?.InnerPawn ?? victimPawn;
            float bodySize = targetPawn?.BodySize ?? 1f;
            visualScale = bodySize <= 1f
                ? Mathf.Lerp(0.78f, 1.0f, Mathf.Clamp01((bodySize - 0.25f) / 0.75f))
                : Mathf.Lerp(1.0f, 1.25f, Mathf.Clamp01((bodySize - 1.0f) / 1.5f));

            if (targetPawn != null && targetPawn.RaceProps?.Humanlike == true
                && targetPawn.story?.bodyType != null && targetPawn.ageTracker?.CurLifeStage != null)
            {
                float headY = targetPawn.story.bodyType.headOffset.y * Mathf.Sqrt(Mathf.Max(0.25f, targetPawn.ageTracker.CurLifeStage.bodySizeFactor));
                neckOffsetZ = Mathf.Clamp(headY - 0.14f, 0.08f, 0.36f);
            }
            else
            {
                neckOffsetZ = 0.14f * visualScale;
            }

            victimPawnId = targetPawn?.thingIDNumber ?? -1;
            targetDownedAngle = targetPawn?.Drawer?.renderer?.wiggler?.downedAngle ?? 65f;
            cachedBaseAlt = AltitudeLayer.MoteOverheadLow.AltitudeFor() + (Mathf.Abs(thingIDNumber) % 7) * 0.0002f;

            if (victimCorpse != null && !victimCorpse.Destroyed && victimCorpse.Spawned && victimCorpse.Map != null)
            {
                Map corpseMap = victimCorpse.Map;
                victimCorpse.DeSpawn(DestroyMode.Vanish);
                if (!innerContainer.TryAdd(victimCorpse))
                {
                    IntVec3 fallbackCell = exactPos.ToIntVec3().ClampInsideMap(corpseMap);
                    if (fallbackCell.InBounds(corpseMap))
                    {
                        GenPlace.TryPlaceThing(victimCorpse, fallbackCell, corpseMap, ThingPlaceMode.Near);
                    }
                }
                else if (victimCorpse.InnerPawn is Pawn innerPawn)
                {
                    innerPawn.Rotation = Rot4.South;
                    if (JudgementBirdDefOf.JudgementBird_VictimHangStart != null)
                    {
                        innerPawn.Drawer?.renderer?.SetAnimation(JudgementBirdDefOf.JudgementBird_VictimHangStart);
                    }
                }
            }

            UpdateCachedPose();
        }

        private float GetChainAnchorZ(float s)
        {
            float baseBottomZ = exactPos.z - 0.40f * s;
            float postBottomZ = baseBottomZ + (BaseH - 0.05f) * s;
            float topBottomZ = postBottomZ + (PostH - 0.05f) * s;
            return topBottomZ + (TopH * 0.617f) * s;
        }

        private static float CosineLerp(float a, float b, float t)
        {
            float k = 0.5f - 0.5f * Mathf.Cos(Mathf.Clamp01(t) * Mathf.PI);
            return Mathf.Lerp(a, b, k);
        }

        private static float EvaluatePendulumAngle(int tick)
        {
            if (tick < HoistEndTick)
            {
                return 0f;
            }

            if (tick < SwayStartTick)
            {
                float dt = tick - HoistEndTick;
                return Mathf.Sin(dt * 0.22f) * 2.5f * (1f - dt / (SwayStartTick - HoistEndTick));
            }

            if (tick < ReleaseStartTick)
            {
                float u = (tick - SwayStartTick) % SwayLoopTicks;
                if (u < 80f)
                {
                    return CosineLerp(0f, 6.28f, u / 80f);
                }
                if (u < 100f)
                {
                    return CosineLerp(6.28f, 2.73f, (u - 80f) / 20f);
                }
                if (u < 160f)
                {
                    return CosineLerp(2.73f, -5.05f, (u - 100f) / 60f);
                }
                return CosineLerp(-5.05f, 0f, (u - 160f) / 30f);
            }

            return 0f;
        }

        private void UpdateCachedPose()
        {
            float s = visualScale;
            float maxLift = HoistLift * s;

            if (ageTicks < HoistStartTick)
            {
                cachedLift = 0f;
            }
            else if (ageTicks < HoistEndTick)
            {
                float p = Mathf.Pow((ageTicks - HoistStartTick) / (float)(HoistEndTick - HoistStartTick), 0.85f);
                cachedLift = Mathf.SmoothStep(0f, 1f, p) * maxLift;
            }
            else if (ageTicks < ReleaseStartTick)
            {
                cachedLift = maxLift;
            }
            else if (ageTicks < DropEndTick)
            {
                float t = (ageTicks - ReleaseStartTick) / (float)(DropEndTick - ReleaseStartTick);
                cachedLift = (1f - t * t) * maxLift;
            }
            else
            {
                cachedLift = 0f;
            }

            cachedChainAngleDeg = EvaluatePendulumAngle(ageTicks);

            if (ageTicks < HoistStartTick)
            {
                cachedStruggleJitterX = 0f;
                cachedBodyAngle = Mathf.Sin(ageTicks * 0.14f) * 1.2f;
                cachedVictimDrawPos = new Vector3(exactPos.x, HeldPawnDrawPos_Y, exactPos.z);
                return;
            }

            float chainAnchorZ = GetChainAnchorZ(s);
            float hangChainLen = Mathf.Max(0.25f * s, chainAnchorZ - (exactPos.z + maxLift + neckOffsetZ));

            if (ageTicks < SwayStartTick)
            {
                if (ageTicks < 136)
                {
                    float t = (ageTicks - HoistStartTick) / 46f;
                    float env = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                    float thrash = Mathf.Sin(t * 28f) * 7.5f + Mathf.Sin(t * 47f) * 3.5f;
                    float limpBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((ageTicks - 122f) / 14f));
                    cachedBodyAngle = Mathf.Lerp(env * thrash, 5.0f, limpBlend);
                    cachedStruggleJitterX = env * Mathf.Sin(t * 34f) * 0.025f * s;
                }
                else
                {
                    float swayBlend = Mathf.Clamp01((ageTicks - 136f) / (float)(SwayStartTick - 136));
                    cachedBodyAngle = 5.0f + cachedChainAngleDeg * 0.65f * swayBlend;
                    cachedStruggleJitterX = 0f;
                }

                float hoistPosX = exactPos.x + cachedStruggleJitterX;
                float hoistPosZ = exactPos.z + cachedLift;
                if (ageTicks >= HoistEndTick)
                {
                    float pinBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((ageTicks - HoistEndTick) / 20f));
                    float chainRad = cachedChainAngleDeg * Mathf.Deg2Rad;
                    float neckX = exactPos.x - Mathf.Sin(chainRad) * hangChainLen;
                    float neckZ = chainAnchorZ - Mathf.Cos(chainRad) * hangChainLen;
                    float bodyRad = cachedBodyAngle * Mathf.Deg2Rad;
                    float pinnedX = neckX - neckOffsetZ * Mathf.Sin(bodyRad) + cachedStruggleJitterX;
                    float pinnedZ = neckZ - neckOffsetZ * Mathf.Cos(bodyRad);
                    cachedVictimDrawPos = new Vector3(
                        Mathf.Lerp(hoistPosX, pinnedX, pinBlend),
                        HeldPawnDrawPos_Y,
                        Mathf.Lerp(hoistPosZ, pinnedZ, pinBlend));
                }
                else
                {
                    cachedVictimDrawPos = new Vector3(hoistPosX, HeldPawnDrawPos_Y, hoistPosZ);
                }
                return;
            }

            if (ageTicks < ReleaseStartTick)
            {
                cachedStruggleJitterX = 0f;
                cachedBodyAngle = 5.0f + cachedChainAngleDeg * 0.65f;

                float chainRad = cachedChainAngleDeg * Mathf.Deg2Rad;
                float neckX = exactPos.x - Mathf.Sin(chainRad) * hangChainLen;
                float neckZ = chainAnchorZ - Mathf.Cos(chainRad) * hangChainLen;

                float bodyRad = cachedBodyAngle * Mathf.Deg2Rad;
                float vx = neckX - neckOffsetZ * Mathf.Sin(bodyRad);
                float vz = neckZ - neckOffsetZ * Mathf.Cos(bodyRad);
                cachedVictimDrawPos = new Vector3(vx, HeldPawnDrawPos_Y, vz);
                return;
            }

            cachedStruggleJitterX = 0f;
            float dropT = Mathf.Clamp01((ageTicks - ReleaseStartTick) / (float)(DropEndTick - ReleaseStartTick));
            float dropEase = dropT * dropT;
            cachedBodyAngle = Mathf.LerpAngle(5.0f, targetDownedAngle, dropEase);
            cachedVictimDrawPos = new Vector3(exactPos.x, HeldPawnDrawPos_Y, exactPos.z + cachedLift);
        }

        private void ReleaseHeldCorpse(Map targetMap = null, bool playLandingDust = true)
        {
            corpseReleased = true;
            Corpse corpse = HeldCorpse;
            if (corpse == null)
            {
                return;
            }

            if (corpse.InnerPawn?.Drawer?.renderer != null)
            {
                corpse.InnerPawn.Drawer.renderer.SetAnimation(null);
            }

            Map dropMap = targetMap ?? Map ?? MapHeld;
            if (dropMap != null)
            {
                IntVec3 dropCell = exactPos.ToIntVec3();
                if (!dropCell.InBounds(dropMap))
                {
                    dropCell = PositionHeld.ClampInsideMap(dropMap);
                }

                if (dropCell.InBounds(dropMap))
                {
                    if (!innerContainer.TryDrop(corpse, dropCell, dropMap, ThingPlaceMode.Direct, out _, playDropSound: false))
                    {
                        innerContainer.TryDrop(corpse, dropCell, dropMap, ThingPlaceMode.Near, out _, playDropSound: false);
                    }

                    if (playLandingDust)
                    {
                        FleckMaker.ThrowDustPuff(exactPos, dropMap, 0.85f);
                    }
                    return;
                }
            }

            innerContainer.ClearAndDestroyContents();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (mode != DestroyMode.WillReplace && innerContainer != null && innerContainer.Count > 0 && Map != null && !corpseReleased)
            {
                ReleaseHeldCorpse(Map, playLandingDust: false);
            }
            base.DeSpawn(mode);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Destroyed)
            {
                return;
            }

            Map prevMap = Map ?? MapHeld;
            if (innerContainer != null && innerContainer.Count > 0)
            {
                if (prevMap != null && !corpseReleased)
                {
                    ReleaseHeldCorpse(prevMap, playLandingDust: false);
                }
                else
                {
                    innerContainer.ClearAndDestroyContents();
                }
            }
            base.Destroy(mode);
        }

        protected override void Tick()
        {
            if (Destroyed)
            {
                return;
            }

            if (!initialized)
            {
                Destroy();
                return;
            }

            ageTicks++;
            UpdateCachedPose();

            if (!neckSnapFxSpawned && ageTicks >= NeckSnapFxTick)
            {
                neckSnapFxSpawned = true;
                if (Map != null && Position.InBounds(Map))
                {
                    for (int i = 0; i < 4; i++)
                    {
                        JudgementBirdVfxUtility.SpawnBandageWisp(
                            Map,
                            exactPos + new Vector3(Rand.Range(-0.45f, 0.45f), 0f, cachedLift + Rand.Range(0.15f, 0.75f)),
                            new Vector3(Rand.Range(-0.016f, 0.016f), 0f, Rand.Range(0.016f, 0.036f)),
                            Rand.Range(0.50f, 0.80f),
                            Rand.Range(48, 76));
                    }
                }
            }

            if (!hangLoopSoundPlayed && ageTicks >= SwayStartTick)
            {
                hangLoopSoundPlayed = true;
                if (HeldCorpse?.InnerPawn is Pawn victim && victim.Drawer?.renderer != null
                    && JudgementBirdDefOf.JudgementBird_VictimHangLoop != null)
                {
                    victim.Drawer.renderer.SetAnimation(JudgementBirdDefOf.JudgementBird_VictimHangLoop);
                }

                if (Map != null && Position.InBounds(Map))
                {
                    LCAbnormalitiesHelper.PlaySoundAt(JudgementBirdDefOf.LC_JudgementBird_Hanging, this);
                }
            }

            if (!corpseReleased && ageTicks >= DropEndTick)
            {
                ReleaseHeldCorpse(Map, playLandingDust: true);
                if (Map != null && Position.InBounds(Map))
                {
                    for (int i = 0; i < 3; i++)
                    {
                        JudgementBirdVfxUtility.SpawnBandageWisp(
                            Map,
                            exactPos + new Vector3(Rand.Range(-0.4f, 0.7f), 0f, Rand.Range(0.2f, 1.4f)),
                            new Vector3(Rand.Range(-0.015f, 0.015f), 0f, Rand.Range(0.015f, 0.032f)),
                            Rand.Range(0.48f, 0.78f),
                            Rand.Range(42, 65));
                    }
                }
            }

            if (ageTicks >= TotalTicks)
            {
                Destroy();
            }
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);

            if (initialized && !corpseReleased && HeldCorpse?.InnerPawn is Pawn victim && victim.Drawer?.renderer != null)
            {
                victim.Drawer.renderer.DynamicDrawPhaseAt(phase, cachedVictimDrawPos, Rot4.South, neverAimWeapon: true);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized)
            {
                return;
            }

            float s = visualScale;
            float gallowsAlpha = ageTicks > DropEndTick
                ? Mathf.Clamp01((TotalTicks - ageTicks) / (float)(TotalTicks - DropEndTick))
                : 1f;
            if (gallowsAlpha <= 0.01f)
            {
                return;
            }

            float chainAlpha = ageTicks < ReleaseStartTick
                ? 1f
                : (ageTicks < DropEndTick
                    ? Mathf.Clamp01((DropEndTick - ageTicks) / (float)(DropEndTick - ReleaseStartTick))
                    : 0f);

            float postX = exactPos.x + ArmReach * s;
            float baseBottomZ = exactPos.z - 0.40f * s;
            float postBottomZ = baseBottomZ + (BaseH - 0.05f) * s;
            float topBottomZ = postBottomZ + (PostH - 0.05f) * s;
            float chainAnchorX = exactPos.x;
            float chainAnchorZ = GetChainAnchorZ(s);

            // 0. Ground shadow directly beneath hoisted victim
            if (JudgementBirdMaterials.GroundShadowMat != null && cachedLift > 0.01f && !corpseReleased)
            {
                float liftFrac = Mathf.Clamp01(cachedLift / (HoistLift * s));
                float shadowAlpha = 0.42f * Mathf.Clamp01(liftFrac * 2.5f) * gallowsAlpha;
                float shadowW = Mathf.Lerp(0.72f, 0.50f, liftFrac) * s;
                float shadowH = Mathf.Lerp(0.36f, 0.24f, liftFrac) * s;
                float shadowAlt = AltitudeLayer.Shadows.AltitudeFor() + 0.005f;
                Vector3 shadowPos = new Vector3(cachedVictimDrawPos.x, shadowAlt, exactPos.z - 0.34f * s);
                propBlock.SetColor(ShaderPropertyIDs.Color, new Color(0f, 0f, 0f, shadowAlpha));
                Matrix4x4 shadowMat = Matrix4x4.TRS(shadowPos, Quaternion.identity, new Vector3(shadowW, 1f, shadowH));
                Graphics.DrawMesh(MeshPool.plane10, shadowMat, JudgementBirdMaterials.GroundShadowMat, 0, null, 0, propBlock);
            }

            // 1. Three-taloned feathered base claw (Gallows_Base)
            float baseT = Mathf.Clamp01((ageTicks - BaseStartTick) / (float)(BaseEndTick - BaseStartTick));
            float baseEase = 1f - Mathf.Pow(1f - baseT, 3f);
            DrawPartAnchored(
                JudgementBirdMaterials.GallowsBaseMat,
                new Vector3(postX, cachedBaseAlt, baseBottomZ),
                0f,
                BaseW * s,
                BaseH * s,
                baseEase,
                baseEase,
                0.520f,
                0.00f,
                gallowsAlpha);

            // 2. Tall black feathered vertical pillar (Gallows_Post)
            if (ageTicks >= PostStartTick)
            {
                float postT = Mathf.Clamp01((ageTicks - PostStartTick) / (float)(PostEndTick - PostStartTick));
                float postEase = 1f - Mathf.Pow(1f - postT, 3f);
                DrawPartAnchored(
                    JudgementBirdMaterials.GallowsPostMat,
                    new Vector3(postX, cachedBaseAlt + 0.002f, postBottomZ),
                    0f,
                    PostW * s,
                    PostH * s,
                    Mathf.Lerp(0.4f, 1.0f, postEase),
                    postEase,
                    0.558f,
                    0.00f,
                    gallowsAlpha);
            }

            // 3. Feathered crossbar with iconic red-tipped white feather (Gallows_Top)
            if (ageTicks >= TopStartTick)
            {
                float topT = Mathf.Clamp01((ageTicks - TopStartTick) / (float)(TopEndTick - TopStartTick));
                float topEase = 1f - Mathf.Pow(1f - topT, 3f);
                DrawPartAnchored(
                    JudgementBirdMaterials.GallowsTopMat,
                    new Vector3(postX, cachedBaseAlt + 0.004f, topBottomZ),
                    0f,
                    TopW * s,
                    TopH * s,
                    topEase,
                    1.0f,
                    0.684f,
                    0.04f,
                    gallowsAlpha);
            }

            // 4. Segmented swaying golden gallows chain (Gallows_Chain x5 segments)
            if (ageTicks >= ChainSegAppearTicks[0] && chainAlpha > 0.01f)
            {
                float maxLift = HoistLift * s;
                float currentChainLen;
                if (!corpseReleased && HeldCorpse != null)
                {
                    if (ageTicks < SwayStartTick)
                    {
                        float neckZ = exactPos.z + cachedLift + neckOffsetZ;
                        float dx = cachedStruggleJitterX;
                        float dz = Mathf.Max(0.20f * s, chainAnchorZ - neckZ);
                        currentChainLen = Mathf.Sqrt(dx * dx + dz * dz);
                    }
                    else
                    {
                        currentChainLen = Mathf.Max(0.25f * s, chainAnchorZ - (exactPos.z + maxLift + neckOffsetZ));
                    }
                }
                else
                {
                    float liftFrac = Mathf.Clamp01(cachedLift / maxLift);
                    currentChainLen = Mathf.Lerp(1.60f * s, 1.00f * s, liftFrac);
                }

                float chainRad = cachedChainAngleDeg * Mathf.Deg2Rad;
                float sinC = Mathf.Sin(chainRad);
                float cosC = Mathf.Cos(chainRad);
                float segW = ChainSegW * s;
                float segH = ChainSegH * s;
                float step = ChainSegStep * s;

                for (int k = 0; k < ChainSegAppearTicks.Length; k++)
                {
                    int appearTick = ChainSegAppearTicks[k];
                    if (ageTicks < appearTick)
                    {
                        continue;
                    }

                    float d0 = k * step;
                    float avail = currentChainLen - d0;
                    if (avail <= 0.02f * s)
                    {
                        continue;
                    }

                    float segAppear = Mathf.Clamp01((ageTicks - appearTick) / 3f);
                    float drawnLen = Mathf.Min(segH, avail) * segAppear;
                    if (drawnLen <= 0.01f * s)
                    {
                        continue;
                    }

                    Vector3 segPivot = new Vector3(
                        chainAnchorX - sinC * d0,
                        cachedBaseAlt + 0.008f,
                        chainAnchorZ - cosC * d0);

                    DrawPartAnchored(
                        JudgementBirdMaterials.GallowsChainMat,
                        segPivot,
                        cachedChainAngleDeg,
                        segW,
                        segH,
                        1.0f,
                        drawnLen / segH,
                        0.5f,
                        1.0f,
                        chainAlpha);
                }
            }
        }

        private void DrawPartAnchored(
            Material mat,
            Vector3 pivotWorldPos,
            float angleDeg,
            float fullW,
            float fullH,
            float scaleX,
            float scaleZ,
            float pivotU,
            float pivotV,
            float alpha)
        {
            if (mat == null || alpha <= 0.01f || scaleX <= 0.01f || scaleZ <= 0.01f)
            {
                return;
            }

            float w = fullW * scaleX;
            float h = fullH * scaleZ;
            if (w <= 0.005f || h <= 0.005f)
            {
                return;
            }

            Vector3 localCenterOffset = new Vector3((0.5f - pivotU) * w, 0f, (0.5f - pivotV) * h);
            Quaternion rot = Mathf.Abs(angleDeg) > 0.01f ? Quaternion.AngleAxis(angleDeg, Vector3.up) : Quaternion.identity;
            Vector3 centerPos = pivotWorldPos + (rot * localCenterOffset);

            propBlock.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, alpha));
            Matrix4x4 matrix = Matrix4x4.TRS(centerPos, rot, new Vector3(w, 1f, h));
            Graphics.DrawMesh(MeshPool.plane10, matrix, mat, 0, null, 0, propBlock);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "jbGallowsContainer", this);
            Scribe_Values.Look(ref exactPos, "jbGallowsPos");
            Scribe_Values.Look(ref ageTicks, "jbGallowsAge", 0);
            Scribe_Values.Look(ref visualScale, "jbGallowsScale", 1f);
            Scribe_Values.Look(ref neckOffsetZ, "jbGallowsNeckZ", 0.20f);
            Scribe_Values.Look(ref targetDownedAngle, "jbGallowsDownAngle", 65f);
            Scribe_Values.Look(ref victimPawnId, "jbGallowsVictimId", -1);
            Scribe_Values.Look(ref hangLoopSoundPlayed, "jbGallowsSound", false);
            Scribe_Values.Look(ref neckSnapFxSpawned, "jbGallowsSnapFx", false);
            Scribe_Values.Look(ref corpseReleased, "jbGallowsReleased", false);
            Scribe_Values.Look(ref initialized, "jbGallowsInit", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                innerContainer ??= new ThingOwner<Thing>(this, oneStackOnly: true, LookMode.Deep);
                visualScale = Mathf.Clamp(visualScale <= 0.01f ? 1f : visualScale, 0.75f, 1.35f);
                neckOffsetZ = Mathf.Clamp(neckOffsetZ <= 0.01f ? 0.20f : neckOffsetZ, 0.08f, 0.45f);
                cachedBaseAlt = AltitudeLayer.MoteOverheadLow.AltitudeFor() + (Mathf.Abs(thingIDNumber) % 7) * 0.0002f;

                if (!corpseReleased && HeldCorpse?.InnerPawn is Pawn victim && victim.Drawer?.renderer != null)
                {
                    victim.Rotation = Rot4.South;
                    AnimationDef anim = ageTicks >= SwayStartTick
                        ? JudgementBirdDefOf.JudgementBird_VictimHangLoop
                        : JudgementBirdDefOf.JudgementBird_VictimHangStart;
                    if (anim != null)
                    {
                        victim.Drawer.renderer.SetAnimation(anim);
                        int animElapsed = ageTicks >= SwayStartTick ? (ageTicks - SwayStartTick) : ageTicks;
                        if (victim.Drawer.renderer.renderTree != null)
                        {
                            victim.Drawer.renderer.renderTree.animationStartTick = GenTicks.TicksGame - animElapsed;
                        }
                    }
                }

                UpdateCachedPose();
            }
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
            SpawnGallowsInternal(map, victimPos, null, null);
        }

        public static void TrySpawnGallowsForVictim(Pawn victim, Map fallbackMap = null, Vector3? fallbackPos = null)
        {
            if (victim == null || victim is LC_JudgementBirdPawn)
            {
                return;
            }

            if (victim.Corpse?.ParentHolder is Thing_JudgementGallows)
            {
                return;
            }

            Corpse corpse = (victim.Corpse != null && !victim.Corpse.Destroyed && victim.Corpse.Spawned && victim.Corpse.Map != null)
                ? victim.Corpse
                : null;
            Map map = corpse?.Map ?? fallbackMap ?? victim.MapHeld;
            if (map == null || JudgementBirdDefOf.LC_JudgementGallowsFX == null)
            {
                return;
            }

            List<Thing> existing = map.listerThings?.ThingsOfDef(JudgementBirdDefOf.LC_JudgementGallowsFX);
            if (existing != null)
            {
                for (int i = 0; i < existing.Count; i++)
                {
                    if (existing[i] is Thing_JudgementGallows g && !g.Destroyed && g.VictimPawnId == victim.thingIDNumber)
                    {
                        return;
                    }
                }
            }

            Vector3 pos = corpse != null
                ? corpse.DrawPos.Yto0()
                : (fallbackPos ?? (victim.PositionHeld.IsValid ? victim.PositionHeld.ToVector3Shifted().Yto0() : Vector3.zero));
            SpawnGallowsInternal(map, pos, corpse, victim);
        }

        private static void SpawnGallowsInternal(Map map, Vector3 victimPos, Corpse corpse, Pawn victim)
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
                gallows.Initialize(victimPos, corpse, victim);
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
