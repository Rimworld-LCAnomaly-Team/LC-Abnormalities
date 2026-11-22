using RimWorld;
using Verse;

namespace NothingThere.Def
{
    [DefOf]
    public static class DamageDefOf
    {
        public static DamageDef LC_NothingThere_RedSlash;
        public static DamageDef LC_NothingThere_BoneSpike;
        public static DamageDef LC_Mimicry_RedLifesteal;

        static DamageDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DamageDefOf));
        }
    }
}
