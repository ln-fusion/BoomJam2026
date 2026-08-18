using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Foundation.Ids;

namespace Game.Content
{
    public sealed class OfficialContentService : IContentService
    {
        private readonly IContentProvider _provider;
        private readonly Dictionary<string, CharacterDefinition> _characters =
            new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ArchiveEntryDefinition> _archiveEntries =
            new Dictionary<string, ArchiveEntryDefinition>(StringComparer.Ordinal);
        private readonly IReadOnlyCollection<LevelDefinition> _knownLevels;

        public OfficialContentService(OfficialContentCatalog catalog)
            : this(new OfficialContentProvider(catalog),
                   catalog == null ? null : catalog.Characters,
                   catalog == null ? null : catalog.ArchiveEntries)
        {
        }

        public OfficialContentService(IContentProvider provider,
            IEnumerable<CharacterDefinition> characters = null,
            IEnumerable<ArchiveEntryDefinition> archiveEntries = null)
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

        public LevelDefinition GetLevel(LevelId levelId)
        {
            return _provider.TryGetLevel(levelId, out LevelDefinition definition)
                ? definition
                : null;
        }

        public StoryDefinition GetStory(StoryId storyId)
        {
            return _provider.TryGetStory(storyId, out StoryDefinition definition)
                ? definition
                : null;
        }

        public CharacterDefinition GetCharacter(CharacterId characterId)
        {
            return _characters.TryGetValue(characterId.Value, out CharacterDefinition definition)
                ? definition
                : null;
        }

        public ArchiveEntryDefinition GetArchiveEntry(ArchiveEntryId entryId)
        {
            return _archiveEntries.TryGetValue(entryId.Value, out ArchiveEntryDefinition definition)
                ? definition
                : null;
        }

        public IReadOnlyList<LevelSummary> GetLevelsForMap(MapId mapId)
        {
            var levelSummaries = new List<LevelSummary>();
            if (_knownLevels == null)
                return levelSummaries.AsReadOnly();

            foreach (LevelDefinition definition in _knownLevels)
            {
                if (definition == null ||
                    !string.Equals(definition.MapId, mapId.Value, StringComparison.Ordinal))
                    continue;
                levelSummaries.Add(definition.Summary);
            }

            levelSummaries.Sort((left, right) => left.SortOrder.CompareTo(right.SortOrder));
            return levelSummaries.AsReadOnly();
        }

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
