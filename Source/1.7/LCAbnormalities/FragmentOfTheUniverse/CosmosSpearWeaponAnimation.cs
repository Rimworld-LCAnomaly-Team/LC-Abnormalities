using System.Collections.Generic;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.FragmentOfTheUniverse
{
    /// <summary>
    /// O-03-60 宇宙碎片 E.G.O. 武器「宇宙之矛」（EgoWeapon_FragmentOfTheUniverse）专属动画处理器：
    /// 贴图原图矛尖朝右上 +45°（angle=0° 右上、+45° 水平正前、-45° 竖直向上）。
    /// 支持敏捷星光突刺连击（ClipNormalThrust）与主动技能「星辉回响」的突刺+宇宙歌咏黑洞音浪冲击。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class CosmosSpearWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_FragmentOfTheUniverse";

        public const string ClipNormalThrust = "CosmosSpear_NormalThrust";
        public const string ClipSkillWarmup = "CosmosSpear_SkillWarmup";
        public const string ClipSkillEcho = "CosmosSpear_SkillEcho";

        private static readonly Vector2 SpearDrawSize = new Vector2(2.20f, 2.20f);
        private static readonly Color StarlightGhostTint = new Color(0.66f, 0.24f, 0.98f, 0.85f);

        // === Normal Thrust (52 ticks): crisp pull-back -> gleaming starlight lance thrust -> sweep recover ===
        private static readonly BodyKeyframe[] NormalThrustBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(8f, -0.10f, 0.01f, -9f),
            new BodyKeyframe(14f, 0.28f, -0.02f, 20f),
            new BodyKeyframe(26f, 0.16f, 0f, 10f),
            new BodyKeyframe(52f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalThrustSpear =
        {
            new WeaponKeyframe(0f, 0.32f, -0.08f, 34f, 1.00f),
            new WeaponKeyframe(8f, -0.04f, 0.24f, 22f, 1.04f),
            new WeaponKeyframe(14f, 0.86f, 0.02f, 45f, 1.16f),
            new WeaponKeyframe(26f, 0.56f, -0.10f, 56f, 1.06f),
            new WeaponKeyframe(52f, 0.32f, -0.08f, 34f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): raise starlight spear high as cosmos resonates ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.07f, 0.02f, -7f),
            new BodyKeyframe(1.00f, -0.14f, 0.03f, -14f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupSpear =
        {
            new WeaponKeyframe(0.00f, 0.32f, -0.08f, 34f, 1.00f),
            new WeaponKeyframe(0.45f, 0.08f, 0.36f, -18f, 1.06f),
            new WeaponKeyframe(1.00f, -0.06f, 0.44f, -40f, 1.12f)
        };

        // === Skill Echo (88 ticks): piercing lance lunge @ 6 -> sweeping resonance flourish @ 20 ===
        private static readonly BodyKeyframe[] SkillEchoBody =
        {
            new BodyKeyframe(0f, -0.14f, 0.03f, -14f),
            new BodyKeyframe(7f, 0.32f, -0.02f, 22f),
            new BodyKeyframe(14f, 0.14f, 0.02f, 8f),
            new BodyKeyframe(20f, 0.24f, -0.03f, 16f),
            new BodyKeyframe(44f, 0.10f, 0f, 6f),
            new BodyKeyframe(88f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillEchoSpear =
        {
            new WeaponKeyframe(0f, -0.06f, 0.44f, -40f, 1.12f),
            new WeaponKeyframe(7f, 0.92f, 0.04f, 45f, 1.24f),
            new WeaponKeyframe(14f, 0.18f, 0.28f, -12f, 1.16f),
            new WeaponKeyframe(20f, 0.72f, -0.14f, 66f, 1.22f),
            new WeaponKeyframe(44f, 0.50f, -0.10f, 44f, 1.10f),
            new WeaponKeyframe(88f, 0.32f, -0.08f, 34f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillEcho))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalThrust, 52, aimAngle);
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
                case ClipSkillEcho:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillEchoBody, elapsedTicks);
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
                float sway = Mathf.Sin(tickPhase) * (moving ? 4.2f : 2.0f);
                float posX = moving ? 0.30f : 0.32f;
                float posZ = (moving ? -0.02f : -0.08f) + Mathf.Sin(tickPhase * 0.7f) * 0.012f;
                float angle = (moving ? 24f : 34f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, SpearDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalThrust:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(NormalThrustSpear, t);
                    if (t >= 7f && t <= 22f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, NormalThrustSpear, t, ctx.BaseDrawPos, alt, SpearDrawSize, ctx.FacingLeft, StarlightGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, SpearDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupSpear, prog);
                    float shimmer = Mathf.Sin(t * 1.3f) * 0.012f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + shimmer, kf.PosZ, kf.AngleDeg, SpearDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillEcho:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillEchoSpear, t);
                    if (t >= 1f && t <= 28f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillEchoSpear, t, ctx.BaseDrawPos, alt, SpearDrawSize, ctx.FacingLeft, StarlightGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, SpearDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「星辉回响」蓄力动词：高举宇宙之矛吟唱星宇低鸣。</summary>
    public class Verb_CastAbility_CosmosSpearEcho : Verb_CastAbility
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
                    CosmosSpearWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                if (CasterPawn.MapHeld != null)
                {
                    FragmentOfTheUniverseDefOf.LC_Cosmos_Sing?.PlayOneShot(new TargetInfo(CasterPawn.PositionHeld, CasterPawn.MapHeld));
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilityCosmosSpearEcho : CompProperties_AbilityEffect
    {
        public float echoRadius = 3.2f;
        public FloatRange pierceDamageRange = new FloatRange(14f, 19f);
        public FloatRange echoDamageRange = new FloatRange(18f, 24f);
        public float armorPenetration = 0.35f;

        public CompProperties_AbilityCosmosSpearEcho()
        {
            compClass = typeof(CompAbilityEffect_CosmosSpearEcho);
        }
    }

    /// <summary>
    /// 「宇宙之矛」主动技能效果处理器：
    /// 第 1 拍长矛贯刺单体（黑伤），第 20 tick 激发半径 3.2 格宇宙咏叹震荡波（黑伤 + 晕眩）。
    /// </summary>
    public class CompAbilityEffect_CosmosSpearEcho : CompAbilityEffect
    {
        public new CompProperties_AbilityCosmosSpearEcho Props => (CompProperties_AbilityCosmosSpearEcho)props;

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
            if (weapon != null && weapon.def?.defName == CosmosSpearWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, CosmosSpearWeaponAnimHandler.ClipSkillEcho, 88, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(CosmosSpearWeaponAnimHandler.WeaponDefName);
            FragmentOfTheUniverseDefOf.LC_Cosmos_SpearHit?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));
            FragmentOfTheUniverseVfxUtility.SpawnSongWave(map, anchorPos, 2.25f, 24);

            if (victim != null && !victim.Dead && !victim.Destroyed)
            {
                victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, Props.pierceDamageRange.RandomInRange, Props.armorPenetration, -1f, caster, weapon: weaponDef));
            }

            if (FragmentOfTheUniverseDefOf.LC_CosmosSpearSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(FragmentOfTheUniverseDefOf.LC_CosmosSpearSkillRunnerFX, caster.PositionHeld, map) is Thing_CosmosSpearSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.echoRadius, Props.echoDamageRange, Props.armorPenetration, weaponDef);
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

    /// <summary>驱动「星辉回响」第 20 tick 宇宙歌咏音浪爆裂的纯逻辑实体。</summary>
    public class Thing_CosmosSpearSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private float echoRadius;
        private FloatRange echoDmgRange;
        private float armorPen;
        private ThingDef weaponDef;
        private int ageTicks;
        private bool burstDone;
        private bool initialized;

        private const int TotalDurationTicks = 80;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 anchor,
            float radius,
            FloatRange echoRange,
            float ap,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = anchor;
            echoRadius = Mathf.Max(2.0f, radius);
            echoDmgRange = echoRange;
            armorPen = ap;
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
            Scribe_Values.Look(ref echoRadius, "echoRadius", 3.2f);
            Scribe_Values.Look(ref echoDmgRange, "echoDmgRange", new FloatRange(18f, 24f));
            Scribe_Values.Look(ref armorPen, "armorPen", 0.35f);
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

            if (!burstDone && ageTicks >= 20)
            {
                burstDone = true;
                ExecuteSongWave();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        private void ExecuteSongWave()
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
            FragmentOfTheUniverseDefOf.LC_Cosmos_Hit?.PlayOneShot(new TargetInfo(cell, map));
            FragmentOfTheUniverseVfxUtility.SpawnSongWave(map, center, 5.4f, 40);
            FleckMaker.ThrowMicroSparks(center, map);

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

                if ((foe.DrawPos.Yto0() - center).sqrMagnitude <= echoRadius * echoRadius)
                {
                    foe.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, echoDmgRange.RandomInRange, armorPen, -1f, caster, weapon: weaponDef));
                    if (!foe.Dead && !foe.Destroyed)
                    {
                        foe.stances?.stunner?.StunFor(50, caster, addBattleLog: false);
                    }
                }
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
        }
    }
}
