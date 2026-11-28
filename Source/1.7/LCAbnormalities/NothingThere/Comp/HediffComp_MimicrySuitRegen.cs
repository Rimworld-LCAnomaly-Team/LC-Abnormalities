using NothingThere.Util;
using RimWorld;
using Verse;

namespace NothingThere.Comp
{
    public class HediffCompProperties_MimicrySuitRegen : HediffCompProperties
    {
        public int intervalTicks = 180;
        public float healAmount = 1.2f;

        public HediffCompProperties_MimicrySuitRegen()
        {
            compClass = typeof(HediffComp_MimicrySuitRegen);
        }
    }

    /// <summary>拟态护甲周期性细胞再生恢复效果。</summary>
    public class HediffComp_MimicrySuitRegen : HediffComp
    {
        public HediffCompProperties_MimicrySuitRegen Props => (HediffCompProperties_MimicrySuitRegen)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || !pawn.IsHashIntervalTick(Props.intervalTicks))
            {
                return;
            }

            if (!IsWearingMimicrySuit(pawn))
            {
                pawn.health?.RemoveHediff(parent);
                return;
            }

            EffectUtil.HealPawnInjuries(pawn, Props.healAmount, showFeedback: false);
        }

        private static bool IsWearingMimicrySuit(Pawn pawn)
        {
            if (pawn?.apparel?.WornApparel == null)
            {
                return false;
            }

            for (int i = 0; i < pawn.apparel.WornApparel.Count; i++)
            {
                Apparel ap = pawn.apparel.WornApparel[i];
                if (ap != null && (ap.def == global::NothingThere.Def.ThingDefOf.Apparel_NothingThereSuit || ap.TryGetComp<CompMimicrySuit>() != null))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
