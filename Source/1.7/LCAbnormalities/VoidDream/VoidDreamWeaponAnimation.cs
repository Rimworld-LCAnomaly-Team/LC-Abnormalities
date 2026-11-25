using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.VoidDream
{
    /// <summary>
    /// T-02-99 虚空之梦 E.G.O. 武器「梦乡」（EgoWeapon_VoidDream）专属动画处理器：
    /// 贴图原图星月梦角铳枪口朝右（0°），支持星辉睡弹轻柔后坐（ClipRangeShot）、近战钝击（ClipMeleeTap）与
    /// 主动技能「吞梦催眠爆裂」的双重唤醒啼鸣-噩梦撕裂波演出。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class VoidDreamWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_VoidDream";

        public const string ClipRangeShot = "VoidDream_RangeShot";
        public const string ClipMeleeTap = "VoidDream_MeleeTap";
        public const string ClipSkillWarmup = "VoidDream_SkillWarmup";
        public const string ClipSkillLullaby = "VoidDream_SkillLullaby";

        private static readonly Vector2 HornDrawSize = new Vector2(1.55f, 1.55f);
        private static readonly Color LilacGhostTint = new Color(0.76f, 0.54f, 1.0f, 0.85f);

        // === Range Shot (24 ticks): gentle dreamy kickback -> calm floaty aim recovery ===
        private static readonly BodyKeyframe[] RangeShotBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -3f),
            new BodyKeyframe(5f, -0.08f, 0.01f, -7f),
            new BodyKeyframe(14f, -0.02f, 0f, -1f),
            new BodyKeyframe(24f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] RangeShotHorn =
        {
            new WeaponKeyframe(0f, 0.42f, -0.02f, -3f, 1.00f),
            new WeaponKeyframe(5f, 0.28f, 0.12f, -24f, 1.08f),
            new WeaponKeyframe(14f, 0.40f, 0.01f, -5f, 1.03f),
            new WeaponKeyframe(24f, 0.38f, -0.04f, 0f, 1.00f)
        };

        // === Melee Tap (40 ticks): sweeping crescent horn whack ===
        private static readonly BodyKeyframe[] MeleeTapBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(7f, 0.16f, -0.01f, 13f),
            new BodyKeyframe(18f, 0.08f, 0f, 6f),
            new BodyKeyframe(40f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] MeleeTapHorn =
        {
            new WeaponKeyframe(0f, 0.14f, 0.24f, -40f, 1.00f),
            new WeaponKeyframe(7f, 0.56f, -0.06f, 24f, 1.08f),
            new WeaponKeyframe(18f, 0.46f, -0.08f, 12f, 1.02f),
            new WeaponKeyframe(40f, 0.38f, -0.04f, 0f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): raise horn heavenward to chime dreamy echoes ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.45f, -0.06f, 0.02f, -6f),
            new BodyKeyframe(1.00f, -0.10f, 0.03f, -10f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupHorn =
        {
            new WeaponKeyframe(0.00f, 0.38f, -0.04f, 0f, 1.00f),
            new WeaponKeyframe(0.45f, 0.22f, 0.24f, -42f, 1.06f),
            new WeaponKeyframe(1.00f, 0.16f, 0.32f, -54f, 1.12f)
        };

        // === Skill Lullaby (88 ticks): forward chime burst (0..10) + nightmare roar recoil @ tick 26 ===
        private static readonly BodyKeyframe[] SkillLullabyBody =
        {
            new BodyKeyframe(0f, -0.10f, 0.03f, -10f),
            new BodyKeyframe(8f, 0.14f, 0.00f, 10f),
            new BodyKeyframe(20f, -0.06f, 0.02f, -6f),
            new BodyKeyframe(26f, -0.16f, 0.03f, -15f),
            new BodyKeyframe(48f, -0.04f, 0.01f, -4f),
            new BodyKeyframe(88f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillLullabyHorn =
        {
            new WeaponKeyframe(0f, 0.16f, 0.32f, -54f, 1.12f),
            new WeaponKeyframe(8f, 0.54f, 0.00f, 4f, 1.16f),
            new WeaponKeyframe(20f, 0.40f, 0.12f, -18f, 1.14f),
            new WeaponKeyframe(26f, 0.24f, 0.26f, -38f, 1.24f),
            new WeaponKeyframe(48f, 0.42f, 0.02f, -6f, 1.08f),
            new WeaponKeyframe(88f, 0.38f, -0.04f, 0f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillLullaby))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipMeleeTap, 40, aimAngle);
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
                case ClipMeleeTap:
                    kf = EgoWeaponAnimationUtility.SampleBody(MeleeTapBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillLullaby:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillLullabyBody, elapsedTicks);
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
                float tickPhase = (Find.TickManager?.TicksGame ?? 0) * 0.09f;
                float sway = Mathf.Sin(tickPhase) * (moving ? 5.0f : 2.2f);
                float posX = moving ? 0.34f : 0.38f;
                float posZ = (moving ? 0.00f : -0.04f) + Mathf.Sin(tickPhase * 0.8f) * 0.014f;
                float angle = (moving ? -6f : 0f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, HornDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipRangeShot:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(RangeShotHorn, t);
                    if (t >= 2f && t <= 12f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, RangeShotHorn, t, ctx.BaseDrawPos, alt, HornDrawSize, ctx.FacingLeft, LilacGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, HornDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipMeleeTap:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(MeleeTapHorn, t);
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ, kf.AngleDeg, HornDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupHorn, prog);
                    float chime = Mathf.Sin(t * 1.4f) * 0.014f * prog;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ + chime, kf.AngleDeg, HornDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillLullaby:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillLullabyHorn, t);
                    if (t >= 3f && t <= 34f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillLullabyHorn, t, ctx.BaseDrawPos, alt, HornDrawSize, ctx.FacingLeft, LilacGhostTint);
                    }
                    float shake = (t >= 25f && t <= 35f) ? Mathf.Sin(t * 2.2f) * 0.02f * (1f - (t - 25f) / 10f) : 0f;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX, kf.PosZ + shake, kf.AngleDeg, HornDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「吞梦催眠爆裂」蓄力动词：抬起星月梦号吹响催眠余音。</summary>
    public class Verb_CastAbility_VoidDreamLullaby : Verb_CastAbility
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
                    VoidDreamWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                if (CasterPawn.MapHeld != null)
                {
                    VoidDreamDefOf.LC_VoidDream_Ball?.PlayOneShot(new TargetInfo(CasterPawn.PositionHeld, CasterPawn.MapHeld));
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilityVoidDreamLullaby : CompProperties_AbilityEffect
    {
        public float burstRadius = 4.2f;
        public FloatRange sleepDamageRange = new FloatRange(14f, 19f);
        public FloatRange nightmareDamageRange = new FloatRange(18f, 24f);
        public float armorPenetration = 0.34f;

        public CompProperties_AbilityVoidDreamLullaby()
        {
            compClass = typeof(CompAbilityEffect_VoidDreamLullaby);
        }
    }

    /// <summary>
    /// 「梦乡」主动技能效果处理器：
    /// 第 1 拍绽放蓝紫催眠音浪冲击（白伤），第 26 tick 在目标中心撕裂噩梦精神崩解波（白伤 + 晕眩）。
    /// </summary>
    public class CompAbilityEffect_VoidDreamLullaby : CompAbilityEffect
    {
        public new CompProperties_AbilityVoidDreamLullaby Props => (CompProperties_AbilityVoidDreamLullaby)props;

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
            if (weapon != null && weapon.def?.defName == VoidDreamWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, VoidDreamWeaponAnimHandler.ClipSkillLullaby, 88, aimAngle);
            }

            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(VoidDreamWeaponAnimHandler.WeaponDefName);
            VoidDreamDefOf.LC_VoidDream_Shout?.PlayOneShot(new TargetInfo(caster.PositionHeld, map));

            VoidDreamVfxUtility.SpawnCrowWave(
                map,
                anchorPos,
                radius: Props.burstRadius,
                duration: 40,
                color: new Color(0.80f, 0.58f, 1.0f, 1.0f),
                accentColor: new Color(0.55f, 0.96f, 0.92f, 1.0f),
                coreColor: new Color(1.0f, 0.98f, 1.0f, 1.0f),
                intensity: 1.48f);

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

                if ((foe.DrawPos.Yto0() - anchorPos).sqrMagnitude <= Props.burstRadius * Props.burstRadius)
                {
                    foe.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, Props.sleepDamageRange.RandomInRange, Props.armorPenetration, -1f, caster, weapon: weaponDef));
                    if (!foe.Dead && !foe.Destroyed)
                    {
                        foe.stances?.stunner?.StunFor(35, caster, addBattleLog: false);
                    }
                }
            }

            if (VoidDreamDefOf.LC_VoidDreamSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(VoidDreamDefOf.LC_VoidDreamSkillRunnerFX, caster.PositionHeld, map) is Thing_VoidDreamSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.burstRadius, Props.nightmareDamageRange, Props.armorPenetration, weaponDef);
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

    /// <summary>驱动「吞梦催眠爆裂」第 26 tick 二次噩梦爆裂的纯逻辑实体。</summary>
    public class Thing_VoidDreamSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private float burstRadius;
        private FloatRange nightmareDmgRange;
        private float armorPen;
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
            FloatRange nightmareRange,
            float ap,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = anchor;
            burstRadius = Mathf.Max(2.0f, radius);
            nightmareDmgRange = nightmareRange;
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
            Scribe_Values.Look(ref burstRadius, "burstRadius", 4.2f);
            Scribe_Values.Look(ref nightmareDmgRange, "nightmareDmgRange", new FloatRange(18f, 24f));
            Scribe_Values.Look(ref armorPen, "armorPen", 0.34f);
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

            if (!burstDone && ageTicks >= 26)
            {
                burstDone = true;
                ExecuteNightmareBurst();
            }

            if (ageTicks >= TotalDurationTicks)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        private void ExecuteNightmareBurst()
        {
            Map map = Map;
            if (map == null)
            {
                return;
            }

            Vector3 center = (primaryTarget != null && primaryTarget.Spawned && primaryTarget.Map == map)
                ? primaryTarget.DrawPos.Yto0()
                : anchorPos;

            IntVec3 centerCell = center.ToIntVec3().ClampInsideMap(map);
            VoidDreamDefOf.LC_VoidDream_Skill?.PlayOneShot(new TargetInfo(centerCell, map));
            VoidDreamVfxUtility.SpawnNightmareBurst(map, center, 3.45f);

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

                if ((foe.DrawPos.Yto0() - center).sqrMagnitude <= burstRadius * burstRadius)
                {
                    foe.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, nightmareDmgRange.RandomInRange, armorPen, -1f, caster, weapon: weaponDef));
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
