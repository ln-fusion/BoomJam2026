using System.Collections.Generic;
using Game.Contracts.Content;

namespace Game.Content
{
    /// <summary>Builds deterministic C06 content used by EditMode acceptance tests.</summary>
    public static class OfficialTestMapCatalog
    {
        /// <summary>创建六张测试地图、每图五个关卡和各关独立的关前、关后测试剧情。</summary>
        /// <returns>包含 30 个稳定测试关卡、关前关后剧情和旧 C06 测试剧情的内容提供者。</returns>
        public static OfficialContentProvider CreateProvider()
        {
            var maps = new List<MapDefinition>();
            var levels = new List<LevelDefinition>();
            var stories = new List<StoryDefinition>
            {
                CreateBranchingStory("official.story.c06_branch")
            };
            for (int mapIndex = 1; mapIndex <= 6; mapIndex++)
            {
                string mapId = "official.map.test_" + mapIndex.ToString("00");
                var map = new MapDefinition
                {
                    Header = Header(mapId), MapId = mapId,
                    DisplayNameKey = "map.test_" + mapIndex.ToString("00"),
                    SortOrder = mapIndex
                };
                for (int levelIndex = 1; levelIndex <= 5; levelIndex++)
                {
                    string levelId = "official.level.test_" + mapIndex.ToString("00") + "_" + levelIndex.ToString("00");
                    string preludeStoryId = "official.story.prelude.test_" +
                        mapIndex.ToString("00") + "_" + levelIndex.ToString("00");
                    var level = new LevelDefinition
                    {
                        Header = Header(levelId), LevelId = levelId, MapId = mapId,
                        DisplayNameKey = "level.test_" + mapIndex.ToString("00") + "_" + levelIndex.ToString("00"),
                        SortOrder = levelIndex, PreludeStoryId = preludeStoryId,
                        PostludeStoryId = "official.story.postlude.test_" +
                            mapIndex.ToString("00") + "_" + levelIndex.ToString("00")
                    };
                    if (levelIndex > 1)
                    {
                        level.UnlockRequirement = new UnlockRequirementData
                        {
                            Mode = UnlockRequirementMode.All,
                            RequiredLevelIds = new List<string> { "official.level.test_" + mapIndex.ToString("00") + "_" + (levelIndex - 1).ToString("00") }
                        };
                    }
                    levels.Add(level);
                    map.Levels.Add(level.Summary);
                    stories.Add(CreateBranchingStory(preludeStoryId));
                    if (!string.IsNullOrWhiteSpace(level.PostludeStoryId))
                        stories.Add(CreateBranchingStory(level.PostludeStoryId));
                }
                maps.Add(map);
            }

            return new OfficialContentProvider(maps, levels, stories);
        }

        /// <summary>创建使用当前白盒对白内容、但拥有独立完成事实 ID 的分支剧情。</summary>
        /// <param name="storyId">剧情稳定标识。</param>
        /// <returns>可由剧情运行器播放的分支剧情定义。</returns>
        private static StoryDefinition CreateBranchingStory(string storyId)
        {
            return new StoryDefinition
            {
                Header = Header(storyId), StoryId = storyId,
                Nodes = new List<StoryNodeDefinition>
                {
                    new StoryNodeDefinition { NodeId = "start", Type = StoryNodeType.Dialogue, TextKey = "story.c06.start", NextNodeId = "choice" },
                    new StoryNodeDefinition
                    {
                        NodeId = "choice", Type = StoryNodeType.Choice,
                        Choices = new List<StoryChoiceDefinition>
                        {
                            new StoryChoiceDefinition { ChoiceId = "left", TextKey = "story.c06.left", NextNodeId = "left_path" },
                            new StoryChoiceDefinition { ChoiceId = "right", TextKey = "story.c06.right", NextNodeId = "right_path" }
                        }
                    },
                    new StoryNodeDefinition { NodeId = "left_path", Type = StoryNodeType.Goto, NextNodeId = "merge" },
                    new StoryNodeDefinition { NodeId = "right_path", Type = StoryNodeType.Goto, NextNodeId = "merge" },
                    new StoryNodeDefinition { NodeId = "merge", Type = StoryNodeType.Dialogue, TextKey = "story.c06.merge", NextNodeId = "end" },
                    new StoryNodeDefinition { NodeId = "end", Type = StoryNodeType.End }
                }
            };
        }

        /// <summary>创建官方测试内容使用的兼容内容头。</summary>
        /// <param name="id">内容稳定标识。</param>
        /// <returns>格式版本为 1 的官方内容头。</returns>
        private static ContentHeader Header(string id)
        {
            return new ContentHeader { ContentId = id, Source = ContentSource.Official, FormatVersion = 1 };
        }
    }
}
