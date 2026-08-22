using System.Collections.Generic;
using Game.Foundation;

namespace Game.Contracts.Progression
{
    /// <summary>
    /// 关卡最佳成绩的只读展示模型。
    /// </summary>
    /// <remarks>
    /// 时间以固定 Tick 计数保存；使用 <see cref="TickRate"/> 将其换算为秒。
    /// 没有最佳成绩的关卡不应创建此对象，而应让调用方使用 null 表示“暂无成绩”。
    /// </remarks>
    public sealed class BestScoreView
    {
        /// <summary>完成关卡所用的 Tick 数。</summary>
        public long ElapsedTicks { get; }

        /// <summary>记录该成绩时使用的模拟 Tick 频率，例如 60 表示 60 Tick/秒。</summary>
        public int TickRate { get; }

        /// <summary>完成关卡时使用的能力框容量。</summary>
        public int CapacityUsed { get; }

        /// <summary>
        /// 创建最佳成绩展示模型。
        /// </summary>
        /// <param name="elapsedTicks">完成关卡所用的 Tick 数。</param>
        /// <param name="tickRate">记录成绩时使用的 Tick 频率。</param>
        /// <param name="capacityUsed">完成关卡时使用的能力框容量。</param>
        public BestScoreView(long elapsedTicks, int tickRate, int capacityUsed)
        {
            ElapsedTicks = elapsedTicks;
            TickRate = tickRate;
            CapacityUsed = capacityUsed;
        }
    }

    /// <summary>
    /// 单个关卡的进度只读展示模型。
    /// </summary>
    public sealed class LevelProgressView
    {
        /// <summary>关卡的稳定标识。</summary>
        public LevelId LevelId { get; }

        /// <summary>玩家是否已经完成该关卡。</summary>
        public bool IsCompleted { get; }

        /// <summary>
        /// 当前规则版本下的最佳成绩；没有成绩时为 null。
        /// </summary>
        public BestScoreView BestScore { get; }

        /// <summary>
        /// 创建关卡进度展示模型。
        /// </summary>
        /// <param name="levelId">关卡的稳定标识。</param>
        /// <param name="isCompleted">玩家是否已经完成该关卡。</param>
        /// <param name="bestScore">当前规则版本下的最佳成绩；可为 null。</param>
        public LevelProgressView(LevelId levelId, bool isCompleted,
            BestScoreView bestScore = null)
        {
            LevelId = levelId;
            IsCompleted = isCompleted;
            BestScore = bestScore;
        }
    }

    /// <summary>
    /// 玩家当前进度的只读快照。
    /// </summary>
    /// <remarks>
    /// 快照用于让地图、档案和剧情界面在同一时刻读取一致的进度视图。
    /// 集合应由创建者以只读形式提供，调用方不应修改其内容。
    /// </remarks>
    public sealed class ProgressSnapshot
    {
        /// <summary>已经完成的关卡集合。</summary>
        public IReadOnlyCollection<LevelId> CompletedLevels { get; }

        /// <summary>已经完成的剧情集合。</summary>
        public IReadOnlyCollection<StoryId> CompletedStories { get; }

        /// <summary>
        /// 创建进度快照。
        /// </summary>
        /// <param name="completedLevels">已经完成的关卡集合。</param>
        /// <param name="completedStories">已经完成的剧情集合。</param>
        public ProgressSnapshot(IReadOnlyCollection<LevelId> completedLevels,
            IReadOnlyCollection<StoryId> completedStories)
        {
            CompletedLevels = completedLevels == null
                ? new List<LevelId>().AsReadOnly()
                : new List<LevelId>(completedLevels).AsReadOnly();
            CompletedStories = completedStories == null
                ? new List<StoryId>().AsReadOnly()
                : new List<StoryId>(completedStories).AsReadOnly();
        }

        /// <summary>
        /// 返回不包含任何完成事实的空进度快照。
        /// </summary>
        public static ProgressSnapshot Empty => new ProgressSnapshot(
            new List<LevelId>().AsReadOnly(), new List<StoryId>().AsReadOnly());
    }

    /// <summary>
    /// 提供玩家进度的只读查询接口。
    /// </summary>
    /// <remarks>
    /// 查询接口不执行存档写入，也不暴露可修改的 Profile DTO。
    /// 地图、档案、角色和流程服务应通过该接口读取进度状态。
    /// </remarks>
    public interface IProgressQuery
    {
        /// <summary>
        /// 获取当前玩家进度的快照。
        /// </summary>
        /// <returns>当前进度的只读快照；没有进度时返回 <see cref="ProgressSnapshot.Empty"/>。</returns>
        ProgressSnapshot GetSnapshot();

        /// <summary>
        /// 判断指定关卡当前是否已解锁。
        /// </summary>
        /// <param name="levelId">要查询的关卡稳定标识。</param>
        /// <returns>已解锁返回 true，否则返回 false。</returns>
        bool IsLevelUnlocked(LevelId levelId);

        /// <summary>
        /// 判断指定剧情是否允许玩家重播。
        /// </summary>
        /// <param name="storyId">要查询的剧情稳定标识。</param>
        /// <returns>允许重播返回 true，否则返回 false。</returns>
        bool IsStoryReplayUnlocked(StoryId storyId);

        /// <summary>
        /// 获取指定关卡当前规则版本下的最佳成绩。
        /// </summary>
        /// <param name="levelId">要查询的关卡稳定标识。</param>
        /// <returns>最佳成绩；没有成绩时返回 null。</returns>
        BestScoreView GetBestScore(LevelId levelId);
    }
}
