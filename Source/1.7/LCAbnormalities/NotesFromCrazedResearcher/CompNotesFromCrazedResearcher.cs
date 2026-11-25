using LCAbnormalities.Shared;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Damage;
using LCAnomalyCore.Interface;
using LCAnomalyCore.ModExtensions;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.NotesFromCrazedResearcher
{
    [DefOf]
    public static class ResearcherNoteDefOf
    {
        public static SoundDef LC_ResearcherNote_Start;
        public static SoundDef LC_ResearcherNote_Finish;
        public static SoundDef LC_ResearcherNote_PageTurn;
        public static SoundDef LC_ResearcherNote_Explode;
        public static SoundDef LC_ResearcherNote_GoreBurst;

        static ResearcherNoteDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ResearcherNoteDefOf));
        }
    }

    public class CompProperties_NotesFromCrazedResearcher : CompProperties_AbnormalityTool
    {
        public int minSafeReturnTicks = 1800;
        public float maxCumulativeDamage = 60f;
        public float explosionRadius = 6.0f;
        public int explosionRedDamage = 35;

        public CompProperties_NotesFromCrazedResearcher()
        {
            compClass = typeof(CompNotesFromCrazedResearcher);
        }
    }

    /// <summary>
    /// T-09-78 癫狂研究员的笔记（可佩戴工具型）：
    /// 大幅提升工作、研究、学习与移动速度，并额外提供 +10% 异想体研究成功率（由 ILCPawnStudyModifier 实时叠加）；
    /// 但累计承受 60 点 HP+SP 有效伤害（黑伤双倍计入、免疫或被肖像等前置转嫁的伤害不计），
    /// 或持有未满 30 秒且未完成至少 1 次非工具异想体研究即归还时，将引发半径 6 格、35 点物理爆发伤的血肉炸裂并当场炸死佩戴者。
    /// </summary>
    public class CompNotesFromCrazedResearcher : CompAbnormalityTool
    {
        private bool exploding;

        public CompProperties_NotesFromCrazedResearcher PropsNote => (CompProperties_NotesFromCrazedResearcher)props;

        public override bool TryDrawHeldOverride(DrawPhase phase, Vector3 drawLoc)
        {
            if (phase != DrawPhase.Draw)
            {
                return true;
            }

            float ageSecs = ((Find.TickManager?.TicksGame ?? 0) + (parent?.thingIDNumber ?? 0) * 11) / 60f;
            float baseY = drawLoc.y;

            float dangerAmt = 0f;
            float readyAmt = 0f;
            if (IsEquipped && ActiveEquipper != null)
            {
                HediffComp_ResearcherNote noteComp = GetNoteComp(ActiveEquipper);
                if (noteComp != null)
                {
                    float maxDmg = Mathf.Max(1f, noteComp.Props?.maxCumulativeDamage ?? PropsNote.maxCumulativeDamage);
                    dangerAmt = Mathf.Clamp01(noteComp.CumulativeDamageTaken / maxDmg);
                    readyAmt = (CurrentEquipTicks >= PropsNote.minSafeReturnTicks && noteComp.CompletedStudies >= 1) ? 1f : 0f;
                }
            }

            ResearcherNoteMaterials.DrawContainedContainer(drawLoc, baseY, ageSecs, IsEquipped, dangerAmt, readyAmt);
            return true;
        }

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
                ResearcherNoteVfxUtility.EnsureBearerAura(user, this);
                if (user.Spawned && user.Map != null)
                {
                    ResearcherNoteVfxUtility.SpawnInsightPulse(user.Map, user.DrawPos, isSafeReady: false);
                }

                Messages.Message("LC_ResearcherNote_EquipMsg".Translate(user.LabelShortCap), user, MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }

        public override void OnEquippableTick(Pawn user, int intervalTicks, int equippedTicks)
        {
            base.OnEquippableTick(user, intervalTicks, equippedTicks);
            LinkEquipHediff(user);
            ResearcherNoteVfxUtility.EnsureBearerAura(user, this);
            TryNotifySafeReady(user, equippedTicks);
        }

        public void NotifyStudyPulse(Pawn wearer)
        {
            if (wearer == null || wearer.Dead)
            {
                return;
            }

            if (wearer.Spawned && wearer.Map != null)
            {
                if (ResearcherNoteDefOf.LC_ResearcherNote_PageTurn != null)
                {
                    ResearcherNoteDefOf.LC_ResearcherNote_PageTurn.PlayOneShot(wearer);
                }

                HediffComp_ResearcherNote noteComp = GetNoteComp(wearer);
                bool ready = CurrentEquipTicks >= PropsNote.minSafeReturnTicks && (noteComp?.CompletedStudies ?? 0) >= 1;
                ResearcherNoteVfxUtility.SpawnInsightPulse(wearer.Map, wearer.DrawPos, isSafeReady: ready);
            }

            TryNotifySafeReady(wearer, CurrentEquipTicks);
        }

        private void TryNotifySafeReady(Pawn user, int equippedTicks)
        {
            HediffComp_ResearcherNote noteComp = GetNoteComp(user);
            if (noteComp == null || noteComp.NotifiedSafeReady)
            {
                return;
            }

            if (equippedTicks >= PropsNote.minSafeReturnTicks && noteComp.CompletedStudies >= 1)
            {
                noteComp.MarkSafeReady();
                if (user != null && user.Spawned && user.Map != null)
                {
                    ResearcherNoteDefOf.LC_ResearcherNote_PageTurn?.PlayOneShot(user);
                    ResearcherNoteVfxUtility.SpawnInsightPulse(user.Map, user.DrawPos, isSafeReady: true);
                }

                if (user != null)
                {
                    Messages.Message("LC_ResearcherNote_SafeReadyMsg".Translate(user.LabelShortCap), user, MessageTypeDefOf.PositiveEvent, historical: false);
                }
            }
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

                if (user.Spawned && user.Map != null)
                {
                    ResearcherNoteVfxUtility.SpawnInsightPulse(user.Map, user.DrawPos, isSafeReady: true);
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
                Vector3 drawCenter = wearer.Spawned ? wearer.DrawPos : pos.ToVector3Shifted();
                base.OnUnequipped(wearer, CurrentEquipTicks, returnedNormally: false);

                if (map != null && pos.InBounds(map))
                {
                    SoundDef explodeSound = ResearcherNoteDefOf.LC_ResearcherNote_Explode;
                    if (ResearcherNoteDefOf.LC_ResearcherNote_GoreBurst != null)
                    {
                        ResearcherNoteDefOf.LC_ResearcherNote_GoreBurst.PlayOneShot(new TargetInfo(pos, map));
                    }

                    ResearcherNoteVfxUtility.SpawnGoreExplosion(map, drawCenter, PropsNote.explosionRadius);

                    GenExplosion.DoExplosion(
                        pos,
                        map,
                        PropsNote.explosionRadius,
                        LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage,
                        parent,
                        damAmount: Mathf.Max(1, PropsNote.explosionRedDamage),
                        armorPenetration: 0.55f,
                        explosionSound: explodeSound,
                        doSoundEffects: explodeSound != null);

                    Find.LetterStack.ReceiveLetter(
                        "LC_ResearcherNote_ExplodeLetterLabel".Translate(),
                        msgKey.Translate(wearer.LabelShortCap),
                        LetterDefOf.ThreatBig,
                        new LookTargets(pos, map));
                }

                if (!wearer.Dead)
                {
                    wearer.Kill(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 200f, instigator: parent));
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
            float maxDmg = PropsNote.maxCumulativeDamage;
            int studies = noteComp?.CompletedStudies ?? 0;
            int secsLeft = Mathf.Max(0, (PropsNote.minSafeReturnTicks - CurrentEquipTicks + 59) / 60);
            string readyTag = (secsLeft == 0 && studies >= 1)
                ? "LC_ResearcherNote_SafeTagReady".Translate().Resolve()
                : "LC_ResearcherNote_SafeTagWait".Translate(secsLeft).Resolve();

            return "LC_ResearcherNote_StatusInspect".Translate(
                dmg.ToString("F0"),
                maxDmg.ToString("F0"),
                studies,
                readyTag);
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
                ResearcherNoteVfxUtility.EnsureBearerAura(ActiveEquipper, this);
            }
        }
    }

    public class HediffCompProperties_ResearcherNote : HediffCompProperties
    {
        public float maxCumulativeDamage = 60f;
        public float studySuccessRateBonus = 0.10f;

        public HediffCompProperties_ResearcherNote()
        {
            compClass = typeof(HediffComp_ResearcherNote);
        }
    }

    public class HediffComp_ResearcherNote :
        HediffComp,
        ILCPawnPostInterceptDamageHandler,
        ILCPawnStudyModifier,
        ILCPawnStudyCompleteListener
    {
        public Thing toolThing;
        private float cumulativeDamageTaken;
        private int completedStudies;
        private bool triggered;
        private bool warnedHighDanger;
        private bool notifiedSafeReady;

        public HediffCompProperties_ResearcherNote Props => (HediffCompProperties_ResearcherNote)props;

        public float CumulativeDamageTaken => cumulativeDamageTaken;

        public int CompletedStudies => completedStudies;

        public bool NotifiedSafeReady => notifiedSafeReady;

        public void MarkSafeReady()
        {
            notifiedSafeReady = true;
        }

        public override string CompTipStringExtra =>
            "LC_ResearcherNote_HediffTip".Translate(
                cumulativeDamageTaken.ToString("F0"),
                (Props?.maxCumulativeDamage ?? 60f).ToString("F0"),
                completedStudies,
                ((Props?.studySuccessRateBonus ?? 0.10f) * 100f).ToString("F0"));

        public bool TryHandleDamageAfterIntercept(Pawn pawn, ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (triggered || pawn != Pawn || pawn == null || pawn.Dead || dinfo.Amount <= 0f)
            {
                return false;
            }

            float delta = ComputeEffectiveTrackingDamage(pawn, dinfo);
            if (delta <= 0f)
            {
                return false;
            }

            cumulativeDamageTaken += delta;
            float limit = Props?.maxCumulativeDamage ?? 60f;
            if (cumulativeDamageTaken >= limit)
            {
                triggered = true;
                (toolThing as ThingWithComps)?.TryGetComp<CompNotesFromCrazedResearcher>()
                    ?.TriggerNoteExplosion(pawn, "LC_ResearcherNote_DamageExplodeMsg");
                absorbed = true;
                return true;
            }

            if (cumulativeDamageTaken >= limit * 0.5f && !warnedHighDanger)
            {
                warnedHighDanger = true;
                if (pawn.Spawned)
                {
                    ResearcherNoteDefOf.LC_ResearcherNote_PageTurn?.PlayOneShot(pawn);
                }

                Messages.Message(
                    "LC_ResearcherNote_DangerWarningMsg".Translate(
                        pawn.LabelShortCap,
                        cumulativeDamageTaken.ToString("F0"),
                        limit.ToString("F0")),
                    pawn,
                    MessageTypeDefOf.ThreatSmall,
                    historical: false);
            }

            return false;
        }

        private static float ComputeEffectiveTrackingDamage(Pawn pawn, DamageInfo dinfo)
        {
            FourColorDamageExtension ext = dinfo.Def?.GetModExtension<FourColorDamageExtension>();
            if (ext != null && ext.damageType != FourColorDamageType.None)
            {
                float adjusted = DamageUtils.ComputeAdjustedDamage(dinfo, pawn, ext.damageType, ext.riskLevel, out float effectiveResistance);
                if (effectiveResistance <= 0.001f || adjusted <= 0f)
                {
                    return 0f;
                }

                // 黑伤同时侵蚀生命与精神，按 HP+SP 双倍计入累计承伤
                if (ext.damageType == FourColorDamageType.Black && DamageUtils.CanTakeMentalDamage(pawn))
                {
                    return adjusted * 2f;
                }

                return adjusted;
            }

            return dinfo.Amount;
        }

        public float GetStudySuccessRateBonus(Pawn studier, CompAbnormality abnormality, EAnomalyWorkType workType)
        {
            if (studier == null || studier != Pawn || studier.Dead || triggered)
            {
                return 0f;
            }

            return Props?.studySuccessRateBonus ?? 0.10f;
        }

        public void OnStudyCompleted(Pawn studier, CompAbnormality abnormality, LC_StudyResult result)
        {
            if (studier != Pawn || abnormality == null || abnormality.parent == toolThing)
            {
                return;
            }

            if (abnormality.parent?.TryGetComp<CompAbnormalityTool>() != null)
            {
                return;
            }

            completedStudies++;
            (toolThing as ThingWithComps)?.TryGetComp<CompNotesFromCrazedResearcher>()?.NotifyStudyPulse(Pawn);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref toolThing, "rnToolThing");
            Scribe_Values.Look(ref cumulativeDamageTaken, "rnCumulativeDamageTaken", 0f);
            Scribe_Values.Look(ref completedStudies, "rnCompletedStudies", 0);
            Scribe_Values.Look(ref triggered, "rnTriggered", false);
            Scribe_Values.Look(ref warnedHighDanger, "rnWarnedHighDanger", false);
            Scribe_Values.Look(ref notifiedSafeReady, "rnNotifiedSafeReady", false);
        }
    }
}
