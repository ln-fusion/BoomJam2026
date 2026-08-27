using System.Collections.Generic;
using Game.Content;
using Game.Contracts.Content;
using NUnit.Framework;

namespace Game.Tests.EditMode.Content
{
    /// <summary>
    /// C17 剧情演出节点校验测试：新演出节点类型的必填字段与资源存在性校验。
    /// </summary>
    public sealed class StoryDefinitionPerformanceValidationTests
    {
        /// <summary>构造带单个演出节点的剧情定义。</summary>
        /// <param name="node">演出节点。</param>
        /// <returns>以 start 开头、以 end 收尾的完整剧情。</returns>
        private static StoryDefinition Story(StoryNodeDefinition node)
        {
            return new StoryDefinition
            {
                StoryId = "official.story.perf_test",
                StartNodeId = "start",
                Nodes = new List<StoryNodeDefinition>
                {
                    node,
                    new StoryNodeDefinition { NodeId = "end", Type = StoryNodeType.End },
                },
            };
        }

        /// <summary>验证 SetBackground 缺 BackgroundId 时校验失败。</summary>
        [Test]
        public void TryValidate_SetBackground_WithoutId_Fails()
        {
            StoryNodeDefinition node = new StoryNodeDefinition
            {
                NodeId = "start",
                Type = StoryNodeType.SetBackground,
                NextNodeId = "end",
            };

            Assert.That(StoryDefinitionValidator.TryValidate(Story(node), out _), Is.False);
        }

        /// <summary>验证 HideCharacter 缺说话角色时校验失败。</summary>
        [Test]
        public void TryValidate_HideCharacter_WithoutSpeaker_Fails()
        {
            StoryNodeDefinition node = new StoryNodeDefinition
            {
                NodeId = "start",
                Type = StoryNodeType.HideCharacter,
                NextNodeId = "end",
            };

            Assert.That(StoryDefinitionValidator.TryValidate(Story(node), out _), Is.False);
        }

        /// <summary>验证 MoveCharacter 缺说话角色时校验失败。</summary>
        [Test]
        public void TryValidate_MoveCharacter_WithoutSpeaker_Fails()
        {
            StoryNodeDefinition node = new StoryNodeDefinition
            {
                NodeId = "start",
                Type = StoryNodeType.MoveCharacter,
                NextNodeId = "end",
            };

            Assert.That(StoryDefinitionValidator.TryValidate(Story(node), out _), Is.False);
        }

        /// <summary>验证 PlayAudio 缺 AudioId 时校验失败。</summary>
        [Test]
        public void TryValidate_PlayAudio_WithoutId_Fails()
        {
            StoryNodeDefinition node = new StoryNodeDefinition
            {
                NodeId = "start",
                Type = StoryNodeType.PlayAudio,
                NextNodeId = "end",
            };

            Assert.That(StoryDefinitionValidator.TryValidate(Story(node), out _), Is.False);
        }

        /// <summary>验证 ScreenEffect 使用 None 效果时校验失败。</summary>
        [Test]
        public void TryValidate_ScreenEffect_None_Fails()
        {
            StoryNodeDefinition node = new StoryNodeDefinition
            {
                NodeId = "start",
                Type = StoryNodeType.ScreenEffect,
                EffectType = StoryScreenEffectType.None,
                NextNodeId = "end",
            };

            Assert.That(StoryDefinitionValidator.TryValidate(Story(node), out _), Is.False);
        }

        /// <summary>验证 Wait 秒数不大于 0 时校验失败。</summary>
        [Test]
        public void TryValidate_Wait_NonPositive_Fails()
        {
            StoryNodeDefinition node = new StoryNodeDefinition
            {
                NodeId = "start",
                Type = StoryNodeType.Wait,
                WaitSeconds = 0f,
                NextNodeId = "end",
            };

            Assert.That(StoryDefinitionValidator.TryValidate(Story(node), out _), Is.False);
        }

        /// <summary>验证带齐全参数的演出剧情通过校验。</summary>
        [Test]
        public void TryValidate_PerformanceNodes_Complete_Passes()
        {
            var story = new StoryDefinition
            {
                StoryId = "official.story.perf_ok",
                StartNodeId = "start",
                Nodes = new List<StoryNodeDefinition>
                {
                    new StoryNodeDefinition
                    {
                        NodeId = "start",
                        Type = StoryNodeType.SetBackground,
                        BackgroundId = "official.background.test_01",
                        NextNodeId = "end",
                    },
                    new StoryNodeDefinition { NodeId = "end", Type = StoryNodeType.End },
                },
            };

            Assert.That(StoryDefinitionValidator.TryValidate(story, out _), Is.True);
        }

        /// <summary>验证资源存在性谓词发现不存在资源时失败。</summary>
        [Test]
        public void TryValidate_AssetExists_MissingAsset_Fails()
        {
            StoryNodeDefinition node = new StoryNodeDefinition
            {
                NodeId = "start",
                Type = StoryNodeType.SetBackground,
                BackgroundId = "official.background.missing",
                NextNodeId = "end",
            };

            Assert.That(
                StoryDefinitionValidator.TryValidate(
                    Story(node),
                    null,
                    id => id == "official.background.test_01",
                    out _
                ),
                Is.False
            );
        }

        /// <summary>验证资源存在性谓词发现资源存在时通过。</summary>
        [Test]
        public void TryValidate_AssetExists_KnownAsset_Passes()
        {
            StoryNodeDefinition node = new StoryNodeDefinition
            {
                NodeId = "start",
                Type = StoryNodeType.SetBackground,
                BackgroundId = "official.background.test_01",
                NextNodeId = "end",
            };

            Assert.That(
                StoryDefinitionValidator.TryValidate(
                    Story(node),
                    null,
                    id => id == "official.background.test_01",
                    out _
                ),
                Is.True
            );
        }
    }
}
