using RimWorld;
using Verse;

namespace PortraitOfAnotherWorld.Def
{
    [DefOf]
    public static class HediffDefOf
    {
        public static HediffDef LC_PortraitProtected;
        public static HediffDef LC_PortraitSubstitute;

        static HediffDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HediffDefOf));
        }
    }
}
