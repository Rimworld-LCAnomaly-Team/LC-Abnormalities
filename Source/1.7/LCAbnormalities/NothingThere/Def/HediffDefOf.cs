using RimWorld;
using Verse;

namespace NothingThere.Def
{
    [DefOf]
    public static class HediffDefOf
    {
        public static HediffDef LC_NothingThere_Form1Buff;
        public static HediffDef LC_NothingThere_TrueFormBuff;
        public static HediffDef LC_MimicrySuitRegen;

        static HediffDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HediffDefOf));
        }
    }
}
