using LCAbnormalities.Shared;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace LCAbnormalities.FairyFestival
{
    [DefOf]
    public static class FairyFestivalDefOf
    {
        public static HediffDef LC_FairyCare;
        public static SoundDef LC_FairyFestival_Special;
        public static SoundDef LC_FairyFestival_Dead;

        static FairyFestivalDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(FairyFestivalDefOf));
        }
    }

    public class CompProperties_FairyFestival : CompProperties_LCAbnormality
    {
        public HediffDef careHediffDef;
        public float goodHealAmount = 14f;
        public float goodMentalRestore = 0.25f;
        public float normalHealAmount = 8f;
        public float normalMentalRestore = 0.15f;
        public float badNibbleRedDamage = 6f;

        public CompProperties_FairyFestival()
        {
            compClass = typeof(CompFairyFestival);
        }
    }

    /// <summary>F-04-83 精灵盛宴核心逻辑：研究后赋予受试者温和疗愈与守护标记，一旦受试者去照料其他异想体即刻吞噬。</summary>
    public class CompFairyFestival : CompLCAbnormality
    {
        public CompProperties_FairyFestival PropsFairy => (CompProperties_FairyFestival)props;

        protected override void StudyEvent_Good(Pawn studier)
        {
            base.StudyEvent_Good(studier);
            ApplyCare(studier, PropsFairy.goodHealAmount, PropsFairy.goodMentalRestore, showGoodMessage: true);
        }

        protected override void StudyEvent_Normal(Pawn studier)
        {
            base.StudyEvent_Normal(studier);
            ApplyCare(studier, PropsFairy.normalHealAmount, PropsFairy.normalMentalRestore, showGoodMessage: false);
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            base.StudyEvent_Bad(studier);
            if (studier != null && !studier.Dead && PropsFairy.badNibbleRedDamage > 0f)
            {
                studier.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, PropsFairy.badNibbleRedDamage, instigator: parent));
            }
            ApplyCare(studier, 0f, 0f, showGoodMessage: false);
        }

        private void ApplyCare(Pawn studier, float healAmount, float mentalRestore, bool showGoodMessage)
        {
            if (studier == null || studier.Dead)
            {
                return;
            }

            if (healAmount > 0f)
            {
                LCAbnormalitiesHelper.HealInjuries(studier, healAmount);
            }

            if (mentalRestore > 0f)
            {
                LCAbnormalitiesHelper.RestoreMentalHealth(studier, mentalRestore, 0.08f);
            }

            HediffDef careDef = PropsFairy.careHediffDef ?? FairyFestivalDefOf.LC_FairyCare;
            LCAbnormalitiesHelper.ApplyOrRefreshHediff(studier, careDef, 1f);
            LCAbnormalitiesHelper.PlaySoundAt(FairyFestivalDefOf.LC_FairyFestival_Special, parent);

            if (showGoodMessage)
            {
                Messages.Message(
                    "LC_FairyFestival_CareMsg".Translate(studier.LabelShortCap),
                    studier,
                    MessageTypeDefOf.PositiveEvent,
                    historical: false);
            }
        }
    }

    public class HediffCompProperties_FairyCare : HediffCompProperties
    {
        public float healPerPulse = 2f;
        public float mentalRestorePerPulse = 0.03f;
        public int pulseIntervalTicks = 250;

        public HediffCompProperties_FairyCare()
        {
            compClass = typeof(HediffComp_FairyCare);
        }
    }

    /// <summary>精灵的照料：周期性回复伤口与精神；一旦持有者开始研究任何其他异想体，立刻惨遭分食。</summary>
    public class HediffComp_FairyCare : HediffComp, ILCPawnStudyStartListener
    {
        private bool triggered;

        public HediffCompProperties_FairyCare Props => (HediffCompProperties_FairyCare)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead || Pawn.Destroyed)
            {
                return;
            }

            int interval = Mathf.Max(60, Props.pulseIntervalTicks);
            if (Pawn.IsHashIntervalTick(interval))
            {
                LCAbnormalitiesHelper.HealInjuries(Pawn, Props.healPerPulse);
                LCAbnormalitiesHelper.RestoreMentalHealth(Pawn, Props.mentalRestorePerPulse);
            }
        }

        public void OnStudyStarted(Pawn studier, CompAbnormality abnormality)
        {
            if (triggered || studier == null || studier != Pawn || studier.Dead || abnormality?.parent == null)
            {
                return;
            }

            if (abnormality.parent.def?.defName == "FairyFestival")
            {
                return;
            }

            triggered = true;
            Map map = studier.MapHeld;
            IntVec3 pos = studier.PositionHeld;

            LCAbnormalitiesHelper.PlaySoundAt(FairyFestivalDefOf.LC_FairyFestival_Dead, studier);
            if (map != null && pos.InBounds(map))
            {
                AbnormalityUtility.Burst(pos, map, 1.5f);
                Find.LetterStack.ReceiveLetter(
                    "LC_FairyFestival_DevouredLetterLabel".Translate(),
                    "LC_FairyFestival_DevouredLetterText".Translate(studier.LabelShortCap, abnormality.parent.LabelCap),
                    LetterDefOf.ThreatBig,
                    new LookTargets(pos, map));
            }

            DamageInfo dinfo = new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 200f, 999f, instigator: abnormality.parent);
            studier.TakeDamage(dinfo);
            if (!studier.Dead)
            {
                studier.Kill(dinfo);
            }

            if (!studier.Dead && studier.health?.hediffSet != null && parent != null)
            {
                studier.health.RemoveHediff(parent);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref triggered, "fairyCareTriggered", false);
        }
    }

    /// <summary>振翅 E.G.O 武器专属伤害处理器：命中血肉目标时为挥舞者吸收 15% 造成的伤害。</summary>
    public class DamageWorker_WingbeatLifesteal : LCAnomalyCore.DamageWorker.DamageWorker_FourColor
    {
        public override Verse.DamageWorker.DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Verse.DamageWorker.DamageResult result = base.Apply(dinfo, victim);
            if (result.totalDamageDealt > 0f
                && dinfo.Instigator is Pawn attacker
                && !attacker.Dead
                && victim is Pawn targetPawn
                && targetPawn.RaceProps?.IsFlesh == true)
            {
                float heal = Mathf.Min(6f, result.totalDamageDealt * 0.15f);
                LCAbnormalitiesHelper.HealInjuries(attacker, heal);
            }

            return result;
        }
    }
}
