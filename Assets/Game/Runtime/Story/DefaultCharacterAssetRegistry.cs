using System;
using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Foundation;
using UnityEngine;

namespace Game.Story
{
    /// <summary>
    /// 基于角色定义的内存形象查询：默认形象取角色声明的 DefaultAppearanceId。
    /// </summary>
    /// <remarks>
    /// C16 占位实现：不依赖人员页面当前形象；人员页面接入后由
    /// <c>ICharacterAppearanceQuery</c> 的组合实现替换（技术设计文档 §8.6）。
    /// </remarks>
    public sealed class DefaultCharacterAssetRegistry : ICharacterAppearanceQuery, ICharacterAssetRegistry
    {
        private readonly Dictionary<string, CharacterDefinition> _characters;
        private readonly IAssetResolver _assetResolver;
        private readonly IGameLogger _logger;

        /// <summary>创建角色形象查询与立绘注册表。</summary>
        /// <param name="characters">角色定义集合；空集合时不返回任何形象。</param>
        /// <param name="assetResolver">资源解析器；为 null 时立绘查询全部返回 null。</param>
        /// <param name="logger">日志；为 null 时静默。</param>
        public DefaultCharacterAssetRegistry(
            IEnumerable<CharacterDefinition> characters,
            IAssetResolver assetResolver = null,
            IGameLogger logger = null
        )
        {
            var indexed = new Dictionary<string, CharacterDefinition>(StringComparer.Ordinal);
            if (characters != null)
                foreach (CharacterDefinition character in characters)
                    if (character != null && !string.IsNullOrWhiteSpace(character.CharacterId))
                        indexed[character.CharacterId] = character;
            _characters = indexed;
            _assetResolver = assetResolver;
            _logger = logger ?? NullLogger.Instance;
        }

        /// <inheritdoc/>
        public AppearanceId GetDefaultAppearance(CharacterId characterId)
        {
            if (characterId == null || !_characters.TryGetValue(characterId.Value, out CharacterDefinition character))
                return null;
            return string.IsNullOrWhiteSpace(character.DefaultAppearanceId)
                ? null
                : new AppearanceId(character.DefaultAppearanceId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<AppearanceId> GetAppearances(CharacterId characterId)
        {
            if (
                characterId == null
                || !_characters.TryGetValue(characterId.Value, out CharacterDefinition character)
                || character.AppearanceIds == null
            )
                return new List<AppearanceId>().AsReadOnly();

            var result = new List<AppearanceId>();
            foreach (string id in character.AppearanceIds)
                if (!string.IsNullOrWhiteSpace(id))
                    result.Add(new AppearanceId(id));
            return result.AsReadOnly();
        }

        /// <inheritdoc/>
        public Sprite GetPortrait(CharacterId characterId, AppearanceId appearanceId, ExpressionId expressionId)
        {
            if (characterId == null)
                return null;
            if (appearanceId == null)
                appearanceId = GetDefaultAppearance(characterId);
            if (appearanceId == null)
            {
                _logger.LogWarning(LogContext.Empty, "[CharacterRegistry] 角色无可用默认形象: " + characterId);
                return null;
            }
            if (_assetResolver == null)
                return null;

            // 表情差分与形象共用同一条 Sprite 查询键：表情为空时只按形象查。
            string key = expressionId == null ? appearanceId.Value : appearanceId.Value + "." + expressionId.Value;
            return _assetResolver.GetSprite(new SpriteId(key));
        }
    }
}
