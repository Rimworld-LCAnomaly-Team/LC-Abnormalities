using System;
using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.BackwardClock
{
    [DefOf]
    public static class BackwardClockDefOf
    {
        public static SoundDef LC_BackwardClock_On;
        public static SoundDef LC_BackwardClock_Put;
        public static SoundDef LC_BackwardClock_Work;
        public static SoundDef LC_BackwardClock_SkillStart;
        public static SoundDef LC_BackwardClock_SkillFinish;

        static BackwardClockDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BackwardClockDefOf));
        }
    }

    public class CompProperties_BackwardClock : CompProperties_AbnormalityTool
    {
        public int maxCharges = 4;
        public int minChargeEmployeeLevel = 3;
        public int minActivateEmployeeLevel = 5;

        public CompProperties_BackwardClock()
        {
            compClass = typeof(CompBackwardClock);
        }
    }

    /// <summary>
    /// D-09-104 逆流时钟核心逻辑（WAW 单次充能/牺牲型工具）：
    /// 充能 0→4 需 III 级及以上员工逐次拨动表盘；满充能（4/4）后仅 V 级员工可发动时光倒流，
    /// 抹除发动者自身，同时立即镇压同地图所有非 ALEPH 级突破异想体与考验实体并重置所有非 ALEPH 收容室逆卡巴拉计数器。
    /// </summary>
    public class CompBackwardClock : CompAbnormalityTool
    {
        private int chargeLevel;
        private int totalRewinds;
        private Pawn pendingRewindUser;
        private int pendingRewindTicks;

        public CompProperties_BackwardClock PropsClock => (CompProperties_BackwardClock)props;

        public int ChargeLevel => chargeLevel;

        public int MaxCharges => Mathf.Max(1, PropsClock.maxCharges);

        public bool IsFullyCharged => chargeLevel >= MaxCharges;

        public int TotalRewinds => totalRewinds;

        public override AcceptanceReport CanUseTool(Pawn pawn)
        {
            AcceptanceReport baseReport = base.CanUseTool(pawn);
            if (!baseReport.Accepted)
            {
                return baseReport;
            }

            CompPawnStatus status = pawn?.GetComp<CompPawnStatus>();
            int empLevel = status != null ? (int)status.SingleLevel : 0;

            if (!IsFullyCharged)
            {
                if (empLevel < PropsClock.minChargeEmployeeLevel)
                {
                    return "LC_BackwardClock_NeedLv3".Translate();
                }

                return true;
            }

            if (empLevel < PropsClock.minActivateEmployeeLevel)
            {
                return "LC_BackwardClock_NeedLv5".Translate();
            }

            return true;
        }

        public override void OnToolUsed(Pawn user)
        {
            RecordToolUse(user, 1, 0);

            if (!IsFullyCharged)
            {
                chargeLevel = Mathf.Clamp(chargeLevel + 1, 0, MaxCharges);

                if (ActiveMap != null && ActivePosition.IsValid)
                {
                    SoundDef sfx = IsFullyCharged
                        ? (BackwardClockDefOf.LC_BackwardClock_On ?? BackwardClockDefOf.LC_BackwardClock_Work)
                        : BackwardClockDefOf.LC_BackwardClock_Work;
                    sfx?.PlayOneShot(new TargetInfo(ActivePosition, ActiveMap));
                    FleckMaker.ThrowMicroSparks(ActiveDrawPos, ActiveMap);
                }

                string msgKey = IsFullyCharged ? "LC_BackwardClock_FullyChargedMsg" : "LC_BackwardClock_ChargedMsg";
                Messages.Message(
                    msgKey.Translate(user?.LabelShortCap ?? "?", chargeLevel, MaxCharges),
                    new LookTargets(ActivePosition, ActiveMap),
                    MessageTypeDefOf.PositiveEvent,
                    historical: false);
                return;
            }

            // 满充能启动时光倒流：推迟 1 Tick 在 TickWhileHeld 中执行抹除，避免在 JobDriver Toil 中同步销毁当前作业者
            pendingRewindUser = user;
            pendingRewindTicks = 1;
        }

        public override void TickWhileHeld()
        {
            base.TickWhileHeld();
            TickPendingRewind();
        }

        public override void CompTick()
        {
            base.CompTick();
            TickPendingRewind();
        }

        private void TickPendingRewind()
        {
            if (pendingRewindTicks <= 0)
            {
                return;
            }

            pendingRewindTicks--;
            if (pendingRewindTicks <= 0)
            {
                Pawn user = pendingRewindUser;
                pendingRewindUser = null;
                ExecuteTimeRewindNow(user);
            }
        }

        /// <summary>
        /// 立即执行时光倒流：抹除发动者，清场同地图所有非 ALEPH 突破异想体与考验单位，并重置收容中的非 ALEPH 逆卡巴拉计数器。
        /// </summary>
        public int ExecuteTimeRewindNow(Pawn activator)
        {
            Map map = ActiveMap ?? activator?.MapHeld;
            IntVec3 pos = ActivePosition.IsValid ? ActivePosition : (activator?.PositionHeld ?? IntVec3.Invalid);

            chargeLevel = 0;
            totalRewinds++;

            if (map != null && pos.IsValid && pos.InBounds(map))
            {
                BackwardClockDefOf.LC_BackwardClock_SkillStart?.PlayOneShot(new TargetInfo(pos, map));
                BackwardClockDefOf.LC_BackwardClock_SkillFinish?.PlayOneShot(new TargetInfo(pos, map));
                FleckMaker.ThrowLightningGlow(pos.ToVector3Shifted(), map, 3.2f);
            }

            int suppressedCount = 0;
            if (map != null)
            {
                // 1. 镇压同地图所有非 ALEPH 突破异想体与考验单位
                List<Pawn> spawned = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
                for (int i = 0; i < spawned.Count; i++)
                {
                    Pawn p = spawned[i];
                    if (p == null || p.Dead || p.Destroyed || !p.Spawned || p == activator || p == parent)
                    {
                        continue;
                    }

                    string defName = p.def?.defName;
                    bool isOrdeal = !string.IsNullOrEmpty(defName)
                        && defName.StartsWith("LCOrdeal_", StringComparison.Ordinal);

                    if (isOrdeal)
                    {
                        p.Kill(null);
                        suppressedCount++;
                        continue;
                    }

                    CompAbnormality abn = p.TryGetComp<CompAbnormality>();
                    if (abn == null)
                    {
                        continue;
                    }

                    string category = abn.AbnormalityCategory?.defName;
                    if (string.Equals(category, "ALEPH", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    CompLCAbnormality lcAbn = p.TryGetComp<CompLCAbnormality>();
                    ThingDef eggDef = lcAbn?.Props?.dyingFxDef?.GetModExtension<EggReturnExtension>()?.eggDef;
                    if (eggDef != null)
                    {
                        LCAbnormalitiesHelper.ReturnEntityToEgg(p, eggDef, lcAbn.biosignature);
                    }
                    else
                    {
                        p.Kill(null);
                    }

                    suppressedCount++;
                }

                // 2. 重置同地图所有收容中的非 ALEPH 逆卡巴拉计数器
                IEnumerable<Building_AbnormalityHoldingPlatform> platforms =
                    map.listerBuildings?.AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>();
                if (platforms != null)
                {
                    foreach (Building_AbnormalityHoldingPlatform plat in platforms)
                    {
                        Pawn held = plat?.HeldPawn;
                        CompAbnormality heldAbn = held?.TryGetComp<CompAbnormality>();
                        if (heldAbn == null || !heldAbn.QliphothEnabled)
                        {
                            continue;
                        }

                        if (string.Equals(heldAbn.AbnormalityCategory?.defName, "ALEPH", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        heldAbn.QliphothCountCurrent = heldAbn.QliphothCountMax;
                    }
                }
            }

            // 3. 发动者被时间抹除（不留尸体）
            string activatorLabel = activator?.LabelShortCap ?? "LC_BackwardClock_UnknownUser".Translate().ToString();
            if (activator != null && !activator.Destroyed)
            {
                activator.jobs?.StopAll(ifLayingKeepLaying: false);
                if (activator.Spawned)
                {
                    activator.DeSpawn(DestroyMode.Vanish);
                }

                if (!activator.Destroyed)
                {
                    activator.Destroy(DestroyMode.Vanish);
                }
            }

            if (map != null && pos.IsValid && pos.InBounds(map))
            {
                Find.LetterStack.ReceiveLetter(
                    "LC_BackwardClock_RewindLabel".Translate(),
                    "LC_BackwardClock_RewindText".Translate(activatorLabel, suppressedCount),
                    LetterDefOf.PositiveEvent,
                    new LookTargets(pos, map));
            }

            return suppressedCount;
        }

        protected override string GetHeldOverrideTexPath()
        {
            int idx = Mathf.Clamp(chargeLevel, 0, 4);
            return "Things/Building/BackwardClock/BackwardClock_" + idx;
        }

        public override string GetToolStatusInspectString()
        {
            string phaseText = IsFullyCharged
                ? "LC_BackwardClock_InspectReady".Translate()
                : "LC_BackwardClock_InspectCharging".Translate();
            return "LC_BackwardClock_InspectCharge".Translate(chargeLevel, MaxCharges, phaseText, totalRewinds);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref chargeLevel, "bcChargeLevel", 0);
            Scribe_Values.Look(ref totalRewinds, "bcTotalRewinds", 0);
            Scribe_References.Look(ref pendingRewindUser, "bcPendingRewindUser");
            Scribe_Values.Look(ref pendingRewindTicks, "bcPendingRewindTicks", 0);
        }
    }
}
