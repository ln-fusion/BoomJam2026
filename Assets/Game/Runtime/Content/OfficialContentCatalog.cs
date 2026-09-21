using System.Collections.Generic;
using System;
using Game.Foundation;
using UnityEngine;
using Game.Contracts.Content;

namespace Game.Content
{
    /// <summary>
    /// 官方内容目录 ScriptableObject，集中保存关卡、剧情、角色、档案和资源 Registry 引用。
    /// </summary>
    [CreateAssetMenu(fileName = "OfficialContentCatalog", menuName = "Game/Content/Official Catalog")]
    public sealed class OfficialContentCatalog : ScriptableObject
    {
        [SerializeField] private List<LevelDefinition> levels = new List<LevelDefinition>();
        [SerializeField] private List<MapDefinition> maps = new List<MapDefinition>();
        [SerializeField] private List<StoryDefinition> stories = new List<StoryDefinition>();
        [SerializeField] private List<CharacterDefinition> characters = new List<CharacterDefinition>();
        [SerializeField] private List<ArchiveEntryDefinition> archiveEntries =
            new List<ArchiveEntryDefinition>();
        [SerializeField] private ContentAssetRegistry assetRegistry;

        /// <summary>目录中的官方关卡定义。</summary>
        public IReadOnlyList<LevelDefinition> Levels => levels;
        /// <summary>Official map definitions in authored order.</summary>
        public IReadOnlyList<MapDefinition> Maps => maps;
        /// <summary>目录中的官方剧情定义。</summary>
        public IReadOnlyList<StoryDefinition> Stories => stories;
        /// <summary>目录中的官方角色定义。</summary>
        public IReadOnlyList<CharacterDefinition> Characters => characters;
        /// <summary>目录中的官方档案条目定义。</summary>
        public IReadOnlyList<ArchiveEntryDefinition> ArchiveEntries => archiveEntries;
        /// <summary>目录关联的官方资源 Registry。</summary>
        public ContentAssetRegistry AssetRegistry => assetRegistry;

        /// <summary>从可编辑目录创建并校验运行时副本；地图内关卡摘要由关卡的所属地图统一生成。</summary>
        /// <param name="generatedStories">编辑器导出的 Runtime 剧情；提供时作为剧情正文唯一来源，目录中的旧正文不参与运行时。</param>
        /// <returns>通过 ID、前置依赖及剧情引用校验的内容服务。</returns>
        /// <exception cref="ArgumentException">目录存在重复 ID、未知引用、非法剧情或前置依赖环。</exception>
        public OfficialContentService CreateValidatedService(
            IReadOnlyDictionary<string, StoryDefinition> generatedStories = null)
        {
            var runtimeMaps = new List<MapDefinition>();
            foreach (var map in maps)
            {
                if (map == null) throw new ArgumentException("地图配置不能为空。");
                runtimeMaps.Add(new MapDefinition { Header = map.Header, MapId = map.MapId,
                    DisplayNameKey = map.DisplayNameKey, SortOrder = map.SortOrder });
            }
            var runtimeStories = new Dictionary<string, StoryDefinition>(StringComparer.Ordinal);
            if (generatedStories != null)
                foreach (var pair in generatedStories)
                {
                    if (pair.Value == null || pair.Key != pair.Value.StoryId)
                        throw new ArgumentException("导出剧情 ID 与索引不一致。");
                    if (!runtimeStories.TryAdd(pair.Key, pair.Value))
                        throw new ArgumentException("Runtime 剧情 ID 重复：" + pair.Key);
                }
            else
                foreach (var story in stories)
                {
                    if (story == null || string.IsNullOrWhiteSpace(story.StoryId) ||
                        !runtimeStories.TryAdd(story.StoryId, story))
                        throw new ArgumentException("目录剧情 ID 缺失或重复。");
                }
            var provider = new OfficialContentProvider(runtimeMaps, levels, runtimeStories.Values);
            foreach (var level in levels)
            {
                if (!provider.TryGetMap(new MapId(level.MapId), out var map))
                    throw new ArgumentException("关卡所属地图不存在：" + level.LevelId);
                map.Levels.Add(level.Summary);
                ValidateStoryReference(provider, level.PreludeStoryId);
                ValidateStoryReference(provider, level.PostludeStoryId);
                if (level.UnlockRequirement != null &&
                    (level.UnlockRequirement.RequiredLevelIds == null ||
                     !Enum.IsDefined(typeof(UnlockRequirementMode), level.UnlockRequirement.Mode)))
                    throw new ArgumentException("关卡前置条件无效：" + level.LevelId);
            }
            if (!MapContentValidator.TryValidate(provider, out var error))
                throw new ArgumentException(error);
            // 只有 Runtime JSON 才执行全量正文校验；旧 Catalog 仅保留调试兼容路径，
            // 不得被 Bootstrap 作为正式剧情来源使用。
            if (generatedStories != null)
                foreach (var story in runtimeStories.Values)
                    if (!StoryDefinitionValidator.TryValidate(story, characters, out error))
                        throw new ArgumentException("剧情校验失败：" + story.StoryId + "：" + error);
            return new OfficialContentService(provider, characters, archiveEntries);
        }

        /// <summary>检查可选关前或关后剧情 ID 是否能在同一目录解析。</summary>
        /// <param name="provider">本次目录内容提供者。</param>
        /// <param name="storyId">剧情 ID；空值表示不播放。</param>
        private static void ValidateStoryReference(OfficialContentProvider provider, string storyId)
        {
            if (!string.IsNullOrWhiteSpace(storyId) &&
                !provider.TryGetStory(new StoryId(storyId), out _))
                throw new ArgumentException("剧情引用不存在：" + storyId);
        }

        /// <summary>通过资源 Inspector 的上下文菜单校验目录并将结果显示在 Console。</summary>
        [ContextMenu("Validate Catalog")]
        private void ValidateCatalog()
        {
            try { CreateValidatedService(GeneratedStoryLoader.LoadAll()); Debug.Log("内容目录校验通过。", this); }
            catch (ArgumentException exception) { Debug.LogError(exception.Message, this); }
        }
    }
}
