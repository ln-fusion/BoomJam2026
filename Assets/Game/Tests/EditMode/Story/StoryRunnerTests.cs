using Game.Contracts.Story;
using Game.Foundation;
using Game.Story;
using NUnit.Framework;

namespace Game.Tests.EditMode.Story
{
    /// <summary>Verifies C07 story execution without a UI.</summary>
    public sealed class StoryRunnerTests
    {
        /// <summary>多次非法推进和选择不消耗有效路径的执行额度。</summary>
        [Test]
        public void InvalidChoiceAttempts_DoNotConsumeExecutionBudget()
        {
            var provider = Game.Content.OfficialTestMapCatalog.CreateProvider();
            var runner = new StoryRunner(id => provider.TryGetStory(id, out var story) ? story : null);
            runner.Start(new StoryId("official.story.c06_branch"));
            Assert.That(runner.Advance().IsSuccess, Is.True);
            for (int i = 0; i < 300; i++)
            {
                Assert.That(runner.Advance().IsSuccess, Is.False);
                Assert.That(runner.Choose(new ChoiceId("missing")).IsSuccess, Is.False);
            }
            Assert.That(runner.Choose(new ChoiceId("left")).IsSuccess, Is.True);
            Assert.That(runner.Skip().IsSuccess, Is.True);
            Assert.That(runner.GetSnapshot().IsCompleted, Is.True);
        }

        /// <summary>新剧情索引无效时保留原有会话、节点和执行能力。</summary>
        [Test]
        public void FailedStart_PreservesExistingSession()
        {
            var provider = Game.Content.OfficialTestMapCatalog.CreateProvider();
            var invalid = new Game.Contracts.Content.StoryDefinition
            {
                StoryId = "invalid",
                StartNodeId = "missing"
            };
            var runner = new StoryRunner(id => id.Value == "invalid" ? invalid :
                (provider.TryGetStory(id, out var story) ? story : null));
            var session = runner.Start(new StoryId("official.story.c06_branch"));
            Assert.That(runner.Advance().IsSuccess, Is.True);
            var before = runner.GetSnapshot();
            Assert.Throws<System.ArgumentException>(() => runner.Start(new StoryId("invalid")));
            Assert.That(runner.GetSnapshot().StoryId, Is.EqualTo(session.StoryId));
            Assert.That(runner.GetSnapshot().CurrentNode.NodeId, Is.EqualTo(before.CurrentNode.NodeId));
            Assert.That(runner.Choose(new ChoiceId("left")).IsSuccess, Is.True);
        }

        /// <summary>Executes a linear story to its end without a UI.</summary>
        [Test]
        public void LinearStory_ExecutesToEnd()
        {
            var story = new Game.Contracts.Content.StoryDefinition
            {
                StoryId = "official.story.c07_linear",
                Nodes = new System.Collections.Generic.List<Game.Contracts.Content.StoryNodeDefinition>
                {
                    new Game.Contracts.Content.StoryNodeDefinition
                    {
                        NodeId = "start",
                        Type = Game.Contracts.Content.StoryNodeType.Dialogue,
                        TextZhCn = "测试对白。",
                        TextEnUs = "Test dialogue.",
                        NextNodeId = "end",
                    },
                    new Game.Contracts.Content.StoryNodeDefinition
                    {
                        NodeId = "end",
                        Type = Game.Contracts.Content.StoryNodeType.End,
                    },
                },
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
                    new Game.Contracts.Content.StoryNodeDefinition
                    {
                        NodeId = "start",
                        Type = Game.Contracts.Content.StoryNodeType.Goto,
                        NextNodeId = "start",
                    },
                },
            };
            var runner = new StoryRunner(id => story);
            runner.Start(new StoryId("official.story.loop"));
            Game.Foundation.Result result = Game.Foundation.Result.Success();
            for (int i = 0; i < 300 && result.IsSuccess; i++)
                result = runner.Advance();
            Assert.That(result.IsSuccess, Is.False);
        }

        /// <summary>演出节点（ShowCharacter/ShowCg）自动推进到下一节点。</summary>
        [Test]
        public void PerformanceNode_AutoAdvancesToNext()
        {
            var story = new Game.Contracts.Content.StoryDefinition
            {
                StoryId = "official.story.perf",
                StartNodeId = "show",
                Nodes = new System.Collections.Generic.List<Game.Contracts.Content.StoryNodeDefinition>
                {
                    new Game.Contracts.Content.StoryNodeDefinition
                    {
                        NodeId = "show",
                        Type = Game.Contracts.Content.StoryNodeType.ShowCharacter,
                        SpeakerCharacterId = "official.character.hani",
                        NextNodeId = "end",
                    },
                    new Game.Contracts.Content.StoryNodeDefinition
                    {
                        NodeId = "end",
                        Type = Game.Contracts.Content.StoryNodeType.End,
                    },
                },
            };
            var runner = new StoryRunner(id => story);
            runner.Start(new StoryId("official.story.perf"));
            Assert.That(runner.Advance().IsSuccess, Is.True);
            Assert.That(runner.Advance().IsSuccess, Is.True);
            Assert.That(runner.GetSnapshot().IsCompleted, Is.True);
        }

        /// <summary>C17 演出节点（SetBackground/HideCharacter/MoveCharacter/PlayAudio/ScreenEffect/Wait）全部自动推进到下一节点。</summary>
        [Test]
        public void C17PerformanceNodes_AutoAdvanceToEnd()
        {
            var story = new Game.Contracts.Content.StoryDefinition
            {
                StoryId = "official.story.c17_perf",
                StartNodeId = "bg",
                Nodes = new System.Collections.Generic.List<Game.Contracts.Content.StoryNodeDefinition>
                {
                    new Game.Contracts.Content.StoryNodeDefinition
                    {
                        NodeId = "bg",
                        Type = Game.Contracts.Content.StoryNodeType.SetBackground,
                        BackgroundId = "official.background.test_01",
                        NextNodeId = "hide",
                    },
                    new Game.Contracts.Content.StoryNodeDefinition
                    {
                        NodeId = "hide",
                        Type = Game.Contracts.Content.StoryNodeType.HideCharacter,
                        SpeakerCharacterId = "official.character.hani",
                        NextNodeId = "move",
                    },
                    new Game.Contracts.Content.StoryNodeDefinition
                    {
                        NodeId = "move",
                        Type = Game.Contracts.Content.StoryNodeType.MoveCharacter,
                        SpeakerCharacterId = "official.character.hani",
                        CharacterPosition = Game.Contracts.Content.StoryCharacterPosition.Center,
                        NextNodeId = "audio",
                    },
                    new Game.Contracts.Content.StoryNodeDefinition
                    {
                        NodeId = "audio",
                        Type = Game.Contracts.Content.StoryNodeType.PlayAudio,
                        AudioId = "official.audio.bgm_01",
                        AudioKind = Game.Contracts.Content.StoryAudioKind.Music,
                        NextNodeId = "effect",
                    },
                    new Game.Contracts.Content.StoryNodeDefinition
                    {
                        NodeId = "effect",
                        Type = Game.Contracts.Content.StoryNodeType.ScreenEffect,
                        EffectType = Game.Contracts.Content.StoryScreenEffectType.WhiteFlash,
                        NextNodeId = "wait",
                    },
                    new Game.Contracts.Content.StoryNodeDefinition
                    {
                        NodeId = "wait",
                        Type = Game.Contracts.Content.StoryNodeType.Wait,
                        WaitSeconds = 0.5f,
                        NextNodeId = "end",
                    },
                    new Game.Contracts.Content.StoryNodeDefinition
                    {
                        NodeId = "end",
                        Type = Game.Contracts.Content.StoryNodeType.End,
                    },
                },
            };
            var runner = new StoryRunner(id => story);
            runner.Start(new StoryId("official.story.c17_perf"));
            for (int i = 0; i < 7; i++)
                Assert.That(runner.Advance().IsSuccess, Is.True, "advance step " + i);
            Assert.That(runner.GetSnapshot().IsCompleted, Is.True);
        }
    }
}
