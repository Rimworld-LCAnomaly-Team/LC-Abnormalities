using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;

namespace LC_SevenSin.Comp
{
    public class CompProperties_SevenSinEntity : CompProperties_Abnormality
    {
        public float instinctBonus = 0.70f;
        public float insightBonus = 0.70f;
        public float attachmentBonus = 0.70f;
        public float repressionBonus = 0.70f;
        public float passiveHealPerRareTick = 0f;

        public CompProperties_SevenSinEntity()
        {
            compClass = typeof(CompSevenSinEntity);
        }

        public override bool TryGetWorkPreferenceRates(EAnomalyWorkType workType, out float[] ratesByLevel)
        {
            float val;
            switch (workType)
            {
                case EAnomalyWorkType.Instinct: val = instinctBonus; break;
                case EAnomalyWorkType.Insight: val = insightBonus; break;
                case EAnomalyWorkType.Attachment: val = attachmentBonus; break;
                case EAnomalyWorkType.Repression: val = repressionBonus; break;
                default:
                    ratesByLevel = null;
                    return false;
            }

            ratesByLevel = new[] { val, val, val, val, val };
            return true;
        }
    }
}
