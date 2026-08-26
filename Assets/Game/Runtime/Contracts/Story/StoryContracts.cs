using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Foundation;

namespace Game.Contracts.Story
{
    /// <summary>剧情历史页中的一条实际经过记录。</summary>
    public sealed class StoryHistoryEntry
    {
        /// <summary>节点稳定标识。</summary>
        public StoryNodeId NodeId { get; }
        /// <summary>说话人本地化键。</summary>
        public string SpeakerKey { get; }
        /// <summary>正文本地化键。</summary>
        public string TextKey { get; }
        /// <summary>实际选择的选项文本键；非选项节点为空。</summary>
        public string ChoiceTextKey { get; }

        /// <summary>创建历史记录。</summary>
        /// <param name="nodeId">实际显示的节点标识。</param>
        /// <param name="speakerKey">说话人键。</param>
        /// <param name="textKey">正文键。</param>
        /// <param name="choiceTextKey">选项文本键，可为空。</param>
        public StoryHistoryEntry(StoryNodeId nodeId, string speakerKey, string textKey,
            string choiceTextKey = null)
        {
            NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            SpeakerKey = speakerKey ?? string.Empty;
            TextKey = textKey ?? string.Empty;
            ChoiceTextKey = choiceTextKey ?? string.Empty;
        }
    }

    /// <summary>基础对白显示数据。</summary>
    public sealed class StoryDialogueView
    {
        /// <summary>说话人本地化键，可为空。</summary>
        public string SpeakerKey { get; }
        /// <summary>正文本地化键。</summary>
        public string TextKey { get; }

        /// <summary>创建对白显示数据。</summary>
        /// <param name="speakerKey">说话人名称键。</param>
        /// <param name="textKey">正文键。</param>
        public StoryDialogueView(string speakerKey, string textKey)
        {
            SpeakerKey = speakerKey ?? string.Empty;
            TextKey = textKey ?? string.Empty;
        }
    }

    /// <summary>基础选项显示数据。</summary>
    public sealed class StoryChoiceView
    {
        /// <summary>选项稳定标识。</summary>
        public ChoiceId ChoiceId { get; }
        /// <summary>选项本地化键。</summary>
        public string TextKey { get; }

        /// <summary>创建选项显示数据。</summary>
        /// <param name="choiceId">选项标识。</param>
        /// <param name="textKey">选项文本键。</param>
        public StoryChoiceView(ChoiceId choiceId, string textKey)
        {
            ChoiceId = choiceId ?? throw new ArgumentNullException(nameof(choiceId));
            TextKey = textKey ?? string.Empty;
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
