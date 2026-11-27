using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;

namespace Laetitia.Comp
{
    public class CompProperties_Laetitia : CompProperties_Abnormality
    {
        public CompProperties_Laetitia()
        {
            compClass = typeof(CompLaetitia);
        }

        public override bool TryGetWorkPreferenceRates(EAnomalyWorkType workType, out float[] ratesByLevel)
        {
            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                    ratesByLevel = new[] { 0.40f, 0.45f, 0.50f, 0.50f, 0.50f };
                    return true;
                case EAnomalyWorkType.Insight:
                    ratesByLevel = new[] { 0.40f, 0.40f, 0.40f, 0.40f, 0.40f };
                    return true;
                case EAnomalyWorkType.Attachment:
                    ratesByLevel = new[] { 0.60f, 0.60f, 0.60f, 0.65f, 0.65f };
                    return true;
                case EAnomalyWorkType.Repression:
                    ratesByLevel = new[] { 0.00f, 0.00f, 0.00f, 0.00f, 0.00f };
                    return true;
                default:
                    ratesByLevel = null;
                    return false;
            }
        }
    }
}
