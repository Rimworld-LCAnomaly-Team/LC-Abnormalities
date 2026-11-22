using NothingThere.Comp;
using NothingThere.Things;
using Verse;
using Verse.AI;

namespace NothingThere.Job
{
    /// <summary>
    /// 根据一无所有当前形态控制 AI 行为分支（例如二阶段肉茧保持静止孵化）。
    /// </summary>
    public class ThinkNode_ConditionalNothingThereForm : ThinkNode_Conditional
    {
        public bool allowForm1 = false;
        public bool allowForm2 = false;
        public bool allowForm3 = false;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            ThinkNode_ConditionalNothingThereForm copy = (ThinkNode_ConditionalNothingThereForm)base.DeepCopy(resolve);
            copy.allowForm1 = allowForm1;
            copy.allowForm2 = allowForm2;
            copy.allowForm3 = allowForm3;
            return copy;
        }

        protected override bool Satisfied(Pawn pawn)
        {
            CompNothingThere comp = pawn?.TryGetComp<CompNothingThere>();
            if (comp == null || !comp.Escaped)
            {
                return false;
            }

            switch (comp.CurrentForm)
            {
                case NothingThereForm.Form1_Disguised:
                    return allowForm1;
                case NothingThereForm.Form2_Cocoon:
                    return allowForm2;
                case NothingThereForm.Form3_TrueForm:
                    return allowForm3;
                default:
                    return false;
            }
        }
    }
}
