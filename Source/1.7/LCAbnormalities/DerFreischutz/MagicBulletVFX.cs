using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.DerFreischutz
{
    /// <summary>魔弹射手专属自定义 GLSL 450 着色器与材质缓存（克制写实的贯穿魔弹拖尾、暗影尾迹与双层钢蓝符文传送门）。</summary>
    [StaticConstructorOnStartup]
    public static class MagicBulletMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropRimColor = Shader.PropertyToID("_RimColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropIntensity = Shader.PropertyToID("_Intensity");
        public static readonly int PropPulse = Shader.PropertyToID("_Pulse");

        public static readonly Material DarkRiftMat;
        public static readonly Material BeamMat;
        public static readonly Material PortalOuterMat;
        public static readonly Material PortalInnerMat;
        public static readonly Material PortalGlowMat;
        public static readonly Material BulletHeadMat;
        public static readonly Material TrailOrbMat;

        static MagicBulletMaterials()
        {
            Shader darkRiftShader = ShaderDatabase.LoadShader("Map/LC_MagicBulletDarkRift") ?? ShaderDatabase.Transparent;
            Shader beamShader = ShaderDatabase.LoadShader("Map/LC_MagicBulletBeam") ?? ShaderDatabase.MoteGlow;
            Shader portalShader = ShaderDatabase.LoadShader("Map/LC_MagicBulletPortal") ?? ShaderDatabase.Transparent;

            Texture2D trailOrbTex = ContentFinder<Texture2D>.Get("Things/VFX/DerFreischutz/MagicBullet_TrailOrb", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D portalOuterTex = ContentFinder<Texture2D>.Get("Things/VFX/DerFreischutz/MagicPortal_Outer", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D portalInnerTex = ContentFinder<Texture2D>.Get("Things/VFX/DerFreischutz/MagicPortal_Inner", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D portalGlowTex = ContentFinder<Texture2D>.Get("Things/VFX/DerFreischutz/MagicPortal_Glow", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D bulletHeadTex = ContentFinder<Texture2D>.Get("Things/VFX/DerFreischutz/MagicBullet_Head", reportFailure: false) ?? BaseContent.WhiteTex;

            DarkRiftMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = darkRiftShader,
                color = new Color(0.02f, 0.04f, 0.11f, 0.55f)
            });

            BeamMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = trailOrbTex,
                shader = beamShader,
                color = new Color(0.18f, 0.48f, 0.92f, 1.0f)
            });

            PortalOuterMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = portalOuterTex,
                shader = portalShader,
                color = new Color(0.32f, 0.56f, 0.88f, 1.0f)
            });

            PortalInnerMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = portalInnerTex,
                shader = portalShader,
                color = new Color(0.42f, 0.68f, 0.96f, 1.0f)
            });

            PortalGlowMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = portalGlowTex,
                shader = ShaderDatabase.MoteGlow,
                color = new Color(0.18f, 0.44f, 0.85f, 0.35f)
            });

            // Use Transparent shader so FreischutzBullet.png preserves its iconic obsidian-black core inside its cobalt-white rim!
            BulletHeadMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = bulletHeadTex,
                shader = ShaderDatabase.Transparent,
                color = Color.white
            });

            TrailOrbMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = trailOrbTex,
                shader = ShaderDatabase.MoteGlow,
                color = new Color(0.32f, 0.68f, 1.0f, 0.65f)
            });
        }
    }

    /// <summary>
    /// 魔弹贯穿拖尾特效实体：贴合脑叶公司原版表现——高速黑蓝魔弹本体拖曳修长收尖的冷蓝幽光尾迹与淡雅侵蚀残烟，
    /// 随弹头掠过后自然收束消散，避免过度浮夸的全屏激光炮观感。
    /// </summary>
    public class Thing_MagicBulletBeam : Thing
    {
        private Vector3 startPos;
        private Vector3 endPos;
        private Vector3 dir = Vector3.right;
        private float beamLength = 1f;
        private float beamWidth = 0.68f;
        private float curProgress;
        private float fallbackSpeedPerTick = 3.2f;
        private int ageTicks;
        private int lingerTicksAfterFull = 26;
        private int ticksSinceFull = -1;
        private bool isCursedSeventh;
        private bool initialized;

        private MaterialPropertyBlock riftProps;
        private MaterialPropertyBlock beamProps;

        public void Initialize(Vector3 start, Vector3 end, float width, float speedCellsPerSec, bool cursedSeventh = false)
        {
            startPos = start.Yto0();
            endPos = end.Yto0();
            Vector3 delta = endPos - startPos;
            beamLength = Mathf.Max(0.5f, delta.magnitude);
            dir = delta / beamLength;
            beamWidth = Mathf.Max(0.38f, width);
            fallbackSpeedPerTick = Mathf.Max(1.2f, speedCellsPerSec / 60f);
            isCursedSeventh = cursedSeventh;
            curProgress = 0.01f;
            ageTicks = 0;
            ticksSinceFull = -1;
            lingerTicksAfterFull = cursedSeventh ? 34 : 26;
            initialized = true;
        }

        public void SyncHeadPosition(Vector3 worldPos)
        {
            if (!initialized || beamLength <= 0.01f)
            {
                return;
            }

            float along = Vector3.Dot((worldPos.Yto0() - startPos), dir);
            float p = Mathf.Clamp01(along / beamLength);
            if (p > curProgress)
            {
                curProgress = p;
            }
        }

        public void NotifyReachedEnd()
        {
            if (curProgress < 1.0f)
            {
                curProgress = 1.0f;
            }
            if (ticksSinceFull < 0)
            {
                ticksSinceFull = 0;
            }
        }

        protected override void Tick()
        {
            if (!initialized)
            {
                Destroy();
                return;
            }

            ageTicks++;

            // Continue advancing curProgress smoothly past 1.0 so the finite-length comet tail naturally sweeps out the far end!
            float maxTailOvershoot = 1.0f + Mathf.Clamp(12.5f / Mathf.Max(beamLength, 1f), 0.15f, 1.4f);
            if (curProgress < maxTailOvershoot)
            {
                curProgress = Mathf.Min(maxTailOvershoot, curProgress + fallbackSpeedPerTick / beamLength);
            }

            if (curProgress >= 1.0f)
            {
                if (ticksSinceFull < 0)
                {
                    ticksSinceFull = 0;
                }
                else
                {
                    ticksSinceFull++;
                }
            }

            if (ticksSinceFull >= lingerTicksAfterFull || ageTicks > 200)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || beamLength <= 0.05f)
            {
                return;
            }

            float fadeT = ticksSinceFull < 0 ? 0f : Mathf.Clamp01(ticksSinceFull / (float)lingerTicksAfterFull);
            float alpha = Mathf.Pow(1f - fadeT, 1.5f);
            if (alpha <= 0.005f)
            {
                return;
            }

            float ageSecs = ageTicks / 60f;
            Vector3 mid = (startPos + endPos) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            float widthAnim = beamWidth * Mathf.Lerp(1.0f, 0.65f, fadeT);
            float baseAlt = AltitudeLayer.MoteOverhead.AltitudeFor();

            Color beamColor = isCursedSeventh
                ? new Color(0.38f, 0.24f, 0.80f, 1.0f)
                : new Color(0.18f, 0.48f, 0.90f, 1.0f);
            Color coreColor = isCursedSeventh
                ? new Color(0.86f, 0.72f, 0.98f, 1.0f)
                : new Color(0.76f, 0.91f, 1.0f, 1.0f);
            Color riftColor = isCursedSeventh
                ? new Color(0.06f, 0.02f, 0.12f, 0.58f)
                : new Color(0.02f, 0.04f, 0.10f, 0.52f);

            // Pass 1: Subtle dark-indigo smoke/erosion wake directly behind the bullet
            if (MagicBulletMaterials.DarkRiftMat != null)
            {
                riftProps ??= new MaterialPropertyBlock();
                riftProps.SetColor(MagicBulletMaterials.PropColor, riftColor);
                riftProps.SetFloat(MagicBulletMaterials.PropProgress, curProgress);
                riftProps.SetFloat(MagicBulletMaterials.PropAgeSecs, ageSecs);
                riftProps.SetFloat(MagicBulletMaterials.PropAlpha, alpha);

                Matrix4x4 riftMat = Matrix4x4.TRS(
                    new Vector3(mid.x, baseAlt - 0.02f, mid.z),
                    rot,
                    new Vector3(widthAnim * 1.08f, 1f, beamLength));
                Graphics.DrawMesh(MeshPool.plane10, riftMat, MagicBulletMaterials.DarkRiftMat, 0, null, 0, riftProps);
            }

            // Pass 2: Sleek tapered blue comet trail + delicate residual vapor thread
            if (MagicBulletMaterials.BeamMat != null)
            {
                beamProps ??= new MaterialPropertyBlock();
                beamProps.SetColor(MagicBulletMaterials.PropColor, beamColor);
                beamProps.SetColor(MagicBulletMaterials.PropCoreColor, coreColor);
                beamProps.SetFloat(MagicBulletMaterials.PropProgress, curProgress);
                beamProps.SetFloat(MagicBulletMaterials.PropAgeSecs, ageSecs);
                beamProps.SetFloat(MagicBulletMaterials.PropAlpha, alpha);
                beamProps.SetFloat(MagicBulletMaterials.PropIntensity, isCursedSeventh ? 1.15f : 1.0f);

                Matrix4x4 beamMat = Matrix4x4.TRS(
                    new Vector3(mid.x, baseAlt, mid.z),
                    rot,
                    new Vector3(widthAnim, 1f, beamLength));
                Graphics.DrawMesh(MeshPool.plane10, beamMat, MagicBulletMaterials.BeamMat, 0, null, 0, beamProps);
            }

            // Pass 3: Crisp obsidian-black & cobalt Magic Bullet head (FreischutzBullet.png) streaking at the tip!
            if (curProgress < 1.0f && MagicBulletMaterials.BulletHeadMat != null)
            {
                Vector3 headPos = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(curProgress));
                float bulletW = beamWidth * 0.58f;
                float bulletL = beamWidth * 1.48f;
                Matrix4x4 headMat = Matrix4x4.TRS(
                    new Vector3(headPos.x, baseAlt + 0.02f, headPos.z),
                    rot,
                    new Vector3(bulletW, 1f, bulletL));
                Graphics.DrawMesh(MeshPool.plane10, headMat, MagicBulletMaterials.BulletHeadMat, 0);
            }
        }
    }

    /// <summary>
    /// 魔弹双层钢蓝符文传送门特效（参考脑叶解包 FreischutzWeapon matanjin_1 & matanjin_2）：
    /// 还原原版细腻克制的几何符文质感，蓄力展开并在击发瞬间利落收束闭合。
    /// </summary>
    public class Thing_MagicBulletPortal : Thing
    {
        private Vector3 exactPos;
        private Vector3 aimDir = Vector3.right;
        private float portalScale = 1.12f;
        private bool isCursedSeventh;
        private bool isCharging;
        private bool burstTriggered;
        private int ageTicks;
        private int burstTick = -1;
        private int collapseDurationTicks = 14;
        private int maxLifetimeTicks = 180;
        private bool initialized;

        // Optional warmup tracking for E.G.O. Weapon colonist caster
        private Pawn trackedCaster;
        private Verb trackedVerb;
        private LocalTargetInfo trackedTarget;
        private float muzzleOffsetDist = 0.88f;

        private MaterialPropertyBlock outerProps;
        private MaterialPropertyBlock innerProps;
        private MaterialPropertyBlock glowProps;

        public void InitializeStatic(Vector3 pos, Vector3 dir, float scale, bool charging, bool cursedSeventh = false)
        {
            exactPos = pos.Yto0();
            aimDir = dir.Yto0().sqrMagnitude > 0.001f ? dir.Yto0().normalized : Vector3.right;
            portalScale = Mathf.Max(0.45f, scale);
            isCharging = charging;
            isCursedSeventh = cursedSeventh;
            burstTriggered = !charging;
            ageTicks = 0;
            burstTick = charging ? -1 : 0;
            collapseDurationTicks = charging ? 14 : 20;
            maxLifetimeTicks = charging ? 220 : 28;
            trackedCaster = null;
            trackedVerb = null;
            initialized = true;
        }

        public void InitializeForWarmup(Pawn caster, Verb verb, LocalTargetInfo target, float scale = 0.82f)
        {
            trackedCaster = caster;
            trackedVerb = verb;
            trackedTarget = target;
            muzzleOffsetDist = 0.86f;
            Vector3 cPos = caster != null ? caster.DrawPos.Yto0() : Position.ToVector3Shifted().Yto0();
            Vector3 tPos = target.IsValid ? target.CenterVector3.Yto0() : (cPos + Vector3.right);
            Vector3 d = (tPos - cPos).Yto0();
            if (d.sqrMagnitude < 0.01f) d = Vector3.right;
            d.Normalize();

            InitializeStatic(cPos + d * muzzleOffsetDist, d, scale, charging: true, cursedSeventh: false);
            maxLifetimeTicks = 200;
        }

        public void TriggerFireBurst()
        {
            if (burstTriggered)
            {
                return;
            }

            isCharging = false;
            burstTriggered = true;
            burstTick = ageTicks;
        }

        public void CancelEarly()
        {
            if (!burstTriggered)
            {
                isCharging = false;
                burstTriggered = true;
                burstTick = ageTicks;
                collapseDurationTicks = 10;
            }
        }

        protected override void Tick()
        {
            if (!initialized)
            {
                Destroy();
                return;
            }

            ageTicks++;

            // Track E.G.O. weapon colonist warmup stance
            if (trackedCaster != null && !burstTriggered)
            {
                if (trackedCaster.Destroyed || trackedCaster.Dead || !trackedCaster.Spawned)
                {
                    CancelEarly();
                }
                else
                {
                    Stance_Warmup warmup = trackedCaster.stances?.curStance as Stance_Warmup;
                    if (warmup == null || (trackedVerb != null && warmup.verb != trackedVerb))
                    {
                        CancelEarly();
                    }
                    else
                    {
                        Vector3 cPos = trackedCaster.DrawPos.Yto0();
                        Vector3 tPos = trackedTarget.IsValid ? trackedTarget.CenterVector3.Yto0() : (cPos + aimDir);
                        Vector3 d = (tPos - cPos).Yto0();
                        if (d.sqrMagnitude > 0.01f)
                        {
                            aimDir = d.normalized;
                        }
                        exactPos = cPos + aimDir * muzzleOffsetDist;
                    }
                }
            }

            if (burstTriggered && burstTick >= 0 && (ageTicks - burstTick) >= collapseDurationTicks)
            {
                Destroy();
                return;
            }

            if (ageTicks >= maxLifetimeTicks)
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
            float openFrac = Mathf.Clamp01(ageTicks / 12f);
            float openEase = 1f - Mathf.Pow(1f - openFrac, 3f);

            float scaleMult = openEase;
            float alpha = openEase;
            float pulse = 0.85f + 0.12f * Mathf.Sin(ageSecs * 14f);

            if (burstTriggered && burstTick >= 0)
            {
                float postT = Mathf.Clamp01((ageTicks - burstTick) / (float)Mathf.Max(1, collapseDurationTicks));
                // Composed crisp flash then sharp collapse matching FreischutzWeapon Attack_01
                pulse = 1.0f + Mathf.Exp(-postT * 7.0f) * 0.45f;
                scaleMult = Mathf.Lerp(1.06f, 0.02f, Mathf.Pow(postT, 1.3f));
                alpha = Mathf.Pow(1f - postT, 1.35f);
            }
            else if (isCharging)
            {
                float jitter = 1f + 0.025f * Mathf.Sin(ageSecs * 26f);
                scaleMult *= jitter;
            }

            if (alpha <= 0.01f || scaleMult <= 0.01f)
            {
                return;
            }

            Quaternion portalRot = Quaternion.LookRotation(aimDir, Vector3.up) * Quaternion.AngleAxis(90f, Vector3.up);
            float baseAlt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;

            Color mainCol = isCursedSeventh
                ? new Color(0.46f, 0.30f, 0.82f, 1.0f)
                : new Color(0.32f, 0.56f, 0.88f, 1.0f);
            Color rimCol = isCursedSeventh
                ? new Color(0.86f, 0.74f, 0.98f, 1.0f)
                : new Color(0.76f, 0.90f, 1.0f, 1.0f);

            // Layer 0: Restrained soft cobalt halo behind the geometric rings
            if (MagicBulletMaterials.PortalGlowMat != null)
            {
                glowProps ??= new MaterialPropertyBlock();
                Color glowCol = mainCol;
                glowCol.a = alpha * 0.24f;
                glowProps.SetColor(MagicBulletMaterials.PropColor, glowCol);

                float glowSize = portalScale * scaleMult * 1.12f;
                Matrix4x4 glowMat = Matrix4x4.TRS(
                    new Vector3(exactPos.x, baseAlt - 0.01f, exactPos.z),
                    portalRot,
                    new Vector3(glowSize * 0.58f, 1f, glowSize));
                Graphics.DrawMesh(MeshPool.plane10, glowMat, MagicBulletMaterials.PortalGlowMat, 0, null, 0, glowProps);
            }

            // Layer 1: Outer runic gate (matanjin_2)
            if (MagicBulletMaterials.PortalOuterMat != null)
            {
                outerProps ??= new MaterialPropertyBlock();
                outerProps.SetColor(MagicBulletMaterials.PropColor, mainCol);
                outerProps.SetColor(MagicBulletMaterials.PropRimColor, rimCol);
                outerProps.SetFloat(MagicBulletMaterials.PropAgeSecs, ageSecs);
                outerProps.SetFloat(MagicBulletMaterials.PropAlpha, alpha * 0.92f);
                outerProps.SetFloat(MagicBulletMaterials.PropPulse, pulse);

                float outerSz = portalScale * scaleMult * 1.05f;
                Matrix4x4 outerMat = Matrix4x4.TRS(
                    new Vector3(exactPos.x, baseAlt, exactPos.z),
                    portalRot,
                    new Vector3(outerSz, 1f, outerSz));
                Graphics.DrawMesh(MeshPool.plane10, outerMat, MagicBulletMaterials.PortalOuterMat, 0, null, 0, outerProps);
            }

            // Layer 2: Inner forward runic gate (matanjin_1) slightly ahead along aimDir
            if (MagicBulletMaterials.PortalInnerMat != null)
            {
                float innerOpen = Mathf.Clamp01((ageTicks - 4) / 10f);
                float innerScale = (burstTriggered ? scaleMult * 0.68f : scaleMult * 0.64f * innerOpen);
                if (innerScale > 0.02f)
                {
                    innerProps ??= new MaterialPropertyBlock();
                    innerProps.SetColor(MagicBulletMaterials.PropColor, mainCol);
                    innerProps.SetColor(MagicBulletMaterials.PropRimColor, rimCol);
                    innerProps.SetFloat(MagicBulletMaterials.PropAgeSecs, -ageSecs * 1.25f);
                    innerProps.SetFloat(MagicBulletMaterials.PropAlpha, alpha * 0.88f);
                    innerProps.SetFloat(MagicBulletMaterials.PropPulse, pulse);

                    Vector3 innerPos = exactPos + aimDir * (0.24f * portalScale * scaleMult);
                    float innerSz = portalScale * innerScale;
                    Matrix4x4 innerMat = Matrix4x4.TRS(
                        new Vector3(innerPos.x, baseAlt + 0.015f, innerPos.z),
                        portalRot,
                        new Vector3(innerSz, 1f, innerSz));
                    Graphics.DrawMesh(MeshPool.plane10, innerMat, MagicBulletMaterials.PortalInnerMat, 0, null, 0, innerProps);
                }
            }
        }
    }

    /// <summary>魔弹贯穿目标瞬间生成的克制蓝白穿透微闪特效。</summary>
    public class Thing_MagicBulletHitBurst : Thing
    {
        private Vector3 exactPos;
        private Vector3 shotDir = Vector3.right;
        private float scale = 0.55f;
        private bool isCursedSeventh;
        private int ageTicks;
        private const int DurationTicks = 12;
        private bool initialized;

        private MaterialPropertyBlock flashProps;

        public void Initialize(Vector3 pos, Vector3 dir, float burstScale = 0.55f, bool cursedSeventh = false)
        {
            exactPos = pos.Yto0();
            shotDir = dir.Yto0().sqrMagnitude > 0.001f ? dir.Yto0().normalized : Vector3.right;
            scale = burstScale;
            isCursedSeventh = cursedSeventh;
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
            float alpha = Mathf.Pow(1f - t, 1.6f) * 0.72f;
            if (alpha <= 0.01f)
            {
                return;
            }

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
            Quaternion alongRot = Quaternion.LookRotation(shotDir, Vector3.up);

            Color glintCol = isCursedSeventh
                ? new Color(0.68f, 0.42f, 0.96f, alpha)
                : new Color(0.45f, 0.76f, 1.0f, alpha);

            // Clean, sharp directional piercing glint along the bullet trajectory
            if (MagicBulletMaterials.TrailOrbMat != null)
            {
                flashProps ??= new MaterialPropertyBlock();
                flashProps.SetColor(MagicBulletMaterials.PropColor, glintCol);

                float sx = scale * Mathf.Lerp(0.42f, 0.12f, t);
                float sz = scale * Mathf.Lerp(1.25f, 2.15f, t);
                Matrix4x4 slashMat = Matrix4x4.TRS(
                    new Vector3(exactPos.x, alt + 0.01f, exactPos.z),
                    alongRot,
                    new Vector3(sx, 1f, sz));
                Graphics.DrawMesh(MeshPool.plane10, slashMat, MagicBulletMaterials.TrailOrbMat, 0, null, 0, flashProps);
            }
        }
    }

    /// <summary>魔弹 VFX 生成辅助工具。</summary>
    public static class MagicBulletVfxUtility
    {
        public static Thing_MagicBulletBeam SpawnBeam(Map map, Vector3 start, Vector3 end, float width, float speedCellsPerSec, bool cursedSeventh = false)
        {
            if (map == null || DerFreischutzDefOf.LC_MagicBulletBeamFX == null)
            {
                return null;
            }

            IntVec3 anchor = start.ToIntVec3().ClampInsideMap(map);
            Thing_MagicBulletBeam beam = (Thing_MagicBulletBeam)GenSpawn.Spawn(DerFreischutzDefOf.LC_MagicBulletBeamFX, anchor, map);
            beam.Initialize(start, end, width, speedCellsPerSec, cursedSeventh);
            return beam;
        }

        public static Thing_MagicBulletPortal SpawnPortal(Map map, Vector3 pos, Vector3 dir, float scale, bool charging, bool cursedSeventh = false)
        {
            if (map == null || DerFreischutzDefOf.LC_MagicBulletPortalFX == null)
            {
                return null;
            }

            IntVec3 anchor = pos.ToIntVec3().ClampInsideMap(map);
            Thing_MagicBulletPortal portal = (Thing_MagicBulletPortal)GenSpawn.Spawn(DerFreischutzDefOf.LC_MagicBulletPortalFX, anchor, map);
            portal.InitializeStatic(pos, dir, scale, charging, cursedSeventh);
            return portal;
        }

        public static Thing_MagicBulletPortal SpawnWarmupPortal(Map map, Pawn caster, Verb verb, LocalTargetInfo target, float scale = 0.82f)
        {
            if (map == null || caster == null || DerFreischutzDefOf.LC_MagicBulletPortalFX == null)
            {
                return null;
            }

            IntVec3 anchor = caster.Position.ClampInsideMap(map);
            Thing_MagicBulletPortal portal = (Thing_MagicBulletPortal)GenSpawn.Spawn(DerFreischutzDefOf.LC_MagicBulletPortalFX, anchor, map);
            portal.InitializeForWarmup(caster, verb, target, scale);
            return portal;
        }

        public static void SpawnHitBurst(Map map, Vector3 pos, Vector3 dir, float scale = 0.55f, bool cursedSeventh = false)
        {
            if (map == null || DerFreischutzDefOf.LC_MagicBulletHitFX == null)
            {
                return;
            }

            IntVec3 anchor = pos.ToIntVec3().ClampInsideMap(map);
            Thing_MagicBulletHitBurst burst = (Thing_MagicBulletHitBurst)GenSpawn.Spawn(DerFreischutzDefOf.LC_MagicBulletHitFX, anchor, map);
            burst.Initialize(pos, dir, scale, cursedSeventh);

            Color smokeCol = cursedSeventh
                ? new Color(0.24f, 0.12f, 0.42f, 0.65f)
                : new Color(0.10f, 0.20f, 0.42f, 0.65f);
            FleckMaker.ThrowDustPuffThick(pos, map, 0.62f, smokeCol);
        }
    }

    /// <summary>
    /// 魔弹 E.G.O. 武器专用射击动词：瞄准蓄力期间在枪口展开克制的双层钢蓝符文魔法阵，击发瞬间收束闭合。
    /// </summary>
    public class Verb_ShootMagicBullet : Verb_Shoot
    {
        private Thing_MagicBulletPortal activeWarmupPortal;

        public override bool TryStartCastOn(
            LocalTargetInfo castTarg,
            LocalTargetInfo destTarg,
            bool surpriseAttack = false,
            bool canHitNonTargetPawns = true,
            bool preventFriendlyFire = false,
            bool nonInterruptingSelfCast = false)
        {
            bool started = base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
            if (started && CasterIsPawn && CasterPawn?.MapHeld != null && WarmupTime > 0f)
            {
                if (activeWarmupPortal != null && !activeWarmupPortal.Destroyed)
                {
                    activeWarmupPortal.CancelEarly();
                }

                activeWarmupPortal = MagicBulletVfxUtility.SpawnWarmupPortal(CasterPawn.MapHeld, CasterPawn, this, castTarg, 0.82f);
                TargetInfo castLoc = new TargetInfo(CasterPawn.PositionHeld, CasterPawn.MapHeld);
                if (DerFreischutzDefOf.LC_DerFreischutz_Portal != null)
                {
                    DerFreischutzDefOf.LC_DerFreischutz_Portal.PlayOneShot(castLoc);
                }
                if (DerFreischutzDefOf.LC_DerFreischutz_WeaponCharge != null)
                {
                    DerFreischutzDefOf.LC_DerFreischutz_WeaponCharge.PlayOneShot(castLoc);
                }

                ThingWithComps weapon = CasterPawn.equipment?.Primary;
                if (weapon != null && weapon.def?.defName == MagicBulletWeaponAnimHandler.WeaponDefName
                    && CasterPawn.stances?.curStance is Stance_Warmup warmup && warmup.verb == this
                    && !EgoWeaponAnimationManager.IsPlaying(CasterPawn, MagicBulletWeaponAnimHandler.ClipSkillWarmup)
                    && !EgoWeaponAnimationManager.IsPlaying(CasterPawn, MagicBulletWeaponAnimHandler.ClipSkillSalvo))
                {
                    Vector3 targetPos = castTarg.IsValid ? castTarg.CenterVector3 : (CasterPawn.DrawPos + Vector3.right);
                    float aim = EgoWeaponAnimationUtility.ResolveAimAngle(CasterPawn, targetPos);
                    int warmupTicks = Mathf.Max(10, warmup.ticksLeft + 2);
                    EgoWeaponAnimationManager.Play(CasterPawn, weapon, MagicBulletWeaponAnimHandler.ClipRangeWarmup, warmupTicks, aim, requiredWarmupVerb: this);
                }
            }

            return started;
        }

        protected override bool TryCastShot()
        {
            if (activeWarmupPortal != null && !activeWarmupPortal.Destroyed)
            {
                activeWarmupPortal.TriggerFireBurst();
                activeWarmupPortal = null;
            }

            bool fired = base.TryCastShot();
            if (fired && CasterIsPawn && CasterPawn?.equipment?.Primary is ThingWithComps weapon &&
                weapon.def?.defName == MagicBulletWeaponAnimHandler.WeaponDefName &&
                !EgoWeaponAnimationManager.IsPlaying(CasterPawn, MagicBulletWeaponAnimHandler.ClipSkillWarmup) &&
                !EgoWeaponAnimationManager.IsPlaying(CasterPawn, MagicBulletWeaponAnimHandler.ClipSkillSalvo))
            {
                Vector3 targetPos = currentTarget.IsValid
                    ? (currentTarget.HasThing && currentTarget.Thing.Spawned ? currentTarget.Thing.DrawPos.Yto0() : currentTarget.CenterVector3.Yto0())
                    : (CasterPawn.DrawPos.Yto0() + Vector3.right);
                float aim = EgoWeaponAnimationUtility.ResolveAimAngle(CasterPawn, targetPos);
                EgoWeaponAnimationManager.Play(CasterPawn, weapon, MagicBulletWeaponAnimHandler.ClipRangeShot, 38, aim);
            }

            return fired;
        }
    }
}
