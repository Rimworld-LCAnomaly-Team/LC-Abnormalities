using BurrowingHeaven.Things;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace BurrowingHeaven.Comp
{
    public class CompBurrowingHeaven : CompAbnormality
    {
        #region 变量

        public new CompProperties_BurrowingHeaven Props => (CompProperties_BurrowingHeaven)props;

        public LC_BurrowingHeavenPawn SelfPawn => (LC_BurrowingHeavenPawn)parent;

        private int lastStudyingTick = -1;
        private int unmonitoredWorkTicks = 0;
        private bool workWarningSent = false;
        private bool hasTriggeredWorkDropThisSession = false;
        private Pawn currentStudier = null;

        public const int MaxHangingHeadSlots = 6;
        private static readonly float[] DefaultSlotTiltAngles = { -18f, 16f, 22f, -20f, 12f, -14f };

        private int unmonitoredBreachTicks = 0;
        private int unmonitoredBreachStacks = 0;
        private int watchedAttackCooldown = 60;

        private List<HangingHeadRecord> hangingHeads = new List<HangingHeadRecord>();
        private int nextHangingHeadSlot = 0;

        public int HangingHeadCount => hangingHeads?.Count ?? 0;

        public bool IsBeingStudiedNow =>
            lastStudyingTick >= 0
            && Find.TickManager != null
            && Find.TickManager.TicksGame - lastStudyingTick <= 5
            && !Escaped;

        public HangingHeadRecord GetHangingHead(int slotIndex)
        {
            if (hangingHeads == null)
                return null;

            for (int i = 0; i < hangingHeads.Count; i++)
            {
                if (hangingHeads[i] != null && hangingHeads[i].slotIndex == slotIndex)
                    return hangingHeads[i];
            }
            return null;
        }

        #endregion 变量

        #region 触发事件

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            ClearWorkMonitoring();
            if (SelfPawn != null)
            {
                SelfPawn.ShouldCheckInsight = false;
                SelfPawn.SetOutOfSight(false);
            }

            Util.EffectUtil.OnBurrowingHeavenDeath((Pawn)parent, prevMap);
        }

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();
            ClearWorkMonitoring();

            if (SelfPawn != null)
            {
                SelfPawn.ShouldCheckInsight = true;
                SelfPawn.SetBreachAnimation();
            }

            unmonitoredBreachTicks = 0;
            unmonitoredBreachStacks = 0;
            watchedAttackCooldown = 60;
        }

        /// <summary>
        /// 绑到收容平台上后的操作
        /// </summary>
        public override void Notify_Holded()
        {
            CheckIsDiscovered();
            SelfPawn?.SetIdleAnimation();
        }

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            ClearWorkMonitoring();
            if (SelfPawn != null)
            {
                SelfPawn.ShouldCheckInsight = false;
                SelfPawn.SetOutOfSight(false);
            }

            base.Notify_Studied(studier, interrupted);
            if (!Escaped)
            {
                SelfPawn?.SetIdleAnimation();
            }
        }

        public override void Notify_Studying(Pawn studier)
        {
            lastStudyingTick = Find.TickManager.TicksGame;
            currentStudier = studier;

            if (!workWarningSent)
            {
                workWarningSent = true;
                Messages.Message(
                    "LC_BurrowingHeaven_WorkWatchWarning".Translate(),
                    new LookTargets(parent.PositionHeld, parent.MapHeld),
                    MessageTypeDefOf.CautionInput,
                    historical: false);
            }

            if (SelfPawn != null)
            {
                SelfPawn.ShouldCheckInsight = true;
                SelfPawn.SetWorkAnimation();
            }
        }

        #endregion 触发事件

        #region 注视与战斗机制

        /// <summary>
        /// 收容工作期间每 Tick 检查玩家摄像机是否持续注视穿刺乐园。
        /// </summary>
        public void TickContainmentSight()
        {
            if (SelfPawn == null)
                return;

            bool studying = lastStudyingTick >= 0
                && Find.TickManager.TicksGame - lastStudyingTick <= 5
                && !Escaped;

            if (!studying)
            {
                if (!Escaped)
                {
                    ClearWorkMonitoring();
                    SelfPawn.ShouldCheckInsight = false;
                    SelfPawn.SetOutOfSight(false);
                }
                return;
            }

            bool outside = SelfPawn.IsOutsidePlayerCamera();
            SelfPawn.SetOutOfSight(outside);

            if (outside)
            {
                unmonitoredWorkTicks++;
                int threshold = hasTriggeredWorkDropThisSession
                    ? Props.workUnmonitoredIntervalTicks
                    : Props.workUnmonitoredGraceTicks;

                if (unmonitoredWorkTicks >= threshold)
                {
                    unmonitoredWorkTicks = 0;
                    hasTriggeredWorkDropThisSession = true;

                    Def.SoundDefOf.BurrowingHeaven_scream?.PlayOneShotOnCamera();
                    Messages.Message(
                        "LC_BurrowingHeaven_LostSightWork".Translate(),
                        new LookTargets(parent.PositionHeld, parent.MapHeld),
                        MessageTypeDefOf.ThreatSmall,
                        historical: false);

                    QliphothCountCurrent--;
                }
            }
            else
            {
                unmonitoredWorkTicks = Mathf.Max(0, unmonitoredWorkTicks - 2);
            }
        }

        /// <summary>
        /// 出逃状态下根据注视状态执行近距离防御反击或全图无人注视穿刺爆发。
        /// </summary>
        public void TickBreachCombat()
        {
            if (!Escaped || !parent.Spawned || SelfPawn == null || SelfPawn.Dead || SelfPawn.Downed)
                return;

            bool outside = SelfPawn.IsOutsidePlayerCamera();
            SelfPawn.SetOutOfSight(outside);

            if (outside)
            {
                unmonitoredBreachTicks++;
                int threshold = unmonitoredBreachStacks == 0
                    ? Props.breachUnwatchedGraceTicks
                    : Props.breachUnwatchedBurstIntervalTicks;

                if (unmonitoredBreachTicks >= threshold)
                {
                    unmonitoredBreachTicks = 0;
                    unmonitoredBreachStacks = Mathf.Min(unmonitoredBreachStacks + 1, 4);

                    float radius = Mathf.Min(
                        Props.breachUnwatchedMaxRadius,
                        Props.breachUnwatchedBaseRadius + (unmonitoredBreachStacks - 1) * Props.breachUnwatchedRadiusStep);

                    Def.SoundDefOf.BurrowingHeaven_scream?.PlayOneShotOnCamera();
                    Messages.Message(
                        "LC_BurrowingHeaven_LostSightBreach".Translate(),
                        new LookTargets(parent),
                        MessageTypeDefOf.ThreatBig,
                        historical: false);

                    HealOnUnmonitoredBurst();

                    List<Pawn> targets = FindValidTargetsInRadius(radius, Props.breachUnwatchedMaxTargets);
                    if (targets.Count > 0)
                    {
                        for (int i = 0; i < targets.Count; i++)
                        {
                            StrikeTarget(targets[i], Props.breachUnwatchedBurstDamage, applyStun: true);
                        }
                        Def.SoundDefOf.BurrowingHeaven_attack?.PlayOneShot(SoundInfo.InMap(SelfPawn));
                    }

                    SelfPawn.PlayAttackAnimation();
                }
            }
            else
            {
                unmonitoredBreachTicks = 0;
                unmonitoredBreachStacks = 0;

                watchedAttackCooldown--;
                if (watchedAttackCooldown <= 0)
                {
                    List<Pawn> targets = FindValidTargetsInRadius(Props.breachWatchedAttackRadius, 2);
                    if (targets.Count > 0)
                    {
                        for (int i = 0; i < targets.Count; i++)
                        {
                            StrikeTarget(targets[i], Props.breachWatchedAttackDamage, applyStun: false);
                        }
                        Def.SoundDefOf.BurrowingHeaven_attack?.PlayOneShot(SoundInfo.InMap(SelfPawn));
                        SelfPawn.PlayAttackAnimation();
                        watchedAttackCooldown = Props.breachWatchedAttackIntervalTicks;
                    }
                    else
                    {
                        watchedAttackCooldown = 30;
                    }
                }
            }
        }

        protected override void QliphothMeltdown()
        {
            if (currentStudier != null
                && !currentStudier.Dead
                && currentStudier.Spawned
                && currentStudier.MapHeld == parent.MapHeld)
            {
                StrikeTarget(currentStudier, Props.workMeltdownStrikeDamage, applyStun: false);
                Def.SoundDefOf.BurrowingHeaven_attack?.PlayOneShot(SoundInfo.InMap(currentStudier));
                SelfPawn?.PlayAttackAnimation();
            }

            ClearWorkMonitoring();
            if (SelfPawn != null)
            {
                SelfPawn.ShouldCheckInsight = false;
                SelfPawn.SetOutOfSight(false);
            }

            base.QliphothMeltdown();
        }

        private void ClearWorkMonitoring()
        {
            lastStudyingTick = -1;
            unmonitoredWorkTicks = 0;
            workWarningSent = false;
            hasTriggeredWorkDropThisSession = false;
            currentStudier = null;
        }

        private List<Pawn> FindValidTargetsInRadius(float radius, int maxTargets)
        {
            var results = new List<Pawn>();
            Map map = parent.MapHeld;
            if (map == null || maxTargets <= 0)
                return results;

            float radiusSq = radius * radius;
            IReadOnlyList<Pawn> allPawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn candidate = allPawns[i];
                if (candidate == null || candidate == SelfPawn || !candidate.Spawned || candidate.Dead)
                    continue;

                if (candidate.Faction == SelfPawn.Faction)
                    continue;

                if (candidate.PositionHeld.DistanceToSquared(parent.PositionHeld) <= radiusSq)
                {
                    results.Add(candidate);
                }
            }

            results.Sort((a, b) =>
            {
                int downedCmp = a.Downed.CompareTo(b.Downed);
                if (downedCmp != 0)
                    return downedCmp;

                float distA = a.PositionHeld.DistanceToSquared(parent.PositionHeld);
                float distB = b.PositionHeld.DistanceToSquared(parent.PositionHeld);
                return distA.CompareTo(distB);
            });

            if (results.Count > maxTargets)
            {
                results.RemoveRange(maxTargets, results.Count - maxTargets);
            }

            return results;
        }

        private void StrikeTarget(Pawn target, float damageAmount, bool applyStun)
        {
            if (target == null || target.Dead || !target.Spawned)
                return;

            bool wasAliveHumanlike = target.RaceProps != null && target.RaceProps.Humanlike;
            IntVec3 deathPos = target.PositionHeld;
            Map deathMap = target.MapHeld;
            HangingHeadRecord preSnapshot = wasAliveHumanlike
                ? HangingHeadRecord.CreateFrom(target, -1, 0f)
                : null;

            DamageDef blackDamage = Def.DamageDefOf.LC_BurrowingHeaven_BlackDamage ?? LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage;
            DamageInfo dinfo = new DamageInfo(blackDamage, damageAmount, instigator: SelfPawn);
            AbnormalityUtility.Burst(deathPos, deathMap);
            target.TakeDamage(dinfo);

            if (applyStun && !target.Dead)
            {
                DamageInfo stunDinfo = new DamageInfo(RimWorld.DamageDefOf.Stun, 1f, instigator: SelfPawn);
                target.TakeDamage(stunDinfo);
            }

            if (wasAliveHumanlike && target.Dead && preSnapshot != null)
            {
                HandleHumanlikeKill(target, preSnapshot, deathPos, deathMap, dinfo);
            }
        }

        private void HandleHumanlikeKill(Pawn victim, HangingHeadRecord snapshot, IntVec3 deathPos, Map deathMap, DamageInfo killDinfo)
        {
            DecapitateVictim(victim, killDinfo);

            if (hangingHeads == null)
                hangingHeads = new List<HangingHeadRecord>();

            int slot = nextHangingHeadSlot % MaxHangingHeadSlots;
            nextHangingHeadSlot = (slot + 1) % MaxHangingHeadSlots;

            snapshot.slotIndex = slot;
            snapshot.tiltAngle = DefaultSlotTiltAngles[slot] + Rand.Range(-6f, 6f);

            int existingIdx = hangingHeads.FindIndex(r => r != null && r.slotIndex == slot);
            if (existingIdx >= 0)
            {
                hangingHeads[existingIdx] = snapshot;
            }
            else
            {
                hangingHeads.Add(snapshot);
            }

            SelfPawn?.Drawer?.renderer?.SetAllGraphicsDirty();

            if (deathMap != null && deathPos.InBounds(deathMap))
            {
                FilthMaker.TryMakeFilth(deathPos, deathMap, RimWorld.ThingDefOf.Filth_Blood, victim.LabelShort, 4);
                ThingDef killTreeDef = Def.ThingDefOf.BurrowingHeaven_KillTree;
                if (killTreeDef != null && deathPos.GetFirstThing(deathMap, killTreeDef) == null)
                {
                    GenSpawn.Spawn(killTreeDef, deathPos, deathMap);
                }
            }

            Messages.Message(
                "LC_BurrowingHeaven_VictimImpaled".Translate(snapshot.victimName),
                new LookTargets(parent.PositionHeld, parent.MapHeld),
                MessageTypeDefOf.NegativeEvent,
                historical: true);
        }

        private static void DecapitateVictim(Pawn victim, DamageInfo killDinfo)
        {
            BodyPartRecord headPart = victim?.health?.hediffSet?
                .GetNotMissingParts()
                .FirstOrDefault(p => p.def == RimWorld.BodyPartDefOf.Head);

            if (headPart == null)
                return;

            if (HediffMaker.MakeHediff(RimWorld.HediffDefOf.MissingBodyPart, victim, headPart) is Hediff_MissingPart missingHead)
            {
                missingHead.lastInjury = RimWorld.HediffDefOf.Cut;
                missingHead.IsFresh = true;
                victim.health.AddHediff(missingHead, headPart, killDinfo);
                victim.Drawer?.renderer?.SetAllGraphicsDirty();
            }
        }

        private void HealOnUnmonitoredBurst()
        {
            float remaining = Props.breachUnwatchedHealAmount;
            List<Hediff> hediffs = SelfPawn?.health?.hediffSet?.hediffs;
            if (remaining <= 0f || hediffs == null)
                return;

            for (int i = hediffs.Count - 1; i >= 0 && remaining > 0f; i--)
            {
                if (hediffs[i] is Hediff_Injury injury && !injury.IsPermanent() && injury.Severity > 0f)
                {
                    float heal = Mathf.Min(remaining, injury.Severity);
                    injury.Heal(heal);
                    remaining -= heal;
                }
            }
        }

        #endregion 注视与战斗机制

        #region 研究与图鉴

        protected override float StudySuccessRateCalculate(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            if (studier == null)
                return 0f;

            float baseRate = base.StudySuccessRateCalculate(studier, workType);
            float workTypeRate = 0f;

            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                    switch (studier.GetPawnStatusELevel(EPawnStatus.Fortitude))
                    {
                        case EPawnLevel.I:
                        case EPawnLevel.II:
                            workTypeRate = 0.25f;
                            break;
                        case EPawnLevel.III:
                            workTypeRate = 0.30f;
                            break;
                        default:
                            workTypeRate = 0.35f;
                            break;
                    }
                    break;

                case EAnomalyWorkType.Insight:
                    switch (studier.GetPawnStatusELevel(EPawnStatus.Prudence))
                    {
                        case EPawnLevel.I:
                            workTypeRate = 0.45f;
                            break;
                        case EPawnLevel.II:
                            workTypeRate = 0.50f;
                            break;
                        case EPawnLevel.III:
                            workTypeRate = 0.55f;
                            break;
                        default:
                            workTypeRate = 0.60f;
                            break;
                    }
                    break;

                case EAnomalyWorkType.Attachment:
                    switch (studier.GetPawnStatusELevel(EPawnStatus.Temperance))
                    {
                        case EPawnLevel.I:
                        case EPawnLevel.II:
                            workTypeRate = 0.40f;
                            break;
                        case EPawnLevel.III:
                            workTypeRate = 0.45f;
                            break;
                        default:
                            workTypeRate = 0.50f;
                            break;
                    }
                    break;

                case EAnomalyWorkType.Repression:
                    switch (studier.GetPawnStatusELevel(EPawnStatus.Justice))
                    {
                        case EPawnLevel.I:
                        case EPawnLevel.II:
                            workTypeRate = 0.20f;
                            break;
                        case EPawnLevel.III:
                            workTypeRate = 0.25f;
                            break;
                        default:
                            workTypeRate = 0.30f;
                            break;
                    }
                    break;
            }

            return Mathf.Clamp(baseRate + workTypeRate, 0f, 0.90f);
        }

        protected override void StudyEvent_Good(Pawn studier)
        {
            base.StudyEvent_Good(studier);
            //穿刺乐园研究为优 +2点计数器
            QliphothCountCurrent++;
        }

        protected override void StudyEvent_Normal(Pawn studier)
        {
            base.StudyEvent_Normal(studier);
            //穿刺乐园研究为良 +1点计数器
            QliphothCountCurrent++;
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            PeBoxComp?.CheckSpawnPeBox(studier, PeBoxProducedTemp);
            StudyUtil.DoStudyResultEffect(studier, (Pawn)parent, LC_StudyResult.Bad);
            //穿刺乐园研究为差不减计数器
        }

        /// <summary>
        /// 检查是否已在图鉴中被解锁
        /// </summary>
        private void CheckIsDiscovered()
        {
            AbnormalityUtility.Discover(parent.def);
        }

        #endregion 研究与图鉴

        #region 持久化与UI

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_Values.Look(ref lastStudyingTick, "lastStudyingTick", -1);
            Scribe_Values.Look(ref unmonitoredWorkTicks, "unmonitoredWorkTicks", 0);
            Scribe_Values.Look(ref workWarningSent, "workWarningSent", false);
            Scribe_Values.Look(ref hasTriggeredWorkDropThisSession, "hasTriggeredWorkDropThisSession", false);
            Scribe_References.Look(ref currentStudier, "currentStudier");

            Scribe_Values.Look(ref unmonitoredBreachTicks, "unmonitoredBreachTicks", 0);
            Scribe_Values.Look(ref unmonitoredBreachStacks, "unmonitoredBreachStacks", 0);
            Scribe_Values.Look(ref watchedAttackCooldown, "watchedAttackCooldown", 60);

            Scribe_Collections.Look(ref hangingHeads, "hangingHeads", LookMode.Deep);
            Scribe_Values.Look(ref nextHangingHeadSlot, "nextHangingHeadSlot", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && hangingHeads == null)
            {
                hangingHeads = new List<HangingHeadRecord>();
            }
        }

        public override string CompInspectStringExtra()
        {
            TaggedString status = (SelfPawn != null && SelfPawn.ShouldCheckInsight)
                ? (SelfPawn.IsOutOfSight
                    ? "LC_BurrowingHeaven_SightStatusUnwatched".Translate()
                    : "LC_BurrowingHeaven_SightStatusWatched".Translate())
                : "LC_BurrowingHeaven_SightStatusIdle".Translate();

            string result = "LC_BurrowingHeaven_Biosignature".Translate() + ": " + BiosignatureName + "\n" + status;
            if (hangingHeads != null && hangingHeads.Count > 0)
            {
                string names = string.Join(", ", hangingHeads.Where(h => h != null).Select(h => h.victimName));
                result += "\n" + "LC_BurrowingHeaven_HangingHeads".Translate(hangingHeads.Count, MaxHangingHeadSlots, names);
            }
            return result;
        }

        #endregion 持久化与UI
    }
}
