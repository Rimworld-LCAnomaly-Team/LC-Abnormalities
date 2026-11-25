using LCAnomalyCore.Comp;
using OneSin.Comp;

namespace OneSin.Things
{
    /// <summary>
    /// 表示“一罪与百善”异常实体。
    /// </summary>
    public class LC_OneSinPawn : LC_EntityBasePawn
    {
        protected override void Tick()
        {
            CompAbnormalityHoldingPlatformTarget holdTarget = GetComp<CompAbnormalityHoldingPlatformTarget>();
            if (holdTarget != null && holdTarget.CurrentlyContainedInRoom)
            {
                TickHolded();
                base.Tick();
                return;
            }

            // 收容状态下脱离收容平台落地即消散化蛋
            if (Spawned && CarriedBy == null)
            {
                GetComp<CompOneSin>()?.Notify_Escaped();
                return;
            }

            base.Tick();
        }

        /// <inheritdoc />
        public override void TickHolded()
        {
            base.TickHolded();
            if (Drawer?.renderer != null && Drawer.renderer.CurAnimation != Def.AnimationDefOf.OneSin_Idle)
            {
                Drawer.renderer.SetAnimation(Def.AnimationDefOf.OneSin_Idle);
            }
        }
    }
}
