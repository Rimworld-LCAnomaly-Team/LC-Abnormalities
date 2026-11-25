using System.Collections.Generic;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.ScorchedGirl
{
    /// <summary>
    /// F-01-02 焦化少女 E.G.O. 武器「终末火柴之光」（EgoWeapon_ScorchedGirl）专属动画处理器：
    /// 贴图原图火柴加农炮朝右（0°），支持厚重火柴炮后坐喷焰（ClipRangeShot）、近战钝砸（ClipMeleeBash）与
    /// 主动技能「第四根火柴之焰」的擦火蓄燃、膛口喷涌及终末爆燃轰击。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class FourthMatchWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_ScorchedGirl";

        public const string ClipRangeShot = "FourthMatch_RangeShot";
        public const string ClipMeleeBash = "FourthMatch_MeleeBash";
        public const string ClipSkillWarmup = "FourthMatch_SkillWarmup";
        public const string ClipSkillIgnition = "FourthMatch_SkillIgnition";

        private static readonly Vector2 CannonDrawSize = new Vector2(1.85f, 1.85f);
        private static readonly Color EmberGhostTint = new Color(1.0f, 0.44f, 0.12f, 0.85f);

        // === Range Shot (28 ticks): braced heavy match-cannon kick -> slow smoke recovery ===
        private static readonly BodyKeyframe[] RangeShotBody =
        {
            new BodyKeyframe(0f, -0.05f, 0f, -4f),
            new BodyKeyframe(5f, -0.14f, 0.01f, -11f),
            new BodyKeyframe(16f, -0.03f, 0f, -2f),
            new BodyKeyframe(28f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] RangeShotCannon =
        {
            new WeaponKeyframe(0f, 0.44f, -0.02f, -2f, 1.00f),
            new WeaponKeyframe(5f, 0.24f, 0.14f, -22f, 1.10f),
            new WeaponKeyframe(16f, 0.42f, 0.00f, -4f, 1.04f),
            new WeaponKeyframe(28f, 0.40f, -0.04f, 0f, 1.00f)
        };

        // === Melee Bash (44 ticks): heavy matchstick club swing ===
        private static readonly BodyKeyframe[] MeleeBashBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(8f, -0.08f, 0.01f, -8f),
            new BodyKeyframe(14f, 0.20f, -0.02f, 16f),
            new BodyKeyframe(28f, 0.10f, 0f, 8f),
            new BodyKeyframe(44f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] MeleeBashCannon =
        {
            new WeaponKeyframe(0f, 0.40f, -0.04f, 0f, 1.00f),
            new WeaponKeyframe(8f, 0.10f, 0.30f, -50f, 1.04f),
            new WeaponKeyframe(14f, 0.62f, -0.10f, 28f, 1.12f),
            new WeaponKeyframe(28f, 0.48f, -0.12f, 18f, 1.04f),
            new WeaponKeyframe(44f, 0.40f, -0.04f, 0f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): raise cannon shoulder-high while striker sparks fizz ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.08f, 0.01f, -8f),
            new BodyKeyframe(1.00f, -0.15f, 0.02f, -15f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupCannon =
        {
            new WeaponKeyframe(0.00f, 0.40f, -0.04f, 0f, 1.00f),
            new WeaponKeyframe(0.45f, 0.22f, 0.18f, -32f, 1.08f),
            new WeaponKeyframe(1.00f, 0.16f, 0.24f, -44f, 1.15f)
        };

        // === Skill Ignition (86 ticks): point-blank ignition roar -> heavy cannon blast @ 12 -> bomb flash @ 24 ===
        private static readonly BodyKeyframe[] SkillIgnitionBody =
        {
            new BodyKeyframe(0f, -0.15f, 0.02f, -15f),
            new BodyKeyframe(12f, 0.12f, 0f, 8f),
            new BodyKeyframe(18f, -0.22f, 0.03f, -20f),
            new BodyKeyframe(32f, -0.12f, 0.02f, -10f),
            new BodyKeyframe(56f, -0.03f, 0f, -2f),
            new BodyKeyframe(86f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillIgnitionCannon =
        {
            new WeaponKeyframe(0f, 0.16f, 0.24f, -44f, 1.15f),
            new WeaponKeyframe(10f, 0.50f, 0.00f, 0f, 1.18f),
            new WeaponKeyframe(15f, 0.18f, 0.18f, -28f, 1.26f),
            new WeaponKeyframe(26f, 0.28f, 0.14f, -18f, 1.18f),
            new WeaponKeyframe(52f, 0.42f, -0.02f, -4f, 1.06f),
            new WeaponKeyframe(86f, 0.40f, -0.04f, 0f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillIgnition))
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
                case ClipSkillIgnition:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillIgnitionBody, elapsedTicks);
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
                float sway = Mathf.Sin(tickPhase) * (moving ? 4.5f : 1.8f);
                float posX = moving ? 0.36f : 0.40f;
                float posZ = (moving ? -0.02f : -0.04f) + Mathf.Sin(tickPhase * 0.7f) * 0.010f;
                float angle = (moving ? -8f : 0f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, CannonDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipRangeShot:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(RangeShotCannon, t);
                    if (t >= 3f && t <= 16f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, RangeShotCannon, t, ctx.BaseDrawPos, alt, CannonDrawSize, ctx.FacingLeft, EmberGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, CannonDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipMeleeBash:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(MeleeBashCannon, t);
                    if (t >= 6f && t <= 20f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, MeleeBashCannon, t, ctx.BaseDrawPos, alt, CannonDrawSize, ctx.FacingLeft, EmberGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, CannonDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupCannon, prog);
                    float ember = Mathf.Sin(t * 1.7f) * 0.014f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + ember, kf.PosZ, kf.AngleDeg, CannonDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillIgnition:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillIgnitionCannon, t);
                    if (t >= 8f && t <= 34f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillIgnitionCannon, t, ctx.BaseDrawPos, alt, CannonDrawSize, ctx.FacingLeft, EmberGhostTint);
                    }
                    float shake = (t >= 12f && t <= 30f) ? Mathf.Sin(t * 2.4f) * 0.022f * (1f - (t - 12f) / 18f) : 0f;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ + shake, kf.AngleDeg, CannonDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「第四根火柴之焰」普通射击动词：开火时触发后坐与膛口烈焰。</summary>
    public class Verb_ShootFourthMatch : Verb_Shoot
    {
        protected override bool TryCastShot()
        {
            bool fired = base.TryCastShot();
            if (fired && CasterPawn != null && CasterPawn.Spawned && CasterPawn.MapHeld != null)
            {
                Vector3 cPos = CasterPawn.DrawPos.Yto0();
                Vector3 targetPos = currentTarget.IsValid
                    ? (currentTarget.HasThing && currentTarget.Thing.Spawned ? currentTarget.Thing.DrawPos.Yto0() : currentTarget.CenterVector3.Yto0())
                    : (cPos + Vector3.right);
                Vector3 dir = (targetPos - cPos).Yto0();
                if (dir.sqrMagnitude < 0.001f)
                {
                    dir = Vector3.right;
                }
                dir.Normalize();

                if (!EgoWeaponAnimationManager.IsPlaying(CasterPawn, FourthMatchWeaponAnimHandler.ClipSkillWarmup)
                    && !EgoWeaponAnimationManager.IsPlaying(CasterPawn, FourthMatchWeaponAnimHandler.ClipSkillIgnition))
                {
                    float aim = EgoWeaponAnimationUtility.ResolveAimAngle(CasterPawn, targetPos);
                    EgoWeaponAnimationManager.Play(CasterPawn, CasterPawn.equipment?.Primary, FourthMatchWeaponAnimHandler.ClipRangeShot, 28, aim);
                }

                ScorchedGirlVfx.SpawnMuzzleBlast(cPos + dir * 0.65f, dir, CasterPawn.MapHeld);
            }

            return fired;
        }
    }

    /// <summary>「第四根火柴之焰」主动技能蓄力动词：举炮擦亮火星准备轰击。</summary>
    public class Verb_CastAbility_FourthMatchIgnition : Verb_CastAbility
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
                    FourthMatchWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                if (CasterPawn.MapHeld != null)
                {
                    ScorchedGirlDefOf.LC_ScorchedGirl_MatchStrike?.PlayOneShot(new TargetInfo(CasterPawn.PositionHeld, CasterPawn.MapHeld));
                    ScorchedGirlVfx.SpawnEmberSpark(CasterPawn.DrawPos.Yto0(), CasterPawn.MapHeld, 0.85f);
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilityFourthMatchIgnition : CompProperties_AbilityEffect
    {
        public float burnRadius = 3.8f;
        public FloatRange directDamageRange = new FloatRange(16f, 22f);
        public FloatRange explosionDamageRange = new FloatRange(20f, 28f);
        public float armorPenetration = 0.38f;

        public CompProperties_AbilityFourthMatchIgnition()
        {
            compClass = typeof(CompAbilityEffect_FourthMatchIgnition);
        }
    }

    /// <summary>
    /// 「终末火柴之光」主动技能效果处理器：
    /// 第 1 拍锁定目标并重击直伤，第 12 tick 加农炮吼喷出膛口火流，第 24 tick 在目标脚下引爆 3.8 格赤焰爆破（红伤 + 晕眩）。
    /// </summary>
    public class CompAbilityEffect_FourthMatchIgnition : CompAbilityEffect
    {
        public new CompProperties_AbilityFourthMatchIgnition Props => (CompProperties_AbilityFourthMatchIgnition)props;

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
            if (weapon != null && weapon.def?.defName == FourthMatchWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, FourthMatchWeaponAnimHandler.ClipSkillIgnition, 86, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(FourthMatchWeaponAnimHandler.WeaponDefName);
            ScorchedGirlDefOf.LC_ScorchedGirl_Prime?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));
            ScorchedGirlVfx.SpawnEmberSpark(caster.DrawPos.Yto0(), map, 0.9f);
            ScorchedGirlVfx.SpawnWarningMark(anchorPos, map, 1.35f);

            if (victim != null && !victim.Dead && !victim.Destroyed)
            {
                victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, Props.directDamageRange.RandomInRange, Props.armorPenetration, -1f, caster, weapon: weaponDef));
            }

            if (ScorchedGirlDefOf.LC_FourthMatchSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(ScorchedGirlDefOf.LC_FourthMatchSkillRunnerFX, caster.PositionHeld, map) is Thing_FourthMatchSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.burnRadius, Props.explosionDamageRange, Props.armorPenetration, weaponDef);
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

    /// <summary>驱动「第四根火柴之焰」第 12 tick 炮焰飞掠与第 24 tick 赤焰轰爆的纯逻辑实体。</summary>
    public class Thing_FourthMatchSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private float burnRadius;
        private FloatRange explosionDmgRange;
        private float armorPen;
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
            FloatRange explosionRange,
            float ap,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = anchor;
            burnRadius = Mathf.Max(2.0f, radius);
            explosionDmgRange = explosionRange;
            armorPen = ap;
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
            Scribe_Values.Look(ref burnRadius, "burnRadius", 3.8f);
            Scribe_Values.Look(ref explosionDmgRange, "explosionDmgRange", new FloatRange(20f, 28f));
            Scribe_Values.Look(ref armorPen, "armorPen", 0.38f);
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
                FireVolleyBeam();
            }
            else if (stage == 1 && ageTicks >= 24)
            {
                stage = 2;
                ExecuteExplosion();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        private void FireVolleyBeam()
        {
            Map map = Map;
            if (map == null)
            {
                return;
            }

            Vector3 cPos = caster.DrawPos.Yto0();
            Vector3 endPos = (primaryTarget != null && primaryTarget.Spawned && primaryTarget.Map == map)
                ? primaryTarget.DrawPos.Yto0()
                : anchorPos;
            Vector3 dir = (endPos - cPos).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();

            float angle = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
            ScorchedGirlDefOf.LC_ScorchedGirl_Cannon?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));
            ScorchedGirlVfx.SpawnMuzzleBlast(cPos + dir * 0.7f, dir, map);

            for (int i = 1; i <= 4; i++)
            {
                Vector3 samplePos = Vector3.Lerp(cPos, endPos, i / 5f);
                ScorchedGirlVfx.SpawnProjectileTrail(samplePos, angle, map);
            }
        }

        private void ExecuteExplosion()
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

            ScorchedGirlDefOf.LC_ScorchedGirl_Explode?.PlayOneShot(new TargetInfo(cell, map));
            ScorchedGirlVfx.SpawnFlameExplosion(center, map, 1.85f);
            FleckMaker.ThrowMicroSparks(center, map);

            if (Find.CurrentMap == map && Find.CameraDriver != null)
            {
                Find.CameraDriver.shaker.DoShake(0.55f);
            }

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

                if ((foe.DrawPos.Yto0() - center).sqrMagnitude <= burnRadius * burnRadius)
                {
                    foe.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, explosionDmgRange.RandomInRange, armorPen, -1f, caster, weapon: weaponDef));
                    if (!foe.Dead && !foe.Destroyed)
                    {
                        foe.stances?.stunner?.StunFor(40, caster, addBattleLog: false);
                    }
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }
    }
}
