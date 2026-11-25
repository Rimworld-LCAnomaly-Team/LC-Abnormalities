using System.Collections.Generic;
using LCAbnormalities.Shared;
using LCAnomalyCore.Buildings;
using LCAnomalyCore.Comp.Pawns;
using LCAnomalyCore.Util;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace LCAbnormalities.WellCheers
{
    /// <summary>韦尔奇乐牌汽水口味/颜色状态：本能→樱桃红，洞察→深海蓝，沟通/压迫→暮紫。</summary>
    public enum WellCheersFlavor
    {
        Purple = 0,
        Red = 1,
        Blue = 2
    }

    [DefOf]
    public static class WellCheersDefOf
    {
        public static HediffDef LC_WellCheers_Red;
        public static HediffDef LC_WellCheers_Blue;
        public static HediffDef LC_WellCheers_Purple;
        public static HediffDef LC_WellCheers_DeepSleep;
        public static SoundDef LC_WellCheers_Drink;
        public static SoundDef LC_WellCheers_Drink_Male;
        public static SoundDef LC_WellCheers_Drink_Female;
        public static SoundDef LC_WellCheers_Dispense;
        public static SoundDef LC_WellCheers_Sleep;
        public static SoundDef LC_WellCheers_Dance;
        public static SoundDef LC_WellCheers_WorkLoop;
        public static JobDef LC_DrinkWellCheers;
        public static ThingDef LC_FX_WellCheersSeaWave;
        public static ThingDef LC_FX_WellCheersBubble;
        public static ThingDef LC_WellCheersMuzzleFizzFX;
        public static ThingDef LC_WellCheersSodaSplashFX;
        public static ThingDef LC_WellCheersHealMistFX;
        public static ThingDef LC_SodaSkillRunnerFX;
        public static AnimationDef WellCheers_Idle;
        public static AnimationDef WellCheers_Work;
        public static AnimationDef WellCheers_Dispense;
        public static AnimationDef WellCheers_Abduct;

        static WellCheersDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WellCheersDefOf));
        }
    }

    public class CompProperties_WellCheers : CompProperties_LCAbnormality
    {
        /// <summary>右键直接饮用汽水的独立冷却时长（Tick，默认 15000 = 6 游戏小时）。</summary>
        public int directDrinkCooldownTicks = 15000;

        /// <summary>右键直接饮用汽水的交互持续时长（Tick）。</summary>
        public int directDrinkDurationTicks = 150;

        /// <summary>未启用四维属性的普通殖民者直接饮用时的基础成功率。</summary>
        public float directDrinkBaseSuccessChance = 0.82f;

        public CompProperties_WellCheers()
        {
            compClass = typeof(CompWellCheers);
        }
    }

    /// <summary>
    /// F-05-52 韦尔奇乐牌汽水核心组件：
    /// 1. 保留标准 ZAYIN 异想体四维工作/产 PE-Box/提取苏打 E.G.O 机制；
    /// 2. 对齐脑叶公司原作口味规则：本能→红罐(外伤)、洞察→蓝罐(精神)、沟通/压迫→紫罐(双效)；
    /// 3. 支持收容平台右键直接交互与 Gizmo 点选饮用汽水（含独立冷却、双虾舞动与差结果海浪渔船演出）。
    /// </summary>
    public class CompWellCheers : CompLCAbnormality
    {
        private WellCheersFlavor currentFlavor = WellCheersFlavor.Purple;
        private EAnomalyWorkType lastStudyWorkType = EAnomalyWorkType.Attachment;
        private int lastDirectDrinkTick = -999999;
        private int canVisibleUntilTick = -1;

        public CompProperties_WellCheers PropsWellCheers => (CompProperties_WellCheers)props;

        public LC_WellCheersPawn WellCheersPawn => parent as LC_WellCheersPawn;

        public WellCheersFlavor CurrentFlavor => currentFlavor;

        public int DirectDrinkCooldownTicksRemaining
        {
            get
            {
                if (PropsWellCheers.directDrinkCooldownTicks <= 0 || Find.TickManager == null)
                {
                    return 0;
                }

                int elapsed = Find.TickManager.TicksGame - lastDirectDrinkTick;
                return Mathf.Max(0, PropsWellCheers.directDrinkCooldownTicks - elapsed);
            }
        }

        public bool IsDirectDrinkOnCooldown => DirectDrinkCooldownTicksRemaining > 0;

        /// <summary>工作、直接交互或刚刚出罐期间在 FREE! 出货槽显示对应颜色汽水罐。</summary>
        public bool ShouldShowDispensedCan
        {
            get
            {
                if (Find.TickManager == null)
                {
                    return true;
                }

                int now = Find.TickManager.TicksGame;
                if (now < canVisibleUntilTick)
                {
                    return true;
                }

                LC_WellCheersPawn pawn = WellCheersPawn;
                return pawn != null && (pawn.IsWorkingNow || pawn.IsDispensingNow);
            }
        }

        public static WellCheersFlavor MapWorkTypeToFlavor(EAnomalyWorkType workType)
        {
            switch (workType)
            {
                case EAnomalyWorkType.Instinct:
                    return WellCheersFlavor.Red;
                case EAnomalyWorkType.Insight:
                    return WellCheersFlavor.Blue;
                default:
                    return WellCheersFlavor.Purple;
            }
        }

        public static EAnomalyWorkType MapFlavorToWorkType(WellCheersFlavor flavor)
        {
            switch (flavor)
            {
                case WellCheersFlavor.Red:
                    return EAnomalyWorkType.Instinct;
                case WellCheersFlavor.Blue:
                    return EAnomalyWorkType.Insight;
                default:
                    return EAnomalyWorkType.Attachment;
            }
        }

        public static string GetFlavorLabel(WellCheersFlavor flavor)
        {
            switch (flavor)
            {
                case WellCheersFlavor.Red:
                    return "LC_WellCheers_FlavorRed".Translate();
                case WellCheersFlavor.Blue:
                    return "LC_WellCheers_FlavorBlue".Translate();
                default:
                    return "LC_WellCheers_FlavorPurple".Translate();
            }
        }

        public static Color GetFlavorColor(WellCheersFlavor flavor)
        {
            switch (flavor)
            {
                case WellCheersFlavor.Red:
                    return new Color(1f, 0.36f, 0.36f, 1f);
                case WellCheersFlavor.Blue:
                    return new Color(0.28f, 0.88f, 0.96f, 1f);
                default:
                    return new Color(0.66f, 0.40f, 1f, 1f);
            }
        }

        public void SetFlavor(WellCheersFlavor flavor)
        {
            currentFlavor = flavor;
        }

        #region 标准工作回调

        public override void Notify_StudyStart(Pawn studier)
        {
            base.Notify_StudyStart(studier);
            Building_AbnormalityHoldingPlatform platform = HoldingPlaformTargetComp?.HeldPlatform;
            if (platform != null)
            {
                lastStudyWorkType = platform.CurWorkType;
                currentFlavor = MapWorkTypeToFlavor(lastStudyWorkType);
            }

            WellCheersPawn?.NotifyWorkingTick();
            LCAbnormalitiesHelper.PlaySoundAt(WellCheersDefOf.LC_WellCheers_Dispense, parent);
        }

        public override void Notify_Studying(Pawn studier)
        {
            base.Notify_Studying(studier);
            WellCheersPawn?.NotifyWorkingTick();
        }

        public override bool Notify_StudyInterval(CompPawnStatus studier, EAnomalyWorkType workType)
        {
            lastStudyWorkType = workType;
            currentFlavor = MapWorkTypeToFlavor(workType);
            return base.Notify_StudyInterval(studier, workType);
        }

        protected override void StudyEvent_Good(Pawn studier)
        {
            base.StudyEvent_Good(studier);
            ServeCan(studier, MapWorkTypeToFlavor(lastStudyWorkType), scale: 1f);
        }

        protected override void StudyEvent_Normal(Pawn studier)
        {
            base.StudyEvent_Normal(studier);
            ServeCan(studier, MapWorkTypeToFlavor(lastStudyWorkType), scale: 0.7f);
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            base.StudyEvent_Bad(studier);
            ApplyBadDrink(studier);
        }

        #endregion 标准工作回调

        #region 直接右键交互与 Gizmo

        public AcceptanceReport CanDirectDrink(Pawn pawn)
        {
            Building_AbnormalityHoldingPlatform platform = HoldingPlaformTargetComp?.HeldPlatform;
            if (platform == null || !platform.Spawned || platform.Map == null)
            {
                return false;
            }

            if (pawn == null || pawn.Dead || pawn.Downed || !pawn.RaceProps.Humanlike)
            {
                return false;
            }

            if (IsDirectDrinkOnCooldown)
            {
                return "LC_WellCheers_DrinkOnCooldown".Translate(DirectDrinkCooldownTicksRemaining.ToStringTicksToPeriod());
            }

            return true;
        }

        public bool TryOrderDirectDrink(Pawn pawn, WellCheersFlavor flavor)
        {
            Building_AbnormalityHoldingPlatform platform = HoldingPlaformTargetComp?.HeldPlatform;
            if (platform == null || WellCheersDefOf.LC_DrinkWellCheers == null || !CanDirectDrink(pawn).Accepted)
            {
                return false;
            }

            if (!pawn.CanReach(platform, PathEndMode.Touch, Danger.Deadly) || !pawn.CanReserve(platform))
            {
                return false;
            }

            Job job = JobMaker.MakeJob(WellCheersDefOf.LC_DrinkWellCheers, platform);
            job.count = (int)flavor;
            return pawn.jobs?.TryTakeOrderedJob(job, JobTag.Misc) ?? false;
        }

        public void Notify_DirectDrinkStart(Pawn drinker, WellCheersFlavor flavor)
        {
            currentFlavor = flavor;
            if (parent != null)
            {
                AbnormalityUtility.Discover(parent.def);
            }

            WellCheersPawn?.NotifyWorkingTick();
            LCAbnormalitiesHelper.PlaySoundAt(WellCheersDefOf.LC_WellCheers_Dispense, parent);
        }

        public void Notify_DirectDrinkTick(Pawn drinker)
        {
            WellCheersPawn?.NotifyWorkingTick();
        }

        public void CompleteDirectDrink(Pawn drinker, WellCheersFlavor flavor)
        {
            if (drinker == null || drinker.Dead)
            {
                return;
            }

            currentFlavor = flavor;
            lastDirectDrinkTick = Find.TickManager?.TicksGame ?? 0;
            if (parent != null)
            {
                AbnormalityUtility.Discover(parent.def);
            }

            float successChance = ComputeDirectDrinkSuccessChance(drinker, flavor);
            if (Rand.Chance(successChance))
            {
                float scale = Rand.Chance(0.6f) ? 0.9f : 0.7f;
                ServeCan(drinker, flavor, scale);
            }
            else
            {
                ApplyBadDrink(drinker);
            }
        }

        private float ComputeDirectDrinkSuccessChance(Pawn drinker, WellCheersFlavor flavor)
        {
            CompPawnStatus status = drinker?.TryGetComp<CompPawnStatus>();
            if (status != null && status.Enabled)
            {
                float rate = CheckStudierSkillRequire(status, MapFlavorToWorkType(flavor));
                return Mathf.Clamp(rate + 0.12f, 0.55f, 0.94f);
            }

            float unlockBonus = StudiableComp?.GetWorkSuccessRateOffset() ?? 0f;
            return Mathf.Clamp(PropsWellCheers.directDrinkBaseSuccessChance + unlockBonus, 0.60f, 0.92f);
        }

        public override IEnumerable<FloatMenuOption> GetPlatformFloatMenuOptions(Pawn selPawn)
        {
            Building_AbnormalityHoldingPlatform platform = HoldingPlaformTargetComp?.HeldPlatform;
            if (selPawn == null || platform == null || !platform.Spawned || platform.Map == null || !selPawn.RaceProps.Humanlike)
            {
                yield break;
            }

            AcceptanceReport canDrink = CanDirectDrink(selPawn);
            if (!canDrink.Accepted)
            {
                string label = canDrink.Reason.NullOrEmpty()
                    ? "LC_WellCheers_DrinkMenuBase".Translate(parent.LabelCap).Resolve()
                    : "LC_WellCheers_DrinkMenuDisabled".Translate(parent.LabelCap, canDrink.Reason).Resolve();
                yield return new FloatMenuOption(label, null);
                yield break;
            }

            if (!selPawn.CanReach(platform, PathEndMode.Touch, Danger.Deadly))
            {
                yield return new FloatMenuOption(
                    "LC_WellCheers_DrinkMenuDisabled".Translate(parent.LabelCap, "NoPath".Translate()).Resolve(),
                    null);
                yield break;
            }

            if (!selPawn.CanReserve(platform))
            {
                yield return new FloatMenuOption(
                    "LC_WellCheers_DrinkMenuDisabled".Translate(parent.LabelCap, "Reserved".Translate()).Resolve(),
                    null);
                yield break;
            }

            yield return MakeFlavorFloatOption(selPawn, platform, WellCheersFlavor.Red, "LC_WellCheers_DrinkRedOption");
            yield return MakeFlavorFloatOption(selPawn, platform, WellCheersFlavor.Blue, "LC_WellCheers_DrinkBlueOption");
            yield return MakeFlavorFloatOption(selPawn, platform, WellCheersFlavor.Purple, "LC_WellCheers_DrinkPurpleOption");
        }

        private FloatMenuOption MakeFlavorFloatOption(Pawn selPawn, Building_AbnormalityHoldingPlatform platform, WellCheersFlavor flavor, string labelKey)
        {
            return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(
                    labelKey.Translate(),
                    delegate
                    {
                        TryOrderDirectDrink(selPawn, flavor);
                    },
                    MenuOptionPriority.Default,
                    null,
                    platform),
                selPawn,
                platform);
        }

        public override IEnumerable<Gizmo> GetPlatformGizmos()
        {
            Building_AbnormalityHoldingPlatform platform = HoldingPlaformTargetComp?.HeldPlatform;
            if (platform == null || !platform.Spawned || platform.Map == null)
            {
                yield break;
            }

            WellCheersFlavor platformFlavor = MapWorkTypeToFlavor(platform.CurWorkType);
            Command_Action cmd = new Command_Action
            {
                defaultLabel = "LC_WellCheers_DrinkGizmoLabel".Translate(),
                defaultDesc = "LC_WellCheers_DrinkGizmoDesc".Translate(GetFlavorLabel(platformFlavor)),
                icon = ContentFinder<Texture2D>.Get("UI/CodexEntries/WellCheers", reportFailure: false) ?? BaseContent.BadTex,
                action = () => OpenDirectDrinkColonistMenu(platform, platformFlavor)
            };

            if (IsDirectDrinkOnCooldown)
            {
                cmd.Disable("LC_WellCheers_DrinkOnCooldown".Translate(DirectDrinkCooldownTicksRemaining.ToStringTicksToPeriod()));
            }

            yield return cmd;
        }

        private void OpenDirectDrinkColonistMenu(Building_AbnormalityHoldingPlatform platform, WellCheersFlavor flavor)
        {
            Map map = platform?.Map;
            if (map?.mapPawns?.FreeColonistsSpawned == null)
            {
                return;
            }

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            List<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn c = colonists[i];
                if (c == null || c.Dead || c.Downed || !c.CanReach(platform, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }

                Pawn captured = c;
                options.Add(new FloatMenuOption(
                    "LC_WellCheers_DrinkColonistOption".Translate(captured.LabelShortCap, GetFlavorLabel(flavor)),
                    () => TryOrderDirectDrink(captured, flavor)));
            }

            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("NoColonistAbleToReachTarget".Translate(), null));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override string GetPlatformInspectString()
        {
            string flavorName = GetFlavorLabel(currentFlavor);
            if (IsDirectDrinkOnCooldown)
            {
                return "LC_WellCheers_InspectCooldown".Translate(flavorName, DirectDrinkCooldownTicksRemaining.ToStringTicksToPeriod());
            }

            return "LC_WellCheers_InspectReady".Translate(flavorName);
        }

        #endregion 直接右键交互与 Gizmo

        #region 饮用结算与演出特效

        private void ServeCan(Pawn drinker, WellCheersFlavor flavor, float scale)
        {
            if (drinker == null || drinker.Dead)
            {
                return;
            }

            currentFlavor = flavor;
            if (Find.TickManager != null)
            {
                canVisibleUntilTick = Find.TickManager.TicksGame + 180;
            }

            LCAbnormalitiesHelper.PlaySoundAt(WellCheersDefOf.LC_WellCheers_Dispense, parent);
            SoundDef drinkSound = drinker.gender == Gender.Female
                ? (WellCheersDefOf.LC_WellCheers_Drink_Female ?? WellCheersDefOf.LC_WellCheers_Drink)
                : (WellCheersDefOf.LC_WellCheers_Drink_Male ?? WellCheersDefOf.LC_WellCheers_Drink);
            LCAbnormalitiesHelper.PlaySoundAt(drinkSound, drinker);
            WellCheersPawn?.PlayDispenseAnimation();
            SpawnBubbleFx(flavor);

            switch (flavor)
            {
                case WellCheersFlavor.Red:
                    LCAbnormalitiesHelper.HealInjuries(drinker, 18f * scale);
                    LCAbnormalitiesHelper.ApplyOrRefreshHediff(drinker, WellCheersDefOf.LC_WellCheers_Red, 1f);
                    Messages.Message("LC_WellCheers_RedCanMsg".Translate(drinker.LabelShortCap), drinker, MessageTypeDefOf.PositiveEvent, historical: false);
                    break;
                case WellCheersFlavor.Blue:
                    LCAbnormalitiesHelper.RestoreMentalHealth(drinker, 0.45f * scale, 0.10f * scale);
                    LCAbnormalitiesHelper.ApplyOrRefreshHediff(drinker, WellCheersDefOf.LC_WellCheers_Blue, 1f);
                    Messages.Message("LC_WellCheers_BlueCanMsg".Translate(drinker.LabelShortCap), drinker, MessageTypeDefOf.PositiveEvent, historical: false);
                    break;
                default:
                    LCAbnormalitiesHelper.HealInjuries(drinker, 10f * scale);
                    LCAbnormalitiesHelper.RestoreMentalHealth(drinker, 0.25f * scale, 0.06f * scale);
                    LCAbnormalitiesHelper.ApplyOrRefreshHediff(drinker, WellCheersDefOf.LC_WellCheers_Purple, 1f);
                    Messages.Message("LC_WellCheers_PurpleCanMsg".Translate(drinker.LabelShortCap), drinker, MessageTypeDefOf.PositiveEvent, historical: false);
                    break;
            }
        }

        private void ApplyBadDrink(Pawn drinker)
        {
            if (drinker == null || drinker.Dead)
            {
                return;
            }

            LCAbnormalitiesHelper.PlaySoundAt(WellCheersDefOf.LC_WellCheers_Sleep, parent);
            WellCheersPawn?.PlayAbductAnimation();
            SpawnSeaWaveFx();

            LCAbnormalitiesHelper.ApplyOrRefreshHediff(drinker, WellCheersDefOf.LC_WellCheers_DeepSleep, 1f);
            LCAbnormalitiesHelper.ApplyOrRefreshHediff(drinker, RimWorld.HediffDefOf.Hypothermia, 0.25f);
            drinker.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 15f, instigator: parent));

            Messages.Message(
                "LC_WellCheers_BadDrinkMsg".Translate(drinker.LabelShortCap),
                drinker,
                MessageTypeDefOf.NegativeEvent,
                historical: false);
        }

        private void SpawnSeaWaveFx()
        {
            Map map = parent?.MapHeld;
            IntVec3 pos = parent?.PositionHeld ?? IntVec3.Invalid;
            if (map == null || !pos.InBounds(map) || WellCheersDefOf.LC_FX_WellCheersSeaWave == null)
            {
                return;
            }

            GenSpawn.Spawn(WellCheersDefOf.LC_FX_WellCheersSeaWave, pos, map);
        }

        private void SpawnBubbleFx(WellCheersFlavor flavor)
        {
            Map map = parent?.MapHeld;
            IntVec3 pos = parent?.PositionHeld ?? IntVec3.Invalid;
            if (map == null || !pos.InBounds(map) || WellCheersDefOf.LC_FX_WellCheersBubble == null)
            {
                return;
            }

            if (GenSpawn.Spawn(WellCheersDefOf.LC_FX_WellCheersBubble, pos, map) is LC_FX_WellCheersBubble fx)
            {
                fx.SetFlavor(flavor);
            }
        }

        #endregion 饮用结算与演出特效

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref currentFlavor, "wcCurrentFlavor", WellCheersFlavor.Purple);
            Scribe_Values.Look(ref lastStudyWorkType, "wcLastStudyWorkType", EAnomalyWorkType.Attachment);
            Scribe_Values.Look(ref lastDirectDrinkTick, "wcLastDirectDrinkTick", -999999);
            Scribe_Values.Look(ref canVisibleUntilTick, "wcCanVisibleUntilTick", -1);
        }
    }

    /// <summary>韦尔奇乐牌汽水异想体 Pawn：驱动待机、工作舞动、出罐欢呼与差结果双虾摆臂多部件动画状态机。</summary>
    public class LC_WellCheersPawn : LC_StandardEntityPawn
    {
        private int oneShotAnimEndTick = -1;
        private bool oneShotIsAbduct;
        private int lastWorkingTick = -999999;

        public CompWellCheers WellCheersComp => EntityComp as CompWellCheers;

        public bool OneShotPlaying => Find.TickManager != null && oneShotAnimEndTick > Find.TickManager.TicksGame;

        public bool IsDispensingNow => OneShotPlaying && !oneShotIsAbduct;

        public bool IsWorkingNow => Find.TickManager != null && (Find.TickManager.TicksGame - lastWorkingTick) <= 6;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref oneShotAnimEndTick, "wcOneShotAnimEndTick", -1);
            Scribe_Values.Look(ref oneShotIsAbduct, "wcOneShotIsAbduct", false);
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Destroyed)
            {
                UpdateAnimation();
            }
        }

        public override void TickHolded()
        {
            base.TickHolded();
            UpdateAnimation();
        }

        public void NotifyWorkingTick()
        {
            if (Find.TickManager != null)
            {
                lastWorkingTick = Find.TickManager.TicksGame;
            }

            if (!OneShotPlaying && WellCheersDefOf.WellCheers_Work != null)
            {
                SetAnimationIfNeeded(WellCheersDefOf.WellCheers_Work);
            }
        }

        public void PlayDispenseAnimation()
        {
            AnimationDef anim = WellCheersDefOf.WellCheers_Dispense;
            if (anim == null || Find.TickManager == null)
            {
                return;
            }

            oneShotIsAbduct = false;
            oneShotAnimEndTick = Find.TickManager.TicksGame + anim.durationTicks;
            SetAnimationIfNeeded(anim);
        }

        public void PlayAbductAnimation()
        {
            AnimationDef anim = WellCheersDefOf.WellCheers_Abduct;
            if (anim == null || Find.TickManager == null)
            {
                return;
            }

            oneShotIsAbduct = true;
            oneShotAnimEndTick = Find.TickManager.TicksGame + anim.durationTicks;
            SetAnimationIfNeeded(anim);
        }

        private void UpdateAnimation()
        {
            if (Drawer?.renderer == null)
            {
                return;
            }

            if (oneShotAnimEndTick >= 0)
            {
                if (OneShotPlaying)
                {
                    AnimationDef target = oneShotIsAbduct ? WellCheersDefOf.WellCheers_Abduct : WellCheersDefOf.WellCheers_Dispense;
                    if (target != null)
                    {
                        SetAnimationIfNeeded(target);
                    }
                    return;
                }

                oneShotAnimEndTick = -1;
                oneShotIsAbduct = false;
            }

            if (IsWorkingNow && WellCheersDefOf.WellCheers_Work != null)
            {
                SetAnimationIfNeeded(WellCheersDefOf.WellCheers_Work);
                return;
            }

            if (WellCheersDefOf.WellCheers_Idle != null)
            {
                SetAnimationIfNeeded(WellCheersDefOf.WellCheers_Idle);
            }
        }

        private void SetAnimationIfNeeded(AnimationDef animation)
        {
            if (animation != null && Drawer?.renderer != null && Drawer.renderer.CurAnimation != animation)
            {
                Drawer.renderer.SetAnimation(animation);
            }
        }
    }

    /// <summary>殖民者直接走近收容平台从韦尔奇乐牌汽水机接汽水饮用的工作驱动。</summary>
    public class JobDriver_DrinkWellCheers : JobDriver
    {
        private Building_AbnormalityHoldingPlatform TargetPlatform => job.targetA.Thing as Building_AbnormalityHoldingPlatform;

        private CompWellCheers WellCheersComp => TargetPlatform?.HeldPawn?.TryGetComp<CompWellCheers>();

        private WellCheersFlavor SelectedFlavor =>
            job.count == (int)WellCheersFlavor.Red
                ? WellCheersFlavor.Red
                : (job.count == (int)WellCheersFlavor.Blue ? WellCheersFlavor.Blue : WellCheersFlavor.Purple);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetPlatform, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => WellCheersComp == null || !WellCheersComp.CanDirectDrink(pawn).Accepted);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            int duration = Mathf.Max(30, WellCheersComp?.PropsWellCheers?.directDrinkDurationTicks ?? 150);
            Toil drinkWait = Toils_General.WaitWith(TargetIndex.A, duration, useProgressBar: true, maintainPosture: false, maintainSleep: false, TargetIndex.A);
            drinkWait.initAction = delegate
            {
                WellCheersComp?.Notify_DirectDrinkStart(pawn, SelectedFlavor);
            };
            drinkWait.AddPreTickAction(delegate
            {
                WellCheersComp?.Notify_DirectDrinkTick(pawn);
            });
            drinkWait.PlaySustainerOrSound(() => WellCheersDefOf.LC_WellCheers_WorkLoop);
            yield return drinkWait;

            Toil finish = ToilMaker.MakeToil("CompleteDrinkWellCheers");
            finish.initAction = delegate
            {
                WellCheersComp?.CompleteDirectDrink(pawn, SelectedFlavor);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }

    /// <summary>
    /// 韦尔奇差结果深海浪潮与橙色远洋渔船演出特效：
    /// 还原脑叶公司原作中海浪漫过收容单元、伴随浪涛与海鸥鸣叫（WellCheers_Dance）且橙色捕虾渔船乘浪驶过的经典演出。
    /// </summary>
    [StaticConstructorOnStartup]
    public class LC_FX_WellCheersSeaWave : Thing
    {
        private const int DurationTicks = 240;
        private static readonly Material WaveMat = MaterialPool.MatFrom("Things/Pawn/WellCheers/FX/WellCheers_Wave", ShaderDatabase.Transparent);
        private static readonly Material ShipMat = MaterialPool.MatFrom("Things/Pawn/WellCheers/FX/WellCheers_Ship", ShaderDatabase.Transparent);

        private int elapsedTicks;
        private Sustainer seaAmbienceSustainer;

        protected override void Tick()
        {
            base.Tick();
            if (Spawned && Map != null && WellCheersDefOf.LC_WellCheers_Dance != null)
            {
                if (seaAmbienceSustainer == null || seaAmbienceSustainer.Ended)
                {
                    seaAmbienceSustainer = WellCheersDefOf.LC_WellCheers_Dance.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
                }
                seaAmbienceSustainer?.Maintain();
            }

            elapsedTicks++;
            if (elapsedTicks >= DurationTicks)
            {
                EndAmbience();
                Destroy(DestroyMode.Vanish);
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            EndAmbience();
            base.DeSpawn(mode);
        }

        private void EndAmbience()
        {
            if (seaAmbienceSustainer != null && !seaAmbienceSustainer.Ended)
            {
                seaAmbienceSustainer.End();
            }
            seaAmbienceSustainer = null;
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float t = Mathf.Clamp01(elapsedTicks / (float)DurationTicks);
            float alpha = ComputeEnvelopeAlpha(t, 0.16f, 0.25f);
            if (alpha <= 0.01f)
            {
                return;
            }

            float baseY = AltitudeLayer.MoteOverhead.AltitudeFor();

            // 1. 涌起的海浪层（上下起伏 + 横向涌动）
            if (WaveMat != null)
            {
                Material fadedWave = FadedMaterialPool.FadedVersionOf(WaveMat, alpha * 0.86f);
                float waveRise = Mathf.Sin(t * Mathf.PI) * 0.45f - 0.30f;
                float waveSwayX = Mathf.Sin(t * Mathf.PI * 5f) * 0.24f;
                Vector3 wavePos = new Vector3(drawLoc.x + waveSwayX, baseY, drawLoc.z + waveRise);
                Matrix4x4 waveMatrix = Matrix4x4.TRS(wavePos, Quaternion.identity, new Vector3(3.6f, 1f, 2.2f));
                GenDraw.DrawMeshNowOrLater(MeshPool.plane10, waveMatrix, fadedWave, drawNow: false);
            }

            // 2. 乘浪驶过的橙色远洋捕虾渔船（左→右航行并随浪摇摆）
            if (ShipMat != null)
            {
                Material fadedShip = FadedMaterialPool.FadedVersionOf(ShipMat, alpha * 0.96f);
                float shipX = Mathf.Lerp(-1.35f, 1.35f, t);
                float shipBobZ = Mathf.Sin(t * Mathf.PI * 6f) * 0.10f + Mathf.Sin(t * Mathf.PI) * 0.36f + 0.18f;
                float shipTilt = Mathf.Sin(t * Mathf.PI * 6f) * 8.5f;
                Vector3 shipPos = new Vector3(drawLoc.x + shipX, baseY + 0.04f, drawLoc.z + shipBobZ);
                Matrix4x4 shipMatrix = Matrix4x4.TRS(shipPos, Quaternion.AngleAxis(shipTilt, Vector3.up), new Vector3(2.15f, 1f, 1.2f));
                GenDraw.DrawMeshNowOrLater(MeshPool.plane10, shipMatrix, fadedShip, drawNow: false);
            }
        }

        private static float ComputeEnvelopeAlpha(float t, float fadeIn, float fadeOut)
        {
            if (t < fadeIn)
            {
                return Mathf.SmoothStep(0f, 1f, t / fadeIn);
            }
            if (t > 1f - fadeOut)
            {
                return Mathf.SmoothStep(0f, 1f, (1f - t) / fadeOut);
            }
            return 1f;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref elapsedTicks, "wcWaveElapsedTicks", 0);
        }
    }

    /// <summary>韦尔奇优/良出罐时的碳酸气泡闪耀特效（按红/蓝/紫口味染色）。</summary>
    [StaticConstructorOnStartup]
    public class LC_FX_WellCheersBubble : Thing
    {
        private const int DurationTicks = 65;
        private static readonly Material BubbleMatPurple = MaterialPool.MatFrom("Things/Pawn/WellCheers/FX/WellCheers_Bubble", ShaderDatabase.Transparent, CompWellCheers.GetFlavorColor(WellCheersFlavor.Purple));
        private static readonly Material BubbleMatRed = MaterialPool.MatFrom("Things/Pawn/WellCheers/FX/WellCheers_Bubble", ShaderDatabase.Transparent, CompWellCheers.GetFlavorColor(WellCheersFlavor.Red));
        private static readonly Material BubbleMatBlue = MaterialPool.MatFrom("Things/Pawn/WellCheers/FX/WellCheers_Bubble", ShaderDatabase.Transparent, CompWellCheers.GetFlavorColor(WellCheersFlavor.Blue));

        private int elapsedTicks;
        private WellCheersFlavor flavor = WellCheersFlavor.Purple;

        public void SetFlavor(WellCheersFlavor value)
        {
            flavor = value;
        }

        protected override void Tick()
        {
            base.Tick();
            elapsedTicks++;
            if (elapsedTicks >= DurationTicks)
            {
                Destroy(DestroyMode.Vanish);
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Material baseMat = flavor == WellCheersFlavor.Red
                ? BubbleMatRed
                : (flavor == WellCheersFlavor.Blue ? BubbleMatBlue : BubbleMatPurple);
            if (baseMat == null)
            {
                return;
            }

            float t = Mathf.Clamp01(elapsedTicks / (float)DurationTicks);
            float alpha = t < 0.2f ? (t / 0.2f) : (1f - (t - 0.2f) / 0.8f);
            alpha = Mathf.Clamp01(alpha) * 0.58f;
            if (alpha <= 0.01f)
            {
                return;
            }

            Material faded = FadedMaterialPool.FadedVersionOf(baseMat, alpha);
            float scale = Mathf.Lerp(1.6f, 2.75f, t);
            float riseZ = Mathf.Lerp(0.05f, 0.55f, t);
            Vector3 pos = new Vector3(drawLoc.x, AltitudeLayer.MoteOverhead.AltitudeFor(), drawLoc.z + riseZ);
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(scale, 1f, scale * 0.65f));
            GenDraw.DrawMeshNowOrLater(MeshPool.plane10, matrix, faded, drawNow: false);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref elapsedTicks, "wcBubbleElapsedTicks", 0);
            Scribe_Values.Look(ref flavor, "wcBubbleFlavor", WellCheersFlavor.Purple);
        }
    }

    /// <summary>苏打手枪 E.G.O 伤害处理器：命中时有 18% 概率为射手恢复少量伤口与精神损伤，并生成葡萄汽水舒缓雾气特效。</summary>
    public class DamageWorker_SodaHeal : LCAnomalyCore.DamageWorker.DamageWorker_FourColor
    {
        public override Verse.DamageWorker.DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            Verse.DamageWorker.DamageResult result = base.Apply(dinfo, victim);
            if (result.totalDamageDealt > 0f
                && dinfo.Instigator is Pawn attacker
                && !attacker.Dead
                && Rand.Chance(0.18f))
            {
                LCAbnormalitiesHelper.HealInjuries(attacker, 2f);
                LCAbnormalitiesHelper.RestoreMentalHealth(attacker, 0.05f);
                WellCheersVfxUtility.SpawnHealMist(attacker);
            }

            return result;
        }
    }
}
