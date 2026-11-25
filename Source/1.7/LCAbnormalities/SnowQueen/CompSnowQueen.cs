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
        public static SoundDef LC_SnowQueen_Kiss2;
        public static SoundDef LC_SnowQueen_Kiss3;
        public static SoundDef LC_SnowQueen_EnergyFreeze;
        public static SoundDef LC_SnowQueen_EnergyMelt;
        public static SoundDef LC_SnowQueen_Filter_On;
        public static SoundDef LC_SnowQueen_Filter_Off;
        public static SoundDef LC_SnowQueen_Fight;
        public static SoundDef LC_SnowQueen_Win;
        public static SoundDef LC_SnowQueen_Win_Sword;
        public static SoundDef LC_SnowQueen_Lose;
        public static SoundDef LC_SnowQueen_Lose_Sword;

        public static AnimationDef SnowQueen_Idle;
        public static AnimationDef SnowQueen_Work;
        public static AnimationDef SnowQueen_KissFirst;
        public static AnimationDef SnowQueen_KissSecond;
        public static AnimationDef SnowQueen_KissThird;
        public static AnimationDef SnowQueen_Duel;
        public static AnimationDef SnowQueen_DuelWin;

        static SnowQueenDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SnowQueenDefOf));
        }
    }

    public enum SnowQueenAnimPhase
    {
        Idle,
        Work,
        KissFirst,
        KissSecond,
        KissThird,
        Duel,
        DuelWin
    }

    public class CompProperties_SnowQueen : CompProperties_LCAbnormality
    {
        public int freezeTimeoutTicks = 5400;
        public float normalKissChance = 0.50f;
        public float badKissChance = 1.0f;
        public int duelDurationTicks = 240;
        public FloatRange duelWhiteDamageTotalRange = new FloatRange(10f, 14f);

        public CompProperties_SnowQueen()
        {
            compClass = typeof(CompSnowQueen);
        }
    }

    /// <summary>
    /// F-01-37 冰雪女皇核心逻辑：
    /// 普通工作 50% / 差评工作 100% 吻下霜印；二度被吻即将员工封入 90 秒冰棺并封锁常规研究，
    /// 需派遣勇毅足够高的员工发起“拯救决斗”——胜出则融冰并双双必得 E.G.O. 饰品“霜之碎片”，败北或超时双亡。
    /// </summary>
    public class CompSnowQueen : CompLCAbnormality, IThingHolder
    {
        private const int AnimKissFirstDuration = 140;
        private const int AnimKissSecondDuration = 130;
        private const int AnimKissThirdDuration = 200;
        private const int AnimDuelWinDuration = 90;

        private ThingOwner<Thing> frozenContainer;
        private int freezeTicksLeft;
        private Pawn activeDuelist;
        private SnowQueenAnimPhase animPhase = SnowQueenAnimPhase.Idle;
        private int animPhaseTicksLeft;
        private int studyingActiveTicks;

        public CompSnowQueen()
        {
            frozenContainer = new ThingOwner<Thing>(this, oneStackOnly: false, LookMode.Deep);
        }

        public CompProperties_SnowQueen PropsSnow => (CompProperties_SnowQueen)props;

        public bool HasFrozenCaptive => frozenContainer != null && frozenContainer.Count > 0;

        public Pawn FrozenCaptive => frozenContainer?.FirstOrDefault() as Pawn;

        public int FreezeTicksLeft => freezeTicksLeft;

        public bool IsDuelInProgress => activeDuelist != null
            && !activeDuelist.Dead
            && activeDuelist.Spawned
            && activeDuelist.CurJobDef == SnowQueenDefOf.LC_DuelSnowQueen;

        public SnowQueenAnimPhase CurrentAnimPhase
        {
            get
            {
                if (animPhaseTicksLeft > 0
                    && (animPhase == SnowQueenAnimPhase.KissFirst
                        || animPhase == SnowQueenAnimPhase.KissSecond
                        || animPhase == SnowQueenAnimPhase.KissThird
                        || animPhase == SnowQueenAnimPhase.DuelWin))
                {
                    return animPhase;
                }

                if (IsDuelInProgress)
                {
                    return SnowQueenAnimPhase.Duel;
                }

                if (studyingActiveTicks > 0)
                {
                    return SnowQueenAnimPhase.Work;
                }

                return SnowQueenAnimPhase.Idle;
            }
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return frozenContainer;
        }

        public override bool CanBeStudiedNow(out string blockReason)
        {
            if (HasFrozenCaptive)
            {
                blockReason = "LC_SnowQueen_StudyBlockedFrozen".Translate(FrozenCaptive?.LabelShortCap ?? "-");
                return false;
            }

            return base.CanBeStudiedNow(out blockReason);
        }

        public override void Notify_Studying(Pawn studier)
        {
            base.Notify_Studying(studier);
            studyingActiveTicks = 30;
            (parent as LC_SnowQueenPawn)?.UpdateAnimation();
        }

        public override void Notify_Studied(Pawn studier, bool interrupted = false)
        {
            base.Notify_Studied(studier, interrupted);
            studyingActiveTicks = 0;
            (parent as LC_SnowQueenPawn)?.UpdateAnimation();
        }

        protected override void StudyEvent_Normal(Pawn studier)
        {
            base.StudyEvent_Normal(studier);
            if (Rand.Chance(PropsSnow.normalKissChance))
            {
                ApplyKissOrFreeze(studier);
            }
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            base.StudyEvent_Bad(studier);
            if (Rand.Chance(PropsSnow.badKissChance))
            {
                ApplyKissOrFreeze(studier);
            }
        }

        private void ApplyKissOrFreeze(Pawn studier)
        {
            if (studier == null || studier.Dead || SnowQueenDefOf.LC_SnowQueenKiss == null)
            {
                return;
            }

            Hediff existingKiss = studier.health?.hediffSet?.GetFirstHediffOfDef(SnowQueenDefOf.LC_SnowQueenKiss);
            if (existingKiss == null)
            {
                LCAbnormalitiesHelper.ApplyOrRefreshHediff(studier, SnowQueenDefOf.LC_SnowQueenKiss, 1f);
                LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Kiss1, parent);
                TriggerOneShotAnim(SnowQueenAnimPhase.KissFirst, AnimKissFirstDuration);
                SnowQueenVfxUtility.EnsureFirstKissMark(studier);

                Map fxMap = studier.MapHeld ?? parent?.MapHeld;
                Vector3 fxPos = studier.Spawned ? studier.DrawPos : (parent?.DrawPos ?? studier.PositionHeld.ToVector3Shifted());
                SnowQueenVfxUtility.SpawnKissBurst(fxMap, fxPos, isThirdKiss: false);

                Messages.Message(
                    "LC_SnowQueen_FirstKissMsg".Translate(studier.LabelShortCap),
                    studier,
                    MessageTypeDefOf.CautionInput,
                    historical: false);
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

            LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Kiss2, parent);
            LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_EnergyFreeze, parent);
            LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Filter_On, parent);
            TriggerOneShotAnim(SnowQueenAnimPhase.KissSecond, AnimKissSecondDuration);

            if (prevMap != null && prevPos.InBounds(prevMap))
            {
                SnowQueenVfxUtility.EnsureFrozenPrison(parent as Pawn);
                SnowQueenVfxUtility.SpawnKissBurst(prevMap, prevPos.ToVector3Shifted(), isThirdKiss: false);
                Find.LetterStack.ReceiveLetter(
                    "LC_SnowQueen_FrozenLetterLabel".Translate(victim.LabelShortCap),
                    "LC_SnowQueen_FrozenLetterText".Translate(victim.LabelShortCap),
                    LetterDefOf.ThreatBig,
                    new LookTargets(prevPos, prevMap));
            }
        }

        public void BeginDuelAnimation()
        {
            animPhaseTicksLeft = 0;
            animPhase = SnowQueenAnimPhase.Duel;
            (parent as LC_SnowQueenPawn)?.UpdateAnimation(force: true);
        }

        private void TriggerOneShotAnim(SnowQueenAnimPhase phase, int ticks)
        {
            animPhase = phase;
            animPhaseTicksLeft = Mathf.Max(1, ticks);
            (parent as LC_SnowQueenPawn)?.UpdateAnimation(force: true);
        }

        public override void TickContained()
        {
            base.TickContained();

            if (studyingActiveTicks > 0)
            {
                studyingActiveTicks--;
            }

            if (animPhaseTicksLeft > 0)
            {
                animPhaseTicksLeft--;
                if (animPhaseTicksLeft == 0)
                {
                    animPhase = SnowQueenAnimPhase.Idle;
                    (parent as LC_SnowQueenPawn)?.UpdateAnimation(force: true);
                }
            }

            if (!HasFrozenCaptive)
            {
                return;
            }

            if (parent is Pawn queenPawn && queenPawn.IsHashIntervalTick(30))
            {
                SnowQueenVfxUtility.EnsureFrozenPrison(queenPawn);
            }

            if (parent != null && parent.IsHashIntervalTick(60))
            {
                Map map = parent.MapHeld;
                IntVec3 pos = parent.PositionHeld;
                if (map != null && pos.InBounds(map) && pos.GetTemperature(map) > -8f)
                {
                    GenTemperature.PushHeat(pos, map, -1.2f);
                }
            }

            if (IsDuelInProgress)
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
            activeDuelist = null;
            Map map = parent?.MapHeld;
            Vector3 anchorPos = parent != null ? parent.PositionHeld.ToVector3Shifted() : Vector3.zero;
            Pawn captive = ReleaseCaptive();

            PlayExecutionVisualsAndSounds(map, anchorPos, null);

            if (captive != null && !captive.Dead)
            {
                Messages.Message(
                    "LC_SnowQueen_FreezeTimeoutMsg".Translate(captive.LabelShortCap),
                    new LookTargets(captive.PositionHeld, captive.MapHeld),
                    MessageTypeDefOf.ThreatBig,
                    historical: false);
                ExecuteVictimWithFrost(captive);
            }
        }

        public void SetActiveDuelist(Pawn duelist)
        {
            activeDuelist = duelist;
            (parent as LC_SnowQueenPawn)?.UpdateAnimation();
        }

        public void ClearActiveDuelist(Pawn duelist)
        {
            if (activeDuelist == duelist)
            {
                activeDuelist = null;
                (parent as LC_SnowQueenPawn)?.UpdateAnimation();
            }
        }

        public void ResolveDuel(Pawn rescuer)
        {
            activeDuelist = null;
            if (!HasFrozenCaptive)
            {
                return;
            }

            bool fighterUnavailable = rescuer == null || rescuer.Dead || rescuer.Downed || rescuer.InMentalState;
            if (!fighterUnavailable && Rand.Chance(GetFortitudeWinChance(rescuer)))
            {
                Pawn captive = ReleaseCaptive();
                LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Win, parent);
                LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Win_Sword, parent);
                LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_EnergyMelt, parent);
                LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Filter_Off, parent);
                TriggerOneShotAnim(SnowQueenAnimPhase.DuelWin, AnimDuelWinDuration);

                if (parent?.MapHeld != null)
                {
                    SnowQueenVfxUtility.SpawnDuelWinThaw(parent.MapHeld, parent.PositionHeld.ToVector3Shifted());
                }

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
                ResolveDuelDefeat(rescuer);
            }
        }

        public void ResolveDuelDefeat(Pawn rescuer)
        {
            activeDuelist = null;
            if (!HasFrozenCaptive)
            {
                return;
            }

            Map map = parent?.MapHeld;
            Vector3 anchorPos = parent != null ? parent.PositionHeld.ToVector3Shifted() : Vector3.zero;
            Vector3? rescuerPos = (rescuer != null && rescuer.Spawned) ? (Vector3?)rescuer.DrawPos : null;
            Pawn captive = ReleaseCaptive();

            PlayExecutionVisualsAndSounds(map, anchorPos, rescuerPos);

            if (map != null && parent != null)
            {
                Find.LetterStack.ReceiveLetter(
                    "LC_SnowQueen_DuelLossLetterLabel".Translate(),
                    "LC_SnowQueen_DuelLossLetterText".Translate(rescuer?.LabelShortCap ?? "-", captive?.LabelShortCap ?? "-"),
                    LetterDefOf.ThreatBig,
                    new LookTargets(parent.PositionHeld, map));
            }

            ExecuteVictimWithFrost(captive);
            ExecuteVictimWithFrost(rescuer);
        }

        private void PlayExecutionVisualsAndSounds(Map map, Vector3 captiveAnchorPos, Vector3? rescuerPos)
        {
            LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Kiss3, parent);
            LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Lose, parent);
            LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Lose_Sword, parent);
            LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Filter_Off, parent);
            TriggerOneShotAnim(SnowQueenAnimPhase.KissThird, AnimKissThirdDuration);

            if (map == null)
            {
                return;
            }

            SnowQueenVfxUtility.SpawnIceDeath(map, captiveAnchorPos);
            SnowQueenVfxUtility.SpawnKissBurst(map, captiveAnchorPos, isThirdKiss: true);
            if (rescuerPos.HasValue)
            {
                SnowQueenVfxUtility.SpawnIceDeath(map, rescuerPos.Value);
            }
        }

        private void ExecuteVictimWithFrost(Pawn victim)
        {
            if (victim == null || victim.Dead)
            {
                return;
            }

            victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 120f, 1f, instigator: parent));
            if (!victim.Dead)
            {
                victim.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_RedDamage, 120f, 1f, instigator: parent));
            }
            if (!victim.Dead)
            {
                victim.Kill(null);
            }
        }

        public static float GetFortitudeWinChance(Pawn rescuer)
        {
            CompPawnStatus status = rescuer?.TryGetComp<CompPawnStatus>();
            EPawnLevel lvl = status != null ? status.GetPawnStatusELevel(EPawnStatus.Fortitude) : EPawnLevel.I;
            switch (lvl)
            {
                case EPawnLevel.EX:
                case EPawnLevel.V:
                    return 1.00f;
                case EPawnLevel.IV:
                    return 0.80f;
                case EPawnLevel.III:
                    return 0.50f;
                case EPawnLevel.II:
                    return 0.30f;
                default:
                    return 0.10f;
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

        public override IEnumerable<FloatMenuOption> GetPlatformFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption opt in base.GetPlatformFloatMenuOptions(selPawn))
            {
                yield return opt;
            }

            if (!HasFrozenCaptive || selPawn == null || selPawn.Dead || selPawn.Downed)
            {
                yield break;
            }

            Building_AbnormalityHoldingPlatform platform = HoldingPlaformTargetComp?.HeldPlatform;
            if (platform == null)
            {
                yield break;
            }

            float pct = GetFortitudeWinChance(selPawn) * 100f;
            if (!selPawn.CanReach(platform, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption(
                    "CannotReach".Translate() + ": " + "LC_SnowQueen_DuelOption".Translate(selPawn.LabelShortCap, pct.ToString("F0")),
                    null);
                yield break;
            }

            yield return new FloatMenuOption(
                "LC_SnowQueen_DuelOption".Translate(selPawn.LabelShortCap, pct.ToString("F0")),
                () =>
                {
                    Job job = JobMaker.MakeJob(SnowQueenDefOf.LC_DuelSnowQueen, platform);
                    selPawn.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
                });
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
                        Job job = JobMaker.MakeJob(SnowQueenDefOf.LC_DuelSnowQueen, platform);
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

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                (parent as LC_SnowQueenPawn)?.UpdateAnimation(force: true);
            }
        }
    }

    /// <summary>
    /// F-01-37 冰雪女皇实体 Pawn：由 11 部件 Spine 渲染树与 7 组 AnimationDef 驱动。
    /// </summary>
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

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            UpdateAnimation(force: true);
        }

        public override void TickHolded()
        {
            base.TickHolded();
            UpdateAnimation();
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Destroyed)
            {
                UpdateAnimation();
            }
        }

        public void UpdateAnimation(bool force = false)
        {
            if (Drawer?.renderer == null)
            {
                return;
            }

            AnimationDef desired;
            switch (SnowComp?.CurrentAnimPhase ?? SnowQueenAnimPhase.Idle)
            {
                case SnowQueenAnimPhase.KissFirst:
                    desired = SnowQueenDefOf.SnowQueen_KissFirst;
                    break;
                case SnowQueenAnimPhase.KissSecond:
                    desired = SnowQueenDefOf.SnowQueen_KissSecond;
                    break;
                case SnowQueenAnimPhase.KissThird:
                    desired = SnowQueenDefOf.SnowQueen_KissThird;
                    break;
                case SnowQueenAnimPhase.Duel:
                    desired = SnowQueenDefOf.SnowQueen_Duel;
                    break;
                case SnowQueenAnimPhase.DuelWin:
                    desired = SnowQueenDefOf.SnowQueen_DuelWin;
                    break;
                case SnowQueenAnimPhase.Work:
                    desired = SnowQueenDefOf.SnowQueen_Work;
                    break;
                case SnowQueenAnimPhase.Idle:
                default:
                    desired = SnowQueenDefOf.SnowQueen_Idle;
                    break;
            }

            if (desired != null && (force || Drawer.renderer.CurAnimation != desired))
            {
                Drawer.renderer.SetAnimation(desired);
            }
        }
    }

    public class JobDriver_DuelSnowQueen : JobDriver
    {
        private bool duelStarted;
        private bool duelResolved;
        private int duelElapsedTicks;
        private float clashWhiteDamage = 4f;

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

            AddFinishAction(_ =>
            {
                CompSnowQueen comp = SnowComp;
                comp?.ClearActiveDuelist(pawn);
                if (duelStarted
                    && !duelResolved
                    && comp != null
                    && comp.HasFrozenCaptive
                    && (pawn.Dead || pawn.Downed || pawn.InMentalState))
                {
                    duelResolved = true;
                    comp.ResolveDuelDefeat(pawn);
                }
            });

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            int duration = Mathf.Max(60, SnowComp?.PropsSnow?.duelDurationTicks ?? 240);
            Toil duelWait = Toils_General.Wait(duration).WithProgressBarToilDelay(TargetIndex.A);
            duelWait.initAction = delegate
            {
                CompSnowQueen comp = SnowComp;
                if (comp == null)
                {
                    return;
                }

                duelStarted = true;
                duelElapsedTicks = 0;
                clashWhiteDamage = comp.PropsSnow.duelWhiteDamageTotalRange.RandomInRange / 3f;
                comp.SetActiveDuelist(pawn);
                comp.BeginDuelAnimation();
                LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Fight, TargetPlatform);
            };
            duelWait.AddPreTickAction(delegate
            {
                if (duelResolved)
                {
                    return;
                }

                duelElapsedTicks++;
                pawn.rotationTracker?.FaceTarget(TargetPlatform);

                CompSnowQueen comp = SnowComp;
                if (comp == null || !comp.HasFrozenCaptive)
                {
                    return;
                }

                if (duelElapsedTicks == 60 || duelElapsedTicks == 120 || duelElapsedTicks == 180)
                {
                    pawn.TakeDamage(new DamageInfo(
                        LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage,
                        clashWhiteDamage,
                        0.4f,
                        instigator: comp.parent));
                    LCAbnormalitiesHelper.PlaySoundAt(SnowQueenDefOf.LC_SnowQueen_Fight, TargetPlatform);
                    if (TargetPlatform != null && TargetPlatform.Map != null)
                    {
                        SnowQueenVfxUtility.SpawnFrostSplinterHit(TargetPlatform.Map, TargetPlatform.DrawPos);
                    }
                }

                if (pawn.Dead || pawn.Downed || pawn.InMentalState)
                {
                    duelResolved = true;
                    comp.ResolveDuelDefeat(pawn);
                    EndJobWith(JobCondition.Incompletable);
                }
            });
            yield return duelWait;

            Toil finish = ToilMaker.MakeToil("ResolveSnowQueenDuel");
            finish.initAction = delegate
            {
                duelResolved = true;
                SnowComp?.ResolveDuel(pawn);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }

    /// <summary>霜之碎片 E.G.O 长矛伤害处理器：命中存活单位时叠加冰霜迟缓状态并绽放霜裂特效。</summary>
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
                if (targetPawn.MapHeld != null && targetPawn.Spawned)
                {
                    SnowQueenVfxUtility.SpawnFrostSplinterHit(targetPawn.MapHeld, targetPawn.DrawPos);
                }
            }

            return result;
        }
    }

    public class HediffCompProperties_SnowQueenKiss : HediffCompProperties
    {
        public HediffCompProperties_SnowQueenKiss()
        {
            compClass = typeof(HediffComp_SnowQueenKiss);
        }
    }

    /// <summary>保持“冰雪女皇的亲吻”随行冰晶碎瓣视觉特效同步存在与消散。</summary>
    public class HediffComp_SnowQueenKiss : HediffComp
    {
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            SnowQueenVfxUtility.EnsureFirstKissMark(Pawn);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn != null && Pawn.Spawned && Pawn.IsHashIntervalTick(30))
            {
                SnowQueenVfxUtility.EnsureFirstKissMark(Pawn);
            }
        }
    }

    public class HediffCompProperties_FrostSlowVfx : HediffCompProperties
    {
        public HediffCompProperties_FrostSlowVfx()
        {
            compClass = typeof(HediffComp_FrostSlowVfx);
        }
    }

    /// <summary>受“霜之碎片”寒气迟缓期间持续散发淡蓝冰雾微粒。</summary>
    public class HediffComp_FrostSlowVfx : HediffComp
    {
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || !Pawn.Spawned || Pawn.Map == null || Pawn.Map.flecks == null || !Pawn.IsHashIntervalTick(18))
            {
                return;
            }

            Vector3 pos = Pawn.DrawPos + new Vector3(Rand.Range(-0.25f, 0.25f), 0f, Rand.Range(-0.15f, 0.35f));
            if (pos.ToIntVec3().InBounds(Pawn.Map))
            {
                FleckMaker.ThrowDustPuffThick(pos, Pawn.Map, Rand.Range(0.5f, 0.85f), new Color(0.62f, 0.92f, 1.0f, 0.45f));
            }
        }
    }
}
