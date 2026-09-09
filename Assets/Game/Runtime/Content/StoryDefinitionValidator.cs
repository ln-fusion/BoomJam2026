using System;
using System.Collections.Generic;
using Game.Contracts.Content;

namespace Game.Content
{
    /// <summary>校验剧情节点 ID、引用、本地化键与分支选择。</summary>
    public static class StoryDefinitionValidator
    {
        /// <summary>校验剧情定义，失败时输出诊断消息。</summary>
        /// <param name="definition">待校验的剧情定义。</param>
        /// <param name="error">失败诊断；校验通过时为 null。</param>
        /// <returns>定义有效时返回 true。</returns>
        public static bool TryValidate(StoryDefinition definition, out string error)
        {
            return TryValidate(definition, null, null, out error);
        }

        /// <summary>校验剧情定义，可带角色引用。</summary>
        /// <param name="definition">待校验的剧情定义。</param>
        /// <param name="characters">已知角色；可选，提供时启用说话者/形象校验。</param>
        /// <param name="error">失败诊断；校验通过时为 null。</param>
        /// <returns>定义有效时返回 true。</returns>
        public static bool TryValidate(
            StoryDefinition definition,
            IReadOnlyCollection<CharacterDefinition> characters,
            out string error
        )
        {
            return TryValidate(definition, characters, null, out error);
        }

        /// <summary>校验剧情定义，可带角色引用与资源存在性谓词。</summary>
        /// <param name="definition">待校验的剧情定义。</param>
        /// <param name="characters">已知角色；可选，提供时启用说话者/形象校验。</param>
        /// <param name="assetExists">资源存在性谓词；为 null 时跳过资源存在性校验。</param>
        /// <param name="error">失败诊断；校验通过时为 null。</param>
        /// <returns>定义有效时返回 true。</returns>
        public static bool TryValidate(
            StoryDefinition definition,
            IReadOnlyCollection<CharacterDefinition> characters,
            Func<string, bool> assetExists,
            out string error
        )
        {
            return TryValidate(definition, characters, assetExists, null, out error);
        }

        /// <summary>校验剧情定义，可带角色引用、资源存在性与本地化键存在性谓词。</summary>
        /// <param name="definition">待校验的剧情定义。</param>
        /// <param name="characters">已知角色；可选，提供时启用说话者/形象校验。</param>
        /// <param name="assetExists">资源存在性谓词；为 null 时跳过资源存在性校验。</param>
        /// <param name="localizationKeyExists">本地化键存在性谓词；为 null 时跳过键校验。</param>
        /// <param name="error">失败诊断；校验通过时为 null。</param>
        /// <returns>定义有效时返回 true。</returns>
        public static bool TryValidate(
            StoryDefinition definition,
            IReadOnlyCollection<CharacterDefinition> characters,
            Func<string, bool> assetExists,
            Func<string, bool> localizationKeyExists,
            out string error
        )
        {
            error = null;
            if (
                definition == null
                || string.IsNullOrWhiteSpace(definition.StoryId)
                || string.IsNullOrWhiteSpace(definition.StartNodeId)
                || definition.Nodes == null
            )
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
                        if (
                            choice == null
                            || string.IsNullOrWhiteSpace(choice.ChoiceId)
                            || string.IsNullOrWhiteSpace(choice.NextNodeId)
                            || !choices.Add(choice.ChoiceId)
                        )
                            return Fail("Choice IDs and targets must be non-empty and unique.", out error);
                }
                if (characters != null && !string.IsNullOrWhiteSpace(node.SpeakerCharacterId))
                {
                    CharacterDefinition speaker = FindCharacter(characters, node.SpeakerCharacterId);
                    if (speaker == null)
                        return Fail("Speaker character does not exist: " + node.SpeakerCharacterId, out error);
                    if (
                        !string.IsNullOrWhiteSpace(node.AppearanceOverride)
                        && !HasAppearance(speaker, node.AppearanceOverride)
                    )
                        return Fail(
                            "Appearance override is not available for character: " + node.AppearanceOverride,
                            out error
                        );
                }
            }
            if (!nodes.Contains(definition.StartNodeId))
                return Fail("Story start node does not exist.", out error);
            foreach (StoryNodeDefinition node in definition.Nodes)
            {
                if (
                    (
                        node.Type == StoryNodeType.Dialogue
                        || node.Type == StoryNodeType.Goto
                        || node.Type == StoryNodeType.ShowCharacter
                        || node.Type == StoryNodeType.ShowCg
                        || node.Type == StoryNodeType.SetBackground
                        || node.Type == StoryNodeType.HideCharacter
                        || node.Type == StoryNodeType.MoveCharacter
                        || node.Type == StoryNodeType.PlayAudio
                        || node.Type == StoryNodeType.ScreenEffect
                        || node.Type == StoryNodeType.Wait
                    )
                    && !string.IsNullOrWhiteSpace(node.NextNodeId)
                    && !nodes.Contains(node.NextNodeId)
                )
                    return Fail("Story node references an unknown target.", out error);
                if (node.Type == StoryNodeType.Choice)
                    foreach (StoryChoiceDefinition choice in node.Choices)
                        if (!nodes.Contains(choice.NextNodeId))
                            return Fail("Choice references an unknown target.", out error);
                if (string.IsNullOrWhiteSpace(node.SpeakerCharacterId) && node.Type == StoryNodeType.ShowCharacter)
                    return Fail("ShowCharacter nodes require a speaker character.", out error);
                if (node.Type == StoryNodeType.ShowCg && string.IsNullOrWhiteSpace(node.AssetId))
                    return Fail("ShowCg nodes require an asset ID.", out error);
                if (node.Type == StoryNodeType.SetBackground && string.IsNullOrWhiteSpace(node.BackgroundId))
                    return Fail("SetBackground nodes require a background ID.", out error);
                if (node.Type == StoryNodeType.HideCharacter || node.Type == StoryNodeType.MoveCharacter)
                {
                    if (string.IsNullOrWhiteSpace(node.SpeakerCharacterId))
                        return Fail(
                            node.Type == StoryNodeType.HideCharacter
                                ? "HideCharacter nodes require a speaker character."
                                : "MoveCharacter nodes require a speaker character.",
                            out error
                        );
                }
                if (node.Type == StoryNodeType.PlayAudio && string.IsNullOrWhiteSpace(node.AudioId))
                    return Fail("PlayAudio nodes require an audio ID.", out error);
                if (node.Type == StoryNodeType.ScreenEffect && node.EffectType == StoryScreenEffectType.None)
                    return Fail("ScreenEffect nodes require a non-None effect type.", out error);
                if (node.Type == StoryNodeType.Wait && node.WaitSeconds <= 0f)
                    return Fail("Wait nodes require a positive wait duration.", out error);
                if (assetExists != null)
                {
                    if (node.Type == StoryNodeType.ShowCg && !assetExists(node.AssetId))
                        return Fail("ShowCg asset does not exist: " + node.AssetId, out error);
                    if (node.Type == StoryNodeType.SetBackground && !assetExists(node.BackgroundId))
                        return Fail("Background asset does not exist: " + node.BackgroundId, out error);
                    if (node.Type == StoryNodeType.PlayAudio && !assetExists(node.AudioId))
                        return Fail("Audio asset does not exist: " + node.AudioId, out error);
                }
                if (localizationKeyExists != null)
                {
                    if (node.Type == StoryNodeType.Dialogue && !localizationKeyExists(node.TextKey))
                        return Fail("Dialogue localization key does not exist: " + node.TextKey, out error);
                    if (
                        node.Type == StoryNodeType.Choice
                        && node.Choices != null
                        && !node.Choices.TrueForAll(c => localizationKeyExists(c.TextKey))
                    )
                        return Fail("Choice localization key does not exist: " + node.Choices[0].TextKey, out error);
                    if (!string.IsNullOrWhiteSpace(node.SpeakerKey) && !localizationKeyExists(node.SpeakerKey))
                        return Fail("Speaker localization key does not exist: " + node.SpeakerKey, out error);
                }
            }
            return true;
        }

        /// <summary>设置校验失败信息并返回失败结果。</summary>
        /// <param name="message">校验失败信息。</param>
        /// <param name="error">接收校验失败信息。</param>
        /// <returns>始终返回 false。</returns>
        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }

        /// <summary>按稳定标识查找角色定义。</summary>
        /// <param name="characters">待查询的角色集合。</param>
        /// <param name="characterId">角色稳定标识。</param>
        /// <returns>匹配定义；不存在时返回 null。</returns>
        private static CharacterDefinition FindCharacter(
            IReadOnlyCollection<CharacterDefinition> characters,
            string characterId
        )
        {
            foreach (CharacterDefinition character in characters)
                if (character != null && string.Equals(character.CharacterId, characterId, StringComparison.Ordinal))
                    return character;
            return null;
        }

        /// <summary>判断角色是否声明指定形象。</summary>
        /// <param name="character">角色定义。</param>
        /// <param name="appearanceId">待检查的形象标识。</param>
        /// <returns>形象属于该角色时返回 true。</returns>
        private static bool HasAppearance(CharacterDefinition character, string appearanceId)
        {
            if (character.AppearanceIds == null)
                return false;
            foreach (string candidate in character.AppearanceIds)
                if (string.Equals(candidate, appearanceId, StringComparison.Ordinal))
                    return true;
            return false;
        }
    }
}
