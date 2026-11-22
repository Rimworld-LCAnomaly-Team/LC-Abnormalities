using BurrowingHeaven.Things;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;
using LCAnomalyCore.Comp.Pawns;
using UnityEngine;
using RimWorld;
using Verse;

namespace BurrowingHeaven.Comp
{
    public class CompBurrowingHeaven : CompAbnormality
    {
        #region 变量

        public new CompProperties_BurrowingHeaven Props => (CompProperties_BurrowingHeaven)props;

        public LC_BurrowingHeavenPawn SelfPawn => (LC_BurrowingHeavenPawn)parent;

        #endregion 变量

        #region 触发事件

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            Util.EffectUtil.OnBurrowingHeavenDeath((Pawn)parent, prevMap);
        }

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();

            SelfPawn.ShouldCheckInsight = true;
        }

        /// <summary>
        /// 绑到收容平台上后的操作
        /// </summary>
        public override void Notify_Holded()
        {
            CheckIsDiscovered();
        }

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            base.Notify_Studied(studier, interrupted);
            SelfPawn.ShouldCheckInsight = false;
        }

        public override void Notify_Studying(Pawn studier)
        {
            Log.Warning("穿刺乐园正在被工作，请注意");
            SelfPawn.ShouldCheckInsight = true;
        }

        #endregion 触发事件

        #region 研究与图鉴

        protected override float StudySuccessRateCalculate(CompPawnStatus employee, EAnomalyWorkType workType)
        {
            //每级智力提供2%成功率，20级智力提供40%成功率
            Pawn studier = (Pawn)employee.parent;
            if (studier.skills == null || studier.skills.GetSkill(SkillDefOf.Intellectual).Level < 10) return 0f;
            float successRate_Intellectual = studier.skills.GetSkill(SkillDefOf.Intellectual).Level * 0.02f;
            //叠加基础成功率，此处是20%，叠加完应是60%
            float finalSuccessRate = successRate_Intellectual + Props.studySucessRateBase;

            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                    finalSuccessRate -= 0.2f;
                    break;

                case EAnomalyWorkType.Insight:
                    finalSuccessRate += 0.1f;
                    break;

                case EAnomalyWorkType.Attachment:
                    finalSuccessRate += 0.2f;
                    break;

                case EAnomalyWorkType.Repression:
                    finalSuccessRate += 0.1f;
                    break;
            }

            //成功率不能超过80%
            if (finalSuccessRate >= 1f)
                finalSuccessRate = 0.8f;

            return Mathf.Clamp(finalSuccessRate, 0f, 0.8f);
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
    }
}
