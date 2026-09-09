using Game.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>验证剧情多角色立绘槽位的独立显示与隐藏。</summary>
    public sealed class StoryDialogueSlotsTests
    {
        /// <summary>双角色各占一个槽位, 隐藏其一不影响另一。</summary>
        [Test]
        public void Slots_HideOneKeepsOther()
        {
            var root = new GameObject("StorySlotsTest", typeof(RectTransform));
            try
            {
                var panel = root.AddComponent<StoryDialoguePanel>();
                panel.ShowCharacter("char.a", "appearance.a", CreateSprite(Color.red));
                panel.ShowCharacter("char.b", "appearance.b", CreateSprite(Color.blue));

                panel.HideCharacter("char.a");
                panel.ShowDialogue(
                    new Game.Contracts.Story.StoryDialogueView("speaker.b", "text.b"),
                    CreateSprite(Color.blue),
                    () => { }
                );

                Assert.That(FindPortrait(root, "char.b"), Is.Not.Null, "B 的立绘应仍在");
                Assert.That(FindPortrait(root, "char.a"), Is.Null, "A 的立绘应已隐藏");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>创建纯色测试精灵。</summary>
        /// <param name="color">精灵颜色。</param>
        /// <returns>新建的 2x2 精灵。</returns>
        private static Sprite CreateSprite(Color color)
        {
            var texture = new Texture2D(2, 2);
            for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                texture.SetPixel(x, y, color);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
        }

        /// <summary>在面板下查找指定角色的槽位图像。</summary>
        /// <param name="root">面板根。</param>
        /// <param name="spriteNamePrefix">槽位名称前缀（角色 ID）。</param>
        /// <returns>找到的立绘图像; 不存在时为 null。</returns>
        private static Image FindPortrait(GameObject root, string spriteNamePrefix)
        {
            foreach (Image image in root.GetComponentsInChildren<Image>(true))
                if (
                    image.gameObject.activeSelf
                    && image.sprite != null
                    && image.name.StartsWith(spriteNamePrefix, System.StringComparison.Ordinal)
                )
                    return image;
            return null;
        }
    }
}
