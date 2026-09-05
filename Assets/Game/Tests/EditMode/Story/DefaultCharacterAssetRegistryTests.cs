using System.Collections.Generic;
using Game.Contracts.Content;
using Game.Foundation;
using Game.Story;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Story
{
    /// <summary>
    /// 角色形象查询与立绘注册表测试：默认形象、显式覆盖与资源回退。
    /// </summary>
    public sealed class DefaultCharacterAssetRegistryTests
    {
        private static readonly CharacterDefinition Hani = new CharacterDefinition
        {
            CharacterId = "official.character.hani",
            AppearanceIds = new List<string> { "official.appearance.hani.casual", "official.appearance.hani.uniform" },
            DefaultAppearanceId = "official.appearance.hani.casual",
        };

        /// <summary>验证默认形象取自角色的 DefaultAppearanceId。</summary>
        [Test]
        public void GetDefaultAppearance_ReturnsDeclaredDefault()
        {
            var registry = new DefaultCharacterAssetRegistry(new[] { Hani });

            AppearanceId result = registry.GetDefaultAppearance(new CharacterId("official.character.hani"));

            Assert.That(result.Value, Is.EqualTo("official.appearance.hani.casual"));
        }

        /// <summary>验证未知角色返回 null。</summary>
        [Test]
        public void GetDefaultAppearance_UnknownCharacter_ReturnsNull()
        {
            var registry = new DefaultCharacterAssetRegistry(new[] { Hani });

            Assert.That(registry.GetDefaultAppearance(new CharacterId("unknown")), Is.Null);
        }

        /// <summary>验证立面列表按声明顺序返回。</summary>
        [Test]
        public void GetAppearances_ReturnsDeclaredOrder()
        {
            var registry = new DefaultCharacterAssetRegistry(new[] { Hani });

            IReadOnlyList<AppearanceId> appearances = registry.GetAppearances(
                new CharacterId("official.character.hani")
            );

            Assert.That(appearances, Has.Count.EqualTo(2));
            Assert.That(appearances[0].Value, Is.EqualTo("official.appearance.hani.casual"));
        }

        /// <summary>验证立绘查询使用显式覆盖时按传入形象查询。</summary>
        [Test]
        public void GetPortrait_UsesExplicitAppearance_WhenProvided()
        {
            var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            var resolver = new StubAssetResolver(sprite);
            var registry = new DefaultCharacterAssetRegistry(new[] { Hani }, resolver);

            Sprite result = registry.GetPortrait(
                new CharacterId("official.character.hani"),
                new AppearanceId("official.appearance.hani.uniform"),
                null
            );

            Assert.That(result, Is.SameAs(sprite));
        }

        /// <summary>验证立绘查询未命中时返回 null。</summary>
        [Test]
        public void GetPortrait_UnknownAppearance_ReturnsNull()
        {
            var registry = new DefaultCharacterAssetRegistry(new[] { Hani }, new StubAssetResolver(null));

            Assert.That(
                registry.GetPortrait(
                    new CharacterId("official.character.hani"),
                    new AppearanceId("official.appearance.missing"),
                    null
                ),
                Is.Null
            );
        }

        /// <summary>测试用资源解析器：始终返回预设精灵。</summary>
        private sealed class StubAssetResolver : IAssetResolver
        {
            private readonly Sprite _sprite;

            public StubAssetResolver(Sprite sprite) => _sprite = sprite;

            /// <inheritdoc/>
            public Sprite GetSprite(SpriteId id) => _sprite;

            /// <inheritdoc/>
            public GameObject GetPrefab(PrefabId id) => null;

            /// <inheritdoc/>
            public GameObject GetUiPrefab(UiPrefabId id) => null;

            /// <inheritdoc/>
            public AudioClip GetAudio(AudioId id) => null;
        }
    }
}
