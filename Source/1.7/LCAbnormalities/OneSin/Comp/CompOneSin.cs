using LCAnomalyCore.Comp;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace OneSin.Comp
{
    /// <summary>
    /// 提供“一罪与百善”的死亡、出逃、精神祝福与工作成功率逻辑。
    /// </summary>
    public class CompOneSin : CompAbnormality
    {
        #region 触发事件

        /// <inheritdoc />
        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            Util.EffectUtil.OnOneSinDeath((Pawn)parent, prevMap);
        }

        /// <inheritdoc />
        public override void Notify_Escaped()
        {
            parent.Kill();
        }

        /// <inheritdoc />
        public override void Notify_Holded()
        {
            base.Notify_Holded();
            if (parent != null)
            {
                AbnormalityUtility.Discover(parent.def);
            }
        }

        /// <inheritdoc />
        protected override void StudyEvent_Good(Pawn studier)
        {
            bool producedMaxPeBox = PeBoxComp != null && PeBoxProducedTemp >= PeBoxComp.Props.amountProdueMax;

            base.StudyEvent_Good(studier);

            if (producedMaxPeBox)
            {
                List<Pawn> colonists = parent.MapHeld?.mapPawns?.FreeColonistsSpawned;
                if (colonists != null)
                {
                    for (int i = colonists.Count - 1; i >= 0; i--)
                    {
                        RestoreMentalHealth(colonists[i], 0.6f, 0.12f);
                    }
                }

                Messages.Message(
                    "LC_OneSin_MaxPeBoxBlessing".Translate(studier?.LabelShortCap ?? parent.LabelCap),
                    new LookTargets(parent.PositionHeld, parent.MapHeld),
                    MessageTypeDefOf.PositiveEvent,
                    historical: false);
            }
            else if (studier != null && !studier.Dead)
            {
                RestoreMentalHealth(studier, 0.5f, 0.10f);
                Messages.Message(
                    "LC_OneSin_GoodWorkBlessing".Translate(studier.LabelShortCap),
                    studier,
                    MessageTypeDefOf.PositiveEvent,
                    historical: false);
            }
        }

        private static void RestoreMentalHealth(Pawn pawn, float spFractionRestore, float moodBoost)
        {
            if (pawn == null || pawn.Dead)
                return;

            Hediff mentalDamage = pawn.health?.hediffSet?.GetFirstHediffOfDef(LCAnomalyCore.Defs.HediffDefOf.LC_MentalDamage);
            if (mentalDamage != null)
            {
                mentalDamage.Severity -= spFractionRestore;
                if (mentalDamage.Severity <= 0.0001f)
                {
                    pawn.health.RemoveHediff(mentalDamage);
                    if (pawn.InMentalState && pawn.MentalState != null)
                    {
                        pawn.MentalState.RecoverFromState();
                    }
                }
            }

            Need_Mood mood = pawn.needs?.mood;
            if (mood != null)
            {
                mood.CurLevel = Mathf.Clamp01(mood.CurLevel + moodBoost);
            }

            if (pawn.Spawned && pawn.Map != null && pawn.Position.InBounds(pawn.Map))
            {
                FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.HealingCross, 0.42f);
            }
        }

        #endregion 触发事件

        #region 研究与图鉴

        /// <inheritdoc />
        protected override float StudySuccessRateCalculate(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            float baseRate = base.StudySuccessRateCalculate(studier, workType);
            float workTypeRate = 0;

            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                    //本能：I级50%，II级40%，别的30%
                    switch (studier.GetPawnStatusELevel(EPawnStatus.Fortitude))
                    {
                        case EPawnLevel.I:
                            workTypeRate = 0.5f;
                            break;
                        case EPawnLevel.II:
                            workTypeRate = 0.4f;
                            break;
                        default:
                            workTypeRate = 0.3f;
                            break;
                    }
                    break;
                case EAnomalyWorkType.Insight:
                    //洞察：I和II级70%，别的50%
                    switch (studier.GetPawnStatusELevel(EPawnStatus.Prudence))
                    {
                        case EPawnLevel.I:
                        case EPawnLevel.II:
                            workTypeRate = 0.7f;
                            break;
                        default:
                            workTypeRate = 0.5f;
                            break;
                    }
                    break;
                case EAnomalyWorkType.Attachment:
                    //沟通：70%
                    switch (studier.GetPawnStatusELevel(EPawnStatus.Temperance))
                    {
                        default:
                            workTypeRate = 0.7f;
                            break;
                    }
                    break;
                case EAnomalyWorkType.Repression:
                    //压迫：I级50%，II级40%，别的30%
                    switch (studier.GetPawnStatusELevel(EPawnStatus.Justice))
                    {
                        case EPawnLevel.I:
                            workTypeRate = 0.5f;
                            break;
                        case EPawnLevel.II:
                            workTypeRate = 0.4f;
                            break;
                        default:
                            workTypeRate = 0.3f;
                            break;
                    }
                    break;
            }

            float finalRate = baseRate + workTypeRate;

            //成功率不能超过95%
            if (finalRate > 0.95f)
                finalRate = 0.95f;

            return finalRate;
        }

        ///// <summary>
        ///// 检查是否已在图鉴中被解锁
        ///// </summary>
        //private void CheckIsDiscovered()
        //{
        //    if (AnomalyUtility.ShouldNotifyCodex((Pawn)parent, EntityDiscoveryType.BecameVisible, out var entries))
        //    {
        //        Find.EntityCodex.SetDiscovered(entries, Def.PawnKindDefOf.OneSin.race, (Pawn)parent);
        //    }
        //}

        #endregion 研究与图鉴
    }
}
