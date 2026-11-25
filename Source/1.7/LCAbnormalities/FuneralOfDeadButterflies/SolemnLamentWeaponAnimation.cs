using System.Collections.Generic;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.FuneralOfDeadButterflies
{
    /// <summary>
    /// T-01-68 亡蝶葬仪 E.G.O. 武器「圣宣」（EgoWeapon_FuneralOfDeadButterflies）专属动画处理器：
    /// 贴图原图黑白双枪枪口朝右（0°），支持敏捷双枪交替连点后坐（ClipRangeShot）、近战枪托鞭击（ClipMeleeWhip）与
    /// 主动技能「死蝶哀歌」（SolemnLament_ButterflyRequiem）的开棺安魂齐射演出。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class SolemnLamentWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_FuneralOfDeadButterflies";

        public const string ClipRangeShot = "SolemnLament_RangeShot";
        public const string ClipMeleeWhip = "SolemnLament_MeleeWhip";
        public const string ClipSkillWarmup = "SolemnLament_SkillWarmup";
        public const string ClipSkillRequiem = "SolemnLament_SkillRequiem";

        private static readonly Vector2 PistolsDrawSize = new Vector2(1.35f, 1.35f);

        // === Range Shot (20 ticks): alternating twin-pistol kick -> calm aim recovery ===
        private static readonly BodyKeyframe[] RangeShotBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -3f),
            new BodyKeyframe(4f, -0.07f, 0.01f, -6f),
            new BodyKeyframe(12f, 0f, 0f, 0f),
            new BodyKeyframe(20f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] RangeShotPistols =
        {
            new WeaponKeyframe(0f, 0.42f, -0.02f, -4f, 1.00f),
            new WeaponKeyframe(4f, 0.30f, 0.12f, -26f, 1.06f),
            new WeaponKeyframe(11f, 0.40f, 0.00f, -6f, 1.02f),
            new WeaponKeyframe(20f, 0.38f, -0.04f, 0f, 1.00f)
        };

        // === Melee Whip (42 ticks): quick solemn strike ===
        private static readonly BodyKeyframe[] MeleeWhipBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(7f, 0.16f, -0.01f, 14f),
            new BodyKeyframe(18f, 0.10f, 0f, 8f),
            new BodyKeyframe(42f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] MeleeWhipPistols =
        {
            new WeaponKeyframe(0f, 0.16f, 0.22f, -42f, 1.00f),
            new WeaponKeyframe(7f, 0.56f, -0.04f, 22f, 1.06f),
            new WeaponKeyframe(18f, 0.46f, -0.08f, 14f, 1.02f),
            new WeaponKeyframe(42f, 0.38f, -0.04f, 0f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): raise both pistols solemnly ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.05f, 0.01f, -5f),
            new BodyKeyframe(1.00f, -0.09f, 0.02f, -9f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupPistols =
        {
            new WeaponKeyframe(0.00f, 0.38f, -0.04f, 0f, 1.00f),
            new WeaponKeyframe(0.45f, 0.30f, 0.16f, -32f, 1.04f),
            new WeaponKeyframe(1.00f, 0.42f, 0.04f, -4f, 1.06f)
        };

        // === Skill Requiem (88 ticks): 6 rapid shots (0..30) + solemn final coffin salvo @ 38 ===
        private static readonly BodyKeyframe[] SkillRequiemBody =
        {
            new BodyKeyframe(0f, -0.07f, 0f, -6f),
            new BodyKeyframe(16f, -0.04f, 0f, -3f),
            new BodyKeyframe(30f, -0.08f, 0.01f, -7f),
            new BodyKeyframe(38f, -0.14f, 0.02f, -14f),
            new BodyKeyframe(56f, -0.03f, 0f, -2f),
            new BodyKeyframe(88f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillRequiemPistols =
        {
            new WeaponKeyframe(0f, 0.34f, 0.10f, -22f, 1.05f),
            new WeaponKeyframe(6f, 0.40f, 0.04f, -12f, 1.04f),
            new WeaponKeyframe(12f, 0.32f, 0.12f, -24f, 1.06f),
            new WeaponKeyframe(18f, 0.40f, 0.04f, -12f, 1.04f),
            new WeaponKeyframe(24f, 0.31f, 0.13f, -26f, 1.06f),
            new WeaponKeyframe(30f, 0.39f, 0.05f, -14f, 1.05f),
            new WeaponKeyframe(38f, 0.24f, 0.18f, -38f, 1.12f),
            new WeaponKeyframe(56f, 0.40f, 0.00f, -8f, 1.03f),
            new WeaponKeyframe(88f, 0.38f, -0.04f, 0f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillRequiem))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipMeleeWhip, 42, aimAngle);
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
                case ClipSkillRequiem:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillRequiemBody, elapsedTicks);
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
                float tickPhase = (Find.TickManager?.TicksGame ?? 0) * 0.11f;
                float sway = Mathf.Sin(tickPhase) * (moving ? 4.5f : 1.8f);
                float posX = moving ? 0.34f : 0.38f;
                float posZ = (moving ? -0.02f : -0.04f) + Mathf.Sin(tickPhase * 0.8f) * 0.008f;
                float angle = (moving ? -6f : 0f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, PistolsDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipRangeShot:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(RangeShotPistols, t);
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, PistolsDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipMeleeWhip:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(MeleeWhipPistols, t);
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, PistolsDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupPistols, prog);
                    float tremble = Mathf.Sin(t * 1.8f) * 0.008f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + tremble, kf.PosZ, kf.AngleDeg, PistolsDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillRequiem:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillRequiemPistols, t);
                    float kick = (t < 34f) ? Mathf.Sin(t * 2.8f) * 2.2f : 0f;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg + kick, PistolsDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>圣宣（Solemn Lament）E.G.O. 普通射击动词：每次连射触发双枪交替上跳后坐动作。</summary>
    public class Verb_ShootSolemnLament : Verb_Shoot
    {
        protected override bool TryCastShot()
        {
            bool fired = base.TryCastShot();
            if (fired && Caster is Pawn casterPawn && casterPawn.equipment?.Primary is ThingWithComps weapon &&
                weapon.def?.defName == SolemnLamentWeaponAnimHandler.WeaponDefName &&
                !EgoWeaponAnimationManager.IsPlaying(casterPawn, SolemnLamentWeaponAnimHandler.ClipSkillWarmup) &&
                !EgoWeaponAnimationManager.IsPlaying(casterPawn, SolemnLamentWeaponAnimHandler.ClipSkillRequiem))
            {
                Vector3 targetPos = currentTarget.IsValid
                    ? (currentTarget.HasThing && currentTarget.Thing.Spawned ? currentTarget.Thing.DrawPos.Yto0() : currentTarget.CenterVector3.Yto0())
                    : (casterPawn.DrawPos.Yto0() + Vector3.right);
                float aim = EgoWeaponAnimationUtility.ResolveAimAngle(casterPawn, targetPos);
                EgoWeaponAnimationManager.Play(casterPawn, weapon, SolemnLamentWeaponAnimHandler.ClipRangeShot, 20, aim);
            }

            return fired;
        }
    }

    /// <summary>「死蝶哀歌」蓄力动词：举起黑白双枪并奏响亡棺开启之音。</summary>
    public class Verb_CastAbility_SolemnLamentRequiem : Verb_CastAbility
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
                    SolemnLamentWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                if (casterPawn.MapHeld != null)
                {
                    FuneralDefOf.LC_Funeral_Open?.PlayOneShot(new TargetInfo(casterPawn.PositionHeld, casterPawn.MapHeld));
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilitySolemnLamentRequiem : CompProperties_AbilityEffect
    {
        public FloatRange volleyDamageRange = new FloatRange(7f, 10f);
        public FloatRange finaleDamageRange = new FloatRange(18f, 24f);
        public float finaleRadius = 3.0f;
        public float armorPenetration = 0.48f;

        public CompProperties_AbilitySolemnLamentRequiem()
        {
            compClass = typeof(CompAbilityEffect_SolemnLamentRequiem);
        }
    }

    /// <summary>
    /// 圣宣主动技能「死蝶哀歌」效果处理器：
    /// 第 0~30 tick 连续六拍交替倾泻黑白双色蝶弹（白伤/黑伤交替绽放），
    /// 第 38 tick 召唤亡蝶棺群奔涌洗礼目标及周围 3.0 格内敌对目标，完成白黑双伤镇压并眩晕敌军。
    /// </summary>
    public class CompAbilityEffect_SolemnLamentRequiem : CompAbilityEffect
    {
        public new CompProperties_AbilitySolemnLamentRequiem Props => (CompProperties_AbilitySolemnLamentRequiem)props;

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
            if (weapon != null && weapon.def?.defName == SolemnLamentWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, SolemnLamentWeaponAnimHandler.ClipSkillRequiem, 88, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(SolemnLamentWeaponAnimHandler.WeaponDefName);
            FireButterflyShot(map, caster, victim, anchorPos, isBlack: false, Props.volleyDamageRange.RandomInRange, Props.armorPenetration, weaponDef);

            if (FuneralDefOf.LC_SolemnLamentSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(FuneralDefOf.LC_SolemnLamentSkillRunnerFX, caster.PositionHeld, map) is Thing_SolemnLamentSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.volleyDamageRange, Props.finaleDamageRange, Props.finaleRadius, Props.armorPenetration, weaponDef);
                }
            }
        }

        internal static void FireButterflyShot(
            Map map,
            Pawn caster,
            Pawn target,
            Vector3 anchorPos,
            bool isBlack,
            float dmg,
            float armorPen,
            ThingDef weaponDef)
        {
            if (map == null || caster == null || !caster.Spawned)
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

            FuneralDefOf.LC_SolemnLament_Shoot?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));
            SoundDef bloomSfx = isBlack ? FuneralDefOf.LC_SolemnLament_BloomBlack : FuneralDefOf.LC_SolemnLament_BloomWhite;
            IntVec3 anchorCell = anchorPos.ToIntVec3().ClampInsideMap(map);
            if (anchorCell.InBounds(map))
            {
                bloomSfx?.PlayOneShot(new TargetInfo(anchorCell, map));
            }

            FuneralVfxUtility.SpawnMuzzleFlutter(map, cPos + dir * 0.45f, dir, isBlack);
            FuneralVfxUtility.SpawnTargetButterflyBloom(map, anchorPos + new Vector3(Rand.Range(-0.2f, 0.2f), 0f, Rand.Range(-0.15f, 0.15f)), isBlack, scale: 1.15f, flutterCount: 3);

            if (target != null && !target.Dead && !target.Destroyed)
            {
                DamageDef damDef = isBlack ? LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage : LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage;
                float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;
                target.TakeDamage(new DamageInfo(damDef, dmg, armorPen, yaw, caster, weapon: weaponDef));
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

    /// <summary>驱动「死蝶哀歌」交替齐射（ticks 6/12/18/24/30）与第 38 tick 亡蝶棺群终结的纯逻辑实体。</summary>
    public class Thing_SolemnLamentSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private FloatRange volleyDmgRange;
        private FloatRange finaleDmgRange;
        private float finaleRadius;
        private float armorPen;
        private ThingDef weaponDef;
        private int ageTicks;
        private int salvoStep;
        private bool finaleExecuted;
        private bool initialized;

        private const int FinaleTick = 38;
        private const int TotalDurationTicks = 84;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 initialAnchor,
            FloatRange volleyDmg,
            FloatRange finaleDmg,
            float radius,
            float armorPenetration,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = initialAnchor.Yto0();
            volleyDmgRange = volleyDmg;
            finaleDmgRange = finaleDmg;
            finaleRadius = Mathf.Max(2.0f, radius);
            armorPen = armorPenetration;
            weaponDef = weapon;
            ageTicks = 0;
            salvoStep = 0;
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

            if (caster.equipment?.Primary?.def?.defName != SolemnLamentWeaponAnimHandler.WeaponDefName)
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

            if ((ageTicks == 6 && salvoStep == 0)
                || (ageTicks == 12 && salvoStep == 1)
                || (ageTicks == 18 && salvoStep == 2)
                || (ageTicks == 24 && salvoStep == 3)
                || (ageTicks == 30 && salvoStep == 4))
            {
                salvoStep++;
                bool isBlack = (salvoStep % 2) == 1;
                CompAbilityEffect_SolemnLamentRequiem.FireButterflyShot(Map, caster, primaryTarget, anchorPos, isBlack, volleyDmgRange.RandomInRange, armorPen, weaponDef);
            }

            if (!finaleExecuted && ageTicks >= FinaleTick)
            {
                finaleExecuted = true;
                ExecuteCoffinFinale();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy();
            }
        }

        private void ExecuteCoffinFinale()
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

            FuneralDefOf.LC_Funeral_Attack?.PlayOneShot(new TargetInfo(cell, Map));
            FuneralDefOf.LC_Funeral_Close?.PlayOneShot(new TargetInfo(caster.PositionHeld, Map));

            bool facingLeft = anchorPos.x < caster.DrawPos.x;
            FuneralVfxUtility.SpawnCoffinSwarmWave(Map, caster.DrawPos.Yto0(), facingLeft, count: 6);
            FuneralVfxUtility.SpawnTargetButterflyBloom(Map, anchorPos + new Vector3(-0.35f, 0f, 0.1f), isBlack: false, scale: 1.65f, flutterCount: 4);
            FuneralVfxUtility.SpawnTargetButterflyBloom(Map, anchorPos + new Vector3(0.35f, 0f, -0.1f), isBlack: true, scale: 1.65f, flutterCount: 4);

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

                if ((foe.DrawPos.Yto0() - anchorPos).sqrMagnitude <= finaleRadius * finaleRadius)
                {
                    float wDmg = finaleDmgRange.RandomInRange * 0.55f;
                    float bDmg = finaleDmgRange.RandomInRange * 0.55f;
                    foe.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, wDmg, armorPen, -1f, caster, weapon: weaponDef));
                    if (!foe.Dead && !foe.Destroyed)
                    {
                        foe.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, bDmg, armorPen, -1f, caster, weapon: weaponDef));
                    }
                    if (!foe.Dead && !foe.Destroyed)
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
