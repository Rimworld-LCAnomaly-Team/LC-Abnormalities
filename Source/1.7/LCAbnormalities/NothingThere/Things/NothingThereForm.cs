using Verse;

namespace NothingThere.Things
{
    /// <summary>一无所有所处的形态阶段。</summary>
    public enum NothingThereForm
    {
        Contained = 0,
        Form1_Disguised = 1,
        Form2_Cocoon = 2,
        Form3_TrueForm = 3
    }

    /// <summary>一无所有一阶段伪装（窃皮目标）的持久化信息。</summary>
    public class DisguiseInfo : IExposable
    {
        public Pawn victimPawn;
        public string victimName;
        public bool hasDisguise;

        public void ExposeData()
        {
            Scribe_References.Look(ref victimPawn, "victimPawn", saveDestroyedThings: true);
            Scribe_Values.Look(ref victimName, "victimName");
            Scribe_Values.Look(ref hasDisguise, "hasDisguise", false);
        }

        public void Clear()
        {
            victimPawn = null;
            victimName = null;
            hasDisguise = false;
        }
    }
}
