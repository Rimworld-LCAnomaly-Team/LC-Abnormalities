using LCAbnormalities.Settings;
using LCAnomalyCore.Comp;
using LCAnomalyCore.Damage;
using LCAnomalyCore.GameComponent;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace LCAbnormalities.OldBeliefAndPromise
{
    /// <summary>T-09-97 古老的信念与承诺组件属性。</summary>
    public class CompProperties_OldBeliefAndPromise : CompProperties_AbnormalityTool
    {
        public float tier1Multiplier = 1.25f;
        public float tier2Multiplier = 1.40f;
        public float tier1BetrayalChance = 0.20f;
        public float tier2BetrayalChance = 0.45f;
        public float betrayalWhiteDamage = 22f;
        public float protectedEgoBacklashWhite = 32f;
        public float protectedEgoBacklashBlack = 18f;

        public CompProperties_OldBeliefAndPromise()
        {
            compClass = typeof(CompOldBeliefAndPromise);
        }
    }

    /// <summary>
    /// T-09-97 古老的信念与承诺核心逻辑：
    /// 单次交互可将使用者当前手持主武器永久强化（+25% -> +40% 伤害倍率，支持 E.G.O 与常规武器），
    /// 并存入 <c>GameComponent_LC</c> 在信息面板展示与随存档持久保存；但背叛概率会随贪婪阶梯攀升。
    /// </summary>
    public class CompOldBeliefAndPromise : CompAbnormalityTool
    {
        private int totalBlessings;
        private int totalBetrayals;
        private bool lastOutcomeBetrayal;

        public CompProperties_OldBeliefAndPromise PropsPromise => (CompProperties_OldBeliefAndPromise)props;

        public int TotalBlessings => totalBlessings;

        public int TotalBetrayals => totalBetrayals;

        public bool LastOutcomeBetrayal => lastOutcomeBetrayal;

        public override AcceptanceReport CanUseTool(Pawn pawn)
        {
            AcceptanceReport baseReport = base.CanUseTool(pawn);
            if (!baseReport.Accepted)
            {
                return baseReport;
            }

            ThingWithComps weapon = pawn?.equipment?.Primary;
            if (weapon == null)
            {
                return "LC_OldBelief_NoWeapon".Translate();
            }

            float curMult = GameComponent_LC.Instance?.GetWeaponBlessingMultiplier(weapon) ?? 1f;
            if (curMult >= PropsPromise.tier2Multiplier - 0.005f)
            {
                return "LC_OldBelief_AlreadyMaxBlessed".Translate(weapon.LabelCap);
            }

            return true;
        }

        public override void OnToolUsed(Pawn user)
        {
            base.OnToolUsed(user);

            ThingWithComps weapon = user?.equipment?.Primary;
            GameComponent_LC gc = GameComponent_LC.Instance;
            if (weapon == null || gc == null)
            {
                return;
            }

            float curMult = gc.GetWeaponBlessingMultiplier(weapon);
            bool isUpgrade = curMult > 1.0001f;
            float betrayalChance = isUpgrade ? PropsPromise.tier2BetrayalChance : PropsPromise.tier1BetrayalChance;

            if (Rand.Chance(betrayalChance))
            {
                lastOutcomeBetrayal = true;
                totalBetrayals++;
                gc.RemoveWeaponBlessing(weapon);

                if (IsOperational && ActiveMap != null && ActivePosition.IsValid)
                {
                    PromiseSoundDefOf.LC_Promise_Fail?.PlayOneShot(new TargetInfo(ActivePosition, ActiveMap));
                }

                bool isAuthoredEgo = FourColorOverrides.TryGetWeaponProfile(weapon.def, out FourColorOverrides.ResolvedWeaponProfile wp)
                    && wp.isAuthoredEgo;
                bool canDestroyEgo = LCAbnormalitiesMod.Settings?.OldBeliefCanDestroyEgoWeapon ?? false;

                if (!isAuthoredEgo || canDestroyEgo)
                {
                    string weaponLabel = weapon.LabelCap;
                    user.equipment.DestroyEquipment(weapon);

                    if (user != null && !user.Dead && !user.Destroyed)
                    {
                        user.TakeDamage(new DamageInfo(
                            LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage,
                            PropsPromise.betrayalWhiteDamage,
                            armorPenetration: 1.5f,
                            instigator: parent));
                    }

                    Messages.Message(
                        "LC_OldBelief_BetrayalDestroyed".Translate(user.LabelShortCap, weaponLabel),
                        new LookTargets(user),
                        MessageTypeDefOf.NegativeEvent);
                }
                else
                {
                    if (user != null && !user.Dead && !user.Destroyed)
                    {
                        user.TakeDamage(new DamageInfo(
                            LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage,
                            PropsPromise.protectedEgoBacklashWhite,
                            armorPenetration: 1.5f,
                            instigator: parent));

                        if (!user.Dead && !user.Destroyed)
                        {
                            user.TakeDamage(new DamageInfo(
                                LCAnomalyCore.Defs.DamageDefOf.LC_BlackDamage,
                                PropsPromise.protectedEgoBacklashBlack,
                                armorPenetration: 1.5f,
                                instigator: parent));
                        }
                    }

                    Messages.Message(
                        "LC_OldBelief_BetrayalBacklash".Translate(user.LabelShortCap, weapon.LabelCap),
                        new LookTargets(user),
                        MessageTypeDefOf.NegativeEvent);
                }

                return;
            }

            lastOutcomeBetrayal = false;
            totalBlessings++;

            float targetMult = isUpgrade ? PropsPromise.tier2Multiplier : PropsPromise.tier1Multiplier;
            gc.SetWeaponBlessing(weapon, targetMult);

            if (IsOperational && ActiveMap != null && ActivePosition.IsValid)
            {
                PromiseSoundDefOf.LC_Promise_Success?.PlayOneShot(new TargetInfo(ActivePosition, ActiveMap));
            }

            string pct = $"+{(targetMult - 1f) * 100f:0.#}%";
            if (user != null && user.Spawned && user.Map != null)
            {
                MoteMaker.ThrowText(user.DrawPos, user.Map, pct, new Color(1.0f, 0.85f, 0.25f), 2.8f);
            }

            string msgKey = isUpgrade ? "LC_OldBelief_BlessUpgraded" : "LC_OldBelief_BlessSuccess";
            Messages.Message(
                msgKey.Translate(weapon.LabelCap, pct),
                new LookTargets(user),
                MessageTypeDefOf.PositiveEvent);
        }

        protected override Color GetHeldOverrideColor()
        {
            if (lastOutcomeBetrayal)
            {
                return new Color(0.65f, 0.65f, 0.92f);
            }

            if (totalBlessings > 0)
            {
                return new Color(1.0f, 0.94f, 0.72f);
            }

            return Color.white;
        }

        public override string GetToolStatusInspectString()
        {
            return "LC_OldBelief_InspectStats".Translate(totalBlessings, totalBetrayals);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref totalBlessings, "oldBeliefTotalBlessings", 0);
            Scribe_Values.Look(ref totalBetrayals, "oldBeliefTotalBetrayals", 0);
            Scribe_Values.Look(ref lastOutcomeBetrayal, "oldBeliefLastBetrayal", false);
        }
    }

    [DefOf]
    public static class PromiseSoundDefOf
    {
        public static SoundDef LC_Promise_Doing;
        public static SoundDef LC_Promise_Success;
        public static SoundDef LC_Promise_Fail;

        static PromiseSoundDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(PromiseSoundDefOf));
        }
    }
}
