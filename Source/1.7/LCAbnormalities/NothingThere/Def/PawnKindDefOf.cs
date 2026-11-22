using RimWorld;
using Verse;

namespace NothingThere.Def
{
    [DefOf]
    public static class PawnKindDefOf
    {
        public static PawnKindDef NothingThere;

        static PawnKindDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PawnKindDefOf));
        }
    }
}
