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

        static ThingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ThingDefOf));
        }
    }
}
