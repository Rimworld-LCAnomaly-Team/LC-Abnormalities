using Laetitia.Things;
using RimWorld;
using Verse;

namespace Laetitia.Comp
{
    public class CompAbilityEffect_MonsterAttack : CompAbilityEffect
    {
        public new CompProperties_AbilityMonsterAttack Props => (CompProperties_AbilityMonsterAttack)props;

        /// <summary>
        /// 每次攻击现算红伤伤害信息，避免全生命周期复用同一次随机骰值。
        /// </summary>
        public DamageInfo CreateAttackDamageInfo()
        {
            return new DamageInfo(
                LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                Props.damageRange.RandomInRange,
                Props.armorPenetrationRange.RandomInRange,
                -1f,
                parent.pawn);
        }

        /// <summary>
        /// 每次攻击现算眩晕伤害信息。
        /// </summary>
        public DamageInfo CreateStunDamageInfo()
        {
            return new DamageInfo(
                DamageDefOf.Stun,
                Props.stunTimeRange.RandomInRange,
                0f,
                -1f,
                parent.pawn);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = target.Pawn;
            if (pawn != null && parent.pawn is LC_LaetitiaMonsterPawn monster)
            {
                monster.SetTargetAndDir(pawn);
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned)
            {
                return false;
            }

            return parent?.pawn == null || pawn.HostileTo(parent.pawn);
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null || pawn.Dead || !pawn.Spawned)
            {
                return false;
            }

            return parent?.pawn == null || pawn.HostileTo(parent.pawn);
        }
    }
}
