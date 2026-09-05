using System.Collections.Generic;
using System.Globalization;
using Game.Contracts.Content;

namespace Game.Content
{
    /// <summary>Builds deterministic C06 content used by EditMode acceptance tests.</summary>
    public static class OfficialTestMapCatalog
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>Creates six maps with five ordered level nodes each and one branching story.</summary>
        /// <returns>A provider containing 30 stable test levels and a branching story.</returns>
        public static OfficialContentProvider CreateProvider()
        {
            var maps = new List<MapDefinition>();
            var levels = new List<LevelDefinition>();
            for (int mapIndex = 1; mapIndex <= 6; mapIndex++)
            {
                string mapId = "official.map.test_" + mapIndex.ToString("00", Invariant);
                var map = new MapDefinition
                {
                    Header = Header(mapId),
                    MapId = mapId,
                    DisplayNameKey = "map.test_" + mapIndex.ToString("00", Invariant),
                    SortOrder = mapIndex,
                };
                for (int levelIndex = 1; levelIndex <= 5; levelIndex++)
                {
                    string levelId =
                        "official.level.test_"
                        + mapIndex.ToString("00", Invariant)
                        + "_"
                        + levelIndex.ToString("00", Invariant);
                    var level = new LevelDefinition
                    {
                        Header = Header(levelId),
                        LevelId = levelId,
                        MapId = mapId,
                        DisplayNameKey =
                            "level.test_"
                            + mapIndex.ToString("00", Invariant)
                            + "_"
                            + levelIndex.ToString("00", Invariant),
                        SortOrder = levelIndex,
                    };
                    if (levelIndex > 1)
                    {
                        level.UnlockRequirement = new UnlockRequirementData
                        {
                            Mode = UnlockRequirementMode.All,
                            RequiredLevelIds = new List<string>
                            {
                                "official.level.test_"
                                    + mapIndex.ToString("00", Invariant)
                                    + "_"
                                    + (levelIndex - 1).ToString("00", Invariant),
                            },
                        };
                    }
                    levels.Add(level);
                    map.Levels.Add(level.Summary);
                }
                maps.Add(map);
            }

            var story = new StoryDefinition
            {
                Header = Header("official.story.c06_branch"),
                StoryId = "official.story.c06_branch",
                Nodes = new List<StoryNodeDefinition>
                {
                    new StoryNodeDefinition
                    {
                        NodeId = "start",
                        Type = StoryNodeType.Dialogue,
                        SpeakerKey = "story.speaker.unknown",
                        SpeakerCharacterId = "official.character.hani",
                        TextKey = "story.c06.start",
                        NextNodeId = "choice",
                    },
                    new StoryNodeDefinition
                    {
                        NodeId = "choice",
                        Type = StoryNodeType.Choice,
                        Choices = new List<StoryChoiceDefinition>
                        {
                            new StoryChoiceDefinition
                            {
                                ChoiceId = "left",
                                TextKey = "story.c06.left",
                                NextNodeId = "left_path",
                            },
                            new StoryChoiceDefinition
                            {
                                ChoiceId = "right",
                                TextKey = "story.c06.right",
                                NextNodeId = "right_path",
                            },
                        },
                    },
                    new StoryNodeDefinition
                    {
                        NodeId = "left_path",
                        Type = StoryNodeType.Goto,
                        NextNodeId = "merge",
                    },
                    new StoryNodeDefinition
                    {
                        NodeId = "right_path",
                        Type = StoryNodeType.Goto,
                        NextNodeId = "merge",
                    },
                    new StoryNodeDefinition
                    {
                        NodeId = "merge",
                        Type = StoryNodeType.Dialogue,
                        SpeakerKey = "story.speaker.unknown",
                        SpeakerCharacterId = "official.character.hani",
                        AppearanceOverride = "official.appearance.hani.casual",
                        TextKey = "story.c06.merge",
                        NextNodeId = "end",
                    },
                    new StoryNodeDefinition { NodeId = "end", Type = StoryNodeType.End },
                },
            };
            return new OfficialContentProvider(maps, levels, new[] { story });
        }

        /// <summary>创建包含 hani 角色的测试角色定义集合。</summary>
        /// <returns>包含一个角色与两种形象的测试集合。</returns>
        public static IReadOnlyCollection<CharacterDefinition> CreateCharacters()
        {
            return new[]
            {
                new CharacterDefinition
                {
                    CharacterId = "official.character.hani",
                    AppearanceIds = new List<string>
                    {
                        "official.appearance.hani.casual",
                        "official.appearance.hani.uniform",
                    },
                    DefaultAppearanceId = "official.appearance.hani.casual",
                },
            };
        }

        private static ContentHeader Header(string id)
        {
            return new ContentHeader
            {
                ContentId = id,
                Source = ContentSource.Official,
                FormatVersion = 1,
            };
        }
    }
}
