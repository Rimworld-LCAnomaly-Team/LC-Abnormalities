using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.WarmHeartedWoodsman
{
    /// <summary>
    /// F-05-32 铁皮樵夫 E.G.O. 武器「伐木」（EgoWeapon_WarmHeartedWoodsman）专属动画处理器：
    /// 贴图原图斧头朝右上 +45°（angle=0° 右上、+45° 水平向前、-110° 脑后高举蓄力）。
    /// 支持沉重蒸汽巨斧蓄力劈砍（ClipNormalChop）与主动技能「热心伐木斩」的高举熔炉处决演出。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class LoggingWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_WarmHeartedWoodsman";

        public const string ClipNormalChop = "Logging_NormalChop";
        public const string ClipSkillWarmup = "Logging_SkillWarmup";
        public const string ClipSkillCleave = "Logging_SkillCleave";

        private static readonly Vector2 AxeDrawSize = new Vector2(2.15f, 2.15f);
        private static readonly Color EmberGhostTint = new Color(1.0f, 0.34f, 0.16f, 0.85f);

        // === Normal Chop (62 ticks): heavy overhead hoist -> crushing downward axe chop -> recover ===
        private static readonly BodyKeyframe[] NormalChopBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(11f, -0.10f, 0.02f, -11f),
            new BodyKeyframe(18f, 0.22f, -0.03f, 18f),
            new BodyKeyframe(36f, 0.14f, -0.01f, 11f),
            new BodyKeyframe(62f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalChopAxe =
        {
            new WeaponKeyframe(0f, 0.30f, -0.14f, 20f, 1.00f),
            new WeaponKeyframe(11f, -0.14f, 0.54f, -112f, 1.08f),
            new WeaponKeyframe(18f, 0.62f, -0.20f, 44f, 1.15f),
            new WeaponKeyframe(36f, 0.54f, -0.24f, 52f, 1.06f),
            new WeaponKeyframe(62f, 0.30f, -0.14f, 20f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): raise massive cog-axe behind shoulder as furnace flares ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.40f, -0.08f, 0.02f, -9f),
            new BodyKeyframe(1.00f, -0.16f, 0.03f, -16f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupAxe =
        {
            new WeaponKeyframe(0.00f, 0.30f, -0.14f, 20f, 1.00f),
            new WeaponKeyframe(0.45f, 0.04f, 0.44f, -76f, 1.06f),
            new WeaponKeyframe(1.00f, -0.18f, 0.56f, -118f, 1.12f)
        };

        // === Skill Cleave (90 ticks): sweeping cleave -> colossal overhead guillotine chop @ tick 24 ===
        private static readonly BodyKeyframe[] SkillCleaveBody =
        {
            new BodyKeyframe(0f, -0.16f, 0.03f, -16f),
            new BodyKeyframe(9f, 0.22f, -0.01f, 16f),
            new BodyKeyframe(16f, -0.16f, 0.04f, -18f),
            new BodyKeyframe(24f, 0.32f, -0.05f, 28f),
            new BodyKeyframe(46f, 0.24f, -0.03f, 20f),
            new BodyKeyframe(90f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillCleaveAxe =
        {
            new WeaponKeyframe(0f, -0.18f, 0.56f, -118f, 1.12f),
            new WeaponKeyframe(9f, 0.58f, -0.10f, 38f, 1.16f),
            new WeaponKeyframe(16f, -0.20f, 0.62f, -128f, 1.20f),
            new WeaponKeyframe(24f, 0.70f, -0.24f, 48f, 1.26f),
            new WeaponKeyframe(46f, 0.64f, -0.26f, 52f, 1.14f),
            new WeaponKeyframe(90f, 0.30f, -0.14f, 20f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillCleave))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalChop, 62, aimAngle);
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
                case ClipNormalChop:
                    kf = EgoWeaponAnimationUtility.SampleBody(NormalChopBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillCleave:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillCleaveBody, elapsedTicks);
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
                float sway = Mathf.Sin(tickPhase) * (moving ? 4.5f : 2.2f);
                float posX = moving ? 0.24f : 0.30f;
                float posZ = (moving ? -0.04f : -0.14f) + Mathf.Sin(tickPhase * 0.7f) * 0.014f;
                float angle = (moving ? 8f : 20f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, AxeDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalChop:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(NormalChopAxe, t);
                    if (t >= 9f && t <= 24f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, NormalChopAxe, t, ctx.BaseDrawPos, alt, AxeDrawSize, ctx.FacingLeft, EmberGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, AxeDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupAxe, prog);
                    float steamPulse = Mathf.Sin(t * 1.3f) * 0.012f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + steamPulse, kf.PosZ, kf.AngleDeg, AxeDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillCleave:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillCleaveAxe, t);
                    if (t >= 1f && t <= 30f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillCleaveAxe, t, ctx.BaseDrawPos, alt, AxeDrawSize, ctx.FacingLeft, EmberGhostTint);
                    }
                    float impactQuake = (t >= 23f && t <= 33f) ? Mathf.Sin(t * 2.5f) * 0.025f * (1f - (t - 23f) / 10f) : 0f;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ + impactQuake, kf.AngleDeg, AxeDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「热心伐木斩」蓄力动词：举斧聚燃炉心烈焰。</summary>
    public class Verb_CastAbility_LoggingCleave : Verb_CastAbility
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
                    LoggingWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                Map map = casterPawn.MapHeld;
                if (map != null)
                {
                    WarmHeartedWoodsmanVfxUtility.SpawnFurnaceAura(map, casterPawn.DrawPos.Yto0(), scale: 2.1f, lifeTicks: Mathf.Min(32, warmupTicks));
                    WarmHeartedWoodsmanDefOf.LC_Woodsman_Special?.PlayOneShot(new TargetInfo(casterPawn.PositionHeld, map));
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilityLoggingCleave : CompProperties_AbilityEffect
    {
        public float cleaveRadius = 2.8f;
        public FloatRange openerDamageRange = new FloatRange(16f, 22f);
        public FloatRange executeDamageRange = new FloatRange(32f, 42f);
        public float armorPenetration = 0.45f;

        public CompProperties_AbilityLoggingCleave()
        {
            compClass = typeof(CompAbilityEffect_LoggingCleave);
        }
    }

    /// <summary>
    /// 伐木主动技能「热心伐木斩」效果处理器：
    /// 第 1 拍重斧横扫斩击，第 24 tick 唤出铁闸断头斧斩爆碎目标（对半血以下敌军追加 +30% 斩杀伤并回复持握者生命）。
    /// </summary>
    public class CompAbilityEffect_LoggingCleave : CompAbilityEffect
    {
        public new CompProperties_AbilityLoggingCleave Props => (CompProperties_AbilityLoggingCleave)props;

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
            if (weapon != null && weapon.def?.defName == LoggingWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, LoggingWeaponAnimHandler.ClipSkillCleave, 90, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(LoggingWeaponAnimHandler.WeaponDefName);
            WarmHeartedWoodsmanDefOf.LC_Woodsman_Atk?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));

            List<Pawn> candidates = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn foe = candidates[i];
                if (foe == null || foe == caster || foe.Dead || foe.Destroyed || !foe.Spawned)
                {
                    continue;
                }
                if (foe != victim && !foe.HostileTo(caster))
                {
                    continue;
                }

                if ((foe.DrawPos.Yto0() - anchorPos).sqrMagnitude <= 2.4f * 2.4f)
                {
                    foe.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, Props.openerDamageRange.RandomInRange, Props.armorPenetration, -1f, caster, weapon: weaponDef));
                }
            }

            if (WarmHeartedWoodsmanDefOf.LC_LoggingSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(WarmHeartedWoodsmanDefOf.LC_LoggingSkillRunnerFX, caster.PositionHeld, map) is Thing_LoggingSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.cleaveRadius, Props.executeDamageRange, Props.armorPenetration, weaponDef);
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

    /// <summary>驱动「热心伐木斩」第 24 tick 铁闸熔炉断头斩的纯逻辑实体。</summary>
    public class Thing_LoggingSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private float cleaveRadius;
        private FloatRange executeDmgRange;
        private float armorPen;
        private ThingDef weaponDef;
        private int ageTicks;
        private bool executeDone;
        private bool initialized;

        private const int ExecuteTick = 24;
        private const int TotalDurationTicks = 86;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 initialAnchor,
            float radius,
            FloatRange executeDmg,
            float armorPenetration,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = initialAnchor.Yto0();
            cleaveRadius = Mathf.Max(2.0f, radius);
            executeDmgRange = executeDmg;
            armorPen = armorPenetration;
            weaponDef = weapon;
            ageTicks = 0;
            executeDone = false;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned || Map == null)
            {
                Destroy();
                return;
            }

            if (caster.equipment?.Primary?.def?.defName != LoggingWeaponAnimHandler.WeaponDefName)
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

            if (!executeDone && ageTicks >= ExecuteTick)
            {
                executeDone = true;
                ExecuteGuillotineChop();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy();
            }
        }

        private void ExecuteGuillotineChop()
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

            WarmHeartedWoodsmanDefOf.LC_Woodsman_AxeHit?.PlayOneShot(new TargetInfo(cell, Map));
            WarmHeartedWoodsmanVfxUtility.SpawnShutterExecution(Map, anchorPos, scale: 2.9f, lifeTicks: 32);
            WarmHeartedWoodsmanVfxUtility.SpawnFurnaceAura(Map, anchorPos, scale: 2.5f, lifeTicks: 28);

            bool anyHit = false;
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

                if ((foe.DrawPos.Yto0() - anchorPos).sqrMagnitude <= cleaveRadius * cleaveRadius)
                {
                    anyHit = true;
                    bool lowHp = (foe.health?.summaryHealth?.SummaryHealthPercent ?? 1f) < 0.50f;
                    float dmg = executeDmgRange.RandomInRange * (lowHp ? 1.30f : 1.0f);
                    foe.TakeDamage(new DamageInfo(redDef, dmg, armorPen + 0.1f, -1f, caster, weapon: weaponDef));
                    if (!foe.Dead && !foe.Destroyed)
                    {
                        foe.stances?.stunner?.StunFor(55, caster, addBattleLog: false);
                    }
                }
            }

            if (anyHit)
            {
                LCAbnormalitiesHelper.HealInjuries(caster, 8f);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }
    }
}
