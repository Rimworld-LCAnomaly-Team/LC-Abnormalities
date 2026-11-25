using LC_SevenSin.Comp;
using LCAnomalyCore.Comp;

namespace LC_SevenSin.Things
{
    public class LC_SevenSinPawn : LC_EntityBasePawn
    {
        protected override void Tick()
        {
            if (IsContainedInRoom)
            {
                TickHolded();
                base.Tick();
                return;
            }

            if (Spawned && CarriedBy == null && EntityComp != null && !EntityComp.Escaped)
            {
                GetComp<CompSevenSinEntity>()?.Notify_Escaped();
            }

            base.Tick();
        }
    }
}
