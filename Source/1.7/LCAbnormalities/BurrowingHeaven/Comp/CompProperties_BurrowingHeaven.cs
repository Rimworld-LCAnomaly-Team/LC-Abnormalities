using LCAnomalyCore.Comp;

namespace BurrowingHeaven.Comp
{
    public class CompProperties_BurrowingHeaven : CompProperties_Abnormality
    {
        public int workUnmonitoredGraceTicks = 90;
        public int workUnmonitoredIntervalTicks = 180;
        public float workMeltdownStrikeDamage = 35f;

        public int breachWatchedAttackIntervalTicks = 150;
        public float breachWatchedAttackRadius = 4.5f;
        public float breachWatchedAttackDamage = 16f;

        public int breachUnwatchedGraceTicks = 60;
        public int breachUnwatchedBurstIntervalTicks = 120;
        public float breachUnwatchedBaseRadius = 18f;
        public float breachUnwatchedRadiusStep = 8f;
        public float breachUnwatchedMaxRadius = 42f;
        public float breachUnwatchedBurstDamage = 28f;
        public int breachUnwatchedMaxTargets = 6;
        public float breachUnwatchedHealAmount = 12f;

        public CompProperties_BurrowingHeaven()
        {
            compClass = typeof(CompBurrowingHeaven);
        }
    }
}
