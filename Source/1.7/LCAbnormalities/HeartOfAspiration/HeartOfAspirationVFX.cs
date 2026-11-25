using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.HeartOfAspiration
{
    [DefOf]
    public static class HeartOfAspirationVfxDefOf
    {
        public static ThingDef LC_HeartBearerAuraFX;
        public static ThingDef LC_HeartPulseWaveFX;

        static HeartOfAspirationVfxDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HeartOfAspirationVfxDefOf));
        }
    }

    /// <summary>T-09-77 渴望之心：活体心脏搏动着色器、静滞容器底座与心律脉动波材质缓存。</summary>
    [StaticConstructorOnStartup]
    public static class HeartOfAspirationMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropGlowColor = Shader.PropertyToID("_GlowColor");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
        public static readonly int PropBeatAmt = Shader.PropertyToID("_BeatAmt");
        public static readonly int PropRestlessAmt = Shader.PropertyToID("_RestlessAmt");
        public static readonly int PropSatisfiedAmt = Shader.PropertyToID("_SatisfiedAmt");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");

        public static readonly Material HeartCoreMat;
        public static readonly Material ContainerMat;
        public static readonly Material PulseAuraMat;

        static HeartOfAspirationMaterials()
        {
            Shader heartShader = ShaderDatabase.LoadShader("Map/LC_HeartAspirationCore") ?? ShaderDatabase.Transparent;
            Shader pulseShader = ShaderDatabase.LoadShader("Map/LC_HeartPulseAura") ?? ShaderDatabase.MoteGlow;

            Texture2D heartTex = ContentFinder<Texture2D>.Get("Things/Building/HeartOfAspiration/HeartOfAspiration", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D containerTex = ContentFinder<Texture2D>.Get("Things/Building/HeartOfAspiration/HeartOfAspiration_Container", reportFailure: false) ?? BaseContent.WhiteTex;

            HeartCoreMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = heartTex,
                shader = heartShader,
                color = Color.white
            });

            ContainerMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = containerTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });

            PulseAuraMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = pulseShader,
                color = new Color(0.96f, 0.16f, 0.26f, 1.0f)
            });
        }

        /// <summary>模拟真实心脏双搏动（Lub-Dub）曲线，返回 0..1 的收缩舒张强度。</summary>
        public static float EvaluateLubDub(float timeSecs, float beatsPerSecond)
        {
            float phase = (timeSecs * Mathf.Max(0.2f, beatsPerSecond)) % 1.0f;
            if (phase < 0f)
            {
                phase += 1.0f;
            }

            float lub = Mathf.Exp(-Mathf.Pow((phase - 0.12f) / 0.055f, 2.0f));
            float dub = 0.72f * Mathf.Exp(-Mathf.Pow((phase - 0.28f) / 0.048f, 2.0f));
            return Mathf.Clamp01(Mathf.Max(lub, dub));
        }
    }

    /// <summary>
    /// 佩戴渴望之心的殖民者专属活体特效：
    /// 1) 脚下随双倍心率脉动的猩红/绯紫/宝石金心律光环；
    /// 2) 悬浮于佩戴者肩侧、随 Lub-Dub 心跳律动收缩发光的迷你渴望之心实体；
    /// 3) 临近 60 秒躁动期时心率骤升、心脏剧烈震颤并迸发血色火星。
    /// </summary>
    public class Thing_HeartBearerAura : Thing
    {
        private Pawn bearer;
        private Thing toolEntity;
        private int ageTicks;
        private bool initialized;
        private MaterialPropertyBlock auraProps;
        private MaterialPropertyBlock heartProps;

        public Pawn Bearer => bearer;

        public void Initialize(Pawn equipper, Thing sourceTool)
        {
            bearer = equipper;
            toolEntity = sourceTool;
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || bearer == null || bearer.Destroyed || bearer.Dead || !bearer.Spawned || bearer.Map != Map)
            {
                Destroy();
                return;
            }

            CompHeartOfAspiration comp = (toolEntity as ThingWithComps)?.TryGetComp<CompHeartOfAspiration>();
            if (comp == null || !comp.IsEquipped || comp.ActiveEquipper != bearer)
            {
                Destroy();
                return;
            }

            ageTicks++;
            if (bearer.Position.InBounds(Map) && Position != bearer.Position)
            {
                Position = bearer.Position;
            }

            bool satisfied = comp.HasDealtCombatDamage;
            float restlessFrac = comp.RestlessFraction;

            int motePeriod = restlessFrac > 0.01f ? 10 : 22;
            if (ageTicks % motePeriod == 0)
            {
                Color moteCol = satisfied
                    ? new Color(1.0f, 0.54f, 0.26f)
                    : Color.Lerp(new Color(0.95f, 0.18f, 0.26f), new Color(0.98f, 0.14f, 0.56f), restlessFrac);
                FleckMaker.ThrowDustPuffThick(
                    bearer.DrawPos + new Vector3(Rand.Range(-0.32f, 0.32f), 0f, Rand.Range(-0.15f, 0.45f)),
                    Map,
                    restlessFrac > 0.01f ? 0.58f : 0.44f,
                    moteCol);

                if (restlessFrac > 0.35f)
                {
                    FleckMaker.ThrowMicroSparks(bearer.DrawPos, Map);
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || bearer == null || !bearer.Spawned)
            {
                return;
            }

            CompHeartOfAspiration comp = (toolEntity as ThingWithComps)?.TryGetComp<CompHeartOfAspiration>();
            if (comp == null || !comp.IsEquipped || comp.ActiveEquipper != bearer)
            {
                return;
            }

            bool satisfied = comp.HasDealtCombatDamage;
            float restless = comp.RestlessFraction;
            float ageSecs = ageTicks / 60f;
            float fadeIn = Mathf.Clamp01(ageTicks / 14f);

            // Doubled heartbeat when equipped; escalates rapidly during Restless warning
            float bpm = satisfied
                ? 1.45f
                : Mathf.Lerp(1.85f, 2.85f, restless);
            float beat = HeartOfAspirationMaterials.EvaluateLubDub(ageSecs, bpm);

            Color auraCol = satisfied
                ? new Color(0.98f, 0.42f, 0.22f, 0.88f)
                : Color.Lerp(new Color(0.96f, 0.16f, 0.26f, 0.90f), new Color(1.0f, 0.12f, 0.48f, 0.96f), restless);
            Color coreCol = satisfied
                ? new Color(1.0f, 0.92f, 0.68f, 1.0f)
                : new Color(1.0f, 0.82f, 0.86f, 1.0f);

            Vector3 p = bearer.DrawPos.Yto0();

            // 1. Underfoot rhythmic heartbeat pulse aura
            if (HeartOfAspirationMaterials.PulseAuraMat != null)
            {
                auraProps ??= new MaterialPropertyBlock();
                auraProps.SetColor(HeartOfAspirationMaterials.PropColor, auraCol);
                auraProps.SetColor(HeartOfAspirationMaterials.PropCoreColor, coreCol);
                auraProps.SetFloat(HeartOfAspirationMaterials.PropProgress, beat);
                auraProps.SetFloat(HeartOfAspirationMaterials.PropAgeSecs, ageSecs);
                auraProps.SetFloat(HeartOfAspirationMaterials.PropAlpha, fadeIn * (0.68f + 0.24f * beat));
                auraProps.SetFloat(HeartOfAspirationMaterials.PropIntensity, 1.15f + 0.45f * restless + 0.35f * beat);
                auraProps.SetFloat(HeartOfAspirationMaterials.PropMode, 0.0f);

                float ringSize = 1.48f + 0.22f * beat + 0.12f * restless;
                float groundAlt = AltitudeLayer.Blueprint.AltitudeFor() + 0.05f;
                Matrix4x4 aMat = Matrix4x4.TRS(
                    new Vector3(p.x, groundAlt, p.z - 0.04f),
                    Quaternion.identity,
                    new Vector3(ringSize, 1f, ringSize));
                Graphics.DrawMesh(MeshPool.plane10, aMat, HeartOfAspirationMaterials.PulseAuraMat, 0, null, 0, auraProps);
            }

            // 2. Floating miniature beating Heart of Aspiration beside/above the bearer
            if (HeartOfAspirationMaterials.HeartCoreMat != null)
            {
                float hoverZ = 0.72f + 0.06f * Mathf.Sin(ageSecs * 3.4f);
                float sideX = 0.34f + 0.04f * Mathf.Cos(ageSecs * 2.1f);
                float shakeX = restless > 0.01f ? Mathf.Sin(ageSecs * 48f) * 0.025f * restless : 0f;
                float shakeZ = restless > 0.01f ? Mathf.Cos(ageSecs * 55f) * 0.020f * restless : 0f;
                float heartScale = 0.46f * (1f + 0.22f * beat);

                heartProps ??= new MaterialPropertyBlock();
                heartProps.SetColor(HeartOfAspirationMaterials.PropColor, new Color(1f, 1f, 1f, fadeIn * 0.95f));
                heartProps.SetColor(HeartOfAspirationMaterials.PropGlowColor, new Color(1.0f, 0.18f, 0.24f, 1.0f));
                heartProps.SetFloat(HeartOfAspirationMaterials.PropAgeSecs, ageSecs);
                heartProps.SetFloat(HeartOfAspirationMaterials.PropBeatAmt, beat);
                heartProps.SetFloat(HeartOfAspirationMaterials.PropRestlessAmt, restless);
                heartProps.SetFloat(HeartOfAspirationMaterials.PropSatisfiedAmt, satisfied ? 1.0f : 0.0f);

                float overAlt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
                Matrix4x4 hMat = Matrix4x4.TRS(
                    new Vector3(p.x + sideX + shakeX, overAlt, p.z + hoverZ + shakeZ),
                    Quaternion.AngleAxis(Mathf.Sin(ageSecs * 3.2f) * 6f, Vector3.up),
                    new Vector3(heartScale, 1f, heartScale));
                Graphics.DrawMesh(MeshPool.plane10, hMat, HeartOfAspirationMaterials.HeartCoreMat, 0, null, 0, heartProps);
            }
        }
    }

    /// <summary>渴望之心脉冲波与狂暴反噬爆发特效（Mode 1 心跳/战斗嗜血涟漪；Mode 2 狂乱猩红碎裂冲击）。</summary>
    public class Thing_HeartPulseWave : Thing
    {
        private Vector3 exactPos;
        private float maxDiameter = 3.2f;
        private int durationTicks = 28;
        private int ageTicks;
        private Color waveColor = new Color(0.96f, 0.16f, 0.26f, 1.0f);
        private Color coreColor = new Color(1.0f, 0.86f, 0.88f, 1.0f);
        private float intensity = 1.35f;
        private float shaderMode = 1.0f;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 pos, float radius, int duration, Color color, Color core, float customIntensity, float mode)
        {
            exactPos = pos.Yto0();
            maxDiameter = Mathf.Max(1.2f, radius * 2.0f);
            durationTicks = Mathf.Max(12, duration);
            waveColor = color;
            coreColor = core;
            intensity = customIntensity;
            shaderMode = mode;
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
            if (!initialized || HeartOfAspirationMaterials.PulseAuraMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float easeOut = 1f - Mathf.Pow(1f - t, 2.2f);
            float alpha = Mathf.Pow(1f - t, 1.30f);
            if (alpha <= 0.01f)
            {
                return;
            }

            props ??= new MaterialPropertyBlock();
            props.SetColor(HeartOfAspirationMaterials.PropColor, waveColor);
            props.SetColor(HeartOfAspirationMaterials.PropCoreColor, coreColor);
            props.SetFloat(HeartOfAspirationMaterials.PropProgress, easeOut);
            props.SetFloat(HeartOfAspirationMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(HeartOfAspirationMaterials.PropAlpha, alpha);
            props.SetFloat(HeartOfAspirationMaterials.PropIntensity, intensity);
            props.SetFloat(HeartOfAspirationMaterials.PropMode, shaderMode);

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.identity,
                new Vector3(maxDiameter, 1f, maxDiameter));
            Graphics.DrawMesh(MeshPool.plane10, mat, HeartOfAspirationMaterials.PulseAuraMat, 0, null, 0, props);
        }
    }

    /// <summary>渴望之心特效生成辅助工具。</summary>
    public static class HeartOfAspirationVfxUtility
    {
        public static void EnsureBearerAura(Pawn bearer, Thing toolEntity)
        {
            if (bearer == null || !bearer.Spawned || bearer.Map == null || toolEntity == null || HeartOfAspirationVfxDefOf.LC_HeartBearerAuraFX == null)
            {
                return;
            }

            var existing = bearer.Map.listerThings?.ThingsOfDef(HeartOfAspirationVfxDefOf.LC_HeartBearerAuraFX);
            if (existing != null)
            {
                for (int i = 0; i < existing.Count; i++)
                {
                    if (existing[i] is Thing_HeartBearerAura aura && !aura.Destroyed && aura.Bearer == bearer)
                    {
                        return;
                    }
                }
            }

            Thing_HeartBearerAura spawned = (Thing_HeartBearerAura)GenSpawn.Spawn(
                HeartOfAspirationVfxDefOf.LC_HeartBearerAuraFX, bearer.Position, bearer.Map);
            spawned.Initialize(bearer, toolEntity);
        }

        public static void SpawnPulseWave(Map map, Vector3 pos, float radius = 1.6f, int duration = 26, bool satisfied = false, bool restless = false)
        {
            if (map == null || HeartOfAspirationVfxDefOf.LC_HeartPulseWaveFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            Thing_HeartPulseWave wave = (Thing_HeartPulseWave)GenSpawn.Spawn(
                HeartOfAspirationVfxDefOf.LC_HeartPulseWaveFX, cell, map);
            Color col = satisfied
                ? new Color(1.0f, 0.48f, 0.22f, 1.0f)
                : (restless ? new Color(1.0f, 0.14f, 0.48f, 1.0f) : new Color(0.96f, 0.16f, 0.26f, 1.0f));
            Color core = satisfied
                ? new Color(1.0f, 0.94f, 0.72f, 1.0f)
                : new Color(1.0f, 0.84f, 0.86f, 1.0f);

            wave.Initialize(pos, radius, duration, col, core, restless ? 1.48f : 1.28f, mode: 1.0f);
        }

        public static void SpawnFrenzyBurst(Map map, Vector3 pos)
        {
            if (map == null || HeartOfAspirationVfxDefOf.LC_HeartPulseWaveFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            Thing_HeartPulseWave wave = (Thing_HeartPulseWave)GenSpawn.Spawn(
                HeartOfAspirationVfxDefOf.LC_HeartPulseWaveFX, cell, map);
            wave.Initialize(
                pos,
                radius: 3.8f,
                duration: 40,
                color: new Color(0.98f, 0.10f, 0.28f, 1.0f),
                core: new Color(1.0f, 0.92f, 0.94f, 1.0f),
                customIntensity: 1.72f,
                mode: 2.0f);

            FleckMaker.ThrowDustPuffThick(pos, map, 2.0f, new Color(0.88f, 0.10f, 0.20f));
            FleckMaker.ThrowDustPuffThick(pos, map, 1.35f, new Color(0.95f, 0.82f, 0.86f));
            FleckMaker.ThrowMicroSparks(pos, map);

            if (Find.CurrentMap == map && Find.CameraDriver != null)
            {
                Find.CameraDriver.shaker.DoShake(0.60f);
            }
        }
    }
}
