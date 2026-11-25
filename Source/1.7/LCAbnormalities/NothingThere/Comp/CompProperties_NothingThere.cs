using LCAnomalyCore.Comp;
using Verse;

namespace NothingThere.Comp
{
    public class CompProperties_NothingThere : CompProperties_Abnormality
    {
        public FloatRange workRedDamageRange = new FloatRange(6f, 9f);
        public int form1DurationTicks = 1800;
        public float form1MorphHealthPct = 0f;
        public float form1SwipeRadius = 2.2f;
        public FloatRange form1SwipeDamageRange = new FloatRange(18f, 21f);
        public int defaultCocoonHatchTicks = 1800;
        public int form3RegenDelayTicks = 600;
        public int form3RegenIntervalTicks = 60;
        public float form3RegenAmount = 8f;
        public int cleaveCooldownTicks = 120;
        public float cleaveRadius = 2.7f;
        public FloatRange cleaveDamageRange = new FloatRange(25f, 35f);
        public int specialCheckIntervalTicks = 150;
        public float specialBaseChance = 0.40f;
        public float specialChanceStep = 0.15f;
        public float specialBoneSpikeShare = 0.70f;
        public int boneSpikeCooldownTicks = 300;
        public int throwWindupTicks = 44;
        public int throwRecoverTicks = 76;
        public float boneSpikeMinRange = 0f;
        public float boneSpikeMaxRange = 16f;
        public int goodbyeCooldownTicks = 420;
        public int goodbyeWindupTicks = 120;
        public int goodbyeSlashTicks = 48;
        public float goodbyeDamage = 150f;
        public float goodbyeRadius = 3.2f;
        public float goodbyeSelfHeal = 0f;

        public CompProperties_NothingThere()
        {
            compClass = typeof(CompNothingThere);
        }
    }
}
