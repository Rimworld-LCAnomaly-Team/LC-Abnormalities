using LCAbnormalities.Shared;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Misc;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.AllAroundHelper
{
    /// <summary>T-05-41 小帮手与 E.G.O. 武器“粉碎机 Mk4”专属自定义 GLSL 450 着色器与解包特效材质缓存。</summary>
    [StaticConstructorOnStartup]
    public static class AllAroundHelperMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropDangerColor = Shader.PropertyToID("_DangerColor");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropBloodColor = Shader.PropertyToID("_BloodColor");
        public static readonly int PropSparkColor = Shader.PropertyToID("_SparkColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");
        public static readonly int PropSpinSpeed = Shader.PropertyToID("_SpinSpeed");
        public static readonly int PropArcSpan = Shader.PropertyToID("_ArcSpan");

        public static readonly Material ChargeTelegraphMat;
        public static readonly Material SawVortexMat;
        public static readonly Material GrinderSlashMat;
        public static readonly Material SawRingMat;
        public static readonly Material ElecArcMat;

        static AllAroundHelperMaterials()
        {
            Shader chargeShader = ShaderDatabase.LoadShader("Map/LC_HelperChargeTelegraph") ?? ShaderDatabase.MoteGlow;
            Shader vortexShader = ShaderDatabase.LoadShader("Map/LC_HelperSawVortex") ?? ShaderDatabase.MoteGlow;
            Shader slashShader = ShaderDatabase.LoadShader("Map/LC_GrinderSawSlash") ?? ShaderDatabase.MoteGlow;

            Texture2D sawRingTex = ContentFinder<Texture2D>.Get("Things/VFX/AllAroundHelper/Helper_SawRing", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D elecArcTex = ContentFinder<Texture2D>.Get("Things/VFX/AllAroundHelper/Helper_ElecArc", reportFailure: false) ?? BaseContent.WhiteTex;

            ChargeTelegraphMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = chargeShader,
                color = new Color(1.0f, 0.84f, 0.14f, 1.0f)
            });

            SawVortexMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = vortexShader,
                color = new Color(0.94f, 0.96f, 1.0f, 1.0f)
            });

            GrinderSlashMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = slashShader,
                color = new Color(0.95f, 0.12f, 0.10f, 1.0f)
            });

            SawRingMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = sawRingTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });

            ElecArcMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = elecArcTex,
                shader = ShaderDatabase.MoteGlow,
                color = new Color(1.0f, 0.86f, 0.35f, 1.0f)
            });
        }
    }

    /// <summary>
    /// 小帮手蓄力预警特效（还原脑叶原作黄色蓄力条 + 旋转警戒光环 + 直线冲锋危险走廊预警）：
    /// 随蓄力进度 0→1 由警戒金黄渐变为猩红闪耀，并从刀刃端迸发电弧火花。
    /// </summary>
    public class Thing_HelperChargeTelegraph : Thing
    {
        private Pawn owner;
        private Vector3 targetWorldPos;
        private int ageTicks;
        private bool initialized;
        private MaterialPropertyBlock ringProps;
        private MaterialPropertyBlock laneProps;

        public void Initialize(Pawn helperPawn, IntVec3 targetCell)
        {
            owner = helperPawn;
            targetWorldPos = targetCell.IsValid ? targetCell.ToVector3Shifted().Yto0() : (helperPawn != null ? helperPawn.DrawPos.Yto0() : Vector3.zero);
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || owner == null || owner.Destroyed || owner.Dead || !owner.Spawned || owner.Map != Map)
            {
                Destroy();
                return;
            }

            CompAllAroundHelper comp = (owner as LC_AllAroundHelperPawn)?.HelperComp ?? owner.TryGetComp<CompAllAroundHelper>();
            if (comp == null || !comp.Escaped || !comp.IsCharging)
            {
                Destroy();
                return;
            }

            ageTicks++;
            if (owner.Position.InBounds(Map) && Position != owner.Position)
            {
                Position = owner.Position;
            }

            if (comp.DashTargetCell.IsValid)
            {
                targetWorldPos = comp.DashTargetCell.ToVector3Shifted().Yto0();
            }

            // Escalating electrical sparks as turbine revs toward max charge
            float prog = comp.ChargeProgress;
            int sparkPeriod = prog > 0.65f ? 7 : 15;
            if (ageTicks % sparkPeriod == 0 && Map != null)
            {
                Vector3 basePos = owner.DrawPos.Yto0();
                Vector3 offset = new Vector3(Rand.Range(-0.75f, 0.75f), 0f, Rand.Range(-0.65f, 0.75f));
                AllAroundHelperVfxUtility.SpawnSparkBurst(Map, basePos + offset, scale: Rand.Range(0.55f, 0.95f), lifeTicks: 14, isCrimson: prog > 0.7f);
                FleckMaker.ThrowMicroSparks(basePos + offset * 0.5f, Map);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || owner == null || !owner.Spawned || AllAroundHelperMaterials.ChargeTelegraphMat == null)
            {
                return;
            }

            CompAllAroundHelper comp = (owner as LC_AllAroundHelperPawn)?.HelperComp ?? owner.TryGetComp<CompAllAroundHelper>();
            if (comp == null || !comp.IsCharging)
            {
                return;
            }

            float prog = comp.ChargeProgress;
            float fadeIn = Mathf.Clamp01(ageTicks / 10f);
            float ageSecs = ageTicks / 60f;
            Vector3 origin = owner.DrawPos.Yto0();
            float groundAlt = AltitudeLayer.Blueprint.AltitudeFor() + 0.03f;

            // Pass 1: Circular yellow -> crimson radial charging gauge & rotating hazard reticle
            ringProps ??= new MaterialPropertyBlock();
            ringProps.SetColor(AllAroundHelperMaterials.PropColor, new Color(1.0f, 0.84f, 0.14f, 1.0f));
            ringProps.SetColor(AllAroundHelperMaterials.PropDangerColor, new Color(1.0f, 0.16f, 0.10f, 1.0f));
            ringProps.SetColor(AllAroundHelperMaterials.PropCoreColor, new Color(1.0f, 0.97f, 0.82f, 1.0f));
            ringProps.SetFloat(AllAroundHelperMaterials.PropProgress, prog);
            ringProps.SetFloat(AllAroundHelperMaterials.PropAgeSecs, ageSecs);
            ringProps.SetFloat(AllAroundHelperMaterials.PropAlpha, fadeIn * 0.95f);
            ringProps.SetFloat(AllAroundHelperMaterials.PropMode, 0f);

            float ringDiameter = Mathf.Lerp(2.65f, 3.15f, prog);
            Matrix4x4 ringMat = Matrix4x4.TRS(
                new Vector3(origin.x, groundAlt + 0.01f, origin.z),
                Quaternion.identity,
                new Vector3(ringDiameter, 1f, ringDiameter));
            Graphics.DrawMesh(MeshPool.plane10, ringMat, AllAroundHelperMaterials.ChargeTelegraphMat, 0, null, 0, ringProps);

            // Pass 2: Linear Dash Danger Corridor pointing from Helper to targetCell
            Vector3 delta = targetWorldPos - origin;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist > 0.65f)
            {
                float laneLen = Mathf.Min(dist + 0.85f, comp.PropsHelper.dashRange + 1.0f);
                Vector3 dir = delta / dist;
                Vector3 mid = origin + dir * (laneLen * 0.5f);
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

                laneProps ??= new MaterialPropertyBlock();
                laneProps.SetColor(AllAroundHelperMaterials.PropColor, new Color(1.0f, 0.80f, 0.12f, 0.92f));
                laneProps.SetColor(AllAroundHelperMaterials.PropDangerColor, new Color(1.0f, 0.14f, 0.10f, 1.0f));
                laneProps.SetColor(AllAroundHelperMaterials.PropCoreColor, new Color(1.0f, 0.95f, 0.78f, 1.0f));
                laneProps.SetFloat(AllAroundHelperMaterials.PropProgress, prog);
                laneProps.SetFloat(AllAroundHelperMaterials.PropAgeSecs, ageSecs);
                laneProps.SetFloat(AllAroundHelperMaterials.PropAlpha, fadeIn * 0.85f);
                laneProps.SetFloat(AllAroundHelperMaterials.PropMode, 1f);

                float laneWidth = Mathf.Lerp(1.65f, 1.95f, prog);
                Matrix4x4 laneMat = Matrix4x4.TRS(
                    new Vector3(mid.x, groundAlt, mid.z),
                    rot,
                    new Vector3(laneWidth, 1f, laneLen));
                Graphics.DrawMesh(MeshPool.plane10, laneMat, AllAroundHelperMaterials.ChargeTelegraphMat, 0, null, 0, laneProps);
            }
        }
    }

    /// <summary>
    /// 小帮手高速旋转冲刺（Lv3_Spin）贴身血钢电锯气旋特效：
    /// 由自定义 GLSL 450 着色器 LC_HelperSawVortex 渲染高速动态模糊钢刃、猩红血雾与高温摩擦火星。
    /// </summary>
    public class Thing_HelperSawVortex : Thing
    {
        private Pawn owner;
        private int ageTicks;
        private int fadeOutTicks;
        private bool finishing;
        private const int FadeOutDuration = 12;
        private Vector3 lastPos;
        private bool initialized;
        private MaterialPropertyBlock vortexProps;

        public void Initialize(Pawn helperPawn)
        {
            owner = helperPawn;
            lastPos = helperPawn != null ? helperPawn.DrawPos.Yto0() : Position.ToVector3Shifted().Yto0();
            ageTicks = 0;
            fadeOutTicks = 0;
            finishing = false;
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
            if (!finishing)
            {
                if (owner == null || owner.Destroyed || owner.Dead || !owner.Spawned || owner.Map != Map)
                {
                    finishing = true;
                }
                else
                {
                    CompAllAroundHelper comp = (owner as LC_AllAroundHelperPawn)?.HelperComp ?? owner.TryGetComp<CompAllAroundHelper>();
                    if (comp == null || !comp.Escaped || !comp.IsDashing)
                    {
                        finishing = true;
                    }
                    else
                    {
                        lastPos = owner.DrawPos.Yto0();
                        if (owner.Position.InBounds(Map) && Position != owner.Position)
                        {
                            Position = owner.Position;
                        }
                    }
                }
            }
            else
            {
                fadeOutTicks++;
                if (fadeOutTicks >= FadeOutDuration)
                {
                    Destroy();
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || AllAroundHelperMaterials.SawVortexMat == null)
            {
                return;
            }

            Vector3 pos = (!finishing && owner != null && owner.Spawned) ? owner.DrawPos.Yto0() : lastPos;
            float fadeIn = Mathf.Clamp01(ageTicks / 5f);
            float fadeOut = finishing ? Mathf.Clamp01(1f - fadeOutTicks / (float)FadeOutDuration) : 1f;
            float alpha = fadeIn * fadeOut;
            if (alpha <= 0.01f)
            {
                return;
            }

            float ageSecs = ageTicks / 60f;
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f;

            vortexProps ??= new MaterialPropertyBlock();
            vortexProps.SetColor(AllAroundHelperMaterials.PropColor, new Color(0.95f, 0.97f, 1.0f, 1.0f));
            vortexProps.SetColor(AllAroundHelperMaterials.PropBloodColor, new Color(0.95f, 0.08f, 0.08f, 1.0f));
            vortexProps.SetColor(AllAroundHelperMaterials.PropSparkColor, new Color(1.0f, 0.78f, 0.24f, 1.0f));
            vortexProps.SetFloat(AllAroundHelperMaterials.PropProgress, 0.5f);
            vortexProps.SetFloat(AllAroundHelperMaterials.PropAgeSecs, ageSecs);
            vortexProps.SetFloat(AllAroundHelperMaterials.PropAlpha, alpha * 0.95f);
            vortexProps.SetFloat(AllAroundHelperMaterials.PropSpinSpeed, finishing ? 12f : 28f);
            vortexProps.SetFloat(AllAroundHelperMaterials.PropMode, 0f);

            float size = finishing
                ? Mathf.Lerp(3.35f, 2.25f, fadeOutTicks / (float)FadeOutDuration)
                : 3.35f + 0.18f * Mathf.Sin(ageSecs * 26f);

            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(pos.x, alt, pos.z),
                Quaternion.identity,
                new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, mat, AllAroundHelperMaterials.SawVortexMat, 0, null, 0, vortexProps);
        }
    }

    /// <summary>小帮手冲刺沿途留下的猩红钢刃残影尾流（LC_HelperSawVortex _Mode=1）。</summary>
    public class Thing_HelperDashTrail : Thing
    {
        private Vector3 startPos;
        private Vector3 endPos;
        private int ageTicks;
        private int durationTicks = 24;
        private float width = 1.85f;
        private bool initialized;
        private MaterialPropertyBlock trailProps;

        public void Initialize(Vector3 fromPos, Vector3 toPos, float trailWidth = 1.85f, int lifeTicks = 24)
        {
            startPos = fromPos.Yto0();
            endPos = toPos.Yto0();
            width = Mathf.Max(0.8f, trailWidth);
            durationTicks = Mathf.Clamp(lifeTicks, 10, 48);
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
            if (!initialized || AllAroundHelperMaterials.SawVortexMat == null)
            {
                return;
            }

            Vector3 delta = endPos - startPos;
            delta.y = 0f;
            float len = delta.magnitude;
            if (len < 0.1f)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            Vector3 mid = (startPos + endPos) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(delta / len, Vector3.up);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.01f;

            trailProps ??= new MaterialPropertyBlock();
            trailProps.SetColor(AllAroundHelperMaterials.PropColor, new Color(0.95f, 0.97f, 1.0f, 1.0f));
            trailProps.SetColor(AllAroundHelperMaterials.PropBloodColor, new Color(0.92f, 0.08f, 0.08f, 1.0f));
            trailProps.SetColor(AllAroundHelperMaterials.PropSparkColor, new Color(1.0f, 0.78f, 0.24f, 1.0f));
            trailProps.SetFloat(AllAroundHelperMaterials.PropProgress, t);
            trailProps.SetFloat(AllAroundHelperMaterials.PropAgeSecs, ageTicks / 60f);
            trailProps.SetFloat(AllAroundHelperMaterials.PropAlpha, 0.90f);
            trailProps.SetFloat(AllAroundHelperMaterials.PropSpinSpeed, 18f);
            trailProps.SetFloat(AllAroundHelperMaterials.PropMode, 1f);

            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(mid.x, alt, mid.z),
                rot,
                new Vector3(width * Mathf.Lerp(1.05f, 0.65f, t), 1f, len + 0.55f));
            Graphics.DrawMesh(MeshPool.plane10, mat, AllAroundHelperMaterials.SawVortexMat, 0, null, 0, trailProps);
        }
    }

    /// <summary>锯齿撕裂刀光特效（由自定义 GLSL 450 着色器 LC_GrinderSawSlash 渲染）。</summary>
    public class Thing_GrinderSawSlash : Thing
    {
        private Vector3 exactPos;
        private float angleDeg;
        private float scale = 1.65f;
        private float arcSpan = 2.65f;
        private bool mirrorSweep;
        private int ageTicks;
        private int durationTicks = 14;
        private bool initialized;
        private MaterialPropertyBlock slashProps;

        public void Initialize(Vector3 pos, float yawAngleDeg, float slashScale = 1.65f, bool flipSweep = false, int lifeTicks = 14, float spanRad = 2.65f)
        {
            exactPos = pos.Yto0();
            angleDeg = yawAngleDeg;
            scale = Mathf.Max(0.6f, slashScale);
            mirrorSweep = flipSweep;
            durationTicks = Mathf.Clamp(lifeTicks, 8, 28);
            arcSpan = Mathf.Clamp(spanRad, 1.5f, 3.6f);
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
            if (!initialized || AllAroundHelperMaterials.GrinderSlashMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.06f;

            slashProps ??= new MaterialPropertyBlock();
            slashProps.SetColor(AllAroundHelperMaterials.PropColor, new Color(0.95f, 0.10f, 0.08f, 1.0f));
            slashProps.SetColor(AllAroundHelperMaterials.PropCoreColor, new Color(1.0f, 0.95f, 0.84f, 1.0f));
            slashProps.SetColor(AllAroundHelperMaterials.PropSparkColor, new Color(1.0f, 0.72f, 0.18f, 1.0f));
            slashProps.SetFloat(AllAroundHelperMaterials.PropProgress, t);
            slashProps.SetFloat(AllAroundHelperMaterials.PropAgeSecs, ageTicks / 60f);
            slashProps.SetFloat(AllAroundHelperMaterials.PropAlpha, 1.0f);
            slashProps.SetFloat(AllAroundHelperMaterials.PropArcSpan, arcSpan);

            float sz = scale * Mathf.Lerp(0.86f, 1.16f, t);
            Mesh mesh = mirrorSweep ? MeshPool.plane10Flip : MeshPool.plane10;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.AngleAxis(angleDeg, Vector3.up),
                new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(mesh, mat, AllAroundHelperMaterials.GrinderSlashMat, 0, null, 0, slashProps);
        }
    }

    /// <summary>
    /// E.G.O. 武器“粉碎机 Mk4”高频旋转锯切多段演出驱动器（还原脑叶解包 HelperWeapon Attack_Spear_01）：
    /// 在命中点高速旋转原作解包带血锯齿环（Helper_Edge）与血钢气旋，并按错落节拍迸发 4 道锯齿撕裂刀光与金属火星。
    /// </summary>
    public class Thing_GrinderMk4FlurryRunner : Thing
    {
        private Vector3 targetPos;
        private float baseAngleDeg;
        private int ageTicks;
        private const int TotalTicks = 22;
        private int nextBeatIndex;
        private float spinAngleDeg;
        private bool initialized;

        private MaterialPropertyBlock vortexProps;
        private MaterialPropertyBlock ringProps;

        private static readonly int[] SlashBeatTicks = { 0, 4, 8, 12 };

        public void Initialize(Vector3 hitPos, float attackYawDeg)
        {
            targetPos = hitPos.Yto0();
            baseAngleDeg = attackYawDeg;
            ageTicks = 0;
            nextBeatIndex = 0;
            spinAngleDeg = Rand.Range(0f, 360f);
            initialized = true;
            TriggerPendingBeats();
        }

        protected override void Tick()
        {
            if (!initialized)
            {
                Destroy();
                return;
            }

            ageTicks++;
            spinAngleDeg = (spinAngleDeg + 34f) % 360f;
            TriggerPendingBeats();

            if (ageTicks >= TotalTicks)
            {
                Destroy();
            }
        }

        private void TriggerPendingBeats()
        {
            if (Map == null)
            {
                return;
            }

            while (nextBeatIndex < SlashBeatTicks.Length && ageTicks >= SlashBeatTicks[nextBeatIndex])
            {
                int idx = nextBeatIndex++;
                Vector3 jitter = new Vector3(Rand.Range(-0.22f, 0.22f), 0f, Rand.Range(-0.22f, 0.22f));
                float angle = baseAngleDeg + (idx - 1.5f) * 34f + Rand.Range(-18f, 18f);
                AllAroundHelperVfxUtility.SpawnSawSlash(
                    Map,
                    targetPos + jitter,
                    angle,
                    scale: Rand.Range(1.35f, 1.85f),
                    flipSweep: (idx % 2) == 1,
                    lifeTicks: 11,
                    spanRad: 2.55f);

                if (idx == 0 || idx == 2)
                {
                    AllAroundHelperVfxUtility.SpawnSparkBurst(Map, targetPos + jitter, scale: Rand.Range(0.55f, 0.85f), lifeTicks: 12, isCrimson: idx == 2);
                    FleckMaker.ThrowMicroSparks(targetPos + jitter, Map);
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)TotalTicks);
            float fadeIn = Mathf.Clamp01(t / 0.14f);
            float fadeOut = t > 0.55f ? Mathf.Clamp01(1f - (t - 0.55f) / 0.45f) : 1f;
            float alpha = fadeIn * fadeOut;
            if (alpha <= 0.01f)
            {
                return;
            }

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
            float ageSecs = ageTicks / 60f;

            // 1. Compact spinning crimson-spark rotary vortex underneath
            if (AllAroundHelperMaterials.SawVortexMat != null)
            {
                vortexProps ??= new MaterialPropertyBlock();
                vortexProps.SetColor(AllAroundHelperMaterials.PropColor, new Color(0.96f, 0.96f, 1.0f, 1.0f));
                vortexProps.SetColor(AllAroundHelperMaterials.PropBloodColor, new Color(0.95f, 0.10f, 0.08f, 1.0f));
                vortexProps.SetColor(AllAroundHelperMaterials.PropSparkColor, new Color(1.0f, 0.78f, 0.24f, 1.0f));
                vortexProps.SetFloat(AllAroundHelperMaterials.PropProgress, t);
                vortexProps.SetFloat(AllAroundHelperMaterials.PropAgeSecs, ageSecs);
                vortexProps.SetFloat(AllAroundHelperMaterials.PropAlpha, alpha * 0.90f);
                vortexProps.SetFloat(AllAroundHelperMaterials.PropSpinSpeed, 30f);
                vortexProps.SetFloat(AllAroundHelperMaterials.PropMode, 0f);

                float vsz = Mathf.Lerp(1.35f, 1.95f, 1f - Mathf.Pow(1f - t, 2f));
                Matrix4x4 vmat = Matrix4x4.TRS(
                    new Vector3(targetPos.x, alt, targetPos.z),
                    Quaternion.identity,
                    new Vector3(vsz, 1f, vsz));
                Graphics.DrawMesh(MeshPool.plane10, vmat, AllAroundHelperMaterials.SawVortexMat, 0, null, 0, vortexProps);
            }

            // 2. Authentic unpacked Helper_Edge bloody sawblade crown spinning at high RPM!
            if (AllAroundHelperMaterials.SawRingMat != null)
            {
                ringProps ??= new MaterialPropertyBlock();
                ringProps.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, alpha * 0.92f));
                float rsz = Mathf.Lerp(0.95f, 1.32f, t);
                Matrix4x4 rmat = Matrix4x4.TRS(
                    new Vector3(targetPos.x, alt + 0.01f, targetPos.z),
                    Quaternion.AngleAxis(spinAngleDeg, Vector3.up),
                    new Vector3(rsz, 1f, rsz));
                Graphics.DrawMesh(MeshPool.plane10, rmat, AllAroundHelperMaterials.SawRingMat, 0, null, 0, ringProps);
            }
        }
    }

    /// <summary>电弧/金属摩擦火花粒子（使用解包 Spark.png，用于蓄力、锯切命中与过热宕机短路）。</summary>
    public class Thing_HelperSparkBurst : Thing
    {
        private Vector3 exactPos;
        private Vector3 velocity;
        private float baseScale = 0.75f;
        private float angleDeg;
        private int ageTicks;
        private int lifetimeTicks = 14;
        private bool crimsonTint;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 pos, float scale = 0.75f, int life = 14, bool isCrimson = false)
        {
            exactPos = pos.Yto0();
            velocity = new Vector3(Rand.Range(-0.025f, 0.025f), 0f, Rand.Range(0.01f, 0.035f));
            baseScale = Mathf.Max(0.25f, scale);
            lifetimeTicks = Mathf.Clamp(life, 6, 36);
            angleDeg = Rand.Range(0f, 360f);
            crimsonTint = isCrimson;
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
            velocity *= 0.92f;
            if (ageTicks >= lifetimeTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || AllAroundHelperMaterials.ElecArcMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)lifetimeTicks);
            float alpha = Mathf.Pow(1f - t, 1.35f);
            if (alpha <= 0.01f)
            {
                return;
            }

            Color tint = crimsonTint
                ? new Color(1.0f, 0.42f, 0.22f, alpha)
                : new Color(1.0f, 0.88f, 0.36f, alpha);

            props ??= new MaterialPropertyBlock();
            props.SetColor(ShaderPropertyIDs.Color, tint);

            float sz = baseScale * Mathf.Lerp(0.75f, 1.28f, t);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.07f;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.AngleAxis(angleDeg, Vector3.up),
                new Vector3(sz, 1f, sz * 0.45f));
            Graphics.DrawMesh(MeshPool.plane10, mat, AllAroundHelperMaterials.ElecArcMat, 0, null, 0, props);
        }
    }

    /// <summary>小帮手被镇压后的短路宕机消散演出：电锯残影减速熄灭、电弧四溅，最终凝结为收容蛋。</summary>
    public class DyingAllAroundHelper : LC_FX_Standard
    {
        private const int TotalDurationTicks = 78;
        private int startTick;
        private MaterialPropertyBlock vortexProps;

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
                LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_SystemOff, this);
                Vector3 center = Position.ToVector3Shifted();
                FleckMaker.ThrowSmoke(center, Map, 1.8f);
                FleckMaker.ThrowMicroSparks(center, Map);
                for (int i = 0; i < 4; i++)
                {
                    AllAroundHelperVfxUtility.SpawnSparkBurst(
                        Map,
                        center + new Vector3(Rand.Range(-0.6f, 0.6f), 0f, Rand.Range(-0.4f, 0.7f)),
                        scale: Rand.Range(0.7f, 1.15f),
                        lifeTicks: Rand.Range(18, 32),
                        isCrimson: i % 2 == 0);
                }
            }
        }

        protected override void Tick()
        {
            base.Tick();
            if (!hasInited || Destroyed || Map == null)
            {
                return;
            }

            int elapsed = (Find.TickManager?.TicksGame ?? startTick) - startTick;
            if (elapsed > 0 && elapsed < TotalDurationTicks - 12 && elapsed % 16 == 0)
            {
                Vector3 center = Position.ToVector3Shifted();
                AllAroundHelperVfxUtility.SpawnSparkBurst(
                    Map,
                    center + new Vector3(Rand.Range(-0.5f, 0.5f), 0f, Rand.Range(-0.3f, 0.6f)),
                    scale: 0.75f,
                    lifeTicks: 14,
                    isCrimson: false);
                FleckMaker.ThrowSmoke(center, Map, 0.9f);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!hasInited || AllAroundHelperMaterials.SawVortexMat == null)
            {
                return;
            }

            int elapsed = Mathf.Clamp((Find.TickManager?.TicksGame ?? startTick) - startTick, 0, TotalDurationTicks);
            float t = elapsed / (float)TotalDurationTicks;
            float alpha = Mathf.Pow(1f - t, 1.35f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float sz = Mathf.Lerp(2.35f, 0.55f, t);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.02f;

            vortexProps ??= new MaterialPropertyBlock();
            vortexProps.SetColor(AllAroundHelperMaterials.PropColor, new Color(0.95f, 0.95f, 1.0f, 1.0f));
            vortexProps.SetColor(AllAroundHelperMaterials.PropBloodColor, new Color(0.92f, 0.14f, 0.10f, 1.0f));
            vortexProps.SetColor(AllAroundHelperMaterials.PropSparkColor, new Color(1.0f, 0.80f, 0.28f, 1.0f));
            vortexProps.SetFloat(AllAroundHelperMaterials.PropProgress, t);
            vortexProps.SetFloat(AllAroundHelperMaterials.PropAgeSecs, elapsed / 60f);
            vortexProps.SetFloat(AllAroundHelperMaterials.PropAlpha, alpha * 0.85f);
            vortexProps.SetFloat(AllAroundHelperMaterials.PropSpinSpeed, Mathf.Lerp(16f, 2f, t));
            vortexProps.SetFloat(AllAroundHelperMaterials.PropMode, 0f);

            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(drawLoc.x, alt, drawLoc.z),
                Quaternion.identity,
                new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, mat, AllAroundHelperMaterials.SawVortexMat, 0, null, 0, vortexProps);
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
            Scribe_Values.Look(ref startTick, "aahDeadStartTick", 0);
        }
    }

    /// <summary>小帮手与粉碎机 Mk4 VFX 生成辅助工具。</summary>
    public static class AllAroundHelperVfxUtility
    {
        public static Thing_HelperChargeTelegraph SpawnChargeTelegraph(Pawn helper, IntVec3 targetCell)
        {
            if (helper == null || !helper.Spawned || helper.Map == null || AllAroundHelperDefOf.LC_HelperChargeTelegraphFX == null)
            {
                return null;
            }

            if (GenSpawn.Spawn(AllAroundHelperDefOf.LC_HelperChargeTelegraphFX, helper.Position, helper.Map) is Thing_HelperChargeTelegraph fx)
            {
                fx.Initialize(helper, targetCell);
                return fx;
            }

            return null;
        }

        public static Thing_HelperSawVortex SpawnSawVortex(Pawn helper)
        {
            if (helper == null || !helper.Spawned || helper.Map == null || AllAroundHelperDefOf.LC_HelperSawVortexFX == null)
            {
                return null;
            }

            if (GenSpawn.Spawn(AllAroundHelperDefOf.LC_HelperSawVortexFX, helper.Position, helper.Map) is Thing_HelperSawVortex fx)
            {
                fx.Initialize(helper);
                return fx;
            }

            return null;
        }

        public static void SpawnDashTrail(Map map, Vector3 fromPos, Vector3 toPos, float width = 1.85f, int lifeTicks = 24)
        {
            if (map == null || AllAroundHelperDefOf.LC_HelperDashTrailFX == null)
            {
                return;
            }

            IntVec3 anchor = ((fromPos + toPos) * 0.5f).ToIntVec3().ClampInsideMap(map);
            if (!anchor.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(AllAroundHelperDefOf.LC_HelperDashTrailFX, anchor, map) is Thing_HelperDashTrail fx)
            {
                fx.Initialize(fromPos, toPos, width, lifeTicks);
            }
        }

        public static void SpawnSawSlash(Map map, Vector3 pos, float yawAngleDeg, float scale = 1.65f, bool flipSweep = false, int lifeTicks = 14, float spanRad = 2.65f)
        {
            if (map == null || AllAroundHelperDefOf.LC_GrinderSawSlashFX == null)
            {
                return;
            }

            IntVec3 anchor = pos.ToIntVec3().ClampInsideMap(map);
            if (!anchor.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(AllAroundHelperDefOf.LC_GrinderSawSlashFX, anchor, map) is Thing_GrinderSawSlash fx)
            {
                fx.Initialize(pos, yawAngleDeg, scale, flipSweep, lifeTicks, spanRad);
            }
        }

        public static void SpawnGrinderFlurry(Map map, Vector3 hitPos, float attackYawDeg)
        {
            if (map == null || AllAroundHelperDefOf.LC_GrinderFlurryFX == null)
            {
                return;
            }

            IntVec3 anchor = hitPos.ToIntVec3().ClampInsideMap(map);
            if (!anchor.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(AllAroundHelperDefOf.LC_GrinderFlurryFX, anchor, map) is Thing_GrinderMk4FlurryRunner fx)
            {
                fx.Initialize(hitPos, attackYawDeg);
            }
        }

        public static void SpawnSparkBurst(Map map, Vector3 pos, float scale = 0.75f, int lifeTicks = 14, bool isCrimson = false)
        {
            if (map == null || AllAroundHelperDefOf.LC_HelperSparkBurstFX == null)
            {
                return;
            }

            IntVec3 anchor = pos.ToIntVec3().ClampInsideMap(map);
            if (!anchor.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(AllAroundHelperDefOf.LC_HelperSparkBurstFX, anchor, map) is Thing_HelperSparkBurst fx)
            {
                fx.Initialize(pos, scale, lifeTicks, isCrimson);
            }
        }
    }
}
