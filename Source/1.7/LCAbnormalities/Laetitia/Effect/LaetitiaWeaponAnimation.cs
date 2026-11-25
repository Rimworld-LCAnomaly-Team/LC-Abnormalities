using System.Collections.Generic;
using Laetitia.Comp;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Laetitia.Effect
{
    /// <summary>O-01-67 蕾蒂希娅 E.G.O. 武器专属定制 GLSL 着色器与绸带曳光材质缓存。</summary>
    [StaticConstructorOnStartup]
    public static class LaetitiaMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");

        public static readonly Material HeartBurstMat;
        public static readonly Material RibbonMat;

        static LaetitiaMaterials()
        {
            Shader heartShader = ShaderDatabase.LoadShader("Map/LC_LaetitiaHeartBurst") ?? ShaderDatabase.Transparent;
            HeartBurstMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = heartShader,
                color = new Color(0.95f, 0.24f, 0.58f, 1.0f)
            });

            RibbonMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = ShaderDatabase.MoteGlow,
                color = Color.white
            });
        }
    }

    /// <summary>
    /// 蕾蒂希娅（EgoWeapon_Laetitia）专属武器动画处理器：
    /// 贴图原图呈 0° 水平朝右。
    /// 支持复古卡宾步枪清脆点射回弹（ClipRangeShot）、近战枪托推击（ClipMeleeBash），
    /// 以及主动技能「送出礼物」的优雅心蕾蓄能（ClipSkillWarmup）与双拍连射礼赞（ClipSkillGift）。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class LaetitiaWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_Laetitia";

        public const string ClipRangeShot = "Laetitia_RangeShot";
        public const string ClipMeleeBash = "Laetitia_MeleeBash";
        public const string ClipSkillWarmup = "Laetitia_SkillWarmup";
        public const string ClipSkillGift = "Laetitia_SkillGift";

        private static readonly Vector2 RifleDrawSize = new Vector2(1.95f, 1.95f);
        private static readonly Color MagentaGhostTint = new Color(0.96f, 0.30f, 0.65f, 0.80f);

        // === Range Shot (28 ticks): crisp carbine recoil tilt -> graceful recover ===
        private static readonly BodyKeyframe[] RangeShotBody =
        {
            new BodyKeyframe(0f, -0.05f, 0f, -4f),
            new BodyKeyframe(6f, -0.09f, 0.01f, -8f),
            new BodyKeyframe(16f, 0f, 0f, 0f),
            new BodyKeyframe(28f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] RangeShotRifle =
        {
            new WeaponKeyframe(0f, 0.36f, -0.04f, -4f, 1.00f),
            new WeaponKeyframe(5f, 0.26f, 0.12f, -24f, 1.08f),
            new WeaponKeyframe(15f, 0.38f, -0.02f, -4f, 1.03f),
            new WeaponKeyframe(28f, 0.30f, -0.10f, 8f, 1.00f)
        };

        // === Melee Bash (44 ticks): short stock swing ===
        private static readonly BodyKeyframe[] MeleeBashBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(8f, 0.16f, -0.01f, 14f),
            new BodyKeyframe(22f, 0.10f, 0f, 8f),
            new BodyKeyframe(44f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] MeleeBashRifle =
        {
            new WeaponKeyframe(0f, 0.12f, 0.26f, -44f, 1.00f),
            new WeaponKeyframe(8f, 0.56f, -0.04f, 22f, 1.06f),
            new WeaponKeyframe(22f, 0.44f, -0.10f, 16f, 1.02f),
            new WeaponKeyframe(44f, 0.30f, -0.10f, 8f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): raise rifle proudly as heart-lace aura gathers ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.05f, 0.01f, -5f),
            new BodyKeyframe(1.00f, -0.09f, 0.02f, -9f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupRifle =
        {
            new WeaponKeyframe(0.00f, 0.30f, -0.10f, 8f, 1.00f),
            new WeaponKeyframe(0.45f, 0.38f, 0.04f, -10f, 1.05f),
            new WeaponKeyframe(1.00f, 0.42f, 0.05f, -4f, 1.08f)
        };

        // === Skill Gift (74 ticks): 2-beat enchanted volley recoil @ tick 0 and tick 16 ===
        private static readonly BodyKeyframe[] SkillGiftBody =
        {
            new BodyKeyframe(0f, -0.08f, 0.01f, -8f),
            new BodyKeyframe(6f, -0.12f, 0.02f, -12f),
            new BodyKeyframe(13f, -0.04f, 0f, -3f),
            new BodyKeyframe(18f, -0.14f, 0.02f, -14f),
            new BodyKeyframe(36f, 0.02f, 0f, 2f),
            new BodyKeyframe(74f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillGiftRifle =
        {
            new WeaponKeyframe(0f, 0.34f, 0.08f, -14f, 1.08f),
            new WeaponKeyframe(6f, 0.25f, 0.16f, -26f, 1.12f),
            new WeaponKeyframe(13f, 0.38f, 0.04f, -6f, 1.06f),
            new WeaponKeyframe(18f, 0.22f, 0.19f, -32f, 1.16f),
            new WeaponKeyframe(38f, 0.38f, -0.02f, -2f, 1.04f),
            new WeaponKeyframe(74f, 0.30f, -0.10f, 8f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillGift))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipMeleeBash, 44, aimAngle);
        }

        public EgoWeaponBodyPose EvaluateBodyPose(EgoWeaponAnimPlayback playback, float elapsedTicks)
        {
            if (playback == null)
            {
                return EgoWeaponBodyPose.None;
            }

            BodyKeyframe kf;
            switch (playback.ClipId)
            {
                case ClipRangeShot:
                    kf = EgoWeaponAnimationUtility.SampleBody(RangeShotBody, elapsedTicks);
                    break;
                case ClipMeleeBash:
                    kf = EgoWeaponAnimationUtility.SampleBody(MeleeBashBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillGift:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillGiftBody, elapsedTicks);
                    break;
                default:
                    return EgoWeaponBodyPose.None;
            }

            return EgoWeaponAnimationUtility.ToWorldBodyPose(kf.OffsetX, kf.OffsetZ, kf.LeanDeg, playback.FacingLeft);
        }

        public bool TryDrawWeapon(in EgoWeaponDrawContext ctx)
        {
            Material mat = ctx.Weapon.Graphic?.MatSingleFor(ctx.Weapon);
            if (mat == null)
            {
                return false;
            }

            float alt = ctx.BaseDrawPos.y + 0.015f;

            if (ctx.ActiveAnim == null)
            {
                bool moving = ctx.Pawn.pather != null && ctx.Pawn.pather.Moving;
                float tickPhase = (Find.TickManager?.TicksGame ?? 0) * 0.10f;
                float sway = Mathf.Sin(tickPhase) * (moving ? 4f : 1.8f);
                float posX = moving ? 0.28f : 0.30f;
                float posZ = (moving ? -0.04f : -0.10f) + Mathf.Sin(tickPhase * 0.75f) * 0.010f;
                float angle = (moving ? 0f : 8f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, RifleDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipRangeShot:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(RangeShotRifle, t);
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, RifleDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipMeleeBash:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(MeleeBashRifle, t);
                    if (t >= 4f && t <= 16f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, MeleeBashRifle, t, ctx.BaseDrawPos, alt, RifleDrawSize, ctx.FacingLeft, MagentaGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, RifleDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupRifle, prog);
                    float pulse = Mathf.Sin(t * 1.4f) * 0.009f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ + pulse, kf.AngleDeg, RifleDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillGift:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillGiftRifle, t);
                    if (t <= 24f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillGiftRifle, t, ctx.BaseDrawPos, alt, RifleDrawSize, ctx.FacingLeft, MagentaGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, RifleDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    public static class LaetitiaVfxUtility
    {
        public static void SpawnHeartBloom(Map map, Vector3 pos, float scale = 1.0f, int lifeTicks = 16)
        {
            SpawnHeartBurstInternal(map, pos, mode: 0f, scale: scale, lifeTicks: lifeTicks);
        }

        public static void SpawnHeartbreakBurst(Map map, Vector3 pos, float scale = 4.2f, int lifeTicks = 34)
        {
            SpawnHeartBurstInternal(map, pos, mode: 1f, scale: scale, lifeTicks: lifeTicks);
        }

        private static void SpawnHeartBurstInternal(Map map, Vector3 pos, float mode, float scale, int lifeTicks)
        {
            if (map == null || Laetitia.Def.ThingDefOf.LC_LaetitiaHeartBurstFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(Laetitia.Def.ThingDefOf.LC_LaetitiaHeartBurstFX, cell, map) is Thing_LaetitiaHeartBurst burst)
            {
                burst.Initialize(pos, mode, scale, lifeTicks);
            }
        }

        public static void SpawnRibbonTracer(Map map, Vector3 start, Vector3 end, bool empowered = false)
        {
            if (map == null || Laetitia.Def.ThingDefOf.LC_LaetitiaRibbonTracerFX == null)
            {
                return;
            }

            IntVec3 cell = start.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(Laetitia.Def.ThingDefOf.LC_LaetitiaRibbonTracerFX, cell, map) is Thing_LaetitiaRibbonTracer tracer)
            {
                tracer.Initialize(start, end, empowered);
            }
        }
    }

    /// <summary>蕾蒂希娅心芒绽放（Mode 0）与碎心黑蕾丝范围爆震（Mode 1）渲染载体。</summary>
    public class Thing_LaetitiaHeartBurst : Thing
    {
        private Vector3 worldPos;
        private float mode;
        private float scale = 1.0f;
        private int ageTicks;
        private int maxTicks = 16;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 pos, float shaderMode, float drawScale, int lifeTicks)
        {
            worldPos = pos.Yto0();
            mode = shaderMode;
            scale = Mathf.Max(0.4f, drawScale);
            ageTicks = 0;
            maxTicks = Mathf.Max(8, lifeTicks);
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || Map == null)
            {
                Destroy();
                return;
            }

            if (++ageTicks >= maxTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || LaetitiaMaterials.HeartBurstMat == null)
            {
                return;
            }

            float prog = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, maxTicks));
            props ??= new MaterialPropertyBlock();
            props.SetColor(LaetitiaMaterials.PropColor, mode < 0.5f ? new Color(0.96f, 0.24f, 0.58f, 1.0f) : new Color(0.90f, 0.14f, 0.48f, 1.0f));
            props.SetColor(LaetitiaMaterials.PropCoreColor, new Color(0.18f, 0.03f, 0.22f, 1.0f));
            props.SetFloat(LaetitiaMaterials.PropProgress, prog);
            props.SetFloat(LaetitiaMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(LaetitiaMaterials.PropAlpha, 1.0f);
            props.SetFloat(LaetitiaMaterials.PropMode, mode);

            Vector3 p = worldPos;
            p.y = AltitudeLayer.MoteOverhead.AltitudeFor() + (mode < 0.5f ? 0.06f : 0.04f);
            float sz = scale * (mode < 0.5f ? Mathf.Lerp(0.85f, 1.35f, prog) : Mathf.Lerp(0.70f, 1.30f, prog));
            Matrix4x4 matrix = Matrix4x4.TRS(p, Quaternion.identity, new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, matrix, LaetitiaMaterials.HeartBurstMat, 0, null, 0, props);
        }
    }

    /// <summary>「送出礼物」蓄力阶段围绕持握者闪耀的玫红心芒光环载体。</summary>
    public class Thing_LaetitiaGiftCharge : Thing
    {
        private Pawn caster;
        private Verb warmupVerb;
        private int ageTicks;
        private int maxTicks = 50;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Pawn casterPawn, Verb verb, int expectedTicks)
        {
            caster = casterPawn;
            warmupVerb = verb;
            ageTicks = 0;
            maxTicks = Mathf.Max(10, expectedTicks);
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned || Map == null)
            {
                Destroy();
                return;
            }

            if (warmupVerb != null)
            {
                if (!(caster.stances?.curStance is Stance_Warmup sw) || sw.verb != warmupVerb)
                {
                    Destroy();
                    return;
                }
            }

            Position = caster.Position.ClampInsideMap(Map);
            if (++ageTicks >= maxTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || caster == null || !caster.Spawned || LaetitiaMaterials.HeartBurstMat == null)
            {
                return;
            }

            float loop = (ageTicks % 20) / 20f;
            float alpha = Mathf.Clamp01(ageTicks / 5f) * 0.85f;
            props ??= new MaterialPropertyBlock();
            props.SetColor(LaetitiaMaterials.PropColor, new Color(0.98f, 0.30f, 0.64f, 1.0f));
            props.SetColor(LaetitiaMaterials.PropCoreColor, new Color(0.24f, 0.04f, 0.30f, 1.0f));
            props.SetFloat(LaetitiaMaterials.PropProgress, loop);
            props.SetFloat(LaetitiaMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(LaetitiaMaterials.PropAlpha, alpha);
            props.SetFloat(LaetitiaMaterials.PropMode, 0f);

            Vector3 p = caster.DrawPos.Yto0();
            p.y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.04f;
            float sz = Mathf.Lerp(1.25f, 1.90f, loop);
            Matrix4x4 matrix = Matrix4x4.TRS(p, Quaternion.identity, new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, matrix, LaetitiaMaterials.HeartBurstMat, 0, null, 0, props);
        }
    }

    /// <summary>蕾蒂希娅粉紫黑伤绸带曳光弹道载体。</summary>
    public class Thing_LaetitiaRibbonTracer : Thing
    {
        private Vector3 startPos;
        private Vector3 endPos;
        private bool empowered;
        private int ageTicks;
        private int maxTicks = 10;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 start, Vector3 end, bool isEmpowered)
        {
            startPos = start.Yto0();
            endPos = end.Yto0();
            empowered = isEmpowered;
            ageTicks = 0;
            maxTicks = isEmpowered ? 14 : 9;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || Map == null)
            {
                Destroy();
                return;
            }

            if (++ageTicks >= maxTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (!initialized || LaetitiaMaterials.RibbonMat == null)
            {
                return;
            }

            Vector3 delta = (endPos - startPos).Yto0();
            float len = delta.magnitude;
            if (len < 0.05f)
            {
                return;
            }

            float fade = 1f - Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, maxTicks));
            float width = (empowered ? 0.24f : 0.13f) * (0.5f + 0.5f * fade);
            Vector3 mid = (startPos + endPos) * 0.5f;
            mid.y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
            float yawDeg = Mathf.Atan2(-delta.z, delta.x) * Mathf.Rad2Deg;

            props ??= new MaterialPropertyBlock();
            Color col = empowered
                ? new Color(0.95f, 0.22f, 0.62f, fade * 0.94f)
                : new Color(0.98f, 0.50f, 0.78f, fade * 0.85f);
            props.SetColor(ShaderPropertyIDs.Color, col);

            Matrix4x4 matrix = Matrix4x4.TRS(mid, Quaternion.AngleAxis(yawDeg, Vector3.up), new Vector3(len, 1f, width));
            Graphics.DrawMesh(MeshPool.plane10, matrix, LaetitiaMaterials.RibbonMat, 0, null, 0, props);
        }
    }

    /// <summary>蕾蒂希娅 E.G.O. 武器普通射击动词：开火时播放后坐回弹动作并绽放枪口心芒。</summary>
    public class Verb_ShootLaetitia : Verb_Shoot
    {
        protected override bool TryCastShot()
        {
            bool fired = base.TryCastShot();
            if (fired && Caster is Pawn casterPawn && casterPawn.equipment?.Primary is ThingWithComps weapon &&
                weapon.def?.defName == LaetitiaWeaponAnimHandler.WeaponDefName)
            {
                Vector3 targetPos = currentTarget.IsValid
                    ? (currentTarget.HasThing && currentTarget.Thing.Spawned ? currentTarget.Thing.DrawPos.Yto0() : currentTarget.CenterVector3.Yto0())
                    : (casterPawn.DrawPos.Yto0() + Vector3.right);

                if (!EgoWeaponAnimationManager.IsPlaying(casterPawn, LaetitiaWeaponAnimHandler.ClipSkillWarmup)
                    && !EgoWeaponAnimationManager.IsPlaying(casterPawn, LaetitiaWeaponAnimHandler.ClipSkillGift))
                {
                    float aim = EgoWeaponAnimationUtility.ResolveAimAngle(casterPawn, targetPos);
                    EgoWeaponAnimationManager.Play(casterPawn, weapon, LaetitiaWeaponAnimHandler.ClipRangeShot, 28, aim);
                }

                Map map = casterPawn.MapHeld;
                if (map != null)
                {
                    Vector3 cPos = casterPawn.DrawPos.Yto0();
                    Vector3 dir = (targetPos - cPos).Yto0();
                    if (dir.sqrMagnitude < 0.001f)
                    {
                        dir = Vector3.right;
                    }
                    dir.Normalize();
                    LaetitiaVfxUtility.SpawnHeartBloom(map, cPos + dir * 0.65f, scale: 0.85f, lifeTicks: 10);
                }
            }

            return fired;
        }
    }

    /// <summary>
    /// 蕾蒂希娅 E.G.O. 心蕾步枪弹丸：
    /// 伴随玫紫绸带曳光与心芒绽放；若命中已携带「蕾蒂希娅的礼物（攻击性）」的目标，
    /// 额外触发 +25% 黑伤裂解并加速倒计时引爆「碎心黑蕾丝」。
    /// </summary>
    public class Projectile_LaetitiaShot : Bullet
    {
        public override void Launch(
            Thing launcher,
            Vector3 origin,
            LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags,
            bool preventFriendlyFire = false,
            Thing equipment = null,
            ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            Map map = launcher?.MapHeld ?? Map;
            if (map != null)
            {
                LaetitiaVfxUtility.SpawnRibbonTracer(map, origin.Yto0(), destination.Yto0(), empowered: false);
            }
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = ExactPosition.Yto0();
            Pawn targetPawn = hitThing as Pawn;
            bool hadGift = targetPawn != null && !targetPawn.Dead && !targetPawn.Destroyed && Laetitia.Def.HediffDefOf.LaetitiaGiftAggresive != null
                && (targetPawn.health?.hediffSet?.HasHediff(Laetitia.Def.HediffDefOf.LaetitiaGiftAggresive) ?? false);

            base.Impact(hitThing, blockedByShield);

            if (map != null && impactPos.ToIntVec3().InBounds(map))
            {
                LaetitiaVfxUtility.SpawnHeartBloom(map, impactPos, scale: hadGift ? 1.55f : 1.0f, lifeTicks: 14);
            }

            if (hadGift && targetPawn != null && !targetPawn.Dead && !targetPawn.Destroyed && !blockedByShield)
            {
                Hediff gift = targetPawn.health?.hediffSet?.GetFirstHediffOfDef(Laetitia.Def.HediffDefOf.LaetitiaGiftAggresive);
                float bonusDmg = Mathf.Max(3f, DamageAmount * 0.25f);
                targetPawn.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, bonusDmg, ArmorPenetration, ExactRotation.eulerAngles.y, launcher, weapon: equipmentDef));

                if (gift != null && !targetPawn.Dead && !targetPawn.Destroyed)
                {
                    gift.TryGetComp<HediffComp_LaetitiaGiftAggresive>()?.Notify_LaetitiaRifleHit();
                }
            }
        }
    }

    /// <summary>「送出礼物」重做蓄力动词：轻盈举枪聚能并在脚下展开心芒缎带光环。</summary>
    public class Verb_CastAbility_LaetitiaGift : Verb_CastAbility
    {
        public override bool TryStartCastOn(
            LocalTargetInfo castTarg,
            LocalTargetInfo destTarg,
            bool surpriseAttack = false,
            bool canHitNonTargetPawns = true,
            bool preventFriendlyFire = false,
            bool nonInterruptingSelfCast = false)
        {
            bool started = base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
            if (started && Caster is Pawn casterPawn && casterPawn.equipment?.Primary != null &&
                casterPawn.stances?.curStance is Stance_Warmup warmup && warmup.verb == this)
            {
                Vector3 targetPos = castTarg.IsValid ? castTarg.CenterVector3 : (casterPawn.DrawPos + Vector3.right);
                float aim = EgoWeaponAnimationUtility.ResolveAimAngle(casterPawn, targetPos);
                int warmupTicks = Mathf.Max(10, warmup.ticksLeft + 2);

                EgoWeaponAnimationManager.Play(
                    casterPawn,
                    casterPawn.equipment.Primary,
                    LaetitiaWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                Map map = casterPawn.MapHeld;
                if (map != null && Laetitia.Def.ThingDefOf.LC_LaetitiaGiftChargeFX != null && casterPawn.PositionHeld.InBounds(map))
                {
                    if (GenSpawn.Spawn(Laetitia.Def.ThingDefOf.LC_LaetitiaGiftChargeFX, casterPawn.PositionHeld, map) is Thing_LaetitiaGiftCharge aura)
                    {
                        aura.Initialize(casterPawn, this, warmupTicks);
                    }
                }
            }

            return started;
        }
    }

    /// <summary>驱动「送出礼物」第 2 拍（第 16 tick）强化心蕾追击射击的纯逻辑实体。</summary>
    public class Thing_LaetitiaSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private ThingDef weaponDef;
        private int ageTicks;
        private bool beat2Fired;
        private bool initialized;

        private const int Beat2Tick = 16;
        private const int TotalDurationTicks = 70;

        public void Initialize(Pawn casterPawn, Pawn targetPawn, Vector3 initialAnchor, ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = initialAnchor.Yto0();
            weaponDef = weapon;
            ageTicks = 0;
            beat2Fired = false;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned || Map == null)
            {
                Destroy();
                return;
            }

            if (caster.equipment?.Primary?.def?.defName != LaetitiaWeaponAnimHandler.WeaponDefName)
            {
                Destroy();
                return;
            }

            ageTicks++;

            if (primaryTarget != null && !primaryTarget.Dead && !primaryTarget.Destroyed && primaryTarget.Spawned && primaryTarget.Map == Map)
            {
                anchorPos = primaryTarget.DrawPos.Yto0();
                EgoWeaponAnimationManager.UpdateAimAngle(caster, EgoWeaponAnimationUtility.ResolveAimAngle(caster, anchorPos));
            }

            if (!beat2Fired && ageTicks >= Beat2Tick)
            {
                beat2Fired = true;
                ExecuteBeat2();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy();
            }
        }

        private void ExecuteBeat2()
        {
            if (Map == null)
            {
                return;
            }

            Vector3 cPos = caster.DrawPos.Yto0();
            Vector3 dir = (anchorPos - cPos).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            SoundDef shotSound = DefDatabase<SoundDef>.GetNamedSilentFail("Shot_Rifle_EgoBase");
            shotSound?.PlayOneShot(new TargetInfo(caster.PositionHeld, Map));

            LaetitiaVfxUtility.SpawnRibbonTracer(Map, cPos + dir * 0.65f, anchorPos, empowered: true);
            LaetitiaVfxUtility.SpawnHeartBloom(Map, anchorPos, scale: 1.85f, lifeTicks: 18);

            if (primaryTarget != null && !primaryTarget.Dead && !primaryTarget.Destroyed)
            {
                float dmg = Rand.Range(18f, 24f);
                float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
                primaryTarget.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, dmg, 0.45f, yaw, caster, weapon: weaponDef));

                if (!primaryTarget.Dead && !primaryTarget.Destroyed && Laetitia.Def.HediffDefOf.LaetitiaGiftAggresive != null)
                {
                    Hediff gift = primaryTarget.health?.GetOrAddHediff(Laetitia.Def.HediffDefOf.LaetitiaGiftAggresive);
                    if (gift != null)
                    {
                        gift.Severity = 1f;
                        gift.TryGetComp<HediffComp_LaetitiaGiftAggresive>()?.MarkFromEgoWeapon(caster);
                    }
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }
    }
}
