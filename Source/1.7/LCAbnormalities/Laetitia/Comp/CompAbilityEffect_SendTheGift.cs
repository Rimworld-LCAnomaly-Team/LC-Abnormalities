using RimWorld;
using Verse;

namespace Laetitia.Comp
{
    public class CompAbilityEffect_SendTheGift : CompAbilityEffect
    {
        private new CompProperties_AbilitySendTheGift Props => (CompProperties_AbilitySendTheGift)props;

        private Pawn Pawn => parent.pawn;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (target.Pawn != null)
            {
                Hediff hediff = target.Pawn.health.GetOrAddHediff(Def.HediffDefOf.LaetitiaGiftAggresive);
                if (hediff != null)
                {
                    hediff.Severity = 1f;
                    hediff.TryGetComp<HediffComp_LaetitiaGiftAggresive>()?.MarkFromEgoWeapon(Pawn);
                }
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.RaceProps.Humanlike)
            {
                if (throwMessages && pawn != null)
                {
                    Messages.Message("Laetitia_TargetMustBeLivingHumanlike".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            if (Pawn != null && pawn.Faction != null && pawn.Faction == Pawn.Faction)
            {
                if (throwMessages)
                {
                    Messages.Message("Laetitia_CannotTargetAlly".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            if (pawn.health?.hediffSet?.HasHediff(Def.HediffDefOf.LaetitiaGiftAggresive) == true)
            {
                if (throwMessages)
                {
                    Messages.Message("Laetitia_TargetAlreadyGifted".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return true;
        }

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            return Valid(target, false) && Pawn != null && target.Pawn.HostileTo(Pawn);
        }
    }
}
