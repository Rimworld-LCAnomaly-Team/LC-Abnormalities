using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.BigBird
{
    /// <summary>大鸟与 E.G.O. 武器“目灯”专属自定义 GLSL 450 着色器与材质缓存。</summary>
    [StaticConstructorOnStartup]
    public static class BigBirdMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropRimColor = Shader.PropertyToID("_RimColor");
        public static readonly int PropEyeTint = Shader.PropertyToID("_EyeTint");
        public static readonly int PropVoidColor = Shader.PropertyToID("_VoidColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
        public static readonly int PropDanger = Shader.PropertyToID("_Danger");
        public static readonly int PropSlashAmt = Shader.PropertyToID("_SlashAmt");

        public static readonly Material BlackoutAuraMat;
        public static readonly Material LanternWaveMat;
        public static readonly Material EyeMarkMat;
        public static readonly Material BiteBurstMat;
        public static readonly Material LureBeamMat;

        static BigBirdMaterials()
        {
            Shader blackoutShader = ShaderDatabase.LoadShader("Map/LC_BigBirdBlackoutAura") ?? ShaderDatabase.Transparent;
            Shader waveShader = ShaderDatabase.LoadShader("Map/LC_BigBirdLanternWave") ?? ShaderDatabase.MoteGlow;
            Shader eyeMarkShader = ShaderDatabase.LoadShader("Map/LC_BigBirdEyeMark") ?? ShaderDatabase.Transparent;
            Shader biteShader = ShaderDatabase.LoadShader("Map/LC_BigBirdBiteBurst") ?? ShaderDatabase.Transparent;

            Texture2D eyesYellowTex = ContentFinder<Texture2D>.Get("Things/VFX/BigBird/BigBird_EyesYellow", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D eyesCrimsonTex = ContentFinder<Texture2D>.Get("Things/VFX/BigBird/BigBird_EyesCrimson", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D eyeSigilTex = ContentFinder<Texture2D>.Get("Things/VFX/BigBird/BigBird_EyeSigil", reportFailure: false) ?? BaseContent.WhiteTex;

            BlackoutAuraMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = eyesYellowTex,
                shader = blackoutShader,
                color = new Color(0.02f, 0.015f, 0.03f, 0.62f)
            });

            LanternWaveMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = waveShader,
                color = new Color(1.0f, 0.78f, 0.18f, 1.0f)
            });

            EyeMarkMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = eyeSigilTex,
                shader = eyeMarkShader,
                color = new Color(1.0f, 0.88f, 0.22f, 1.0f)
            });

            BiteBurstMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = eyesCrimsonTex,
                shader = biteShader,
                color = new Color(0.92f, 0.10f, 0.08f, 1.0f)
            });

            LureBeamMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = ShaderDatabase.MoteGlow,
                color = new Color(1.0f, 0.80f, 0.22f, 0.35f)
            });
        }
    }

    /// <summary>大鸟出逃期间随行的“部门大停电”黑森林暗域特效（中央明灯微光 + 暗影中隐现的金色眼眸）。</summary>
    public class Thing_BigBirdBlackoutAura : Thing
    {
        private Pawn owner;
        private int ageTicks;
        private float currentRadius = 9.5f;
        private MaterialPropertyBlock props;

        public void Initialize(Pawn bigBird, float radius = 11.5f)
        {
            owner = bigBird;
            currentRadius = Mathf.Clamp(radius, 6f, 18f);
            ageTicks = 0;
        }

        protected override void Tick()
        {
            ageTicks++;
            if (owner == null || owner.Destroyed || owner.Dead || !owner.Spawned || owner.Map != Map)
            {
                Destroy();
                return;
            }

            CompBigBird comp = (owner as LC_BigBirdPawn)?.BigBirdComp ?? owner.TryGetComp<CompBigBird>();
            if (comp == null || !comp.Escaped)
            {
                Destroy();
                return;
            }

            if (owner.Position.InBounds(Map) && Position != owner.Position)
            {
                Position = owner.Position;
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (owner == null || !owner.Spawned || BigBirdMaterials.BlackoutAuraMat == null)
            {
                return;
            }

            CompBigBird comp = (owner as LC_BigBirdPawn)?.BigBirdComp ?? owner.TryGetComp<CompBigBird>();
            float fadeIn = Mathf.Clamp01(ageTicks / 45f);
            float intensity = (comp != null && (comp.IsPerformingAttract || comp.IsPerformingDeadshot)) ? 1.55f : 1.0f;
            Color eyeTint = (comp != null && comp.IsPerformingDeadshot)
                ? new Color(0.96f, 0.18f, 0.10f, 1.0f)
                : new Color(1.0f, 0.84f, 0.24f, 1.0f);

            props ??= new MaterialPropertyBlock();
            props.SetColor(BigBirdMaterials.PropColor, new Color(0.02f, 0.015f, 0.03f, 0.56f));
            props.SetColor(BigBirdMaterials.PropEyeTint, eyeTint);
            props.SetFloat(BigBirdMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(BigBirdMaterials.PropAlpha, fadeIn);
            props.SetFloat(BigBirdMaterials.PropIntensity, intensity);

            Vector3 pos = owner.DrawPos.Yto0();
            float alt = AltitudeLayer.Blueprint.AltitudeFor() + 0.02f;
            float size = currentRadius * 2.0f;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(pos.x, alt, pos.z),
                Quaternion.identity,
                new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, mat, BigBirdMaterials.BlackoutAuraMat, 0, null, 0, props);
        }
    }

    /// <summary>大鸟摇灯魅惑 / 精神冲击的扩张金光脉冲波特效。</summary>
    public class Thing_BigBirdEnchantWave : Thing
    {
        private Vector3 centerPos;
        private float maxDiameter = 18f;
        private int durationTicks = 54;
        private int ageTicks;
        private Color waveColor = new Color(1.0f, 0.78f, 0.18f, 1.0f);
        private Color coreColor = new Color(1.0f, 0.96f, 0.78f, 1.0f);
        private float intensity = 1.45f;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 pos, float radius, int duration = 54, Color? customColor = null, float customIntensity = 1.45f)
        {
            centerPos = pos.Yto0();
            maxDiameter = Mathf.Max(2.2f, radius * 2.0f);
            durationTicks = Mathf.Max(16, duration);
            waveColor = customColor ?? new Color(1.0f, 0.78f, 0.18f, 1.0f);
            coreColor = new Color(1.0f, 0.96f, 0.78f, 1.0f);
            intensity = customIntensity;
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
            if (!initialized || BigBirdMaterials.LanternWaveMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float easeOut = 1f - Mathf.Pow(1f - t, 2.2f);
            float alpha = Mathf.Pow(1f - t, 1.25f);
            if (alpha <= 0.01f)
            {
                return;
            }

            props ??= new MaterialPropertyBlock();
            props.SetColor(BigBirdMaterials.PropColor, waveColor);
            props.SetColor(BigBirdMaterials.PropCoreColor, coreColor);
            props.SetFloat(BigBirdMaterials.PropProgress, easeOut);
            props.SetFloat(BigBirdMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(BigBirdMaterials.PropAlpha, alpha);
            props.SetFloat(BigBirdMaterials.PropIntensity, intensity);

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor();
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(centerPos.x, alt, centerPos.z),
                Quaternion.identity,
                new Vector3(maxDiameter, 1f, maxDiameter));
            Graphics.DrawMesh(MeshPool.plane10, mat, BigBirdMaterials.LanternWaveMat, 0, null, 0, props);
        }
    }

    /// <summary>
    /// 头顶大鸟同心圆眼眸标记特效：
    /// 1) 绑定于被魅惑者时，随距离/着魔程度由【金黄 → 橙红 → 猩红】渐变，并牵引细微金光丝线指向大鸟；
    /// 2) 绑定于目灯武器被动易伤目标时，呈现高危闪烁金眸印记。
    /// </summary>
    public class Thing_BigBirdEyeMark : Thing
    {
        private Pawn targetPawn;
        private Pawn sourceBigBird;
        private bool trackEnchantment;
        private int ageTicks;
        private int fixedDurationTicks = 90;
        private bool initialized;
        private MaterialPropertyBlock props;
        private MaterialPropertyBlock beamProps;

        public void InitializeEnchantMark(Pawn victim, Pawn bigBird)
        {
            targetPawn = victim;
            sourceBigBird = bigBird;
            trackEnchantment = true;
            ageTicks = 0;
            initialized = true;
        }

        public void InitializeLampMark(Pawn victim, int duration = 75)
        {
            targetPawn = victim;
            sourceBigBird = null;
            trackEnchantment = false;
            fixedDurationTicks = Mathf.Max(20, duration);
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || targetPawn == null || targetPawn.Destroyed || targetPawn.Dead || !targetPawn.Spawned || targetPawn.Map != Map)
            {
                Destroy();
                return;
            }

            ageTicks++;
            if (targetPawn.Position.InBounds(Map) && Position != targetPawn.Position)
            {
                Position = targetPawn.Position;
            }

            if (trackEnchantment)
            {
                bool hasHediff = BigBirdDefOf.LC_BigBirdEnchanted != null
                    && (targetPawn.health?.hediffSet?.HasHediff(BigBirdDefOf.LC_BigBirdEnchanted) ?? false);
                if (!hasHediff || sourceBigBird == null || sourceBigBird.Destroyed || sourceBigBird.Dead || !sourceBigBird.Spawned)
                {
                    Destroy();
                    return;
                }

                // Subtle golden hypnotic dust motes drifting off the entranced pawn
                if (ageTicks % 24 == 0)
                {
                    FleckMaker.ThrowDustPuffThick(targetPawn.DrawPos + new Vector3(0f, 0f, 0.35f), Map, 0.55f, new Color(1.0f, 0.82f, 0.25f));
                }
            }
            else if (ageTicks >= fixedDurationTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || targetPawn == null || !targetPawn.Spawned || BigBirdMaterials.EyeMarkMat == null)
            {
                return;
            }

            float ageSecs = ageTicks / 60f;
            float danger = 0f;
            float alpha = 1f;
            float size = 1.05f;

            if (trackEnchantment)
            {
                // Stage progression mimicking Lobotomy Corp Yellow -> Orange -> Red as victim nears Big Bird or time passes
                float dist = sourceBigBird != null ? targetPawn.Position.DistanceTo(sourceBigBird.Position) : 10f;
                float distDanger = Mathf.Clamp01(1f - (dist - 2.2f) / 10.5f);
                float timeDanger = Mathf.Clamp01(ageTicks / 320f);
                danger = Mathf.Clamp01(Mathf.Max(distDanger, timeDanger * 0.85f));
                alpha = Mathf.Clamp01(ageTicks / 14f);
                size = Mathf.Lerp(0.95f, 1.32f, danger);
            }
            else
            {
                float t = Mathf.Clamp01(ageTicks / (float)fixedDurationTicks);
                danger = 0.35f + 0.35f * Mathf.Sin(t * Mathf.PI);
                alpha = Mathf.Clamp01(ageTicks / 8f) * Mathf.Pow(1f - t, 1.35f);
                size = Mathf.Lerp(1.25f, 0.88f, t);
            }

            if (alpha <= 0.01f)
            {
                return;
            }

            // Yellow (1.0, 0.88, 0.22) -> Orange (1.0, 0.48, 0.10) -> Crimson (0.95, 0.12, 0.08)
            Color yellow = new Color(1.0f, 0.88f, 0.22f, 1.0f);
            Color orange = new Color(1.0f, 0.48f, 0.10f, 1.0f);
            Color crimson = new Color(0.95f, 0.12f, 0.08f, 1.0f);
            Color markCol = danger < 0.5f
                ? Color.Lerp(yellow, orange, danger * 2f)
                : Color.Lerp(orange, crimson, (danger - 0.5f) * 2f);
            Color rimCol = Color.Lerp(new Color(1.0f, 0.98f, 0.72f, 1.0f), new Color(1.0f, 0.45f, 0.35f, 1.0f), danger);

            Vector3 pawnPos = targetPawn.DrawPos.Yto0();
            float bobZ = 1.08f + 0.08f * Mathf.Sin(ageSecs * (3.6f + danger * 4.0f));
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.06f;

            props ??= new MaterialPropertyBlock();
            props.SetColor(BigBirdMaterials.PropColor, markCol);
            props.SetColor(BigBirdMaterials.PropRimColor, rimCol);
            props.SetFloat(BigBirdMaterials.PropAgeSecs, ageSecs);
            props.SetFloat(BigBirdMaterials.PropAlpha, alpha);
            props.SetFloat(BigBirdMaterials.PropDanger, danger);

            Matrix4x4 eyeMat = Matrix4x4.TRS(
                new Vector3(pawnPos.x, alt, pawnPos.z + bobZ),
                Quaternion.identity,
                new Vector3(size * 0.82f, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, eyeMat, BigBirdMaterials.EyeMarkMat, 0, null, 0, props);

            // Subtle golden-crimson hypnotic lure tether connecting Big Bird's lantern to the enchanted pawn
            if (trackEnchantment && sourceBigBird != null && sourceBigBird.Spawned && BigBirdMaterials.LureBeamMat != null)
            {
                Vector3 birdPos = sourceBigBird.DrawPos.Yto0() + new Vector3(0f, 0f, 0.25f);
                Vector3 victimHead = pawnPos + new Vector3(0f, 0f, 0.55f);
                Vector3 delta = victimHead - birdPos;
                float len = delta.magnitude;
                if (len > 0.6f)
                {
                    Vector3 mid = (birdPos + victimHead) * 0.5f;
                    Quaternion rot = Quaternion.LookRotation(delta / len, Vector3.up);
                    float pulseWidth = 0.09f + 0.04f * Mathf.Sin(ageSecs * 7.0f);

                    beamProps ??= new MaterialPropertyBlock();
                    Color tetherCol = markCol;
                    tetherCol.a = (0.24f + 0.16f * danger) * alpha;
                    beamProps.SetColor(BigBirdMaterials.PropColor, tetherCol);

                    Matrix4x4 beamMat = Matrix4x4.TRS(
                        new Vector3(mid.x, alt - 0.03f, mid.z),
                        rot,
                        new Vector3(pulseWidth, 1f, len));
                    Graphics.DrawMesh(MeshPool.plane10, beamMat, BigBirdMaterials.LureBeamMat, 0, null, 0, beamProps);
                }
            }
        }
    }

    /// <summary>大鸟咬头处决特效：蓄力压暗凝视 + 咬合瞬间的猩红群瞳黑域裂斩冲击（BigBirdDead.png 着色器）。</summary>
    public class Thing_BigBirdBiteBurst : Thing
    {
        private Vector3 exactPos;
        private float scale = 3.2f;
        private int ageTicks;
        private int durationTicks = 38;
        private bool isWindupOnly;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 pos, float burstScale = 3.2f, int duration = 38, bool windup = false)
        {
            exactPos = pos.Yto0();
            scale = Mathf.Max(1.4f, burstScale);
            durationTicks = Mathf.Max(14, duration);
            isWindupOnly = windup;
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
            if (!initialized || BigBirdMaterials.BiteBurstMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alpha = isWindupOnly
                ? Mathf.Sin(t * Mathf.PI) * 0.72f
                : Mathf.Pow(1f - t, 1.25f);
            if (alpha <= 0.01f)
            {
                return;
            }

            Color col = isWindupOnly
                ? new Color(1.0f, 0.58f, 0.14f, 1.0f)
                : new Color(0.95f, 0.12f, 0.08f, 1.0f);
            Color voidCol = isWindupOnly
                ? new Color(0.03f, 0.02f, 0.04f, 0.72f)
                : new Color(0.03f, 0.005f, 0.02f, 0.94f);

            props ??= new MaterialPropertyBlock();
            props.SetColor(BigBirdMaterials.PropColor, col);
            props.SetColor(BigBirdMaterials.PropVoidColor, voidCol);
            props.SetFloat(BigBirdMaterials.PropProgress, t);
            props.SetFloat(BigBirdMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(BigBirdMaterials.PropAlpha, alpha);
            props.SetFloat(BigBirdMaterials.PropSlashAmt, isWindupOnly ? 0.25f : 1.35f);

            float curSize = isWindupOnly
                ? scale * Mathf.Lerp(1.15f, 0.70f, t)
                : scale * Mathf.Lerp(0.75f, 1.35f, 1f - Mathf.Pow(1f - t, 2f));
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.identity,
                new Vector3(curSize, 1f, curSize));
            Graphics.DrawMesh(MeshPool.plane10, mat, BigBirdMaterials.BiteBurstMat, 0, null, 0, props);
        }
    }

    /// <summary>E.G.O. 武器“目灯”重击特效（BigBirdWeaponEffect）：黑金侵蚀裂隙 + 2 格黄金冲击波环。</summary>
    public class Thing_LampImpactVFX : Thing
    {
        private Vector3 exactPos;
        private float splashRadius = 2.2f;
        private bool vulnerabilityProc;
        private int ageTicks;
        private const int DurationTicks = 30;
        private bool initialized;
        private MaterialPropertyBlock waveProps;
        private MaterialPropertyBlock riftProps;

        public void Initialize(Vector3 pos, float radius = 2.2f, bool proc = false)
        {
            exactPos = pos.Yto0();
            splashRadius = Mathf.Max(1.4f, radius);
            vulnerabilityProc = proc;
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
            float easeOut = 1f - Mathf.Pow(1f - t, 2.3f);
            float alpha = Mathf.Pow(1f - t, 1.35f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float ageSecs = ageTicks / 60f;
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f;

            // Pass 1: Obsidian Black-damage dark-gold impact burst underneath
            if (BigBirdMaterials.BiteBurstMat != null)
            {
                riftProps ??= new MaterialPropertyBlock();
                riftProps.SetColor(BigBirdMaterials.PropColor, vulnerabilityProc
                    ? new Color(1.0f, 0.72f, 0.16f, 1.0f)
                    : new Color(0.72f, 0.45f, 0.95f, 1.0f));
                riftProps.SetColor(BigBirdMaterials.PropVoidColor, new Color(0.05f, 0.02f, 0.08f, 0.84f));
                riftProps.SetFloat(BigBirdMaterials.PropProgress, easeOut);
                riftProps.SetFloat(BigBirdMaterials.PropAgeSecs, ageSecs);
                riftProps.SetFloat(BigBirdMaterials.PropAlpha, alpha * 0.85f);
                riftProps.SetFloat(BigBirdMaterials.PropSlashAmt, vulnerabilityProc ? 1.1f : 0.75f);

                float darkSz = splashRadius * 1.45f * Mathf.Lerp(0.55f, 1.15f, easeOut);
                Matrix4x4 darkMat = Matrix4x4.TRS(
                    new Vector3(exactPos.x, alt - 0.01f, exactPos.z),
                    Quaternion.identity,
                    new Vector3(darkSz, 1f, darkSz));
                Graphics.DrawMesh(MeshPool.plane10, darkMat, BigBirdMaterials.BiteBurstMat, 0, null, 0, riftProps);
            }

            // Pass 2: Blazing golden-amber 2-cell lantern shockwave ring
            if (BigBirdMaterials.LanternWaveMat != null)
            {
                waveProps ??= new MaterialPropertyBlock();
                waveProps.SetColor(BigBirdMaterials.PropColor, vulnerabilityProc
                    ? new Color(1.0f, 0.84f, 0.20f, 1.0f)
                    : new Color(0.98f, 0.72f, 0.22f, 1.0f));
                waveProps.SetColor(BigBirdMaterials.PropCoreColor, new Color(1.0f, 0.97f, 0.82f, 1.0f));
                waveProps.SetFloat(BigBirdMaterials.PropProgress, easeOut);
                waveProps.SetFloat(BigBirdMaterials.PropAgeSecs, ageSecs);
                waveProps.SetFloat(BigBirdMaterials.PropAlpha, alpha);
                waveProps.SetFloat(BigBirdMaterials.PropIntensity, vulnerabilityProc ? 1.75f : 1.35f);

                float waveSz = splashRadius * 2.15f;
                Matrix4x4 waveMat = Matrix4x4.TRS(
                    new Vector3(exactPos.x, alt + 0.01f, exactPos.z),
                    Quaternion.identity,
                    new Vector3(waveSz, 1f, waveSz));
                Graphics.DrawMesh(MeshPool.plane10, waveMat, BigBirdMaterials.LanternWaveMat, 0, null, 0, waveProps);
            }
        }
    }

    /// <summary>大鸟与目灯 VFX 生成辅助工具。</summary>
    public static class BigBirdVfxUtility
    {
        public static Thing_BigBirdBlackoutAura SpawnBlackoutAura(Pawn bigBird, float radius = 11.5f)
        {
            if (bigBird == null || !bigBird.Spawned || bigBird.Map == null || BigBirdDefOf.LC_BigBirdBlackoutAuraFX == null)
            {
                return null;
            }

            Thing_BigBirdBlackoutAura aura = (Thing_BigBirdBlackoutAura)GenSpawn.Spawn(
                BigBirdDefOf.LC_BigBirdBlackoutAuraFX, bigBird.Position, bigBird.Map);
            aura.Initialize(bigBird, radius);
            return aura;
        }

        public static void SpawnEnchantWave(Map map, Vector3 pos, float radius, int duration = 56, Color? color = null, float intensity = 1.45f)
        {
            if (map == null || BigBirdDefOf.LC_BigBirdEnchantWaveFX == null)
            {
                return;
            }

            IntVec3 anchor = pos.ToIntVec3().ClampInsideMap(map);
            Thing_BigBirdEnchantWave wave = (Thing_BigBirdEnchantWave)GenSpawn.Spawn(
                BigBirdDefOf.LC_BigBirdEnchantWaveFX, anchor, map);
            wave.Initialize(pos, radius, duration, color, intensity);

            FleckMaker.ThrowDustPuffThick(pos, map, 2.2f, new Color(1.0f, 0.84f, 0.24f));
            FleckMaker.ThrowMicroSparks(pos, map);
        }

        public static Thing_BigBirdEyeMark SpawnEnchantEyeMark(Pawn victim, Pawn bigBird)
        {
            if (victim == null || !victim.Spawned || victim.Map == null || BigBirdDefOf.LC_BigBirdEyeMarkFX == null)
            {
                return null;
            }

            Thing_BigBirdEyeMark mark = (Thing_BigBirdEyeMark)GenSpawn.Spawn(
                BigBirdDefOf.LC_BigBirdEyeMarkFX, victim.Position, victim.Map);
            mark.InitializeEnchantMark(victim, bigBird);
            return mark;
        }

        public static void SpawnLampEyeMark(Pawn victim, int duration = 75)
        {
            if (victim == null || !victim.Spawned || victim.Map == null || BigBirdDefOf.LC_BigBirdEyeMarkFX == null)
            {
                return;
            }

            Thing_BigBirdEyeMark mark = (Thing_BigBirdEyeMark)GenSpawn.Spawn(
                BigBirdDefOf.LC_BigBirdEyeMarkFX, victim.Position, victim.Map);
            mark.InitializeLampMark(victim, duration);
        }

        public static void SpawnBiteBurst(Map map, Vector3 pos, float scale = 3.2f, int duration = 38, bool windup = false)
        {
            if (map == null || BigBirdDefOf.LC_BigBirdBiteBurstFX == null)
            {
                return;
            }

            IntVec3 anchor = pos.ToIntVec3().ClampInsideMap(map);
            Thing_BigBirdBiteBurst burst = (Thing_BigBirdBiteBurst)GenSpawn.Spawn(
                BigBirdDefOf.LC_BigBirdBiteBurstFX, anchor, map);
            burst.Initialize(pos, scale, duration, windup);

            if (!windup)
            {
                FleckMaker.ThrowDustPuffThick(pos, map, 2.0f, new Color(0.18f, 0.04f, 0.08f));
                FleckMaker.ThrowDustPuffThick(pos, map, 1.4f, new Color(0.75f, 0.08f, 0.08f));
                FleckMaker.ThrowMicroSparks(pos, map);
                if (Find.CurrentMap == map && Find.CameraDriver != null)
                {
                    Find.CameraDriver.shaker.DoShake(0.65f);
                }
            }
        }

        public static void SpawnLampImpact(Map map, Vector3 pos, float radius = 2.2f, bool proc = false)
        {
            if (map == null || BigBirdDefOf.LC_LampImpactFX == null)
            {
                return;
            }

            IntVec3 anchor = pos.ToIntVec3().ClampInsideMap(map);
            Thing_LampImpactVFX fx = (Thing_LampImpactVFX)GenSpawn.Spawn(
                BigBirdDefOf.LC_LampImpactFX, anchor, map);
            fx.Initialize(pos, radius, proc);

            FleckMaker.ThrowDustPuffThick(pos, map, proc ? 1.85f : 1.35f, new Color(0.14f, 0.08f, 0.18f));
            FleckMaker.ThrowDustPuffThick(pos, map, proc ? 1.60f : 1.10f, new Color(1.0f, 0.80f, 0.22f));
            FleckMaker.ThrowMicroSparks(pos, map);
        }
    }
}
