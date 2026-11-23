using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.DerFreischutz
{
    /// <summary>魔弹射手专属自定义 GLSL 450 着色器与材质缓存（贯穿激光束、暗影侵蚀尾痕与双层符文传送门）。</summary>
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
            Shader portalShader = ShaderDatabase.LoadShader("Map/LC_MagicBulletPortal") ?? ShaderDatabase.MoteGlow;

            Texture2D trailOrbTex = ContentFinder<Texture2D>.Get("Things/VFX/DerFreischutz/MagicBullet_TrailOrb", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D portalOuterTex = ContentFinder<Texture2D>.Get("Things/VFX/DerFreischutz/MagicPortal_Outer", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D portalInnerTex = ContentFinder<Texture2D>.Get("Things/VFX/DerFreischutz/MagicPortal_Inner", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D portalGlowTex = ContentFinder<Texture2D>.Get("Things/VFX/DerFreischutz/MagicPortal_Glow", reportFailure: false) ?? BaseContent.WhiteTex;
            Texture2D bulletHeadTex = ContentFinder<Texture2D>.Get("Things/VFX/DerFreischutz/MagicBullet_Head", reportFailure: false) ?? BaseContent.WhiteTex;

            DarkRiftMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = darkRiftShader,
                color = new Color(0.02f, 0.03f, 0.10f, 0.88f)
            });

            BeamMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = trailOrbTex,
                shader = beamShader,
                color = new Color(0.20f, 0.62f, 1.0f, 1.0f)
            });

            PortalOuterMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = portalOuterTex,
                shader = portalShader,
                color = new Color(0.24f, 0.66f, 1.0f, 1.0f)
            });

            PortalInnerMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = portalInnerTex,
                shader = portalShader,
                color = new Color(0.38f, 0.78f, 1.0f, 1.0f)
            });

            PortalGlowMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = portalGlowTex,
                shader = ShaderDatabase.MoteGlow,
                color = new Color(0.20f, 0.56f, 1.0f, 0.85f)
            });

            BulletHeadMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = bulletHeadTex,
                shader = ShaderDatabase.MoteGlow,
                color = Color.white
            });

            TrailOrbMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = trailOrbTex,
                shader = ShaderDatabase.MoteGlow,
                color = new Color(0.45f, 0.84f, 1.0f, 1.0f)
            });
        }
    }

    /// <summary>
    /// 魔弹贯穿射线特效实体：结合暗影侵蚀底痕与自定义 GLSL 贯穿光束着色器，随弹道实时推进并残留收缩蓝电余辉。
    /// </summary>
    public class Thing_MagicBulletBeam : Thing
    {
        private Vector3 startPos;
        private Vector3 endPos;
        private Vector3 dir = Vector3.right;
        private float beamLength = 1f;
        private float beamWidth = 1.65f;
        private float curProgress;
        private float fallbackSpeedPerTick = 3.5f;
        private int ageTicks;
        private int lingerTicksAfterFull = 44;
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
            beamWidth = Mathf.Max(0.7f, width);
            fallbackSpeedPerTick = Mathf.Max(1.0f, speedCellsPerSec / 60f);
            isCursedSeventh = cursedSeventh;
            curProgress = 0.02f;
            ageTicks = 0;
            ticksSinceFull = -1;
            lingerTicksAfterFull = cursedSeventh ? 58 : 44;
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
            curProgress = 1.08f;
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
            if (curProgress < 1.05f)
            {
                curProgress = Mathf.Min(1.08f, curProgress + fallbackSpeedPerTick / beamLength);
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

            if (ticksSinceFull >= lingerTicksAfterFull || ageTicks > 240)
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
            // Smooth ease-out collapse after the piercing lance finishes sweeping
            float alpha = Mathf.Pow(1f - fadeT, 1.45f);
            if (alpha <= 0.005f)
            {
                return;
            }

            float ageSecs = ageTicks / 60f;
            Vector3 mid = (startPos + endPos) * 0.5f;
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            float widthAnim = beamWidth * Mathf.Lerp(1.05f, 0.42f, fadeT);
            float baseAlt = AltitudeLayer.MoteOverhead.AltitudeFor();

            Color beamColor = isCursedSeventh
                ? new Color(0.55f, 0.22f, 1.0f, 1.0f)
                : new Color(0.18f, 0.62f, 1.0f, 1.0f);
            Color coreColor = isCursedSeventh
                ? new Color(1.0f, 0.80f, 0.96f, 1.0f)
                : new Color(0.88f, 0.98f, 1.0f, 1.0f);
            Color riftColor = isCursedSeventh
                ? new Color(0.08f, 0.01f, 0.14f, 0.90f)
                : new Color(0.01f, 0.04f, 0.12f, 0.86f);

            // Pass 1: Dark obsidian Black-damage erosion rift beneath the beam
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
                    new Vector3(widthAnim * 1.18f, 1f, beamLength));
                Graphics.DrawMesh(MeshPool.plane10, riftMat, MagicBulletMaterials.DarkRiftMat, 0, null, 0, riftProps);
            }

            // Pass 2: Blazing custom-shader Magic Bullet piercing lance
            if (MagicBulletMaterials.BeamMat != null)
            {
                beamProps ??= new MaterialPropertyBlock();
                beamProps.SetColor(MagicBulletMaterials.PropColor, beamColor);
                beamProps.SetColor(MagicBulletMaterials.PropCoreColor, coreColor);
                beamProps.SetFloat(MagicBulletMaterials.PropProgress, curProgress);
                beamProps.SetFloat(MagicBulletMaterials.PropAgeSecs, ageSecs);
                beamProps.SetFloat(MagicBulletMaterials.PropAlpha, alpha);
                beamProps.SetFloat(MagicBulletMaterials.PropIntensity, isCursedSeventh ? 1.85f : 1.65f);

                Matrix4x4 beamMat = Matrix4x4.TRS(
                    new Vector3(mid.x, baseAlt, mid.z),
                    rot,
                    new Vector3(widthAnim, 1f, beamLength));
                Graphics.DrawMesh(MeshPool.plane10, beamMat, MagicBulletMaterials.BeamMat, 0, null, 0, beamProps);
            }

            // Pass 3: Leading supersonic spearhead flare while actively piercing across the map
            if (curProgress < 1.0f && MagicBulletMaterials.BulletHeadMat != null)
            {
                Vector3 headPos = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(curProgress));
                Matrix4x4 headMat = Matrix4x4.TRS(
                    new Vector3(headPos.x, baseAlt + 0.02f, headPos.z),
                    rot,
                    new Vector3(beamWidth * 0.85f, 1f, beamWidth * 2.15f));
                Graphics.DrawMesh(MeshPool.plane10, headMat, MagicBulletMaterials.BulletHeadMat, 0);

                if (MagicBulletMaterials.TrailOrbMat != null)
                {
                    Matrix4x4 orbMat = Matrix4x4.TRS(
                        new Vector3(headPos.x, baseAlt + 0.03f, headPos.z),
                        rot,
                        new Vector3(beamWidth * 1.25f, 1f, beamWidth * 1.55f));
                    Graphics.DrawMesh(MeshPool.plane10, orbMat, MagicBulletMaterials.TrailOrbMat, 0);
                }
            }
        }
    }

    /// <summary>
    /// 魔弹双层蓝光符文传送门特效（参考脑叶解包 FreischutzWeapon matanjin_1 & matanjin_2）：
    /// 支持蓄力展开、跟踪枪口朝向、开火瞬间能量脉冲爆发及收缩闭合。
    /// </summary>
    public class Thing_MagicBulletPortal : Thing
    {
        private Vector3 exactPos;
        private Vector3 aimDir = Vector3.right;
        private float portalScale = 1.6f;
        private bool isCursedSeventh;
        private bool isCharging;
        private bool burstTriggered;
        private int ageTicks;
        private int burstTick = -1;
        private int collapseDurationTicks = 28;
        private int maxLifetimeTicks = 180;
        private bool initialized;

        // Optional warmup tracking for E.G.O. Weapon colonist caster
        private Pawn trackedCaster;
        private Verb trackedVerb;
        private LocalTargetInfo trackedTarget;
        private float muzzleOffsetDist = 0.95f;

        private MaterialPropertyBlock outerProps;
        private MaterialPropertyBlock innerProps;
        private MaterialPropertyBlock glowProps;

        public void InitializeStatic(Vector3 pos, Vector3 dir, float scale, bool charging, bool cursedSeventh = false)
        {
            exactPos = pos.Yto0();
            aimDir = dir.Yto0().sqrMagnitude > 0.001f ? dir.Yto0().normalized : Vector3.right;
            portalScale = Mathf.Max(0.6f, scale);
            isCharging = charging;
            isCursedSeventh = cursedSeventh;
            burstTriggered = !charging;
            ageTicks = 0;
            burstTick = charging ? -1 : 0;
            collapseDurationTicks = charging ? 28 : 34;
            maxLifetimeTicks = charging ? 240 : 40;
            trackedCaster = null;
            trackedVerb = null;
            initialized = true;
        }

        public void InitializeForWarmup(Pawn caster, Verb verb, LocalTargetInfo target, float scale = 1.15f)
        {
            trackedCaster = caster;
            trackedVerb = verb;
            trackedTarget = target;
            muzzleOffsetDist = 0.92f;
            Vector3 cPos = caster != null ? caster.DrawPos.Yto0() : Position.ToVector3Shifted().Yto0();
            Vector3 tPos = target.IsValid ? target.CenterVector3.Yto0() : (cPos + Vector3.right);
            Vector3 d = (tPos - cPos).Yto0();
            if (d.sqrMagnitude < 0.01f) d = Vector3.right;
            d.Normalize();

            InitializeStatic(cPos + d * muzzleOffsetDist, d, scale, charging: true, cursedSeventh: false);
            maxLifetimeTicks = 220;
        }

        public void UpdateAnchor(Vector3 pos, Vector3 dir)
        {
            exactPos = pos.Yto0();
            if (dir.Yto0().sqrMagnitude > 0.001f)
            {
                aimDir = dir.Yto0().normalized;
            }
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
                collapseDurationTicks = 12;
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
            float pulse = 1.15f + 0.15f * Mathf.Sin(ageSecs * 18f);

            if (burstTriggered && burstTick >= 0)
            {
                float postT = Mathf.Clamp01((ageTicks - burstTick) / (float)Mathf.Max(1, collapseDurationTicks));
                // Flash flare on firing, then crisp collapse
                float flashBoost = Mathf.Exp(-postT * 6.5f) * 1.45f;
                pulse = 1.25f + flashBoost;
                scaleMult = Mathf.Lerp(1.22f, 0.05f, Mathf.Pow(postT, 1.35f));
                alpha = Mathf.Pow(1f - postT, 1.25f);
            }
            else if (isCharging)
            {
                // Subtle arcane vibration just like FreischutzWeapon Attack_01
                float jitter = 1f + 0.04f * Mathf.Sin(ageSecs * 34f);
                scaleMult *= jitter;
            }

            if (alpha <= 0.01f || scaleMult <= 0.01f)
            {
                return;
            }

            // Align the tall elliptical runic circle perpendicular to aimDir (its thin axis along aimDir, wide axis perpendicular!)
            // On MagicPortal_Outer/Inner, the ellipse is tall along V (local Z) and narrow along U (local X).
            // Rotating LookRotation(aimDir) by +90° puts local X along aimDir and local Z perpendicular to aimDir!
            Quaternion portalRot = Quaternion.LookRotation(aimDir, Vector3.up) * Quaternion.AngleAxis(90f, Vector3.up);
            float baseAlt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;

            Color mainCol = isCursedSeventh
                ? new Color(0.62f, 0.24f, 1.0f, 1.0f)
                : new Color(0.24f, 0.68f, 1.0f, 1.0f);
            Color rimCol = isCursedSeventh
                ? new Color(1.0f, 0.82f, 0.96f, 1.0f)
                : new Color(0.86f, 0.98f, 1.0f, 1.0f);

            // Layer 0: Soft cobalt-cyan radial glow halo behind the rings
            if (MagicBulletMaterials.PortalGlowMat != null)
            {
                glowProps ??= new MaterialPropertyBlock();
                Color glowCol = mainCol;
                glowCol.a = alpha * 0.65f;
                glowProps.SetColor(MagicBulletMaterials.PropColor, glowCol);

                float glowSize = portalScale * scaleMult * 1.35f;
                Matrix4x4 glowMat = Matrix4x4.TRS(
                    new Vector3(exactPos.x, baseAlt - 0.01f, exactPos.z),
                    portalRot,
                    new Vector3(glowSize * 0.68f, 1f, glowSize));
                Graphics.DrawMesh(MeshPool.plane10, glowMat, MagicBulletMaterials.PortalGlowMat, 0, null, 0, glowProps);
            }

            // Layer 1: Outer larger runic gate (matanjin_2)
            if (MagicBulletMaterials.PortalOuterMat != null)
            {
                outerProps ??= new MaterialPropertyBlock();
                outerProps.SetColor(MagicBulletMaterials.PropColor, mainCol);
                outerProps.SetColor(MagicBulletMaterials.PropRimColor, rimCol);
                outerProps.SetFloat(MagicBulletMaterials.PropAgeSecs, ageSecs);
                outerProps.SetFloat(MagicBulletMaterials.PropAlpha, alpha);
                outerProps.SetFloat(MagicBulletMaterials.PropPulse, pulse);

                float outerSz = portalScale * scaleMult * 1.15f;
                Matrix4x4 outerMat = Matrix4x4.TRS(
                    new Vector3(exactPos.x, baseAlt, exactPos.z),
                    portalRot,
                    new Vector3(outerSz, 1f, outerSz));
                Graphics.DrawMesh(MeshPool.plane10, outerMat, MagicBulletMaterials.PortalOuterMat, 0, null, 0, outerProps);
            }

            // Layer 2: Inner forward runic gate (matanjin_1) slightly ahead along aimDir (matching bone vs bone2 depth!)
            if (MagicBulletMaterials.PortalInnerMat != null)
            {
                float innerOpen = Mathf.Clamp01((ageTicks - 4) / 10f);
                float innerScale = (burstTriggered ? scaleMult * 0.78f : scaleMult * 0.72f * innerOpen);
                if (innerScale > 0.02f)
                {
                    innerProps ??= new MaterialPropertyBlock();
                    innerProps.SetColor(MagicBulletMaterials.PropColor, mainCol);
                    innerProps.SetColor(MagicBulletMaterials.PropRimColor, rimCol);
                    innerProps.SetFloat(MagicBulletMaterials.PropAgeSecs, -ageSecs * 1.35f);
                    innerProps.SetFloat(MagicBulletMaterials.PropAlpha, alpha * 0.95f);
                    innerProps.SetFloat(MagicBulletMaterials.PropPulse, pulse * 1.15f);

                    Vector3 innerPos = exactPos + aimDir * (0.28f * portalScale * scaleMult);
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

    /// <summary>魔弹贯穿目标瞬间生成的蓝黑裂隙冲击特效。</summary>
    public class Thing_MagicBulletHitBurst : Thing
    {
        private Vector3 exactPos;
        private Vector3 shotDir = Vector3.right;
        private float scale = 1.25f;
        private bool isCursedSeventh;
        private int ageTicks;
        private const int DurationTicks = 20;
        private bool initialized;

        private MaterialPropertyBlock ringProps;
        private MaterialPropertyBlock flashProps;

        public void Initialize(Vector3 pos, Vector3 dir, float burstScale = 1.25f, bool cursedSeventh = false)
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
            float alpha = Mathf.Pow(1f - t, 1.4f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float alt = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
            Quaternion alongRot = Quaternion.LookRotation(shotDir, Vector3.up);
            Quaternion perpRot = alongRot * Quaternion.AngleAxis(90f, Vector3.up);

            Color col = isCursedSeventh
                ? new Color(0.68f, 0.28f, 1.0f, alpha)
                : new Color(0.28f, 0.72f, 1.0f, alpha);
            Color rim = isCursedSeventh
                ? new Color(1.0f, 0.85f, 0.98f, alpha)
                : new Color(0.90f, 0.99f, 1.0f, alpha);

            // Expanding pierced shock ring perpendicular to the trajectory
            if (MagicBulletMaterials.PortalInnerMat != null)
            {
                ringProps ??= new MaterialPropertyBlock();
                ringProps.SetColor(MagicBulletMaterials.PropColor, col);
                ringProps.SetColor(MagicBulletMaterials.PropRimColor, rim);
                ringProps.SetFloat(MagicBulletMaterials.PropAgeSecs, ageTicks / 60f);
                ringProps.SetFloat(MagicBulletMaterials.PropAlpha, alpha);
                ringProps.SetFloat(MagicBulletMaterials.PropPulse, 1.85f * (1f - t * 0.5f));

                float ringSz = scale * Mathf.Lerp(0.45f, 1.55f, 1f - Mathf.Pow(1f - t, 2f));
                Matrix4x4 ringMat = Matrix4x4.TRS(
                    new Vector3(exactPos.x, alt, exactPos.z),
                    perpRot,
                    new Vector3(ringSz, 1f, ringSz));
                Graphics.DrawMesh(MeshPool.plane10, ringMat, MagicBulletMaterials.PortalInnerMat, 0, null, 0, ringProps);
            }

            // Intense elongated entry/exit flare along the piercing axis
            if (MagicBulletMaterials.TrailOrbMat != null)
            {
                flashProps ??= new MaterialPropertyBlock();
                flashProps.SetColor(MagicBulletMaterials.PropColor, rim);

                float sx = scale * Mathf.Lerp(0.75f, 0.15f, t);
                float sz = scale * Mathf.Lerp(1.6f, 3.2f, t);
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

        public static Thing_MagicBulletPortal SpawnWarmupPortal(Map map, Pawn caster, Verb verb, LocalTargetInfo target, float scale = 1.15f)
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

        public static void SpawnHitBurst(Map map, Vector3 pos, Vector3 dir, float scale = 1.25f, bool cursedSeventh = false)
        {
            if (map == null || DerFreischutzDefOf.LC_MagicBulletHitFX == null)
            {
                return;
            }

            IntVec3 anchor = pos.ToIntVec3().ClampInsideMap(map);
            Thing_MagicBulletHitBurst burst = (Thing_MagicBulletHitBurst)GenSpawn.Spawn(DerFreischutzDefOf.LC_MagicBulletHitFX, anchor, map);
            burst.Initialize(pos, dir, scale, cursedSeventh);

            Color smokeCol = cursedSeventh
                ? new Color(0.32f, 0.14f, 0.52f, 0.85f)
                : new Color(0.12f, 0.24f, 0.56f, 0.85f);
            FleckMaker.ThrowDustPuffThick(pos, map, 1.0f, smokeCol);
            FleckMaker.ThrowMicroSparks(pos, map);
        }
    }

    /// <summary>
    /// 魔弹 E.G.O. 武器专用射击动词：瞄准蓄力期间在枪口展开双层蓝光符文魔法阵，击发瞬间引爆传送门并释放贯穿魔弹光束。
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

                activeWarmupPortal = MagicBulletVfxUtility.SpawnWarmupPortal(CasterPawn.MapHeld, CasterPawn, this, castTarg, 1.12f);
                TargetInfo castLoc = new TargetInfo(CasterPawn.PositionHeld, CasterPawn.MapHeld);
                if (DerFreischutzDefOf.LC_DerFreischutz_Portal != null)
                {
                    DerFreischutzDefOf.LC_DerFreischutz_Portal.PlayOneShot(castLoc);
                }
                if (DerFreischutzDefOf.LC_DerFreischutz_WeaponCharge != null)
                {
                    DerFreischutzDefOf.LC_DerFreischutz_WeaponCharge.PlayOneShot(castLoc);
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

            return base.TryCastShot();
        }
    }
}
