using System;
using System.Collections.Generic;
using Game.Foundation;
using UnityEngine;

namespace Game.Contracts.Content
{
    public enum ContentSource
    {
        Official,
        Ugc
    }

    [Serializable]
    public sealed class ContentHeader
    {
        public string ContentId;
        public ContentSource Source = ContentSource.Official;
        public int FormatVersion = 1;
        public int ContentRevision = 1;
        public string MinGameVersion;
        public string MaxTestedGameVersion;
        public string PayloadSha256;
    }

    public enum ContentCompatibility
    {
        Compatible,
        MissingHeader,
        UnsupportedFormat,
        WrongSource,
        InvalidPayload
    }

    [Serializable]
    public sealed class LevelSummary
    {
        public string LevelId;
        public string MapId;
        public string DisplayNameKey;
        public int SortOrder;
    }

    [Serializable]
    public sealed class LevelDefinition
    {
        public ContentHeader Header;
        public string LevelId;
        public string MapId;
        public string DisplayNameKey;
        public LevelSummary Summary => new LevelSummary
        {
            LevelId = LevelId,
            MapId = MapId,
            DisplayNameKey = DisplayNameKey
        };
    }

    public enum StoryNodeType
    {
        Dialogue,
        End
    }

    [Serializable]
    public sealed class StoryNodeDefinition
    {
        public string NodeId;
        public StoryNodeType Type;
        public string TextKey;
        public string NextNodeId;
    }

    [Serializable]
    public sealed class StoryDefinition
    {
        public ContentHeader Header;
        public string StoryId;
        public List<StoryNodeDefinition> Nodes = new List<StoryNodeDefinition>();
    }

    [Serializable]
    public sealed class CharacterDefinition
    {
        public string CharacterId;
    }

    [Serializable]
    public sealed class ArchiveEntryDefinition
    {
        public string EntryId;
    }

    public interface IContentProvider
    {
        ContentSource Source { get; }
        bool TryGetLevel(LevelId levelId, out LevelDefinition definition);
        bool TryGetStory(StoryId storyId, out StoryDefinition definition);
    }

    public interface IContentService
    {
        LevelDefinition GetLevel(LevelId levelId);
        StoryDefinition GetStory(StoryId storyId);
        CharacterDefinition GetCharacter(CharacterId characterId);
        ArchiveEntryDefinition GetArchiveEntry(ArchiveEntryId entryId);
        IReadOnlyList<LevelSummary> GetLevelsForMap(MapId mapId);
        ContentCompatibility CheckCompatibility(ContentHeader header);
    }

    public interface IAssetResolver
    {
        GameObject GetPrefab(PrefabId id);
        Sprite GetSprite(SpriteId id);
        AudioClip GetAudio(AudioId id);
    }
}
