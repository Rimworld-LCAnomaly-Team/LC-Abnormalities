using RimWorld;
using Verse;

namespace NothingThere.Comp
{
    public class CompProperties_AbilityMimicryGoodbye : CompProperties_AbilityEffect
    {
        public FloatRange damageRange = new FloatRange(65f, 95f);
        public float armorPenetration = 0.75f;
        public float lifestealRatio = 0.35f;

        public CompProperties_AbilityMimicryGoodbye()
        {
            compClass = typeof(CompAbilityEffect_MimicryGoodbye);
        }
    }
}
