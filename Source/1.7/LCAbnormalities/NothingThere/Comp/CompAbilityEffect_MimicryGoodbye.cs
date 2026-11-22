using LCAnomalyCore.Util;
using NothingThere.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NothingThere.Comp
{
    /// <summary>
    /// 拟态 E.G.O 武器主动技能「再见。」：造成高额红伤并将部分伤害转化为持握者生命恢复。
    /// </summary>
    public class CompAbilityEffect_MimicryGoodbye : CompAbilityEffect
    {
        public new CompProperties_AbilityMimicryGoodbye Props => (CompProperties_AbilityMimicryGoodbye)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            Pawn victim = target.Pawn;
            if (caster == null || victim == null || victim.Dead || victim.Destroyed)
            {
                return;
            }

            Map map = victim.MapHeld ?? caster.MapHeld;
            IntVec3 pos = victim.PositionHeld;
            bool isMechanoid = victim.def?.race?.IsMechanoid == true;

            float rawDamage = Props.damageRange.RandomInRange;
            DamageDef slashDef = Def.DamageDefOf.LC_NothingThere_RedSlash ?? LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage;
            DamageInfo dinfo = new DamageInfo(slashDef, rawDamage, Props.armorPenetration, -1f, caster, weapon: Def.ThingDefOf.EgoWeapon_NothingThere);
            DamageWorker.DamageResult result = victim.TakeDamage(dinfo);

            if (map != null && pos.InBounds(map))
            {
                Def.SoundDefOf.NothingThere_GoodbyeFinish?.PlayOneShot(new TargetInfo(pos, map));
                AbnormalityUtility.Burst(pos, map, 1.4f);
                Vector3 motePos = pos.ToVector3Shifted();
                if (motePos.ShouldSpawnMotesAt(map))
                {
                    MoteMaker.ThrowText(motePos, map, "LC_NothingThere_Goodbye".Translate(), new Color(0.95f, 0.12f, 0.16f), 2.4f);
                }
            }

            if (!isMechanoid && result.totalDamageDealt > 0f)
            {
                EffectUtil.HealPawnInjuries(caster, result.totalDamageDealt * Props.lifestealRatio, showFeedback: true);
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null || pawn.Dead)
            {
                if (throwMessages && pawn != null)
                {
                    Messages.Message("LC_NothingThere_TargetMustBeAlive".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            if (parent?.pawn != null && pawn.Faction != null && pawn.Faction == parent.pawn.Faction)
            {
                if (throwMessages)
                {
                    Messages.Message("LC_NothingThere_CannotTargetAlly".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return true;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return Valid(target, false) && parent?.pawn != null && target.Pawn.HostileTo(parent.pawn);
        }
    }
}
