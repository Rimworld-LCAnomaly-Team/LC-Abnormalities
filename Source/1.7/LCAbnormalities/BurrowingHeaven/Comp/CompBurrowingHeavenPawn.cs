using BurrowingHeaven.Things;
using LCAbnormalities.Settings;
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
        private bool hasTriggeredBreachCycle = false;
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
            hasTriggeredBreachCycle = false;
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
                float graceMult = Mathf.Clamp(LCAbnormalitiesMod.Settings?.BurrowingHeavenSightGraceMultiplier ?? 1f, 0.5f, 3f);
                int baseThreshold = hasTriggeredWorkDropThisSession
                    ? Props.workUnmonitoredIntervalTicks
                    : Props.workUnmonitoredGraceTicks;
                int threshold = Mathf.Max(15, Mathf.RoundToInt(baseThreshold * graceMult));

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
        /// 出逃状态下被注视时静止不动（除非配置开启近距离反击），脱离注视则爆发全房间/18格穿刺伤害并瞬移至其他目标附近。
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
                float graceMult = Mathf.Clamp(LCAbnormalitiesMod.Settings?.BurrowingHeavenSightGraceMultiplier ?? 1f, 0.5f, 3f);
                int baseThreshold = hasTriggeredBreachCycle
                    ? Props.breachUnwatchedBurstIntervalTicks
                    : Props.breachUnwatchedGraceTicks;
                int threshold = Mathf.Max(15, Mathf.RoundToInt(baseThreshold * graceMult));

                if (unmonitoredBreachTicks >= threshold)
                {
                    unmonitoredBreachTicks = 0;
                    hasTriggeredBreachCycle = true;

                    HealOnUnmonitoredBurst();

                    List<Pawn> targets = FindUnwatchedBurstTargets();
                    for (int i = 0; i < targets.Count; i++)
                    {
                        StrikeTarget(targets[i], Props.breachUnwatchedBurstDamage, applyStun: true);
                    }
                    if (targets.Count > 0)
                    {
                        Def.SoundDefOf.BurrowingHeaven_attack?.PlayOneShot(SoundInfo.InMap(SelfPawn));
                    }

                    bool allowTeleport = LCAbnormalitiesMod.Settings?.BurrowingHeavenAllowUnwatchedTeleport ?? true;
                    bool teleported = Props.breachTeleportOnUnwatched && allowTeleport && TryTeleportNearVictim();

                    Def.SoundDefOf.BurrowingHeaven_scream?.PlayOneShotOnCamera();
                    if (teleported)
                    {
                        Def.SoundDefOf.BurrowingHeaven_attack?.PlayOneShot(SoundInfo.InMap(SelfPawn));
                    }

                    SelfPawn.PlayAttackAnimation();

                    Messages.Message(
                        (teleported
                            ? "LC_BurrowingHeaven_LostSightBreachTeleport"
                            : "LC_BurrowingHeaven_LostSightBreach").Translate(),
                        new LookTargets(SelfPawn),
                        MessageTypeDefOf.ThreatBig,
                        historical: false);
                }
                return;
            }

            unmonitoredBreachTicks = 0;
            hasTriggeredBreachCycle = false;

            if (!Props.breachAttackWhileWatched)
                return;

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

        private static bool IsProperIndoorRoom(Room room)
        {
            return room != null
                && room.ProperRoom
                && !room.PsychologicallyOutdoors
                && !room.TouchesMapEdge
                && !room.IsDoorway;
        }

        private bool IsValidTargetCandidate(Pawn candidate)
        {
            return candidate != null
                && candidate != SelfPawn
                && candidate.Spawned
                && !candidate.Dead
                && candidate.Faction != SelfPawn.Faction;
        }

        private List<Pawn> FindUnwatchedBurstTargets()
        {
            var results = new List<Pawn>();
            Map map = parent.MapHeld;
            if (map == null)
                return results;

            Room currentRoom = parent.PositionHeld.GetRoom(map);
            bool useFullRoom = IsProperIndoorRoom(currentRoom);
            float radiusSq = Props.breachUnwatchedOutdoorRadius * Props.breachUnwatchedOutdoorRadius;

            IReadOnlyList<Pawn> allPawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn candidate = allPawns[i];
                if (!IsValidTargetCandidate(candidate))
                    continue;

                if (useFullRoom)
                {
                    if (candidate.PositionHeld.GetRoom(map) == currentRoom)
                        results.Add(candidate);
                }
                else if (candidate.PositionHeld.DistanceToSquared(parent.PositionHeld) <= radiusSq)
                {
                    results.Add(candidate);
                }
            }

            return results;
        }

        private bool TryTeleportNearVictim()
        {
            Map map = SelfPawn?.Map;
            if (map == null || !SelfPawn.Spawned)
                return false;

            Pawn anchor = SelectTeleportAnchor(map);
            if (anchor == null)
                return false;

            if (!TryFindTeleportCellNear(anchor, map, out IntVec3 destCell))
                return false;

            IntVec3 oldPos = SelfPawn.Position;
            AbnormalityUtility.Burst(oldPos, map);
            FilthMaker.TryMakeFilth(oldPos, map, RimWorld.ThingDefOf.Filth_Blood, SelfPawn.LabelShort, 1);

            SelfPawn.Position = destCell;
            SelfPawn.Notify_Teleported(endCurrentJob: true, resetTweenedPos: true);
            map.attackTargetsCache.UpdateTarget(SelfPawn);

            AbnormalityUtility.Burst(destCell, map);
            return true;
        }

        private Pawn SelectTeleportAnchor(Map map)
        {
            var tier1PlayerAway = new List<Pawn>();
            var tier2PlayerAny = new List<Pawn>();
            var tier3HumanlikeAny = new List<Pawn>();
            var tier4Fallback = new List<Pawn>();

            Room selfRoom = SelfPawn.Position.GetRoom(map);
            IReadOnlyList<Pawn> allPawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn candidate = allPawns[i];
                if (!IsValidTargetCandidate(candidate) || candidate.PositionHeld == SelfPawn.Position)
                    continue;

                bool isPlayerHumanlike = candidate.Faction == Faction.OfPlayer
                    && candidate.RaceProps != null
                    && candidate.RaceProps.Humanlike;

                if (isPlayerHumanlike && !candidate.Downed)
                {
                    bool differentRoom = selfRoom == null || candidate.PositionHeld.GetRoom(map) != selfRoom;
                    if (differentRoom || candidate.PositionHeld.DistanceToSquared(SelfPawn.Position) > 64f)
                    {
                        tier1PlayerAway.Add(candidate);
                        continue;
                    }
                }

                if (isPlayerHumanlike)
                {
                    tier2PlayerAny.Add(candidate);
                }
                else if (candidate.RaceProps != null && candidate.RaceProps.Humanlike)
                {
                    tier3HumanlikeAny.Add(candidate);
                }
                else
                {
                    tier4Fallback.Add(candidate);
                }
            }

            if (tier1PlayerAway.Count > 0) return tier1PlayerAway.RandomElement();
            if (tier2PlayerAny.Count > 0) return tier2PlayerAny.RandomElement();
            if (tier3HumanlikeAny.Count > 0) return tier3HumanlikeAny.RandomElement();
            if (tier4Fallback.Count > 0) return tier4Fallback.RandomElement();
            return null;
        }

        private bool IsValidTeleportCell(IntVec3 c, Map map)
        {
            return c.InBounds(map)
                && c.Standable(map)
                && !c.Fogged(map)
                && c != SelfPawn.Position
                && c.GetFirstPawn(map) == null
                && c.GetEdifice(map) == null;
        }

        private bool TryFindTeleportCellNear(Pawn anchor, Map map, out IntVec3 destCell)
        {
            destCell = IntVec3.Invalid;
            IntVec3 anchorPos = anchor.PositionHeld;
            Room anchorRoom = anchorPos.GetRoom(map);

            if (IsProperIndoorRoom(anchorRoom))
            {
                var preferredCells = new List<IntVec3>();
                var roomCells = new List<IntVec3>();
                foreach (IntVec3 cell in anchorRoom.Cells)
                {
                    if (!IsValidTeleportCell(cell, map))
                        continue;

                    roomCells.Add(cell);
                    float distSq = cell.DistanceToSquared(anchorPos);
                    if (distSq >= 4f && distSq <= 64f)
                        preferredCells.Add(cell);
                }

                if (preferredCells.Count > 0)
                {
                    destCell = preferredCells.RandomElement();
                    return true;
                }
                if (roomCells.Count > 0)
                {
                    destCell = roomCells.RandomElement();
                    return true;
                }
            }

            if (CellFinder.TryFindRandomCellNear(
                anchorPos,
                map,
                9,
                c => IsValidTeleportCell(c, map) && c.DistanceToSquared(anchorPos) >= 9f,
                out destCell))
            {
                return true;
            }

            return CellFinder.TryFindRandomCellNear(
                anchorPos,
                map,
                12,
                c => IsValidTeleportCell(c, map),
                out destCell);
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
                if (!IsValidTargetCandidate(candidate))
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

            DamageDef burstDamage = Def.DamageDefOf.LC_BurrowingHeaven_BlackDamage ?? LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
            DamageInfo dinfo = new DamageInfo(burstDamage, damageAmount, instigator: SelfPawn);
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
                    // 原作 LookAtMe_stat: 本能各等级均为 0%
                    workTypeRate = 0.0f;
                    break;

                case EAnomalyWorkType.Insight:
                    // 原作 LookAtMe_stat: 洞察 I=30%, II/III=40%, IV/V=50%
                    switch (studier.GetPawnStatusELevel(EPawnStatus.Prudence))
                    {
                        case EPawnLevel.I:
                            workTypeRate = 0.30f;
                            break;
                        case EPawnLevel.II:
                        case EPawnLevel.III:
                            workTypeRate = 0.40f;
                            break;
                        default:
                            workTypeRate = 0.50f;
                            break;
                    }
                    break;

                case EAnomalyWorkType.Attachment:
                    // 原作 LookAtMe_stat: 沟通 I/II/III=40%, IV=30%, V=20%
                    switch (studier.GetPawnStatusELevel(EPawnStatus.Temperance))
                    {
                        case EPawnLevel.I:
                        case EPawnLevel.II:
                        case EPawnLevel.III:
                            workTypeRate = 0.40f;
                            break;
                        case EPawnLevel.IV:
                            workTypeRate = 0.30f;
                            break;
                        default:
                            workTypeRate = 0.20f;
                            break;
                    }
                    break;

                case EAnomalyWorkType.Repression:
                    // 原作 LookAtMe_stat: 压迫 I=40%, II=45%, III=50%, IV=55%, V=60%
                    switch (studier.GetPawnStatusELevel(EPawnStatus.Justice))
                    {
                        case EPawnLevel.I:
                            workTypeRate = 0.40f;
                            break;
                        case EPawnLevel.II:
                            workTypeRate = 0.45f;
                            break;
                        case EPawnLevel.III:
                            workTypeRate = 0.50f;
                            break;
                        case EPawnLevel.IV:
                            workTypeRate = 0.55f;
                            break;
                        default:
                            workTypeRate = 0.60f;
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
            Scribe_Values.Look(ref hasTriggeredBreachCycle, "hasTriggeredBreachCycle", false);
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
