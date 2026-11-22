using RimWorld;
using Verse;

namespace BurrowingHeaven.Def
{
    [DefOf]
    public static class SoundDefOf
    {
        public static SoundDef BurrowingHeaven_attack;
        public static SoundDef BurrowingHeaven_scream;
        public static SoundDef BurrowingHeaven_Death;

        static SoundDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SoundDefOf));
        }
    }
}
