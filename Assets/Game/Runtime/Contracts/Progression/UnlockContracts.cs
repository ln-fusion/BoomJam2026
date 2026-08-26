using Game.Contracts.Content;
using Game.Foundation;

namespace Game.Contracts.Progression
{
    /// <summary>评估关卡 All/Any 前置条件的统一入口。</summary>
    public interface IUnlockEvaluator
    {
        /// <summary>判断关卡是否满足进入条件。</summary>
        /// <param name="level">待评估关卡摘要。</param>
        /// <param name="progress">玩家进度快照。</param>
        /// <returns>满足条件返回 true。</returns>
        bool IsUnlocked(LevelSummary level, ProgressSnapshot progress);
    }
}
