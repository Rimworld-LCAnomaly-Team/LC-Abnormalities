using System;
using LCAnomalyCore.Defs;
using MeatLantern.Setting;
using UnityEngine;
using Verse;

namespace LCAbnormalities.Settings
{
    /// <summary>
    /// LC 异想体合集统一设置，继承旧版肉食提灯设置字段以保持配置兼容。
    /// </summary>
    public class Setting_LCAbnormalities : Setting_MeatLantern
    {
        public const int CurrentSettingsVersion = 1;

        private int settingsVersion;

        public bool EnableOneSin = true;
        public bool EnableMeatLantern = true;
        public bool EnableLaetitia = true;
        public bool EnableBurrowingHeaven = true;
        public bool EnablePortraitOfAnotherWorld = true;
        public bool EnableSevenSinEntities = true;
        public bool EnableFleshIdol = true;
        public bool EnableWeCanChangeAnything = true;
        public bool EnableOldBeliefAndPromise = true;
        public bool EnableNothingThere = true;

        public bool BurrowingHeavenAllowUnwatchedTeleport = true;
        public float BurrowingHeavenSightGraceMultiplier = 1.0f;

        public bool PortraitAllowNonColonistSubstitute = false;
        public float PortraitDamageTransferMultiplier = 1.15f;

        public bool WeCanChangeAnythingAllowCancel = false;
        public bool OldBeliefCanDestroyEgoWeapon = false;

        public bool NothingThereAllowDisguiseForm = true;
        public int NothingThereCocoonHatchSeconds = 30;

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref settingsVersion, "settingsVersion", 0);
            Scribe_Values.Look(ref EnableOneSin, "EnableOneSin", true);
            Scribe_Values.Look(ref EnableMeatLantern, "EnableMeatLantern", true);
            Scribe_Values.Look(ref EnableLaetitia, "EnableLaetitia", true);
            Scribe_Values.Look(ref EnableBurrowingHeaven, "EnableBurrowingHeaven", true);
            Scribe_Values.Look(ref EnablePortraitOfAnotherWorld, "EnablePortraitOfAnotherWorld", true);
            Scribe_Values.Look(ref EnableSevenSinEntities, "EnableSevenSinEntities", true);
            Scribe_Values.Look(ref EnableFleshIdol, "EnableFleshIdol", true);
            Scribe_Values.Look(ref EnableWeCanChangeAnything, "EnableWeCanChangeAnything", true);
            Scribe_Values.Look(ref EnableOldBeliefAndPromise, "EnableOldBeliefAndPromise", true);
            Scribe_Values.Look(ref EnableNothingThere, "EnableNothingThere", true);

            Scribe_Values.Look(ref BurrowingHeavenAllowUnwatchedTeleport, "BurrowingHeavenAllowUnwatchedTeleport", true);
            Scribe_Values.Look(ref BurrowingHeavenSightGraceMultiplier, "BurrowingHeavenSightGraceMultiplier", 1.0f);

            Scribe_Values.Look(ref PortraitAllowNonColonistSubstitute, "PortraitAllowNonColonistSubstitute", false);
            Scribe_Values.Look(ref PortraitDamageTransferMultiplier, "PortraitDamageTransferMultiplier", 1.15f);

            Scribe_Values.Look(ref WeCanChangeAnythingAllowCancel, "WeCanChangeAnythingAllowCancel", false);
            Scribe_Values.Look(ref OldBeliefCanDestroyEgoWeapon, "OldBeliefCanDestroyEgoWeapon", false);

            Scribe_Values.Look(ref NothingThereAllowDisguiseForm, "NothingThereAllowDisguiseForm", true);
            Scribe_Values.Look(ref NothingThereCocoonHatchSeconds, "NothingThereCocoonHatchSeconds", 30);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                BurrowingHeavenSightGraceMultiplier = Mathf.Clamp(BurrowingHeavenSightGraceMultiplier, 0.5f, 3.0f);
                PortraitDamageTransferMultiplier = Mathf.Clamp(PortraitDamageTransferMultiplier, 0.5f, 2.5f);
                NothingThereCocoonHatchSeconds = Mathf.Clamp(NothingThereCocoonHatchSeconds, 15, 60);
                if (settingsVersion < CurrentSettingsVersion)
                {
                    settingsVersion = CurrentSettingsVersion;
                }
            }
        }

        /// <summary>判断指定 Def 是否允许进入抽取池。</summary>
        public bool IsDefEnabledForExtraction(Def def)
        {
            string defName = def?.defName;
            if (string.IsNullOrEmpty(defName))
            {
                return true;
            }

            if (defName.IndexOf("OneSin", StringComparison.Ordinal) >= 0)
            {
                return EnableOneSin;
            }

            if (defName.IndexOf("MeatLantern", StringComparison.Ordinal) >= 0)
            {
                return EnableMeatLantern;
            }

            if (defName.IndexOf("Laetitia", StringComparison.Ordinal) >= 0)
            {
                return EnableLaetitia;
            }

            if (defName.IndexOf("BurrowingHeaven", StringComparison.Ordinal) >= 0)
            {
                return EnableBurrowingHeaven;
            }

            if (defName.IndexOf("PortraitOfAnotherWorld", StringComparison.Ordinal) >= 0)
            {
                return EnablePortraitOfAnotherWorld;
            }

            if (defName.IndexOf("FleshIdol", StringComparison.Ordinal) >= 0)
            {
                return EnableFleshIdol;
            }

            if (defName.IndexOf("WeCanChangeAnything", StringComparison.Ordinal) >= 0)
            {
                return EnableWeCanChangeAnything;
            }

            if (defName.IndexOf("OldBeliefAndPromise", StringComparison.Ordinal) >= 0)
            {
                return EnableOldBeliefAndPromise;
            }

            if (defName.IndexOf("NothingThere", StringComparison.Ordinal) >= 0)
            {
                return EnableNothingThere;
            }

            if (def is PawnKindDef_AnomalyEntity_SevenSin
                || defName == "GluttonyEntity"
                || defName == "LustEntity"
                || defName == "MelancholyEntity"
                || defName == "PrideEntity"
                || defName == "SlothEntity"
                || defName == "WrathEntity")
            {
                return EnableSevenSinEntities;
            }

            return true;
        }

        /// <summary>将全部合集设置恢复为默认值。</summary>
        public void ResetToDefaults()
        {
            EnableOneSin = true;
            EnableMeatLantern = true;
            EnableLaetitia = true;
            EnableBurrowingHeaven = true;
            EnablePortraitOfAnotherWorld = true;
            EnableSevenSinEntities = true;
            EnableFleshIdol = true;
            EnableWeCanChangeAnything = true;
            EnableOldBeliefAndPromise = true;
            EnableNothingThere = true;

            If_ShowVampireText = true;
            If_ShowVampireHealPartText = false;
            If_ShowVampireHealVFX = true;

            BurrowingHeavenAllowUnwatchedTeleport = true;
            BurrowingHeavenSightGraceMultiplier = 1.0f;

            PortraitAllowNonColonistSubstitute = false;
            PortraitDamageTransferMultiplier = 1.15f;

            WeCanChangeAnythingAllowCancel = false;
            OldBeliefCanDestroyEgoWeapon = false;

            NothingThereAllowDisguiseForm = true;
            NothingThereCocoonHatchSeconds = 30;

            settingsVersion = CurrentSettingsVersion;
        }
    }
}
