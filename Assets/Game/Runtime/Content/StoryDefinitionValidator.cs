using System;
using System.Collections.Generic;
using Game.Contracts.Content;

namespace Game.Content
{
    /// <summary>Validates story node IDs, references, localized keys and choice branches.</summary>
    public static class StoryDefinitionValidator
    {
        /// <summary>Validates a story definition and returns a diagnostic message on failure.</summary>
        /// <param name="definition">Story definition to validate.</param>
        /// <param name="error">Failure diagnostic, or null when valid.</param>
        /// <returns>True when the definition is valid.</returns>
        public static bool TryValidate(StoryDefinition definition, out string error)
        {
            error = null;
            if (definition == null || string.IsNullOrWhiteSpace(definition.StoryId) ||
                string.IsNullOrWhiteSpace(definition.StartNodeId) || definition.Nodes == null)
                return Fail("Story ID, start node and nodes are required.", out error);

            var nodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (StoryNodeDefinition node in definition.Nodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.NodeId) || !nodes.Add(node.NodeId))
                    return Fail("Story node IDs must be non-empty and unique.", out error);
                if (node.Type == StoryNodeType.Dialogue && string.IsNullOrWhiteSpace(node.TextKey))
                    return Fail("Dialogue nodes require a localization key.", out error);
                if (node.Type == StoryNodeType.Choice)
                {
                    if (node.Choices == null || node.Choices.Count == 0)
                        return Fail("Choice nodes require at least one choice.", out error);
                    var choices = new HashSet<string>(StringComparer.Ordinal);
                    foreach (StoryChoiceDefinition choice in node.Choices)
                        if (choice == null || string.IsNullOrWhiteSpace(choice.ChoiceId) ||
                            string.IsNullOrWhiteSpace(choice.NextNodeId) || !choices.Add(choice.ChoiceId))
                            return Fail("Choice IDs and targets must be non-empty and unique.", out error);
                }
            }
            if (!nodes.Contains(definition.StartNodeId))
                return Fail("Story start node does not exist.", out error);
            foreach (StoryNodeDefinition node in definition.Nodes)
            {
                if ((node.Type == StoryNodeType.Dialogue || node.Type == StoryNodeType.Goto) &&
                    !string.IsNullOrWhiteSpace(node.NextNodeId) && !nodes.Contains(node.NextNodeId))
                    return Fail("Story node references an unknown target.", out error);
                if (node.Type == StoryNodeType.Choice)
                    foreach (StoryChoiceDefinition choice in node.Choices)
                        if (!nodes.Contains(choice.NextNodeId))
                            return Fail("Choice references an unknown target.", out error);
            }
            return true;
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
