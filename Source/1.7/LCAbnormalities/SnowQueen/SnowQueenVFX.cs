using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.SnowQueen
{
    [DefOf]
    public static class SnowQueenVfxDefOf
    {
        public static ThingDef LC_SnowQueenFrozenPrisonFX;
        public static ThingDef LC_SnowQueenFirstKissMarkFX;
        public static ThingDef LC_SnowQueenFrostBurstFX;
        public static ThingDef LC_SnowQueenIceDeathFX;
        public static ThingDef LC_FrostSplinterSkillRunnerFX;

        static SnowQueenVfxDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SnowQueenVfxDefOf));
        }
    }

    /// <summary>
    /// F-01-37 冰雪女皇解包视觉特效与自定义 GLSL 450 着色器材质缓存。
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class SnowQueenMaterials
    {
        public static readonly Shader FrostAuraShader;
        public static readonly Shader IceCrystalShader;

        public static readonly Material FrostAuraMat;
        public static readonly Material FirstKissShardMat;
        public static readonly Material FrozenCaptiveMat;
        public static readonly Material RoseSwordMat;
        public static readonly Material DuelFrostFrameMat;
        public static readonly Material DuelIronGateMat;
        public static readonly Material[] IceDeathMats;
        public static readonly Material[] IceShatterMats;
        public static readonly Material[] EnergyFreezeMats;

        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropGlowColor = Shader.PropertyToID("_GlowColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");
        public static readonly int PropUrgency = Shader.PropertyToID("_Urgency");
        public static readonly int PropShimmer = Shader.PropertyToID("_Shimmer");

        static SnowQueenMaterials()
        {
            FrostAuraShader = ShaderDatabase.LoadShader("Map/LC_SnowQueenFrostAura") ?? ShaderDatabase.MoteGlow;
            IceCrystalShader = ShaderDatabase.LoadShader("Map/LC_SnowQueenIceCrystal") ?? ShaderDatabase.Transparent;

            FrostAuraMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = FrostAuraShader,
                color = new Color(0.45f, 0.90f, 1.0f, 1.0f)
            });

            FirstKissShardMat = CreateTexMat("Things/Pawn/SnowQueen/VFX/FirstKissShard", IceCrystalShader);
            FrozenCaptiveMat = CreateTexMat("Things/Pawn/SnowQueen/VFX/FrozenCaptive", IceCrystalShader);
            RoseSwordMat = CreateTexMat("Things/Pawn/SnowQueen/VFX/RoseSword", ShaderDatabase.Transparent);
            DuelFrostFrameMat = CreateTexMat("Things/Pawn/SnowQueen/VFX/DuelFrostFrame", ShaderDatabase.Transparent);
            DuelIronGateMat = CreateTexMat("Things/Pawn/SnowQueen/VFX/DuelIronGate", ShaderDatabase.Transparent);

            IceDeathMats = new Material[3];
            for (int i = 0; i < IceDeathMats.Length; i++)
            {
                IceDeathMats[i] = CreateTexMat($"Things/Pawn/SnowQueen/VFX/IceDeath_{i + 1}", IceCrystalShader);
            }

            IceShatterMats = new Material[5];
            for (int i = 0; i < IceShatterMats.Length; i++)
            {
                IceShatterMats[i] = CreateTexMat($"Things/Pawn/SnowQueen/VFX/IceShatter_{i + 1}", ShaderDatabase.Transparent);
            }

            EnergyFreezeMats = new Material[6];
            for (int i = 0; i < EnergyFreezeMats.Length; i++)
            {
                EnergyFreezeMats[i] = CreateTexMat($"Things/Pawn/SnowQueen/VFX/EnergyFreeze_{i}", IceCrystalShader);
            }
        }

        private static Material CreateTexMat(string texPath, Shader shader)
        {
            Texture2D tex = ContentFinder<Texture2D>.Get(texPath, reportFailure: false) ?? BaseContent.ClearTex;
            return MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = tex,
                shader = shader ?? ShaderDatabase.Transparent,
                color = Color.white
            });
        }
    }

    /// <summary>
    /// 冰雪女皇收容冰棺与决斗结界持续视觉实体：
    /// 呈现地面六瓣冰晶阵、渐进式 EnergyFreeze 霜蚀纹理、FrozenCaptive 冰封囚笼，
    /// 以及决斗期间激活的 DuelFrostFrame / DuelIronGate 决斗框与 RoseSword 双剑交锋光影。
    /// </summary>
    public class Thing_SnowQueenFrozenPrison : Thing
    {
        private Pawn queenPawn;
        private int ageTicks;
        private MaterialPropertyBlock auraProps;
        private MaterialPropertyBlock crystalProps;
        private MaterialPropertyBlock alphaProps;

        public Pawn QueenPawn => queenPawn;

        public void Attach(Pawn queen)
        {
            queenPawn = queen;
            ageTicks = 0;
        }

        protected override void Tick()
        {
            base.Tick();
            ageTicks++;

            CompSnowQueen comp = queenPawn?.TryGetComp<CompSnowQueen>();
            if (queenPawn == null || queenPawn.Dead || queenPawn.Destroyed || comp == null || !comp.HasFrozenCaptive || Map == null)
            {
                Destroy();
                return;
            }

            IntVec3 queenCell = queenPawn.PositionHeld;
            if (queenCell.InBounds(Map) && Position != queenCell)
            {
                Position = queenCell;
            }

            if (ageTicks % 32 == 0 && Map.flecks != null && queenCell.InBounds(Map))
            {
                Vector3 puffPos = queenPawn.DrawPos + new Vector3(Rand.Range(-0.85f, 0.25f), 0f, Rand.Range(-0.4f, 0.55f));
                if (puffPos.ToIntVec3().InBounds(Map))
                {
                    FleckMaker.ThrowDustPuffThick(puffPos, Map, Rand.Range(0.75f, 1.25f), new Color(0.68f, 0.94f, 1.0f, 0.55f));
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            CompSnowQueen comp = queenPawn?.TryGetComp<CompSnowQueen>();
            if (queenPawn == null || comp == null || !comp.HasFrozenCaptive)
            {
                return;
            }

            Vector3 anchor = queenPawn.Spawned ? queenPawn.DrawPos : queenPawn.PositionHeld.ToVector3Shifted();
            int maxFreezeTicks = Mathf.Max(1, comp.PropsSnow.freezeTimeoutTicks);
            float freezeUrgency = Mathf.Clamp01(1f - (comp.FreezeTicksLeft / (float)maxFreezeTicks));
            bool inDuel = comp.IsDuelInProgress;
            float visualUrgency = inDuel ? Mathf.Max(0.75f, freezeUrgency) : freezeUrgency;
            float ageSecs = ageTicks / 60f;

            // 1. 地面六瓣霜阵光环
            if (auraProps == null) auraProps = new MaterialPropertyBlock();
            auraProps.Clear();
            auraProps.SetColor(SnowQueenMaterials.PropColor, inDuel ? new Color(0.55f, 0.94f, 1.0f, 0.95f) : new Color(0.42f, 0.88f, 1.0f, 0.80f));
            auraProps.SetColor(SnowQueenMaterials.PropCoreColor, new Color(0.92f, 0.99f, 1.0f, 1.0f));
            auraProps.SetFloat(SnowQueenMaterials.PropProgress, visualUrgency);
            auraProps.SetFloat(SnowQueenMaterials.PropAgeSecs, ageSecs);
            auraProps.SetFloat(SnowQueenMaterials.PropAlpha, 0.85f);
            auraProps.SetFloat(SnowQueenMaterials.PropIntensity, inDuel ? 1.55f : 1.25f);
            auraProps.SetFloat(SnowQueenMaterials.PropMode, 0f);

            float auraScale = inDuel ? 3.45f : 2.95f;
            Vector3 auraPos = new Vector3(anchor.x, AltitudeLayer.MoteLow.AltitudeFor(), anchor.z - 0.1f);
            Matrix4x4 auraMat = Matrix4x4.TRS(auraPos, Quaternion.identity, new Vector3(auraScale, 1f, auraScale));
            Graphics.DrawMesh(MeshPool.plane10, auraMat, SnowQueenMaterials.FrostAuraMat, 0, null, 0, auraProps);

            // 2. EnergyFreeze_0..5 渐进霜蚀晶体纹
            if (crystalProps == null) crystalProps = new MaterialPropertyBlock();
            crystalProps.Clear();
            crystalProps.SetColor(SnowQueenMaterials.PropColor, Color.white);
            crystalProps.SetColor(SnowQueenMaterials.PropGlowColor, new Color(0.52f, 0.94f, 1.0f, 1.0f));
            crystalProps.SetFloat(SnowQueenMaterials.PropAgeSecs, ageSecs);
            crystalProps.SetFloat(SnowQueenMaterials.PropUrgency, visualUrgency);
            crystalProps.SetFloat(SnowQueenMaterials.PropShimmer, 1.15f);

            int freezeIdx = Mathf.Clamp(Mathf.FloorToInt(freezeUrgency * 5.99f), 0, SnowQueenMaterials.EnergyFreezeMats.Length - 1);
            Material freezeMat = SnowQueenMaterials.EnergyFreezeMats[freezeIdx];
            if (freezeMat != null)
            {
                Vector3 freezePos = new Vector3(anchor.x - 0.65f, AltitudeLayer.MoteOverhead.AltitudeFor() - 0.03f, anchor.z - 0.12f);
                float freezeScale = 1.65f + Mathf.Sin(ageSecs * 2.2f) * 0.03f;
                Matrix4x4 freezeMtx = Matrix4x4.TRS(freezePos, Quaternion.identity, new Vector3(freezeScale, 1f, freezeScale));
                Graphics.DrawMesh(MeshPool.plane10, freezeMtx, freezeMat, 0, null, 0, crystalProps);
            }

            // 3. 被冰封的囚笼员工冰棺（位于女皇身侧略前方）
            if (SnowQueenMaterials.FrozenCaptiveMat != null)
            {
                float pulseScale = 1.55f + 0.03f * Mathf.Sin(ageSecs * (3.0f + 8.0f * visualUrgency));
                Vector3 captivePos = new Vector3(anchor.x - 0.65f, AltitudeLayer.MoteOverhead.AltitudeFor(), anchor.z - 0.02f);
                Matrix4x4 captiveMtx = Matrix4x4.TRS(captivePos, Quaternion.identity, new Vector3(pulseScale, 1f, pulseScale));
                Graphics.DrawMesh(MeshPool.plane10, captiveMtx, SnowQueenMaterials.FrozenCaptiveMat, 0, null, 0, crystalProps);
            }

            // 4. 决斗状态叠加铁栅决斗框、霜花边饰与交锋玫瑰剑
            if (inDuel)
            {
                if (alphaProps == null) alphaProps = new MaterialPropertyBlock();
                float duelFade = Mathf.Clamp01(0.72f + 0.25f * Mathf.Sin(ageSecs * 6.5f));
                alphaProps.Clear();
                alphaProps.SetColor(SnowQueenMaterials.PropColor, new Color(1f, 1f, 1f, duelFade));

                Vector3 gatePos = new Vector3(anchor.x, AltitudeLayer.MoteOverhead.AltitudeFor() + 0.02f, anchor.z + 0.08f);
                if (SnowQueenMaterials.DuelIronGateMat != null)
                {
                    Matrix4x4 gateMtx = Matrix4x4.TRS(gatePos, Quaternion.identity, new Vector3(2.85f, 1f, 2.25f));
                    Graphics.DrawMesh(MeshPool.plane10, gateMtx, SnowQueenMaterials.DuelIronGateMat, 0, null, 0, alphaProps);
                }

                if (SnowQueenMaterials.DuelFrostFrameMat != null)
                {
                    Vector3 framePos = new Vector3(anchor.x, AltitudeLayer.MoteOverhead.AltitudeFor() + 0.035f, anchor.z + 0.08f);
                    Matrix4x4 frameMtx = Matrix4x4.TRS(framePos, Quaternion.identity, new Vector3(2.95f, 1f, 2.35f));
                    Graphics.DrawMesh(MeshPool.plane10, frameMtx, SnowQueenMaterials.DuelFrostFrameMat, 0, null, 0, alphaProps);
                }

                if (SnowQueenMaterials.RoseSwordMat != null)
                {
                    float clashPhase = Mathf.Sin(ageSecs * 14.0f);
                    float leftAngle = -28f + clashPhase * 16f;
                    float rightAngle = 28f - clashPhase * 16f;
                    Vector3 leftSwordPos = new Vector3(anchor.x - 0.14f, AltitudeLayer.MoteOverhead.AltitudeFor() + 0.055f, anchor.z + 0.55f);
                    Vector3 rightSwordPos = new Vector3(anchor.x + 0.14f, AltitudeLayer.MoteOverhead.AltitudeFor() + 0.055f, anchor.z + 0.55f);

                    Matrix4x4 leftMtx = Matrix4x4.TRS(leftSwordPos, Quaternion.AngleAxis(leftAngle, Vector3.up), new Vector3(1.15f, 1f, 1.15f));
                    Matrix4x4 rightMtx = Matrix4x4.TRS(rightSwordPos, Quaternion.AngleAxis(rightAngle, Vector3.up), new Vector3(-1.15f, 1f, 1.15f));
                    Graphics.DrawMesh(MeshPool.plane10, leftMtx, SnowQueenMaterials.RoseSwordMat, 0, null, 0, alphaProps);
                    Graphics.DrawMesh(MeshPool.plane10, rightMtx, SnowQueenMaterials.RoseSwordMat, 0, null, 0, alphaProps);
                }
            }
        }
    }

    /// <summary>
    /// 冰雪女皇“初吻”员工头顶/肩畔的冰晶吻痕随行特效。
    /// </summary>
    public class Thing_SnowQueenFirstKissMark : Thing
    {
        private Pawn hostPawn;
        private int ageTicks;
        private MaterialPropertyBlock shardProps;
        private MaterialPropertyBlock auraProps;

        public Pawn HostPawn => hostPawn;

        public void Attach(Pawn target)
        {
            hostPawn = target;
            ageTicks = 0;
        }

        protected override void Tick()
        {
            base.Tick();
            ageTicks++;

            bool hasKiss = hostPawn != null
                && !hostPawn.Dead
                && hostPawn.Spawned
                && SnowQueenDefOf.LC_SnowQueenKiss != null
                && hostPawn.health?.hediffSet?.HasHediff(SnowQueenDefOf.LC_SnowQueenKiss) == true;
            if (!hasKiss || Map == null || hostPawn.Map != Map)
            {
                Destroy();
                return;
            }

            if (hostPawn.Position.InBounds(Map) && Position != hostPawn.Position)
            {
                Position = hostPawn.Position;
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (hostPawn == null || !hostPawn.Spawned)
            {
                return;
            }

            float ageSecs = ageTicks / 60f;
            Vector3 hostPos = hostPawn.DrawPos;
            float bob = Mathf.Sin(ageSecs * 2.8f) * 0.045f;
            Vector3 shardPos = new Vector3(hostPos.x + 0.30f, AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f, hostPos.z + 0.36f + bob);

            if (auraProps == null) auraProps = new MaterialPropertyBlock();
            auraProps.Clear();
            auraProps.SetColor(SnowQueenMaterials.PropColor, new Color(0.45f, 0.90f, 1.0f, 0.45f));
            auraProps.SetColor(SnowQueenMaterials.PropCoreColor, new Color(0.92f, 0.98f, 1.0f, 0.70f));
            auraProps.SetFloat(SnowQueenMaterials.PropProgress, 0.25f);
            auraProps.SetFloat(SnowQueenMaterials.PropAgeSecs, ageSecs);
            auraProps.SetFloat(SnowQueenMaterials.PropAlpha, 0.55f);
            auraProps.SetFloat(SnowQueenMaterials.PropIntensity, 1.0f);
            auraProps.SetFloat(SnowQueenMaterials.PropMode, 0f);
            Matrix4x4 ringMtx = Matrix4x4.TRS(new Vector3(shardPos.x, shardPos.y - 0.01f, shardPos.z), Quaternion.identity, new Vector3(0.62f, 1f, 0.62f));
            Graphics.DrawMesh(MeshPool.plane10, ringMtx, SnowQueenMaterials.FrostAuraMat, 0, null, 0, auraProps);

            if (shardProps == null) shardProps = new MaterialPropertyBlock();
            shardProps.Clear();
            shardProps.SetColor(SnowQueenMaterials.PropColor, new Color(1f, 1f, 1f, 0.94f));
            shardProps.SetColor(SnowQueenMaterials.PropGlowColor, new Color(0.55f, 0.94f, 1.0f, 1.0f));
            shardProps.SetFloat(SnowQueenMaterials.PropAgeSecs, ageSecs);
            shardProps.SetFloat(SnowQueenMaterials.PropUrgency, 0.15f);
            shardProps.SetFloat(SnowQueenMaterials.PropShimmer, 1.25f);

            float tilt = Mathf.Sin(ageSecs * 1.9f) * 8f;
            Matrix4x4 shardMtx = Matrix4x4.TRS(shardPos, Quaternion.AngleAxis(tilt, Vector3.up), new Vector3(0.55f, 1f, 0.55f));
            Graphics.DrawMesh(MeshPool.plane10, shardMtx, SnowQueenMaterials.FirstKissShardMat, 0, null, 0, shardProps);
        }
    }

    /// <summary>
    /// 冰雪女皇瞬态脉冲视觉特效：支持凝霜冲击波、胜出春暖玫瑰绽放及冰晶碎裂飞溅。
    /// </summary>
    public class Thing_SnowQueenFrostBurst : Thing
    {
        private Vector3 exactPos;
        private float maxRadius = 2.4f;
        private float mode = 1f;
        private Color mainColor = new Color(0.42f, 0.88f, 1.0f, 1.0f);
        private Color coreColor = new Color(0.92f, 0.98f, 1.0f, 1.0f);
        private int ageTicks;
        private int durationTicks = 36;
        private bool spawnShatterSprites;
        private MaterialPropertyBlock propsBlock;
        private MaterialPropertyBlock spriteBlock;

        public void Configure(Vector3 pos, float radius, float shaderMode, Color primary, Color core, int lifetime, bool withShatters)
        {
            exactPos = pos;
            maxRadius = Mathf.Max(0.8f, radius);
            mode = shaderMode;
            mainColor = primary;
            coreColor = core;
            durationTicks = Mathf.Max(12, lifetime);
            spawnShatterSprites = withShatters;
            ageTicks = 0;
        }

        protected override void Tick()
        {
            base.Tick();
            ageTicks++;
            if (ageTicks >= durationTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float progress = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, durationTicks));
            float fade = 1f - progress * progress;
            float ageSecs = ageTicks / 60f;

            if (propsBlock == null) propsBlock = new MaterialPropertyBlock();
            propsBlock.Clear();
            propsBlock.SetColor(SnowQueenMaterials.PropColor, mainColor);
            propsBlock.SetColor(SnowQueenMaterials.PropCoreColor, coreColor);
            propsBlock.SetFloat(SnowQueenMaterials.PropProgress, progress);
            propsBlock.SetFloat(SnowQueenMaterials.PropAgeSecs, ageSecs);
            propsBlock.SetFloat(SnowQueenMaterials.PropAlpha, fade);
            propsBlock.SetFloat(SnowQueenMaterials.PropIntensity, 1.45f);
            propsBlock.SetFloat(SnowQueenMaterials.PropMode, mode);

            float size = maxRadius * 2f;
            Vector3 drawCenter = new Vector3(exactPos.x, AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f, exactPos.z);
            Matrix4x4 waveMtx = Matrix4x4.TRS(drawCenter, Quaternion.identity, new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, waveMtx, SnowQueenMaterials.FrostAuraMat, 0, null, 0, propsBlock);

            if (spawnShatterSprites && SnowQueenMaterials.IceShatterMats != null)
            {
                if (spriteBlock == null) spriteBlock = new MaterialPropertyBlock();
                spriteBlock.Clear();
                spriteBlock.SetColor(SnowQueenMaterials.PropColor, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - progress) * 1.15f)));

                int shardCount = SnowQueenMaterials.IceShatterMats.Length;
                for (int i = 0; i < shardCount; i++)
                {
                    Material mat = SnowQueenMaterials.IceShatterMats[i];
                    if (mat == null) continue;
                    float baseAng = (i * (Mathf.PI * 2f / shardCount)) + 0.35f;
                    float dist = maxRadius * 0.65f * progress;
                    Vector3 shardPos = new Vector3(
                        exactPos.x + Mathf.Cos(baseAng) * dist,
                        drawCenter.y + 0.02f,
                        exactPos.z + Mathf.Sin(baseAng) * dist + progress * 0.25f);
                    float spin = baseAng * Mathf.Rad2Deg + ageSecs * (160f + i * 25f);
                    float sScale = 0.58f * Mathf.Lerp(1.1f, 0.45f, progress);
                    Matrix4x4 sMtx = Matrix4x4.TRS(shardPos, Quaternion.AngleAxis(spin, Vector3.up), new Vector3(sScale, 1f, sScale));
                    Graphics.DrawMesh(MeshPool.plane10, sMtx, mat, 0, null, 0, spriteBlock);
                }
            }

            if (Mathf.Abs(mode - 2f) < 0.25f && SnowQueenMaterials.RoseSwordMat != null)
            {
                if (spriteBlock == null) spriteBlock = new MaterialPropertyBlock();
                spriteBlock.Clear();
                spriteBlock.SetColor(SnowQueenMaterials.PropColor, new Color(1f, 0.94f, 0.92f, Mathf.Clamp01((1f - progress) * 1.1f)));

                float lift = progress * 0.45f;
                Vector3 rosePos = new Vector3(exactPos.x, drawCenter.y + 0.03f, exactPos.z + 0.25f + lift);
                float roseScale = Mathf.Lerp(0.85f, 1.35f, progress);
                Matrix4x4 roseMtx = Matrix4x4.TRS(rosePos, Quaternion.identity, new Vector3(roseScale, 1f, roseScale));
                Graphics.DrawMesh(MeshPool.plane10, roseMtx, SnowQueenMaterials.RoseSwordMat, 0, null, 0, spriteBlock);
            }
        }
    }

    /// <summary>
    /// 第三吻/决斗败北时处决目标的三段渐进冰封处决动画（IceDeath_1 -> 2 -> 3 + 碎裂波）。
    /// </summary>
    public class Thing_SnowQueenIceDeath : Thing
    {
        private Vector3 exactPos;
        private int ageTicks;
        private const int TotalTicks = 96;
        private bool spawnedShatterWave;
        private MaterialPropertyBlock crystalBlock;

        public void Configure(Vector3 pos)
        {
            exactPos = pos;
            ageTicks = 0;
            spawnedShatterWave = false;
        }

        protected override void Tick()
        {
            base.Tick();
            ageTicks++;

            if (!spawnedShatterWave && ageTicks >= 64 && Map != null)
            {
                spawnedShatterWave = true;
                SnowQueenVfxUtility.SpawnKissBurst(Map, exactPos, isThirdKiss: true);
            }

            if (ageTicks >= TotalTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (SnowQueenMaterials.IceDeathMats == null || SnowQueenMaterials.IceDeathMats.Length < 3)
            {
                return;
            }

            float progress = Mathf.Clamp01(ageTicks / (float)TotalTicks);
            int stage = progress < 0.34f ? 0 : (progress < 0.68f ? 1 : 2);
            Material mat = SnowQueenMaterials.IceDeathMats[stage];
            if (mat == null)
            {
                return;
            }

            float fadeOut = progress > 0.78f ? Mathf.Lerp(1f, 0f, (progress - 0.78f) / 0.22f) : 1f;
            float ageSecs = ageTicks / 60f;

            if (crystalBlock == null) crystalBlock = new MaterialPropertyBlock();
            crystalBlock.Clear();
            crystalBlock.SetColor(SnowQueenMaterials.PropColor, new Color(1f, 1f, 1f, fadeOut));
            crystalBlock.SetColor(SnowQueenMaterials.PropGlowColor, new Color(0.55f, 0.94f, 1.0f, 1.0f));
            crystalBlock.SetFloat(SnowQueenMaterials.PropAgeSecs, ageSecs);
            crystalBlock.SetFloat(SnowQueenMaterials.PropUrgency, stage >= 2 ? 0.95f : 0.35f);
            crystalBlock.SetFloat(SnowQueenMaterials.PropShimmer, 1.35f);

            float scale = Mathf.Lerp(1.35f, 1.75f, progress);
            Vector3 drawPos = new Vector3(exactPos.x, AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f, exactPos.z + 0.1f);
            Matrix4x4 mtx = Matrix4x4.TRS(drawPos, Quaternion.identity, new Vector3(scale, 1f, scale));
            Graphics.DrawMesh(MeshPool.plane10, mtx, mat, 0, null, 0, crystalBlock);
        }
    }

    /// <summary>
    /// 冰雪女皇视觉特效生成与防重工具类。
    /// </summary>
    internal static class SnowQueenVfxUtility
    {
        public static void EnsureFrozenPrison(Pawn queen)
        {
            if (queen == null || queen.Dead || queen.Destroyed)
            {
                return;
            }

            Map map = queen.MapHeld;
            IntVec3 cell = queen.PositionHeld;
            if (map == null || !cell.InBounds(map) || SnowQueenVfxDefOf.LC_SnowQueenFrozenPrisonFX == null)
            {
                return;
            }

            List<Thing> existing = map.listerThings.ThingsOfDef(SnowQueenVfxDefOf.LC_SnowQueenFrozenPrisonFX);
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i] is Thing_SnowQueenFrozenPrison prison && prison.QueenPawn == queen && !prison.Destroyed)
                {
                    return;
                }
            }

            if (GenSpawn.Spawn(SnowQueenVfxDefOf.LC_SnowQueenFrozenPrisonFX, cell, map) is Thing_SnowQueenFrozenPrison created)
            {
                created.Attach(queen);
            }
        }

        public static void EnsureFirstKissMark(Pawn target)
        {
            if (target == null || target.Dead || !target.Spawned || target.Map == null)
            {
                return;
            }

            if (SnowQueenVfxDefOf.LC_SnowQueenFirstKissMarkFX == null)
            {
                return;
            }

            Map map = target.Map;
            List<Thing> existing = map.listerThings.ThingsOfDef(SnowQueenVfxDefOf.LC_SnowQueenFirstKissMarkFX);
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i] is Thing_SnowQueenFirstKissMark mark && mark.HostPawn == target && !mark.Destroyed)
                {
                    return;
                }
            }

            if (target.Position.InBounds(map)
                && GenSpawn.Spawn(SnowQueenVfxDefOf.LC_SnowQueenFirstKissMarkFX, target.Position, map) is Thing_SnowQueenFirstKissMark created)
            {
                created.Attach(target);
            }
        }

        public static void SpawnKissBurst(Map map, Vector3 pos, bool isThirdKiss)
        {
            if (isThirdKiss)
            {
                SpawnBurst(
                    map,
                    pos,
                    radius: 3.1f,
                    mode: 3f,
                    primary: new Color(0.50f, 0.92f, 1.0f, 1.0f),
                    core: new Color(0.95f, 0.99f, 1.0f, 1.0f),
                    lifetime: 44,
                    withShatters: true);
            }
            else
            {
                SpawnBurst(
                    map,
                    pos,
                    radius: 2.1f,
                    mode: 1f,
                    primary: new Color(0.45f, 0.90f, 1.0f, 0.95f),
                    core: new Color(0.90f, 0.98f, 1.0f, 1.0f),
                    lifetime: 32,
                    withShatters: true);
            }
        }

        public static void SpawnDuelWinThaw(Map map, Vector3 pos)
        {
            SpawnBurst(
                map,
                pos,
                radius: 3.3f,
                mode: 2f,
                primary: new Color(0.96f, 0.42f, 0.56f, 1.0f),
                core: new Color(1.0f, 0.90f, 0.72f, 1.0f),
                lifetime: 52,
                withShatters: false);
        }

        public static void SpawnFrostSplinterHit(Map map, Vector3 pos)
        {
            SpawnBurst(
                map,
                pos,
                radius: 1.55f,
                mode: 1f,
                primary: new Color(0.42f, 0.88f, 1.0f, 0.90f),
                core: new Color(0.92f, 0.99f, 1.0f, 1.0f),
                lifetime: 24,
                withShatters: true);
        }

        public static void SpawnIceDeath(Map map, Vector3 pos)
        {
            if (map == null || SnowQueenVfxDefOf.LC_SnowQueenIceDeathFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(SnowQueenVfxDefOf.LC_SnowQueenIceDeathFX, cell, map) is Thing_SnowQueenIceDeath fx)
            {
                fx.Configure(pos);
            }
        }

        private static void SpawnBurst(Map map, Vector3 pos, float radius, float mode, Color primary, Color core, int lifetime, bool withShatters)
        {
            if (map == null || SnowQueenVfxDefOf.LC_SnowQueenFrostBurstFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(SnowQueenVfxDefOf.LC_SnowQueenFrostBurstFX, cell, map) is Thing_SnowQueenFrostBurst fx)
            {
                fx.Configure(pos, radius, mode, primary, core, lifetime, withShatters);
            }
        }
    }
}
