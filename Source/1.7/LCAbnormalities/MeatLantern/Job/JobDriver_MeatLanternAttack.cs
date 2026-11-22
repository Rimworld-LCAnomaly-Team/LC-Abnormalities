using MeatLantern.Comp;
using MeatLantern.Utility;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace MeatLantern.Job
{
    /// <summary>
    /// 攻击状态
    /// </summary>
    public class JobDriver_MeatLanternAttack : JobDriver
    {
        private CompMeatLantern Comp => pawn.TryGetComp<CompMeatLantern>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            AddFinishAction(delegate
            {
                if (Comp != null)
                {
                    Comp.meatLanternState = MeatLanternState.Wait;
                }
            });

            yield return Toils_General.Do(delegate
            {
                if (Comp == null || pawn.Map == null)
                {
                    return;
                }

                if (pawn.mindState?.enemyTarget != null)
                {
                    List<Pawn> victims = MeatLanternUtility.ScanForTarget_NewTemp(pawn, 2.2f);
                    if (victims != null && victims.Count > 0)
                    {
                        Comp.Eat(victims);
                    }
                }

                Comp.meatLanternState = MeatLanternState.Wait;
            });
        }

        public override bool IsContinuation(Verse.AI.Job j)
        {
            return job.GetTarget(TargetIndex.A) == j.GetTarget(TargetIndex.A);
        }
    }
}