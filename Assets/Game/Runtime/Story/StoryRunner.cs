using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Contracts.Story;
using Game.Foundation;

namespace Game.Story
{
    /// <summary>Executes C07 story nodes without any UI dependency.</summary>
    public sealed class StoryRunner : IStoryService
    {
        private static readonly ErrorCode ContentError =
            new ErrorCode(ErrorCategory.Content, "story.invalid_transition");
        private readonly Func<StoryId, StoryDefinition> _storyLookup;
        private readonly Dictionary<string, StoryNodeDefinition> _nodes =
            new Dictionary<string, StoryNodeDefinition>(StringComparer.Ordinal);
        private readonly List<StoryNodeId> _visited = new List<StoryNodeId>();
        private StoryDefinition _definition;
        private StorySession _session;
        private int _executionSteps;
        private const int MaxExecutionSteps = 256;

        /// <summary>Creates a runner backed by a stable-ID story lookup.</summary>
        /// <param name="storyLookup">Story lookup function.</param>
        public StoryRunner(Func<StoryId, StoryDefinition> storyLookup)
        {
            _storyLookup = storyLookup ?? throw new ArgumentNullException(nameof(storyLookup));
        }

        /// <inheritdoc/>
        public StorySession Start(StoryId storyId)
        {
            _definition = _storyLookup(storyId);
            if (_definition == null)
                throw new ArgumentException("Story was not found.", nameof(storyId));
            if (!TryBuildNodeIndex(_definition, out string error))
                throw new ArgumentException(error, nameof(storyId));

            _visited.Clear();
            _executionSteps = 0;
            _session = new StorySession(storyId, new StoryNodeId(_definition.StartNodeId));
            _visited.Add(_session.CurrentNodeId);
            return _session;
        }

        /// <inheritdoc/>
        public Result Advance()
        {
            if (!CanOperate())
                return Result.Failure(ContentError, "No active story session.");
            if (_session.IsCompleted)
                return Result.Failure(ContentError, "Story session is already complete.");
            if (_executionSteps++ >= MaxExecutionSteps)
                return Result.Failure(ContentError, "Story exceeded the maximum execution step limit.");

            StoryNodeDefinition node = CurrentNode();
            switch (node.Type)
            {
                case StoryNodeType.End:
                    _session.Complete();
                    return Result.Success();
                case StoryNodeType.Choice:
                    return Result.Failure(ContentError, "Choice requires Choose.");
                case StoryNodeType.Dialogue:
                case StoryNodeType.Goto:
                    return MoveTo(node.NextNodeId);
                default:
                    return Result.Failure(ContentError, "Unknown story node type.");
            }
        }

        /// <inheritdoc/>
        public Result Skip()
        {
            if (!CanOperate()) return Result.Failure(ContentError, "No active story session.");
            while (!_session.IsCompleted)
            {
                StoryNodeDefinition node = CurrentNode();
                if (node.Type == StoryNodeType.Choice &&
                    (node.Choices == null || node.Choices.Count == 0 || node.Choices[0] == null))
                    return Result.Failure(ContentError, "Choice has no selectable branch.");
                Result result = node.Type == StoryNodeType.Choice
                    ? Choose(new ChoiceId(node.Choices[0].ChoiceId))
                    : Advance();
                if (!result.IsSuccess) return result;
            }
            return Result.Success();
        }

        /// <inheritdoc/>
        public Result Choose(ChoiceId choiceId)
        {
            if (!CanOperate() || choiceId == null)
                return Result.Failure(ContentError, "No active choice is available.");
            if (_session.IsCompleted)
                return Result.Failure(ContentError, "Story session is already complete.");
            if (_executionSteps++ >= MaxExecutionSteps)
                return Result.Failure(ContentError, "Story exceeded the maximum execution step limit.");
            StoryNodeDefinition node = CurrentNode();
            if (node.Type != StoryNodeType.Choice || node.Choices == null)
                return Result.Failure(ContentError, "Current node is not a choice.");
            foreach (StoryChoiceDefinition choice in node.Choices)
                if (choice != null && string.Equals(choice.ChoiceId, choiceId.Value,
                    StringComparison.Ordinal))
                    return MoveTo(choice.NextNodeId);
            return Result.Failure(ContentError, "Choice does not belong to current node.");
        }

        /// <inheritdoc/>
        public StorySnapshot GetSnapshot()
        {
            return _session == null
                ? null
                : new StorySnapshot(_session.StoryId, CurrentNode(), _session.IsCompleted,
                    new List<StoryNodeId>(_visited).AsReadOnly());
        }

        private bool CanOperate() => _session != null && _definition != null;

        private StoryNodeDefinition CurrentNode() => _nodes[_session.CurrentNodeId.Value];

        private Result MoveTo(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId) || !_nodes.ContainsKey(nodeId))
                return Result.Failure(ContentError, "Next story node was not found.");
            _session.MoveTo(new StoryNodeId(nodeId));
            _visited.Add(_session.CurrentNodeId);
            return Result.Success();
        }

        private bool TryBuildNodeIndex(StoryDefinition definition, out string error)
        {
            error = null;
            _nodes.Clear();
            if (definition.Nodes == null || definition.Nodes.Count == 0 ||
                string.IsNullOrWhiteSpace(definition.StartNodeId))
            {
                error = "Story requires nodes and a start node.";
                return false;
            }

            foreach (StoryNodeDefinition node in definition.Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId) ||
                    !_nodes.TryAdd(node.NodeId, node))
                {
                    error = "Story contains a missing or duplicate node ID.";
                    return false;
                }
            }
            if (!_nodes.ContainsKey(definition.StartNodeId))
            {
                error = "Story start node was not found.";
                return false;
            }
            return true;
        }
    }
}
