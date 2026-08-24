using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Foundation;

namespace Game.Content
{
    /// <summary>
    /// 官方内容提供者，按稳定 ID 索引官方关卡和剧情定义。
    /// </summary>
    public sealed class OfficialContentProvider : IContentProvider
    {
        private readonly Dictionary<string, LevelDefinition> _levels = new Dictionary<string, LevelDefinition>(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, StoryDefinition> _stories = new Dictionary<string, StoryDefinition>(
            StringComparer.Ordinal
        );

        /// <summary>该提供者固定负责官方内容。</summary>
        public ContentSource Source => ContentSource.Official;

        /// <summary>当前索引中的官方关卡集合。</summary>
        public IReadOnlyCollection<LevelDefinition> Levels => _levels.Values;

        /// <summary>从关卡和剧情定义集合创建官方内容提供者。</summary>
        /// <param name="levels">官方关卡定义集合。</param>
        /// <param name="stories">官方剧情定义集合。</param>
        public OfficialContentProvider(IEnumerable<LevelDefinition> levels, IEnumerable<StoryDefinition> stories)
        {
            if (levels != null)
                foreach (LevelDefinition definition in levels)
                    AddLevel(definition);
            if (stories != null)
                foreach (StoryDefinition definition in stories)
                    AddStory(definition);
        }

        /// <summary>从官方内容目录创建内容提供者。</summary>
        /// <param name="catalog">官方内容目录；为空时创建空提供者。</param>
        public OfficialContentProvider(OfficialContentCatalog catalog)
            : this(catalog == null ? null : catalog.Levels, catalog == null ? null : catalog.Stories) { }

        /// <summary>按稳定 ID 尝试获取官方关卡定义。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <param name="definition">找到的关卡定义。</param>
        /// <returns>找到返回 true，否则返回 false。</returns>
        public bool TryGetLevel(LevelId levelId, out LevelDefinition definition)
        {
            if (levelId == null)
                throw new ArgumentNullException(nameof(levelId));

            return _levels.TryGetValue(levelId.Value, out definition);
        }

        /// <summary>按稳定 ID 尝试获取官方剧情定义。</summary>
        /// <param name="storyId">剧情稳定标识。</param>
        /// <param name="definition">找到的剧情定义。</param>
        /// <returns>找到返回 true，否则返回 false。</returns>
        public bool TryGetStory(StoryId storyId, out StoryDefinition definition)
        {
            if (storyId == null)
                throw new ArgumentNullException(nameof(storyId));

            return _stories.TryGetValue(storyId.Value, out definition);
        }

        /// <summary>校验并加入一条官方关卡定义。</summary>
        /// <param name="definition">待加入的关卡定义。</param>
        private void AddLevel(LevelDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.LevelId))
                throw new ArgumentException("Official levels require a stable LevelId.");
            if (!_levels.TryAdd(definition.LevelId, definition))
                throw new ArgumentException("Duplicate official LevelId: " + definition.LevelId);
        }

        /// <summary>校验并加入一条官方剧情定义。</summary>
        /// <param name="definition">待加入的剧情定义。</param>
        private void AddStory(StoryDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.StoryId))
                throw new ArgumentException("Official stories require a stable StoryId.");
            if (!_stories.TryAdd(definition.StoryId, definition))
                throw new ArgumentException("Duplicate official StoryId: " + definition.StoryId);
        }
    }
}
