using RimWorld;
using Verse;

namespace BurrowingHeaven.Def
{
    [DefOf]
    public static class DamageDefOf
    {
        public static DamageDef LC_BurrowingHeaven_BlackDamage;
        public static DamageDef LC_Heaven_RedImpale;
        public static DamageDef LC_Heaven_SkillRed;

        static DamageDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(DamageDefOf));
        }
    }
}
