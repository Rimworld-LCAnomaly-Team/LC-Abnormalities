using LCAnomalyCore.Defs;
using RimWorld;
using Verse;

namespace Laetitia.Def
{
    [DefOf]
    public class ThingDefOf
    {
        public static ThingDef_AbnormalityEntity_Spawn DyingLaetitia;

        public static ThingDef SpawningLaetitiaMonster;

        public static ThingDef DyingLaetitiaMonster;

        public static ThingDef LaetitiaEgg;
        public static ThingDef LC_LaetitiaHeartBurstFX;
        public static ThingDef LC_LaetitiaGiftChargeFX;
        public static ThingDef LC_LaetitiaRibbonTracerFX;
        public static ThingDef LC_LaetitiaSkillRunnerFX;

        static ThingDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ThingDefOf));
        }
    }
}
