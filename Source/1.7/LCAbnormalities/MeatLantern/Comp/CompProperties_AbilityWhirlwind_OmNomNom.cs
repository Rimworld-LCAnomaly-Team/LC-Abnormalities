using RimWorld;
using System.Collections.Generic;
using Verse;

namespace MeatLantern.Comp
{
    public class CompProperties_AbilityWhirlwindOmNomNom : CompProperties_AbilityEffect
    {
        public FloatRange damageRange;
        public float armorPenetration;
        public float suckPercent;
        public float stunPercent;

        public CompProperties_AbilityWhirlwindOmNomNom()
        {
            compClass = typeof(CompAbilityEffect_WhirlwindOmNomNom);
        }

        public override IEnumerable<string> ExtraStatSummary()
        {
            yield return "Damage".Translate() + ": " + $"{damageRange.min:0.#} ~ {damageRange.max:0.#}";
            yield return "ArmorPenetration".Translate() + ": " + $"{armorPenetration * 100f:0.#}%";
            yield return "SuckAmount".Translate() + ": " + $"{damageRange.min * suckPercent:0.#} ~ {damageRange.max * suckPercent:0.#}";
            yield return "StunTime".Translate() + ": " + $"{damageRange.min * stunPercent * 0.5f:0.#} ~ {damageRange.max * stunPercent * 0.5f:0.#}s";
        }
    }
}