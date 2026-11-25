using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Render.Weapon;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.Schadenfreude
{
    /// <summary>
    /// O-05-76 幸灾乐祸 E.G.O. 武器「凝视」（EgoWeapon_Schadenfreude）专属动画处理器：
    /// 贴图原图刃盘朝右上 +45°（angle=0° 右上、+45° 水平向前、-45° 垂直向上）。
    /// 支持高速高频锯齿震颤（电锯嗡鸣手感）、回拉前压撕锯（ClipNormalGrind），
    /// 以及主动技能「窥孔锯暴」的四拍连环高速切割与末段红眸破裂终结。
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class GazeWeaponAnimHandler : IEgoWeaponAnimationHandler
    {
        public const string WeaponDefName = "EgoWeapon_Schadenfreude";

        public const string ClipNormalGrind = "Gaze_NormalGrind";
        public const string ClipSkillWarmup = "Gaze_SkillWarmup";
        public const string ClipSkillStorm = "Gaze_SkillStorm";

        private static readonly Vector2 SawDrawSize = new Vector2(1.95f, 1.95f);
        private static readonly Color CrimsonSawGhostTint = new Color(0.95f, 0.14f, 0.22f, 0.85f);

        // === Normal Grind (58 ticks): pullback rev -> high-RPM forward buzzsaw carve -> retract ===
        private static readonly BodyKeyframe[] NormalGrindBody =
        {
            new BodyKeyframe(0f, -0.04f, 0f, -4f),
            new BodyKeyframe(9f, -0.10f, 0.01f, -9f),
            new BodyKeyframe(16f, 0.22f, -0.02f, 16f),
            new BodyKeyframe(32f, 0.18f, -0.01f, 12f),
            new BodyKeyframe(58f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] NormalGrindSaw =
        {
            new WeaponKeyframe(0f, 0.28f, -0.14f, 26f, 1.00f),
            new WeaponKeyframe(9f, -0.04f, 0.34f, -56f, 1.05f),
            new WeaponKeyframe(16f, 0.62f, -0.06f, 46f, 1.14f),
            new WeaponKeyframe(32f, 0.54f, -0.10f, 52f, 1.08f),
            new WeaponKeyframe(58f, 0.28f, -0.14f, 26f, 1.00f)
        };

        // === Skill Warmup (normalized 0..1): coil red saw across shoulder while keyhole pulses ===
        private static readonly BodyKeyframe[] SkillWarmupBody =
        {
            new BodyKeyframe(0.00f, 0.00f, 0.00f, 0f),
            new BodyKeyframe(0.35f, -0.08f, 0.02f, -9f),
            new BodyKeyframe(1.00f, -0.14f, 0.02f, -15f)
        };

        private static readonly WeaponKeyframe[] SkillWarmupSaw =
        {
            new WeaponKeyframe(0.00f, 0.28f, -0.14f, 26f, 1.00f),
            new WeaponKeyframe(0.45f, -0.04f, 0.36f, -54f, 1.08f),
            new WeaponKeyframe(1.00f, -0.14f, 0.42f, -72f, 1.14f)
        };

        // === Skill Storm (96 ticks): 4-beat continuous keyhole frenzy slash ===
        private static readonly BodyKeyframe[] SkillStormBody =
        {
            new BodyKeyframe(0f, -0.14f, 0.02f, -15f),
            new BodyKeyframe(8f, 0.28f, -0.02f, 20f),
            new BodyKeyframe(20f, 0.18f, 0.02f, 10f),
            new BodyKeyframe(32f, 0.28f, -0.03f, 22f),
            new BodyKeyframe(42f, -0.08f, 0.03f, -12f),
            new BodyKeyframe(50f, 0.32f, -0.04f, 26f),
            new BodyKeyframe(70f, 0.22f, -0.02f, 14f),
            new BodyKeyframe(96f, 0f, 0f, 0f)
        };

        private static readonly WeaponKeyframe[] SkillStormSaw =
        {
            new WeaponKeyframe(0f, -0.14f, 0.42f, -72f, 1.14f),
            new WeaponKeyframe(8f, 0.66f, -0.06f, 44f, 1.20f),
            new WeaponKeyframe(18f, 0.14f, 0.28f, -38f, 1.16f),
            new WeaponKeyframe(28f, 0.68f, -0.14f, 62f, 1.22f),
            new WeaponKeyframe(40f, -0.10f, 0.46f, -84f, 1.24f),
            new WeaponKeyframe(49f, 0.72f, -0.12f, 52f, 1.28f),
            new WeaponKeyframe(68f, 0.56f, -0.18f, 58f, 1.12f),
            new WeaponKeyframe(96f, 0.28f, -0.14f, 26f, 1.00f)
        };

        public void OnNormalMeleeAttack(Pawn attacker, ThingWithComps weapon, Thing target, Verb_MeleeAttack verb)
        {
            if (attacker == null || weapon == null)
            {
                return;
            }

            if (EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillWarmup)
                || EgoWeaponAnimationManager.IsPlaying(attacker, ClipSkillStorm))
            {
                return;
            }

            Vector3 targetPos = target != null
                ? (target.Spawned ? target.DrawPos.Yto0() : target.PositionHeld.ToVector3Shifted().Yto0())
                : (attacker.DrawPos.Yto0() + Vector3.right);
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, targetPos);
            EgoWeaponAnimationManager.Play(attacker, weapon, ClipNormalGrind, 58, aimAngle);
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
                case ClipNormalGrind:
                    kf = EgoWeaponAnimationUtility.SampleBody(NormalGrindBody, elapsedTicks);
                    break;
                case ClipSkillWarmup:
                {
                    float prog = Mathf.Clamp01(elapsedTicks / Mathf.Max(1, playback.DurationTicks));
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillWarmupBody, prog);
                    break;
                }
                case ClipSkillStorm:
                    kf = EgoWeaponAnimationUtility.SampleBody(SkillStormBody, elapsedTicks);
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
            float motorJitter = Mathf.Sin((Find.TickManager?.TicksGame ?? 0) * 2.3f) * 0.008f;

            if (ctx.ActiveAnim == null)
            {
                bool moving = ctx.Pawn.pather != null && ctx.Pawn.pather.Moving;
                float sway = Mathf.Sin((Find.TickManager?.TicksGame ?? 0) * 0.12f) * (moving ? 5f : 2.5f);
                float posX = moving ? 0.24f : 0.28f;
                float posZ = (moving ? -0.06f : -0.14f) + motorJitter;
                float angle = (moving ? 36f : 26f) + sway;

                EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, posX, posZ, angle, SawDrawSize, ctx.FacingLeft);
                return true;
            }

            float t = ctx.ElapsedTicks;
            float prog = ctx.NormalizedProgress;
            float grindVib = Mathf.Sin(t * 3.1f) * 0.018f;

            switch (ctx.ActiveAnim.ClipId)
            {
                case ClipNormalGrind:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(NormalGrindSaw, t);
                    if (t >= 8f && t <= 24f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, NormalGrindSaw, t, ctx.BaseDrawPos, alt, SawDrawSize, ctx.FacingLeft, CrimsonSawGhostTint);
                    }
                    float activeVib = (t >= 12f && t <= 34f) ? grindVib : motorJitter;
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + activeVib, kf.PosZ + activeVib * 0.6f, kf.AngleDeg, SawDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillWarmup:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillWarmupSaw, prog);
                    float revVib = Mathf.Sin(t * 2.8f) * (0.010f + 0.018f * prog);
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + revVib, kf.PosZ, kf.AngleDeg, SawDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                case ClipSkillStorm:
                {
                    WeaponKeyframe kf = EgoWeaponAnimationUtility.SampleWeapon(SkillStormSaw, t);
                    if (t >= 4f && t <= 56f)
                    {
                        EgoWeaponAnimationUtility.DrawWeaponAfterimages(mat, SkillStormSaw, t, ctx.BaseDrawPos, alt, SawDrawSize, ctx.FacingLeft, CrimsonSawGhostTint);
                    }
                    EgoWeaponAnimationUtility.DrawWeaponSprite(mat, ctx.BaseDrawPos, alt, kf.PosX + grindVib, kf.PosZ - grindVib * 0.6f, kf.AngleDeg, SawDrawSize, ctx.FacingLeft, kf.Scale);
                    return true;
                }

                default:
                    return false;
            }
        }
    }

    /// <summary>「窥孔锯暴」蓄力动词：背锯蓄转并伴随锁孔监视脉动光环与音效。</summary>
    public class Verb_CastAbility_GazeSawstorm : Verb_CastAbility
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
                    GazeWeaponAnimHandler.ClipSkillWarmup,
                    warmupTicks,
                    aim,
                    requiredWarmupVerb: this);

                Map map = casterPawn.MapHeld;
                if (map != null)
                {
                    SchadenfreudeVfxUtility.SpawnKeyholePulse(map, casterPawn.DrawPos.Yto0(), scale: 2.3f, lifeTicks: Mathf.Min(36, warmupTicks), withEyeMedallion: false);
                    LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_SkillStart, casterPawn);
                }
            }

            return started;
        }
    }

    public class CompProperties_AbilityGazeSawstorm : CompProperties_AbilityEffect
    {
        public float slashRadius = 2.6f;
        public FloatRange openerDamageRange = new FloatRange(12f, 16f);
        public FloatRange tickDamageRange = new FloatRange(6f, 9f);
        public FloatRange finisherDamageRange = new FloatRange(18f, 25f);
        public float armorPenetration = 0.42f;

        public CompProperties_AbilityGazeSawstorm()
        {
            compClass = typeof(CompAbilityEffect_GazeSawstorm);
        }
    }

    /// <summary>
    /// 凝视主动技能「窥孔锯暴」效果处理器：
    /// 第 1 拍释放赤红锁孔凝视爆震并斩击切入，随后于第 12 / 24 / 36 tick 连续三次高速锯刃撕裂，
    /// 最终于第 48 tick 斩出红眸破裂终结连段，对已撕裂流血或被窥视目标造成额外暴虐红伤。
    /// </summary>
    public class CompAbilityEffect_GazeSawstorm : CompAbilityEffect
    {
        public new CompProperties_AbilityGazeSawstorm Props => (CompProperties_AbilityGazeSawstorm)props;

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

            Vector3 casterPos = caster.DrawPos.Yto0();
            Vector3 anchorPos = victim != null && victim.Spawned ? victim.DrawPos.Yto0() : target.CenterVector3.Yto0();
            float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(caster, anchorPos);
            Vector3 dir = (anchorPos - casterPos).Yto0();
            if (dir.sqrMagnitude < 0.001f)
            {
                dir = Vector3.right;
            }
            dir.Normalize();
            float yaw = Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;

            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon != null && weapon.def?.defName == GazeWeaponAnimHandler.WeaponDefName)
            {
                EgoWeaponAnimationManager.Play(caster, weapon, GazeWeaponAnimHandler.ClipSkillStorm, 96, aimAngle);
            }

            LCAbnormalitiesHelper.PlaySoundAt(SchadenfreudeDefOf.LC_Schadenfreude_Skill, caster);
            SchadenfreudeVfxUtility.SpawnKeyholePulse(map, anchorPos, scale: 3.1f, lifeTicks: 38, withEyeMedallion: true);
            SchadenfreudeVfxUtility.SpawnGazeSlash(map, Vector3.Lerp(casterPos, anchorPos, 0.65f), yaw, scale: 2.35f, flipSweep: false, lifeTicks: 18, spanRad: 2.5f);

            DamageDef skillDmg = SchadenfreudeDefOf.LC_Gaze_SkillRed ?? LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
            ThingDef weaponDef = weapon?.def ?? DefDatabase<ThingDef>.GetNamedSilentFail(GazeWeaponAnimHandler.WeaponDefName);

            ApplySawSlice(map, caster, victim, anchorPos, Props.slashRadius, Props.openerDamageRange, Props.armorPenetration, skillDmg, weaponDef, yaw);

            if (SchadenfreudeDefOf.LC_GazeSkillRunnerFX != null && caster.PositionHeld.InBounds(map))
            {
                if (GenSpawn.Spawn(SchadenfreudeDefOf.LC_GazeSkillRunnerFX, caster.PositionHeld, map) is Thing_GazeSkillRunner runner)
                {
                    runner.Initialize(caster, victim, anchorPos, Props.slashRadius, Props.tickDamageRange, Props.finisherDamageRange, Props.armorPenetration, weaponDef);
                }
            }
        }

        internal static void ApplySawSlice(
            Map map,
            Pawn caster,
            Pawn primaryTarget,
            Vector3 center,
            float radius,
            FloatRange dmgRange,
            float armorPen,
            DamageDef dmgDef,
            ThingDef weaponDef,
            float yawDeg,
            float bonusMultiplier = 1.0f,
            int stunTicks = 0)
        {
            if (map == null || caster == null)
            {
                return;
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

                if ((foe.DrawPos.Yto0() - center).sqrMagnitude <= radius * radius)
                {
                    float dmg = dmgRange.RandomInRange * bonusMultiplier;
                    foe.TakeDamage(new DamageInfo(dmgDef, dmg, armorPen, yawDeg, caster, weapon: weaponDef));
                    if (!foe.Dead)
                    {
                        if (SchadenfreudeDefOf.LC_GazeRending != null)
                        {
                            LCAbnormalitiesHelper.ApplyOrRefreshHediff(foe, SchadenfreudeDefOf.LC_GazeRending, 0.45f);
                        }
                        if (stunTicks > 0)
                        {
                            foe.stances?.stunner?.StunFor(stunTicks, caster, addBattleLog: false);
                        }
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

    /// <summary>驱动「窥孔锯暴」多拍锯齿连击与第 48 tick 终结斩的纯逻辑实体。</summary>
    public class Thing_GazeSkillRunner : Thing
    {
        private Pawn caster;
        private Pawn primaryTarget;
        private Vector3 anchorPos;
        private float slashRadius;
        private FloatRange tickDmgRange;
        private FloatRange finisherDmgRange;
        private float armorPen;
        private ThingDef weaponDef;
        private int ageTicks;
        private int flurryStep;
        private bool finisherExecuted;
        private bool initialized;

        private const int FinisherTick = 48;
        private const int TotalDurationTicks = 92;

        public void Initialize(
            Pawn casterPawn,
            Pawn targetPawn,
            Vector3 initialAnchor,
            float radius,
            FloatRange tickDamage,
            FloatRange finisherDamage,
            float armorPenetration,
            ThingDef weapon)
        {
            caster = casterPawn;
            primaryTarget = targetPawn;
            anchorPos = initialAnchor.Yto0();
            slashRadius = Mathf.Max(1.8f, radius);
            tickDmgRange = tickDamage;
            finisherDmgRange = finisherDamage;
            armorPen = armorPenetration;
            weaponDef = weapon;
            ageTicks = 0;
            flurryStep = 0;
            finisherExecuted = false;
            initialized = true;
        }

        protected override void Tick()
        {
            if (!initialized || caster == null || caster.Dead || caster.Destroyed || !caster.Spawned || Map == null)
            {
                Destroy();
                return;
            }

            if (caster.equipment?.Primary?.def?.defName != GazeWeaponAnimHandler.WeaponDefName)
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
            DamageDef skillDmg = SchadenfreudeDefOf.LC_Gaze_SkillRed ?? LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;

            if ((ageTicks == 12 && flurryStep == 0) || (ageTicks == 24 && flurryStep == 1) || (ageTicks == 36 && flurryStep == 2))
            {
                flurryStep++;
                bool flip = (flurryStep % 2) == 1;
                SchadenfreudeVfxUtility.SpawnGazeSlash(Map, Vector3.Lerp(caster.DrawPos.Yto0(), anchorPos, 0.72f), yaw + (flip ? 24f : -24f), scale: 2.25f, flipSweep: flip, lifeTicks: 14, spanRad: 2.35f);
                SchadenfreudeVfxUtility.SpawnBloodBurst(Map, anchorPos, yaw, scale: 1.65f, lifeTicks: 16);
                if (SchadenfreudeDefOf.LC_Gaze_SawLoop != null)
                {
                    SchadenfreudeDefOf.LC_Gaze_SawLoop.PlayOneShot(new TargetInfo(anchorPos.ToIntVec3().ClampInsideMap(Map), Map));
                }
                CompAbilityEffect_GazeSawstorm.ApplySawSlice(Map, caster, primaryTarget, anchorPos, slashRadius, tickDmgRange, armorPen, skillDmg, weaponDef, yaw);
            }

            if (!finisherExecuted && ageTicks >= FinisherTick)
            {
                finisherExecuted = true;
                IntVec3 cell = anchorPos.ToIntVec3().ClampInsideMap(Map);
                if (cell.InBounds(Map))
                {
                    SchadenfreudeDefOf.LC_Schadenfreude_SkillEnd?.PlayOneShot(new TargetInfo(cell, Map));
                }
                SchadenfreudeVfxUtility.SpawnGazeFlurry(Map, anchorPos, yaw, watchedBoost: true);
                SchadenfreudeVfxUtility.SpawnKeyholePulse(Map, anchorPos, scale: 3.6f, lifeTicks: 30, withEyeMedallion: true);
                bool watched = DamageWorker_Gaze.IsInstigatorWatched(caster);
                CompAbilityEffect_GazeSawstorm.ApplySawSlice(Map, caster, primaryTarget, anchorPos, slashRadius + 0.4f, finisherDmgRange, armorPen, skillDmg, weaponDef, yaw, bonusMultiplier: watched ? 1.25f : 1.0f, stunTicks: 50);
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
