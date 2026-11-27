using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;

namespace MeatLantern.Comp
{
    public class CompProperties_MeatLantern : CompProperties_Abnormality
    {
        public int eatCooldownTick = 600;

        public CompProperties_MeatLantern()
        {
            compClass = typeof(CompMeatLantern);
        }

        public override bool TryGetWorkPreferenceRates(EAnomalyWorkType workType, out float[] ratesByLevel)
        {
            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                    ratesByLevel = new[] { 0.45f, 0.45f, 0.50f, 0.55f, 0.55f };
                    return true;
                case EAnomalyWorkType.Insight:
                    ratesByLevel = new[] { 0.60f, 0.60f, 0.60f, 0.60f, 0.60f };
                    return true;
                case EAnomalyWorkType.Attachment:
                    ratesByLevel = new[] { 0.45f, 0.45f, 0.45f, 0.45f, 0.45f };
                    return true;
                case EAnomalyWorkType.Repression:
                    ratesByLevel = new[] { 0.30f, 0.30f, 0.30f, 0.30f, 0.30f };
                    return true;
                default:
                    ratesByLevel = null;
                    return false;
            }
        }
    }
}
