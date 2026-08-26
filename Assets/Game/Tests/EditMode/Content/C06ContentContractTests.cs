using Game.Content;
using NUnit.Framework;

namespace Game.Tests.EditMode.Content
{
    /// <summary>Validates the deterministic C06 map and branching story fixtures.</summary>
    public sealed class C06ContentContractTests
    {
        /// <summary>Ensures six maps and thirty ordered levels pass stable ID validation.</summary>
        [Test]
        public void OfficialFixtures_ContainSixMapsAndThirtyLevels()
        {
            OfficialContentProvider provider = OfficialTestMapCatalog.CreateProvider();
            Assert.That(MapContentValidator.TryValidate(provider, out string error), Is.True, error);
            Assert.That(provider.Maps, Has.Count.EqualTo(6));
            Assert.That(provider.Levels, Has.Count.EqualTo(30));
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
