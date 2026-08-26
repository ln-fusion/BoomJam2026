using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Meta;
using Game.Contracts.Progression;
using Game.Foundation;

namespace Game.Meta
{
    /// <summary>将官方地图内容和玩家进度合并为地图选择模型。</summary>
    public sealed class MetaMapQuery : IMetaMapQuery
    {
        private readonly IContentService _content;
        private readonly IProgressQuery _progress;
        private readonly IUnlockEvaluator _unlockEvaluator;

        /// <summary>创建地图查询。</summary>
        /// <param name="content">官方内容查询服务。</param>
        /// <param name="progress">玩家进度查询服务。</param>
        public MetaMapQuery(IContentService content, IProgressQuery progress,
            IUnlockEvaluator unlockEvaluator = null)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _unlockEvaluator = unlockEvaluator ?? new Game.Progression.UnlockEvaluator();
        }

        /// <inheritdoc/>
        public IReadOnlyList<MapTabViewModel> GetMaps()
        {
            var maps = new List<MapTabViewModel>();
            foreach (MapDefinition map in _content.GetMaps())
            {
                if (map == null || string.IsNullOrWhiteSpace(map.MapId))
                    continue;
                maps.Add(new MapTabViewModel(new MapId(map.MapId), map.DisplayNameKey,
                    GetLevels(new MapId(map.MapId))));
            }
            return maps.AsReadOnly();
        }

        /// <inheritdoc/>
        public IReadOnlyList<LevelNodeViewModel> GetLevels(MapId mapId)
        {
            if (mapId == null)
                return new List<LevelNodeViewModel>().AsReadOnly();

            var summaries = _content.GetLevelsForMap(mapId);
            var result = new List<LevelNodeViewModel>();
            bool currentAssigned = false;
            foreach (LevelSummary summary in summaries)
            {
                if (summary == null || string.IsNullOrWhiteSpace(summary.LevelId))
                    continue;
                var id = new LevelId(summary.LevelId);
                bool completed = IsCompleted(id);
                LevelNodeState state;
                if (completed)
                    state = LevelNodeState.Completed;
                else if (!_unlockEvaluator.IsUnlocked(summary, _progress.GetSnapshot()))
                    state = LevelNodeState.Locked;
                else if (!currentAssigned)
                {
                    state = LevelNodeState.Current;
                    currentAssigned = true;
                }
                else
                    state = LevelNodeState.Unlocked;
                result.Add(new LevelNodeViewModel(id, summary.DisplayNameKey, state));
            }
            return result.AsReadOnly();
        }

        /// <inheritdoc/>
        public LevelCardViewModel GetLevelCard(LevelId levelId)
        {
            if (levelId == null)
                return null;
            LevelDefinition definition = _content.GetLevel(levelId);
            if (definition == null)
                return null;
            LevelNodeViewModel node = null;
            foreach (LevelNodeViewModel candidate in GetLevels(new MapId(definition.MapId)))
                if (candidate.LevelId == levelId) { node = candidate; break; }
            return node == null ? null : new LevelCardViewModel(node, _progress.GetBestScore(levelId));
        }

        private bool IsCompleted(LevelId levelId)
        {
            foreach (LevelId completed in _progress.GetSnapshot().CompletedLevels)
                if (completed == levelId)
                    return true;
            return false;
        }

    }
}
