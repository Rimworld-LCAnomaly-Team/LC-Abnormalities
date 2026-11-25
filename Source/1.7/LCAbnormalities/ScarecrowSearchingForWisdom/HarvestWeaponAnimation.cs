using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.ScarecrowSearchingForWisdom
{
    /// <summary>
    /// F-01-87 索求智慧的稻草人 E.G.O. 武器「收获」（EgoWeapon_ScarecrowSearchingForWisdom）专属动画处理器：
    /// 贴图原图铁耙朝右上 +45°（angle=0° 右上、+45° 水平向前）。
    /// 支持凶狠长耙拖刺连击（ClipNormalRake）与主动技能「智慧收割」的吸取脑智共鸣波动演出。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class HarvestWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_ScarecrowSearchingForWisdom";

        public const string ClipNormalRake = "Harvest_NormalRake";
        public const string ClipSkillWarmup = "Harvest_SkillWarmup";
        public const string ClipSkillDrain = "Harvest_SkillDrain";

        private static readonly Vector2 RakeDrawSize = new Vector2(2.15f, 2.15f);
        private static readonly Color VioletGhostTint = new Color(0.84f, 0.28f, 0.92f, 0.85f);

        // === Normal Rake (54 ticks): pull rake high/back -> snapping forward claw rake -> recover ===
        private static readonly BodyKeyframe[] NormalRakeBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(9f, -0.10f, 0.01f, -9f),
            new BodyKeyframe(16f, 0.22f, -0.02f, 17f),
            new BodyKeyframe(30f, 0.14f, -0.01f, 10f),
            new BodyKeyframe(54f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalRakeKeys =
        {
            new WeaponKeyframe(0f, 0.28f, -0.12f, 22f, 1.00f),
            new WeaponKeyframe(9f, -0.06f, 0.40f, -68f, 1.06f),
            new WeaponKeyframe(16f, 0.72f, -0.06f, 44f, 1.15f),
            new WeaponKeyframe(30f, 0.56f, -0.12f, 54f, 1.05f),
            new WeaponKeyframe(54f, 0.28f, -0.12f, 22f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): raise rake overhead as violet siphon whispers ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.40f, -0.08f, 0.02f, -8f),
            new BodyKeyframe(1.00f, -0.15f, 0.02f, -14f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupKeys =
        {
            new WeaponKeyframe(0.00f, 0.28f, -0.12f, 22f, 1.00f),
            new WeaponKeyframe(0.45f, -0.02f, 0.40f, -66f, 1.06f),
            new WeaponKeyframe(1.00f, -0.14f, 0.48f, -92f, 1.12f)
        };

        // === Skill Drain (88 ticks): thrust rake deep into target + hold siphon @ ticks 16 & 32 ===
        private static readonly BodyKeyframe[] SkillDrainBody =
        {
            new BodyKeyframe(0f, -0.15f, 0.02f, -14f),
            new BodyKeyframe(8f, 0.28f, -0.02f, 20f),
            new BodyKeyframe(22f, 0.20f, 0f, 14f),
            new BodyKeyframe(36f, 0.26f, -0.02f, 18f),
            new BodyKeyframe(56f, 0.12f, 0f, 8f),
            new BodyKeyframe(88f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillDrainKeys =
        {
            new WeaponKeyframe(0f, -0.14f, 0.48f, -92f, 1.12f),
            new WeaponKeyframe(8f, 0.78f, -0.04f, 44f, 1.22f),
            new WeaponKeyframe(18f, 0.62f, -0.02f, 38f, 1.16f),
            new WeaponKeyframe(32f, 0.76f, -0.08f, 48f, 1.20f),
            new WeaponKeyframe(56f, 0.50f, -0.12f, 36f, 1.08f),
            new WeaponKeyframe(88f, 0.28f, -0.12f, 22f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillDrain))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalRake, 54, aimAngle);
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
                case ClipNormalRake:
                    kf = EgoWeaponAnimationUtility.SampleBody(NormalRakeBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillDrain:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillDrainBody, elapsedTicks);
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
                float sway = Mathf.Sin(tickPhase) * (moving ? 4.5f : 2.0f);
                float posX = moving ? 0.24f : 0.28f;
                float posZ = (moving ? -0.05f : -0.12f) + Mathf.Sin(tickPhase * 0.8f) * 0.012f;
                float angle = (moving ? 12f : 22f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, RakeDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalRake:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(NormalRakeKeys, t);
                    if (t >= 7f && t <= 22f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, NormalRakeKeys, t, ctx.BaseDrawPos, alt, RakeDrawSize, ctx.FacingLeft, VioletGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, RakeDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupKeys, prog);
                    float hum = Mathf.Sin(t * 1.5f) * 0.010f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + hum, kf.PosZ, kf.AngleDeg, RakeDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillDrain:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillDrainKeys, t);
                    if (t >= 1f && t <= 36f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillDrainKeys, t, ctx.BaseDrawPos, alt, RakeDrawSize, ctx.FacingLeft, VioletGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, RakeDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「智慧收割」蓄力动词：举起四齿铁耙聚拢渴智黑雾。</summary>
    public class Verb_CastAbility_HarvestDrain : Verb_CastAbility
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
                    HarvestWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                if (casterPawn.MapHeld != null)
                {
                    ScarecrowDefOf.LC_Scarecrow_Start?.PlayOneShot(new TargetInfo(casterPawn.PositionHeld, casterPawn.MapHeld));
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilityHarvestDrain : CompProperties_AbilityEffect
    {
        public float drainRadius = 2.8f;
        public FloatRange openerDamageRange = new FloatRange(14f, 20f);
        public FloatRange siphonDamageRange = new FloatRange(12f, 17f);
        public float armorPenetration = 0.42f;

        public CompProperties_AbilityHarvestDrain()
        {
            compClass = typeof(CompAbilityEffect_HarvestDrain);
        }
    }

    /// <summary>
    /// 收获主动技能「智慧收割」效果处理器：
    /// 第 1 拍狠耙贯入目标并展开汲取脉动光环，第 16 与 32 tick 连续两次吸取敌军黑伤并将活力与心智反哺持握者。
    /// </summary>
    public class CompAbilityEffect_HarvestDrain : CompAbilityEffect
    {
        public new CompProperties_AbilityHarvestDrain Props => (CompProperties_AbilityHarvestDrain)props;

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

            Vector3 anchorPos = victim != null && victim.Spawned ? victim.DrawPos.Yto0() : target.CenterVector3.Yto0();
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(caster, anchorPos);
            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon != null && weapon.def?.defName == HarvestWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, HarvestWeaponAnimHandler.ClipSkillDrain, 88, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(HarvestWeaponAnimHandler.WeaponDefName);
            ScarecrowDefOf.LC_Scarecrow_SpearHit?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));
            ScarecrowVfxUtility.SpawnDrainAura(map, anchorPos, scale: 2.65f, lifeTicks: 48);

            if (victim != null && !victim.Dead && !victim.Destroyed)
            {
                victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, Props.openerDamageRange.RandomInRange, Props.armorPenetration, -1f, caster, weapon: weaponDef));
                if (!victim.Dead && !victim.Destroyed)
                {
                    victim.stances?.stunner?.StunFor(45, caster, addBattleLog: false);
                }
            }

            if (ScarecrowDefOf.LC_HarvestSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(ScarecrowDefOf.LC_HarvestSkillRunnerFX, caster.PositionHeld, map) is Thing_HarvestSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.drainRadius, Props.siphonDamageRange, Props.armorPenetration, weaponDef);
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

    /// <summary>驱动「智慧收割」第 16 与 32 tick 脑智虹吸脉动的纯逻辑实体。</summary>
    public class Thing_HarvestSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private float drainRadius;
        private FloatRange siphonDmgRange;
        private float armorPen;
        private ThingDef weaponDef;
        private int ageTicks;
        private int siphonStep;
        private bool initialized;

        private const int TotalDurationTicks = 84;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 initialAnchor,
            float radius,
            FloatRange siphonDmg,
            float armorPenetration,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = initialAnchor.Yto0();
            drainRadius = Mathf.Max(1.8f, radius);
            siphonDmgRange = siphonDmg;
            armorPen = armorPenetration;
            weaponDef = weapon;
            ageTicks = 0;
            siphonStep = 0;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned || Map == null)
            {
                Destroy();
                return;
            }

            if (caster.equipment?.Primary?.def?.defName != HarvestWeaponAnimHandler.WeaponDefName)
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

            if ((ageTicks == 16 && siphonStep == 0) || (ageTicks == 32 && siphonStep == 1))
            {
                siphonStep++;
                ExecuteSiphonPulse(siphonStep == 2);
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy();
            }
        }

        private void ExecuteSiphonPulse(bool isSecond)
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

            SoundDef sfx = isSecond ? ScarecrowDefOf.LC_Scarecrow_Drink : ScarecrowDefOf.LC_Scarecrow_Atk;
            sfx?.PlayOneShot(new TargetInfo(cell, Map));

            bool anySiphoned = false;
            DamageDef blackDef = LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage;
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

                if ((foe.DrawPos.Yto0() - anchorPos).sqrMagnitude <= drainRadius * drainRadius)
                {
                    anySiphoned = true;
                    foe.TakeDamage(new DamageInfo(blackDef, siphonDmgRange.RandomInRange, armorPen, -1f, caster, weapon: weaponDef));
                }
            }

            if (anySiphoned)
            {
                LCAbnormalitiesHelper.HealInjuries(caster, 6f);
                LCAbnormalitiesHelper.RestoreMentalHealth(caster, 0.12f, 0.04f);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }
    }
}
