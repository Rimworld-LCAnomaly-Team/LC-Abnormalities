using System.Collections.Generic;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;
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

    /// <summary>
    /// 集中订阅 Core 的全局轻量静态事件并派发至持有对应 Hediff 的单位，避免每实例订阅导致的生命周期泄漏。
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
            LCAnomalyCore.Patch.Patch_Thing_TakeDamage.OnPawnDealtDamage += HandlePawnDealtDamage;
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
