using LCAnomalyCore.Misc;
using NothingThere.Comp;
using RimWorld;
using Verse;

namespace NothingThere.Effect
{
    /// <summary>
    /// 一无所有死亡并返回异常蛋的演出对象。
    /// </summary>
    public class DyingNothingThere : LC_FX_Standard
    {
        public override void InitWith(Pawn targetPawn)
        {
            CompNothingThere comp = targetPawn?.TryGetComp<CompNothingThere>();
            if (comp != null)
            {
                bioSignature = comp.biosignature;
            }

            if (Map != null && Position.InBounds(Map))
            {
                LCAnomalyCore.Util.AbnormalityUtility.Burst(Position, Map, 2.6f);
            }
            completeTick = TickSpawned + 60;
            hasInited = true;
        }

        public override void Complete()
        {
            if (!hasInited)
            {
                Destroy();
                return;
            }

            ThingDef filthDef = RimWorld.ThingDefOf.Filth_Blood;
            if (filthDef != null && Map != null && PositionHeld.InBounds(Map))
            {
                foreach (IntVec3 cell in CellRect.CenteredOn(PositionHeld, 1))
                {
                    if (cell.InBounds(Map))
                    {
                        FilthMaker.TryMakeFilth(cell, Map, filthDef, count: 2);
                    }
                }
            }

            if (MapHeld != null && PositionHeld.InBounds(MapHeld))
            {
                Thing egg = ThingMaker.MakeThing(Def.ThingDefOf.NothingThereEgg);
                LCAnomalyCore.Util.AbnormalityUtility.TransferSignature(egg, bioSignature);
                GenSpawn.Spawn(egg, PositionHeld, MapHeld);
            }

            Destroy();
        }
    }
}
