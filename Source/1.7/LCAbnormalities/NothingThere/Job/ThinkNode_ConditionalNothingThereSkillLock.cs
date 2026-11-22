using NothingThere.Comp;
using Verse;
using Verse.AI;

namespace NothingThere.Job
{
    /// <summary>
    /// 当一无所有正在执行骨矛投掷或 Goodbye 处决等原地技能动画时，锁止 ThinkTree 切入短暂停顿，避免与寻路器反复打架。
    /// </summary>
    public class ThinkNode_ConditionalNothingThereSkillLock : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn)
        {
            CompNothingThere comp = pawn?.TryGetComp<CompNothingThere>();
            return comp != null
                && comp.Escaped
                && (comp.IsPerformingGoodbye || comp.IsThrowingBoneSpike);
        }
    }
}
