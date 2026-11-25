using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.AllAroundHelper
{
    /// <summary>
    /// T-05-41 万能帮手 E.G.O. 武器「研磨机 Mk4」（EgoWeapon_AllAroundHelper）专属动画处理器：
    /// 贴图原图刃盘朝右上 +45°（angle=+45° 水平向前、-45° 垂直向上）。
    /// 支持机械电机高频细颤、高速回拉平斩（ClipNormalSaw），以及主动技能「过载突切」的多拍火花撕锯连段。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class GrinderMk4WeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_AllAroundHelper";

        public const string ClipNormalSaw = "GrinderMk4_NormalSaw";
        public const string ClipSkillWarmup = "GrinderMk4_SkillWarmup";
        public const string ClipSkillRush = "GrinderMk4_SkillRush";

        private static readonly Vector2 SawDrawSize = new Vector2(2.05f, 2.05f);
        private static readonly Color CrimsonSawGhostTint = new Color(0.98f, 0.22f, 0.24f, 0.85f);

        // === Normal Saw (54 ticks): windup pullback -> ferocious forward saw carve -> retract ===
        private static readonly BodyKeyframe[] NormalSawBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(8f, -0.10f, 0.01f, -9f),
            new BodyKeyframe(15f, 0.22f, -0.01f, 16f),
            new BodyKeyframe(30f, 0.16f, 0f, 11f),
            new BodyKeyframe(54f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalSawKeys =
        {
            new WeaponKeyframe(0f, 0.28f, -0.12f, 22f, 1.00f),
            new WeaponKeyframe(8f, -0.04f, 0.36f, -62f, 1.06f),
            new WeaponKeyframe(15f, 0.64f, -0.04f, 44f, 1.14f),
            new WeaponKeyframe(30f, 0.56f, -0.08f, 50f, 1.06f),
            new WeaponKeyframe(54f, 0.28f, -0.12f, 22f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): coil red-spattered buzzsaw behind hip ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.40f, -0.09f, 0.01f, -9f),
            new BodyKeyframe(1.00f, -0.16f, 0.02f, -15f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupKeys =
        {
            new WeaponKeyframe(0.00f, 0.28f, -0.12f, 22f, 1.00f),
            new WeaponKeyframe(0.45f, -0.06f, 0.32f, -54f, 1.06f),
            new WeaponKeyframe(1.00f, -0.16f, 0.38f, -74f, 1.12f)
        };

        // === Skill Rush (88 ticks): dash surge -> 3 rapid saw arcs (12, 24, 36) -> overdrive cleave @ 44 ===
        private static readonly BodyKeyframe[] SkillRushBody =
        {
            new BodyKeyframe(0f, -0.16f, 0.02f, -15f),
            new BodyKeyframe(7f, 0.32f, -0.02f, 22f),
            new BodyKeyframe(18f, 0.20f, 0.02f, 12f),
            new BodyKeyframe(30f, 0.28f, -0.02f, 20f),
            new BodyKeyframe(40f, -0.06f, 0.03f, -10f),
            new BodyKeyframe(46f, 0.34f, -0.03f, 26f),
            new BodyKeyframe(66f, 0.18f, -0.01f, 10f),
            new BodyKeyframe(88f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillRushKeys =
        {
            new WeaponKeyframe(0f, -0.16f, 0.38f, -74f, 1.12f),
            new WeaponKeyframe(7f, 0.68f, -0.04f, 44f, 1.20f),
            new WeaponKeyframe(18f, 0.14f, 0.26f, -34f, 1.16f),
            new WeaponKeyframe(28f, 0.66f, -0.12f, 58f, 1.20f),
            new WeaponKeyframe(38f, -0.08f, 0.44f, -82f, 1.22f),
            new WeaponKeyframe(45f, 0.74f, -0.10f, 48f, 1.26f),
            new WeaponKeyframe(66f, 0.52f, -0.14f, 50f, 1.10f),
            new WeaponKeyframe(88f, 0.28f, -0.12f, 22f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillRush))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalSaw, 54, aimAngle);
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
                case ClipNormalSaw:
                    kf = EgoWeaponAnimationUtility.SampleBody(NormalSawBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillRush:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillRushBody, elapsedTicks);
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
            float idleHum = Mathf.Sin((Find.TickManager?.TicksGame ?? 0) * 2.1f) * 0.007f;

            if (ctx.ActiveAnim == null)
            {
                bool moving = ctx.Pawn.pather != null && ctx.Pawn.pather.Moving;
                float sway = Mathf.Sin((Find.TickManager?.TicksGame ?? 0) * 0.11f) * (moving ? 4.5f : 2.0f);
                float posX = moving ? 0.25f : 0.28f;
                float posZ = (moving ? -0.05f : -0.12f) + idleHum;
                float angle = (moving ? 32f : 22f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, SawDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;
            float revVib = Mathf.Sin(t * 3.0f) * 0.017f;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalSaw:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(NormalSawKeys, t);
                    if (t >= 7f && t <= 22f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, NormalSawKeys, t, ctx.BaseDrawPos, alt, SawDrawSize, ctx.FacingLeft, CrimsonSawGhostTint);
                    }
                    float vib = (t >= 11f && t <= 32f) ? revVib : idleHum;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + vib, kf.PosZ, kf.AngleDeg, SawDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupKeys, prog);
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + revVib * prog, kf.PosZ, kf.AngleDeg, SawDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillRush:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillRushKeys, t);
                    if (t >= 3f && t <= 52f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillRushKeys, t, ctx.BaseDrawPos, alt, SawDrawSize, ctx.FacingLeft, CrimsonSawGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + revVib, kf.PosZ, kf.AngleDeg, SawDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「过载突切」蓄力动词：飞轮预热发出刺耳充能音并迸射火花。</summary>
    public class Verb_CastAbility_GrinderMk4Rush : Verb_CastAbility
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
                    GrinderMk4WeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                Map map = casterPawn.MapHeld;
                if (map != null)
                {
                    AllAroundHelperVfxUtility.SpawnSparkBurst(map, casterPawn.DrawPos.Yto0(), scale: 1.0f, lifeTicks: 16, isCrimson: false);
                    LCAbnormalitiesHelper.PlaySoundAt(AllAroundHelperDefOf.LC_AllAroundHelper_Gaging, casterPawn);
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilityGrinderMk4Rush : CompProperties_AbilityEffect
    {
        public float shredRadius = 2.5f;
        public FloatRange openerDamageRange = new FloatRange(12f, 16f);
        public FloatRange pulseDamageRange = new FloatRange(6f, 9f);
        public FloatRange finaleDamageRange = new FloatRange(18f, 24f);
        public float armorPenetration = 0.42f;

        public CompProperties_AbilityGrinderMk4Rush()
        {
            compClass = typeof(CompAbilityEffect_GrinderMk4Rush);
        }
    }

    /// <summary>
    /// 研磨机 Mk4 主动技能「过载突切」效果处理器：
    /// 第 1 拍甩出冲刺光影轨迹与旋转锯阵切入敌群，随后连续三拍高速猩红锯切，第 44 tick 过载终结重斩。
    /// </summary>
    public class CompAbilityEffect_GrinderMk4Rush : CompAbilityEffect
    {
        public new CompProperties_AbilityGrinderMk4Rush Props => (CompProperties_AbilityGrinderMk4Rush)props;

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

            Vector3 cPos = caster.DrawPos.Yto0();
            Vector3 anchorPos = victim != null && victim.Spawned ? victim.DrawPos.Yto0() : target.CenterVector3.Yto0();
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(caster, anchorPos);
            Vector3 dir = (anchorPos - cPos).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();
            float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;

            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon != null && weapon.def?.defName == GrinderMk4WeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, GrinderMk4WeaponAnimHandler.ClipSkillRush, 88, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(GrinderMk4WeaponAnimHandler.WeaponDefName);
            AllAroundHelperDefOf.LC_AllAroundHelper_GageMax?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));
            AllAroundHelperVfxUtility.SpawnDashTrail(map, cPos, anchorPos, width: 1.95f, lifeTicks: 22);
            AllAroundHelperVfxUtility.SpawnGrinderFlurry(map, anchorPos, yaw);

            ApplySawBurst(map, caster, victim, anchorPos, Props.shredRadius, Props.openerDamageRange, Props.armorPenetration, weaponDef, yaw);

            if (AllAroundHelperDefOf.LC_GrinderMk4SkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(AllAroundHelperDefOf.LC_GrinderMk4SkillRunnerFX, caster.PositionHeld, map) is Thing_GrinderMk4SkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.shredRadius, Props.pulseDamageRange, Props.finaleDamageRange, Props.armorPenetration, weaponDef);
                }
            }
        }

        internal static void ApplySawBurst(
            Map map,
            Pawn caster,
            Pawn primaryTarget,
            Vector3 center,
            float radius,
            FloatRange dmgRange,
            float armorPen,
            ThingDef weaponDef,
            float yawDeg,
            int stunTicks = 0)
        {
            if (map == null || caster == null)
            {
                return;
            }

            DamageDef redDef = LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
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

                if ((foe.DrawPos.Yto0() - center).sqrMagnitude <= radius * radius)
                {
                    foe.TakeDamage(new DamageInfo(redDef, dmgRange.RandomInRange, armorPen, yawDeg, caster, weapon: weaponDef));
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

    /// <summary>驱动「过载突切」多拍连锯与第 44 tick 终结重斩的纯逻辑实体。</summary>
    public class Thing_GrinderMk4SkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private float shredRadius;
        private FloatRange pulseDmgRange;
        private FloatRange finaleDmgRange;
        private float armorPen;
        private ThingDef weaponDef;
        private int ageTicks;
        private int pulseStep;
        private bool finaleExecuted;
        private bool initialized;

        private const int FinaleTick = 44;
        private const int TotalDurationTicks = 84;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 initialAnchor,
            float radius,
            FloatRange pulseDmg,
            FloatRange finaleDmg,
            float armorPenetration,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = initialAnchor.Yto0();
            shredRadius = Mathf.Max(1.8f, radius);
            pulseDmgRange = pulseDmg;
            finaleDmgRange = finaleDmg;
            armorPen = armorPenetration;
            weaponDef = weapon;
            ageTicks = 0;
            pulseStep = 0;
            finaleExecuted = false;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned || Map == null)
            {
                Destroy();
                return;
            }

            if (caster.equipment?.Primary?.def?.defName != GrinderMk4WeaponAnimHandler.WeaponDefName)
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

            Vector3 dir = (anchorPos - caster.DrawPos.Yto0()).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();
            float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;

            if ((ageTicks == 12 && pulseStep == 0) || (ageTicks == 24 && pulseStep == 1) || (ageTicks == 36 && pulseStep == 2))
            {
                pulseStep++;
                bool flip = (pulseStep % 2) == 1;
                AllAroundHelperVfxUtility.SpawnSawSlash(Map, anchorPos, yaw + (flip ? 28f : -28f), scale: 2.15f, flipSweep: flip, lifeTicks: 14, spanRad: 2.6f);
                AllAroundHelperVfxUtility.SpawnSparkBurst(Map, anchorPos, scale: 1.0f, lifeTicks: 12, isCrimson: true);
                IntVec3 cell = anchorPos.ToIntVec3().ClampInsideMap(Map);
                if (cell.InBounds(Map))
                {
                    AllAroundHelperDefOf.LC_GrinderMk4_Hit?.PlayOneShot(new TargetInfo(cell, Map));
                }
                CompAbilityEffect_GrinderMk4Rush.ApplySawBurst(Map, caster, primaryTarget, anchorPos, shredRadius, pulseDmgRange, armorPen, weaponDef, yaw);
            }

            if (!finaleExecuted && ageTicks >= FinaleTick)
            {
                finaleExecuted = true;
                IntVec3 cell = anchorPos.ToIntVec3().ClampInsideMap(Map);
                if (cell.InBounds(Map))
                {
                    AllAroundHelperDefOf.LC_AllAroundHelper_Transform?.PlayOneShot(new TargetInfo(cell, Map));
                }
                AllAroundHelperVfxUtility.SpawnGrinderFlurry(Map, anchorPos, yaw);
                AllAroundHelperVfxUtility.SpawnSparkBurst(Map, anchorPos, scale: 1.45f, lifeTicks: 16, isCrimson: true);
                CompAbilityEffect_GrinderMk4Rush.ApplySawBurst(Map, caster, primaryTarget, anchorPos, shredRadius + 0.4f, finaleDmgRange, armorPen + 0.06f, weaponDef, yaw, stunTicks: 45);
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }
    }
}
