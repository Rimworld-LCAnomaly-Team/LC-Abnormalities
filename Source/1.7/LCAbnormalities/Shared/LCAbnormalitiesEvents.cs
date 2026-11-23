using System.Collections.Generic;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;
using RimWorld;
using Verse;

namespace LCAbnormalities.Shared
{
    /// <summary>允许通过静态路由器接收任意异想体研究开始事件的状态接口。</summary>
    public interface ILCPawnStudyStartListener
    {
        void OnStudyStarted(Pawn studier, CompAbnormality abnormality);
    }

    /// <summary>允许通过静态路由器接收任意异想体研究完成事件的状态接口。</summary>
    public interface ILCPawnStudyCompleteListener
    {
        void OnStudyCompleted(Pawn studier, CompAbnormality abnormality, LC_StudyResult result);
    }

    /// <summary>允许通过静态路由器接收宿主造成有效伤害事件的状态接口。</summary>
    public interface ILCPawnCombatDamageListener
    {
        void OnDealtDamage(Pawn attacker, Thing target, DamageInfo dinfo, float dealt);
    }

    /// <summary>允许收容于平台内的异想体组件接收同地图其他异想体出逃事件。</summary>
    public interface ILCMapAbnormalityEscapeListener
    {
        void NotifyOtherAbnormalityEscaped(CompAbnormality escapedComp);
    }

    /// <summary>允许收容于平台内的异想体组件接收同地图人类死亡事件。</summary>
    public interface ILCMapHumanlikeDeathListener
    {
        void NotifyHumanlikeDied(Pawn victim, Map map);
    }

    /// <summary>
    /// 集中订阅 Core 的全局轻量静态事件并派发至持有对应 Hediff 或收容平台内组件的单位，避免每实例订阅导致的生命周期泄漏。
    /// </summary>
    public static class LCAbnormalitiesEvents
    {
        private static bool initialized;

        public static void EnsureRegistered()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            CompAbnormality.OnAnyAbnormalityStudyStarted += HandleStudyStarted;
            CompAbnormality.OnAnyAbnormalityStudyCompleted += HandleStudyCompleted;
            CompAbnormality.OnAnyAbnormalityEscaped += HandleAbnormalityEscaped;
            LCAnomalyCore.Patch.Patch_Thing_TakeDamage.OnPawnDealtDamage += HandlePawnDealtDamage;
            LCAnomalyCore.Patch.Patch_PawnKill.OnAnyPawnKilled += HandlePawnKilled;
        }

        /// <summary>统计指定地图上当前正处于突破状态的黑森林三鸟数量（惩戒鸟 / 审判鸟 / 大鸟）。</summary>
        public static int CountBreachedForestBirds(Map map)
        {
            if (map?.mapPawns == null)
            {
                return 0;
            }

            bool hasPunishing = false;
            bool hasJudgement = false;
            bool hasBig = false;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn p = pawns[i];
                if (p == null || p.Dead || p.Destroyed || p.def == null)
                {
                    continue;
                }

                string dn = p.def.defName;
                if (dn == "PunishingBird") hasPunishing = true;
                else if (dn == "JudgementBird") hasJudgement = true;
                else if (dn == "BigBird") hasBig = true;
            }

            return (hasPunishing ? 1 : 0) + (hasJudgement ? 1 : 0) + (hasBig ? 1 : 0);
        }

        private static void HandleAbnormalityEscaped(CompAbnormality escapedComp)
        {
            Map map = escapedComp?.parent?.MapHeld;
            if (map == null || escapedComp == null)
            {
                return;
            }

            // 通知同地图所有收容于平台内的监听者（如小红帽雇佣兵）
            List<Building_AbnormalityHoldingPlatform> platforms =
                map.listerBuildings?.AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>() is IEnumerable<Building_AbnormalityHoldingPlatform> seq
                    ? new List<Building_AbnormalityHoldingPlatform>(seq)
                    : null;
            if (platforms != null)
            {
                for (int i = 0; i < platforms.Count; i++)
                {
                    Pawn held = platforms[i]?.HeldPawn;
                    if (held == null || held == escapedComp.parent || held.AllComps == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < held.AllComps.Count; j++)
                    {
                        if (held.AllComps[j] is ILCMapAbnormalityEscapeListener listener)
                        {
                            listener.NotifyOtherAbnormalityEscaped(escapedComp);
                        }
                    }
                }
            }

            // 黑森林三鸟联动前置预警：当惩戒鸟、审判鸟、大鸟同时突破时发出特殊提示
            string defName = escapedComp.parent?.def?.defName;
            if ((defName == "PunishingBird" || defName == "JudgementBird" || defName == "BigBird")
                && CountBreachedForestBirds(map) >= 3)
            {
                Messages.Message(
                    "LC_ThreeBirds_ResonanceWarning".Translate(),
                    new LookTargets(escapedComp.parent.PositionHeld, map),
                    MessageTypeDefOf.ThreatBig,
                    historical: false);
            }
        }

        private static void HandlePawnKilled(Pawn victim, DamageInfo? dinfo)
        {
            if (victim == null || !(victim.RaceProps?.Humanlike ?? false))
            {
                return;
            }

            Map map = victim.MapHeld;
            if (map == null)
            {
                return;
            }

            List<Building_AbnormalityHoldingPlatform> platforms =
                map.listerBuildings?.AllBuildingsColonistOfClass<Building_AbnormalityHoldingPlatform>() is IEnumerable<Building_AbnormalityHoldingPlatform> seq
                    ? new List<Building_AbnormalityHoldingPlatform>(seq)
                    : null;
            if (platforms == null)
            {
                return;
            }

            for (int i = 0; i < platforms.Count; i++)
            {
                Pawn held = platforms[i]?.HeldPawn;
                if (held == null || held.AllComps == null)
                {
                    continue;
                }

                for (int j = 0; j < held.AllComps.Count; j++)
                {
                    if (held.AllComps[j] is ILCMapHumanlikeDeathListener listener)
                    {
                        listener.NotifyHumanlikeDied(victim, map);
                    }
                }
            }
        }

        private static void HandleStudyStarted(Pawn studier, CompAbnormality abnormality)
        {
            if (studier?.health?.hediffSet?.hediffs == null || abnormality == null)
            {
                return;
            }

            List<Hediff> hediffs = studier.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if (!(hediffs[i] is HediffWithComps hwc) || hwc.comps == null)
                {
                    continue;
                }

                for (int j = 0; j < hwc.comps.Count; j++)
                {
                    if (hwc.comps[j] is ILCPawnStudyStartListener listener)
                    {
                        listener.OnStudyStarted(studier, abnormality);
                    }
                }
            }
        }

        private static void HandleStudyCompleted(Pawn studier, CompAbnormality abnormality, LC_StudyResult result)
        {
            if (studier?.health?.hediffSet?.hediffs == null || abnormality == null)
            {
                return;
            }

            List<Hediff> hediffs = studier.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if (!(hediffs[i] is HediffWithComps hwc) || hwc.comps == null)
                {
                    continue;
                }

                for (int j = 0; j < hwc.comps.Count; j++)
                {
                    if (hwc.comps[j] is ILCPawnStudyCompleteListener listener)
                    {
                        listener.OnStudyCompleted(studier, abnormality, result);
                    }
                }
            }
        }

        private static void HandlePawnDealtDamage(Pawn attacker, Thing target, DamageInfo dinfo, float dealt)
        {
            if (attacker?.health?.hediffSet?.hediffs == null)
            {
                return;
            }

            List<Hediff> hediffs = attacker.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if (!(hediffs[i] is HediffWithComps hwc) || hwc.comps == null)
                {
                    continue;
                }

                for (int j = 0; j < hwc.comps.Count; j++)
                {
                    if (hwc.comps[j] is ILCPawnCombatDamageListener listener)
                    {
                        listener.OnDealtDamage(attacker, target, dinfo, dealt);
                    }
                }
            }
        }
    }
}
