using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.SnowQueen
{
    /// <summary>
    /// F-01-37 冰雪女皇 E.G.O. 武器「霜之碎片」（EgoWeapon_SnowQueen）专属动画处理器：
    /// 关键几何约束：EgoWeapon_SnowQueen.png 原图枪柄在右上、冰晶矛尖指向左下 -135°（CW +135°），
    /// 故 localAngleDeg = -135° 时冰晶矛尖水平向前，-165° 上挑，-215° 高举，-115° 下压戒备。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class FrostSplinterWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_SnowQueen";

        public const string ClipNormalThrust = "FrostSplinter_NormalThrust";
        public const string ClipSkillWarmup = "FrostSplinter_SkillWarmup";
        public const string ClipSkillPierce = "FrostSplinter_SkillPierce";

        private static readonly Vector2 LanceDrawSize = new Vector2(2.10f, 2.10f);
        private static readonly Color IceAfterimageTint = new Color(0.62f, 0.94f, 1.0f, 0.85f);

        // === Normal Thrust (54 ticks): pull back glacial lance -> razor forward lunge -> recover ===
        private static readonly BodyKeyframe[] NormalThrustBody =
        {
            new BodyKeyframe(0f, -0.05f, 0f, -5f),
            new BodyKeyframe(10f, -0.13f, 0.01f, -11f),
            new BodyKeyframe(17f, 0.24f, 0f, 18f),
            new BodyKeyframe(30f, 0.14f, 0f, 10f),
            new BodyKeyframe(54f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalThrustLance =
        {
            new WeaponKeyframe(0f, 0.24f, -0.08f, -148f, 1.00f),
            new WeaponKeyframe(10f, -0.08f, 0.04f, -118f, 1.05f),
            new WeaponKeyframe(17f, 0.88f, -0.02f, -135f, 1.15f),
            new WeaponKeyframe(30f, 0.68f, -0.05f, -128f, 1.05f),
            new WeaponKeyframe(54f, 0.24f, -0.08f, -148f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): raise lance diagonally & draw back ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.09f, 0.01f, -9f),
            new BodyKeyframe(1.00f, -0.17f, 0.02f, -15f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupLance =
        {
            new WeaponKeyframe(0.00f, 0.24f, -0.08f, -148f, 1.00f),
            new WeaponKeyframe(0.45f, -0.04f, 0.14f, -172f, 1.06f),
            new WeaponKeyframe(1.00f, -0.18f, 0.18f, -122f, 1.12f)
        };

        // === Skill Pierce (84 ticks): long glacial skewer lunge + burst shatter @ tick 18 ===
        private static readonly BodyKeyframe[] SkillPierceBody =
        {
            new BodyKeyframe(0f, -0.17f, 0.02f, -15f),
            new BodyKeyframe(7f, 0.34f, -0.01f, 24f),
            new BodyKeyframe(16f, 0.22f, 0.02f, 14f),
            new BodyKeyframe(22f, 0.14f, 0.03f, 6f),
            new BodyKeyframe(50f, 0.08f, 0.01f, 4f),
            new BodyKeyframe(84f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillPierceLance =
        {
            new WeaponKeyframe(0f, -0.18f, 0.18f, -122f, 1.12f),
            new WeaponKeyframe(7f, 0.98f, -0.02f, -135f, 1.24f),
            new WeaponKeyframe(16f, 0.80f, 0.04f, -152f, 1.18f),
            new WeaponKeyframe(22f, 0.36f, 0.22f, -198f, 1.14f),
            new WeaponKeyframe(50f, 0.32f, 0.08f, -172f, 1.06f),
            new WeaponKeyframe(84f, 0.24f, -0.08f, -148f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillPierce))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalThrust, 54, aimAngle);
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
                case ClipNormalThrust:
                    kf = EgoWeaponAnimationUtility.SampleBody(NormalThrustBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillPierce:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillPierceBody, elapsedTicks);
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
                float posX = moving ? 0.26f : 0.24f;
                float posZ = (moving ? -0.04f : -0.08f) + Mathf.Sin(tickPhase * 0.8f) * 0.010f;
                float angle = (moving ? -140f : -148f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, LanceDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalThrust:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(NormalThrustLance, t);
                    if (t >= 9f && t <= 22f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, NormalThrustLance, t, ctx.BaseDrawPos, alt, LanceDrawSize, ctx.FacingLeft, IceAfterimageTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, LanceDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupLance, prog);
                    float chill = Mathf.Sin(t * 1.6f) * 0.010f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + chill, kf.PosZ, kf.AngleDeg, LanceDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillPierce:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillPierceLance, t);
                    if (t >= 1f && t <= 20f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillPierceLance, t, ctx.BaseDrawPos, alt, LanceDrawSize, ctx.FacingLeft, IceAfterimageTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, LanceDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「极寒决斗」蓄力动词：举矛凝结冰晶霜气。</summary>
    public class Verb_CastAbility_FrostSplinterDuel : Verb_CastAbility
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
                    FrostSplinterWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                if (casterPawn.MapHeld != null)
                {
                    SnowQueenVfxUtility.SpawnFrostSplinterHit(casterPawn.MapHeld, casterPawn.DrawPos.Yto0());
                    LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Kiss1, casterPawn);
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilityFrostSplinterDuel : CompProperties_AbilityEffect
    {
        public float burstRadius = 3.2f;
        public FloatRange lanceDamageRange = new FloatRange(16f, 22f);
        public FloatRange shatterDamageRange = new FloatRange(22f, 28f);
        public float armorPenetration = 0.42f;

        public CompProperties_AbilityFrostSplinterDuel()
        {
            compClass = typeof(CompAbilityEffect_FrostSplinterDuel);
        }
    }

    /// <summary>
    /// 霜之碎片主动技能「极寒决斗」效果处理器：
    /// 第 1 拍极寒长矛贯刺爆发冰霜碎瓣并施加迟缓，第 18 tick 引爆三阶冰吻霜裂对 3.2 格敌军造成白伤与眩晕。
    /// </summary>
    public class CompAbilityEffect_FrostSplinterDuel : CompAbilityEffect
    {
        public new CompProperties_AbilityFrostSplinterDuel Props => (CompProperties_AbilityFrostSplinterDuel)props;

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
            if (weapon != null && weapon.def?.defName == FrostSplinterWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, FrostSplinterWeaponAnimHandler.ClipSkillPierce, 84, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(FrostSplinterWeaponAnimHandler.WeaponDefName);
            SnowQueenDefOf.LC_SnowQueen_Win_Sword?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));
            SnowQueenVfxUtility.SpawnFrostSplinterHit(map, anchorPos);

            if (victim != null && !victim.Dead && !victim.Destroyed)
            {
                victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, Props.lanceDamageRange.RandomInRange, Props.armorPenetration, -1f, caster, weapon: weaponDef));
                if (!victim.Dead && SnowQueenDefOf.LC_FrostSlow != null)
                {
                    LCAbnormalitiesHelper.ApplyOrRefreshHediff(victim, SnowQueenDefOf.LC_FrostSlow, 1f);
                }
            }

            if (SnowQueenVfxDefOf.LC_FrostSplinterSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(SnowQueenVfxDefOf.LC_FrostSplinterSkillRunnerFX, caster.PositionHeld, map) is Thing_FrostSplinterSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.burstRadius, Props.shatterDamageRange, Props.armorPenetration, weaponDef);
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

    /// <summary>驱动「极寒决斗」第 18 tick 三阶冰吻霜裂爆发的纯逻辑实体。</summary>
    public class Thing_FrostSplinterSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private float burstRadius;
        private FloatRange shatterDmgRange;
        private float armorPen;
        private ThingDef weaponDef;
        private int ageTicks;
        private bool burstExecuted;
        private bool initialized;

        private const int BurstTick = 18;
        private const int TotalDurationTicks = 80;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 initialAnchor,
            float radius,
            FloatRange shatterDmg,
            float armorPenetration,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = initialAnchor.Yto0();
            burstRadius = Mathf.Max(2.0f, radius);
            shatterDmgRange = shatterDmg;
            armorPen = armorPenetration;
            weaponDef = weapon;
            ageTicks = 0;
            burstExecuted = false;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned || Map == null)
            {
                Destroy();
                return;
            }

            if (caster.equipment?.Primary?.def?.defName != FrostSplinterWeaponAnimHandler.WeaponDefName)
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

            if (!burstExecuted && ageTicks >= BurstTick)
            {
                burstExecuted = true;
                ExecuteGlacialShatter();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy();
            }
        }

        private void ExecuteGlacialShatter()
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

            SnowQueenDefOf.LC_SnowQueen_EnergyFreeze?.PlayOneShot(new TargetInfo(cell, Map));
            SnowQueenVfxUtility.SpawnKissBurst(Map, anchorPos, isThirdKiss: true);
            SnowQueenVfxUtility.SpawnFrostSplinterHit(Map, anchorPos);

            DamageDef whiteDef = LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage;
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

                if ((foe.DrawPos.Yto0() - anchorPos).sqrMagnitude <= burstRadius * burstRadius)
                {
                    foe.TakeDamage(new DamageInfo(whiteDef, shatterDmgRange.RandomInRange, armorPen, -1f, caster, weapon: weaponDef));
                    if (!foe.Dead && !foe.Destroyed)
                    {
                        if (SnowQueenDefOf.LC_FrostSlow != null)
                        {
                            LCAbnormalitiesHelper.ApplyOrRefreshHediff(foe, SnowQueenDefOf.LC_FrostSlow, 1f);
                        }
                        foe.stances?.stunner?.StunFor(65, caster, addBattleLog: false);
                    }
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }
    }
}
