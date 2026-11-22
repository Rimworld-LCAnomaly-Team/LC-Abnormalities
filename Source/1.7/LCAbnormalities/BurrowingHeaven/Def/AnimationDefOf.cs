using RimWorld;
using Verse;

namespace BurrowingHeaven.Def
{
    [DefOf]
    public static class AnimationDefOf
    {
        public static AnimationDef BurrowingHeaven_Idle;
        public static AnimationDef BurrowingHeaven_Work;
        public static AnimationDef BurrowingHeaven_Attack;

        static AnimationDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AnimationDefOf));
        }
    }
}
