using System.Collections;
using System.Collections.Generic;
using Game.Bootstrap;
using Game.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Game.Tests.PlayMode
{
    /// <summary>验证 Bootstrap 启动后 C03/C05 全局和功能 UI 会被安装。</summary>
    public sealed class BootstrapUiSmokeTests
    {
        /// <summary>每个测试后释放 Bootstrap 及其跨场景 UI，保持用例独立。</summary>
        [UnityTearDown]
        public IEnumerator TearDownBootstrap()
        {
            foreach (GameRoot root in Object.FindObjectsOfType<GameRoot>())
                Object.Destroy(root.gameObject);
            yield return null;

            foreach (GlobalCanvasLayer layer in Object.FindObjectsOfType<GlobalCanvasLayer>())
                Object.Destroy(layer.gameObject);
            yield return null;
        }

        /// <summary>加载 Bootstrap 并检查全局 Canvas、开始菜单 View 和 Presenter。</summary>
        [UnityTest]
        public IEnumerator Bootstrap_Installs_GlobalAndStartMenuUi()
        {
            yield return SceneManager.LoadSceneAsync("00_Bootstrap", LoadSceneMode.Single);
            yield return new UnityEngine.WaitForSeconds(1.5f);

            Assert.That(GameObject.FindObjectOfType<GlobalCanvasLayer>(), Is.Not.Null);
            Assert.That(GameObject.Find("GlobalOverlayCanvas"), Is.Not.Null);
            Assert.That(GameObject.Find("ModalCanvas"), Is.Not.Null);
            Assert.That(GameObject.FindObjectOfType<StartMenuView>(), Is.Not.Null);
            Assert.That(GameObject.FindObjectOfType<StartMenuPresenter>(), Is.Not.Null);
        }

        /// <summary>全局反馈必须忽略鼠标射线，并在停留时间结束后自行隐藏。</summary>
        [UnityTest]
        public IEnumerator GlobalFeedback_IgnoresRaycastsAndExpires()
        {
            yield return SceneManager.LoadSceneAsync("00_Bootstrap", LoadSceneMode.Single);
            yield return new WaitForSeconds(1.5f);

            GlobalCanvasLayer globalLayer = Object.FindObjectOfType<GlobalCanvasLayer>();
            Assert.That(globalLayer, Is.Not.Null);

            globalLayer.ShowFeedback("Saved");
            Assert.That(globalLayer.FeedbackText.raycastTarget, Is.False);
            Assert.That(globalLayer.FeedbackText.gameObject.activeSelf, Is.True);

            yield return new WaitForSecondsRealtime(3.1f);

            Assert.That(globalLayer.FeedbackText.gameObject.activeSelf, Is.False);
            Assert.That(globalLayer.FeedbackText.text, Is.Empty);
        }

        /// <summary>通过真实 UI 射线链路打开语言下拉框并选择第二个选项。</summary>
        [UnityTest]
        public IEnumerator SettingsPrefab_LanguageDropdown_CanSelectSecondOptionThroughRaycaster()
        {
            yield return SceneManager.LoadSceneAsync("00_Bootstrap", LoadSceneMode.Single);
            yield return new WaitForSeconds(1.5f);

            GlobalCanvasLayer globalLayer = Object.FindObjectOfType<GlobalCanvasLayer>();
            Assert.That(globalLayer, Is.Not.Null);
            globalLayer.OpenSettings();
            yield return null;

            SettingsUiBindings bindings = Object.FindObjectOfType<SettingsUiBindings>();
            Assert.That(bindings, Is.Not.Null, "设置界面必须来自 Registry 中登记的预制体。");
            Assert.That(bindings.IsComplete, Is.True);

            ClickThroughRaycaster(bindings.LanguageDropdown.GetComponent<RectTransform>());
            yield return new WaitForSeconds(0.25f);
            Canvas.ForceUpdateCanvases();

            GameObject list = GameObject.Find("Dropdown List");
            Assert.That(list, Is.Not.Null, "点击语言控件后必须创建可见选项列表。");
            Toggle[] items = list.GetComponentsInChildren<Toggle>(false);
            Assert.That(items, Has.Length.EqualTo(2));
            Assert.That(items[1].GetComponent<RectTransform>().rect.height, Is.GreaterThan(0f));
            Text firstItemText = items[0].GetComponentInChildren<Text>();
            Text secondItemText = items[1].GetComponentInChildren<Text>();
            Assert.That(firstItemText, Is.Not.Null);
            Assert.That(secondItemText, Is.Not.Null);
            Assert.That(firstItemText.text, Is.EqualTo("zh-CN"));
            Assert.That(secondItemText.text, Is.EqualTo("en-US"));
            Assert.That(firstItemText.canvasRenderer.GetAlpha(), Is.GreaterThan(0f));
            Assert.That(secondItemText.canvasRenderer.GetAlpha(), Is.GreaterThan(0f));

            ClickThroughRaycaster(items[1].GetComponent<RectTransform>());
            yield return null;

            Assert.That(bindings.LanguageDropdown.value, Is.EqualTo(1));
            Assert.That(bindings.LanguageDropdown.captionText.text, Is.EqualTo("en-US"));
            Assert.That(bindings.LanguageDropdown.captionText.canvasRenderer.GetAlpha(),
                Is.GreaterThan(0f));
        }

        /// <summary>从矩形中心执行与鼠标点击一致的 EventSystem 射线和事件分发。</summary>
        /// <param name="rectTransform">待点击的 UI 矩形。</param>
        private static void ClickThroughRaycaster(RectTransform rectTransform)
        {
            Canvas.ForceUpdateCanvases();
            EventSystem eventSystem = EventSystem.current;
            Assert.That(eventSystem, Is.Not.Null);
            var pointer = new PointerEventData(eventSystem)
            {
                position = RectTransformUtility.WorldToScreenPoint(null,
                    rectTransform.TransformPoint(rectTransform.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, results);
            Assert.That(results, Is.Not.Empty,
                "目标位置必须至少命中一个可交互的 UI Graphic。");

            GameObject target = results[0].gameObject;
            GameObject handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            Assert.That(handler, Is.Not.Null,
                "射线首个命中对象必须存在点击事件处理器。");
            Assert.That(handler.transform == rectTransform ||
                handler.transform.IsChildOf(rectTransform), Is.True,
                $"目标 {rectTransform.name} 的点击被 {handler.name} 截获。命中顺序：" +
                string.Join(", ", results.ConvertAll(result => result.gameObject.name)) +
                "。目标层级：" + DescribeRectHierarchy(rectTransform));
            ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerClickHandler);
        }

        /// <summary>输出目标及父节点的世界矩形，用于定位遮罩与布局范围。</summary>
        /// <param name="rectTransform">起始矩形。</param>
        /// <returns>从目标到根节点的矩形摘要。</returns>
        private static string DescribeRectHierarchy(RectTransform rectTransform)
        {
            string description = string.Empty;
            Transform current = rectTransform;
            var corners = new Vector3[4];
            while (current != null)
            {
                if (current is RectTransform currentRect)
                {
                    currentRect.GetWorldCorners(corners);
                    description += $"{current.name}[{corners[0].x:F1},{corners[0].y:F1}-" +
                        $"{corners[2].x:F1},{corners[2].y:F1}] ";
                }

                current = current.parent;
            }

            return description;
        }
    }
}
