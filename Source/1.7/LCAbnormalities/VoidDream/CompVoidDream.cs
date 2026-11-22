using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Comp.Pawns;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace LCAbnormalities.VoidDream
{
    [DefOf]
    public static class VoidDreamDefOf
    {
        public static HediffDef LC_VoidDreamSleep;
        public static JobDef LC_WakeVoidDreamSleeper;
        public static SoundDef LC_VoidDream_Change;
        public static SoundDef LC_VoidDream_Shout;
        public static SoundDef LC_VoidDream_Skill;

        static VoidDreamDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VoidDreamDefOf));
        }
    }

    public class CompProperties_VoidDream : CompProperties_LCAbnormality
    {
        public int crowIntervalTicks = 420;
        public float crowRadius = 10f;

        public CompProperties_VoidDream()
        {
            compClass = typeof(CompVoidDream);
        }
    }

    /// <summary>T-02-99 虚空之梦核心逻辑：自律 I 工作必降计数；突破后蜕变为公鸡并周期啼鸣令周边人类沉睡。</summary>
    public class CompVoidDream : CompLCAbnormality
    {
        private int crowCooldownTicks = 180;

        public CompProperties_VoidDream PropsDream => (CompProperties_VoidDream)props;

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            base.Notify_Studied(studier, interrupted);
            if (studier == null || parent == null || parent.Destroyed || Escaped)
            {
                return;
            }

            CompPawnStatus status = studier.TryGetComp<CompPawnStatus>();
            if (status != null && status.GetPawnStatusELevel(EPawnStatus.Temperance) <= EPawnLevel.I)
            {
                QliphothCountCurrent--;
                Messages.Message(
                    "LC_VoidDream_LowTemperanceMsg".Translate(studier.LabelShortCap),
                    parent,
                    MessageTypeDefOf.NegativeEvent,
                    historical: false);
            }
        }

        public override void Notify_Escaped()
        {
            base.Notify_Escaped();
            crowCooldownTicks = 180;
            LCAbnormalitiesHelper.PlaySoundAt(VoidDreamDefOf.LC_VoidDream_Change, parent);
        }

        public override void Notify_Holded()
        {
            base.Notify_Holded();
            crowCooldownTicks = 180;
        }

        public override void TickBreached()
        {
            base.TickBreached();
            if (!Escaped || parent == null || !parent.Spawned || parent.Map == null)
            {
                return;
            }

            crowCooldownTicks--;
            if (crowCooldownTicks <= 0)
            {
                crowCooldownTicks = PropsDream.crowIntervalTicks;
                EmitHypnoticCrow();
            }
        }

        private void EmitHypnoticCrow()
        {
            Map map = parent.Map;
            IntVec3 pos = parent.Position;
            if (map == null || !pos.InBounds(map))
            {
                return;
            }

            LCAbnormalitiesHelper.PlaySoundAt(VoidDreamDefOf.LC_VoidDream_Shout, parent);
            FleckMaker.ThrowDustPuffThick(pos.ToVector3Shifted(), map, 2.2f, new Color(0.65f, 0.45f, 0.9f));

            List<Pawn> candidates = new List<Pawn>(map.mapPawns.AllPawnsSpawned);
            float radiusSq = PropsDream.crowRadius * PropsDream.crowRadius;
            for (int i = 0; i < candidates.Count; i++)
            {
                Pawn target = candidates[i];
                if (target == null || target.Dead || !(target.RaceProps?.Humanlike ?? false))
                {
                    continue;
                }

                if ((target.Position - pos).LengthHorizontalSquared > radiusSq)
                {
                    continue;
                }

                Hediff existing = target.health?.hediffSet?.GetFirstHediffOfDef(VoidDreamDefOf.LC_VoidDreamSleep);
                if (existing != null)
                {
                    target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 35f, instigator: parent));
                    target.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, 25f, instigator: parent));
                    if (!target.Dead && target.health?.hediffSet != null)
                    {
                        target.health.RemoveHediff(existing);
                    }
                    Messages.Message("LC_VoidDream_SecondCrowMsg".Translate(target.LabelShortCap), target, MessageTypeDefOf.ThreatBig, historical: false);
                }
                else
                {
                    LCAbnormalitiesHelper.ApplyOrRefreshHediff(target, VoidDreamDefOf.LC_VoidDreamSleep, 1f);
                    LCAbnormalitiesHelper.PlaySoundAt(VoidDreamDefOf.LC_VoidDream_Skill, target);
                    Messages.Message("LC_VoidDream_SleepMsg".Translate(target.LabelShortCap), target, MessageTypeDefOf.ThreatSmall, historical: false);
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref crowCooldownTicks, "vdCrowCooldownTicks", 180);
        }
    }

    public class LC_VoidDreamPawn : LC_StandardEntityPawn
    {
        public override string CurrentBodyTexPath =>
            LCComp?.Escaped == true
                ? "Things/Pawn/VoidDream/VoidDream_Rooster"
                : "Things/Pawn/VoidDream/VoidDream_Sheep";

        public override Vector2 CurrentBodyDrawSize =>
            LCComp?.Escaped == true
                ? new Vector2(2.1f, 2.1f)
                : new Vector2(1.6f, 1.6f);
    }

    public class HediffCompProperties_VoidDreamSleep : HediffCompProperties
    {
        public int nightmareTimeoutTicks = 3600;

        public HediffCompProperties_VoidDreamSleep()
        {
            compClass = typeof(HediffComp_VoidDreamSleep);
        }
    }

    /// <summary>虚空催眠：陷入深度昏睡；超过 60 秒未被同事拍醒将承受致命精神噩梦反噬。</summary>
    public class HediffComp_VoidDreamSleep : HediffComp
    {
        private int sleepTicks;

        public HediffCompProperties_VoidDreamSleep Props => (HediffCompProperties_VoidDreamSleep)props;

        public override string CompTipStringExtra =>
            "LC_VoidDream_SleepCountdownTip".Translate(Mathf.Max(0, (Props.nightmareTimeoutTicks - sleepTicks) / 60));

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead || Pawn.Destroyed)
            {
                return;
            }

            sleepTicks++;
            if (sleepTicks >= Props.nightmareTimeoutTicks)
            {
                Messages.Message("LC_VoidDream_NightmareMsg".Translate(Pawn.LabelShortCap), Pawn, MessageTypeDefOf.ThreatBig, historical: false);
                Pawn.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 40f));
                Pawn.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage, 35f));
                if (!Pawn.Dead && Pawn.health?.hediffSet != null && parent != null)
                {
                    Pawn.health.RemoveHediff(parent);
                }
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Pawn == null || Pawn.Dead || !Pawn.Spawned || Pawn.MapHeld == null)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "LC_VoidDream_WakeGizmoLabel".Translate(),
                defaultDesc = "LC_VoidDream_WakeGizmoDesc".Translate(Pawn.LabelShortCap),
                icon = ContentFinder<Texture2D>.Get("UI/CodexEntries/VoidDream", reportFailure: false) ?? BaseContent.BadTex,
                action = OpenWakeMenu
            };
        }

        private void OpenWakeMenu()
        {
            Map map = Pawn?.MapHeld;
            if (map?.mapPawns?.FreeColonistsSpawned == null)
            {
                return;
            }

            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn c = colonists[i];
                if (c == null || c == Pawn || c.Dead || c.Downed || !c.CanReach(Pawn, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                opts.Add(new FloatMenuOption(c.LabelShortCap, () =>
                {
                    Verse.AI.Job job = JobMaker.MakeJob(VoidDreamDefOf.LC_WakeVoidDreamSleeper, Pawn);
                    c.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
                }));
            }

            if (opts.Count == 0)
            {
                opts.Add(new FloatMenuOption("NoColonistAbleToReachTarget".Translate(), null));
            }

            Find.WindowStack.Add(new FloatMenu(opts));
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref sleepTicks, "vdSleepTicks", 0);
        }
    }

    /// <summary>唤醒陷入虚空催眠的同伴并驱散其噩梦。</summary>
    public class JobDriver_WakeVoidDreamSleeper : JobDriver
    {
        private Pawn TargetSleeper => (Pawn)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetSleeper, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(60).WithProgressBarToilDelay(TargetIndex.A);
            Toil wake = ToilMaker.MakeToil("WakeVoidDreamSleeper");
            wake.initAction = delegate
            {
                Pawn target = TargetSleeper;
                if (target != null && !target.Dead)
                {
                    Hediff sleep = target.health?.hediffSet?.GetFirstHediffOfDef(VoidDreamDefOf.LC_VoidDreamSleep);
                    if (sleep != null)
                    {
                        target.health.RemoveHediff(sleep);
                    }
                    Messages.Message("LC_VoidDream_WokeMsg".Translate(target.LabelShortCap, pawn.LabelShortCap), target, MessageTypeDefOf.PositiveEvent, historical: false);
                }
            };
            wake.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return wake;
        }
    }
}
