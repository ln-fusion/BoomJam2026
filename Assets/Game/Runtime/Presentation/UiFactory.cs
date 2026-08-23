using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>运行时 uGUI 控件工厂；集中处理占位界面的基础布局和颜色。</summary>
    public static class UiFactory
    {
        /// <summary>创建全屏 Screen Space Overlay Canvas。</summary>
        /// <param name="name">GameObject 名称。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="sortingOrder">Canvas 排序层。</param>
        /// <returns>创建的 Canvas。</returns>
        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            gameObject.transform.SetParent(parent, false);
            var canvas = gameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = gameObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>创建带纯色背景的面板。</summary>
        /// <param name="name">GameObject 名称。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="color">背景颜色。</param>
        /// <returns>面板 Image。</returns>
        public static Image CreatePanel(string name, Transform parent, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>创建文本标签。</summary>
        /// <param name="name">GameObject 名称。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="text">初始文本。</param>
        /// <param name="fontSize">字体大小。</param>
        /// <param name="color">文本颜色。</param>
        /// <param name="alignment">对齐方式。</param>
        /// <returns>创建的 Text。</returns>
        public static Text CreateText(string name, Transform parent, string text, int fontSize,
            Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var label = gameObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text ?? string.Empty;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        /// <summary>创建按钮及其文本。</summary>
        /// <param name="name">GameObject 名称。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="label">按钮文本。</param>
        /// <returns>创建的 Button。</returns>
        public static Button CreateButton(string name, Transform parent, string label)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(Button));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = UiTheme.Button;
            var button = gameObject.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = UiTheme.Button;
            colors.highlightedColor = UiTheme.ButtonHighlight;
            colors.pressedColor = UiTheme.ButtonPressed;
            colors.disabledColor = UiTheme.ButtonDisabled;
            button.colors = colors;
            var text = CreateText("Label", gameObject.transform, label, 28, UiTheme.Text);
            Stretch(text.rectTransform, new Vector2(8f, 4f));
            return button;
        }

        /// <summary>创建水平滑块。</summary>
        /// <param name="name">GameObject 名称。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="value">初始值。</param>
        /// <returns>创建的 Slider。</returns>
        public static Slider CreateSlider(string name, Transform parent, float value)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Slider));
            gameObject.transform.SetParent(parent, false);
            var slider = gameObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = Mathf.Clamp01(value);

            var background = CreatePanel("Background", gameObject.transform, UiTheme.SliderTrack);
            Stretch(background.rectTransform, new Vector2(0f, 12f));
            var fill = CreatePanel("Fill", gameObject.transform, UiTheme.Accent);
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0f, 0.5f);
            fillRect.anchorMax = new Vector2(1f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.sizeDelta = new Vector2(0f, 12f);
            var handle = CreatePanel("Handle", gameObject.transform, UiTheme.Text);
            var handleRect = handle.rectTransform;
            handleRect.sizeDelta = new Vector2(24f, 24f);
            slider.targetGraphic = handle;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            return slider;
        }

        /// <summary>创建下拉选择框。</summary>
        /// <param name="name">GameObject 名称。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="options">选项文本。</param>
        /// <param name="value">初始索引。</param>
        /// <returns>创建的 Dropdown。</returns>
        public static Dropdown CreateDropdown(string name, Transform parent,
            System.Collections.Generic.List<string> options, int value)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(Dropdown));
            gameObject.transform.SetParent(parent, false);
            gameObject.GetComponent<Image>().color = UiTheme.Input;
            var dropdown = gameObject.GetComponent<Dropdown>();
            dropdown.options.Clear();
            if (options != null)
            {
                foreach (string option in options)
                    dropdown.options.Add(new Dropdown.OptionData(option));
            }
            dropdown.value = options == null || options.Count == 0
                ? 0 : Mathf.Clamp(value, 0, options.Count - 1);
            var caption = CreateText("Label", gameObject.transform, string.Empty, 22, UiTheme.Text,
                TextAnchor.MiddleLeft);
            Stretch(caption.rectTransform, new Vector2(16f, 2f));
            dropdown.captionText = caption;
            CreateDropdownTemplate(gameObject.transform, dropdown);
            return dropdown;
        }

        /// <summary>按 legacy Dropdown 的手动排版规则配置选项模板。</summary>
        /// <param name="dropdown">待配置的下拉控件。</param>
        public static void ConfigureDropdownTemplate(Dropdown dropdown)
        {
            if (dropdown == null || dropdown.template == null)
                return;

            Toggle item = dropdown.template.GetComponentInChildren<Toggle>(true);
            if (item == null || !(item.transform.parent is RectTransform contentRect))
                return;

            VerticalLayoutGroup layout = contentRect.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
                layout.enabled = false;
            ContentSizeFitter fitter = contentRect.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                fitter.enabled = false;

            Mask mask = dropdown.template.GetComponentInChildren<Mask>(true);
            if (mask != null)
            {
                Image maskImage = mask.GetComponent<Image>();
                if (maskImage != null)
                    maskImage.color = Color.white;
                mask.showMaskGraphic = false;
            }

            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 44f);

            var itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.sizeDelta = new Vector2(0f, 36f);
        }

        /// <summary>创建复选框。</summary>
        /// <param name="name">GameObject 名称。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="label">复选框文本。</param>
        /// <param name="value">初始状态。</param>
        /// <returns>创建的 Toggle。</returns>
        public static Toggle CreateToggle(string name, Transform parent, string label, bool value)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            gameObject.transform.SetParent(parent, false);
            var background = CreatePanel("Background", gameObject.transform, UiTheme.Input);
            background.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            background.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            background.rectTransform.sizeDelta = new Vector2(32f, 32f);
            var checkmark = CreatePanel("Checkmark", background.transform, UiTheme.Accent);
            Stretch(checkmark.rectTransform, new Vector2(6f, 6f));
            var text = CreateText("Label", gameObject.transform, label, 22, UiTheme.Text,
                TextAnchor.MiddleLeft);
            var textRect = text.rectTransform;
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(48f, 0f);
            textRect.offsetMax = Vector2.zero;
            var toggle = gameObject.GetComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.isOn = value;
            return toggle;
        }

        /// <summary>创建单行输入框。</summary>
        /// <param name="name">GameObject 名称。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="placeholder">占位文本。</param>
        /// <returns>创建的 InputField。</returns>
        public static InputField CreateInputField(string name, Transform parent, string placeholder)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(InputField));
            gameObject.transform.SetParent(parent, false);
            gameObject.GetComponent<Image>().color = UiTheme.Input;
            var input = gameObject.GetComponent<InputField>();
            var text = CreateText("Text", gameObject.transform, string.Empty, 24, UiTheme.Text,
                TextAnchor.MiddleLeft);
            Stretch(text.rectTransform, new Vector2(14f, 2f));
            var hint = CreateText("Placeholder", gameObject.transform, placeholder, 24,
                UiTheme.Muted, TextAnchor.MiddleLeft);
            Stretch(hint.rectTransform, new Vector2(14f, 2f));
            input.textComponent = text;
            input.placeholder = hint;
            input.characterLimit = 32;
            return input;
        }

        /// <summary>将 RectTransform 拉伸到父节点并保留边距。</summary>
        /// <param name="rectTransform">目标 RectTransform。</param>
        /// <param name="margin">水平和垂直边距。</param>
        public static void Stretch(RectTransform rectTransform, Vector2 margin)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = margin * 0.5f;
            rectTransform.offsetMax = -margin * 0.5f;
        }

        /// <summary>创建 legacy Dropdown 所需的隐藏模板和列表项。</summary>
        /// <param name="parent">Dropdown 根节点。</param>
        /// <param name="dropdown">目标 Dropdown。</param>
        private static void CreateDropdownTemplate(Transform parent, Dropdown dropdown)
        {
            var templateObject = new GameObject("Template", typeof(RectTransform), typeof(Image),
                typeof(ScrollRect));
            templateObject.transform.SetParent(parent, false);
            var templateRect = templateObject.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -4f);
            templateRect.sizeDelta = new Vector2(0f, 220f);
            templateObject.GetComponent<Image>().color = UiTheme.Input;
            var scroll = templateObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image),
                typeof(Mask));
            viewportObject.transform.SetParent(templateObject.transform, false);
            var viewportRect = viewportObject.GetComponent<RectTransform>();
            Stretch(viewportRect, Vector2.zero);
            viewportObject.GetComponent<Image>().color = Color.white;
            viewportObject.GetComponent<Mask>().showMaskGraphic = false;

            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewportObject.transform, false);
            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 44f);
            scroll.viewport = viewportRect;
            scroll.content = contentRect;

            var itemObject = new GameObject("Item", typeof(RectTransform), typeof(Toggle),
                typeof(Image));
            itemObject.transform.SetParent(contentObject.transform, false);
            var itemRect = itemObject.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 36f);
            itemObject.GetComponent<Image>().color = UiTheme.Button;
            var itemToggle = itemObject.GetComponent<Toggle>();
            var itemLabel = CreateText("Item Label", itemObject.transform, string.Empty, 20,
                UiTheme.Text, TextAnchor.MiddleLeft);
            Stretch(itemLabel.rectTransform, new Vector2(20f, 2f));
            itemToggle.targetGraphic = itemObject.GetComponent<Image>();
            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;
            dropdown.itemImage = null;
            templateObject.SetActive(false);
        }
    }

    /// <summary>占位 UI 使用的统一颜色。</summary>
    public static class UiTheme
    {
        /// <summary>深色背景。</summary>
        public static readonly Color Background = new Color(0.035f, 0.047f, 0.075f, 1f);
        /// <summary>面板颜色。</summary>
        public static readonly Color Panel = new Color(0.08f, 0.11f, 0.17f, 0.97f);
        /// <summary>按钮颜色。</summary>
        public static readonly Color Button = new Color(0.14f, 0.24f, 0.38f, 1f);
        /// <summary>按钮高亮颜色。</summary>
        public static readonly Color ButtonHighlight = new Color(0.2f, 0.38f, 0.58f, 1f);
        /// <summary>按钮按下颜色。</summary>
        public static readonly Color ButtonPressed = new Color(0.1f, 0.18f, 0.3f, 1f);
        /// <summary>禁用按钮颜色。</summary>
        public static readonly Color ButtonDisabled = new Color(0.2f, 0.22f, 0.25f, 1f);
        /// <summary>输入框颜色。</summary>
        public static readonly Color Input = new Color(0.04f, 0.06f, 0.1f, 1f);
        /// <summary>滑块轨道颜色。</summary>
        public static readonly Color SliderTrack = new Color(0.02f, 0.03f, 0.05f, 1f);
        /// <summary>强调色。</summary>
        public static readonly Color Accent = new Color(0.28f, 0.72f, 0.9f, 1f);
        /// <summary>主文本颜色。</summary>
        public static readonly Color Text = new Color(0.92f, 0.96f, 1f, 1f);
        /// <summary>次要文本颜色。</summary>
        public static readonly Color Muted = new Color(0.62f, 0.7f, 0.8f, 1f);
        /// <summary>遮罩颜色。</summary>
        public static readonly Color Overlay = new Color(0f, 0f, 0f, 0.72f);
    }
}
