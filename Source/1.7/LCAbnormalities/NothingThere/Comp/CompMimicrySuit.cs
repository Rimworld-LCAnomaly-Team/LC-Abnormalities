using Verse;

namespace NothingThere.Comp
{
    /// <summary>
    /// 拟态 E.G.O 护甲组件：穿戴时赋予生命再生 Hediff，卸下或摧毁时移除。
    /// </summary>
    public class CompMimicrySuit : ThingComp
    {
        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null || Def.HediffDefOf.LC_MimicrySuitRegen == null)
            {
                return;
            }

            if (!pawn.health.hediffSet.HasHediff(Def.HediffDefOf.LC_MimicrySuitRegen))
            {
                pawn.health.AddHediff(Def.HediffDefOf.LC_MimicrySuitRegen);
            }
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            if (pawn?.health?.hediffSet == null || Def.HediffDefOf.LC_MimicrySuitRegen == null)
            {
                return;
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(Def.HediffDefOf.LC_MimicrySuitRegen);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
            }
        }
    }
}
