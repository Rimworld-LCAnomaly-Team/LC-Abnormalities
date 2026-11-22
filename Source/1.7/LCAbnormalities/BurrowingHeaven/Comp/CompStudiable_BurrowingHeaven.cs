using LCAnomalyCore.Comp;

namespace BurrowingHeaven.Comp
{
    /// <summary>
    /// 根据研究解锁进度提供穿刺乐园的工作速度与成功率加成。
    /// </summary>
    public class CompStudiable_BurrowingHeaven : CompAbnormalityStudiable
    {
        /// <inheritdoc />
        public override float GetWorkSpeedOffset()
        {
            if (CompStudyUnlocks == null)
                return 0f;

            if (CompStudyUnlocks.Completed)
                return 0.08f;

            if (CompStudyUnlocks.Progress >= 2)
                return 0.04f;

            return 0f;
        }

        /// <inheritdoc />
        public override float GetWorkSuccessRateOffset()
        {
            if (CompStudyUnlocks == null)
                return 0f;

            if (CompStudyUnlocks.Progress >= 3)
                return 0.08f;

            if (CompStudyUnlocks.Progress >= 1)
                return 0.04f;

            return 0f;
        }
    }
}
