using LCAnomalyCore.Interface;
using LCAnomalyCore.ModExtensions;
using PortraitOfAnotherWorld.Comp;
using Verse;

namespace PortraitOfAnotherWorld.Comp
{
    public class HediffCompProperties_PortraitLink : HediffCompProperties
    {
        public bool isProtected;

        public HediffCompProperties_PortraitLink()
        {
            compClass = typeof(HediffComp_PortraitLink);
        }
    }

    /// <summary>
    /// 异界肖像绑定状态组件：受保护者触发 PreApplyDamage 转嫁，替死替身死亡时通知肖像染血或反噬。
    /// </summary>
    public class HediffComp_PortraitLink : HediffComp, ILCPawnDamageInterceptor
    {
        public Thing portraitThing;

        public HediffCompProperties_PortraitLink Props => (HediffCompProperties_PortraitLink)props;

        private CompPortraitOfAnotherWorld CompPortrait =>
            portraitThing?.TryGetComp<CompPortraitOfAnotherWorld>();

        public override bool CompShouldRemove
        {
            get
            {
                if (portraitThing == null || portraitThing.Destroyed)
                {
                    return true;
                }

                CompPortraitOfAnotherWorld comp = CompPortrait;
                return comp == null || !comp.IsBoundAs(Pawn, Props.isProtected);
            }
        }

        public bool TryInterceptDamage(Pawn pawn, ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (!Props.isProtected || dinfo.Amount <= 0f || dinfo.Def == null)
            {
                return false;
            }

            if (!dinfo.Def.ExternalViolenceFor(pawn) && dinfo.Def.GetModExtension<FourColorDamageExtension>() == null)
            {
                return false;
            }

            CompPortraitOfAnotherWorld comp = CompPortrait;
            if (comp == null)
            {
                return false;
            }

            return comp.TryRedirectDamage(pawn, ref dinfo, out absorbed);
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);

            CompPortraitOfAnotherWorld comp = CompPortrait;
            if (comp == null)
            {
                return;
            }

            if (Props.isProtected)
            {
                comp.Notify_ProtectedDied(Pawn);
            }
            else
            {
                comp.Notify_SubstituteDied(Pawn);
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                CompPortraitOfAnotherWorld comp = CompPortrait;
                if (comp == null)
                {
                    return "LC_Portrait_TipUnlinked".Translate();
                }

                if (Props.isProtected)
                {
                    string partner = comp.SubstitutePawn?.LabelShortCap ?? "None".Translate().ToString();
                    return "LC_Portrait_TipProtected".Translate(partner);
                }
                else
                {
                    string partner = comp.ProtectedPawn?.LabelShortCap ?? "None".Translate().ToString();
                    return "LC_Portrait_TipSubstitute".Translate(partner);
                }
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref portraitThing, "portraitThing");
        }
    }
}
