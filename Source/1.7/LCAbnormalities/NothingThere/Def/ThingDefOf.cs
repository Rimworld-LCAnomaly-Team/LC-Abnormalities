using RimWorld;
using Verse;

namespace NothingThere.Def
{
    [DefOf]
    public static class ThingDefOf
    {
        public static ThingDef NothingThere;
        public static ThingDef NothingThereEgg;
        public static ThingDef DyingNothingThere;
        public static ThingDef NothingThere_BoneSpikeProj;
        public static ThingDef EgoWeapon_NothingThere;
        public static ThingDef Apparel_NothingThereSuit;
        public static ThingDef Accessory_NothingThere;
        public static ThingDef LC_MimicrySlashFX;
        public static ThingDef LC_MimicryGroundRuptureFX;
        public static ThingDef LC_MimicryChargeAuraFX;

        static ThingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ThingDefOf));
        }
    }
}
