#nullable enable
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>
    /// 程序化 uGUI 构建工具：Canvas/Text/Button 工厂（C03 占位期使用，美术介入后迁移 Prefab）.
    /// </summary>
    public static class UIFactory
    {
        /// <summary>创建一个 Screen Space Overlay Canvas 根对象.</summary>
        public static Canvas CreateCanvas(string name, int sortingOrder = 0)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>创建文本组件.</summary>
        public static Text CreateText(
            string name,
            Transform parent,
            string content,
            int fontSize = 24,
            TextAnchor alignment = TextAnchor.MiddleCenter,
            Color? color = null
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color ?? Color.white;
            // 系统字体可能在运行时不可用，先给 Legacy 字体；正式美术介入后替换
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.raycastTarget = false;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200f, 40f);
            return text;
        }

        /// <summary>创建按钮（带背景 Image 与文本子对象）.</summary>
        public static Button CreateButton(
            string name,
            Transform parent,
            string label,
            UnityAction onClick,
            Vector2 size,
            Vector2 anchoredPosition
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(0.25f, 0.25f, 0.35f, 1f);

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;

            var labelText = CreateText("Label", button.transform, label, 22);
            labelText.rectTransform.sizeDelta = new Vector2(size.x - 20f, 30f);
            return button;
        }

        /// <summary>创建滑动条（控制音量等）.</summary>
        public static Slider CreateSlider(
            string name,
            Transform parent,
            float initialValue,
            UnityAction<float> onValueChanged,
            Vector2 size,
            Vector2 anchoredPosition
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var slider = go.AddComponent<Slider>();
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;

            // 背景轨道
            var background = new GameObject("Background");
            background.transform.SetParent(go.transform, false);
            var bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            bgImage.raycastTarget = true;
            ((RectTransform)background.transform).sizeDelta = size;

            // 填充区域（左起，随 value 缩放）
            var fill = new GameObject("Fill");
            fill.transform.SetParent(go.transform, false);
            var fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.4f, 0.7f, 1f, 1f);
            fillImage.raycastTarget = false;
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0.5f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            // 拖动把手
            var handle = new GameObject("Handle");
            handle.transform.SetParent(go.transform, false);
            var handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            handleImage.raycastTarget = true;
            var handleRect = (RectTransform)handle.transform;
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(20f, 32f);

            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = initialValue;
            slider.onValueChanged.AddListener(onValueChanged);

            return slider;
        }

        /// <summary>创建全屏半透明背景（用于弹窗遮罩）.</summary>
        public static Image CreateOverlay(string name, Transform parent, float alpha = 0.7f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, alpha);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return image;
        }

        /// <summary>创建固定位置的矩形面板.</summary>
        public static Image CreatePanel(
            string name,
            Transform parent,
            Vector2 size,
            Vector2 anchoredPosition,
            Color color
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = color;

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            return image;
        }
    }
}
