using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Foundation;

namespace Game.Content
{
    /// <summary>
    /// 官方内容查询服务，组合关卡/剧情提供者与角色/档案索引。
    /// </summary>
    public sealed class OfficialContentService : IContentService
    {
        private readonly IContentProvider _provider;
        private readonly Dictionary<string, CharacterDefinition> _characters = new Dictionary<
            string,
            CharacterDefinition
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, ArchiveEntryDefinition> _archiveEntries = new Dictionary<
            string,
            ArchiveEntryDefinition
        >(StringComparer.Ordinal);
        private readonly IReadOnlyCollection<LevelDefinition> _knownLevels;

        /// <summary>从官方内容目录创建内容查询服务。</summary>
        /// <param name="catalog">官方内容目录。</param>
        public OfficialContentService(OfficialContentCatalog catalog)
            : this(
                new OfficialContentProvider(catalog),
                catalog == null ? null : catalog.Characters,
                catalog == null ? null : catalog.ArchiveEntries
            ) { }

        /// <summary>从内容提供者及可选角色、档案定义创建查询服务。</summary>
        /// <param name="provider">关卡与剧情内容提供者。</param>
        /// <param name="characters">角色定义集合。</param>
        /// <param name="archiveEntries">档案条目定义集合。</param>
        public OfficialContentService(
            IContentProvider provider,
            IEnumerable<CharacterDefinition> characters = null,
            IEnumerable<ArchiveEntryDefinition> archiveEntries = null
        )
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _knownLevels = (provider as OfficialContentProvider)?.Levels;
            if (characters != null)
                foreach (CharacterDefinition character in characters)
                    if (character != null && !string.IsNullOrWhiteSpace(character.CharacterId))
                        _characters[character.CharacterId] = character;
            if (archiveEntries != null)
                foreach (ArchiveEntryDefinition entry in archiveEntries)
                    if (entry != null && !string.IsNullOrWhiteSpace(entry.EntryId))
                        _archiveEntries[entry.EntryId] = entry;
        }

        /// <summary>按稳定 ID 获取关卡定义。</summary>
        /// <param name="levelId">关卡稳定标识。</param>
        /// <returns>找到的关卡定义；不存在时为 null。</returns>
        public LevelDefinition GetLevel(LevelId levelId)
        {
            return _provider.TryGetLevel(levelId, out LevelDefinition definition) ? definition : null;
        }

        /// <summary>按稳定 ID 获取剧情定义。</summary>
        /// <param name="storyId">剧情稳定标识。</param>
        /// <returns>找到的剧情定义；不存在时为 null。</returns>
        public StoryDefinition GetStory(StoryId storyId)
        {
            return _provider.TryGetStory(storyId, out StoryDefinition definition) ? definition : null;
        }

        /// <summary>按稳定 ID 获取角色定义。</summary>
        /// <param name="characterId">角色稳定标识。</param>
        /// <returns>找到的角色定义；不存在时为 null。</returns>
        public CharacterDefinition GetCharacter(CharacterId characterId)
        {
            if (characterId == null)
                throw new ArgumentNullException(nameof(characterId));

            return _characters.TryGetValue(characterId.Value, out CharacterDefinition definition) ? definition : null;
        }

        /// <summary>按稳定 ID 获取档案条目定义。</summary>
        /// <param name="entryId">档案条目稳定标识。</param>
        /// <returns>找到的档案条目定义；不存在时为 null。</returns>
        public ArchiveEntryDefinition GetArchiveEntry(ArchiveEntryId entryId)
        {
            if (entryId == null)
                throw new ArgumentNullException(nameof(entryId));

            return _archiveEntries.TryGetValue(entryId.Value, out ArchiveEntryDefinition definition)
                ? definition
                : null;
        }

        /// <summary>获取指定地图下按排序值排列的关卡摘要。</summary>
        /// <param name="mapId">地图稳定标识。</param>
        /// <returns>关卡摘要只读列表；没有已知关卡时为空列表。</returns>
        public IReadOnlyList<LevelSummary> GetLevelsForMap(MapId mapId)
        {
            if (mapId == null)
                throw new ArgumentNullException(nameof(mapId));

            var levelSummaries = new List<LevelSummary>();
            if (_knownLevels == null)
                return levelSummaries.AsReadOnly();

            foreach (LevelDefinition definition in _knownLevels)
            {
                if (definition == null || !string.Equals(definition.MapId, mapId.Value, StringComparison.Ordinal))
                    continue;
                levelSummaries.Add(definition.Summary);
            }

            levelSummaries.Sort((left, right) => left.SortOrder.CompareTo(right.SortOrder));
            return levelSummaries.AsReadOnly();
        }

        /// <summary>检查内容头是否符合当前官方内容服务的兼容要求。</summary>
        /// <param name="header">待检查的内容头。</param>
        /// <returns>对应的兼容性结果。</returns>
        public ContentCompatibility CheckCompatibility(ContentHeader header)
        {
            if (header == null)
                return ContentCompatibility.MissingHeader;
            if (header.FormatVersion != 1)
                return ContentCompatibility.UnsupportedFormat;
            if (header.Source != ContentSource.Official)
                return ContentCompatibility.WrongSource;
            if (string.IsNullOrWhiteSpace(header.ContentId))
                return ContentCompatibility.InvalidPayload;
            return ContentCompatibility.Compatible;
        }
    }
}
