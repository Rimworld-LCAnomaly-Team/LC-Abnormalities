using RimWorld;
using Verse;

namespace NothingThere.Def
{
    [DefOf]
    public static class SoundDefOf
    {
        public static SoundDef NothingThere_WorkLoop;
        public static SoundDef NothingThere_Form1Voice;
        public static SoundDef NothingThere_Form1Meet;
        public static SoundDef NothingThere_CocoonHeartbeat;
        public static SoundDef NothingThere_Transform;
        public static SoundDef NothingThere_Hatch;
        public static SoundDef NothingThere_Attack;
        public static SoundDef NothingThere_BoneSpikeCast;
        public static SoundDef NothingThere_BoneSpikeHit;
        public static SoundDef NothingThere_GoodbyeCast;
        public static SoundDef NothingThere_GoodbyeFinish;
        public static SoundDef NothingThere_Death;

        static SoundDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SoundDefOf));
        }
    }
}
