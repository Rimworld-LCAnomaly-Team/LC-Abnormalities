using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.WellCheers
{
    /// <summary>
    /// F-05-52 韦尔奇乐牌汽水 E.G.O. 武器「汽水」（EgoWeapon_WellCheers）专属动画处理器：
    /// 贴图原图汽水喷枪朝右（0°），支持清凉气泡后坐连射（ClipRangeShot）、近战钝敲（ClipMeleeBash）与
    /// 主动技能「气泡激浪喷射」的加压汽水范围洗礼与回复演出。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class SodaWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_WellCheers";

        public const string ClipRangeShot = "Soda_RangeShot";
        public const string ClipMeleeBash = "Soda_MeleeBash";
        public const string ClipSkillWarmup = "Soda_SkillWarmup";
        public const string ClipSkillSplash = "Soda_SkillSplash";

        private static readonly Vector2 SodaDrawSize = new Vector2(1.55f, 1.55f);
        private static readonly Color FizzGhostTint = new Color(0.24f, 0.88f, 0.96f, 0.85f);

        // === Range Shot (22 ticks): snappy fizzy kickback -> crisp aim recovery ===
        private static readonly BodyKeyframe[] RangeShotBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -3f),
            new BodyKeyframe(4f, -0.08f, 0.01f, -7f),
            new BodyKeyframe(12f, -0.01f, 0f, -1f),
            new BodyKeyframe(22f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] RangeShotGun =
        {
            new WeaponKeyframe(0f, 0.42f, -0.02f, -2f, 1.00f),
            new WeaponKeyframe(4f, 0.28f, 0.12f, -24f, 1.08f),
            new WeaponKeyframe(12f, 0.40f, 0.00f, -5f, 1.03f),
            new WeaponKeyframe(22f, 0.38f, -0.04f, 0f, 1.00f)
        };

        // === Melee Bash (40 ticks): light pressurized canister bash ===
        private static readonly BodyKeyframe[] MeleeBashBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(7f, 0.16f, -0.01f, 13f),
            new BodyKeyframe(18f, 0.08f, 0f, 6f),
            new BodyKeyframe(40f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] MeleeBashGun =
        {
            new WeaponKeyframe(0f, 0.16f, 0.22f, -38f, 1.00f),
            new WeaponKeyframe(7f, 0.54f, -0.06f, 24f, 1.08f),
            new WeaponKeyframe(18f, 0.44f, -0.08f, 12f, 1.02f),
            new WeaponKeyframe(40f, 0.38f, -0.04f, 0f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): shake pressurized soda gun enthusiastically ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.06f, 0.01f, -6f),
            new BodyKeyframe(1.00f, -0.10f, 0.02f, -10f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupGun =
        {
            new WeaponKeyframe(0.00f, 0.38f, -0.04f, 0f, 1.00f),
            new WeaponKeyframe(0.45f, 0.24f, 0.14f, -26f, 1.06f),
            new WeaponKeyframe(1.00f, 0.20f, 0.18f, -34f, 1.12f)
        };

        // === Skill Splash (84 ticks): pressure blast forward (0..10) -> giant fizz splash recoil @ 18 ===
        private static readonly BodyKeyframe[] SkillSplashBody =
        {
            new BodyKeyframe(0f, -0.10f, 0.02f, -10f),
            new BodyKeyframe(7f, 0.14f, 0f, 10f),
            new BodyKeyframe(18f, -0.16f, 0.03f, -15f),
            new BodyKeyframe(42f, -0.04f, 0.01f, -4f),
            new BodyKeyframe(84f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillSplashGun =
        {
            new WeaponKeyframe(0f, 0.20f, 0.18f, -34f, 1.12f),
            new WeaponKeyframe(7f, 0.54f, -0.02f, 2f, 1.16f),
            new WeaponKeyframe(18f, 0.24f, 0.22f, -36f, 1.22f),
            new WeaponKeyframe(42f, 0.42f, 0.00f, -6f, 1.06f),
            new WeaponKeyframe(84f, 0.38f, -0.04f, 0f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillSplash))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipMeleeBash, 40, aimAngle);
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
                case ClipSkillSplash:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillSplashBody, elapsedTicks);
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
                float sway = Mathf.Sin(tickPhase) * (moving ? 5.0f : 2.0f);
                float posX = moving ? 0.34f : 0.38f;
                float posZ = (moving ? 0.00f : -0.04f) + Mathf.Sin(tickPhase * 0.8f) * 0.010f;
                float angle = (moving ? -6f : 0f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, SodaDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipRangeShot:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(RangeShotGun, t);
                    if (t >= 2f && t <= 12f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, RangeShotGun, t, ctx.BaseDrawPos, alt, SodaDrawSize, ctx.FacingLeft, FizzGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, SodaDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipMeleeBash:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(MeleeBashGun, t);
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, SodaDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupGun, prog);
                    float shakeFizz = Mathf.Sin(t * 2.2f) * 0.016f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + shakeFizz, kf.PosZ, kf.AngleDeg, SodaDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillSplash:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillSplashGun, t);
                    if (t >= 2f && t <= 26f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillSplashGun, t, ctx.BaseDrawPos, alt, SodaDrawSize, ctx.FacingLeft, FizzGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, SodaDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「气泡激浪喷射」蓄力动词：摇晃加压汽水罐蓄力准备喷涌。</summary>
    public class Verb_CastAbility_SodaSplash : Verb_CastAbility
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
                    SodaWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                if (CasterPawn.MapHeld != null)
                {
                    WellCheersDefOf.LC_WellCheers_Dispense?.PlayOneShot(new TargetInfo(CasterPawn.PositionHeld, CasterPawn.MapHeld));
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilitySodaSplash : CompProperties_AbilityEffect
    {
        public float splashRadius = 4.0f;
        public FloatRange shotDamageRange = new FloatRange(10f, 14f);
        public FloatRange splashDamageRange = new FloatRange(14f, 18f);
        public float armorPenetration = 0.25f;
        public float healAmount = 7f;

        public CompProperties_AbilitySodaSplash()
        {
            compClass = typeof(CompAbilityEffect_SodaSplash);
        }
    }

    /// <summary>
    /// 「汽水」主动技能效果处理器：
    /// 第 1 拍发射加压樱桃气泡高压冲击单体（红伤），第 18 tick 爆开深海汽水浪潮洗刷 4.0 格内敌军（红伤 + 晕眩），并散落治愈水雾滋润持握者伤势与精神。
    /// </summary>
    public class CompAbilityEffect_SodaSplash : CompAbilityEffect
    {
        public new CompProperties_AbilitySodaSplash Props => (CompProperties_AbilitySodaSplash)props;

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
            Vector3 dir = (anchorPos - cPos).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(caster, anchorPos);
            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon != null && weapon.def?.defName == SodaWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, SodaWeaponAnimHandler.ClipSkillSplash, 84, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(SodaWeaponAnimHandler.WeaponDefName);
            WellCheersDefOf.LC_WellCheers_Drink?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));
            WellCheersVfxUtility.SpawnMuzzleFizz(map, cPos + dir * 0.55f, dir);
            WellCheersVfxUtility.SpawnImpactSplash(map, anchorPos, hitLiving: true);

            if (victim != null && !victim.Dead && !victim.Destroyed)
            {
                victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, Props.shotDamageRange.RandomInRange, Props.armorPenetration, -1f, caster, weapon: weaponDef));
            }

            if (WellCheersDefOf.LC_SodaSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(WellCheersDefOf.LC_SodaSkillRunnerFX, caster.PositionHeld, map) is Thing_SodaSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.splashRadius, Props.splashDamageRange, Props.armorPenetration, Props.healAmount, weaponDef);
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

    /// <summary>驱动「气泡激浪喷射」第 18 tick 三色汽水激浪爆发与治愈水雾的纯逻辑实体。</summary>
    public class Thing_SodaSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private float splashRadius;
        private FloatRange splashDmgRange;
        private float armorPen;
        private float healAmount;
        private ThingDef weaponDef;
        private int ageTicks;
        private bool burstDone;
        private bool initialized;

        private const int TotalDurationTicks = 76;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 anchor,
            float radius,
            FloatRange splashRange,
            float ap,
            float heal,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = anchor;
            splashRadius = Mathf.Max(2.0f, radius);
            splashDmgRange = splashRange;
            armorPen = ap;
            healAmount = heal;
            weaponDef = weapon;
            ageTicks = 0;
            burstDone = false;
            initialized = true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref primaryTarget, "primaryTarget");
            Scribe_Values.Look(ref anchorPos, "anchorPos");
            Scribe_Values.Look(ref splashRadius, "splashRadius", 4.0f);
            Scribe_Values.Look(ref splashDmgRange, "splashDmgRange", new FloatRange(14f, 18f));
            Scribe_Values.Look(ref armorPen, "armorPen", 0.25f);
            Scribe_Values.Look(ref healAmount, "healAmount", 7f);
            Scribe_Defs.Look(ref weaponDef, "weaponDef");
            Scribe_Values.Look(ref ageTicks, "ageTicks", 0);
            Scribe_Values.Look(ref burstDone, "burstDone", false);
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

            if (!burstDone && ageTicks >= 18)
            {
                burstDone = true;
                ExecuteSodaWave();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        private void ExecuteSodaWave()
        {
            Map map = Map;
            if (map == null)
            {
                return;
            }

            Vector3 center = (primaryTarget != null && primaryTarget.Spawned && primaryTarget.Map == map)
                ? primaryTarget.DrawPos.Yto0()
                : anchorPos;

            IntVec3 cell = center.ToIntVec3().ClampInsideMap(map);
            WellCheersDefOf.LC_WellCheers_Dispense?.PlayOneShot(new TargetInfo(cell, map));
            WellCheersVfxUtility.SpawnImpactSplash(map, center, hitLiving: true);
            FleckMaker.ThrowDustPuffThick(center, map, 1.8f, new Color(0.24f, 0.88f, 0.96f));
            FleckMaker.ThrowDustPuffThick(center, map, 1.4f, new Color(0.95f, 0.34f, 0.56f));
            WellCheersVfxUtility.SpawnHealMist(caster);
            LCAbnormalitiesHelper.HealInjuries(caster, healAmount);
            LCAbnormalitiesHelper.RestoreMentalHealth(caster, 0.16f, 0.05f);

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

                if ((foe.DrawPos.Yto0() - center).sqrMagnitude <= splashRadius * splashRadius)
                {
                    foe.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, splashDmgRange.RandomInRange, armorPen, -1f, caster, weapon: weaponDef));
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
