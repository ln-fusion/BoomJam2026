using System.Collections.Generic;
using Game.Content;
using Game.Contracts.Content;
using NUnit.Framework;

namespace Game.Tests.EditMode.Content
{
    /// <summary>验证剧情编译信封的序列化、兼容解析与摘要稳定性。</summary>
    public sealed class StoryRuntimeSerializerTests
    {
        /// <summary>构造最小线性剧情: start 对白 -> end。</summary>
        private static StoryDefinition LinearStory()
        {
            return new StoryDefinition
            {
                StoryId = "official.story.serializer_test",
                StartNodeId = "start",
                Nodes = new List<StoryNodeDefinition>
                {
                    new StoryNodeDefinition { NodeId = "end", Type = StoryNodeType.End },
                    new StoryNodeDefinition
                    {
                        NodeId = "start",
                        Type = StoryNodeType.Dialogue,
                        TextKey = "story.test.line",
                        NextNodeId = "end",
                    },
                },
            };
        }

        /// <summary>信封序列化后能解析出等价的剧情定义。</summary>
        [Test]
        public void Envelope_SerializesAndRoundTrips()
        {
            StoryDefinition story = LinearStory();
            string json = StoryRuntimeSerializer.SerializeEnvelope(story);
            Assert.That(StoryRuntimeSerializer.TryDeserialize(json, out StoryDefinition parsed), Is.True);
            Assert.That(parsed.StoryId, Is.EqualTo(story.StoryId));
            Assert.That(parsed.StartNodeId, Is.EqualTo(story.StartNodeId));
            Assert.That(parsed.Nodes, Has.Count.EqualTo(2));
        }

        /// <summary>同内容两次编译摘要一致, 与节点声明顺序无关。</summary>
        [Test]
        public void SourceHash_IsOrderIndependentAndStable()
        {
            string first = StoryRuntimeSerializer.ComputeSourceHash(LinearStory());
            StoryDefinition reversed = LinearStory();
            reversed.Nodes.Reverse();
            Assert.That(StoryRuntimeSerializer.ComputeSourceHash(reversed), Is.EqualTo(first));
        }

        /// <summary>改动节点内容后摘要变化。</summary>
        [Test]
        public void SourceHash_ChangesWithContent()
        {
            StoryDefinition story = LinearStory();
            string before = StoryRuntimeSerializer.ComputeSourceHash(story);
            story.Nodes[1].TextKey = "story.test.line.changed";
            Assert.That(StoryRuntimeSerializer.ComputeSourceHash(story), Is.Not.EqualTo(before));
        }

        /// <summary>旧版裸 StoryDefinition JSON 仍可回退解析（c06 回归不破）。</summary>
        [Test]
        public void TryDeserialize_FallsBackToBareStoryJson()
        {
            StoryDefinition story = LinearStory();
            string bareJson = UnityEngine.JsonUtility.ToJson(story, true);
            Assert.That(StoryRuntimeSerializer.TryDeserialize(bareJson, out StoryDefinition parsed), Is.True);
            Assert.That(parsed.StoryId, Is.EqualTo(story.StoryId));
        }

        /// <summary>空白与损坏 JSON 返回失败且不抛异常。</summary>
        [Test]
        public void TryDeserialize_RejectsEmptyAndGarbage()
        {
            Assert.That(StoryRuntimeSerializer.TryDeserialize("", out _), Is.False);
            Assert.That(StoryRuntimeSerializer.TryDeserialize("{ not json", out _), Is.False);
            Assert.That(StoryRuntimeSerializer.TryDeserialize(null, out _), Is.False);
        }
    }
}
