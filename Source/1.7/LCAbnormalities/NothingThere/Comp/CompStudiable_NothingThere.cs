using LCAnomalyCore.Comp;

namespace NothingThere.Comp
{
    /// <summary>
    /// 根据研究解锁进度提供一无所有（O-06-20）的工作速度与成功率加成。
    /// </summary>
    public class CompStudiable_NothingThere : CompAbnormalityStudiable
    {
        public override float GetWorkSpeedOffset()
        {
            if (CompStudyUnlocks == null)
                return 0f;

            if (CompStudyUnlocks.Progress >= 3)
                return 0.06f;

            if (CompStudyUnlocks.Progress >= 1)
                return 0.03f;

            return 0f;
        }

        public override float GetWorkSuccessRateOffset()
        {
            if (CompStudyUnlocks == null)
                return 0f;

            if (CompStudyUnlocks.Completed)
                return 0.06f;

            if (CompStudyUnlocks.Progress >= 2)
                return 0.03f;

            return 0f;
        }
    }
}
