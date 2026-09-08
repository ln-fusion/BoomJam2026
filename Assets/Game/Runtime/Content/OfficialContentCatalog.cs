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
        /// <returns>通过 ID、前置依赖及剧情引用校验的内容服务。</returns>
        /// <exception cref="ArgumentException">目录存在重复 ID、未知引用、非法剧情或前置依赖环。</exception>
        public OfficialContentService CreateValidatedService()
        {
            var runtimeMaps = new List<MapDefinition>();
            foreach (var map in maps)
            {
                if (map == null) throw new ArgumentException("地图配置不能为空。");
                runtimeMaps.Add(new MapDefinition { Header = map.Header, MapId = map.MapId,
                    DisplayNameKey = map.DisplayNameKey, SortOrder = map.SortOrder });
            }
            var provider = new OfficialContentProvider(runtimeMaps, levels, stories);
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
            foreach (var story in stories)
                if (!StoryDefinitionValidator.TryValidate(story, out error))
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
            try { CreateValidatedService(); Debug.Log("内容目录校验通过。", this); }
            catch (ArgumentException exception) { Debug.LogError(exception.Message, this); }
        }
    }
}
