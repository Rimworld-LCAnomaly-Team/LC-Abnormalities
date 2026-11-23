using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.FairyFestival
{
    /// <summary>精灵盛宴与 E.G.O. 武器“振翅”专属自定义 GLSL 450 着色器与解包精灵贴图缓存。</summary>
    [StaticConstructorOnStartup]
    public static class FairyFestivalMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropMintColor = Shader.PropertyToID("_MintColor");
        public static readonly int PropVoidColor = Shader.PropertyToID("_VoidColor");
        public static readonly int PropWingColor = Shader.PropertyToID("_WingColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");
        public static readonly int PropFrenzy = Shader.PropertyToID("_Frenzy");
        public static readonly int PropArcSpan = Shader.PropertyToID("_ArcSpan");

        public static readonly Material BlessingAuraMat;
        public static readonly Material DevourFeastMat;
        public static readonly Material WingSlashMat;

        public static readonly Material LightOrbMat;
        public static readonly Material RedOrbMat;
        public static readonly Material SiphonBeamMat;

        public static readonly Material[] MiniFairyMats;
        public static readonly Material[] MiniFairyCrimsonMats;

        static FairyFestivalMaterials()
        {
            Shader blessingShader = ShaderDatabase.LoadShader("Map/LC_FairyBlessingAura") ?? ShaderDatabase.MoteGlow;
            Shader devourShader = ShaderDatabase.LoadShader("Map/LC_FairyDevourFeast") ?? ShaderDatabase.Transparent;
            Shader slashShader = ShaderDatabase.LoadShader("Map/LC_FairyWingSlash") ?? ShaderDatabase.MoteGlow;

            Texture2D goreMarkTex = ContentFinder<Texture2D>.Get("Things/VFX/FairyFestival/FairyEat_GoreMark", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D lightOrbTex = ContentFinder<Texture2D>.Get("Things/VFX/FairyFestival/FairyLightOrb", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D redOrbTex = ContentFinder<Texture2D>.Get("Things/VFX/FairyFestival/FairyEat_RedOrb", reportFailure: false) ?? BaseContent.WhiteTex;

            BlessingAuraMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = blessingShader,
                color = new Color(0.32f, 0.98f, 0.72f, 1.0f)
            });

            DevourFeastMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = goreMarkTex,
                shader = devourShader,
                color = new Color(0.92f, 0.08f, 0.12f, 1.0f)
            });

            WingSlashMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = slashShader,
                color = new Color(0.96f, 0.20f, 0.28f, 1.0f)
            });

            LightOrbMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = lightOrbTex,
                shader = ShaderDatabase.MoteGlow,
                color = new Color(0.55f, 1.0f, 0.82f, 0.85f)
            });

            RedOrbMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = redOrbTex,
                shader = ShaderDatabase.MoteGlow,
                color = new Color(1.0f, 0.24f, 0.22f, 0.95f)
            });

            SiphonBeamMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = ShaderDatabase.MoteGlow,
                color = new Color(0.45f, 1.0f, 0.78f, 0.75f)
            });

            MiniFairyMats = new Material[8];
            MiniFairyCrimsonMats = new Material[8];
            for (int i = 0; i < 8; i++)
            {
                Texture2D frameTex = ContentFinder<Texture2D>.Get($"Things/VFX/FairyFestival/FairyMini_{i}", reportFailure: false) ?? BaseContent.WhiteTex;
                MiniFairyMats[i] = MaterialPool.MatFrom(new MaterialRequest
                {
                    mainTex = frameTex,
                    shader = ShaderDatabase.Transparent,
                    color = Color.white
                });
                MiniFairyCrimsonMats[i] = MaterialPool.MatFrom(new MaterialRequest
                {
                    mainTex = frameTex,
                    shader = ShaderDatabase.Transparent,
                    color = new Color(1.0f, 0.30f, 0.28f, 1.0f)
                });
            }
        }
    }

    /// <summary>
    /// “精灵的照料”随行护卫小精灵群特效（还原解包 Fairy_Effect_0..7 振翅动画）：
    /// 3 只散发苍白翠绿微光的小精灵围绕受照料的殖民者盘旋飞舞，并在脚下泛起柔和的荧光花粉光环。
    /// </summary>
    public class Thing_FairyCareSwarm : Thing
    {
        private Pawn hostPawn;
        private int ageTicks;
        private bool initialized;
        private MaterialPropertyBlock auraProps;
        private MaterialPropertyBlock fairyProps;

        public Pawn HostPawn => hostPawn;

        public void Initialize(Pawn host)
        {
            hostPawn = host;
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || hostPawn == null || hostPawn.Destroyed || hostPawn.Dead || !hostPawn.Spawned || hostPawn.Map != Map)
            {
                Destroy();
                return;
            }

            bool hasCare = FairyFestivalDefOf.LC_FairyCare != null
                && (hostPawn.health?.hediffSet?.HasHediff(FairyFestivalDefOf.LC_FairyCare) ?? false);
            if (!hasCare)
            {
                Destroy();
                return;
            }

            ageTicks++;
            if (hostPawn.Position.InBounds(Map) && Position != hostPawn.Position)
            {
                Position = hostPawn.Position;
            }

            // Gentle drifting emerald fairy dust motes
            if (ageTicks % 65 == 0)
            {
                Vector3 pos = hostPawn.DrawPos + new Vector3(Rand.Range(-0.45f, 0.45f), 0f, Rand.Range(-0.15f, 0.55f));
                FleckMaker.ThrowDustPuffThick(pos, Map, 0.48f, new Color(0.45f, 1.0f, 0.80f));
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || hostPawn == null || !hostPawn.Spawned)
            {
                return;
            }

            float ageSecs = ageTicks / 60f;
            float fadeIn = Mathf.Clamp01(ageTicks / 24f);
            Vector3 center = hostPawn.DrawPos.Yto0();

            // 1. Soft bioluminescent mint-emerald care mist beneath/around the blessed colonist
            if (FairyFestivalMaterials.BlessingAuraMat != null)
            {
                auraProps ??= new MaterialPropertyBlock();
                auraProps.SetColor(FairyFestivalMaterials.PropColor, new Color(0.32f, 0.98f, 0.72f, 1.0f));
                auraProps.SetColor(FairyFestivalMaterials.PropCoreColor, new Color(0.86f, 1.0f, 0.94f, 1.0f));
                auraProps.SetFloat(FairyFestivalMaterials.PropProgress, 0.5f);
                auraProps.SetFloat(FairyFestivalMaterials.PropAgeSecs, ageSecs);
                auraProps.SetFloat(FairyFestivalMaterials.PropAlpha, fadeIn * 0.46f);
                auraProps.SetFloat(FairyFestivalMaterials.PropMode, 0.0f);

                float groundAlt = AltitudeLayer.Blueprint.AltitudeFor() + 0.03f;
                Matrix4x4 auraMat = Matrix4x4.TRS(
                    new Vector3(center.x, groundAlt, center.z),
                    Quaternion.identity,
                    new Vector3(1.85f, 1f, 1.85f));
                Graphics.DrawMesh(MeshPool.plane10, auraMat, FairyFestivalMaterials.BlessingAuraMat, 0, null, 0, auraProps);
            }

            // 2. Three animated wing-flapping mini fairies orbiting the host pawn (Fairy_Effect_0..7)
            float baseAlt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f;
            fairyProps ??= new MaterialPropertyBlock();
            fairyProps.SetColor(FairyFestivalMaterials.PropColor, new Color(1f, 1f, 1f, fadeIn));

            for (int i = 0; i < 3; i++)
            {
                float phase = ageSecs * (1.75f + i * 0.28f) + i * 2.094395f;
                float radiusX = 0.62f + 0.10f * Mathf.Sin(ageSecs * 1.4f + i);
                float radiusZ = 0.34f + 0.06f * Mathf.Cos(ageSecs * 1.9f + i * 1.3f);
                float ox = Mathf.Cos(phase) * radiusX;
                float oz = Mathf.Sin(phase) * radiusZ + 0.28f + 0.08f * Mathf.Sin(ageSecs * 4.2f + i * 1.7f);
                float depthBias = Mathf.Sin(phase) < 0f ? 0.02f : -0.02f;
                float miniScale = (0.44f + 0.05f * Mathf.Sin(ageSecs * 3.1f + i)) * fadeIn;
                float tilt = -Mathf.Sin(phase) * 10f;

                int frameIdx = ((ageTicks / 3) + i * 3) % 8;
                Material spriteMat = FairyFestivalMaterials.MiniFairyMats[frameIdx];

                Vector3 fairyPos = new Vector3(center.x + ox, baseAlt + depthBias, center.z + oz);

                // Soft glowing halo orb behind each mini fairy
                if (FairyFestivalMaterials.LightOrbMat != null)
                {
                    Matrix4x4 orbMat = Matrix4x4.TRS(
                        new Vector3(fairyPos.x, fairyPos.y - 0.005f, fairyPos.z),
                        Quaternion.identity,
                        new Vector3(miniScale * 1.55f, 1f, miniScale * 1.55f));
                    Graphics.DrawMesh(MeshPool.plane10, orbMat, FairyFestivalMaterials.LightOrbMat, 0, null, 0, fairyProps);
                }

                if (spriteMat != null)
                {
                    Matrix4x4 fMat = Matrix4x4.TRS(
                        fairyPos,
                        Quaternion.AngleAxis(tilt, Vector3.up),
                        new Vector3(miniScale, 1f, miniScale));
                    Graphics.DrawMesh(MeshPool.plane10, fMat, spriteMat, 0, null, 0, fairyProps);
                }
            }
        }
    }

    /// <summary>
    /// 精灵祝福脉冲与护卫小精灵派遣演出：
    /// 1) 赋予照料时从精灵盛宴本体飞出 3 只小精灵奔赴受试者，并在目标处绽放翠绿荧光波环；
    /// 2) 周期性疗愈触发时在殖民者周身泛起轻盈的翡翠疗愈光晕。
    /// </summary>
    public class Thing_FairyBlessingBurst : Thing
    {
        private Vector3 startPos;
        private Vector3 targetPos;
        private bool hasDispatchFlight;
        private float maxDiameter = 2.6f;
        private int durationTicks = 44;
        private int ageTicks;
        private bool initialized;
        private MaterialPropertyBlock waveProps;
        private MaterialPropertyBlock fairyProps;

        public void InitializePulse(Vector3 center, float radius = 1.4f, int duration = 36)
        {
            startPos = center.Yto0();
            targetPos = center.Yto0();
            hasDispatchFlight = false;
            maxDiameter = Mathf.Max(1.6f, radius * 2.0f);
            durationTicks = Mathf.Max(18, duration);
            ageTicks = 0;
            initialized = true;
        }

        public void InitializeDispatch(Vector3 from, Vector3 to, float radius = 1.85f, int duration = 52)
        {
            startPos = from.Yto0();
            targetPos = to.Yto0();
            hasDispatchFlight = (to - from).Yto0().sqrMagnitude > 0.25f;
            maxDiameter = Mathf.Max(2.0f, radius * 2.0f);
            durationTicks = Mathf.Max(24, duration);
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
            if (!initialized)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float easeOut = 1f - Mathf.Pow(1f - t, 2.2f);
            float alpha = Mathf.Pow(1f - t, 1.15f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float ageSecs = ageTicks / 60f;
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;

            // 1. Expanding bioluminescent mint-emerald blessing pulse ring
            if (FairyFestivalMaterials.BlessingAuraMat != null)
            {
                waveProps ??= new MaterialPropertyBlock();
                waveProps.SetColor(FairyFestivalMaterials.PropColor, new Color(0.28f, 0.98f, 0.70f, 1.0f));
                waveProps.SetColor(FairyFestivalMaterials.PropCoreColor, new Color(0.90f, 1.0f, 0.96f, 1.0f));
                waveProps.SetFloat(FairyFestivalMaterials.PropProgress, easeOut);
                waveProps.SetFloat(FairyFestivalMaterials.PropAgeSecs, ageSecs);
                waveProps.SetFloat(FairyFestivalMaterials.PropAlpha, alpha * 0.95f);
                waveProps.SetFloat(FairyFestivalMaterials.PropMode, 1.0f);

                Matrix4x4 mat = Matrix4x4.TRS(
                    new Vector3(targetPos.x, alt, targetPos.z),
                    Quaternion.identity,
                    new Vector3(maxDiameter, 1f, maxDiameter));
                Graphics.DrawMesh(MeshPool.plane10, mat, FairyFestivalMaterials.BlessingAuraMat, 0, null, 0, waveProps);
            }

            // 2. Three tiny glowing fairies darting along curved arcs from Fairy Festival to the blessed worker
            if (hasDispatchFlight && t < 0.85f)
            {
                float flightT = Mathf.Clamp01(t / 0.78f);
                float smoothT = Mathf.SmoothStep(0f, 1f, flightT);
                float flightAlpha = flightT < 0.75f ? 1f : Mathf.Clamp01(1f - (flightT - 0.75f) / 0.25f);
                Vector3 delta = targetPos - startPos;
                Vector3 perp = new Vector3(-delta.z, 0f, delta.x).normalized;

                fairyProps ??= new MaterialPropertyBlock();
                fairyProps.SetColor(FairyFestivalMaterials.PropColor, new Color(1f, 1f, 1f, flightAlpha));

                for (int i = 0; i < 3; i++)
                {
                    float curveOffset = (i - 1) * 0.72f * Mathf.Sin(smoothT * Mathf.PI);
                    float liftZ = 0.25f + 0.35f * Mathf.Sin(smoothT * Mathf.PI + i * 0.7f);
                    Vector3 pos = Vector3.Lerp(startPos, targetPos, smoothT) + perp * curveOffset;
                    pos.z += liftZ;

                    int frameIdx = ((ageTicks / 2) + i * 3) % 8;
                    Material spriteMat = FairyFestivalMaterials.MiniFairyMats[frameIdx];
                    float sz = 0.50f;

                    if (FairyFestivalMaterials.LightOrbMat != null)
                    {
                        Matrix4x4 orbMat = Matrix4x4.TRS(
                            new Vector3(pos.x, alt + 0.01f, pos.z),
                            Quaternion.identity,
                            new Vector3(sz * 1.7f, 1f, sz * 1.7f));
                        Graphics.DrawMesh(MeshPool.plane10, orbMat, FairyFestivalMaterials.LightOrbMat, 0, null, 0, fairyProps);
                    }

                    if (spriteMat != null)
                    {
                        Matrix4x4 fMat = Matrix4x4.TRS(
                            new Vector3(pos.x, alt + 0.02f, pos.z),
                            Quaternion.identity,
                            new Vector3(sz, 1f, sz));
                        Graphics.DrawMesh(MeshPool.plane10, fMat, spriteMat, 0, null, 0, fairyProps);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 精灵盛宴嫉妒分食处决演出（还原解包 fairyAgentDead 死亡动画）：
    /// 阶段 1（0~22t）：环绕小精灵由翠绿骤然转为嗜血猩红并向受害者扑拢；
    /// 阶段 2（22~118t）：自定义 GLSL 血雾漩涡 + 撕咬利爪闪光 + 4 只染血小精灵高频扑咬 + 红光球爆闪 + 血迹飞溅；
    /// 阶段 3（118~150t）：饱餐消散。
    /// </summary>
    public class Thing_FairyDevourExecution : Thing
    {
        private Vector3 exactPos;
        private int ageTicks;
        private const int DurationTicks = 150;
        private bool initialized;
        private MaterialPropertyBlock feastProps;
        private MaterialPropertyBlock spriteProps;

        public void Initialize(Vector3 pos)
        {
            exactPos = pos.Yto0();
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

            // Periodic bloody chomp bursts & gore flecks during the feeding frenzy (ticks 20..115)
            if (Map != null && ageTicks >= 20 && ageTicks <= 115 && (ageTicks % 18 == 2))
            {
                IntVec3 cell = exactPos.ToIntVec3().ClampInsideMap(Map);
                Vector3 jitter = new Vector3(Rand.Range(-0.45f, 0.45f), 0f, Rand.Range(-0.35f, 0.45f));
                FleckMaker.ThrowDustPuffThick(exactPos + jitter, Map, 1.35f, new Color(0.75f, 0.06f, 0.09f));
                FleckMaker.ThrowMicroSparks(exactPos + jitter * 0.6f, Map);

                FairyFestivalVfxUtility.SpawnWingbeatSlash(
                    Map,
                    exactPos + jitter * 0.35f,
                    Rand.Range(0f, 360f),
                    scale: Rand.Range(1.35f, 1.85f),
                    flipSweep: (ageTicks % 36) == 2,
                    crimsonFeastMode: true,
                    durationTicks: 14);

                if (cell.InBounds(Map))
                {
                    IntVec3 bloodCell = (cell + GenRadial.RadialPattern[Rand.Range(0, 9)]).ClampInsideMap(Map);
                    if (bloodCell.InBounds(Map) && RimWorld.ThingDefOf.Filth_Blood != null)
                    {
                        FilthMaker.TryMakeFilth(bloodCell, Map, RimWorld.ThingDefOf.Filth_Blood);
                    }
                }

                if (Find.CurrentMap == Map && Find.CameraDriver != null)
                {
                    Find.CameraDriver.shaker.DoShake(0.32f);
                }
            }

            if (ageTicks >= DurationTicks)
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
            float fadeIn = Mathf.Clamp01(ageTicks / 12f);
            float fadeOut = t > 0.74f ? Mathf.Clamp01(1f - (t - 0.74f) / 0.26f) : 1f;
            float alpha = fadeIn * fadeOut;
            if (alpha <= 0.01f)
            {
                return;
            }

            float frenzy = (ageTicks >= 18 && ageTicks <= 120) ? 1.25f : 0.45f;
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;

            // 1. Swirling crimson-mint predatory feast vortex & gore mark shader
            if (FairyFestivalMaterials.DevourFeastMat != null)
            {
                feastProps ??= new MaterialPropertyBlock();
                feastProps.SetColor(FairyFestivalMaterials.PropColor, new Color(0.94f, 0.08f, 0.12f, 1.0f));
                feastProps.SetColor(FairyFestivalMaterials.PropMintColor, new Color(0.30f, 0.98f, 0.68f, 1.0f));
                feastProps.SetColor(FairyFestivalMaterials.PropVoidColor, new Color(0.11f, 0.01f, 0.02f, 0.90f));
                feastProps.SetFloat(FairyFestivalMaterials.PropProgress, t);
                feastProps.SetFloat(FairyFestivalMaterials.PropAgeSecs, ageSecs);
                feastProps.SetFloat(FairyFestivalMaterials.PropAlpha, alpha);
                feastProps.SetFloat(FairyFestivalMaterials.PropFrenzy, frenzy);

                float vortexSz = Mathf.Lerp(1.65f, 2.85f, 1f - Mathf.Pow(1f - t, 2f));
                Matrix4x4 feastMat = Matrix4x4.TRS(
                    new Vector3(exactPos.x, alt - 0.01f, exactPos.z),
                    Quaternion.identity,
                    new Vector3(vortexSz, 1f, vortexSz));
                Graphics.DrawMesh(MeshPool.plane10, feastMat, FairyFestivalMaterials.DevourFeastMat, 0, null, 0, feastProps);
            }

            // 2. Four blood-stained devouring mini fairies + glowing crimson orbs (effect_eating/light)
            bool isCrimson = ageTicks >= 16;
            float converge = Mathf.Clamp01(ageTicks / 24f);
            spriteProps ??= new MaterialPropertyBlock();
            spriteProps.SetColor(FairyFestivalMaterials.PropColor, new Color(1f, 1f, 1f, alpha));

            for (int i = 0; i < 4; i++)
            {
                float speed = isCrimson ? (7.5f + i * 1.1f) : 2.6f;
                float ang = ageSecs * speed + i * 1.570796f;
                float lunge = isCrimson ? (0.18f + 0.36f * Mathf.Abs(Mathf.Sin(ageSecs * (9.5f + i * 1.3f) + i))) : Mathf.Lerp(0.85f, 0.32f, converge);
                float ox = Mathf.Cos(ang) * lunge;
                float oz = Mathf.Sin(ang) * lunge * 0.72f + 0.16f + 0.14f * Mathf.Sin(ageSecs * 11f + i * 2.1f);
                Vector3 fPos = new Vector3(exactPos.x + ox, alt + 0.02f + i * 0.004f, exactPos.z + oz);

                // Red devouring orb flare (matching fairyAgentDead effect_eating/light)
                if (isCrimson && FairyFestivalMaterials.RedOrbMat != null)
                {
                    float orbPulse = 0.62f + 0.32f * Mathf.Sin(ageSecs * 14f + i * 1.6f);
                    Matrix4x4 orbMat = Matrix4x4.TRS(
                        new Vector3(fPos.x, fPos.y - 0.006f, fPos.z),
                        Quaternion.identity,
                        new Vector3(orbPulse, 1f, orbPulse));
                    Graphics.DrawMesh(MeshPool.plane10, orbMat, FairyFestivalMaterials.RedOrbMat, 0, null, 0, spriteProps);
                }

                int frameIdx = ((ageTicks / 2) + i * 2) % 8;
                Material mat = isCrimson
                    ? FairyFestivalMaterials.MiniFairyCrimsonMats[frameIdx]
                    : FairyFestivalMaterials.MiniFairyMats[frameIdx];
                if (mat != null)
                {
                    float chompScale = isCrimson
                        ? (0.54f + 0.10f * Mathf.Sin(ageSecs * 18f + i))
                        : 0.46f;
                    float tilt = Mathf.Sin(ageSecs * 15f + i * 1.7f) * 22f;
                    Matrix4x4 fMat = Matrix4x4.TRS(
                        fPos,
                        Quaternion.AngleAxis(tilt, Vector3.up),
                        new Vector3(chompScale, 1f, chompScale));
                    Graphics.DrawMesh(MeshPool.plane10, fMat, mat, 0, null, 0, spriteProps);
                }
            }
        }
    }

    /// <summary>
    /// E.G.O. 武器“振翅（Wingbeat）”双段交替斩击特效 &amp; 精灵盛宴啃咬弧光：
    /// 结合红伤血刃前缘与荧光薄荷绿精灵羽翼脉络弧尾，并伴随轻盈的星尘闪光。
    /// </summary>
    public class Thing_WingbeatSlash : Thing
    {
        private Vector3 exactPos;
        private float yawAngleDeg;
        private float scale = 1.65f;
        private bool flipSweep;
        private bool crimsonFeastMode;
        private int durationTicks = 16;
        private int ageTicks;
        private bool initialized;
        private MaterialPropertyBlock slashProps;
        private MaterialPropertyBlock orbProps;

        public void Initialize(Vector3 pos, float yawDeg, float slashScale = 1.65f, bool flip = false, bool crimsonMode = false, int duration = 16)
        {
            exactPos = pos.Yto0();
            yawAngleDeg = yawDeg;
            scale = Mathf.Max(0.8f, slashScale);
            flipSweep = flip;
            crimsonFeastMode = crimsonMode;
            durationTicks = Mathf.Max(10, duration);
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
            if (!initialized || FairyFestivalMaterials.WingSlashMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alpha = Mathf.Pow(1f - t, 0.85f);
            if (alpha <= 0.01f)
            {
                return;
            }

            Color fangCol = crimsonFeastMode
                ? new Color(0.98f, 0.08f, 0.14f, 1.0f)
                : new Color(0.96f, 0.22f, 0.30f, 1.0f);
            Color wingCol = crimsonFeastMode
                ? new Color(0.92f, 0.32f, 0.36f, 1.0f)
                : new Color(0.30f, 0.98f, 0.74f, 1.0f);
            Color coreCol = crimsonFeastMode
                ? new Color(1.0f, 0.86f, 0.86f, 1.0f)
                : new Color(0.95f, 1.0f, 0.96f, 1.0f);

            slashProps ??= new MaterialPropertyBlock();
            slashProps.SetColor(FairyFestivalMaterials.PropColor, fangCol);
            slashProps.SetColor(FairyFestivalMaterials.PropWingColor, wingCol);
            slashProps.SetColor(FairyFestivalMaterials.PropCoreColor, coreCol);
            slashProps.SetFloat(FairyFestivalMaterials.PropProgress, t);
            slashProps.SetFloat(FairyFestivalMaterials.PropAgeSecs, ageTicks / 60f);
            slashProps.SetFloat(FairyFestivalMaterials.PropAlpha, alpha);
            slashProps.SetFloat(FairyFestivalMaterials.PropArcSpan, crimsonFeastMode ? 2.05f : 2.35f);

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.06f;
            float sz = scale * Mathf.Lerp(0.88f, 1.14f, t);
            float zScale = flipSweep ? -sz : sz;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.AngleAxis(yawAngleDeg, Vector3.up),
                new Vector3(sz, 1f, zScale));
            Graphics.DrawMesh(MeshPool.plane10, mat, FairyFestivalMaterials.WingSlashMat, 0, null, 0, slashProps);

            // Luminous emerald wing-spark glint at impact core
            if (!crimsonFeastMode && FairyFestivalMaterials.LightOrbMat != null && t < 0.65f)
            {
                float glintA = (1f - t / 0.65f) * 0.85f;
                orbProps ??= new MaterialPropertyBlock();
                orbProps.SetColor(FairyFestivalMaterials.PropColor, new Color(0.55f, 1.0f, 0.82f, glintA));
                float orbSz = scale * Mathf.Lerp(0.45f, 0.90f, t);
                Matrix4x4 orbMat = Matrix4x4.TRS(
                    new Vector3(exactPos.x, alt + 0.01f, exactPos.z),
                    Quaternion.identity,
                    new Vector3(orbSz, 1f, orbSz));
                Graphics.DrawMesh(MeshPool.plane10, orbMat, FairyFestivalMaterials.LightOrbMat, 0, null, 0, orbProps);
            }
        }
    }

    /// <summary>
    /// E.G.O. 武器“振翅（Wingbeat）”吸血回复视觉特效：
    /// 命中血肉目标后，一道由猩红渐变为翠绿的荧光生命丝带与小精灵光球沿弧线飞回挥舞者体内，并绽放柔和疗愈光环。
    /// </summary>
    public class Thing_WingbeatLifesteal : Thing
    {
        private Vector3 startPos;
        private Pawn recipient;
        private Vector3 lastRecipientPos;
        private float arcSign = 1f;
        private int ageTicks;
        private const int DurationTicks = 24;
        private bool initialized;
        private MaterialPropertyBlock orbProps;
        private MaterialPropertyBlock auraProps;

        public void Initialize(Vector3 fromPos, Pawn healRecipient)
        {
            startPos = fromPos.Yto0();
            recipient = healRecipient;
            lastRecipientPos = healRecipient != null && healRecipient.Spawned ? healRecipient.DrawPos.Yto0() : startPos;
            arcSign = Rand.Bool ? 1f : -1f;
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

            if (recipient != null && recipient.Spawned && !recipient.Dead)
            {
                lastRecipientPos = recipient.DrawPos.Yto0();
            }

            ageTicks++;
            if (ageTicks >= DurationTicks)
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
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.06f;

            Vector3 delta = lastRecipientPos - startPos;
            Vector3 perp = delta.sqrMagnitude > 0.01f
                ? new Vector3(-delta.z, 0f, delta.x).normalized * (0.55f * arcSign)
                : new Vector3(0.35f * arcSign, 0f, 0.35f);
            Vector3 ctrl = (startPos + lastRecipientPos) * 0.5f + perp;

            // 1. Multi-node luminous vitality siphon trail arcing from victim -> attacker (Crimson -> Mint Green)
            if (FairyFestivalMaterials.LightOrbMat != null && t < 0.85f)
            {
                orbProps ??= new MaterialPropertyBlock();
                for (int k = 0; k < 5; k++)
                {
                    float sampleT = Mathf.Clamp01(t * 1.25f - k * 0.09f);
                    float inv = 1f - sampleT;
                    Vector3 pt = inv * inv * startPos + 2f * inv * sampleT * ctrl + sampleT * sampleT * lastRecipientPos;
                    Color c = Color.Lerp(new Color(1.0f, 0.25f, 0.32f, 1.0f), new Color(0.32f, 1.0f, 0.74f, 1.0f), sampleT);
                    c.a = (1f - k * 0.16f) * Mathf.Clamp01(1f - Mathf.Max(0f, t - 0.65f) / 0.20f);
                    orbProps.SetColor(FairyFestivalMaterials.PropColor, c);

                    float sz = (0.42f - k * 0.055f);
                    Matrix4x4 m = Matrix4x4.TRS(
                        new Vector3(pt.x, alt + 0.01f, pt.z + 0.15f),
                        Quaternion.identity,
                        new Vector3(sz, 1f, sz));
                    Graphics.DrawMesh(MeshPool.plane10, m, FairyFestivalMaterials.LightOrbMat, 0, null, 0, orbProps);
                }
            }

            // 2. Emerald healing bloom ring around the wielder once the siphon arrives
            if (t >= 0.42f && FairyFestivalMaterials.BlessingAuraMat != null)
            {
                float bloomT = Mathf.Clamp01((t - 0.42f) / 0.58f);
                float bloomAlpha = Mathf.Pow(1f - bloomT, 1.15f) * 0.85f;
                auraProps ??= new MaterialPropertyBlock();
                auraProps.SetColor(FairyFestivalMaterials.PropColor, new Color(0.30f, 0.98f, 0.72f, 1.0f));
                auraProps.SetColor(FairyFestivalMaterials.PropCoreColor, new Color(0.90f, 1.0f, 0.95f, 1.0f));
                auraProps.SetFloat(FairyFestivalMaterials.PropProgress, bloomT);
                auraProps.SetFloat(FairyFestivalMaterials.PropAgeSecs, ageTicks / 60f);
                auraProps.SetFloat(FairyFestivalMaterials.PropAlpha, bloomAlpha);
                auraProps.SetFloat(FairyFestivalMaterials.PropMode, 1.0f);

                Matrix4x4 bloomMat = Matrix4x4.TRS(
                    new Vector3(lastRecipientPos.x, alt, lastRecipientPos.z),
                    Quaternion.identity,
                    new Vector3(1.85f, 1f, 1.85f));
                Graphics.DrawMesh(MeshPool.plane10, bloomMat, FairyFestivalMaterials.BlessingAuraMat, 0, null, 0, auraProps);
            }
        }
    }

    /// <summary>精灵盛宴与振翅武器 VFX 生成辅助工具。</summary>
    public static class FairyFestivalVfxUtility
    {
        public static Thing_FairyCareSwarm SpawnCareSwarm(Pawn host)
        {
            if (host == null || !host.Spawned || host.Map == null || FairyFestivalDefOf.LC_FairyCareSwarmFX == null)
            {
                return null;
            }

            IntVec3 cell = host.Position.ClampInsideMap(host.Map);
            if (!cell.InBounds(host.Map))
            {
                return null;
            }

            if (GenSpawn.Spawn(FairyFestivalDefOf.LC_FairyCareSwarmFX, cell, host.Map) is Thing_FairyCareSwarm swarm)
            {
                swarm.Initialize(host);
                return swarm;
            }

            return null;
        }

        public static void SpawnBlessingDispatch(Map map, Vector3 fromPos, Vector3 toPos)
        {
            if (map == null || FairyFestivalDefOf.LC_FairyBlessingBurstFX == null)
            {
                return;
            }

            IntVec3 cell = toPos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(FairyFestivalDefOf.LC_FairyBlessingBurstFX, cell, map) is Thing_FairyBlessingBurst burst)
            {
                burst.InitializeDispatch(fromPos, toPos, radius: 1.75f, duration: 50);
                FleckMaker.ThrowDustPuffThick(toPos, map, 1.25f, new Color(0.45f, 1.0f, 0.80f));
                FleckMaker.ThrowMicroSparks(toPos, map);
            }
        }

        public static void SpawnHealPulse(Map map, Vector3 pos, float radius = 1.15f)
        {
            if (map == null || FairyFestivalDefOf.LC_FairyBlessingBurstFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(FairyFestivalDefOf.LC_FairyBlessingBurstFX, cell, map) is Thing_FairyBlessingBurst burst)
            {
                burst.InitializePulse(pos, radius, duration: 34);
                FleckMaker.ThrowDustPuffThick(pos, map, 0.72f, new Color(0.42f, 1.0f, 0.78f));
            }
        }

        public static void SpawnDevourExecution(Map map, Vector3 pos)
        {
            if (map == null || FairyFestivalDefOf.LC_FairyDevourExecutionFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(FairyFestivalDefOf.LC_FairyDevourExecutionFX, cell, map) is Thing_FairyDevourExecution fx)
            {
                fx.Initialize(pos);
                FleckMaker.ThrowDustPuffThick(pos, map, 1.85f, new Color(0.80f, 0.08f, 0.12f));
                FleckMaker.ThrowMicroSparks(pos, map);
                if (Find.CurrentMap == map && Find.CameraDriver != null)
                {
                    Find.CameraDriver.shaker.DoShake(0.55f);
                }
            }
        }

        public static void SpawnWingbeatSlash(Map map, Vector3 pos, float yawDeg, float scale = 1.65f, bool flipSweep = false, bool crimsonFeastMode = false, int durationTicks = 16)
        {
            if (map == null || FairyFestivalDefOf.LC_WingbeatSlashFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(FairyFestivalDefOf.LC_WingbeatSlashFX, cell, map) is Thing_WingbeatSlash slash)
            {
                slash.Initialize(pos, yawDeg, scale, flipSweep, crimsonFeastMode, durationTicks);
                Color dustCol = crimsonFeastMode
                    ? new Color(0.88f, 0.12f, 0.16f)
                    : new Color(0.45f, 1.0f, 0.80f);
                FleckMaker.ThrowDustPuffThick(pos, map, crimsonFeastMode ? 0.95f : 0.82f, dustCol);
            }
        }

        public static void SpawnWingbeatLifesteal(Map map, Vector3 fromPos, Pawn healRecipient)
        {
            if (map == null || healRecipient == null || FairyFestivalDefOf.LC_WingbeatLifestealFX == null)
            {
                return;
            }

            IntVec3 cell = fromPos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(FairyFestivalDefOf.LC_WingbeatLifestealFX, cell, map) is Thing_WingbeatLifesteal fx)
            {
                fx.Initialize(fromPos, healRecipient);
            }
        }
    }
}
