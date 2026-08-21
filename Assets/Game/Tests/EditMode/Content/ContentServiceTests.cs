using System.Collections.Generic;
using Game.Content;
using Game.Contracts.Content;
using Game.Foundation;
using NUnit.Framework;

namespace Game.Tests.EditMode.Content
{
    public sealed class ContentServiceTests
    {
        [Test]
        public void OfficialProvider_ReadsTestLevelSummaryAndStory()
        {
            var level = new LevelDefinition
            {
                Header = Header("official.level.factory_001"),
                LevelId = "official.level.factory_001",
                MapId = "official.map.factory",
                DisplayNameKey = "level.factory_001.name"
            };
            var story = new StoryDefinition
            {
                Header = Header("official.story.prologue"),
                StoryId = "official.story.prologue",
                Nodes = new List<StoryNodeDefinition>
                {
                    new StoryNodeDefinition
                    {
                        NodeId = "start",
                        Type = StoryNodeType.Dialogue,
                        TextKey = "story.prologue.start",
                        NextNodeId = "end"
                    },
                    new StoryNodeDefinition { NodeId = "end", Type = StoryNodeType.End }
                }
            };

            var provider = new OfficialContentProvider(new[] { level }, new[] { story });
            var service = new OfficialContentService(provider);

            IReadOnlyList<LevelSummary> summaries = service.GetLevelsForMap(
                new MapId("official.map.factory"));
            Assert.That(summaries, Has.Count.EqualTo(1));
            Assert.That(summaries[0].DisplayNameKey, Is.EqualTo("level.factory_001.name"));
            Assert.That(service.GetStory(new StoryId("official.story.prologue")), Is.SameAs(story));
            Assert.That(service.CheckCompatibility(level.Header),
                Is.EqualTo(ContentCompatibility.Compatible));
        }

        [Test]
        public void OfficialProvider_RejectsDuplicateStableIds()
        {
            var first = new LevelDefinition { LevelId = "official.level.duplicate" };
            var second = new LevelDefinition { LevelId = "official.level.duplicate" };

            Assert.Throws<System.ArgumentException>(() => new OfficialContentProvider(
                new[] { first, second }, null));
        }

        private static ContentHeader Header(string id)
        {
            return new ContentHeader { ContentId = id, FormatVersion = 1 };
        }
    }
}
