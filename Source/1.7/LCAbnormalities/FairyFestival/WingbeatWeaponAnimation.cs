using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.FairyFestival
{
    /// <summary>
    /// F-04-83 精灵盛宴 E.G.O. 武器「振翅」（EgoWeapon_FairyFestival）专属动画处理器：
    /// 贴图原图翅刃朝右（0°），支持轻盈妖精连斩（ClipNormalSlash）与
    /// 主动技能「精灵贪飨盛宴」的猩红翅刃连劈及吸血愈伤演出。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class WingbeatWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_FairyFestival";

        public const string ClipNormalSlash = "Wingbeat_NormalSlash";
        public const string ClipSkillWarmup = "Wingbeat_SkillWarmup";
        public const string ClipSkillFeast = "Wingbeat_SkillFeast";

        private static readonly Vector2 WingDrawSize = new Vector2(1.70f, 1.70f);
        private static readonly Color RoseGhostTint = new Color(0.95f, 0.34f, 0.56f, 0.85f);

        // === Normal Slash (48 ticks): nimble twin-wing sweep -> crisp follow-through -> recover ===
        private static readonly BodyKeyframe[] NormalSlashBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(7f, -0.09f, 0.01f, -9f),
            new BodyKeyframe(14f, 0.22f, -0.02f, 16f),
            new BodyKeyframe(26f, 0.12f, 0f, 8f),
            new BodyKeyframe(48f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalSlashWingA =
        {
            new WeaponKeyframe(0f, 0.30f, -0.06f, -10f, 1.00f),
            new WeaponKeyframe(7f, 0.04f, 0.34f, -48f, 1.05f),
            new WeaponKeyframe(14f, 0.62f, -0.08f, 28f, 1.14f),
            new WeaponKeyframe(26f, 0.48f, -0.14f, 38f, 1.04f),
            new WeaponKeyframe(48f, 0.30f, -0.06f, -10f, 1.00f)
        };

        private static readonly WeaponKeyframe[] NormalSlashWingB =
        {
            new WeaponKeyframe(0f, 0.30f, -0.06f, -10f, 1.00f),
            new WeaponKeyframe(7f, 0.16f, -0.24f, 38f, 1.05f),
            new WeaponKeyframe(14f, 0.64f, 0.14f, -26f, 1.14f),
            new WeaponKeyframe(26f, 0.46f, 0.10f, -18f, 1.04f),
            new WeaponKeyframe(48f, 0.30f, -0.06f, -10f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): raise wing fan as emerald-crimson sparkles gather ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.06f, 0.02f, -7f),
            new BodyKeyframe(1.00f, -0.14f, 0.03f, -14f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupWing =
        {
            new WeaponKeyframe(0.00f, 0.30f, -0.06f, -10f, 1.00f),
            new WeaponKeyframe(0.45f, 0.10f, 0.32f, -46f, 1.08f),
            new WeaponKeyframe(1.00f, -0.04f, 0.44f, -64f, 1.14f)
        };

        // === Skill Feast (86 ticks): Slash 1 @ 5 -> Reversed Slash 2 @ 12 -> Feast Rend @ 24 ===
        private static readonly BodyKeyframe[] SkillFeastBody =
        {
            new BodyKeyframe(0f, -0.14f, 0.03f, -14f),
            new BodyKeyframe(5f, 0.24f, -0.02f, 18f),
            new BodyKeyframe(12f, 0.18f, 0.01f, 10f),
            new BodyKeyframe(18f, -0.12f, 0.03f, -12f),
            new BodyKeyframe(24f, 0.34f, -0.04f, 26f),
            new BodyKeyframe(48f, 0.16f, -0.01f, 10f),
            new BodyKeyframe(86f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillFeastWing =
        {
            new WeaponKeyframe(0f, -0.04f, 0.44f, -64f, 1.14f),
            new WeaponKeyframe(5f, 0.64f, -0.08f, 28f, 1.18f),
            new WeaponKeyframe(12f, 0.60f, 0.16f, -24f, 1.18f),
            new WeaponKeyframe(18f, -0.06f, 0.48f, -70f, 1.22f),
            new WeaponKeyframe(24f, 0.72f, -0.14f, 40f, 1.28f),
            new WeaponKeyframe(48f, 0.50f, -0.10f, 16f, 1.10f),
            new WeaponKeyframe(86f, 0.30f, -0.06f, -10f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillFeast))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            int variant = ((Find.TickManager?.TicksGame ?? 0) & 1);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalSlash, 48, aimAngle, variant: variant);
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
                case ClipNormalSlash:
                    kf = EgoWeaponAnimationUtility.SampleBody(NormalSlashBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillFeast:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillFeastBody, elapsedTicks);
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
                float tickPhase = (Find.TickManager?.TicksGame ?? 0) * 0.12f;
                float flutter = Mathf.Sin(tickPhase) * (moving ? 5.5f : 2.6f);
                float posX = moving ? 0.30f : 0.30f;
                float posZ = (moving ? -0.02f : -0.06f) + Mathf.Sin(tickPhase * 0.9f) * 0.014f;
                float angle = (moving ? -16f : -10f) + flutter;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, WingDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalSlash:
                {
                    WeaponKeyframe[] keys = ctx.ActiveAnim.Variant == 0 ? NormalSlashWingA : NormalSlashWingB;
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(keys, t);
                    if (t >= 6f && t <= 20f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, keys, t, ctx.BaseDrawPos, alt, WingDrawSize, ctx.FacingLeft, RoseGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, WingDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupWing, prog);
                    float shimmer = Mathf.Sin(t * 1.5f) * 0.012f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + shimmer, kf.PosZ, kf.AngleDeg, WingDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillFeast:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillFeastWing, t);
                    if (t >= 1f && t <= 34f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillFeastWing, t, ctx.BaseDrawPos, alt, WingDrawSize, ctx.FacingLeft, RoseGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, WingDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「精灵贪飨盛宴」蓄力动词：举起精灵翅刃召唤妖精粉尘。</summary>
    public class Verb_CastAbility_WingbeatFeast : Verb_CastAbility
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
            if (started && CasterPawn != null && CasterPawn.equipment?.Primary != null &&
                CasterPawn.stances?.curStance is Stance_Warmup warmup && warmup.verb == this)
            {
                Vector3 targetPos = castTarg.IsValid ? castTarg.CenterVector3 : (CasterPawn.DrawPos + Vector3.right);
                float aim = EgoWeaponAnimationUtility.ResolveAimAngle(CasterPawn, targetPos);
                int warmupTicks = Mathf.Max(10, warmup.ticksLeft + 2);

                EgoWeaponAnimationManager.Play(
                    CasterPawn,
                    CasterPawn.equipment.Primary,
                    WingbeatWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                if (CasterPawn.MapHeld != null)
                {
                    FairyFestivalVfxUtility.SpawnHealPulse(CasterPawn.MapHeld, CasterPawn.DrawPos.Yto0(), 1.3f);
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilityWingbeatFeast : CompProperties_AbilityEffect
    {
        public float feastRadius = 2.8f;
        public FloatRange slashDamageRange = new FloatRange(7f, 10f);
        public FloatRange feastDamageRange = new FloatRange(14f, 18f);
        public float armorPenetration = 0.26f;
        public float healAmount = 8f;

        public CompProperties_AbilityWingbeatFeast()
        {
            compClass = typeof(CompAbilityEffect_WingbeatFeast);
        }
    }

    /// <summary>
    /// 「振翅」主动技能效果处理器：
    /// 第 1 拍与第 12 tick 左右交错挥出精灵翅刃斩击吸血，第 24 tick 释放腥红噬飨处决斩击全场敌军并大幅回复持握者生命。
    /// </summary>
    public class CompAbilityEffect_WingbeatFeast : CompAbilityEffect
    {
        public new CompProperties_AbilityWingbeatFeast Props => (CompProperties_AbilityWingbeatFeast)props;

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
            if (weapon != null && weapon.def?.defName == WingbeatWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, WingbeatWeaponAnimHandler.ClipSkillFeast, 86, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(WingbeatWeaponAnimHandler.WeaponDefName);
            FairyFestivalDefOf.LC_Wingbeat_Hit?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));

            Vector3 dir = (anchorPos - caster.DrawPos.Yto0()).Yto0();
            float yaw = dir.sqrMagnitude > 0.001f ? Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg : 0f;
            FairyFestivalVfxUtility.SpawnWingbeatSlash(map, anchorPos, yaw, scale: 1.75f, flipSweep: false, crimsonFeastMode: false, durationTicks: 16);

            if (victim != null && !victim.Dead && !victim.Destroyed)
            {
                victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, Props.slashDamageRange.RandomInRange, Props.armorPenetration, yaw, caster, weapon: weaponDef));
            }

            if (FairyFestivalDefOf.LC_WingbeatSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(FairyFestivalDefOf.LC_WingbeatSkillRunnerFX, caster.PositionHeld, map) is Thing_WingbeatSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.feastRadius, Props.slashDamageRange, Props.feastDamageRange, Props.armorPenetration, Props.healAmount, weaponDef);
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

    /// <summary>驱动「精灵贪飨盛宴」第 12 tick 回旋斩与第 24 tick 腥红盛宴吸血处决的纯逻辑实体。</summary>
    public class Thing_WingbeatSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private float feastRadius;
        private FloatRange slashDmgRange;
        private FloatRange feastDmgRange;
        private float armorPen;
        private float healAmount;
        private ThingDef weaponDef;
        private int ageTicks;
        private int stage;
        private bool initialized;

        private const int TotalDurationTicks = 78;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 anchor,
            float radius,
            FloatRange slashRange,
            FloatRange feastRange,
            float ap,
            float heal,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = anchor;
            feastRadius = Mathf.Max(1.8f, radius);
            slashDmgRange = slashRange;
            feastDmgRange = feastRange;
            armorPen = ap;
            healAmount = heal;
            weaponDef = weapon;
            ageTicks = 0;
            stage = 0;
            initialized = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref primaryTarget, "primaryTarget");
            Scribe_Values.Look(ref anchorPos, "anchorPos");
            Scribe_Values.Look(ref feastRadius, "feastRadius", 2.8f);
            Scribe_Values.Look(ref slashDmgRange, "slashDmgRange", new FloatRange(7f, 10f));
            Scribe_Values.Look(ref feastDmgRange, "feastDmgRange", new FloatRange(14f, 18f));
            Scribe_Values.Look(ref armorPen, "armorPen", 0.26f);
            Scribe_Values.Look(ref healAmount, "healAmount", 8f);
            Scribe_Defs.Look(ref weaponDef, "weaponDef");
            Scribe_Values.Look(ref ageTicks, "ageTicks", 0);
            Scribe_Values.Look(ref stage, "stage", 0);
            Scribe_Values.Look(ref initialized, "initialized", false);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!initialized || Map == null || caster == null || caster.Destroyed || caster.Dead || !caster.Spawned)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            ageTicks++;

            if (stage == 0 && ageTicks >= 12)
            {
                stage = 1;
                Vector3 center = (primaryTarget != null && primaryTarget.Spawned && primaryTarget.Map == Map)
                    ? primaryTarget.DrawPos.Yto0()
                    : anchorPos;
                Vector3 dir = (center - caster.DrawPos.Yto0()).Yto0();
                float yaw = dir.sqrMagnitude > 0.001f ? Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg : 0f;

                FairyFestivalDefOf.LC_Wingbeat_Hit?.PlayOneShot(new TargetInfo(center.ToIntVec3().ClampInsideMap(Map), Map));
                FairyFestivalVfxUtility.SpawnWingbeatSlash(Map, center, yaw, scale: 1.95f, flipSweep: true, crimsonFeastMode: true, durationTicks: 18);
                FairyFestivalVfxUtility.SpawnWingbeatLifesteal(Map, center, caster);

                if (primaryTarget != null && !primaryTarget.Dead && !primaryTarget.Destroyed)
                {
                    primaryTarget.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, slashDmgRange.RandomInRange, armorPen, yaw, caster, weapon: weaponDef));
                }
            }
            else if (stage == 1 && ageTicks >= 24)
            {
                stage = 2;
                ExecuteFeastFinale();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        private void ExecuteFeastFinale()
        {
            Map map = Map;
            if (map == null)
            {
                return;
            }

            Vector3 center = (primaryTarget != null && primaryTarget.Spawned && primaryTarget.Map == map)
                ? primaryTarget.DrawPos.Yto0()
                : anchorPos;
            Vector3 dir = (center - caster.DrawPos.Yto0()).Yto0();
            float yaw = dir.sqrMagnitude > 0.001f ? Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg : 0f;

            IntVec3 cell = center.ToIntVec3().ClampInsideMap(map);
            FairyFestivalDefOf.LC_FairyFestival_Special?.PlayOneShot(new TargetInfo(cell, map));
            FairyFestivalVfxUtility.SpawnDevourExecution(map, center);
            FairyFestivalVfxUtility.SpawnWingbeatSlash(map, center, yaw, scale: 2.55f, flipSweep: false, crimsonFeastMode: true, durationTicks: 20);
            FairyFestivalVfxUtility.SpawnHealPulse(map, caster.DrawPos.Yto0(), 1.45f);
            LCAbnormalitiesHelper.HealInjuries(caster, healAmount);

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

                if ((foe.DrawPos.Yto0() - center).sqrMagnitude <= feastRadius * feastRadius)
                {
                    foe.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, feastDmgRange.RandomInRange, armorPen, yaw, caster, weapon: weaponDef));
                    if (!foe.Dead && !foe.Destroyed)
                    {
                        foe.stances?.stunner?.StunFor(45, caster, addBattleLog: false);
                    }
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }
    }
}
