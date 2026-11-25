using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.OldBeliefAndPromise
{
    [DefOf]
    public static class OldBeliefVfxDefOf
    {
        public static ThingDef LC_OldBeliefOracleAuraFX;
        public static ThingDef LC_OldBeliefResultBurstFX;
        public static ThingDef LC_OldBeliefOrbTransferFX;

        static OldBeliefVfxDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(OldBeliefVfxDefOf));
        }
    }

    /// <summary>T-09-97 古老的信念与承诺：解包多部件材质与 GLSL 450 预言灵光着色器缓存。</summary>
    [StaticConstructorOnStartup]
    public static class OldBeliefMaterials
    {
        private const string PartsPrefix = "Things/Building/OldBeliefAndPromise/Parts/";

        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");

        public static readonly Material OracleAuraMat;
        public static readonly Material TetherBeamMat;

        public static readonly Material Line1Mat;
        public static readonly Material Line2Mat;
        public static readonly Material Line3Mat;

        public static readonly Material LizardUpDefaultMat;
        public static readonly Material LizardUpDoingMat;
        public static readonly Material LizardUpSuccessMat;
        public static readonly Material LizardUpEyeMat;

        public static readonly Material LizardDownDefaultMat;
        public static readonly Material LizardDownDoingMat;
        public static readonly Material LizardDownSuccessMat;
        public static readonly Material LizardDownEyeMat;

        public static readonly Material BallDefaultMat;
        public static readonly Material BallDoingMat;
        public static readonly Material BallSuccessMat;
        public static readonly Material BallDoingLightMat;
        public static readonly Material BallSuccessLightMat;
        public static readonly Material BallSuccessLightSmallMat;
        public static readonly Material SuccessLightMat;
        public static readonly Material Reflected1Mat;
        public static readonly Material Reflected2Mat;
        public static readonly Material Dark1Mat;
        public static readonly Material Dark2Mat;
        public static readonly Material[] DistortionMats;

        public static readonly Material MarbleSuccessMat;
        public static readonly Material MarbleDarkMat;

        static OldBeliefMaterials()
        {
            Shader oracleShader = ShaderDatabase.LoadShader("Map/LC_OldBeliefOracleAura") ?? ShaderDatabase.MoteGlow;
            OracleAuraMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = oracleShader,
                color = new Color(0.22f, 0.80f, 1.0f, 1.0f)
            });

            TetherBeamMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = ShaderDatabase.MoteGlow,
                color = new Color(0.32f, 0.85f, 1.0f, 0.45f)
            });

            Line1Mat = LoadPartMat("Line1", ShaderDatabase.Transparent);
            Line2Mat = LoadPartMat("Line2", ShaderDatabase.Transparent);
            Line3Mat = LoadPartMat("Line3", ShaderDatabase.Transparent);

            LizardUpDefaultMat = LoadPartMat("Lizard_Up_Default", ShaderDatabase.Transparent);
            LizardUpDoingMat = LoadPartMat("Lizard_Up_Doing", ShaderDatabase.Transparent);
            LizardUpSuccessMat = LoadPartMat("Lizard_Up_Success", ShaderDatabase.Transparent);
            LizardUpEyeMat = LoadPartMat("Lizard_Up_Eye", ShaderDatabase.Transparent);

            LizardDownDefaultMat = LoadPartMat("Lizard_Down_Default", ShaderDatabase.Transparent);
            LizardDownDoingMat = LoadPartMat("Lizard_Down_Doing", ShaderDatabase.Transparent);
            LizardDownSuccessMat = LoadPartMat("Lizard_Down_Success", ShaderDatabase.Transparent);
            LizardDownEyeMat = LoadPartMat("Lizard_Down_Eye", ShaderDatabase.Transparent);

            BallDefaultMat = LoadPartMat("Ball_Default", ShaderDatabase.Transparent);
            BallDoingMat = LoadPartMat("Ball_Doing", ShaderDatabase.Transparent);
            BallSuccessMat = LoadPartMat("Ball_Success", ShaderDatabase.Transparent);

            BallDoingLightMat = LoadPartMat("Ball_Doing_Light", ShaderDatabase.MoteGlow);
            BallSuccessLightMat = LoadPartMat("Ball_Success_Light", ShaderDatabase.MoteGlow);
            BallSuccessLightSmallMat = LoadPartMat("Ball_Success_Light_Small", ShaderDatabase.MoteGlow);
            SuccessLightMat = LoadPartMat("Success_Light", ShaderDatabase.MoteGlow);

            Reflected1Mat = LoadPartMat("Reflected_1", ShaderDatabase.MoteGlow);
            Reflected2Mat = LoadPartMat("Reflected_2", ShaderDatabase.MoteGlow);
            Dark1Mat = LoadPartMat("Dark_1", ShaderDatabase.Transparent);
            Dark2Mat = LoadPartMat("Dark_2", ShaderDatabase.Transparent);

            DistortionMats = new[]
            {
                LoadPartMat("Distortion_1", ShaderDatabase.Transparent),
                LoadPartMat("Distortion_2", ShaderDatabase.Transparent),
                LoadPartMat("Distortion_3", ShaderDatabase.Transparent)
            };

            MarbleSuccessMat = LoadPartMat("Marble_Success", ShaderDatabase.Transparent);
            MarbleDarkMat = LoadPartMat("Marble_Dark", ShaderDatabase.Transparent);
        }

        private static Material LoadPartMat(string partName, Shader shader)
        {
            Texture2D tex = ContentFinder<Texture2D>.Get(PartsPrefix + partName, reportFailure: false) ?? BaseContent.WhiteTex;
            return MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = tex,
                shader = shader,
                color = Color.white
            });
        }
    }

    /// <summary>
    /// 古老的信念与承诺仪式旋涡灵光：
    /// 交互祈愿（3_Doing）期间在神谕水晶球与祈愿者之间呈现青蓝旋转旋涡与细微能量牵引；
    /// 祈愿结束后随赐福金光或背叛暗蚀自然淡出。
    /// </summary>
    public class Thing_OldBeliefOracleAura : Thing
    {
        private Thing hostEntity;
        private int ageTicks;
        private bool initialized;
        private MaterialPropertyBlock auraProps;
        private MaterialPropertyBlock beamProps;

        public Thing HostEntity => hostEntity;

        public void Initialize(Thing entity)
        {
            hostEntity = entity;
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || hostEntity == null || hostEntity.Destroyed || Map == null)
            {
                Destroy();
                return;
            }

            CompOldBeliefAndPromise comp = (hostEntity as ThingWithComps)?.TryGetComp<CompOldBeliefAndPromise>();
            if (comp == null || !comp.IsOperational || comp.ActiveMap != Map || !comp.IsVisualSessionActive)
            {
                Destroy();
                return;
            }

            ageTicks++;
            IntVec3 pos = comp.ActivePosition;
            if (pos.IsValid && pos.InBounds(Map) && Position != pos)
            {
                Position = pos;
            }

            if (comp.VisualPhase == OldBeliefVisualPhase.Doing && ageTicks % 14 == 0)
            {
                Vector3 center = comp.ActiveDrawPos;
                FleckMaker.ThrowDustPuffThick(
                    center + new Vector3(Rand.Range(-0.45f, 0.45f), 0f, Rand.Range(-0.25f, 0.45f)),
                    Map,
                    0.55f,
                    new Color(0.32f, 0.86f, 1.0f));
            }
            else if (comp.VisualPhase == OldBeliefVisualPhase.Success && ageTicks % 18 == 0)
            {
                Vector3 center = comp.ActiveDrawPos;
                FleckMaker.ThrowDustPuffThick(
                    center + new Vector3(Rand.Range(-0.5f, 0.5f), 0f, Rand.Range(-0.2f, 0.5f)),
                    Map,
                    0.58f,
                    new Color(1.0f, 0.88f, 0.34f));
            }
            else if (comp.VisualPhase == OldBeliefVisualPhase.Fail && ageTicks % 16 == 0)
            {
                Vector3 center = comp.ActiveDrawPos;
                FleckMaker.ThrowDustPuffThick(
                    center + new Vector3(Rand.Range(-0.45f, 0.45f), 0f, Rand.Range(-0.2f, 0.45f)),
                    Map,
                    0.62f,
                    new Color(0.24f, 0.14f, 0.38f));
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || hostEntity == null || OldBeliefMaterials.OracleAuraMat == null)
            {
                return;
            }

            CompOldBeliefAndPromise comp = (hostEntity as ThingWithComps)?.TryGetComp<CompOldBeliefAndPromise>();
            if (comp == null || !comp.IsVisualSessionActive)
            {
                return;
            }

            Vector3 anchor = comp.ActiveDrawPos.Yto0();
            float ageSecs = ageTicks / 60f;
            float fadeIn = Mathf.Clamp01(ageTicks / 14f);
            float alpha = fadeIn;
            float size = 2.65f;
            Color mainCol;
            Color coreCol;
            float intensity;
            float mode = 0f;

            switch (comp.VisualPhase)
            {
                case OldBeliefVisualPhase.Doing:
                    mainCol = new Color(0.20f, 0.78f, 1.0f, 0.92f);
                    coreCol = new Color(0.92f, 0.98f, 1.0f, 1.0f);
                    intensity = 1.25f;
                    size = 2.65f + 0.14f * Mathf.Sin(ageSecs * 7.0f);
                    break;
                case OldBeliefVisualPhase.Success:
                    mainCol = new Color(1.0f, 0.82f, 0.24f, 0.95f);
                    coreCol = new Color(1.0f, 0.98f, 0.82f, 1.0f);
                    alpha *= comp.ResultFadeAlpha;
                    intensity = 1.35f;
                    size = 2.95f;
                    break;
                default:
                    mainCol = new Color(0.42f, 0.24f, 0.74f, 0.92f);
                    coreCol = new Color(0.75f, 0.62f, 1.0f, 0.95f);
                    alpha *= comp.ResultFadeAlpha;
                    intensity = 1.25f;
                    size = 2.75f;
                    break;
            }

            if (alpha <= 0.01f)
            {
                return;
            }

            auraProps ??= new MaterialPropertyBlock();
            auraProps.SetColor(OldBeliefMaterials.PropColor, mainCol);
            auraProps.SetColor(OldBeliefMaterials.PropCoreColor, coreCol);
            auraProps.SetFloat(OldBeliefMaterials.PropProgress, 0.5f);
            auraProps.SetFloat(OldBeliefMaterials.PropAgeSecs, ageSecs);
            auraProps.SetFloat(OldBeliefMaterials.PropAlpha, alpha * 0.82f);
            auraProps.SetFloat(OldBeliefMaterials.PropIntensity, intensity);
            auraProps.SetFloat(OldBeliefMaterials.PropMode, mode);

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() - 0.03f;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(anchor.x, alt, anchor.z + 0.12f),
                Quaternion.identity,
                new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, mat, OldBeliefMaterials.OracleAuraMat, 0, null, 0, auraProps);

            // Subtle channeling energy beam connecting the praying/oathing colonist to the sphere
            Pawn user = comp.ActiveChanneler;
            if (comp.VisualPhase == OldBeliefVisualPhase.Doing && user != null && user.Spawned && user.Map == Map && OldBeliefMaterials.TetherBeamMat != null)
            {
                Vector3 spherePos = anchor + new Vector3(0f, 0f, 0.15f);
                Vector3 userPos = user.DrawPos.Yto0() + new Vector3(0f, 0f, 0.25f);
                Vector3 delta = userPos - spherePos;
                float len = delta.magnitude;
                if (len > 0.25f)
                {
                    Vector3 mid = (spherePos + userPos) * 0.5f;
                    Quaternion rot = Quaternion.LookRotation(delta / len, Vector3.up);
                    float width = 0.11f + 0.05f * Mathf.Sin(ageSecs * 10.5f);

                    beamProps ??= new MaterialPropertyBlock();
                    beamProps.SetColor(OldBeliefMaterials.PropColor, new Color(0.32f, 0.88f, 1.0f, 0.42f * fadeIn));

                    Matrix4x4 beamMat = Matrix4x4.TRS(
                        new Vector3(mid.x, alt + 0.01f, mid.z),
                        rot,
                        new Vector3(width, 1f, len));
                    Graphics.DrawMesh(MeshPool.plane10, beamMat, OldBeliefMaterials.TetherBeamMat, 0, null, 0, beamProps);
                }
            }
        }
    }

    /// <summary>古老的信念与承诺裁决爆发波：赐福成功时绽放圣金星芒，背叛毁灭时炸裂暗紫蚀裂冲击波。</summary>
    public class Thing_OldBeliefResultBurst : Thing
    {
        private Vector3 exactPos;
        private float maxDiameter = 5.4f;
        private int durationTicks = 44;
        private int ageTicks;
        private bool isBetrayal;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 pos, bool betrayal, float radius = 2.8f, int duration = 44)
        {
            exactPos = pos.Yto0();
            isBetrayal = betrayal;
            maxDiameter = Mathf.Max(1.8f, radius * 2.0f);
            durationTicks = Mathf.Max(18, duration);
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
            if (!initialized || OldBeliefMaterials.OracleAuraMat == null)
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

            Color mainCol = isBetrayal
                ? new Color(0.46f, 0.20f, 0.85f, 1.0f)
                : new Color(1.0f, 0.82f, 0.22f, 1.0f);
            Color coreCol = isBetrayal
                ? new Color(0.90f, 0.78f, 1.0f, 1.0f)
                : new Color(1.0f, 0.99f, 0.86f, 1.0f);

            props ??= new MaterialPropertyBlock();
            props.SetColor(OldBeliefMaterials.PropColor, mainCol);
            props.SetColor(OldBeliefMaterials.PropCoreColor, coreCol);
            props.SetFloat(OldBeliefMaterials.PropProgress, easeOut);
            props.SetFloat(OldBeliefMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(OldBeliefMaterials.PropAlpha, alpha);
            props.SetFloat(OldBeliefMaterials.PropIntensity, isBetrayal ? 1.55f : 1.48f);
            props.SetFloat(OldBeliefMaterials.PropMode, isBetrayal ? 2.0f : 1.0f);

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;
            Matrix4x4 mat = Matrix4x4.TRS(
                new Vector3(exactPos.x, alt, exactPos.z),
                Quaternion.identity,
                new Vector3(maxDiameter, 1f, maxDiameter));
            Graphics.DrawMesh(MeshPool.plane10, mat, OldBeliefMaterials.OracleAuraMat, 0, null, 0, props);
        }
    }

    /// <summary>
    /// 原作 Spine（2_Get_marble / 5_Sucess_give_marble / 4_Fail）中的弹珠献祭与返还飞光特效：
    /// 献祭武器精神球飞入水晶球，成功后圣金宝珠（Marble_s）飞返员工并爆发金芒，失败时黑紫碎珠（Marble_d）于员工头顶崩裂。
    /// </summary>
    public class Thing_OldBeliefOrbTransfer : Thing
    {
        private Vector3 startPos;
        private Vector3 endPos;
        private Pawn trackEndPawn;
        private bool isGoldenSuccess;
        private bool shatterOnEnd;
        private int ageTicks;
        private int durationTicks = 32;
        private bool initialized;
        private MaterialPropertyBlock orbProps;
        private MaterialPropertyBlock glowProps;

        public void Initialize(Vector3 from, Vector3 to, Pawn endPawn, bool goldenSuccess, bool shatter, int duration = 32)
        {
            startPos = from.Yto0();
            endPos = to.Yto0();
            trackEndPawn = endPawn;
            isGoldenSuccess = goldenSuccess;
            shatterOnEnd = shatter;
            durationTicks = Mathf.Max(14, duration);
            ageTicks = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || Map == null)
            {
                Destroy();
                return;
            }

            if (trackEndPawn != null && trackEndPawn.Spawned && trackEndPawn.Map == Map)
            {
                endPos = trackEndPawn.DrawPos.Yto0() + new Vector3(0f, 0f, 0.35f);
            }

            ageTicks++;
            Vector3 cur = CurrentWorldPos();

            if (ageTicks % 4 == 0)
            {
                Color dust = isGoldenSuccess
                    ? new Color(1.0f, 0.86f, 0.28f)
                    : (shatterOnEnd ? new Color(0.32f, 0.16f, 0.52f) : new Color(0.38f, 0.85f, 1.0f));
                FleckMaker.ThrowDustPuffThick(cur, Map, 0.42f, dust);
            }

            if (ageTicks >= durationTicks)
            {
                if (isGoldenSuccess)
                {
                    FleckMaker.ThrowMicroSparks(endPos, Map);
                    FleckMaker.ThrowDustPuffThick(endPos, Map, 1.25f, new Color(1.0f, 0.88f, 0.34f));
                }
                else if (shatterOnEnd)
                {
                    FleckMaker.ThrowMicroSparks(endPos, Map);
                    FleckMaker.ThrowDustPuffThick(endPos, Map, 1.45f, new Color(0.24f, 0.12f, 0.40f));
                }
                Destroy();
            }
        }

        private Vector3 CurrentWorldPos()
        {
            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float smooth = t * t * (3f - 2f * t);
            Vector3 pos = Vector3.Lerp(startPos, endPos, smooth);
            pos.z += Mathf.Sin(t * Mathf.PI) * 0.55f;
            return pos;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized)
            {
                return;
            }

            float t = Mathf.Clamp01(ageTicks / (float)durationTicks);
            float alpha = Mathf.Clamp01(ageTicks / 6f) * Mathf.Clamp01((durationTicks - ageTicks) / 6f + 0.35f);
            Vector3 pos = CurrentWorldPos();
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.08f;

            // Additive glow behind the flying marble
            if (OldBeliefMaterials.BallSuccessLightSmallMat != null)
            {
                Color glowCol = isGoldenSuccess
                    ? new Color(1.0f, 0.88f, 0.36f, 0.85f * alpha)
                    : (shatterOnEnd
                        ? new Color(0.54f, 0.24f, 0.90f, 0.82f * alpha)
                        : new Color(0.32f, 0.88f, 1.0f, 0.78f * alpha));

                glowProps ??= new MaterialPropertyBlock();
                glowProps.SetColor(OldBeliefMaterials.PropColor, glowCol);

                float glowSize = 0.72f + 0.14f * Mathf.Sin(t * Mathf.PI * 3f);
                Matrix4x4 gMat = Matrix4x4.TRS(
                    new Vector3(pos.x, alt - 0.005f, pos.z),
                    Quaternion.identity,
                    new Vector3(glowSize, 1f, glowSize));
                Graphics.DrawMesh(MeshPool.plane10, gMat, OldBeliefMaterials.BallSuccessLightSmallMat, 0, null, 0, glowProps);
            }

            Material orbMat = isGoldenSuccess ? OldBeliefMaterials.MarbleSuccessMat : OldBeliefMaterials.MarbleDarkMat;
            if (orbMat != null)
            {
                float orbSize = shatterOnEnd
                    ? Mathf.Lerp(0.48f, 0.20f, Mathf.Pow(t, 2f))
                    : 0.44f + 0.08f * Mathf.Sin(t * Mathf.PI);
                orbProps ??= new MaterialPropertyBlock();
                orbProps.SetColor(OldBeliefMaterials.PropColor, new Color(1f, 1f, 1f, alpha));

                Matrix4x4 oMat = Matrix4x4.TRS(
                    new Vector3(pos.x, alt, pos.z),
                    Quaternion.AngleAxis(ageTicks * (shatterOnEnd ? 14f : 5f), Vector3.up),
                    new Vector3(orbSize, 1f, orbSize));
                Graphics.DrawMesh(MeshPool.plane10, oMat, orbMat, 0, null, 0, orbProps);
            }
        }
    }

    /// <summary>古老的信念与承诺特效生成工具。</summary>
    public static class OldBeliefVfxUtility
    {
        public static void EnsureOracleAura(Thing hostEntity, Map map, IntVec3 pos)
        {
            if (hostEntity == null || map == null || !pos.IsValid || !pos.InBounds(map) || OldBeliefVfxDefOf.LC_OldBeliefOracleAuraFX == null)
            {
                return;
            }

            var existing = map.listerThings?.ThingsOfDef(OldBeliefVfxDefOf.LC_OldBeliefOracleAuraFX);
            if (existing != null)
            {
                for (int i = 0; i < existing.Count; i++)
                {
                    if (existing[i] is Thing_OldBeliefOracleAura aura && !aura.Destroyed && aura.HostEntity == hostEntity)
                    {
                        return;
                    }
                }
            }

            Thing_OldBeliefOracleAura spawned = (Thing_OldBeliefOracleAura)GenSpawn.Spawn(
                OldBeliefVfxDefOf.LC_OldBeliefOracleAuraFX, pos, map);
            spawned.Initialize(hostEntity);
        }

        public static void SpawnResultBurst(Map map, Vector3 pos, bool betrayal, float radius = 2.8f, int duration = 44)
        {
            if (map == null || OldBeliefVfxDefOf.LC_OldBeliefResultBurstFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            Thing_OldBeliefResultBurst burst = (Thing_OldBeliefResultBurst)GenSpawn.Spawn(
                OldBeliefVfxDefOf.LC_OldBeliefResultBurstFX, cell, map);
            burst.Initialize(pos, betrayal, radius, duration);

            if (betrayal)
            {
                FleckMaker.ThrowDustPuffThick(pos, map, 1.8f, new Color(0.20f, 0.10f, 0.36f));
                FleckMaker.ThrowMicroSparks(pos, map);
                if (Find.CurrentMap == map && Find.CameraDriver != null)
                {
                    Find.CameraDriver.shaker.DoShake(0.48f);
                }
            }
            else
            {
                FleckMaker.ThrowDustPuffThick(pos, map, 1.6f, new Color(1.0f, 0.88f, 0.34f));
                FleckMaker.ThrowMicroSparks(pos, map);
            }
        }

        public static void SpawnOrbTransfer(Map map, Vector3 from, Vector3 to, Pawn trackEndPawn, bool goldenSuccess, bool shatter, int duration = 32)
        {
            if (map == null || OldBeliefVfxDefOf.LC_OldBeliefOrbTransferFX == null)
            {
                return;
            }

            IntVec3 cell = from.ToIntVec3().ClampInsideMap(map);
            Thing_OldBeliefOrbTransfer orb = (Thing_OldBeliefOrbTransfer)GenSpawn.Spawn(
                OldBeliefVfxDefOf.LC_OldBeliefOrbTransferFX, cell, map);
            orb.Initialize(from, to, trackEndPawn, goldenSuccess, shatter, duration);
        }
    }
}
