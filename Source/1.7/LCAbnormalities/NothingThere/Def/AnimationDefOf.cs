using RimWorld;
using Verse;

namespace NothingThere.Def
{
    /// <summary>一无所有（O-06-20）多阶段动画 Def 引用表。</summary>
    [DefOf]
    public static class AnimationDefOf
    {
        public static AnimationDef NothingThere_DogIdle;
        public static AnimationDef NothingThere_DogWork;
        public static AnimationDef NothingThere_DogWalk;
        public static AnimationDef NothingThere_DogAttack;
        public static AnimationDef NothingThere_DogAttack2;
        public static AnimationDef NothingThere_DisguiseIdle;
        public static AnimationDef NothingThere_DisguiseWalk;
        public static AnimationDef NothingThere_CocoonIdle;
        public static AnimationDef NothingThere_TrueIdle;
        public static AnimationDef NothingThere_TrueWalk;
        public static AnimationDef NothingThere_TrueMelee;
        public static AnimationDef NothingThere_TrueThrow;
        public static AnimationDef NothingThere_TrueGoodbye;

        static AnimationDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(AnimationDefOf));
        }
    }
}
