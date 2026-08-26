using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Progression;
using Game.Foundation;

namespace Game.Contracts.Meta
{
    /// <summary>当前关卡节点在地图选择界面中的状态。</summary>
    public enum LevelNodeState
    {
        /// <summary>前置条件未满足，不能进入。</summary>
        Locked,
        /// <summary>已满足条件但尚未完成。</summary>
        Unlocked,
        /// <summary>当前建议继续游玩的节点。</summary>
        Current,
        /// <summary>已经完成的节点。</summary>
        Completed
    }

    /// <summary>地图页签的只读显示模型。</summary>
    public sealed class MapTabViewModel
    {
        /// <summary>地图稳定标识。</summary>
        public MapId MapId { get; }
        /// <summary>地图本地化名称键。</summary>
        public string DisplayNameKey { get; }
        /// <summary>地图内关卡节点。</summary>
        public IReadOnlyList<LevelNodeViewModel> Levels { get; }

        /// <summary>创建地图页签模型。</summary>
        /// <param name="mapId">地图稳定标识。</param>
        /// <param name="displayNameKey">地图名称键。</param>
        /// <param name="levels">关卡节点列表。</param>
        public MapTabViewModel(MapId mapId, string displayNameKey,
            IReadOnlyList<LevelNodeViewModel> levels)
        {
            MapId = mapId ?? throw new ArgumentNullException(nameof(mapId));
            DisplayNameKey = displayNameKey ?? string.Empty;
            Levels = levels ?? new List<LevelNodeViewModel>().AsReadOnly();
        }
    }

    /// <summary>地图节点的只读显示模型。</summary>
    public sealed class LevelNodeViewModel
    {
        /// <summary>关卡稳定标识。</summary>
        public LevelId LevelId { get; }
        /// <summary>关卡名称本地化键。</summary>
        public string DisplayNameKey { get; }
        /// <summary>节点状态。</summary>
        public LevelNodeState State { get; }
        /// <summary>是否允许点击进入。</summary>
        public bool IsInteractable => State != LevelNodeState.Locked;

        /// <summary>创建关卡节点模型。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <param name="displayNameKey">关卡名称键。</param>
        /// <param name="state">节点状态。</param>
        public LevelNodeViewModel(LevelId levelId, string displayNameKey, LevelNodeState state)
        {
            LevelId = levelId ?? throw new ArgumentNullException(nameof(levelId));
            DisplayNameKey = displayNameKey ?? string.Empty;
            State = state;
        }
    }

    /// <summary>关卡详情卡片的只读显示模型。</summary>
    public sealed class LevelCardViewModel
    {
        /// <summary>关卡节点模型。</summary>
        public LevelNodeViewModel Node { get; }
        /// <summary>玩家最佳成绩；没有成绩时为 null。</summary>
        public BestScoreView BestScore { get; }

        /// <summary>创建关卡详情模型。</summary>
        /// <param name="node">关卡节点。</param>
        /// <param name="bestScore">最佳成绩，可为 null。</param>
        public LevelCardViewModel(LevelNodeViewModel node, BestScoreView bestScore)
        {
            Node = node ?? throw new ArgumentNullException(nameof(node));
            BestScore = bestScore;
        }
    }

    /// <summary>合并官方内容与玩家进度的地图查询接口。</summary>
    public interface IMetaMapQuery
    {
        /// <summary>获取按内容排序的地图页签。</summary>
        /// <returns>只读地图模型列表。</returns>
        IReadOnlyList<MapTabViewModel> GetMaps();
        /// <summary>获取指定地图的关卡节点。</summary>
        /// <param name="mapId">地图稳定标识。</param>
        /// <returns>找不到地图时返回空列表。</returns>
        IReadOnlyList<LevelNodeViewModel> GetLevels(MapId mapId);
        /// <summary>获取指定关卡的详情卡片。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <returns>找不到关卡时返回 null。</returns>
        LevelCardViewModel GetLevelCard(LevelId levelId);
    }
}
