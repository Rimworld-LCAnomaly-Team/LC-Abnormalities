using MeatLantern.Comp;
using MeatLantern.Utility;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace MeatLantern.Job
{
    /// <summary>
    /// 等待/诱捕状态
    /// </summary>
    public class JobDriver_MeatLanternWait : JobDriver
    {
        private const float AmbushScanRadius = 1.8f;

        private CompMeatLantern Comp => pawn.TryGetComp<CompMeatLantern>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil toil = ToilMaker.MakeToil("MakeNewToils");
            toil.tickAction = delegate
            {
                if (Comp == null || pawn.Map == null || !pawn.IsHashIntervalTick(15))
                {
                    return;
                }

                if (Find.TickManager.TicksGame >= Comp.nextEat)
                {
                    Pawn target = MeatLanternUtility.GetClosestTargetInRadius(base.pawn, AmbushScanRadius);
                    if (target != null)
                    {
                        base.pawn.mindState.enemyTarget = target;
                        Comp.meatLanternState = MeatLanternState.Attack;
                        EndJobWith(JobCondition.InterruptForced);
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return toil;
        }
    }
}