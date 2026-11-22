using RimWorld;
using Verse;

namespace BurrowingHeaven.Def
{
    [DefOf]
    public static class DamageDefOf
    {
        public static DamageDef LC_BurrowingHeaven_BlackDamage;

        static DamageDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DamageDefOf));
        }
    }
}
