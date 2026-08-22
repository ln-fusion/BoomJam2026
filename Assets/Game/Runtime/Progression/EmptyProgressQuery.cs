using Game.Contracts.Progression;
using Game.Foundation;

namespace Game.Progression
{
    /// <summary>
    /// 空进度查询实现，用于进度系统尚未接入前提供安全的只读默认值。
    /// </summary>
    /// <remarks>
    /// 开发计划 C01/C02 阶段先建立契约边界；该实现保证地图、剧情和档案界面可以在无档案状态下查询进度。
    /// </remarks>
    public sealed class EmptyProgressQuery : IProgressQuery
    {
        /// <summary>获取不包含任何完成事实的进度快照。</summary>
        /// <returns>空进度快照。</returns>
        public ProgressSnapshot GetSnapshot()
        {
            return ProgressSnapshot.Empty;
        }

        /// <summary>判断关卡是否解锁；空实现中所有关卡均未解锁。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <returns>始终返回 false。</returns>
        public bool IsLevelUnlocked(LevelId levelId)
        {
            return false;
        }

        /// <summary>判断剧情是否允许重播；空实现中所有剧情均不可重播。</summary>
        /// <param name="storyId">剧情稳定标识。</param>
        /// <returns>始终返回 false。</returns>
        public bool IsStoryReplayUnlocked(StoryId storyId)
        {
            return false;
        }

        /// <summary>获取关卡最佳成绩；空实现中没有任何成绩。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <returns>始终返回 null。</returns>
        public BestScoreView GetBestScore(LevelId levelId)
        {
            return null;
        }
    }
}
