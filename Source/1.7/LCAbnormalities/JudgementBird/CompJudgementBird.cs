using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.DamageWorker;
using LCAnomalyCore.Misc;
using LCAnomalyCore.Render.Weapon;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAbnormalities.JudgementBird
{
    [DefOf]
    public static class JudgementBirdDefOf
    {
        public static SoundDef LC_JudgementBird_On;
        public static SoundDef LC_JudgementBird_Down;
        public static SoundDef LC_JudgementBird_Stun;
        public static SoundDef LC_JudgementBird_Wave;
        public static SoundDef LC_JudgementBird_Hang;
        public static SoundDef LC_JudgementBird_Hanging;
        public static SoundDef LC_Justitia_Slash1;
        public static SoundDef LC_Justitia_Slash2;
        public static SoundDef LC_Justitia_Slash3;
        public static SoundDef LC_Justitia_Slash4;

        public static DamageDef LC_Justitia_VerdictPale;

        public static AnimationDef JudgementBird_Standing;
        public static AnimationDef JudgementBird_Seeing;
        public static AnimationDef JudgementBird_Walk;
        public static AnimationDef JudgementBird_Judgement;
        public static AnimationDef JudgementBird_VictimHangStart;
        public static AnimationDef JudgementBird_VictimHangLoop;

        public static ThingDef LC_JudgementCastAuraFX;
        public static ThingDef LC_JudgementPaleWaveFX;
        public static ThingDef LC_JudgementGallowsFX;
        public static ThingDef LC_JudgementBandageFX;
        public static ThingDef LC_JustitiaSlashFX;
        public static ThingDef LC_JustitiaComboFX;
        public static ThingDef LC_JustitiaChargeSigilFX;
        public static ThingDef LC_JustitiaVerdictRunnerFX;

        static JudgementBirdDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(JudgementBirdDefOf));
        }
    }

    public class CompProperties_JudgementBird : CompProperties_LCAbnormality
    {
        public int judgementWindupTicks = 275;
        public int judgementTotalAnimTicks = 330;
        public int judgementCooldownTicks = 300;
        public float judgementRadius = 8.5f;
        public float judgementPaleMin = 30f;
        public float judgementPaleMax = 38f;

        public CompProperties_JudgementBird()
        {
            compClass = typeof(CompJudgementBird);
        }
    }

    /// <summary>
    /// O-02-62 审判鸟核心逻辑：首个蓝伤来源，完整驱动脑叶公司 Standing / Seeing / Walk / Judgement 四套 Spine 动画，
    /// 突破后周期性显化巨型不公天平与苍白审判领域，对 8.5 格内所有人类降下无视护甲的灵魂蓝伤，并以黑羽绞刑架处决陨落者。
    /// </summary>
    public class CompJudgementBird : CompLCAbnormality
    {
        // Exact BigChoo visibility window from Spine 긴새.json.txt (1.33s..4.73s -> ticks 80..284)
        private const int BigScaleStartTick = 80;
        private const int BigScaleEndTick = 284;
        private const int ScaleTiltChimeTick = 150;

        private bool isJudging;
        private int judgingElapsedTicks;
        private bool verdictExecuted;
        private int judgementCooldownTicks = 150;
        private int studyingGraceTicks;

        // Non-saved runtime reference to active cast domain & floating Unbalanced Scale sigil
        private Thing_JudgementCastAura activeCastAura;

        public CompProperties_JudgementBird PropsJudge => (CompProperties_JudgementBird)props;

        public bool IsJudging => isJudging;

        public bool IsBeingStudiedNow => studyingGraceTicks > 0 && !Escaped;

        public bool IsBigScaleVisible => isJudging && judgingElapsedTicks >= BigScaleStartTick && judgingElapsedTicks <= BigScaleEndTick;

        protected override void StudyEvent_Normal(Pawn studier)
        {
            base.StudyEvent_Normal(studier);
            if (Rand.Chance(0.50f))
            {
                QliphothCountCurrent--;
            }
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            base.StudyEvent_Bad(studier);
            QliphothCountCurrent--;
        }

        public override void Notify_Studying(Pawn studier)
        {
            base.Notify_Studying(studier);
            studyingGraceTicks = 120;
            (parent as LC_JudgementBirdPawn)?.UpdateAnimation();
        }

        public override bool Notify_StudyInterval(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            studyingGraceTicks = 120;
            return base.Notify_StudyInterval(studier, workType);
        }

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            base.Notify_Studied(studier, interrupted);
            studyingGraceTicks = 0;
            (parent as LC_JudgementBirdPawn)?.UpdateAnimation();
        }

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();
            studyingGraceTicks = 0;
            CancelJudgementState();
            judgementCooldownTicks = 120;
            LCAbnormalitiesHelper.PlaySoundAt(JudgementBirdDefOf.LC_JudgementBird_On, parent);
            (parent as LC_JudgementBirdPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            studyingGraceTicks = 0;
            CancelJudgementState();
            (parent as LC_JudgementBirdPawn)?.UpdateAnimation(force: true);
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            CancelJudgementState();
            base.Notify_Killed(prevMap, dinfo);
        }

        public override void TickContained()
        {
            base.TickContained();
            if (studyingGraceTicks > 0)
            {
                studyingGraceTicks--;
            }

            (parent as LC_JudgementBirdPawn)?.UpdateAnimation();
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (!Escaped || !(parent is Pawn pawn) || pawn.Destroyed || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            if (isJudging)
            {
                judgingElapsedTicks++;

                // Recreate cast aura seamlessly if loaded from save mid-cast
                if (!verdictExecuted && (activeCastAura == null || activeCastAura.Destroyed) && judgingElapsedTicks < PropsJudge.judgementWindupTicks)
                {
                    activeCastAura = JudgementBirdVfxUtility.SpawnCastAura(pawn, PropsJudge.judgementRadius, Mathf.Max(30, PropsJudge.judgementWindupTicks - judgingElapsedTicks));
                }

                // Mid-cast brass scale tilt resonance right when BigChoo tilts heavily (t=2.5s)
                if (judgingElapsedTicks == ScaleTiltChimeTick)
                {
                    LCAbnormalitiesHelper.PlaySoundAt(JudgementBirdDefOf.LC_JudgementBird_On, pawn);
                }

                // Climax verdict moment right before BigChoo vanishes & arm snaps downward (t=4.58s)
                if (!verdictExecuted && judgingElapsedTicks >= PropsJudge.judgementWindupTicks)
                {
                    verdictExecuted = true;
                    if (activeCastAura != null && !activeCastAura.Destroyed)
                    {
                        activeCastAura.NotifyVerdictTriggered();
                        activeCastAura = null;
                    }

                    ExecuteSoulJudgement(pawn);
                }

                // End of full 330-tick Judgement animation (arm recovery complete)
                int totalAnim = Mathf.Max(PropsJudge.judgementWindupTicks + 30, PropsJudge.judgementTotalAnimTicks);
                if (judgingElapsedTicks >= totalAnim)
                {
                    isJudging = false;
                    judgingElapsedTicks = 0;
                    verdictExecuted = false;
                    judgementCooldownTicks = PropsJudge.judgementCooldownTicks;
                    (parent as LC_JudgementBirdPawn)?.UpdateAnimation(force: true);
                }

                return;
            }

            if (judgementCooldownTicks > 0)
            {
                judgementCooldownTicks--;
                return;
            }

            Pawn nearby = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position, PropsJudge.judgementRadius);
            if (nearby != null)
            {
                BeginJudgement(nearby);
            }
        }

        private void CancelJudgementState()
        {
            isJudging = false;
            judgingElapsedTicks = 0;
            verdictExecuted = false;
            if (activeCastAura != null && !activeCastAura.Destroyed)
            {
                activeCastAura.CancelEarly();
            }
            activeCastAura = null;
        }

        public void BeginJudgement(Pawn focusTarget = null)
        {
            if (parent == null || parent.Destroyed)
            {
                return;
            }

            isJudging = true;
            judgingElapsedTicks = 0;
            verdictExecuted = false;

            if (parent is LC_JudgementBirdPawn jbPawn)
            {
                if (focusTarget != null && focusTarget.Spawned)
                {
                    jbPawn.FaceToward(focusTarget.Position);
                }
                jbPawn.UpdateAnimation(force: true);
            }

            if (parent is Pawn casterPawn && casterPawn.Spawned)
            {
                if (activeCastAura != null && !activeCastAura.Destroyed)
                {
                    activeCastAura.CancelEarly();
                }
                activeCastAura = JudgementBirdVfxUtility.SpawnCastAura(casterPawn, PropsJudge.judgementRadius, PropsJudge.judgementWindupTicks);
            }

            LCAbnormalitiesHelper.PlaySoundAt(JudgementBirdDefOf.LC_JudgementBird_On, parent);
            Messages.Message(
                "LC_JudgementBird_ScaleTiltMsg".Translate(),
                parent,
                MessageTypeDefOf.ThreatBig,
                historical: false);
        }

        /// <summary>结算天平审判：释放全范围苍白灵魂冲击波，对范围内所有非森林三鸟单位造成蓝伤，并对陨落者降下黑羽绞刑架。</summary>
        public int ExecuteSoulJudgement(Pawn self)
        {
            if (self == null || !self.Spawned || self.Map == null)
            {
                return 0;
            }

            Map map = self.Map;
            IntVec3 center = self.Position;
            Vector3 centerVec = self.DrawPos.Yto0();
            float radiusSq = PropsJudge.judgementRadius * PropsJudge.judgementRadius;

            LCAbnormalitiesHelper.PlaySoundAt(JudgementBirdDefOf.LC_JudgementBird_Down, self);
            LCAbnormalitiesHelper.PlaySoundAt(JudgementBirdDefOf.LC_JudgementBird_Wave, self);
            JudgementBirdVfxUtility.SpawnPaleWave(map, centerVec, PropsJudge.judgementRadius);
            FleckMaker.ThrowDustPuffThick(centerVec, map, 3.0f, new Color(0.68f, 0.94f, 1.0f));

            for (int b = 0; b < 5; b++)
            {
                float ang = Rand.Range(0f, Mathf.PI * 2f);
                float spd = Rand.Range(0.025f, 0.055f);
                JudgementBirdVfxUtility.SpawnBandageWisp(
                    map,
                    centerVec + new Vector3(Rand.Range(-0.8f, 0.8f), 0f, Rand.Range(-0.2f, 1.1f)),
                    new Vector3(Mathf.Cos(ang) * spd, 0f, Mathf.Sin(ang) * spd + 0.015f),
                    Rand.Range(0.65f, 0.95f),
                    Rand.Range(45, 75));
            }

            int struck = 0;
            List<Pawn> candidates = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn target = candidates[i];
                if (target == null || target == self || target.Dead || target.Destroyed || !target.Spawned)
                {
                    continue;
                }

                string dn = target.def?.defName;
                if (dn == "PunishingBird" || dn == "JudgementBird" || dn == "BigBird")
                {
                    continue;
                }

                if ((target.Position - center).LengthHorizontalSquared > radiusSq)
                {
                    continue;
                }

                Vector3 targetVec = target.DrawPos.Yto0();
                float palePoints = Rand.Range(PropsJudge.judgementPaleMin, PropsJudge.judgementPaleMax);

                // Crisp cross Pale verdict slash VFX on every judged victim
                float slashAngle = Rand.Range(0f, 360f);
                JudgementBirdVfxUtility.SpawnPaleSlash(map, targetVec, slashAngle, 1.85f, flipSweep: false, isGoldenVerdict: true, lifeTicks: 18);
                JudgementBirdVfxUtility.SpawnPaleSlash(map, targetVec, slashAngle + 105f, 1.65f, flipSweep: true, isGoldenVerdict: false, lifeTicks: 16);

                target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_PaleDamage, palePoints, 1f, instigator: self));
                struck++;

                // Iconic Lobotomy Corporation Gallows execution VFX whenever a unit is killed by Judgement Bird (deduplicated with Patch_PawnKill)
                if (target.Dead || target.Destroyed)
                {
                    JudgementBirdVfxUtility.TrySpawnGallowsForVictim(target, map, targetVec);
                }
            }

            return struck;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref isJudging, "jbIsJudging", false);
            Scribe_Values.Look(ref judgingElapsedTicks, "jbJudgingElapsed", 0);
            Scribe_Values.Look(ref verdictExecuted, "jbVerdictExecuted", false);
            Scribe_Values.Look(ref judgementCooldownTicks, "jbJudgementCooldownTicks", 150);
            Scribe_Values.Look(ref studyingGraceTicks, "jbStudyingGraceTicks", 0);
        }
    }

    /// <summary>审判鸟实体 Pawn：驱动四套 Spine 烘焙动画（Standing / Seeing / Walk / Judgement）与左右朝向同步。</summary>
    public class LC_JudgementBirdPawn : LC_StandardEntityPawn
    {
        private bool facingWest;

        public CompJudgementBird JudgeComp => EntityComp as CompJudgementBird;

        public bool IsHeldOnPlatform => ParentHolder is LCAnomalyCore.Buildings.Building_AbnormalityHoldingPlatform;

        public bool IsFacingLeft => facingWest && JudgeComp?.Escaped == true;

        public bool IsBigScaleVisible => JudgeComp?.IsBigScaleVisible == true;

        public override string CurrentBodyTexPath =>
            JudgeComp?.IsJudging == true
                ? "Things/Pawn/JudgementBird/JudgementBird_Judging"
                : "Things/Pawn/JudgementBird/JudgementBird";

        public override Vector2 CurrentBodyDrawSize => new Vector2(2.55f, 2.55f);

        public void FaceToward(IntVec3 targetCell)
        {
            if (targetCell.IsValid)
            {
                if (targetCell.x < Position.x)
                {
                    facingWest = true;
                }
                else if (targetCell.x > Position.x)
                {
                    facingWest = false;
                }
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            UpdateAnimation(force: true);
        }

        public override void TickHolded()
        {
            base.TickHolded();
            facingWest = false;
            UpdateAnimation();
        }

        protected override void Tick()
        {
            CompJudgementBird comp = JudgeComp;
            if (comp != null)
            {
                if (comp.IsJudging && pather?.Moving == true)
                {
                    pather.StopDead();
                }

                if (comp.Escaped && Spawned && !Dead)
                {
                    UpdateHorizontalFacing();
                }
                else if (!comp.Escaped)
                {
                    facingWest = false;
                }
            }

            base.Tick();
            if (!Destroyed)
            {
                UpdateAnimation();
            }
        }

        private void UpdateHorizontalFacing()
        {
            if (JudgeComp?.IsJudging == true)
            {
                return;
            }

            if (pather != null && pather.Moving && pather.nextCell.IsValid)
            {
                if (pather.nextCell.x < Position.x)
                {
                    facingWest = true;
                    return;
                }
                if (pather.nextCell.x > Position.x)
                {
                    facingWest = false;
                    return;
                }
            }

            if (Rotation == Rot4.West)
            {
                facingWest = true;
            }
            else if (Rotation == Rot4.East)
            {
                facingWest = false;
            }
        }

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null)
            {
                return;
            }

            CompJudgementBird comp = JudgeComp;
            AnimationDef desired;

            if (IsHeldOnPlatform || (comp != null && !comp.Escaped))
            {
                desired = (comp != null && comp.IsBeingStudiedNow)
                    ? JudgementBirdDefOf.JudgementBird_Seeing
                    : JudgementBirdDefOf.JudgementBird_Standing;
            }
            else if (comp != null && comp.IsJudging)
            {
                desired = JudgementBirdDefOf.JudgementBird_Judgement;
            }
            else
            {
                bool isMoving = Spawned && pather?.Moving == true;
                desired = isMoving
                    ? JudgementBirdDefOf.JudgementBird_Walk
                    : JudgementBirdDefOf.JudgementBird_Standing;
            }

            if (desired != null && (force || Drawer.renderer.CurAnimation != desired))
            {
                Drawer.renderer.SetAnimation(desired);
            }
        }
    }

    /// <summary>审判鸟突破 AI：缓慢踱步逼近最近人类目标，天平倾斜施法期间驻足等待。</summary>
    public class JobGiver_JudgementBirdStalk : ThinkNode_JobGiver
    {
        protected override Verse.AI.Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
            {
                return null;
            }

            CompJudgementBird comp = pawn.TryGetComp<CompJudgementBird>();
            if (comp == null || !comp.Escaped)
            {
                return null;
            }

            if (comp.IsJudging)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 30);
            }

            Pawn target = LCAbnormalitiesHelper.FindNearestHumanlike(pawn.Map, pawn.Position);
            if (target == null)
            {
                return null;
            }

            if (pawn.Position.DistanceTo(target.Position) <= 5.5f)
            {
                return JobMaker.MakeJob(JobDefOf.Wait, 35);
            }

            Verse.AI.Job stalk = JobMaker.MakeJob(JobDefOf.Goto, target);
            stalk.expiryInterval = 60;
            stalk.checkOverrideOnExpire = true;
            stalk.canBashDoors = true;
            return stalk;
        }
    }

    /// <summary>
    /// 正义裁决者（ALEPH 蓝伤长刃）专属伤害处理器：
    /// 还原脑叶公司 60% 常规苍白连斩（5 段刃光）与 40% 扩展天平裁决连击（黄金天平印记 + 8 段裁决刃光 + 增伤顺劈），
    /// 播放原版 judgement2_1~4 音效并顺劈周围敌对目标。
    /// </summary>
    public class DamageWorker_Justitia : DamageWorker_FourColor
    {
        private const float ExtendedComboChance = 0.40f;
        private static bool inJustitiaCleavePass;

        public override Verse.DamageWorker.DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            if (inJustitiaCleavePass)
            {
                return base.Apply(dinfo, victim);
            }

            bool isExtendedCombo = Rand.Chance(ExtendedComboChance);
            if (isExtendedCombo)
            {
                // Extended 10-hit judgement verdict pattern delivers an enhanced Pale strike
                dinfo.SetAmount(dinfo.Amount * 1.35f);
            }

            Map victimMap = victim?.MapHeld;
            IntVec3 center = victim?.PositionHeld ?? IntVec3.Invalid;
            Vector3 victimVec = victim != null
                ? (victim.Spawned ? victim.DrawPos.Yto0() : center.ToVector3Shifted().Yto0())
                : Vector3.zero;

            Verse.DamageWorker.DamageResult result = base.Apply(dinfo, victim);

            if (dinfo.Instigator is Pawn attacker && victim != null && victimMap != null && center.InBounds(victimMap))
            {
                Map map = victimMap;
                Vector3 attackerVec = attacker.Spawned ? attacker.DrawPos.Yto0() : victimVec + Vector3.left;
                Vector3 attackDir = (victimVec - attackerVec).Yto0();
                float attackYaw = attackDir.sqrMagnitude > 0.001f
                    ? Mathf.Atan2(-attackDir.z, attackDir.x) * Mathf.Rad2Deg
                    : dinfo.Angle;

                // Trigger vertical overhead slash animation on Justitia wielder when not in Verdict Charge / Flurry
                if (!EgoWeaponAnimationManager.IsPlaying(attacker, JustitiaWeaponAnimHandler.ClipVerdictFlurry)
                    && !EgoWeaponAnimationManager.IsPlaying(attacker, JustitiaWeaponAnimHandler.ClipVerdictCharge))
                {
                    ThingWithComps heldWeapon = attacker.equipment?.Primary;
                    if (heldWeapon != null && heldWeapon.def?.defName == JustitiaWeaponAnimHandler.WeaponDefName)
                    {
                        float aimAngle = EgoWeaponAnimationUtility.ResolveAimAngle(attacker, victimVec);
                        EgoWeaponAnimationManager.Play(attacker, heldWeapon, JustitiaWeaponAnimHandler.ClipNormalSlash, 62, aimAngle);
                    }
                }

                // Spawn authentic multi-slash Pale crescent combo VFX & SFX (60% standard / 40% extended golden scale verdict)
                JudgementBirdVfxUtility.SpawnJustitiaCombo(map, victimVec, attackYaw, isExtendedCombo);

                int maxCleaveTargets = isExtendedCombo ? 3 : 2;
                float cleaveRadius = isExtendedCombo ? 2.6f : 2.2f;
                float cleavePoints = Mathf.Max(2f, dinfo.Amount * (isExtendedCombo ? 0.65f : 0.60f));
                int cleaved = 0;

                List<Pawn> nearby = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
                inJustitiaCleavePass = true;
                try
                {
                    for (int i = 0; i < nearby.Count && cleaved < maxCleaveTargets; i++)
                    {
                        Pawn other = nearby[i];
                        if (other == null || other == victim || other == attacker || other.Dead || other.Destroyed || !other.Spawned)
                        {
                            continue;
                        }

                        if (!other.HostileTo(attacker))
                        {
                            continue;
                        }

                        if (other.Position.DistanceTo(center) <= cleaveRadius)
                        {
                            Vector3 otherVec = other.DrawPos.Yto0();
                            JudgementBirdVfxUtility.SpawnPaleSlash(
                                map,
                                otherVec,
                                attackYaw + Rand.Range(-55f, 55f),
                                isExtendedCombo ? 1.75f : 1.45f,
                                flipSweep: (cleaved % 2) == 1,
                                isGoldenVerdict: isExtendedCombo,
                                lifeTicks: 15);

                            other.TakeDamage(new DamageInfo(
                                LCAnomalyCore.Defs.DamageDefOf.LC_PaleDamage,
                                cleavePoints,
                                1f,
                                dinfo.Angle,
                                attacker,
                                weapon: dinfo.Weapon));
                            cleaved++;
                        }
                    }
                }
                finally
                {
                    inJustitiaCleavePass = false;
                }
            }

            return result;
        }
    }
}
