using System.Collections.Generic;
using NothingThere.Comp;
using NothingThere.Things;
using RimWorld;
using Verse;
using Verse.AI;

namespace NothingThere.Job
{
    /// <summary>
    /// 一无所有专属无情追击节点：支持无初始视线全图索敌、自动破门破栏，不会因断臂或障碍遮挡而原地卡死。
    /// </summary>
    public class JobGiver_NothingThereHunt : ThinkNode_JobGiver
    {
        public float targetAcquireRadius = 9999f;
        public bool allowTurrets = true;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            JobGiver_NothingThereHunt copy = (JobGiver_NothingThereHunt)base.DeepCopy(resolve);
            copy.targetAcquireRadius = targetAcquireRadius;
            copy.allowTurrets = allowTurrets;
            return copy;
        }

        protected override Verse.AI.Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Map == null)
            {
                return null;
            }

            CompNothingThere comp = (pawn as LC_NothingTherePawn)?.SelfEntityComp
                ?? pawn.TryGetComp<CompNothingThere>();
            if (comp == null
                || !comp.Escaped
                || comp.CurrentForm == NothingThereForm.Form2_Cocoon
                || comp.IsPerformingGoodbye
                || comp.IsThrowingBoneSpike)
            {
                return null;
            }

            if (pawn.TryGetAttackVerb(null, true, allowTurrets) == null)
            {
                return null;
            }

            Thing target = FindBestHuntTarget(pawn);
            if (target == null)
            {
                pawn.mindState.enemyTarget = null;
                return null;
            }

            pawn.mindState.enemyTarget = target;

            if (!pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly, canBashDoors: false, canBashFences: true))
            {
                using (PawnPath path = pawn.Map.pathFinder.FindPathNow(
                    pawn.Position,
                    target.Position,
                    TraverseParms.For(pawn, Danger.Deadly, TraverseMode.PassDoors, canBashDoors: true, alwaysUseAvoidGrid: false, canBashFences: true)))
                {
                    if (path.Found)
                    {
                        Thing blocker = path.FirstBlockingBuilding(out _, pawn);
                        if (blocker != null
                            && !blocker.Destroyed
                            && blocker.Spawned
                            && pawn.CanReach(blocker, PathEndMode.Touch, Danger.Deadly, canBashDoors: false, canBashFences: true))
                        {
                            Verse.AI.Job bashJob = JobMaker.MakeJob(JobDefOf.AttackMelee, blocker);
                            bashJob.maxNumMeleeAttacks = 4;
                            bashJob.expiryInterval = 240;
                            bashJob.checkOverrideOnExpire = true;
                            bashJob.canBashDoors = true;
                            bashJob.canBashFences = true;
                            return bashJob;
                        }
                    }
                }
            }

            Verse.AI.Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            job.expiryInterval = 240;
            job.checkOverrideOnExpire = true;
            job.expireRequiresEnemiesNearby = false;
            job.canBashDoors = true;
            job.canBashFences = true;
            job.attackDoorIfTargetLost = true;
            return job;
        }

        private Thing FindBestHuntTarget(Pawn pawn)
        {
            Thing primary = (Thing)AttackTargetFinder.BestAttackTarget(
                pawn,
                TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                t => IsValidHuntTarget(pawn, t),
                0f,
                targetAcquireRadius,
                default(IntVec3),
                float.MaxValue,
                canBashDoors: true,
                canTakeTargetsCloserThanEffectiveMinRange: true,
                canBashFences: true);

            if (primary != null)
            {
                return primary;
            }

            // 兜底：当场上仅剩非战斗单位（如逃亡工友或幼畜）时，继续锁定最近的存活敌对单位
            float bestDistSq = float.MaxValue;
            Pawn bestFallback = null;
            IReadOnlyList<Pawn> allPawns = pawn.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawns.Count; i++)
            {
                Pawn c = allPawns[i];
                if (!IsValidHostilePawn(pawn, c))
                {
                    continue;
                }

                float distSq = c.Position.DistanceToSquared(pawn.Position);
                if (distSq < bestDistSq
                    && distSq <= targetAcquireRadius * targetAcquireRadius
                    && pawn.CanReach(c, PathEndMode.Touch, Danger.Deadly, canBashDoors: true, canBashFences: true))
                {
                    bestDistSq = distSq;
                    bestFallback = c;
                }
            }

            return bestFallback;
        }

        private bool IsValidHuntTarget(Pawn searcher, Thing t)
        {
            if (t == null || t == searcher || !t.Spawned || t.Destroyed)
            {
                return false;
            }

            if (t is Pawn p)
            {
                return IsValidHostilePawn(searcher, p);
            }

            if (allowTurrets && t is Building_Turret turret)
            {
                return searcher.HostileTo(turret);
            }

            return false;
        }

        private static bool IsValidHostilePawn(Pawn searcher, Pawn candidate)
        {
            return candidate != null
                && candidate != searcher
                && candidate.Spawned
                && !candidate.Dead
                && !candidate.Downed
                && !candidate.IsPsychologicallyInvisible()
                && searcher.HostileTo(candidate);
        }
    }
}
