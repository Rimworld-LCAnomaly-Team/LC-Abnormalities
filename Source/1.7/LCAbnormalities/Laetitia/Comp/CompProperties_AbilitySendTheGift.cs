using RimWorld;
using Verse;

namespace Laetitia.Comp
{
    public class CompProperties_AbilitySendTheGift : CompProperties_AbilityEffect
    {
        public FloatRange beat1DamageRange = new FloatRange(14f, 19f);
        public FloatRange beat2DamageRange = new FloatRange(18f, 24f);

        public CompProperties_AbilitySendTheGift()
        {
            compClass = typeof(CompAbilityEffect_SendTheGift);
        }
    }
}
