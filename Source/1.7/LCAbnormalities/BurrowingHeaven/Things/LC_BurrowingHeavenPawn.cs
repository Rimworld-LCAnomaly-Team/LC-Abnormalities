using BurrowingHeaven.Comp;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;
using RimWorld;

namespace BurrowingHeaven.Things
{
    public class LC_BurrowingHeavenPawn : LC_EntityBasePawn
    {
        public CompBurrowingHeaven SelfEntityComp => (CompBurrowingHeaven)EntityComp;

        public bool ShouldCheckInsight = false;
        public bool IsOutOfSight => isOutOfSight;
        protected bool isOutOfSight = false;

        public LC_BurrowingHeavenPawn()
        {
        }

        protected override void Tick()
        {
            //收容状态下丢下就出逃
            if (Spawned && CarriedBy == null && !SelfEntityComp.Escaped)
                SelfEntityComp.HoldingPlaformTargetComp.Escape();

            //检查是否在视野内
            if (ShouldCheckInsight)
                isOutOfSight = SightUtil.IsOutsideView(this, Faction.OfPlayer);

            base.Tick();
        }
    }
}
