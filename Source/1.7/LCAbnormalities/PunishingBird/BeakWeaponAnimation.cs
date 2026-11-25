using System.Collections.Generic;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.PunishingBird
{
    /// <summary>O-02-56 惩戒鸟 E.G.O. 武器（喙）定制 GLSL 着色器与曳光材质缓存。</summary>
    [StaticConstructorOnStartup]
    public static class BeakMaterials
    {
        public static readonly int PropColor = Shader.PropertyToID("_Color");
        public static readonly int PropCoreColor = Shader.PropertyToID("_CoreColor");
        public static readonly int PropProgress = Shader.PropertyToID("_Progress");
        public static readonly int PropAgeSecs = Shader.PropertyToID("_AgeSecs");
        public static readonly int PropAlpha = Shader.PropertyToID("_Alpha");
        public static readonly int PropMode = Shader.PropertyToID("_Mode");

        public static readonly Material ImpactMat;
        public static readonly Material TracerMat;

        static BeakMaterials()
        {
            Shader impactShader = ShaderDatabase.LoadShader("Map/LC_BeakImpact") ?? ShaderDatabase.Transparent;
            ImpactMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = impactShader,
                color = new Color(0.95f, 0.16f, 0.20f, 1.0f)
            });

            TracerMat = MaterialPool.MatFrom(new MaterialRequest
            {
                mainTex = BaseContent.WhiteTex,
                shader = ShaderDatabase.MoteGlow,
                color = Color.white
            });
        }
    }

    /// <summary>
    /// 喙（EgoWeapon_PunishingBird）专属武器动画处理器：
    /// 关键几何约束：EgoWeapon_PunishingBird.png 原图枪口朝左（-X），所有 DrawWeaponSprite 调用必须传入 spriteNativeFacingLeft: true。
    /// 支持轻快手枪连射跳动后坐力（ClipRangeShot）、紧急枪托鞭击（ClipMeleeWhip）与「惩戒连射」疾速点射加血口终结（ClipSkillBarrage）。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class BeakWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_PunishingBird";

        public const string ClipRangeShot = "Beak_RangeShot";
        public const string ClipMeleeWhip = "Beak_MeleeWhip";
        public const string ClipSkillWarmup = "Beak_SkillWarmup";
        public const string ClipSkillBarrage = "Beak_SkillBarrage";

        private static readonly Vector2 PistolDrawSize = new Vector2(1.55f, 1.55f);

        // === Range Shot (22 ticks): snappy recoil kick-up -> quick snap back to bead ===
        private static readonly BodyKeyframe[] RangeShotBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -3f),
            new BodyKeyframe(5f, -0.07f, 0.01f, -6f),
            new BodyKeyframe(12f, 0.01f, 0f, 1f),
            new BodyKeyframe(22f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] RangeShotPistol =
        {
            new WeaponKeyframe(0f, 0.42f, -0.04f, -4f, 1.00f),
            new WeaponKeyframe(4f, 0.30f, 0.12f, -28f, 1.06f),
            new WeaponKeyframe(11f, 0.39f, 0.01f, -8f, 1.02f),
            new WeaponKeyframe(22f, 0.40f, -0.05f, 0f, 1.00f)
        };

        // === Melee Whip (44 ticks): crisp close pistol whip ===
        private static readonly BodyKeyframe[] MeleeWhipBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(7f, 0.16f, -0.01f, 14f),
            new BodyKeyframe(18f, 0.12f, 0f, 9f),
            new BodyKeyframe(44f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] MeleeWhipPistol =
        {
            new WeaponKeyframe(0f, 0.18f, 0.24f, -42f, 1.00f),
            new WeaponKeyframe(7f, 0.58f, -0.04f, 24f, 1.08f),
            new WeaponKeyframe(18f, 0.48f, -0.08f, 14f, 1.03f),
            new WeaponKeyframe(44f, 0.40f, -0.05f, 0f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): raise pistol sharp toward target ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.05f, 0.01f, -5f),
            new BodyKeyframe(1.00f, -0.09f, 0.02f, -8f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupPistol =
        {
            new WeaponKeyframe(0.00f, 0.40f, -0.05f, 0f, 1.00f),
            new WeaponKeyframe(0.45f, 0.34f, 0.16f, -32f, 1.04f),
            new WeaponKeyframe(1.00f, 0.44f, 0.04f, -6f, 1.06f)
        };

        // === Skill Barrage (82 ticks): 5 rapid shots (0, 6, 12, 18, 24) + enraged crimson maw kick @ 32 ===
        private static readonly BodyKeyframe[] SkillBarrageBody =
        {
            new BodyKeyframe(0f, -0.06f, 0f, -5f),
            new BodyKeyframe(14f, -0.04f, 0f, -4f),
            new BodyKeyframe(26f, -0.08f, 0.01f, -8f),
            new BodyKeyframe(32f, -0.14f, 0.03f, -15f),
            new BodyKeyframe(48f, -0.04f, 0f, -4f),
            new BodyKeyframe(82f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillBarragePistol =
        {
            new WeaponKeyframe(0f, 0.34f, 0.10f, -24f, 1.06f),
            new WeaponKeyframe(6f, 0.38f, 0.06f, -18f, 1.05f),
            new WeaponKeyframe(12f, 0.33f, 0.11f, -26f, 1.06f),
            new WeaponKeyframe(18f, 0.38f, 0.06f, -18f, 1.05f),
            new WeaponKeyframe(24f, 0.32f, 0.13f, -28f, 1.07f),
            new WeaponKeyframe(32f, 0.24f, 0.20f, -42f, 1.14f),
            new WeaponKeyframe(48f, 0.42f, 0.02f, -10f, 1.04f),
            new WeaponKeyframe(82f, 0.40f, -0.05f, 0f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillBarrage))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipMeleeWhip, 44, aimAngle);
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
                case ClipMeleeWhip:
                    kf = EgoWeaponAnimationUtility.SampleBody(MeleeWhipBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillBarrage:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillBarrageBody, elapsedTicks);
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
                float tickPhase = (Find.TickManager?.TicksGame ?? 0) * 0.14f;
                float sway = Mathf.Sin(tickPhase) * (moving ? 5f : 2.2f);
                float posX = moving ? 0.34f : 0.40f;
                float posZ = (moving ? -0.02f : -0.05f) + Mathf.Sin(tickPhase * 0.8f) * 0.010f;
                float angle = (moving ? -8f : 0f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(
                    mat,
                    ctx.BaseDrawPos,
                    alt,
                    posX,
                    posZ,
                    angle,
                    PistolDrawSize,
                    ctx.FacingLeft,
                    spriteNativeFacingLeft: true);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipRangeShot:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(RangeShotPistol, t);
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, PistolDrawSize, ctx.FacingLeft, kf.Scale, spriteNativeFacingLeft: true);
                    return true;
                }

                case ClipMeleeWhip:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(MeleeWhipPistol, t);
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, PistolDrawSize, ctx.FacingLeft, kf.Scale, spriteNativeFacingLeft: true);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupPistol, prog);
                    float jitter = Mathf.Sin(t * 2.2f) * 0.010f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + jitter, kf.PosZ, kf.AngleDeg, PistolDrawSize, ctx.FacingLeft, kf.Scale, spriteNativeFacingLeft: true);
                    return true;
                }

                case ClipSkillBarrage:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillBarragePistol, t);
                    float fireJitter = (t < 30f) ? Mathf.Sin(t * 3.1f) * 2.6f : 0f;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg + fireJitter, PistolDrawSize, ctx.FacingLeft, kf.Scale, spriteNativeFacingLeft: true);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    public static class BeakVfxUtility
    {
        public static void SpawnTracer(Map map, Vector3 start, Vector3 end, bool heavy = false)
        {
            if (map == null || PunishingBirdDefOf.LC_BeakTracerFX == null)
            {
                return;
            }

            IntVec3 cell = start.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(PunishingBirdDefOf.LC_BeakTracerFX, cell, map) is Thing_BeakTracer tracer)
            {
                tracer.Initialize(start, end, heavy);
            }
        }

        public static void SpawnPeckBurst(Map map, Vector3 pos, float scale = 1.0f, int lifeTicks = 12)
        {
            SpawnImpact(map, pos, mode: 0f, scale: scale, lifeTicks: lifeTicks);
        }

        public static void SpawnMawSnap(Map map, Vector3 pos, float scale = 2.85f, int lifeTicks = 26)
        {
            SpawnImpact(map, pos, mode: 1f, scale: scale, lifeTicks: lifeTicks);
        }

        private static void SpawnImpact(Map map, Vector3 pos, float mode, float scale, int lifeTicks)
        {
            if (map == null || PunishingBirdDefOf.LC_BeakImpactFX == null)
            {
                return;
            }

            IntVec3 cell = pos.ToIntVec3().ClampInsideMap(map);
            if (!cell.InBounds(map))
            {
                return;
            }

            if (GenSpawn.Spawn(PunishingBirdDefOf.LC_BeakImpactFX, cell, map) is Thing_BeakImpact impact)
            {
                impact.Initialize(pos, mode, scale, lifeTicks);
            }
        }
    }

    /// <summary>惩戒鸟「喙」纯白绯红流光曳光轨迹载体。</summary>
    public class Thing_BeakTracer : Thing
    {
        private Vector3 startPos;
        private Vector3 endPos;
        private bool heavy;
        private int ageTicks;
        private int maxTicks = 8;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 start, Vector3 end, bool isHeavy)
        {
            startPos = start.Yto0();
            endPos = end.Yto0();
            heavy = isHeavy;
            ageTicks = 0;
            maxTicks = isHeavy ? 12 : 8;
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
            if (!initialized || BeakMaterials.TracerMat == null)
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
            float width = (heavy ? 0.24f : 0.11f) * (0.5f + 0.5f * fade);
            Vector3 mid = (startPos + endPos) * 0.5f;
            mid.y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.05f;
            float yawDeg = Mathf.Atan2(-delta.z, delta.x) * Mathf.Rad2Deg;

            props ??= new MaterialPropertyBlock();
            Color col = heavy
                ? new Color(1.0f, 0.20f, 0.24f, fade * 0.92f)
                : new Color(1.0f, 0.92f, 0.90f, fade * 0.88f);
            props.SetColor(ShaderPropertyIDs.Color, col);

            Matrix4x4 matrix = Matrix4x4.TRS(mid, Quaternion.AngleAxis(yawDeg, Vector3.up), new Vector3(len, 1f, width));
            Graphics.DrawMesh(MeshPool.plane10, matrix, BeakMaterials.TracerMat, 0, null, 0, props);
        }
    }

    /// <summary>惩戒鸟星芒啄击火花（Mode 0）与血色兽口啃噬震荡（Mode 1）渲染载体。</summary>
    public class Thing_BeakImpact : Thing
    {
        private Vector3 worldPos;
        private float mode;
        private float scale = 1.0f;
        private int ageTicks;
        private int maxTicks = 12;
        private bool initialized;
        private MaterialPropertyBlock props;

        public void Initialize(Vector3 pos, float shaderMode, float drawScale, int lifeTicks)
        {
            worldPos = pos.Yto0();
            mode = shaderMode;
            scale = Mathf.Max(0.4f, drawScale);
            ageTicks = 0;
            maxTicks = Mathf.Max(6, lifeTicks);
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
            if (!initialized || BeakMaterials.ImpactMat == null)
            {
                return;
            }

            float prog = Mathf.Clamp01(ageTicks / (float)Mathf.Max(1, maxTicks));
            props ??= new MaterialPropertyBlock();
            props.SetColor(BeakMaterials.PropColor, mode < 0.5f ? new Color(0.96f, 0.18f, 0.22f, 1.0f) : new Color(0.88f, 0.08f, 0.14f, 1.0f));
            props.SetColor(BeakMaterials.PropCoreColor, new Color(1.0f, 0.96f, 0.92f, 1.0f));
            props.SetFloat(BeakMaterials.PropProgress, prog);
            props.SetFloat(BeakMaterials.PropAgeSecs, ageTicks / 60f);
            props.SetFloat(BeakMaterials.PropAlpha, 1.0f);
            props.SetFloat(BeakMaterials.PropMode, mode);

            Vector3 p = worldPos;
            p.y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.06f;
            float sz = scale * (mode < 0.5f ? Mathf.Lerp(0.85f, 1.25f, prog) : Mathf.Lerp(0.75f, 1.30f, prog));
            Matrix4x4 matrix = Matrix4x4.TRS(p, Quaternion.identity, new Vector3(sz, 1f, sz));
            Graphics.DrawMesh(MeshPool.plane10, matrix, BeakMaterials.ImpactMat, 0, null, 0, props);
        }
    }

    /// <summary>喙 E.G.O. 武器普通射击动词：开火瞬间触发手枪上跳后坐力动作与枪口啄击火花。</summary>
    public class Verb_ShootBeak : Verb_Shoot
    {
        protected override bool TryCastShot()
        {
            bool fired = base.TryCastShot();
            if (fired && Caster is Pawn casterPawn && casterPawn.equipment?.Primary is ThingWithComps weapon &&
                weapon.def?.defName == BeakWeaponAnimHandler.WeaponDefName)
            {
                Vector3 targetPos = currentTarget.IsValid
                    ? (currentTarget.HasThing && currentTarget.Thing.Spawned ? currentTarget.Thing.DrawPos.Yto0() : currentTarget.CenterVector3.Yto0())
                    : (casterPawn.DrawPos.Yto0() + Vector3.right);

                if (!EgoWeaponAnimationManager.IsPlaying(casterPawn, BeakWeaponAnimHandler.ClipSkillWarmup)
                    && !EgoWeaponAnimationManager.IsPlaying(casterPawn, BeakWeaponAnimHandler.ClipSkillBarrage))
                {
                    float aim = EgoWeaponAnimationUtility.ResolveAimAngle(casterPawn, targetPos);
                    EgoWeaponAnimationManager.Play(casterPawn, weapon, BeakWeaponAnimHandler.ClipRangeShot, 22, aim);
                }

                Map map = casterPawn.MapHeld;
                if (map != null)
                {
                    Vector3 casterPos = casterPawn.DrawPos.Yto0();
                    Vector3 dir = (targetPos - casterPos).Yto0();
                    if (dir.sqrMagnitude < 0.001f)
                    {
                        dir = Vector3.right;
                    }
                    dir.Normalize();
                    BeakVfxUtility.SpawnPeckBurst(map, casterPos + dir * 0.56f, scale: 0.78f, lifeTicks: 8);
                }
            }

            return fired;
        }
    }

    /// <summary>喙 E.G.O. 弹丸：出膛伴随纯白绯红曳光轨迹，命中爆发六芒啄击星芒。</summary>
    public class Projectile_BeakShot : Bullet
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
                BeakVfxUtility.SpawnTracer(map, origin.Yto0(), destination.Yto0(), heavy: false);
            }
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = ExactPosition.Yto0();
            base.Impact(hitThing, blockedByShield);

            if (map != null && impactPos.ToIntVec3().InBounds(map))
            {
                BeakVfxUtility.SpawnPeckBurst(map, impactPos, scale: 1.05f, lifeTicks: 12);
            }
        }
    }

    /// <summary>「惩戒连射」蓄力动词：举枪凝聚杀意并播放振翅声。</summary>
    public class Verb_CastAbility_BeakBarrage : Verb_CastAbility
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
                int warmupTicks = Mathf.Max(8, warmup.ticksLeft + 2);

                EgoWeaponAnimationManager.Play(
                    casterPawn,
                    casterPawn.equipment.Primary,
                    BeakWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                if (casterPawn.MapHeld != null && PunishingBirdDefOf.LC_PunishingBird_WingFlap != null)
                {
                    PunishingBirdDefOf.LC_PunishingBird_WingFlap.PlayOneShot(new TargetInfo(casterPawn.PositionHeld, casterPawn.MapHeld));
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilityBeakBarrage : CompProperties_AbilityEffect
    {
        public FloatRange pelletDamageRange = new FloatRange(4f, 6f);
        public FloatRange mawDamageRange = new FloatRange(14f, 19f);
        public float mawRadius = 2.5f;
        public float armorPenetration = 0.32f;

        public CompProperties_AbilityBeakBarrage()
        {
            compClass = typeof(CompAbilityEffect_BeakBarrage);
        }
    }

    /// <summary>
    /// 喙主动技能「惩戒连射」效果处理器：
    /// 第 1 拍至第 24 tick 连续打出 5 发高速纯白绯红点射，随后于第 32 tick 以猩红巨口噬咬终结，晕眩并重创 2.5 格范围敌军。
    /// </summary>
    public class CompAbilityEffect_BeakBarrage : CompAbilityEffect
    {
        public new CompProperties_AbilityBeakBarrage Props => (CompProperties_AbilityBeakBarrage)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            Pawn victim = target.Pawn;
            if (caster == null || caster.Dead || !caster.Spawned)
            {
                return;
            }

            Map map = caster.MapHeld;
            if (map == null)
            {
                return;
            }

            Vector3 casterPos = caster.DrawPos.Yto0();
            Vector3 anchorPos = victim != null && victim.Spawned ? victim.DrawPos.Yto0() : target.CenterVector3.Yto0();
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(caster, anchorPos);

            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon != null && weapon.def?.defName == BeakWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, BeakWeaponAnimHandler.ClipSkillBarrage, 82, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(BeakWeaponAnimHandler.WeaponDefName);
            FireSinglePeckShot(map, caster, victim, anchorPos, Props.pelletDamageRange.RandomInRange, Props.armorPenetration, weaponDef, Vector3.zero);

            if (PunishingBirdDefOf.LC_BeakSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(PunishingBirdDefOf.LC_BeakSkillRunnerFX, caster.PositionHeld, map) is Thing_BeakSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.pelletDamageRange, Props.mawDamageRange, Props.mawRadius, Props.armorPenetration, weaponDef);
                }
            }
        }

        internal static void FireSinglePeckShot(
            Map map,
            Pawn caster,
            Pawn target,
            Vector3 targetPos,
            float dmg,
            float armorPen,
            ThingDef weaponDef,
            Vector3 spreadOffset)
        {
            if (map == null || caster == null || !caster.Spawned)
            {
                return;
            }

            Vector3 cPos = caster.DrawPos.Yto0();
            Vector3 hitPoint = targetPos.Yto0() + spreadOffset;
            Vector3 dir = (hitPoint - cPos).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            Vector3 muzzle = cPos + dir * 0.56f;
            if (PunishingBirdDefOf.LC_PunishingBird_BeakShot != null)
            {
                PunishingBirdDefOf.LC_PunishingBird_BeakShot.PlayOneShot(new TargetInfo(caster.PositionHeld, map));
            }
            BeakVfxUtility.SpawnTracer(map, muzzle, hitPoint, heavy: false);
            BeakVfxUtility.SpawnPeckBurst(map, muzzle, scale: 0.72f, lifeTicks: 7);
            BeakVfxUtility.SpawnPeckBurst(map, hitPoint, scale: 1.15f, lifeTicks: 11);

            if (target != null && !target.Dead && !target.Destroyed)
            {
                DamageDef redDef = LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
                float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
                target.TakeDamage(new DamageInfo(redDef, dmg, armorPen, yaw, caster, weapon: weaponDef));
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target);

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null || pawn.Dead)
            {
                if (throwMessages && pawn != null)
                {
                    Messages.Message("LC_EgoSkill_TargetMustBeAlive".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            if (parent?.pawn != null && pawn.Faction != null && pawn.Faction == parent.pawn.Faction)
            {
                if (throwMessages)
                {
                    Messages.Message("LC_EgoSkill_CannotTargetAlly".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return true;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return Valid(target, false) && parent?.pawn != null && target.Pawn.HostileTo(parent.pawn);
        }
    }

    /// <summary>驱动「惩戒连射」多发点射与第 32 tick 猩红巨口噬咬终结的纯逻辑实体。</summary>
    public class Thing_BeakSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private FloatRange pelletDmgRange;
        private FloatRange mawDmgRange;
        private float mawRadius;
        private float armorPen;
        private ThingDef weaponDef;
        private int ageTicks;
        private int shotStep;
        private bool mawExecuted;
        private bool initialized;

        private const int MawTriggerTick = 32;
        private const int TotalDurationTicks = 78;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 initialAnchor,
            FloatRange pelletDmg,
            FloatRange mawDmg,
            float radius,
            float armorPenetration,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = initialAnchor.Yto0();
            pelletDmgRange = pelletDmg;
            mawDmgRange = mawDmg;
            mawRadius = Mathf.Max(1.8f, radius);
            armorPen = armorPenetration;
            weaponDef = weapon;
            ageTicks = 0;
            shotStep = 0;
            mawExecuted = false;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned || Map == null)
            {
                Destroy();
                return;
            }

            if (caster.equipment?.Primary?.def?.defName != BeakWeaponAnimHandler.WeaponDefName)
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

            if ((ageTicks == 6 && shotStep == 0)
                || (ageTicks == 12 && shotStep == 1)
                || (ageTicks == 18 && shotStep == 2)
                || (ageTicks == 24 && shotStep == 3))
            {
                shotStep++;
                Vector3 jitter = new Vector3(Rand.Range(-0.28f, 0.28f), 0f, Rand.Range(-0.22f, 0.22f));
                CompAbilityEffect_BeakBarrage.FireSinglePeckShot(Map, caster, primaryTarget, anchorPos, pelletDmgRange.RandomInRange, armorPen, weaponDef, jitter);
            }

            if (!mawExecuted && ageTicks >= MawTriggerTick)
            {
                mawExecuted = true;
                ExecutePunishingMaw();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy();
            }
        }

        private void ExecutePunishingMaw()
        {
            if (Map == null)
            {
                return;
            }

            IntVec3 cell = anchorPos.ToIntVec3().ClampInsideMap(Map);
            if (!cell.InBounds(Map))
            {
                return;
            }

            TargetInfo loc = new TargetInfo(cell, Map);
            PunishingBirdDefOf.LC_PunishingBird_MawBite?.PlayOneShot(loc);
            PunishingBirdDefOf.LC_PunishingBird_Open?.PlayOneShot(new TargetInfo(caster.PositionHeld, Map));

            Vector3 cPos = caster.DrawPos.Yto0();
            Vector3 dir = (anchorPos - cPos).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            BeakVfxUtility.SpawnTracer(Map, cPos + dir * 0.58f, anchorPos, heavy: true);
            BeakVfxUtility.SpawnMawSnap(Map, anchorPos, scale: 2.95f, lifeTicks: 26);

            DamageDef redDef = LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
            List<Pawn> candidates = new List<Pawn>(Map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn foe = candidates[i];
                if (foe == null || foe == caster || foe.Dead || foe.Destroyed || !foe.Spawned)
                {
                    continue;
                }
                if (foe != primaryTarget && !foe.HostileTo(caster))
                {
                    continue;
                }

                if ((foe.DrawPos.Yto0() - anchorPos).sqrMagnitude <= mawRadius * mawRadius)
                {
                    foe.TakeDamage(new DamageInfo(redDef, mawDmgRange.RandomInRange, armorPen + 0.08f, -1f, caster, weapon: weaponDef));
                    if (!foe.Dead)
                    {
                        foe.stances?.stunner?.StunFor(55, caster, addBattleLog: false);
                    }
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }
    }
}
