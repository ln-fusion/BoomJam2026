using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Foundation.Ids;

namespace Game.Content
{
    public sealed class OfficialContentProvider : IContentProvider
    {
        private readonly Dictionary<string, LevelDefinition> _levels =
            new Dictionary<string, LevelDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, StoryDefinition> _stories =
            new Dictionary<string, StoryDefinition>(StringComparer.Ordinal);

        public ContentSource Source => ContentSource.Official;
        public IReadOnlyCollection<LevelDefinition> Levels => _levels.Values;

        public OfficialContentProvider(IEnumerable<LevelDefinition> levels,
            IEnumerable<StoryDefinition> stories)
        {
            if (levels != null)
                foreach (LevelDefinition definition in levels)
                    AddLevel(definition);
            if (stories != null)
                foreach (StoryDefinition definition in stories)
                    AddStory(definition);
        }

        public OfficialContentProvider(OfficialContentCatalog catalog)
            : this(catalog == null ? null : catalog.Levels,
                   catalog == null ? null : catalog.Stories)
        {
        }

        public bool TryGetLevel(LevelId levelId, out LevelDefinition definition)
        {
            return _levels.TryGetValue(levelId.Value, out definition);
        }

        public bool TryGetStory(StoryId storyId, out StoryDefinition definition)
        {
            return _stories.TryGetValue(storyId.Value, out definition);
        }

        private void AddLevel(LevelDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.LevelId))
                throw new ArgumentException("Official levels require a stable LevelId.");
            if (!_levels.TryAdd(definition.LevelId, definition))
                throw new ArgumentException("Duplicate official LevelId: " + definition.LevelId);
        }

        private void AddStory(StoryDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.StoryId))
                throw new ArgumentException("Official stories require a stable StoryId.");
            if (!_stories.TryAdd(definition.StoryId, definition))
                throw new ArgumentException("Duplicate official StoryId: " + definition.StoryId);
        }
    }
}
