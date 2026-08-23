using Game.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.EditMode.Presentation
{
    /// <summary>验证 UI 预制体契约对设置滑条范围的约束。</summary>
    public sealed class UiPrefabContractsTests
    {
        /// <summary>三条设置音量滑条必须使用 0～1 范围。</summary>
        [Test]
        public void SettingsBindings_RequireUnitVolumeRanges()
        {
            var root = new GameObject("SettingsContractTest");
            try
            {
                var bindings = root.AddComponent<SettingsUiBindings>();
                bindings.MasterVolumeSlider = AddSlider(root, "Master");
                bindings.MusicVolumeSlider = AddSlider(root, "Music");
                bindings.SfxVolumeSlider = AddSlider(root, "Sfx");

                Assert.That(bindings.VolumeSlidersHaveUnitRange, Is.True);
                bindings.SfxVolumeSlider.maxValue = 2f;
                Assert.That(bindings.VolumeSlidersHaveUnitRange, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>下拉菜单模板必须使用 Dropdown 自带的手动选项排版。</summary>
        [Test]
        public void UiFactory_DropdownTemplateUsesManualItemLayout()
        {
            var root = new GameObject("DropdownLayoutTest");
            try
            {
                Dropdown dropdown = UiFactory.CreateDropdown("Dropdown", root.transform,
                    new System.Collections.Generic.List<string> { "A", "B" }, 0);
                Toggle item = dropdown.template.GetComponentInChildren<Toggle>(true);
                RectTransform itemRect = item == null ? null : item.GetComponent<RectTransform>();
                RectTransform contentRect = itemRect == null ? null : itemRect.parent as RectTransform;
                Mask mask = dropdown.template.GetComponentInChildren<Mask>(true);

                Assert.That(item, Is.Not.Null);
                Assert.That(contentRect, Is.Not.Null);
                Assert.That(contentRect.GetComponent<VerticalLayoutGroup>(), Is.Null);
                Assert.That(contentRect.GetComponent<ContentSizeFitter>(), Is.Null);
                Assert.That(contentRect.rect.height, Is.GreaterThan(itemRect.rect.height));
                Assert.That(itemRect.anchorMin, Is.EqualTo(new Vector2(0f, 0.5f)));
                Assert.That(itemRect.anchorMax, Is.EqualTo(new Vector2(1f, 0.5f)));
                Assert.That(mask, Is.Not.Null);
                Assert.That(mask.showMaskGraphic, Is.False);
                Assert.That(mask.GetComponent<Image>().color.a, Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>设置界面的相邻行必须保留稳定的纵向间距。</summary>
        [Test]
        public void SettingsPreview_AdjacentRowsHaveVerticalSpacing()
        {
            var root = new GameObject("SettingsLayoutTest", typeof(RectTransform));
            try
            {
                root.AddComponent<SettingsModalPresenter>().BuildPreview();

                AssertVerticalGap(root, "Title", UiTextKeys.MasterVolume + "Label");
                AssertVerticalGap(root, UiTextKeys.MasterVolume + "Label",
                    UiTextKeys.MusicVolume + "Label");
                AssertVerticalGap(root, UiTextKeys.MusicVolume + "Label",
                    UiTextKeys.SfxVolume + "Label");
                AssertVerticalGap(root, UiTextKeys.SfxVolume + "Label", "Language");
                AssertVerticalGap(root, "Language", "Resolution");
                AssertVerticalGap(root, "Resolution", "Fullscreen");
                AssertVerticalGap(root, "Fullscreen", "Feedback");
                AssertVerticalGap(root, "Feedback", "Apply");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>断言上方控件与下方控件之间至少保留 1% 面板高度。</summary>
        /// <param name="root">设置界面根节点。</param>
        /// <param name="upperName">上方控件名称。</param>
        /// <param name="lowerName">下方控件名称。</param>
        private static void AssertVerticalGap(GameObject root, string upperName, string lowerName)
        {
            RectTransform upper = FindRect(root, upperName);
            RectTransform lower = FindRect(root, lowerName);
            Assert.That(upper, Is.Not.Null, upperName);
            Assert.That(lower, Is.Not.Null, lowerName);
            Assert.That(upper.anchorMin.y - lower.anchorMax.y, Is.GreaterThanOrEqualTo(0.01f),
                upperName + " 与 " + lowerName + " 的纵向间距不足。");
        }

        /// <summary>按名称查找子节点的矩形组件。</summary>
        /// <param name="root">查找根节点。</param>
        /// <param name="name">目标名称。</param>
        /// <returns>找到的矩形；否则返回 null。</returns>
        private static RectTransform FindRect(GameObject root, string name)
        {
            foreach (RectTransform child in root.GetComponentsInChildren<RectTransform>(true))
                if (child.name == name)
                    return child;
            return null;
        }

        /// <summary>创建用于契约测试的滑条组件。</summary>
        /// <param name="parent">测试根节点。</param>
        /// <param name="name">滑条节点名称。</param>
        /// <returns>创建的滑条。</returns>
        private static Slider AddSlider(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child.AddComponent<Slider>();
        }
    }
}
