using System;
using Game.Contracts.Story;
using Game.Foundation;
using NUnit.Framework;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>验证剧情历史记录与文本快照的字段语义。</summary>
    public sealed class StoryHistoryEntryTests
    {
        /// <summary>对白记录保留序号、角色、说话人与正文快照, 且不标记为选项。</summary>
        [Test]
        public void ForDialogue_KeepsSnapshotAndSequence()
        {
            var storyId = new StoryId("official.story.test");
            var nodeId = new StoryNodeId("start");
            var speakerId = new CharacterId("official.character.hani");
            var speaker = new LocalizedTextSnapshot("story.speaker.hani", "Hanī", "zh-CN");
            var text = new LocalizedTextSnapshot("story.text.hello", "你好", "zh-CN");

            StoryHistoryEntry entry = StoryHistoryEntry.ForDialogue(
                7,
                storyId,
                nodeId,
                speakerId,
                speaker,
                text
            );

            Assert.That(entry.Sequence, Is.EqualTo(7));
            Assert.That(entry.StoryId, Is.SameAs(storyId));
            Assert.That(entry.NodeId, Is.SameAs(nodeId));
            Assert.That(entry.SpeakerId, Is.SameAs(speakerId));
            Assert.That(entry.Speaker.Text, Is.EqualTo("Hanī"));
            Assert.That(entry.Speaker.LocaleCode, Is.EqualTo("zh-CN"));
            Assert.That(entry.Text.Text, Is.EqualTo("你好"));
            Assert.That(entry.IsChoice, Is.False);
            Assert.That(entry.SelectedChoiceId, Is.Null);
        }

        /// <summary>选项记录标记 IsChoice 并保留所选选项标识, 且不带说话人。</summary>
        [Test]
        public void ForChoice_MarksChoiceAndSelectedId()
        {
            var choiceId = new ChoiceId("left");
            StoryHistoryEntry entry = StoryHistoryEntry.ForChoice(
                3,
                new StoryId("official.story.test"),
                new StoryNodeId("choice"),
                choiceId,
                new LocalizedTextSnapshot("story.c06.left", "向左", "zh-CN")
            );

            Assert.That(entry.IsChoice, Is.True);
            Assert.That(entry.SelectedChoiceId, Is.SameAs(choiceId));
            Assert.That(entry.Text.Text, Is.EqualTo("向左"));
            Assert.That(entry.SpeakerId, Is.Null);
            Assert.That(entry.Speaker.IsEmpty, Is.True, "选项记录不应带说话人文本");
        }

        /// <summary>缺失的标识按契约抛出参数异常, 避免产生无法定位的历史。</summary>
        [Test]
        public void NullIdentifiers_Throw()
        {
            Assert.Throws<ArgumentNullException>(() =>
                StoryHistoryEntry.ForDialogue(
                    0,
                    null,
                    new StoryNodeId("start"),
                    null,
                    LocalizedTextSnapshot.Empty,
                    LocalizedTextSnapshot.Empty
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                StoryHistoryEntry.ForDialogue(
                    0,
                    new StoryId("official.story.test"),
                    null,
                    null,
                    LocalizedTextSnapshot.Empty,
                    LocalizedTextSnapshot.Empty
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                StoryHistoryEntry.ForChoice(
                    0,
                    new StoryId("official.story.test"),
                    new StoryNodeId("choice"),
                    null,
                    LocalizedTextSnapshot.Empty
                )
            );
        }

        /// <summary>未提供说话人时会话不区分 null 与空快照, 统一回退为空快照。</summary>
        [Test]
        public void NullSnapshots_FallBackToEmpty()
        {
            StoryHistoryEntry entry = StoryHistoryEntry.ForDialogue(
                0,
                new StoryId("official.story.test"),
                new StoryNodeId("start"),
                null,
                null,
                null
            );

            Assert.That(entry.Speaker, Is.SameAs(LocalizedTextSnapshot.Empty));
            Assert.That(entry.Text, Is.SameAs(LocalizedTextSnapshot.Empty));
            Assert.That(entry.Text.IsEmpty, Is.True);
        }

        /// <summary>空快照不携带可见文本, 但仍保留键与 Locale 的默认空值。</summary>
        [Test]
        public void EmptySnapshot_HasNoVisibleText()
        {
            Assert.That(LocalizedTextSnapshot.Empty.IsEmpty, Is.True);
            Assert.That(LocalizedTextSnapshot.Empty.Key, Is.Empty);
            Assert.That(LocalizedTextSnapshot.Empty.LocaleCode, Is.Empty);
        }
    }
}
