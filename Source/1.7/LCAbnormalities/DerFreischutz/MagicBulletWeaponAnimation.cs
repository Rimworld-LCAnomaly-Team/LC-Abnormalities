using System.Collections.Generic;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.DerFreischutz
{
    /// <summary>
    /// F-01-69 魔弹射手 E.G.O. 武器「魔弹」（EgoWeapon_DerFreischutz）专属动画处理器：
    /// 贴图原图呈 0° 水平朝右（左为暗色枪托、右为长管魔弹步枪枪口）。
    /// 支持沉稳扛肩瞄准蓄力（ClipRangeWarmup）、后坐重震（ClipRangeShot）、近战枪托敲击（ClipMeleeBash），
    /// 以及主动技能「魔弹齐射」的冥蓝主弹贯穿 + 双翼紫黑魔门会心汇聚演出。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class MagicBulletWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_DerFreischutz";

        public const string ClipRangeWarmup = "MagicBullet_RangeWarmup";
        public const string ClipRangeShot = "MagicBullet_RangeShot";
        public const string ClipMeleeBash = "MagicBullet_MeleeBash";
        public const string ClipSkillWarmup = "MagicBullet_SkillWarmup";
        public const string ClipSkillSalvo = "MagicBullet_SkillSalvo";

        private static readonly Vector2 RifleDrawSize = new Vector2(2.15f, 2.15f);
        private static readonly Color ArcaneGhostTint = new Color(0.28f, 0.58f, 1.0f, 0.80f);

        // === Range Warmup (normalized 0..1): shoulder rifle firmly and steady aim ===
        private static readonly BodyKeyframe[] RangeWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.04f, 0.01f, -4f),
            new BodyKeyframe(1.00f, -0.07f, 0.01f, -6f)
        };

        private static readonly WeaponKeyframe[] RangeWarmupRifle =
        {
            new WeaponKeyframe(0.00f, 0.30f, -0.10f, 10f, 1.00f),
            new WeaponKeyframe(0.45f, 0.38f, 0.02f, -4f, 1.03f),
            new WeaponKeyframe(1.00f, 0.42f, 0.03f, -2f, 1.05f)
        };

        // === Range Shot (38 ticks): heavy caliber recoil kick -> smooth lower ===
        private static readonly BodyKeyframe[] RangeShotBody =
        {
            new BodyKeyframe(0f, -0.10f, 0.02f, -10f),
            new BodyKeyframe(7f, -0.14f, 0.02f, -12f),
            new BodyKeyframe(20f, -0.04f, 0f, -3f),
            new BodyKeyframe(38f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] RangeShotRifle =
        {
            new WeaponKeyframe(0f, 0.38f, 0.04f, -6f, 1.06f),
            new WeaponKeyframe(5f, 0.24f, 0.14f, -24f, 1.10f),
            new WeaponKeyframe(18f, 0.36f, 0.02f, -4f, 1.04f),
            new WeaponKeyframe(38f, 0.30f, -0.10f, 10f, 1.00f)
        };

        // === Melee Bash (46 ticks): barrel swing knock ===
        private static readonly BodyKeyframe[] MeleeBashBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(8f, 0.18f, -0.02f, 15f),
            new BodyKeyframe(22f, 0.12f, 0f, 8f),
            new BodyKeyframe(46f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] MeleeBashRifle =
        {
            new WeaponKeyframe(0f, 0.12f, 0.28f, -48f, 1.00f),
            new WeaponKeyframe(8f, 0.62f, -0.04f, 22f, 1.08f),
            new WeaponKeyframe(22f, 0.48f, -0.10f, 18f, 1.03f),
            new WeaponKeyframe(46f, 0.30f, -0.10f, 10f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): solemn ceremonial brace ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.06f, 0.01f, -6f),
            new BodyKeyframe(1.00f, -0.12f, 0.02f, -11f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupRifle =
        {
            new WeaponKeyframe(0.00f, 0.30f, -0.10f, 10f, 1.00f),
            new WeaponKeyframe(0.40f, 0.40f, 0.04f, -6f, 1.06f),
            new WeaponKeyframe(1.00f, 0.44f, 0.05f, -3f, 1.10f)
        };

        // === Skill Salvo (92 ticks): main lance recoil + steady command pose for flanking convergence @ 28 ===
        private static readonly BodyKeyframe[] SkillSalvoBody =
        {
            new BodyKeyframe(0f, -0.12f, 0.02f, -12f),
            new BodyKeyframe(6f, -0.18f, 0.03f, -16f),
            new BodyKeyframe(20f, -0.06f, 0.01f, -5f),
            new BodyKeyframe(32f, -0.10f, 0.01f, -8f),
            new BodyKeyframe(56f, -0.04f, 0f, -3f),
            new BodyKeyframe(92f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillSalvoRifle =
        {
            new WeaponKeyframe(0f, 0.34f, 0.06f, -10f, 1.12f),
            new WeaponKeyframe(6f, 0.22f, 0.16f, -26f, 1.14f),
            new WeaponKeyframe(20f, 0.40f, 0.03f, -4f, 1.08f),
            new WeaponKeyframe(32f, 0.34f, 0.06f, -12f, 1.10f),
            new WeaponKeyframe(58f, 0.36f, -0.02f, 0f, 1.04f),
            new WeaponKeyframe(92f, 0.30f, -0.10f, 10f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillSalvo))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipMeleeBash, 46, aimAngle);
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
                case ClipRangeWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(RangeWarmupBody, prog);
                    break;
                }
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
                case ClipSkillSalvo:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillSalvoBody, elapsedTicks);
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
                float tickPhase = (Find.TickManager?.TicksGame ?? 0) * 0.08f;
                float sway = Mathf.Sin(tickPhase) * (moving ? 3.5f : 1.5f);
                float posX = moving ? 0.28f : 0.30f;
                float posZ = (moving ? -0.04f : -0.10f) + Mathf.Sin(tickPhase * 0.7f) * 0.008f;
                float angle = (moving ? 2f : 10f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, RifleDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipRangeWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(RangeWarmupRifle, prog);
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, RifleDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

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
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, MeleeBashRifle, t, ctx.BaseDrawPos, alt, RifleDrawSize, ctx.FacingLeft, ArcaneGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, RifleDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupRifle, prog);
                    float hum = Mathf.Sin(t * 1.3f) * 0.008f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + hum, kf.PosZ, kf.AngleDeg, RifleDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillSalvo:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillSalvoRifle, t);
                    if (t <= 14f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillSalvoRifle, t, ctx.BaseDrawPos, alt, RifleDrawSize, ctx.FacingLeft, ArcaneGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, RifleDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「魔弹齐射」蓄力动词：举枪凝聚双层符文阵并在击发前收束。</summary>
    public class Verb_CastAbility_MagicBulletBarrage : Verb_CastAbility
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
            if (started && Caster is Pawn casterPawn && casterPawn.equipment?.Primary != null &&
                casterPawn.stances?.curStance is Stance_Warmup warmup && warmup.verb == this)
            {
                Vector3 targetPos = castTarg.IsValid ? castTarg.CenterVector3 : (casterPawn.DrawPos + Vector3.right);
                float aim = EgoWeaponAnimationUtility.ResolveAimAngle(casterPawn, targetPos);
                int warmupTicks = Mathf.Max(10, warmup.ticksLeft + 2);

                EgoWeaponAnimationManager.Play(
                    casterPawn,
                    casterPawn.equipment.Primary,
                    MagicBulletWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                Map map = casterPawn.MapHeld;
                if (map != null)
                {
                    if (activeWarmupPortal != null && !activeWarmupPortal.Destroyed)
                    {
                        activeWarmupPortal.CancelEarly();
                    }

                    activeWarmupPortal = MagicBulletVfxUtility.SpawnWarmupPortal(map, casterPawn, this, castTarg, 1.05f);
                    TargetInfo loc = new TargetInfo(casterPawn.PositionHeld, map);
                    DerFreischutzDefOf.LC_DerFreischutz_Portal?.PlayOneShot(loc);
                    DerFreischutzDefOf.LC_DerFreischutz_WeaponCharge?.PlayOneShot(loc);
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

    public class CompProperties_AbilityMagicBulletBarrage : CompProperties_AbilityEffect
    {
        public float beamRange = 38f;
        public FloatRange mainDamageRange = new FloatRange(28f, 36f);
        public FloatRange portalDamageRange = new FloatRange(22f, 28f);
        public float armorPenetration = 0.80f;

        public CompProperties_AbilityMagicBulletBarrage()
        {
            compClass = typeof(CompAbilityEffect_MagicBulletBarrage);
        }
    }

    /// <summary>
    /// 魔弹主动技能「魔弹齐射」效果处理器：
    /// 第 1 拍射出冥蓝贯穿主魔弹穿过目标沿线（严格跳过友军）；
    /// 第 14 tick 于目标左右侧翼开启双座紫黑厄运魔门；
    /// 第 28 tick 从双翼魔门交叉贯射两条诅咒黑伤光束交汇穿透目标区域。
    /// </summary>
    public class CompAbilityEffect_MagicBulletBarrage : CompAbilityEffect
    {
        public new CompProperties_AbilityMagicBulletBarrage Props => (CompProperties_AbilityMagicBulletBarrage)props;

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
            Vector3 dir = (anchorPos - casterPos).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(caster, anchorPos);
            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon != null && weapon.def?.defName == MagicBulletWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, MagicBulletWeaponAnimHandler.ClipSkillSalvo, 92, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(MagicBulletWeaponAnimHandler.WeaponDefName);
            Vector3 muzzle = casterPos + dir * 0.72f;
            Vector3 mainEnd = ComputeMapBoundedEndPoint(map, muzzle, dir, Props.beamRange);

            TargetInfo loc = new TargetInfo(caster.PositionHeld, map);
            DerFreischutzDefOf.LC_DerFreischutz_Shoot?.PlayOneShot(loc);
            DerFreischutzDefOf.LC_DerFreischutz_Shot?.PlayOneShot(loc);

            FireAllySafePiercingBeam(
                map,
                caster,
                victim,
                muzzle,
                mainEnd,
                beamWidth: 0.72f,
                corridorWidth: 0.95f,
                dmgRange: Props.mainDamageRange,
                armorPen: Props.armorPenetration,
                weaponDef: weaponDef,
                cursedSeventh: false,
                stunTicks: 30);

            if (DerFreischutzDefOf.LC_MagicBulletSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(DerFreischutzDefOf.LC_MagicBulletSkillRunnerFX, caster.PositionHeld, map) is Thing_MagicBulletSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, dir, Props.portalDamageRange, Props.armorPenetration, weaponDef);
                }
            }
        }

        internal static Vector3 ComputeMapBoundedEndPoint(Map map, Vector3 origin, Vector3 dir, float maxDist)
        {
            if (map == null)
            {
                return origin + dir * maxDist;
            }

            float minX = 0.5f;
            float maxX = map.Size.x - 0.5f;
            float minZ = 0.5f;
            float maxZ = map.Size.z - 0.5f;

            float tMax = Mathf.Max(1f, maxDist);
            if (Mathf.Abs(dir.x) > 1e-4f)
            {
                float tx = dir.x > 0f ? (maxX - origin.x) / dir.x : (minX - origin.x) / dir.x;
                if (tx > 0f)
                {
                    tMax = Mathf.Min(tMax, tx);
                }
            }
            if (Mathf.Abs(dir.z) > 1e-4f)
            {
                float tz = dir.z > 0f ? (maxZ - origin.z) / dir.z : (minZ - origin.z) / dir.z;
                if (tz > 0f)
                {
                    tMax = Mathf.Min(tMax, tz);
                }
            }

            Vector3 res = origin + dir * Mathf.Max(0.5f, tMax);
            res.x = Mathf.Clamp(res.x, minX, maxX);
            res.z = Mathf.Clamp(res.z, minZ, maxZ);
            return res;
        }

        internal static void FireAllySafePiercingBeam(
            Map map,
            Pawn caster,
            Pawn primaryTarget,
            Vector3 start,
            Vector3 end,
            float beamWidth,
            float corridorWidth,
            FloatRange dmgRange,
            float armorPen,
            ThingDef weaponDef,
            bool cursedSeventh,
            int stunTicks)
        {
            if (map == null || caster == null)
            {
                return;
            }

            Vector3 a = start.Yto0();
            Vector3 b = end.Yto0();
            Vector3 seg = b - a;
            float len = seg.magnitude;
            if (len < 0.05f)
            {
                return;
            }

            Vector3 dir = seg / len;
            float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
            MagicBulletVfxUtility.SpawnBeam(map, a, b, beamWidth, cursedSeventh ? 210f : 195f, cursedSeventh);

            DamageDef blackDef = LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage;
            List<Pawn> candidates = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
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

                Vector3 p = foe.DrawPos.Yto0();
                float proj = Vector3.Dot(p - a, dir);
                if (proj < -0.6f || proj > len + 0.6f)
                {
                    continue;
                }

                Vector3 closest = a + dir * Mathf.Clamp(proj, 0f, len);
                if ((p - closest).sqrMagnitude <= corridorWidth * corridorWidth)
                {
                    MagicBulletVfxUtility.SpawnHitBurst(map, foe.DrawPos, dir, beamWidth * 1.10f, cursedSeventh);
                    IntVec3 hitCell = foe.Position.ClampInsideMap(map);
                    if (hitCell.InBounds(map))
                    {
                        DerFreischutzDefOf.LC_DerFreischutz_PierceHit?.PlayOneShot(new TargetInfo(hitCell, map));
                    }

                    foe.TakeDamage(new DamageInfo(blackDef, dmgRange.RandomInRange, armorPen, yaw, caster, weapon: weaponDef));
                    if (!foe.Dead && stunTicks > 0)
                    {
                        foe.stances?.stunner?.StunFor(stunTicks, caster, addBattleLog: false);
                    }
                }
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

    /// <summary>驱动「魔弹齐射」双翼厄运魔门开启（tick 14）与交叉诅咒光束齐射（tick 28）的纯逻辑实体。</summary>
    public class Thing_MagicBulletSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private Vector3 aimDir = Vector3.right;
        private Vector3 leftPortalPos;
        private Vector3 rightPortalPos;
        private FloatRange portalDmgRange;
        private float armorPen;
        private ThingDef weaponDef;
        private int ageTicks;
        private bool portalsOpened;
        private bool beamsFired;
        private bool initialized;

        private const int PortalOpenTick = 14;
        private const int BeamFireTick = 28;
        private const int TotalDurationTicks = 88;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 initialAnchor,
            Vector3 initialDir,
            FloatRange portalDamage,
            float armorPenetration,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = initialAnchor.Yto0();
            aimDir = initialDir.sqrMagnitude > 0.001f ? initialDir.Yto0().normalized : Vector3.right;
            portalDmgRange = portalDamage;
            armorPen = armorPenetration;
            weaponDef = weapon;
            ageTicks = 0;
            portalsOpened = false;
            beamsFired = false;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned || Map == null)
            {
                Destroy();
                return;
            }

            if (caster.equipment?.Primary?.def?.defName != MagicBulletWeaponAnimHandler.WeaponDefName)
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

            if (!portalsOpened && ageTicks >= PortalOpenTick)
            {
                portalsOpened = true;
                OpenFlankingPortals();
            }

            if (!beamsFired && ageTicks >= BeamFireTick)
            {
                beamsFired = true;
                FireConvergingCursedBeams();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy();
            }
        }

        private void OpenFlankingPortals()
        {
            if (Map == null)
            {
                return;
            }

            Vector3 perp = new Vector3(-aimDir.z, 0f, aimDir.x);
            leftPortalPos = ClampToMap(anchorPos - aimDir * 2.8f + perp * 4.8f);
            rightPortalPos = ClampToMap(anchorPos - aimDir * 2.8f - perp * 4.8f);

            Vector3 dirL = (anchorPos - leftPortalPos).Yto0();
            dirL = dirL.sqrMagnitude > 0.001f ? dirL.normalized : aimDir;
            Vector3 dirR = (anchorPos - rightPortalPos).Yto0();
            dirR = dirR.sqrMagnitude > 0.001f ? dirR.normalized : aimDir;

            MagicBulletVfxUtility.SpawnPortal(Map, leftPortalPos, dirL, scale: 1.05f, charging: true, cursedSeventh: true);
            MagicBulletVfxUtility.SpawnPortal(Map, rightPortalPos, dirR, scale: 1.05f, charging: true, cursedSeventh: true);

            TargetInfo loc = new TargetInfo(anchorPos.ToIntVec3().ClampInsideMap(Map), Map);
            DerFreischutzDefOf.LC_DerFreischutz_CursedCreate?.PlayOneShot(loc);
            DerFreischutzDefOf.LC_DerFreischutz_Portal?.PlayOneShot(loc);
        }

        private void FireConvergingCursedBeams()
        {
            if (Map == null)
            {
                return;
            }

            Vector3 dirL = (anchorPos - leftPortalPos).Yto0();
            dirL = dirL.sqrMagnitude > 0.001f ? dirL.normalized : aimDir;
            Vector3 dirR = (anchorPos - rightPortalPos).Yto0();
            dirR = dirR.sqrMagnitude > 0.001f ? dirR.normalized : aimDir;

            float spanL = (anchorPos - leftPortalPos).magnitude + 5.5f;
            float spanR = (anchorPos - rightPortalPos).magnitude + 5.5f;
            Vector3 endL = CompAbilityEffect_MagicBulletBarrage.ComputeMapBoundedEndPoint(Map, leftPortalPos, dirL, spanL);
            Vector3 endR = CompAbilityEffect_MagicBulletBarrage.ComputeMapBoundedEndPoint(Map, rightPortalPos, dirR, spanR);

            TargetInfo loc = new TargetInfo(anchorPos.ToIntVec3().ClampInsideMap(Map), Map);
            DerFreischutzDefOf.LC_DerFreischutz_Shoot?.PlayOneShot(loc);
            DerFreischutzDefOf.LC_DerFreischutz_Bullet?.PlayOneShot(loc);

            CompAbilityEffect_MagicBulletBarrage.FireAllySafePiercingBeam(
                Map, caster, primaryTarget, leftPortalPos, endL,
                beamWidth: 0.64f, corridorWidth: 1.05f, dmgRange: portalDmgRange, armorPen: armorPen, weaponDef: weaponDef, cursedSeventh: true, stunTicks: 45);
            CompAbilityEffect_MagicBulletBarrage.FireAllySafePiercingBeam(
                Map, caster, primaryTarget, rightPortalPos, endR,
                beamWidth: 0.64f, corridorWidth: 1.05f, dmgRange: portalDmgRange, armorPen: armorPen, weaponDef: weaponDef, cursedSeventh: true, stunTicks: 45);
        }

        private Vector3 ClampToMap(Vector3 pos)
        {
            if (Map == null)
            {
                return pos.Yto0();
            }

            return new Vector3(
                Mathf.Clamp(pos.x, 0.8f, Map.Size.x - 0.8f),
                0f,
                Mathf.Clamp(pos.z, 0.8f, Map.Size.z - 0.8f));
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }
    }
}
