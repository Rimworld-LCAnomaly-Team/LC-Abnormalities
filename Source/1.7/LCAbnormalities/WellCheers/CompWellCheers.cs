using LCAbnormalities.Shared;
using RimWorld;
using Verse;

namespace LCAbnormalities.WellCheers
{
    [DefOf]
    public static class WellCheersDefOf
    {
        public static HediffDef LC_WellCheers_Red;
        public static HediffDef LC_WellCheers_Blue;
        public static HediffDef LC_WellCheers_Purple;
        public static HediffDef LC_WellCheers_DeepSleep;
        public static SoundDef LC_WellCheers_Drink;
        public static SoundDef LC_WellCheers_Sleep;

        static WellCheersDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(WellCheersDefOf));
        }
    }

    public class CompProperties_WellCheers : CompProperties_LCAbnormality
    {
        public CompProperties_WellCheers()
        {
            compClass = typeof(CompWellCheers);
        }
    }

    /// <summary>F-05-52 韦尔奇牌汽水机：优/良随机供应红(外伤)/蓝(精神)/紫(双效)汽水；差则直接使饮用者沉睡、失温并遭受精神冲击。</summary>
    public class CompWellCheers : CompLCAbnormality
    {
        protected override void StudyEvent_Good(Pawn studier)
        {
            base.StudyEvent_Good(studier);
            ServeRandomCan(studier, isGood: true);
        }

        protected override void StudyEvent_Normal(Pawn studier)
        {
            base.StudyEvent_Normal(studier);
            ServeRandomCan(studier, isGood: false);
        }

        protected override void StudyEvent_Bad(Pawn studier)
        {
            base.StudyEvent_Bad(studier);
            if (studier == null || studier.Dead)
            {
                return;
            }

            LCAbnormalitiesHelper.PlaySoundAt(WellCheersDefOf.LC_WellCheers_Sleep, parent);
            LCAbnormalitiesHelper.ApplyOrRefreshHediff(studier, WellCheersDefOf.LC_WellCheers_DeepSleep, 1f);
            LCAbnormalitiesHelper.ApplyOrRefreshHediff(studier, RimWorld.HediffDefOf.Hypothermia, 0.25f);
            studier.TakeDamage(new DamageInfo(LCAnomalyCore.Defs.DamageDefOf.LC_WhiteDamage, 15f, instigator: parent));

            Messages.Message(
                "LC_WellCheers_BadDrinkMsg".Translate(studier.LabelShortCap),
                studier,
                MessageTypeDefOf.NegativeEvent,
                historical: false);
        }

        private void ServeRandomCan(Pawn studier, bool isGood)
        {
            if (studier == null || studier.Dead)
            {
                return;
            }

            LCAbnormalitiesHelper.PlaySoundAt(WellCheersDefOf.LC_WellCheers_Drink, parent);
            int roll = Rand.Range(0, 3);
            float scale = isGood ? 1f : 0.7f;

            switch (roll)
            {
                case 0:
                    LCAbnormalitiesHelper.HealInjuries(studier, 18f * scale);
                    LCAbnormalitiesHelper.ApplyOrRefreshHediff(studier, WellCheersDefOf.LC_WellCheers_Red, 1f);
                    Messages.Message("LC_WellCheers_RedCanMsg".Translate(studier.LabelShortCap), studier, MessageTypeDefOf.PositiveEvent, historical: false);
                    break;
                case 1:
                    LCAbnormalitiesHelper.RestoreMentalHealth(studier, 0.45f * scale, 0.10f * scale);
                    LCAbnormalitiesHelper.ApplyOrRefreshHediff(studier, WellCheersDefOf.LC_WellCheers_Blue, 1f);
                    Messages.Message("LC_WellCheers_BlueCanMsg".Translate(studier.LabelShortCap), studier, MessageTypeDefOf.PositiveEvent, historical: false);
                    break;
                default:
                    LCAbnormalitiesHelper.HealInjuries(studier, 10f * scale);
                    LCAbnormalitiesHelper.RestoreMentalHealth(studier, 0.25f * scale, 0.06f * scale);
                    LCAbnormalitiesHelper.ApplyOrRefreshHediff(studier, WellCheersDefOf.LC_WellCheers_Purple, 1f);
                    Messages.Message("LC_WellCheers_PurpleCanMsg".Translate(studier.LabelShortCap), studier, MessageTypeDefOf.PositiveEvent, historical: false);
                    break;
            }
        }
    }

    /// <summary>苏打步枪 E.G.O 伤害处理器：命中时有 18% 概率为射手恢复少量伤口与精神损伤。</summary>
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
            }

            return result;
        }
    }
}
