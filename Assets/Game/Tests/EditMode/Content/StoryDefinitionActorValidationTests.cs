using System.Collections.Generic;
using Game.Content;
using Game.Contracts.Content;
using NUnit.Framework;

namespace Game.Tests.EditMode.Content
{
    /// <summary>
    /// C16 剧情定义校验测试：角色引用、形象覆盖和演出节点约束。
    /// </summary>
    public sealed class StoryDefinitionActorValidationTests
    {
        private static readonly CharacterDefinition Hani = new CharacterDefinition
        {
            CharacterId = "official.character.hani",
            AppearanceIds = new List<string> { "official.appearance.hani.casual" },
            DefaultAppearanceId = "official.appearance.hani.casual",
        };

        /// <summary>验证带说话角色的剧情通过校验。</summary>
        [Test]
        public void TryValidate_WithKnownSpeaker_Passes()
        {
            StoryDefinition story = Story(
                new StoryNodeDefinition
                {
                    NodeId = "start",
                    Type = StoryNodeType.Dialogue,
                    SpeakerKey = "story.speaker.unknown",
                    SpeakerCharacterId = "official.character.hani",
                    TextKey = "story.test",
                    NextNodeId = "end",
                }
            );

            bool valid = StoryDefinitionValidator.TryValidate(story, new[] { Hani }, out string error);

            Assert.That(valid, Is.True, error);
        }

        /// <summary>验证未知说话角色会被拒绝。</summary>
        [Test]
        public void TryValidate_UnknownSpeaker_Fails()
        {
            StoryDefinition story = Story(
                new StoryNodeDefinition
                {
                    NodeId = "start",
                    Type = StoryNodeType.Dialogue,
                    SpeakerCharacterId = "official.character.unknown",
                    TextKey = "story.test",
                    NextNodeId = "end",
                }
            );

            Assert.That(StoryDefinitionValidator.TryValidate(story, new[] { Hani }, out _), Is.False);
        }

        /// <summary>验证形象覆盖不属于角色时被拒绝。</summary>
        [Test]
        public void TryValidate_InvalidAppearanceOverride_Fails()
        {
            StoryDefinition story = Story(
                new StoryNodeDefinition
                {
                    NodeId = "start",
                    Type = StoryNodeType.Dialogue,
                    SpeakerCharacterId = "official.character.hani",
                    AppearanceOverride = "official.appearance.hani.wedding",
                    TextKey = "story.test",
                    NextNodeId = "end",
                }
            );

            Assert.That(StoryDefinitionValidator.TryValidate(story, new[] { Hani }, out _), Is.False);
        }

        /// <summary>验证 ShowCharacter 缺少说话角色时被拒绝。</summary>
        [Test]
        public void TryValidate_ShowCharacter_WithoutSpeaker_Fails()
        {
            StoryDefinition story = Story(
                new StoryNodeDefinition
                {
                    NodeId = "start",
                    Type = StoryNodeType.ShowCharacter,
                    NextNodeId = "end",
                }
            );

            Assert.That(StoryDefinitionValidator.TryValidate(story, new[] { Hani }, out _), Is.False);
        }

        /// <summary>验证 ShowCg 缺少资源 ID 时被拒绝。</summary>
        [Test]
        public void TryValidate_ShowCg_WithoutAsset_Fails()
        {
            StoryDefinition story = Story(
                new StoryNodeDefinition
                {
                    NodeId = "start",
                    Type = StoryNodeType.ShowCg,
                    NextNodeId = "end",
                }
            );

            Assert.That(StoryDefinitionValidator.TryValidate(story, new[] { Hani }, out _), Is.False);
        }

        /// <summary>创建包含 start 与 end 的剧情。</summary>
        /// <param name="start">起始节点。</param>
        /// <returns>剧情定义。</returns>
        private static StoryDefinition Story(StoryNodeDefinition start)
        {
            return new StoryDefinition
            {
                StoryId = "official.story.test",
                StartNodeId = "start",
                Nodes = new List<StoryNodeDefinition>
                {
                    start,
                    new StoryNodeDefinition { NodeId = "end", Type = StoryNodeType.End },
                },
            };
        }
    }
}
