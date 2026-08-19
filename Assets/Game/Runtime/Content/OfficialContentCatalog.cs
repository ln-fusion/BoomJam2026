using System.Collections.Generic;
using UnityEngine;
using Game.Contracts.Content;

namespace Game.Content
{
    [CreateAssetMenu(fileName = "OfficialContentCatalog", menuName = "Game/Content/Official Catalog")]
    public sealed class OfficialContentCatalog : ScriptableObject
    {
        [SerializeField] private List<LevelDefinition> levels = new List<LevelDefinition>();
        [SerializeField] private List<StoryDefinition> stories = new List<StoryDefinition>();
        [SerializeField] private List<CharacterDefinition> characters = new List<CharacterDefinition>();
        [SerializeField] private List<ArchiveEntryDefinition> archiveEntries =
            new List<ArchiveEntryDefinition>();
        [SerializeField] private ContentAssetRegistry assetRegistry;

        public IReadOnlyList<LevelDefinition> Levels => levels;
        public IReadOnlyList<StoryDefinition> Stories => stories;
        public IReadOnlyList<CharacterDefinition> Characters => characters;
        public IReadOnlyList<ArchiveEntryDefinition> ArchiveEntries => archiveEntries;
        public ContentAssetRegistry AssetRegistry => assetRegistry;
    }
}
