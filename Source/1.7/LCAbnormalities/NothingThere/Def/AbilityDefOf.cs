using RimWorld;
using Verse;

namespace NothingThere.Def
{
    [DefOf]
    public static class AbilityDefOf
    {
        public static AbilityDef Mimicry_Goodbye;

        static AbilityDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AbilityDefOf));
        }
    }
}
