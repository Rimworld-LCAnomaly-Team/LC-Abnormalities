using Laetitia.Things;
using RimWorld;
using Verse;
using Verse.AI;

namespace Laetitia.Job
{
    public class JobGiver_LaetitiaCastAbility : ThinkNode_JobGiver
    {
        protected Verse.AI.Job MeleeAttack(Pawn user)
        {
            if (user is LC_LaetitiaMonsterPawn monster && monster.IsBusyForAI)
            {
                return null;
            }

            Ability ability = user.abilities?.GetAbility(Def.AbilityDefOf.LaetitiaMonster_Attack, false);
            if (ability == null || !ability.CanCast || ability.Casting)
            {
                return null;
            }

            Pawn target = FindAttackTarget(user, ability);
            if (target == null)
            {
                return null;
            }

            return ability.GetJob(target, target);
        }

        private Pawn FindAttackTarget(Pawn user, Ability ability)
        {
            Pawn meleeThreat = user.mindState?.meleeThreat;
            if (IsValidTarget(user, meleeThreat, ability))
            {
                return meleeThreat;
            }

            Pawn enemyTarget = user.mindState?.enemyTarget as Pawn;
            if (IsValidTarget(user, enemyTarget, ability)
                && enemyTarget.Position.InHorDistOf(user.Position, 65f)
                && user.CanReach(enemyTarget, PathEndMode.Touch, Danger.Deadly))
            {
                return enemyTarget;
            }

            return AttackTargetFinder.BestAttackTarget(
                user,
                TargetScanFlags.NeedThreat | TargetScanFlags.NeedReachable,
                t => t is Pawn p && !p.Dead && !p.Downed && ability.CanApplyOn(new LocalTargetInfo(p)),
                0f,
                65f) as Pawn;
        }

        private static bool IsValidTarget(Pawn user, Pawn target, Ability ability)
        {
            return target != null
                && !target.Dead
                && !target.Downed
                && target.Spawned
                && target.MapHeld == user.MapHeld
                && target.HostileTo(user)
                && ability.CanApplyOn(new LocalTargetInfo(target));
        }

        protected override Verse.AI.Job TryGiveJob(Pawn user)
        {
            if (user.kindDef == Def.PawnKindDefOf.LaetitiaMonster)
                return this.MeleeAttack(user);

            return null;
        }
    }
}
