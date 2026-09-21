using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Foundation;

namespace Game.Contracts.Story
{
    /// <summary>某次显示时解析完成的文本快照，用于在会话内切换语言后仍按当时所见回放历史。</summary>
    public sealed class LocalizedTextSnapshot
    {
        /// <summary>解析时使用的本地化键；无键时为空字符串。</summary>
        public string Key { get; }
        /// <summary>本次实际显示的文本；剧情播放时来自 JSON 语言选择结果。</summary>
        public string Text { get; }
        /// <summary>解析时生效的 Locale 代码；未知时为空字符串。</summary>
        public string LocaleCode { get; }

        /// <summary>不含任何可见文本的空快照，供可为空的字段复用。</summary>
        public static LocalizedTextSnapshot Empty { get; } = new LocalizedTextSnapshot(
            string.Empty,
            string.Empty,
            string.Empty
        );

        /// <summary>创建文本快照。</summary>
        /// <param name="key">本次使用的本地化键。</param>
        /// <param name="text">本次实际显示的文本。</param>
        /// <param name="localeCode">解析时生效的 Locale 代码。</param>
        public LocalizedTextSnapshot(string key, string text, string localeCode)
        {
            Key = key ?? string.Empty;
            Text = text ?? string.Empty;
            LocaleCode = localeCode ?? string.Empty;
        }

        /// <summary>是否不含可见文本。</summary>
        public bool IsEmpty => string.IsNullOrEmpty(Text);
    }

    /// <summary>剧情历史页中的一条实际经过记录。</summary>
    public sealed class StoryHistoryEntry
    {
        /// <summary>所属会话内的追加序号，从 0 开始递增。</summary>
        public long Sequence { get; }
        /// <summary>所属剧情稳定标识。</summary>
        public StoryId StoryId { get; }
        /// <summary>实际经过的节点稳定标识。</summary>
        public StoryNodeId NodeId { get; }
        /// <summary>说话角色稳定标识；叙述或选项记录为 null。</summary>
        public CharacterId SpeakerId { get; }
        /// <summary>说话人文本快照；无说话人时为空快照。</summary>
        public LocalizedTextSnapshot Speaker { get; }
        /// <summary>正文或选项文本快照。</summary>
        public LocalizedTextSnapshot Text { get; }
        /// <summary>玩家点击的选项稳定标识；对白记录为 null。</summary>
        public ChoiceId SelectedChoiceId { get; }
        /// <summary>是否为玩家点击选项产生的记录。</summary>
        public bool IsChoice { get; }

        /// <summary>创建一条剧情历史记录。</summary>
        /// <param name="sequence">所属会话内的追加序号。</param>
        /// <param name="storyId">所属剧情稳定标识。</param>
        /// <param name="nodeId">实际经过的节点标识。</param>
        /// <param name="speakerId">说话角色；可为 null。</param>
        /// <param name="speaker">说话人文本快照；为 null 时使用空快照。</param>
        /// <param name="text">正文文本快照；为 null 时使用空快照。</param>
        /// <param name="selectedChoiceId">已选选项标识；非选项记录为 null。</param>
        /// <param name="isChoice">是否为选项记录。</param>
        /// <exception cref="ArgumentNullException">剧情标识或节点标识为 null 时抛出。</exception>
        public StoryHistoryEntry(long sequence, StoryId storyId, StoryNodeId nodeId,
            CharacterId speakerId, LocalizedTextSnapshot speaker, LocalizedTextSnapshot text,
            ChoiceId selectedChoiceId, bool isChoice)
        {
            Sequence = sequence;
            StoryId = storyId ?? throw new ArgumentNullException(nameof(storyId));
            NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            SpeakerId = speakerId;
            Speaker = speaker ?? LocalizedTextSnapshot.Empty;
            Text = text ?? LocalizedTextSnapshot.Empty;
            SelectedChoiceId = selectedChoiceId;
            IsChoice = isChoice;
        }

        /// <summary>创建一条对白或叙述记录。</summary>
        /// <param name="sequence">所属会话内的追加序号。</param>
        /// <param name="storyId">所属剧情稳定标识。</param>
        /// <param name="nodeId">实际经过的节点标识。</param>
        /// <param name="speakerId">说话角色；可为 null。</param>
        /// <param name="speaker">说话人文本快照；可为 null。</param>
        /// <param name="text">正文文本快照；可为 null。</param>
        /// <returns>非选项类型的记录。</returns>
        /// <exception cref="ArgumentNullException">剧情标识或节点标识为 null 时抛出。</exception>
        public static StoryHistoryEntry ForDialogue(long sequence, StoryId storyId,
            StoryNodeId nodeId, CharacterId speakerId, LocalizedTextSnapshot speaker,
            LocalizedTextSnapshot text)
        {
            return new StoryHistoryEntry(
                sequence,
                storyId,
                nodeId,
                speakerId,
                speaker,
                text,
                null,
                false
            );
        }

        /// <summary>创建一条玩家选项记录。</summary>
        /// <param name="sequence">所属会话内的追加序号。</param>
        /// <param name="storyId">所属剧情稳定标识。</param>
        /// <param name="nodeId">选项所在节点标识。</param>
        /// <param name="selectedChoiceId">玩家点击的选项标识。</param>
        /// <param name="choiceText">选项文本快照；可为 null。</param>
        /// <returns>选项类型的记录。</returns>
        /// <exception cref="ArgumentNullException">剧情标识、节点标识或选项标识为 null 时抛出。</exception>
        public static StoryHistoryEntry ForChoice(long sequence, StoryId storyId,
            StoryNodeId nodeId, ChoiceId selectedChoiceId, LocalizedTextSnapshot choiceText)
        {
            return new StoryHistoryEntry(
                sequence,
                storyId,
                nodeId,
                null,
                LocalizedTextSnapshot.Empty,
                choiceText,
                selectedChoiceId ?? throw new ArgumentNullException(nameof(selectedChoiceId)),
                true
            );
        }
    }

    /// <summary>基础对白显示数据。</summary>
    public sealed class StoryDialogueView
    {
        /// <summary>说话人实际文本，可为空。</summary>
        public string SpeakerText { get; }
        /// <summary>正文实际文本。</summary>
        public string Text { get; }

        /// <summary>创建对白显示数据。</summary>
        /// <param name="speakerText">说话人实际文本。</param>
        /// <param name="text">正文实际文本。</param>
        public StoryDialogueView(string speakerText, string text)
        {
            SpeakerText = speakerText ?? string.Empty;
            Text = text ?? string.Empty;
        }
    }

    /// <summary>基础选项显示数据。</summary>
    public sealed class StoryChoiceView
    {
        /// <summary>选项稳定标识。</summary>
        public ChoiceId ChoiceId { get; }
        /// <summary>选项实际文本。</summary>
        public string Text { get; }

        /// <summary>创建选项显示数据。</summary>
        /// <param name="choiceId">选项标识。</param>
        /// <param name="text">选项实际文本。</param>
        public StoryChoiceView(ChoiceId choiceId, string text)
        {
            ChoiceId = choiceId ?? throw new ArgumentNullException(nameof(choiceId));
            Text = text ?? string.Empty;
        }
    }

    /// <summary>剧情基础 uGUI 表现端口，不包含打字机和历史记录。</summary>
    public interface IStoryPresentationPort
    {
        /// <summary>显示对白并等待继续操作。</summary>
        /// <param name="dialogue">对白数据。</param>
        /// <param name="onContinue">继续回调。</param>
        void ShowDialogue(StoryDialogueView dialogue, Action onContinue);
        /// <summary>显示选项并等待点击。</summary>
        /// <param name="choices">选项数据。</param>
        /// <param name="onChoice">选中回调。</param>
        void ShowChoices(IReadOnlyList<StoryChoiceView> choices, Action<ChoiceId> onChoice);
        /// <summary>清空剧情表现。</summary>
        void Clear();
    }

    /// <summary>Mutable runtime state for one active story session.</summary>
    public sealed class StorySession
    {
        /// <summary>Story being played.</summary>
        public StoryId StoryId { get; }
        /// <summary>Node awaiting execution.</summary>
        public StoryNodeId CurrentNodeId { get; private set; }
        /// <summary>Whether the session reached an End node.</summary>
        public bool IsCompleted { get; private set; }

        /// <summary>Creates a session at its start node.</summary>
        /// <param name="storyId">Story stable identifier.</param>
        /// <param name="startNodeId">Initial node identifier.</param>
        public StorySession(StoryId storyId, StoryNodeId startNodeId)
        {
            StoryId = storyId ?? throw new ArgumentNullException(nameof(storyId));
            CurrentNodeId = startNodeId ?? throw new ArgumentNullException(nameof(startNodeId));
        }

        /// <summary>Moves the session to a validated next node.</summary>
        /// <param name="nodeId">Next node identifier.</param>
        public void MoveTo(StoryNodeId nodeId)
        {
            CurrentNodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        }

        /// <summary>Marks the session complete after an End node executes.</summary>
        public void Complete() => IsCompleted = true;
    }

    /// <summary>Read-only snapshot of story execution state.</summary>
    public sealed class StorySnapshot
    {
        /// <summary>Story stable identifier.</summary>
        public StoryId StoryId { get; }
        /// <summary>Current node definition.</summary>
        public StoryNodeDefinition CurrentNode { get; }
        /// <summary>Whether the story has completed.</summary>
        public bool IsCompleted { get; }
        /// <summary>Nodes visited in execution order.</summary>
        public IReadOnlyList<StoryNodeId> VisitedNodes { get; }

        /// <summary>Creates an immutable story snapshot.</summary>
        /// <param name="storyId">Story identifier.</param>
        /// <param name="currentNode">Current node definition.</param>
        /// <param name="isCompleted">Completion state.</param>
        /// <param name="visitedNodes">Visited node IDs.</param>
        public StorySnapshot(StoryId storyId, StoryNodeDefinition currentNode,
            bool isCompleted, IReadOnlyList<StoryNodeId> visitedNodes)
        {
            StoryId = storyId;
            CurrentNode = currentNode;
            IsCompleted = isCompleted;
            VisitedNodes = visitedNodes ?? new List<StoryNodeId>().AsReadOnly();
        }
    }

    /// <summary>Story runtime operations required by C07.</summary>
    public interface IStoryService
    {
        /// <summary>跳过当前剧情并执行必要节点，直到剧情结束。</summary>
        /// <returns>跳过结果。</returns>
        Result Skip();
        /// <summary>Starts a story at its declared start node.</summary>
        /// <param name="storyId">Story stable identifier.</param>
        /// <returns>The new active session.</returns>
        StorySession Start(StoryId storyId);

        /// <summary>Executes the current Dialogue, Choice, Goto or End node.</summary>
        /// <returns>Success or a content/state error.</returns>
        Result Advance();

        /// <summary>Executes a choice node using the requested choice.</summary>
        /// <param name="choiceId">Choice stable identifier.</param>
        /// <returns>Success or a validation/content error.</returns>
        Result Choose(ChoiceId choiceId);

        /// <summary>Gets a read-only snapshot of the active session.</summary>
        /// <returns>Snapshot, or null when no story is active.</returns>
        StorySnapshot GetSnapshot();
    }
}
