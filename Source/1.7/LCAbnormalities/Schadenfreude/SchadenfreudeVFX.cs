using LCAbnormalities.Shared;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Misc;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.Schadenfreude
{
    /// <summary>O-05-76 幸灾乐祸与 E.G.O. 武器“凝视”专属自定义 GLSL 450 着色器及解包特效材质缓存。</summary>
    [StaticConstructorOnStartup]
    public static class SchadenfreudeMaterials
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

        public static readonly Material KeyholeAuraMat;
        public static readonly Material SawShredMat;
        public static readonly Material GazeSlashMat;
        public static readonly Material GazeSawDiscMat;
        public static readonly Material KeyholeEyeMat;
        public static readonly Material BloodSprayMat;

        static SchadenfreudeMaterials()
        {
            Shader keyholeShader = ShaderDatabase.LoadShader("Map/LC_SchadenfreudeKeyholeAura") ?? ShaderDatabase.MoteGlow;
            Shader sawShredShader = ShaderDatabase.LoadShader("Map/LC_SchadenfreudeSawShred") ?? ShaderDatabase.MoteGlow;
            Shader gazeSlashShader = ShaderDatabase.LoadShader("Map/LC_GazeSawSlash") ?? ShaderDatabase.MoteGlow;

            Texture2D sawDiscTex = ContentFinder<Texture2D>.Get("Things/VFX/Schadenfreude/Gaze_SawDisc", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D keyholeEyeTex = ContentFinder<Texture2D>.Get("Things/VFX/Schadenfreude/Schadenfreude_KeyholeEye", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D bloodSprayTex = ContentFinder<Texture2D>.Get("Things/VFX/Schadenfreude/Schadenfreude_BloodSpray", reportFailure: false) ?? BaseContent.WhiteTex;

            KeyholeAuraMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = keyholeShader,
                color = new Color(0.65f, 0.74f, 0.80f, 1.0f)
            });

            SawShredMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = sawShredShader,
                color = new Color(0.76f, 0.86f, 0.90f, 1.0f)
            });

            GazeSlashMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = gazeSlashShader,
                color = new Color(0.92f, 0.08f, 0.12f, 1.0f)
            });

            GazeSawDiscMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = sawDiscTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });

            KeyholeEyeMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = keyholeEyeTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });

            BloodSprayMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = bloodSprayTex,
                shader = ShaderDatabase.Transparent,
                color = new Color(0.86f, 0.08f, 0.10f, 1.0f)
            });
        }
    }

    /// <summary>
    /// 幸灾乐祸监控光圈 / 危险预警光环（LC_SchadenfreudeKeyholeAura _Mode=0）：
    /// 1) 收容作业期间玩家注视时，光圈由冷铁灰急速转向猩红，并显示注视计时进度；
    /// 2) 突破后发动双电锯必杀蓄力（SawWindup）时，投射 3.2 格范围的猩红锁孔警戒环。
    /// </summary>
    public class Thing_SchadenfreudeWatchAura : Thing
    {
        private Pawn owner;
        private bool isSawTelegraph;
        private int ageTicks;
        private bool initialized;
        private MaterialPropertyBlock auraProps;

        public void Initialize(Pawn schadenPawn, bool sawTelegraphMode)
        {
            owner = schadenPawn;
            isSawTelegraph = sawTelegraphMode;
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || owner == null || owner.Destroyed || owner.Dead || owner.MapHeld != Map)
            {
                Destroy();
                return;
            }

            CompSchadenfreude comp = (owner as LC_SchadenfreudePawn)?.SchadenComp ?? owner.TryGetComp<CompSchadenfreude>();
            if (comp == null)
            {
                Destroy();
                return;
            }

            bool keepAlive = isSawTelegraph
                ? (comp.Escaped && comp.IsWatchedActive && comp.IsSawWindingUp)
                : (!comp.Escaped && comp.WatchedWorkTicks > 0);

            if (!keepAlive)
            {
                Destroy();
                return;
            }

            ageTicks++;
            IntVec3 hostCell = owner.PositionHeld;
            if (hostCell.InBounds(Map) && Position != hostCell)
            {
                Position = hostCell;
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || owner == null || SchadenfreudeMaterials.KeyholeAuraMat == null)
            {
                return;
            }

            CompSchadenfreude comp = (owner as LC_SchadenfreudePawn)?.SchadenComp ?? owner.TryGetComp<CompSchadenfreude>();
            if (comp == null)
            {
                return;
            }

            float prog = isSawTelegraph ? comp.SawWindupProgress : comp.WatchedWorkProgress;
            float fadeIn = Mathf.Clamp01(ageTicks / 8f);
            float ageSecs = ageTicks / 60f;
            Vector3 origin = (owner.Spawned ? owner.DrawPos : owner.PositionHeld.ToVector3Shifted()).Yto0();
            float alt = isSawTelegraph
                ? AltitudeLayer.Blueprint.AltitudeFor() + 0.03f
                : AltitudeLayer.MoteOverhead.AltitudeFor() + 0.01f;

            auraProps ??= new MaterialPropertyBlock();
            auraProps.SetColor(SchadenfreudeMaterials.PropColor, isSawTelegraph
                ? new Color(0.88f, 0.34f, 0.24f, 1.0f)
                : new Color(0.62f, 0.74f, 0.80f, 1.0f));
            auraProps.SetColor(SchadenfreudeMaterials.PropDangerColor, new Color(0.96f, 0.10f, 0.12f, 1.0f));
            auraProps.SetColor(SchadenfreudeMaterials.PropCoreColor, new Color(1.0f, 0.90f, 0.88f, 1.0f));
            auraProps.SetFloat(SchadenfreudeMaterials.PropProgress, prog);
            auraProps.SetFloat(SchadenfreudeMaterials.PropAgeSecs, ageSecs);
            auraProps.SetFloat(SchadenfreudeMaterials.PropAlpha, fadeIn * (isSawTelegraph ? 0.94f : Mathf.Lerp(0.55f, 0.98f, prog)));
            auraProps.SetFloat(SchadenfreudeMaterials.PropMode, 0f);

            float diameter = isSawTelegraph
                ? Mathf.Lerp(comp.PropsSchaden.spinRadius * 1.85f, comp.PropsSchaden.spinRadius * 2.10f, prog)
                : Mathf.Lerp(2.35f, 3.05f, prog);

            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(origin.x, alt, origin.z),
                Quaternion.identity,
                new Vector3(diameter, 1f, diameter));
            Graphics.DrawMesh(MeshPool.plane10, mat, SchadenfreudeMaterials.KeyholeAuraMat, 0, null, 0, auraProps);
        }
    }

    /// <summary>
    /// 锁孔注视冲击波与窥视之眼闪现（LC_SchadenfreudeKeyholeAura _Mode=1 + 解包 KeyholeEye 贴图）：
    /// 用于幸灾乐祸视野激活变形、双电锯启动以及 E.G.O. 武器“凝视”高叠层触发。
    /// </summary>
    public class Thing_SchadenfreudeKeyholePulse : Thing
    {
        private Vector3 exactPos;
        private float maxScale = 3.2f;
        private int ageTicks;
        private int durationTicks = 26;
        private bool showKeyholeSprite = true;
        private bool initialized;
        private MaterialPropertyBlock pulseProps;
        private MaterialPropertyBlock eyeProps;

        public void Initialize(Vector3 pos, float scale = 3.2f, int lifeTicks = 26, bool withEyeMedallion = true)
        {
            exactPos = pos.Yto0();
            maxScale = Mathf.Max(1.0f, scale);
            durationTicks = Mathf.Clamp(lifeTicks, 12, 60);
            showKeyholeSprite = withEyeMedallion;
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
            if (!initialized || SchadenfreudeMaterials.KeyholeAuraMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;

            pulseProps ??= new MaterialPropertyBlock();
            pulseProps.SetColor(SchadenfreudeMaterials.PropColor, new Color(0.68f, 0.78f, 0.84f, 1.0f));
            pulseProps.SetColor(SchadenfreudeMaterials.PropDangerColor, new Color(0.95f, 0.10f, 0.14f, 1.0f));
            pulseProps.SetColor(SchadenfreudeMaterials.PropCoreColor, new Color(1.0f, 0.92f, 0.90f, 1.0f));
            pulseProps.SetFloat(SchadenfreudeMaterials.PropProgress, t);
            pulseProps.SetFloat(SchadenfreudeMaterials.PropAgeSecs, ageTicks / 60f);
            pulseProps.SetFloat(SchadenfreudeMaterials.PropAlpha, 0.95f);
            pulseProps.SetFloat(SchadenfreudeMaterials.PropMode, 1f);

            float sz = maxScale * Mathf.Lerp(0.55f, 1.15f, 1f - Mathf.Pow(1f - t, 2f));
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.identity,
                new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, mat, SchadenfreudeMaterials.KeyholeAuraMat, 0, null, 0, pulseProps);

            if (showKeyholeSprite && SchadenfreudeMaterials.KeyholeEyeMat != null)
            {
                float eyeAlpha = Mathf.Sin(Mathf.Clamp01(t * 1.25f) * Mathf.PI) * (1f - t * 0.35f) * 0.88f;
                if (eyeAlpha > 0.02f)
                {
                    eyeProps ??= new MaterialPropertyBlock();
                    eyeProps.SetColor(ShaderPropertyIDs.Color, new Color(1f, 0.85f, 0.85f, eyeAlpha));
                    float eyeSz = Mathf.Min(1.55f, maxScale * 0.44f) * Mathf.Lerp(0.72f, 1.12f, t);
                    Matrix4x4 eyeMat = Matrix4x4.TRS(
                        new Vector3(exactPos.x, alt + 0.02f, exactPos.z + 0.25f),
                        Quaternion.identity,
                        new Vector3(eyeSz, 1f, eyeSz));
                    Graphics.DrawMesh(MeshPool.plane10, eyeMat, SchadenfreudeMaterials.KeyholeEyeMat, 0, null, 0, eyeProps);
                }
            }
        }
    }

    /// <summary>
    /// 幸灾乐祸双电锯狂暴切割气旋（LC_SchadenfreudeSawShred _Mode=0）：
    /// 在 3 秒双电锯连切（special2）期间贴身高速对向旋转猩红锯齿环并持续迸发火星与血雾。
    /// </summary>
    public class Thing_SchadenfreudeSawVortex : Thing
    {
        private Pawn owner;
        private Vector3 lastPos;
        private int ageTicks;
        private int fadeOutTicks;
        private bool finishing;
        private const int FadeOutDuration = 12;
        private bool initialized;
        private MaterialPropertyBlock vortexProps;

        public void Initialize(Pawn schadenPawn)
        {
            owner = schadenPawn;
            lastPos = schadenPawn != null ? schadenPawn.DrawPos.Yto0() : Position.ToVector3Shifted().Yto0();
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
                    CompSchadenfreude comp = (owner as LC_SchadenfreudePawn)?.SchadenComp ?? owner.TryGetComp<CompSchadenfreude>();
                    if (comp == null || !comp.Escaped || !comp.IsWatchedActive || !comp.IsSawSpinning)
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

                        if (ageTicks % 6 == 0 && Map != null)
                        {
                            Vector3 sparkPos = lastPos + new Vector3(Rand.Range(-1.45f, 1.45f), 0f, Rand.Range(-1.15f, 1.15f));
                            FleckMaker.ThrowMicroSparks(sparkPos, Map);
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
            if (!initialized || SchadenfreudeMaterials.SawShredMat == null)
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

            CompSchadenfreude comp = (owner as LC_SchadenfreudePawn)?.SchadenComp ?? owner?.TryGetComp<CompSchadenfreude>();
            float baseDiam = (comp != null ? comp.PropsSchaden.spinRadius : 3.2f) * 2.02f;
            float ageSecs = ageTicks / 60f;
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f;

            vortexProps ??= new MaterialPropertyBlock();
            vortexProps.SetColor(SchadenfreudeMaterials.PropColor, new Color(0.76f, 0.88f, 0.92f, 1.0f));
            vortexProps.SetColor(SchadenfreudeMaterials.PropBloodColor, new Color(0.95f, 0.06f, 0.09f, 1.0f));
            vortexProps.SetColor(SchadenfreudeMaterials.PropSparkColor, new Color(1.0f, 0.82f, 0.42f, 1.0f));
            vortexProps.SetFloat(SchadenfreudeMaterials.PropProgress, 0.5f);
            vortexProps.SetFloat(SchadenfreudeMaterials.PropAgeSecs, ageSecs);
            vortexProps.SetFloat(SchadenfreudeMaterials.PropAlpha, alpha * 0.94f);
            vortexProps.SetFloat(SchadenfreudeMaterials.PropSpinSpeed, finishing ? 10f : 26f);
            vortexProps.SetFloat(SchadenfreudeMaterials.PropMode, 0f);

            float size = finishing
                ? Mathf.Lerp(baseDiam, baseDiam * 0.65f, fadeOutTicks / (float)FadeOutDuration)
                : baseDiam + 0.22f * Mathf.Sin(ageSecs * 28f);

            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(pos.x, alt, pos.z),
                Quaternion.identity,
                new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, mat, SchadenfreudeMaterials.SawShredMat, 0, null, 0, vortexProps);
        }
    }

    /// <summary>
    /// 幸灾乐祸机械钻腿穿刺冲击特效（LC_SchadenfreudeSawShred _Mode=1）：
    /// 渲染从本体射向受击者的螺旋钻头钢刃突刺与猩红血雾飞溅。
    /// </summary>
    public class Thing_SchadenfreudeDrillThrust : Thing
    {
        private Vector3 startPos;
        private Vector3 endPos;
        private int ageTicks;
        private int durationTicks = 18;
        private bool initialized;
        private MaterialPropertyBlock thrustProps;

        public void Initialize(Vector3 fromPos, Vector3 toPos, int lifeTicks = 18)
        {
            startPos = fromPos.Yto0();
            endPos = toPos.Yto0();
            durationTicks = Mathf.Clamp(lifeTicks, 10, 32);
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
            if (!initialized || SchadenfreudeMaterials.SawShredMat == null)
            {
                return;
            }

            Vector3 delta = endPos - startPos;
            delta.y = 0f;
            float dist = Mathf.Max(0.8f, delta.magnitude);
            Vector3 dir = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector3.forward;
            float len = Mathf.Clamp(dist + 0.85f, 1.65f, 3.6f);
            Vector3 mid = startPos + dir * (len * 0.5f);
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;

            thrustProps ??= new MaterialPropertyBlock();
            thrustProps.SetColor(SchadenfreudeMaterials.PropColor, new Color(0.80f, 0.90f, 0.94f, 1.0f));
            thrustProps.SetColor(SchadenfreudeMaterials.PropBloodColor, new Color(0.95f, 0.07f, 0.10f, 1.0f));
            thrustProps.SetColor(SchadenfreudeMaterials.PropSparkColor, new Color(1.0f, 0.86f, 0.50f, 1.0f));
            thrustProps.SetFloat(SchadenfreudeMaterials.PropProgress, t);
            thrustProps.SetFloat(SchadenfreudeMaterials.PropAgeSecs, ageTicks / 60f);
            thrustProps.SetFloat(SchadenfreudeMaterials.PropAlpha, 0.96f);
            thrustProps.SetFloat(SchadenfreudeMaterials.PropSpinSpeed, 24f);
            thrustProps.SetFloat(SchadenfreudeMaterials.PropMode, 1f);

            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(mid.x, alt, mid.z),
                rot,
                new Vector3(1.15f, 1f, len));
            Graphics.DrawMesh(MeshPool.plane10, mat, SchadenfreudeMaterials.SawShredMat, 0, null, 0, thrustProps);
        }
    }

    /// <summary>猩红血溅粒子（使用解包 blood-splatter-1.png，用于钻腿穿刺、双电锯切割与 E.G.O. 凝视撕裂命中）。</summary>
    public class Thing_SchadenfreudeBloodBurst : Thing
    {
        private Vector3 exactPos;
        private Vector3 velocity;
        private float baseScale = 1.0f;
        private float angleDeg;
        private int ageTicks;
        private int lifetimeTicks = 18;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 pos, float yawDeg, float scale = 1.0f, int life = 18)
        {
            exactPos = pos.Yto0();
            float rad = yawDeg * Mathf.Deg2Rad;
            velocity = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * Rand.Range(0.015f, 0.042f);
            baseScale = Mathf.Max(0.35f, scale);
            angleDeg = yawDeg + Rand.Range(-35f, 35f);
            lifetimeTicks = Mathf.Clamp(life, 8, 36);
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
            velocity *= 0.88f;
            if (ageTicks >= lifetimeTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || SchadenfreudeMaterials.BloodSprayMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)lifetimeTicks);
            float alpha = Mathf.Pow(1f - t, 1.25f) * 0.92f;
            if (alpha <= 0.01f)
            {
                return;
            }

            props ??= new MaterialPropertyBlock();
            props.SetColor(ShaderPropertyIDs.Color, new Color(0.90f, 0.07f, 0.10f, alpha));

            float sz = baseScale * Mathf.Lerp(0.55f, 1.38f, 1f - Mathf.Pow(1f - t, 2f));
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.06f;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.AngleAxis(angleDeg, Vector3.up),
                new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, mat, SchadenfreudeMaterials.BloodSprayMat, 0, null, 0, props);
        }
    }

    /// <summary>E.G.O. 武器“凝视”猩红黑铁锯齿刀光特效（LC_GazeSawSlash _Mode=0）。</summary>
    public class Thing_GazeSawSlash : Thing
    {
        private Vector3 exactPos;
        private float angleDeg;
        private float scale = 1.60f;
        private float arcSpan = 2.65f;
        private bool mirrorSweep;
        private int ageTicks;
        private int durationTicks = 14;
        private bool initialized;
        private MaterialPropertyBlock slashProps;

        public void Initialize(Vector3 pos, float yawAngleDeg, float slashScale = 1.60f, bool flipSweep = false, int lifeTicks = 14, float spanRad = 2.65f)
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
            if (!initialized || SchadenfreudeMaterials.GazeSlashMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.06f;

            slashProps ??= new MaterialPropertyBlock();
            slashProps.SetColor(SchadenfreudeMaterials.PropColor, new Color(0.94f, 0.07f, 0.11f, 1.0f));
            slashProps.SetColor(SchadenfreudeMaterials.PropCoreColor, new Color(0.88f, 0.95f, 0.98f, 1.0f));
            slashProps.SetColor(SchadenfreudeMaterials.PropSparkColor, new Color(1.0f, 0.64f, 0.24f, 1.0f));
            slashProps.SetFloat(SchadenfreudeMaterials.PropProgress, t);
            slashProps.SetFloat(SchadenfreudeMaterials.PropAgeSecs, ageTicks / 60f);
            slashProps.SetFloat(SchadenfreudeMaterials.PropAlpha, 1.0f);
            slashProps.SetFloat(SchadenfreudeMaterials.PropArcSpan, arcSpan);
            slashProps.SetFloat(SchadenfreudeMaterials.PropMode, 0f);

            float sz = scale * Mathf.Lerp(0.84f, 1.18f, t);
            Mesh mesh = mirrorSweep ? MeshPool.plane10Flip : MeshPool.plane10;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.AngleAxis(angleDeg, Vector3.up),
                new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(mesh, mat, SchadenfreudeMaterials.GazeSlashMat, 0, null, 0, slashProps);
        }
    }

    /// <summary>
    /// E.G.O. 武器“凝视（Gaze）”多段电锯研磨演出驱动器（还原脑叶 LookAtMeWeapon 高频 10 连切割）：
    /// 在命中点高速旋转原作解包带血锯盘（Gaze_SawDisc.png）与猩红锯齿气旋（LC_GazeSawSlash _Mode=1），
    /// 并按错落节拍迸发 4 道黑铁猩红锯齿斩击、血雾飞溅与摩擦火星！
    /// </summary>
    public class Thing_GazeSawFlurryRunner : Thing
    {
        private Vector3 targetPos;
        private float baseAngleDeg;
        private bool empoweredByCameraGaze;
        private int ageTicks;
        private const int TotalTicks = 24;
        private int nextBeatIndex;
        private float spinAngleDeg;
        private bool initialized;

        private MaterialPropertyBlock haloProps;
        private MaterialPropertyBlock discProps;

        private static readonly int[] SlashBeatTicks = { 0, 5, 10, 15 };

        public void Initialize(Vector3 hitPos, float attackYawDeg, bool watchedBoost)
        {
            targetPos = hitPos.Yto0();
            baseAngleDeg = attackYawDeg;
            empoweredByCameraGaze = watchedBoost;
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
            spinAngleDeg = (spinAngleDeg + (empoweredByCameraGaze ? 42f : 34f)) % 360f;
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
                Vector3 jitter = new Vector3(Rand.Range(-0.20f, 0.20f), 0f, Rand.Range(-0.20f, 0.20f));
                float angle = baseAngleDeg + (idx - 1.5f) * 38f + Rand.Range(-16f, 16f);
                SchadenfreudeVfxUtility.SpawnGazeSlash(
                    Map,
                    targetPos + jitter,
                    angle,
                    scale: Rand.Range(1.30f, 1.80f) * (empoweredByCameraGaze ? 1.12f : 1.0f),
                    flipSweep: (idx % 2) == 1,
                    lifeTicks: 11,
                    spanRad: 2.65f);

                if (idx == 0 || idx == 2)
                {
                    SchadenfreudeVfxUtility.SpawnBloodBurst(Map, targetPos + jitter, angle, scale: Rand.Range(0.75f, 1.10f), lifeTicks: 16);
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
            float fadeOut = t > 0.56f ? Mathf.Clamp01(1f - (t - 0.56f) / 0.44f) : 1f;
            float alpha = fadeIn * fadeOut;
            if (alpha <= 0.01f)
            {
                return;
            }

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
            float ageSecs = ageTicks / 60f;

            // 1. Compact high-RPM crimson-steel grinding halo underneath
            if (SchadenfreudeMaterials.GazeSlashMat != null)
            {
                haloProps ??= new MaterialPropertyBlock();
                haloProps.SetColor(SchadenfreudeMaterials.PropColor, new Color(0.95f, 0.07f, 0.11f, 1.0f));
                haloProps.SetColor(SchadenfreudeMaterials.PropCoreColor, new Color(0.86f, 0.94f, 0.98f, 1.0f));
                haloProps.SetColor(SchadenfreudeMaterials.PropSparkColor, new Color(1.0f, 0.66f, 0.24f, 1.0f));
                haloProps.SetFloat(SchadenfreudeMaterials.PropProgress, t);
                haloProps.SetFloat(SchadenfreudeMaterials.PropAgeSecs, ageSecs);
                haloProps.SetFloat(SchadenfreudeMaterials.PropAlpha, alpha * 0.92f);
                haloProps.SetFloat(SchadenfreudeMaterials.PropArcSpan, 2.65f);
                haloProps.SetFloat(SchadenfreudeMaterials.PropMode, 1f);

                float hsz = Mathf.Lerp(1.30f, 1.92f, 1f - Mathf.Pow(1f - t, 2f)) * (empoweredByCameraGaze ? 1.12f : 1.0f);
                Matrix4x4 hmat = Matrix4x4.TRS(
                    new Vector3(targetPos.x, alt, targetPos.z),
                    Quaternion.identity,
                    new Vector3(hsz, 1f, hsz));
                Graphics.DrawMesh(MeshPool.plane10, hmat, SchadenfreudeMaterials.GazeSlashMat, 0, null, 0, haloProps);
            }

            // 2. Authentic unpacked Gaze circular bloody sawblade grinding into the victim at high RPM!
            if (SchadenfreudeMaterials.GazeSawDiscMat != null)
            {
                discProps ??= new MaterialPropertyBlock();
                discProps.SetColor(ShaderPropertyIDs.Color, new Color(1f, 1f, 1f, alpha * 0.95f));
                float dsz = Mathf.Lerp(0.88f, 1.24f, t) * (empoweredByCameraGaze ? 1.10f : 1.0f);
                Matrix4x4 dmat = Matrix4x4.TRS(
                    new Vector3(targetPos.x, alt + 0.01f, targetPos.z),
                    Quaternion.AngleAxis(spinAngleDeg, Vector3.up),
                    new Vector3(dsz, 1f, dsz));
                Graphics.DrawMesh(MeshPool.plane10, dmat, SchadenfreudeMaterials.GazeSawDiscMat, 0, null, 0, discProps);
            }
        }
    }

    /// <summary>幸灾乐祸被镇压后的机械崩溃消散演出（对应 LookAtMe dead：锁孔瞳光湮灭、铁壳炸出火花与血雾后凝结为收容蛋）。</summary>
    public class DyingSchadenfreude : LC_FX_Standard
    {
        private const int TotalDurationTicks = 72;
        private int startTick;
        private bool secondDeadSoundPlayed;
        private MaterialPropertyBlock auraProps;
        private MaterialPropertyBlock eyeProps;

        public override void InitWith(Pawn targetPawn)
        {
            CompAbnormality comp = targetPawn?.TryGetComp<CompAbnormality>();
            if (comp != null)
            {
                bioSignature = comp.biosignature;
            }

            startTick = Find.TickManager?.TicksGame ?? TickSpawned;
            completeTick = startTick + TotalDurationTicks;
            secondDeadSoundPlayed = false;
            hasInited = true;

            if (Map != null && Position.InBounds(Map))
            {
                LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_Dead1, this);
                Vector3 center = Position.ToVector3Shifted();
                FleckMaker.ThrowSmoke(center, Map, 1.8f);
                FleckMaker.ThrowMicroSparks(center, Map);
                SchadenfreudeVfxUtility.SpawnBloodBurst(Map, center, Rand.Range(0f, 360f), scale: 1.35f, lifeTicks: 24);
                SchadenfreudeVfxUtility.SpawnBloodBurst(Map, center, Rand.Range(0f, 360f), scale: 1.15f, lifeTicks: 22);
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
            if (!secondDeadSoundPlayed && elapsed >= 42)
            {
                secondDeadSoundPlayed = true;
                LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_Dead2, this);
                Vector3 center = Position.ToVector3Shifted();
                FleckMaker.ThrowMicroSparks(center, Map);
                FleckMaker.ThrowSmoke(center, Map, 1.3f);
            }
            else if (elapsed > 0 && elapsed < TotalDurationTicks - 10 && elapsed % 15 == 0)
            {
                Vector3 center = Position.ToVector3Shifted() + new Vector3(Rand.Range(-0.45f, 0.45f), 0f, Rand.Range(-0.35f, 0.45f));
                FleckMaker.ThrowMicroSparks(center, Map);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!hasInited || SchadenfreudeMaterials.KeyholeAuraMat == null)
            {
                return;
            }

            int elapsed = Mathf.Clamp((Find.TickManager?.TicksGame ?? startTick) - startTick, 0, TotalDurationTicks);
            float t = elapsed / (float)TotalDurationTicks;
            float alpha = Mathf.Pow(1f - t, 1.15f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f;
            auraProps ??= new MaterialPropertyBlock();
            auraProps.SetColor(SchadenfreudeMaterials.PropColor, new Color(0.55f, 0.65f, 0.72f, 1.0f));
            auraProps.SetColor(SchadenfreudeMaterials.PropDangerColor, new Color(0.92f, 0.08f, 0.12f, 1.0f));
            auraProps.SetColor(SchadenfreudeMaterials.PropCoreColor, new Color(1.0f, 0.85f, 0.85f, 1.0f));
            auraProps.SetFloat(SchadenfreudeMaterials.PropProgress, t);
            auraProps.SetFloat(SchadenfreudeMaterials.PropAgeSecs, elapsed / 60f);
            auraProps.SetFloat(SchadenfreudeMaterials.PropAlpha, alpha * 0.90f);
            auraProps.SetFloat(SchadenfreudeMaterials.PropMode, 1f);

            float sz = Mathf.Lerp(2.85f, 0.65f, t);
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(drawLoc.x, alt, drawLoc.z),
                Quaternion.identity,
                new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, mat, SchadenfreudeMaterials.KeyholeAuraMat, 0, null, 0, auraProps);

            if (SchadenfreudeMaterials.KeyholeEyeMat != null)
            {
                eyeProps ??= new MaterialPropertyBlock();
                eyeProps.SetColor(ShaderPropertyIDs.Color, new Color(1f - t * 0.6f, 0.7f * (1f - t), 0.7f * (1f - t), alpha * 0.85f));
                float eyeSz = Mathf.Lerp(1.35f, 0.35f, t);
                Matrix4x4 eyeMat = Matrix4x4.TRS(
                    new Vector3(drawLoc.x, alt + 0.02f, drawLoc.z),
                    Quaternion.identity,
                    new Vector3(eyeSz, 1f, eyeSz));
                Graphics.DrawMesh(MeshPool.plane10, eyeMat, SchadenfreudeMaterials.KeyholeEyeMat, 0, null, 0, eyeProps);
            }
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
            Scribe_Values.Look(ref startTick, "sfDeadStartTick", 0);
            Scribe_Values.Look(ref secondDeadSoundPlayed, "sfDeadSfx2", false);
        }
    }

    /// <summary>幸灾乐祸与 E.G.O. 武器“凝视”VFX 生成辅助工具。</summary>
    public static class SchadenfreudeVfxUtility
    {
        public static Thing_SchadenfreudeWatchAura SpawnWatchAura(Pawn schadenPawn, bool sawTelegraphMode)
        {
            if (schadenPawn == null || schadenPawn.MapHeld == null || !schadenPawn.PositionHeld.InBounds(schadenPawn.MapHeld) || SchadenfreudeDefOf.LC_SchadenfreudeWatchAuraFX == null)
            {
                return null;
            }

            if (GenSpawn.Spawn(SchadenfreudeDefOf.LC_SchadenfreudeWatchAuraFX, schadenPawn.PositionHeld, schadenPawn.MapHeld) is Thing_SchadenfreudeWatchAura fx)
            {
                fx.Initialize(schadenPawn, sawTelegraphMode);
                return fx;
            }

            return null;
        }

        public static Thing_SchadenfreudeKeyholePulse SpawnKeyholePulse(Map map, Vector3 pos, float scale = 3.2f, int lifeTicks = 26, bool withEyeMedallion = true)
        {
            if (map == null || SchadenfreudeDefOf.LC_SchadenfreudeKeyholePulseFX == null)
            {
                return null;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (GenSpawn.Spawn(SchadenfreudeDefOf.LC_SchadenfreudeKeyholePulseFX, cell, map) is Thing_SchadenfreudeKeyholePulse fx)
            {
                fx.Initialize(pos, scale, lifeTicks, withEyeMedallion);
                return fx;
            }

            return null;
        }

        public static Thing_SchadenfreudeSawVortex SpawnSawVortex(Pawn schadenPawn)
        {
            if (schadenPawn == null || !schadenPawn.Spawned || schadenPawn.Map == null || SchadenfreudeDefOf.LC_SchadenfreudeSawVortexFX == null)
            {
                return null;
            }

            if (GenSpawn.Spawn(SchadenfreudeDefOf.LC_SchadenfreudeSawVortexFX, schadenPawn.Position, schadenPawn.Map) is Thing_SchadenfreudeSawVortex fx)
            {
                fx.Initialize(schadenPawn);
                return fx;
            }

            return null;
        }

        public static Thing_SchadenfreudeDrillThrust SpawnDrillThrust(Map map, Vector3 fromPos, Vector3 toPos, int lifeTicks = 18)
        {
            if (map == null || SchadenfreudeDefOf.LC_SchadenfreudeDrillThrustFX == null)
            {
                return null;
            }

            IntVec3 cell = toPos.ToIntVec3().ClampInsideMap(map);
            if (GenSpawn.Spawn(SchadenfreudeDefOf.LC_SchadenfreudeDrillThrustFX, cell, map) is Thing_SchadenfreudeDrillThrust fx)
            {
                fx.Initialize(fromPos, toPos, lifeTicks);
                return fx;
            }

            return null;
        }

        public static Thing_SchadenfreudeBloodBurst SpawnBloodBurst(Map map, Vector3 pos, float yawDeg, float scale = 1.0f, int lifeTicks = 18)
        {
            if (map == null || SchadenfreudeDefOf.LC_SchadenfreudeBloodBurstFX == null)
            {
                return null;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (GenSpawn.Spawn(SchadenfreudeDefOf.LC_SchadenfreudeBloodBurstFX, cell, map) is Thing_SchadenfreudeBloodBurst fx)
            {
                fx.Initialize(pos, yawDeg, scale, lifeTicks);
                return fx;
            }

            return null;
        }

        public static Thing_GazeSawSlash SpawnGazeSlash(Map map, Vector3 pos, float yawDeg, float scale = 1.60f, bool flipSweep = false, int lifeTicks = 14, float spanRad = 2.65f)
        {
            if (map == null || SchadenfreudeDefOf.LC_GazeSawSlashFX == null)
            {
                return null;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (GenSpawn.Spawn(SchadenfreudeDefOf.LC_GazeSawSlashFX, cell, map) is Thing_GazeSawSlash fx)
            {
                fx.Initialize(pos, yawDeg, scale, flipSweep, lifeTicks, spanRad);
                return fx;
            }

            return null;
        }

        public static Thing_GazeSawFlurryRunner SpawnGazeFlurry(Map map, Vector3 hitPos, float attackYawDeg, bool watchedBoost)
        {
            if (map == null || SchadenfreudeDefOf.LC_GazeFlurryFX == null)
            {
                return null;
            }

            IntVec3 cell = hitPos.ToIntVec3().ClampInsideMap(map);
            if (GenSpawn.Spawn(SchadenfreudeDefOf.LC_GazeFlurryFX, cell, map) is Thing_GazeSawFlurryRunner fx)
            {
                fx.Initialize(hitPos, attackYawDeg, watchedBoost);
                return fx;
            }

            return null;
        }
    }
}
