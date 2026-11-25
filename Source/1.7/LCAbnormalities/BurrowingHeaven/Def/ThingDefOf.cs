using RimWorld;
using Verse;

namespace BurrowingHeaven.Def
{
    [DefOf]
    public static class ThingDefOf
    {
        public static ThingDef DyingBurrowingHeaven;

        public static ThingDef BurrowingHeavenEgg;

        public static ThingDef BurrowingHeaven_KillTree;
        public static ThingDef LC_HeavenThrustFX;
        public static ThingDef LC_HeavenChargeAuraFX;
        public static ThingDef LC_HeavenSkillRunnerFX;

        static ThingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ThingDefOf));
        }
    }
}
