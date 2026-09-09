using Game.Content;
using Game.Contracts.Content;
using Game.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>验证剧情演出资源源解析与面板背景/CG 回退行为。</summary>
    public sealed class StoryStageAssetTests
    {
        /// <summary>背景与 CG 通过同一稳定 ID 索引解析。</summary>
        [Test]
        public void StoryAssetSource_ResolvesByIds()
        {
            var registry = ScriptableObject.CreateInstance<ContentAssetRegistry>();
            var sprite = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.zero);
            try
            {
                AddSprite(registry, "official.background.test_01", sprite);
                var resolver = new OfficialAssetResolver(registry);
                var source = new StoryAssetSource(resolver);

                Assert.That(source.GetBackground("official.background.test_01"), Is.SameAs(sprite));
                Assert.That(source.GetBackground("missing"), Is.Null);
                Assert.That(source.GetCg("missing"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(registry);
            }
        }

        /// <summary>通过反射写入私有精灵映射列表（Registry 无公开编辑入口）。</summary>
        /// <param name="registry">目标 Registry。</param>
        /// <param name="id">精灵稳定标识。</param>
        /// <param name="sprite">目标精灵。</param>
        private static void AddSprite(ContentAssetRegistry registry, string id, Sprite sprite)
        {
            var field = typeof(ContentAssetRegistry).GetField(
                "sprites",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            );
            var list = field.GetValue(registry) as System.Collections.Generic.List<SpriteAssetEntry>;
            list.Add(new SpriteAssetEntry { Id = id, Asset = sprite });
        }

        /// <summary>缺失资源时面板回退纯色背景且不抛异常。</summary>
        [Test]
        public void Panel_SetBackgroundFallsBackOnMissingAsset()
        {
            var root = new GameObject("StoryPanelTest", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<StoryDialoguePanel>();
                panel.SetBackground("missing.background");
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.GetComponentInChildren<Image>(true), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>CG 层在资源存在时激活, 不存在时保持隐藏。</summary>
        [Test]
        public void Panel_ShowCgActivatesLayer()
        {
            var root = new GameObject("StoryPanelTest", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<StoryDialoguePanel>();
                panel.SetStageAssetSource(null);
                panel.ShowCg("missing.cg");
                Assert.That(panel, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
