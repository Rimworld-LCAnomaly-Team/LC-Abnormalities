using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;

namespace BurrowingHeaven.Comp
{
    public class CompProperties_BurrowingHeaven : CompProperties_Abnormality
    {
        public int workUnmonitoredGraceTicks = 90;
        public int workUnmonitoredIntervalTicks = 180;
        public float workMeltdownStrikeDamage = 35f;

        public bool breachAttackWhileWatched = false;
        public int breachWatchedAttackIntervalTicks = 150;
        public float breachWatchedAttackRadius = 4.5f;
        public float breachWatchedAttackDamage = 16f;

        public bool breachTeleportOnUnwatched = true;
        public int breachUnwatchedGraceTicks = 90;
        public int breachUnwatchedBurstIntervalTicks = 180;
        public float breachUnwatchedOutdoorRadius = 18f;
        public float breachUnwatchedBurstDamage = 28f;
        public float breachUnwatchedHealAmount = 12f;

        // Legacy fields preserved for third-party XML compatibility
        public float breachUnwatchedBaseRadius = 18f;
        public float breachUnwatchedRadiusStep = 8f;
        public float breachUnwatchedMaxRadius = 42f;
        public int breachUnwatchedMaxTargets = 6;

        public CompProperties_BurrowingHeaven()
        {
            compClass = typeof(CompBurrowingHeaven);
        }

        public override bool TryGetWorkPreferenceRates(EAnomalyWorkType workType, out float[] ratesByLevel)
        {
            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                    ratesByLevel = new[] { 0.00f, 0.00f, 0.00f, 0.00f, 0.00f };
                    return true;
                case EAnomalyWorkType.Insight:
                    ratesByLevel = new[] { 0.30f, 0.40f, 0.40f, 0.50f, 0.50f };
                    return true;
                case EAnomalyWorkType.Attachment:
                    ratesByLevel = new[] { 0.40f, 0.40f, 0.40f, 0.30f, 0.20f };
                    return true;
                case EAnomalyWorkType.Repression:
                    ratesByLevel = new[] { 0.40f, 0.45f, 0.50f, 0.55f, 0.60f };
                    return true;
                default:
                    ratesByLevel = null;
                    return false;
            }
        }
    }
}
