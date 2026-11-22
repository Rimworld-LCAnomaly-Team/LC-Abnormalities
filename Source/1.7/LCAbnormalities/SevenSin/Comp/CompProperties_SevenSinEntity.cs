using LCAnomalyCore.Comp;

namespace LC_SevenSin.Comp
{
    public class CompProperties_SevenSinEntity : CompProperties_Abnormality
    {
        public float instinctBonus = 0.70f;
        public float insightBonus = 0.70f;
        public float attachmentBonus = 0.70f;
        public float repressionBonus = 0.70f;
        public float passiveHealPerRareTick = 0f;

        public CompProperties_SevenSinEntity()
        {
            compClass = typeof(CompSevenSinEntity);
        }
    }
}
