using LCAbnormalities.Shared;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Interface;
using LCAnomalyCore.Util;
using RimWorld;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.NotesFromCrazedResearcher
{
    [DefOf]
    public static class ResearcherNoteDefOf
    {
        public static SoundDef LC_ResearcherNote_Start;
        public static SoundDef LC_ResearcherNote_Finish;

        static ResearcherNoteDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ResearcherNoteDefOf));
        }
    }

    public class CompProperties_NotesFromCrazedResearcher : CompProperties_AbnormalityTool
    {
        public int minSafeReturnTicks = 1800;
        public float maxCumulativeDamage = 100f;
        public float explosionRadius = 4.5f;

        public CompProperties_NotesFromCrazedResearcher()
        {
            compClass = typeof(CompNotesFromCrazedResearcher);
        }
    }

    /// <summary>
    /// T-09-78 癫狂研究员的笔记（可佩戴工具型）：大幅加速研究与工作，但佩戴期间累计受击达 100 或未满 30 秒且未完成至少 1 次研究即归还，将引发剧烈爆炸并当场炸死佩戴者。
    /// </summary>
    public class CompNotesFromCrazedResearcher : CompAbnormalityTool
    {
        private bool exploding;

        public CompProperties_NotesFromCrazedResearcher PropsNote => (CompProperties_NotesFromCrazedResearcher)props;

        public override void OnEquipped(Pawn user)
        {
            base.OnEquipped(user);
            exploding = false;
            LinkEquipHediff(user);

            if (ResearcherNoteDefOf.LC_ResearcherNote_Start != null && user != null && user.Spawned)
            {
                ResearcherNoteDefOf.LC_ResearcherNote_Start.PlayOneShot(user);
            }

            if (user != null)
            {
                Messages.Message("LC_ResearcherNote_EquipMsg".Translate(user.LabelShortCap), user, MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }

        public override void OnEquippableTick(Pawn user, int intervalTicks, int equippedTicks)
        {
            base.OnEquippableTick(user, intervalTicks, equippedTicks);
            LinkEquipHediff(user);
        }

        public override void OnUnequipped(Pawn user, int equippedTicks, bool returnedNormally)
        {
            if (returnedNormally && !exploding && user != null && !user.Dead)
            {
                HediffComp_ResearcherNote noteComp = GetNoteComp(user);
                int studies = noteComp?.CompletedStudies ?? 0;
                if (equippedTicks < PropsNote.minSafeReturnTicks || studies < 1)
                {
                    TriggerNoteExplosion(user, "LC_ResearcherNote_PrematureExplodeMsg");
                    return;
                }

                if (ResearcherNoteDefOf.LC_ResearcherNote_Finish != null && user.Spawned)
                {
                    ResearcherNoteDefOf.LC_ResearcherNote_Finish.PlayOneShot(user);
                }

                Messages.Message("LC_ResearcherNote_SafeReturnMsg".Translate(user.LabelShortCap), user, MessageTypeDefOf.PositiveEvent, historical: false);
            }

            base.OnUnequipped(user, equippedTicks, returnedNormally);
        }

        public void TriggerNoteExplosion(Pawn wearer, string msgKey)
        {
            if (exploding || wearer == null)
            {
                return;
            }

            exploding = true;
            try
            {
                Map map = wearer.MapHeld;
                IntVec3 pos = wearer.PositionHeld;
                base.OnUnequipped(wearer, CurrentEquipTicks, returnedNormally: false);

                if (map != null && pos.InBounds(map))
                {
                    GenExplosion.DoExplosion(
                        pos,
                        map,
                        PropsNote.explosionRadius,
                        LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage,
                        parent,
                        damAmount: 45,
                        armorPenetration: 0.6f,
                        explosionSound: ScorchedGirl.ScorchedGirlDefOf.LC_ScorchedGirl_Explode,
                        doSoundEffects: true);
                    GenExplosion.DoExplosion(
                        pos,
                        map,
                        PropsNote.explosionRadius * 0.8f,
                        LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                        parent,
                        damAmount: 35,
                        armorPenetration: 0.5f,
                        explosionSound: ScorchedGirl.ScorchedGirlDefOf.LC_ScorchedGirl_Explode,
                        doSoundEffects: false);

                    Find.LetterStack.ReceiveLetter(
                        "LC_ResearcherNote_ExplodeLetterLabel".Translate(),
                        msgKey.Translate(wearer.LabelShortCap),
                        LetterDefOf.ThreatBig,
                        new LookTargets(pos, map));
                }

                if (!wearer.Dead)
                {
                    wearer.Kill(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, 200f, instigator: parent));
                }
            }
            finally
            {
                exploding = false;
            }
        }

        public override string GetToolStatusInspectString()
        {
            if (!IsEquipped || ActiveEquipper == null)
            {
                return null;
            }

            HediffComp_ResearcherNote noteComp = GetNoteComp(ActiveEquipper);
            float dmg = noteComp?.CumulativeDamageTaken ?? 0f;
            int studies = noteComp?.CompletedStudies ?? 0;
            return "LC_ResearcherNote_StatusInspect".Translate(
                dmg.ToString("F0"),
                PropsNote.maxCumulativeDamage.ToString("F0"),
                studies);
        }

        private HediffComp_ResearcherNote GetNoteComp(Pawn user)
        {
            if (user == null || Props.equipHediffDef == null || user.health?.hediffSet == null)
            {
                return null;
            }

            return user.health.hediffSet.GetFirstHediffOfDef(Props.equipHediffDef)?.TryGetComp<HediffComp_ResearcherNote>();
        }

        private void LinkEquipHediff(Pawn user)
        {
            HediffComp_ResearcherNote noteComp = GetNoteComp(user);
            if (noteComp != null && noteComp.toolThing != parent)
            {
                noteComp.toolThing = parent;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && ActiveEquipper != null)
            {
                LinkEquipHediff(ActiveEquipper);
            }
        }
    }

    public class HediffCompProperties_ResearcherNote : HediffCompProperties
    {
        public float maxCumulativeDamage = 100f;

        public HediffCompProperties_ResearcherNote()
        {
            compClass = typeof(HediffComp_ResearcherNote);
        }
    }

    public class HediffComp_ResearcherNote : HediffComp, ILCPawnDamageInterceptor, ILCPawnStudyCompleteListener
    {
        public Thing toolThing;
        private float cumulativeDamageTaken;
        private int completedStudies;
        private bool triggered;

        public HediffCompProperties_ResearcherNote Props => (HediffCompProperties_ResearcherNote)props;

        public float CumulativeDamageTaken => cumulativeDamageTaken;

        public int CompletedStudies => completedStudies;

        public override string CompTipStringExtra =>
            "LC_ResearcherNote_HediffTip".Translate(
                cumulativeDamageTaken.ToString("F0"),
                (Props?.maxCumulativeDamage ?? 100f).ToString("F0"),
                completedStudies);

        public bool TryInterceptDamage(Pawn pawn, ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (triggered || pawn != Pawn || pawn == null || pawn.Dead || dinfo.Amount <= 0f)
            {
                return false;
            }

            cumulativeDamageTaken += dinfo.Amount;
            float limit = Props?.maxCumulativeDamage ?? 100f;
            if (cumulativeDamageTaken >= limit)
            {
                triggered = true;
                (toolThing as ThingWithComps)?.TryGetComp<CompNotesFromCrazedResearcher>()
                    ?.TriggerNoteExplosion(pawn, "LC_ResearcherNote_DamageExplodeMsg");
                absorbed = true;
                return true;
            }

            return false;
        }

        public void OnStudyCompleted(Pawn studier, CompAbnormality abnormality, LC_StudyResult result)
        {
            if (studier == Pawn)
            {
                completedStudies++;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref toolThing, "rnToolThing");
            Scribe_Values.Look(ref cumulativeDamageTaken, "rnCumulativeDamageTaken", 0f);
            Scribe_Values.Look(ref completedStudies, "rnCompletedStudies", 0);
            Scribe_Values.Look(ref triggered, "rnTriggered", false);
        }
    }
}
