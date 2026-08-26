using Game.Contracts.Story;
using Game.Foundation;
using Game.Story;
using NUnit.Framework;

namespace Game.Tests.EditMode.Story
{
    /// <summary>Verifies C07 story execution without a UI.</summary>
    public sealed class StoryRunnerTests
    {
        /// <summary>Executes a linear story to its end without a UI.</summary>
        [Test]
        public void LinearStory_ExecutesToEnd()
        {
            var story = new Game.Contracts.Content.StoryDefinition
            {
                StoryId = "official.story.c07_linear",
                Nodes = new System.Collections.Generic.List<Game.Contracts.Content.StoryNodeDefinition>
                {
                    new Game.Contracts.Content.StoryNodeDefinition { NodeId = "start", Type = Game.Contracts.Content.StoryNodeType.Dialogue, TextKey = "story.c07.start", NextNodeId = "end" },
                    new Game.Contracts.Content.StoryNodeDefinition { NodeId = "end", Type = Game.Contracts.Content.StoryNodeType.End }
                }
            };
            var runner = new StoryRunner(id => story);
            runner.Start(new StoryId("official.story.c07_linear"));
            Assert.That(runner.Advance().IsSuccess, Is.True);
            Assert.That(runner.Advance().IsSuccess, Is.True);
            Assert.That(runner.GetSnapshot().IsCompleted, Is.True);
        }

        /// <summary>Executes a selected branch and reaches the shared convergence node.</summary>
        [Test]
        public void ChoiceBranch_Converges()
        {
            var provider = Game.Content.OfficialTestMapCatalog.CreateProvider();
            var runner = new StoryRunner(id => provider.TryGetStory(id, out var story) ? story : null);
            runner.Start(new StoryId("official.story.c06_branch"));
            Assert.That(runner.Advance().IsSuccess, Is.True);
            Assert.That(runner.Choose(new ChoiceId("left")).IsSuccess, Is.True);
            Assert.That(runner.Advance().IsSuccess, Is.True);
            Assert.That(runner.Advance().IsSuccess, Is.True);
            Assert.That(runner.Advance().IsSuccess, Is.True);
            Assert.That(runner.GetSnapshot().IsCompleted, Is.True);
        }

        /// <summary>Stops a self-loop after the configured execution limit.</summary>
        [Test]
        public void SelfLoop_IsBoundedByExecutionLimit()
        {
            var story = new Game.Contracts.Content.StoryDefinition
            {
                StoryId = "official.story.loop",
                Nodes = new System.Collections.Generic.List<Game.Contracts.Content.StoryNodeDefinition>
                {
                    new Game.Contracts.Content.StoryNodeDefinition { NodeId = "start", Type = Game.Contracts.Content.StoryNodeType.Goto, NextNodeId = "start" }
                }
            };
            var runner = new StoryRunner(id => story);
            runner.Start(new StoryId("official.story.loop"));
            Game.Foundation.Result result = Game.Foundation.Result.Success();
            for (int i = 0; i < 300 && result.IsSuccess; i++)
                result = runner.Advance();
            Assert.That(result.IsSuccess, Is.False);
        }
    }
}
