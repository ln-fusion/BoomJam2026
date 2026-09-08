using System;
using System.Linq;
using Game.Content;
using Game.Contracts.Content;
using NUnit.Framework;

namespace Game.Tests.EditMode.Content
{
    /// <summary>Validates the deterministic C06 map and branching story fixtures.</summary>
    public sealed class C06ContentContractTests
    {
        /// <summary>验证第一关的关后剧情可播放、摘要保留引用，且不会与关前剧情共享完成标识。</summary>
        [Test]
        public void FirstLevel_PostludeIsValidAndDistinctFromPrelude()
        {
            var provider = OfficialTestMapCatalog.CreateProvider();
            var first = provider.Levels.Single(level => level.LevelId == "official.level.test_01_01");
            Assert.That(first.PostludeStoryId, Is.EqualTo("official.story.postlude.test_01_01"));
            Assert.That(first.Summary.PostludeStoryId, Is.EqualTo(first.PostludeStoryId));
            Assert.That(first.PostludeStoryId, Is.Not.EqualTo(first.PreludeStoryId));
            Assert.That(provider.TryGetStory(new Game.Foundation.StoryId(first.PostludeStoryId),
                out StoryDefinition story), Is.True);
            Assert.That(StoryDefinitionValidator.TryValidate(story, out string error), Is.True, error);
            Assert.That(provider.Levels.Where(level => level != first)
                .All(level => string.IsNullOrEmpty(level.PostludeStoryId)), Is.True);
        }

        /// <summary>Ensures six maps and thirty ordered levels pass stable ID validation.</summary>
        [Test]
        public void OfficialFixtures_ContainSixMapsAndThirtyLevels()
        {
            OfficialContentProvider provider = OfficialTestMapCatalog.CreateProvider();
            Assert.That(MapContentValidator.TryValidate(provider, out string error), Is.True, error);
            Assert.That(provider.Maps, Has.Count.EqualTo(6));
            Assert.That(provider.Levels, Has.Count.EqualTo(30));
            Assert.That(provider.Levels, Has.All.Matches<LevelDefinition>(level =>
                !string.IsNullOrWhiteSpace(level.PreludeStoryId)));
            Assert.That(provider.Levels.Select(level => level.PreludeStoryId).Distinct(),
                Has.Count.EqualTo(30));
        }

        /// <summary>Ensures the fixture story has a choice branch that converges.</summary>
        [Test]
        public void OfficialFixtures_ContainValidBranchingStory()
        {
            OfficialContentProvider provider = OfficialTestMapCatalog.CreateProvider();
            Assert.That(StoryDefinitionValidator.TryValidate(
                provider.TryGetStory(new Game.Foundation.StoryId("official.story.c06_branch"),
                    out Game.Contracts.Content.StoryDefinition story) ? story : null,
                out string error), Is.True, error);
        }
    }
}
