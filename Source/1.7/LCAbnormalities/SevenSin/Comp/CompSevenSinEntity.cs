using System.Collections.Generic;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;
using UnityEngine;
using Verse;

namespace LC_SevenSin.Comp
{
    /// <summary>大罪罪种核心组件：支持按罪种区分工作成功率加成与可选的轻量自愈。</summary>
    public class CompSevenSinEntity : CompAbnormality
    {
        public new CompProperties_SevenSinEntity Props => (CompProperties_SevenSinEntity)props;

        protected override float StudySuccessRateCalculate(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            if (studier == null)
            {
                return 0f;
            }

            float baseRate = base.StudySuccessRateCalculate(studier, workType);
            float workTypeRate;

            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                    workTypeRate = Props.instinctBonus;
                    break;
                case EAnomalyWorkType.Insight:
                    workTypeRate = Props.insightBonus;
                    break;
                case EAnomalyWorkType.Attachment:
                    workTypeRate = Props.attachmentBonus;
                    break;
                case EAnomalyWorkType.Repression:
                    workTypeRate = Props.repressionBonus;
                    break;
                default:
                    workTypeRate = 0.70f;
                    break;
            }

            return Mathf.Clamp(baseRate + workTypeRate, 0.05f, 0.95f);
        }

        public override void CompTickRare()
        {
            base.CompTickRare();

            if (Props.passiveHealPerRareTick <= 0f || !(parent is Pawn pawn) || pawn.Dead || !pawn.Spawned)
            {
                return;
            }

            List<Hediff> hediffs = pawn.health?.hediffSet?.hediffs;
            if (hediffs == null)
            {
                return;
            }

            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Injury injury && (injury.TendableNow() || injury.CanHealNaturally()))
                {
                    injury.Heal(Props.passiveHealPerRareTick);
                    break;
                }
            }
        }
    }
}
