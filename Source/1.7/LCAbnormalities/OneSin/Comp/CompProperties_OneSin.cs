using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;

namespace OneSin.Comp
{
    /// <summary>
    /// 定义“一罪与百善”异常实体组件的配置。
    /// </summary>
    public class CompProperties_OneSin : CompProperties_Abnormality
    {
        /// <summary>
        /// 初始化组件类型。
        /// </summary>
        public CompProperties_OneSin()
        {
            compClass = typeof(CompOneSin);
        }

        public override bool TryGetWorkPreferenceRates(EAnomalyWorkType workType, out float[] ratesByLevel)
        {
            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                case EAnomalyWorkType.Repression:
                    ratesByLevel = new[] { 0.50f, 0.40f, 0.30f, 0.30f, 0.30f };
                    return true;
                case EAnomalyWorkType.Insight:
                    ratesByLevel = new[] { 0.70f, 0.70f, 0.50f, 0.50f, 0.50f };
                    return true;
                case EAnomalyWorkType.Attachment:
                    ratesByLevel = new[] { 0.70f, 0.70f, 0.70f, 0.70f, 0.70f };
                    return true;
                default:
                    ratesByLevel = null;
                    return false;
            }
        }
    }
}
