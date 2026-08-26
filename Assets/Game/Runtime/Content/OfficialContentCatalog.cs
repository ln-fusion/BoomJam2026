using System.Collections.Generic;
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
    }
}
