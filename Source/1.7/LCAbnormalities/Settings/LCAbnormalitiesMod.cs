using LCAnomalyCore.Util;
using UnityEngine;
using Verse;

namespace LCAbnormalities.Settings
{
    /// <summary>
    /// LC Abnormalities 统一 Mod 入口与设置窗口绘制。
    /// </summary>
    public class LCAbnormalitiesMod : Mod
    {
        public static Setting_LCAbnormalities Settings { get; private set; }

        private Vector2 scrollPosition;
        private float scrollViewHeight = 600f;

        public LCAbnormalitiesMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<Setting_LCAbnormalities>();
        }

        public override string SettingsCategory()
        {
            return "Setting_LCAbnormalities_Label".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (Settings == null)
            {
                return;
            }

            Rect outRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height);
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, Mathf.Max(scrollViewHeight, outRect.height));

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            try
            {
                DrawExtractionSection(listing);
                listing.Gap(12f);
                DrawMeatLanternSection(listing);
                listing.Gap(12f);
                DrawPortraitSection(listing);
                listing.Gap(12f);
                DrawToolAbnormalitiesSection(listing);
                listing.Gap(12f);
                DrawBurrowingHeavenSection(listing);
                listing.Gap(12f);
                DrawNothingThereSection(listing);
                listing.Gap(16f);

                if (listing.ButtonText("LCAbn_Settings_ResetDefaults".Translate(), widthPct: 0.3f))
                {
                    Settings.ResetToDefaults();
                }

                scrollViewHeight = listing.CurHeight + 24f;
            }
            finally
            {
                listing.End();
                Widgets.EndScrollView();
            }

            base.DoSettingsWindowContents(inRect);
        }

        private static void DrawExtractionSection(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("LCAbn_Settings_ExtractionHeader".Translate());
            Text.Font = GameFont.Small;

            using (new TextBlock(ColoredText.SubtleGrayColor))
            {
                listing.Label("LCAbn_Settings_ExtractionNote".Translate());
            }

            listing.GapLine(6f);
            listing.CheckboxLabeled("LCAbn_Settings_EnableOneSin".Translate(), ref Settings.EnableOneSin);
            listing.CheckboxLabeled("LCAbn_Settings_EnableSevenSin".Translate(), ref Settings.EnableSevenSinEntities);
            listing.CheckboxLabeled("LCAbn_Settings_EnableWeCanChangeAnything".Translate(), ref Settings.EnableWeCanChangeAnything);
            listing.CheckboxLabeled("LCAbn_Settings_EnableOldBelief".Translate(), ref Settings.EnableOldBeliefAndPromise);
            listing.CheckboxLabeled("LCAbn_Settings_EnableMeatLantern".Translate(), ref Settings.EnableMeatLantern);
            listing.CheckboxLabeled("LCAbn_Settings_EnableLaetitia".Translate(), ref Settings.EnableLaetitia);
            listing.CheckboxLabeled("LCAbn_Settings_EnablePortrait".Translate(), ref Settings.EnablePortraitOfAnotherWorld);
            listing.CheckboxLabeled("LCAbn_Settings_EnableBurrowingHeaven".Translate(), ref Settings.EnableBurrowingHeaven);
            listing.CheckboxLabeled("LCAbn_Settings_EnableFleshIdol".Translate(), ref Settings.EnableFleshIdol);
            listing.CheckboxLabeled("LCAbn_Settings_EnableNothingThere".Translate(), ref Settings.EnableNothingThere);
            listing.CheckboxLabeled("LCAbn_Settings_EnableFairyFestival".Translate(), ref Settings.EnableFairyFestival);
            listing.CheckboxLabeled("LCAbn_Settings_EnableWellCheers".Translate(), ref Settings.EnableWellCheers);
            listing.CheckboxLabeled("LCAbn_Settings_EnablePunishingBird".Translate(), ref Settings.EnablePunishingBird);
            listing.CheckboxLabeled("LCAbn_Settings_EnableVoidDream".Translate(), ref Settings.EnableVoidDream);
            listing.CheckboxLabeled("LCAbn_Settings_EnableHeartOfAspiration".Translate(), ref Settings.EnableHeartOfAspiration);
            listing.CheckboxLabeled("LCAbn_Settings_EnableScorchedGirl".Translate(), ref Settings.EnableScorchedGirl);
            listing.CheckboxLabeled("LCAbn_Settings_EnableDerFreischutz".Translate(), ref Settings.EnableDerFreischutz);
            listing.CheckboxLabeled("LCAbn_Settings_EnableFuneralOfDeadButterflies".Translate(), ref Settings.EnableFuneralOfDeadButterflies);
            listing.CheckboxLabeled("LCAbn_Settings_EnableSnowQueen".Translate(), ref Settings.EnableSnowQueen);
            listing.CheckboxLabeled("LCAbn_Settings_EnableNotesFromCrazedResearcher".Translate(), ref Settings.EnableNotesFromCrazedResearcher);
            listing.CheckboxLabeled("LCAbn_Settings_EnableAllAroundHelper".Translate(), ref Settings.EnableAllAroundHelper);
            listing.CheckboxLabeled("LCAbn_Settings_EnableJudgementBird".Translate(), ref Settings.EnableJudgementBird);
            listing.CheckboxLabeled("LCAbn_Settings_EnableBigBird".Translate(), ref Settings.EnableBigBird);
            listing.CheckboxLabeled("LCAbn_Settings_EnableLittleRedRidingHoodedMercenary".Translate(), ref Settings.EnableLittleRedRidingHoodedMercenary);
            listing.CheckboxLabeled("LCAbn_Settings_EnableBackwardClock".Translate(), ref Settings.EnableBackwardClock);
            listing.CheckboxLabeled("LCAbn_Settings_EnableSchadenfreude".Translate(), ref Settings.EnableSchadenfreude);
        }

        private static void DrawMeatLanternSection(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("Setting_MeatLantern_Label".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            listing.CheckboxLabeled("LC_MeatLantern_ShowVampireText".Translate(), ref Settings.If_ShowVampireText);
            if (Settings.If_ShowVampireText)
            {
                listing.CheckboxLabeled("LC_MeatLantern_ShowVampireHealPartText".Translate(), ref Settings.If_ShowVampireHealPartText, tabIn: 18f);
            }
            listing.CheckboxLabeled("LC_MeatLantern_ShowVampireHealVFX".Translate(), ref Settings.If_ShowVampireHealVFX);
        }

        private static void DrawBurrowingHeavenSection(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("LCAbn_Settings_BurrowingHeavenHeader".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            listing.CheckboxLabeled(
                "LCAbn_Settings_BurrowingHeavenTeleport".Translate(),
                ref Settings.BurrowingHeavenAllowUnwatchedTeleport,
                "LCAbn_Settings_BurrowingHeavenTeleportDesc".Translate());

            string multLabel = "LCAbn_Settings_BurrowingHeavenGrace".Translate(Settings.BurrowingHeavenSightGraceMultiplier.ToString("0.00"));
            Settings.BurrowingHeavenSightGraceMultiplier = Mathf.Clamp(
                listing.SliderLabeled(multLabel, Settings.BurrowingHeavenSightGraceMultiplier, 0.5f, 3.0f,
                    tooltip: "LCAbn_Settings_BurrowingHeavenGraceDesc".Translate()),
                0.5f, 3.0f);
            Settings.BurrowingHeavenSightGraceMultiplier = Mathf.Round(Settings.BurrowingHeavenSightGraceMultiplier * 20f) / 20f;
        }

        private static void DrawPortraitSection(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("LCAbn_Settings_PortraitHeader".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            listing.CheckboxLabeled(
                "LCAbn_Settings_PortraitAllowNonColonist".Translate(),
                ref Settings.PortraitAllowNonColonistSubstitute,
                "LCAbn_Settings_PortraitAllowNonColonistDesc".Translate());

            string multLabel = "LCAbn_Settings_PortraitTransferMult".Translate(Settings.PortraitDamageTransferMultiplier.ToString("0.00"));
            Settings.PortraitDamageTransferMultiplier = Mathf.Clamp(
                listing.SliderLabeled(multLabel, Settings.PortraitDamageTransferMultiplier, 0.5f, 2.5f,
                    tooltip: "LCAbn_Settings_PortraitTransferMultDesc".Translate()),
                0.5f, 2.5f);
            Settings.PortraitDamageTransferMultiplier = Mathf.Round(Settings.PortraitDamageTransferMultiplier * 20f) / 20f;
        }

        private static void DrawToolAbnormalitiesSection(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("LCAbn_Settings_ToolAbnormalitiesHeader".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            listing.CheckboxLabeled(
                "LCAbn_Settings_IronMaidenAllowCancel".Translate(),
                ref Settings.WeCanChangeAnythingAllowCancel,
                "LCAbn_Settings_IronMaidenAllowCancelDesc".Translate());

            listing.CheckboxLabeled(
                "LCAbn_Settings_OldBeliefDestroyEgo".Translate(),
                ref Settings.OldBeliefCanDestroyEgoWeapon,
                "LCAbn_Settings_OldBeliefDestroyEgoDesc".Translate());
        }

        private static void DrawNothingThereSection(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("LCAbn_Settings_NothingThereHeader".Translate());
            Text.Font = GameFont.Small;
            listing.GapLine(6f);

            listing.CheckboxLabeled(
                "LCAbn_Settings_NothingThereAllowDisguise".Translate(),
                ref Settings.NothingThereAllowDisguiseForm,
                "LCAbn_Settings_NothingThereAllowDisguiseDesc".Translate());

            string hatchLabel = "LCAbn_Settings_NothingThereHatchSeconds".Translate(Settings.NothingThereCocoonHatchSeconds);
            Settings.NothingThereCocoonHatchSeconds = Mathf.RoundToInt(listing.SliderLabeled(
                hatchLabel,
                Settings.NothingThereCocoonHatchSeconds,
                15f,
                60f,
                tooltip: "LCAbn_Settings_NothingThereHatchSecondsDesc".Translate()));
        }
    }

    /// <summary>合集包启动引导：向 Core 注册异想体抽取池过滤器。</summary>
    [StaticConstructorOnStartup]
    public static class LCAbnormalitiesBootstrap
    {
        static LCAbnormalitiesBootstrap()
        {
            LCAbnormalities.Shared.LCAbnormalitiesEvents.EnsureRegistered();
            ExtractUtil.RegisterEligibilityFilter(def => LCAbnormalitiesMod.Settings?.IsDefEnabledForExtraction(def) ?? true);
        }
    }
}
