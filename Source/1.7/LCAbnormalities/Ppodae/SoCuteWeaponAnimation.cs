using System.Collections.Generic;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.Ppodae
{
    /// <summary>
    /// D-02-107 宇宙小可爱 / 波大 E.G.O. 武器「好可爱」（EgoWeapon_Ppodae）专属动画处理器：
    /// 贴图原图兽爪朝正下方（-Z），因此 localAngleDeg=-90° 为正前方（+X），-138° 为高抬上举，-46° 为猛扑下劈。
    /// 支持凶猛兽爪撕抓连击（ClipNormalClaw）与主动技能「狂犬撕咬扑杀」的三连猛抓扑地红震爆裂。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class SoCuteWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_Ppodae";

        public const string ClipNormalClaw = "SoCute_NormalClaw";
        public const string ClipSkillWarmup = "SoCute_SkillWarmup";
        public const string ClipSkillMaul = "SoCute_SkillMaul";

        private static readonly Vector2 PawDrawSize = new Vector2(1.55f, 1.55f);
        private static readonly Color CrimsonGhostTint = new Color(0.95f, 0.16f, 0.22f, 0.85f);

        // === Normal Claw (46 ticks): quick raise -> ferocious downward beast swipe -> recover ===
        private static readonly BodyKeyframe[] NormalClawBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -5f),
            new BodyKeyframe(7f, -0.10f, 0.02f, -10f),
            new BodyKeyframe(14f, 0.24f, -0.02f, 18f),
            new BodyKeyframe(26f, 0.12f, 0f, 8f),
            new BodyKeyframe(46f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalClawPaws =
        {
            new WeaponKeyframe(0f, 0.28f, -0.08f, -80f, 1.00f),
            new WeaponKeyframe(7f, 0.04f, 0.34f, -138f, 1.06f),
            new WeaponKeyframe(14f, 0.62f, -0.10f, -48f, 1.16f),
            new WeaponKeyframe(26f, 0.50f, -0.16f, -38f, 1.04f),
            new WeaponKeyframe(46f, 0.28f, -0.08f, -80f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): crouch low and draw claws back with beastly growl ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.10f, 0.01f, -11f),
            new BodyKeyframe(1.00f, -0.18f, 0.02f, -18f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupPaws =
        {
            new WeaponKeyframe(0.00f, 0.28f, -0.08f, -80f, 1.00f),
            new WeaponKeyframe(0.45f, 0.02f, 0.30f, -132f, 1.06f),
            new WeaponKeyframe(1.00f, -0.12f, 0.40f, -148f, 1.12f)
        };

        // === Skill Maul (84 ticks): Strike 1 @ 5 -> Strike 2 @ 15 -> Leap Beast Slam @ 28 ===
        private static readonly BodyKeyframe[] SkillMaulBody =
        {
            new BodyKeyframe(0f, -0.18f, 0.02f, -18f),
            new BodyKeyframe(5f, 0.24f, -0.01f, 17f),
            new BodyKeyframe(11f, -0.10f, 0.02f, -10f),
            new BodyKeyframe(16f, 0.26f, -0.02f, 20f),
            new BodyKeyframe(22f, -0.16f, 0.04f, -16f),
            new BodyKeyframe(28f, 0.34f, -0.05f, 26f),
            new BodyKeyframe(48f, 0.20f, -0.02f, 14f),
            new BodyKeyframe(84f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillMaulPaws =
        {
            new WeaponKeyframe(0f, -0.12f, 0.40f, -148f, 1.12f),
            new WeaponKeyframe(5f, 0.64f, -0.08f, -50f, 1.16f),
            new WeaponKeyframe(11f, 0.06f, 0.36f, -138f, 1.12f),
            new WeaponKeyframe(16f, 0.66f, -0.06f, -46f, 1.18f),
            new WeaponKeyframe(22f, -0.14f, 0.44f, -154f, 1.20f),
            new WeaponKeyframe(28f, 0.72f, -0.16f, -40f, 1.26f),
            new WeaponKeyframe(48f, 0.54f, -0.14f, -46f, 1.12f),
            new WeaponKeyframe(84f, 0.28f, -0.08f, -80f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillMaul))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalClaw, 46, aimAngle);
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
                case ClipNormalClaw:
                    kf = EgoWeaponAnimationUtility.SampleBody(NormalClawBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillMaul:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillMaulBody, elapsedTicks);
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
                float sway = Mathf.Sin(tickPhase) * (moving ? 6.5f : 2.8f);
                float posX = moving ? 0.30f : 0.28f;
                float posZ = (moving ? -0.04f : -0.08f) + Mathf.Sin(tickPhase * 0.8f) * 0.012f;
                float angle = (moving ? -94f : -80f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, PawDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalClaw:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(NormalClawPaws, t);
                    if (t >= 6f && t <= 20f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, NormalClawPaws, t, ctx.BaseDrawPos, alt, PawDrawSize, ctx.FacingLeft, CrimsonGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, PawDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupPaws, prog);
                    float snarl = Mathf.Sin(t * 1.9f) * 0.014f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + snarl, kf.PosZ, kf.AngleDeg, PawDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillMaul:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillMaulPaws, t);
                    if (t >= 1f && t <= 36f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillMaulPaws, t, ctx.BaseDrawPos, alt, PawDrawSize, ctx.FacingLeft, CrimsonGhostTint);
                    }
                    float quake = (t >= 27f && t <= 37f) ? Mathf.Sin(t * 2.5f) * 0.022f * (1f - (t - 27f) / 10f) : 0f;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ + quake, kf.AngleDeg, PawDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「狂犬扑杀」蓄力动词：低身龇牙蓄力并吠叫震慑。</summary>
    public class Verb_CastAbility_SoCuteMaul : Verb_CastAbility
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
                    SoCuteWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                if (CasterPawn.MapHeld != null)
                {
                    PpodaeDefOf.LC_Ppodae_Bark?.PlayOneShot(new TargetInfo(CasterPawn.PositionHeld, CasterPawn.MapHeld));
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilitySoCuteMaul : CompProperties_AbilityEffect
    {
        public float maulRadius = 2.8f;
        public FloatRange slashDamageRange = new FloatRange(7f, 10f);
        public FloatRange slamDamageRange = new FloatRange(18f, 24f);
        public float armorPenetration = 0.34f;

        public CompProperties_AbilitySoCuteMaul()
        {
            compClass = typeof(CompAbilityEffect_SoCuteMaul);
        }
    }

    /// <summary>
    /// 「好可爱」主动技能效果处理器：
    /// 第 1 拍、第 14 tick 连续凶猛红伤撕抓，第 28 tick 魔犬重爪扑杀震撼地面，造成大范围红伤与眩晕。
    /// </summary>
    public class CompAbilityEffect_SoCuteMaul : CompAbilityEffect
    {
        public new CompProperties_AbilitySoCuteMaul Props => (CompProperties_AbilitySoCuteMaul)props;

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
            if (weapon != null && weapon.def?.defName == SoCuteWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, SoCuteWeaponAnimHandler.ClipSkillMaul, 84, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(SoCuteWeaponAnimHandler.WeaponDefName);
            PpodaeDefOf.LC_Ppodae_Atk?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));
            PpodaeVfxUtility.SpawnSlamBurst(map, anchorPos, 1.45f, 16);

            if (victim != null && !victim.Dead && !victim.Destroyed)
            {
                victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, Props.slashDamageRange.RandomInRange, Props.armorPenetration, -1f, caster, weapon: weaponDef));
            }

            if (PpodaeDefOf.LC_SoCuteSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(PpodaeDefOf.LC_SoCuteSkillRunnerFX, caster.PositionHeld, map) is Thing_SoCuteSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.maulRadius, Props.slashDamageRange, Props.slamDamageRange, Props.armorPenetration, weaponDef);
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

    /// <summary>驱动「狂犬扑杀」二段撕抓与第 28 tick 狂暴震地重击的纯逻辑实体。</summary>
    public class Thing_SoCuteSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private float maulRadius;
        private FloatRange slashDmgRange;
        private FloatRange slamDmgRange;
        private float armorPen;
        private ThingDef weaponDef;
        private int ageTicks;
        private int stage;
        private bool initialized;

        private const int TotalDurationTicks = 76;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 anchor,
            float radius,
            FloatRange slashRange,
            FloatRange slamRange,
            float ap,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = anchor;
            maulRadius = Mathf.Max(1.8f, radius);
            slashDmgRange = slashRange;
            slamDmgRange = slamRange;
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
            Scribe_Values.Look(ref maulRadius, "maulRadius", 2.8f);
            Scribe_Values.Look(ref slashDmgRange, "slashDmgRange", new FloatRange(7f, 10f));
            Scribe_Values.Look(ref slamDmgRange, "slamDmgRange", new FloatRange(18f, 24f));
            Scribe_Values.Look(ref armorPen, "armorPen", 0.34f);
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

            if (stage == 0 && ageTicks >= 14)
            {
                stage = 1;
                Vector3 center = (primaryTarget != null && primaryTarget.Spawned && primaryTarget.Map == Map)
                    ? primaryTarget.DrawPos.Yto0()
                    : anchorPos;
                PpodaeDefOf.LC_Ppodae_Atk?.PlayOneShot(new TargetInfo(center.ToIntVec3().ClampInsideMap(Map), Map));
                FleckMaker.ThrowMicroSparks(center, Map);
                if (primaryTarget != null && !primaryTarget.Dead && !primaryTarget.Destroyed)
                {
                    primaryTarget.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, slashDmgRange.RandomInRange, armorPen, -1f, caster, weapon: weaponDef));
                }
            }
            else if (stage == 1 && ageTicks >= 28)
            {
                stage = 2;
                ExecuteGrandSlam();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        private void ExecuteGrandSlam()
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
            PpodaeDefOf.LC_Ppodae_Transform?.PlayOneShot(new TargetInfo(cell, map));
            PpodaeDefOf.LC_Ppodae_Fist?.PlayOneShot(new TargetInfo(cell, map));
            PpodaeVfxUtility.SpawnSlamBurst(map, center, 3.05f, 30);
            FleckMaker.ThrowDustPuffThick(center, map, 1.8f, new Color(0.95f, 0.22f, 0.26f));

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

                if ((foe.DrawPos.Yto0() - center).sqrMagnitude <= maulRadius * maulRadius)
                {
                    foe.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, slamDmgRange.RandomInRange, armorPen, -1f, caster, weapon: weaponDef));
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
