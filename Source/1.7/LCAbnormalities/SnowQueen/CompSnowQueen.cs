using System.Collections.Generic;
using System.Linq;
using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp.Pawns;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LCAbnormalities.SnowQueen
{
    [DefOf]
    public static class SnowQueenDefOf
    {
        public static HediffDef LC_SnowQueenKiss;
        public static HediffDef LC_FrostSlow;
        public static ThingDef Accessory_SnowQueen;
        public static JobDef LC_DuelSnowQueen;
        public static SoundDef LC_SnowQueen_Kiss1;
        public static SoundDef LC_SnowQueen_EnergyFreeze;
        public static SoundDef LC_SnowQueen_EnergyMelt;
        public static SoundDef LC_SnowQueen_Fight;
        public static SoundDef LC_SnowQueen_Win;
        public static SoundDef LC_SnowQueen_Lose;

        static SnowQueenDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SnowQueenDefOf));
        }
    }

    public class CompProperties_SnowQueen : CompProperties_LCAbnormality
    {
        public int freezeTimeoutTicks = 5400;

        public CompProperties_SnowQueen()
        {
            compClass = typeof(CompSnowQueen);
        }
    }

    /// <summary>
    /// F-01-37 冰雪女皇核心逻辑：工作差首次吻下霜印，二度落差即将员工封冻进冰棺倒计时；派人决斗获胜可拯救同伴并双双赢取 E.G.O 饰品，败北或超时双亡。
    /// </summary>
    public class CompSnowQueen : CompLCAbnormality, IThingHolder
    {
        private ThingOwner<Thing> frozenContainer;
        private int freezeTicksLeft;
        private Pawn activeDuelist;

        public CompSnowQueen()
        {
            frozenContainer = new ThingOwner<Thing>(this, oneStackOnly: false, LookMode.Deep);
        }

        public CompProperties_SnowQueen PropsSnow => (CompProperties_SnowQueen)props;

        public bool HasFrozenCaptive => frozenContainer != null && frozenContainer.Count > 0;

        public Pawn FrozenCaptive => frozenContainer?.FirstOrDefault() as Pawn;

        public int FreezeTicksLeft => freezeTicksLeft;

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return frozenContainer;
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            base.StudyEvent_Bad(studier);
            if (studier == null || studier.Dead)
            {
                return;
            }

            if (SnowQueenDefOf.LC_SnowQueenKiss == null)
            {
                return;
            }

            Hediff existingKiss = studier.health?.hediffSet?.GetFirstHediffOfDef(SnowQueenDefOf.LC_SnowQueenKiss);
            if (existingKiss == null)
            {
                LCAbnormalitiesHelper.ApplyOrRefreshHediff(studier, SnowQueenDefOf.LC_SnowQueenKiss, 1f);
                LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Kiss1, parent);
                Messages.Message("LC_SnowQueen_FirstKissMsg".Translate(studier.LabelShortCap), studier, MessageTypeDefOf.CautionInput, historical: false);
            }
            else if (!HasFrozenCaptive)
            {
                FreezeCaptive(studier);
            }
        }

        public void FreezeCaptive(Pawn victim)
        {
            if (victim == null || victim.Dead || HasFrozenCaptive || parent == null || parent.Destroyed)
            {
                return;
            }

            Map prevMap = victim.MapHeld ?? parent.MapHeld;
            IntVec3 prevPos = parent.PositionHeld;

            if (victim.Spawned)
            {
                victim.DeSpawn(DestroyMode.Vanish);
            }

            frozenContainer.TryAddOrTransfer(victim, canMergeWithExistingStacks: false);
            freezeTicksLeft = PropsSnow.freezeTimeoutTicks;

            LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_EnergyFreeze, parent);
            if (prevMap != null && prevPos.InBounds(prevMap))
            {
                Find.LetterStack.ReceiveLetter(
                    "LC_SnowQueen_FrozenLetterLabel".Translate(),
                    "LC_SnowQueen_FrozenLetterText".Translate(victim.LabelShortCap),
                    LetterDefOf.ThreatBig,
                    new LookTargets(prevPos, prevMap));
            }
        }

        public override void TickContained()
        {
            base.TickContained();
            if (!HasFrozenCaptive)
            {
                return;
            }

            bool duelRunning = activeDuelist != null
                && !activeDuelist.Dead
                && activeDuelist.Spawned
                && activeDuelist.CurJobDef == SnowQueenDefOf.LC_DuelSnowQueen;
            if (duelRunning)
            {
                return;
            }

            freezeTicksLeft--;
            if (freezeTicksLeft <= 0)
            {
                KillFrozenCaptive();
            }
        }

        private void KillFrozenCaptive()
        {
            Pawn captive = ReleaseCaptive();
            activeDuelist = null;
            LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Lose, parent);

            if (captive != null && !captive.Dead)
            {
                Messages.Message(
                    "LC_SnowQueen_FreezeTimeoutMsg".Translate(captive.LabelShortCap),
                    new LookTargets(captive.PositionHeld, captive.MapHeld),
                    MessageTypeDefOf.ThreatBig,
                    historical: false);
                captive.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 120f, 1f, instigator: parent));
                if (!captive.Dead)
                {
                    captive.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 120f, 1f, instigator: parent));
                }
                if (!captive.Dead)
                {
                    captive.Kill(null);
                }
            }
        }

        public void SetActiveDuelist(Pawn duelist)
        {
            activeDuelist = duelist;
        }

        public void ClearActiveDuelist(Pawn duelist)
        {
            if (activeDuelist == duelist)
            {
                activeDuelist = null;
            }
        }

        public void ResolveDuel(Pawn rescuer)
        {
            activeDuelist = null;
            if (!HasFrozenCaptive)
            {
                return;
            }

            float winChance = GetFortitudeWinChance(rescuer);
            if (Rand.Chance(winChance))
            {
                Pawn captive = ReleaseCaptive();
                LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Win, parent);
                LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_EnergyMelt, parent);

                RemoveKissFrom(rescuer);
                RemoveKissFrom(captive);

                LCAbnormalitiesHelper.GiveOrDropApparel(rescuer, SnowQueenDefOf.Accessory_SnowQueen);
                LCAbnormalitiesHelper.GiveOrDropApparel(captive, SnowQueenDefOf.Accessory_SnowQueen);

                if (parent?.MapHeld != null)
                {
                    Find.LetterStack.ReceiveLetter(
                        "LC_SnowQueen_DuelWinLetterLabel".Translate(),
                        "LC_SnowQueen_DuelWinLetterText".Translate(rescuer?.LabelShortCap ?? "-", captive?.LabelShortCap ?? "-"),
                        LetterDefOf.PositiveEvent,
                        new LookTargets(parent.PositionHeld, parent.MapHeld));
                }
            }
            else
            {
                Pawn captive = ReleaseCaptive();
                LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Lose, parent);

                if (parent?.MapHeld != null)
                {
                    Find.LetterStack.ReceiveLetter(
                        "LC_SnowQueen_DuelLossLetterLabel".Translate(),
                        "LC_SnowQueen_DuelLossLetterText".Translate(rescuer?.LabelShortCap ?? "-", captive?.LabelShortCap ?? "-"),
                        LetterDefOf.ThreatBig,
                        new LookTargets(parent.PositionHeld, parent.MapHeld));
                }

                ExecuteDuelFrostBurst(captive);
                ExecuteDuelFrostBurst(rescuer);
            }
        }

        private void ExecuteDuelFrostBurst(Pawn victim)
        {
            if (victim == null || victim.Dead)
            {
                return;
            }

            victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 90f, 1f, instigator: parent));
            if (!victim.Dead)
            {
                victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 90f, 1f, instigator: parent));
            }
            if (!victim.Dead)
            {
                victim.Kill(null);
            }
        }

        private static float GetFortitudeWinChance(Pawn rescuer)
        {
            CompPawnStatus status = rescuer?.TryGetComp<CompPawnStatus>();
            EPawnLevel lvl = status != null ? status.GetPawnStatusELevel(EPawnStatus.Fortitude) : EPawnLevel.I;
            switch (lvl)
            {
                case EPawnLevel.EX:
                case EPawnLevel.V:
                    return 0.95f;
                case EPawnLevel.IV:
                    return 0.85f;
                case EPawnLevel.III:
                    return 0.65f;
                case EPawnLevel.II:
                    return 0.45f;
                default:
                    return 0.25f;
            }
        }

        private static void RemoveKissFrom(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || SnowQueenDefOf.LC_SnowQueenKiss == null)
            {
                return;
            }

            Hediff kiss = pawn.health?.hediffSet?.GetFirstHediffOfDef(SnowQueenDefOf.LC_SnowQueenKiss);
            if (kiss != null)
            {
                pawn.health.RemoveHediff(kiss);
            }
        }

        public Pawn ReleaseCaptive()
        {
            if (frozenContainer == null || frozenContainer.Count == 0)
            {
                freezeTicksLeft = 0;
                return null;
            }

            Pawn captive = frozenContainer.FirstOrDefault() as Pawn;
            Map targetMap = parent?.MapHeld ?? Find.AnyPlayerHomeMap;
            IntVec3 targetPos = parent?.PositionHeld ?? (targetMap?.Center ?? IntVec3.Invalid);

            if (targetMap != null && targetPos.IsValid && targetPos.InBounds(targetMap))
            {
                frozenContainer.TryDropAll(targetPos, targetMap, ThingPlaceMode.Near);
            }
            else
            {
                frozenContainer.ClearAndDestroyContents();
                captive = null;
            }

            freezeTicksLeft = 0;
            return captive;
        }

        public override void Notify_Killed(Map prevMap, DamageInfo? dinfo = null)
        {
            ReleaseCaptive();
            base.Notify_Killed(prevMap, dinfo);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            ReleaseCaptive();
            base.PostDestroy(mode, previousMap);
        }

        public override IEnumerable<Gizmo> GetPlatformGizmos()
        {
            if (!HasFrozenCaptive || parent == null || parent.Destroyed)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "LC_SnowQueen_DuelGizmoLabel".Translate(),
                defaultDesc = "LC_SnowQueen_DuelGizmoDesc".Translate(FrozenCaptive?.LabelShortCap ?? "-"),
                icon = ContentFinder<Texture2D>.Get("UI/CodexEntries/SnowQueen", reportFailure: false) ?? BaseContent.BadTex,
                action = OpenDuelMenu
            };
        }

        private void OpenDuelMenu()
        {
            Map map = parent?.MapHeld;
            Building_AbnormalityHoldingPlatform platform = HoldingPlaformTargetComp?.HeldPlatform;
            if (map?.mapPawns?.FreeColonistsSpawned == null || platform == null)
            {
                return;
            }

            List<FloatMenuOption> opts = new List<FloatMenuOption>();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn c = colonists[i];
                if (c == null || c.Dead || c.Downed || !c.CanReach(platform, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                float pct = GetFortitudeWinChance(c) * 100f;
                opts.Add(new FloatMenuOption(
                    "LC_SnowQueen_DuelOption".Translate(c.LabelShortCap, pct.ToString("F0")),
                    () =>
                    {
                        Verse.AI.Job job = JobMaker.MakeJob(SnowQueenDefOf.LC_DuelSnowQueen, platform);
                        c.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
                    }));
            }

            if (opts.Count == 0)
            {
                opts.Add(new FloatMenuOption("NoColonistAbleToReachTarget".Translate(), null));
            }

            Find.WindowStack.Add(new FloatMenu(opts));
        }

        public override string GetPlatformInspectString()
        {
            if (!HasFrozenCaptive)
            {
                return null;
            }

            return "LC_SnowQueen_InspectFrozen".Translate(
                FrozenCaptive?.LabelShortCap ?? "-",
                Mathf.Max(0, freezeTicksLeft / 60));
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (frozenContainer == null)
            {
                frozenContainer = new ThingOwner<Thing>(this, oneStackOnly: false, LookMode.Deep);
            }
            Scribe_Deep.Look(ref frozenContainer, "sqFrozenContainer", this);
            Scribe_Values.Look(ref freezeTicksLeft, "sqFreezeTicksLeft", 0);
            Scribe_References.Look(ref activeDuelist, "sqActiveDuelist");
        }
    }

    public class LC_SnowQueenPawn : LC_StandardEntityPawn, IThingHolder
    {
        public CompSnowQueen SnowComp => EntityComp as CompSnowQueen;

        public new void GetChildHolders(List<IThingHolder> outChildren)
        {
            base.GetChildHolders(outChildren);
            if (SnowComp != null)
            {
                outChildren.Add(SnowComp);
            }
        }

        public override string CurrentBodyTexPath =>
            SnowComp?.HasFrozenCaptive == true
                ? "Things/Pawn/SnowQueen/SnowQueen_Frozen"
                : "Things/Pawn/SnowQueen/SnowQueen";

        public override Vector2 CurrentBodyDrawSize => new Vector2(1.9f, 1.9f);
    }

    public class JobDriver_DuelSnowQueen : JobDriver
    {
        private Building_AbnormalityHoldingPlatform TargetPlatform => job.targetA.Thing as Building_AbnormalityHoldingPlatform;

        private CompSnowQueen SnowComp => TargetPlatform?.HeldPawn?.TryGetComp<CompSnowQueen>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetPlatform, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => SnowComp == null || !SnowComp.HasFrozenCaptive);

            AddFinishAction(_ => SnowComp?.ClearActiveDuelist(pawn));

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil duelWait = Toils_General.Wait(240).WithProgressBarToilDelay(TargetIndex.A);
            duelWait.initAction = delegate
            {
                CompSnowQueen comp = SnowComp;
                comp?.SetActiveDuelist(pawn);
                LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Fight, TargetPlatform);
            };
            yield return duelWait;

            Toil finish = ToilMaker.MakeToil("ResolveSnowQueenDuel");
            finish.initAction = delegate
            {
                SnowComp?.ResolveDuel(pawn);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }

    /// <summary>霜之碎片 E.G.O 长矛伤害处理器：命中存活单位时叠加冰霜迟缓状态。</summary>
    public class DamageWorker_FrostSplinter : LCAnomalyCore.DamageWorker.DamageWorker_FourColor
    {
        public override Verse.DamageWorker.DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Verse.DamageWorker.DamageResult result = base.Apply(dinfo, victim);
            if (result.totalDamageDealt > 0f
                && victim is Pawn targetPawn
                && !targetPawn.Dead
                && SnowQueenDefOf.LC_FrostSlow != null)
            {
                LCAbnormalitiesHelper.ApplyOrRefreshHediff(targetPawn, SnowQueenDefOf.LC_FrostSlow, 1f);
            }

            return result;
        }
    }
}
