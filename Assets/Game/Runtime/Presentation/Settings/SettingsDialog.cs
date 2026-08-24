#nullable enable
using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>
    /// 设置弹窗：音量滑条 + 关闭按钮（C03 骨架，C04 接入本地化/分辨率/全屏）.
    /// </summary>
    public sealed class SettingsDialog : MonoBehaviour
    {
        private Slider? _masterSlider;
        private Slider? _musicSlider;
        private Slider? _sfxSlider;
        private string _title = string.Empty;

        /// <summary>音量预览事件（滑动时触发，未确认时不写盘）.</summary>
        public event Action<float, float, float>? OnVolumePreview;

        /// <summary>关闭弹窗事件.</summary>
        public event Action? OnClosed;

        /// <summary>当前弹窗是否打开.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>关闭弹窗并广播.</summary>
        public void Hide()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            gameObject.SetActive(false);
            OnClosed?.Invoke();
        }

        /// <summary>打开弹窗.</summary>
        public void Show()
        {
            if (IsOpen)
                return;

            IsOpen = true;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 构建弹窗（运行时程序化创建，不依赖场景手动搭建）.
        /// </summary>
        /// <param name="parent">父节点（SceneCanvas 下）</param>
        /// <param name="title">窗口标题</param>
        public static SettingsDialog Create(Transform parent, string title)
        {
            var go = new GameObject("SettingsDialog");
            go.transform.SetParent(parent, false);

            var dialog = go.AddComponent<SettingsDialog>();
            dialog._title = title;
            dialog.Build();
            return dialog;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            UIFactory.CreateOverlay("DimOverlay", transform, 0.6f);

            var panel = UIFactory.CreatePanel(
                "Panel",
                transform,
                new Vector2(520f, 420f),
                Vector2.zero,
                new Color(0.15f, 0.17f, 0.22f, 0.98f)
            );

            UIFactory.CreateText("Title", panel.transform, _title, 28);

            _masterSlider = CreateVolumeRow(panel.transform, "Master", "主音量", new Vector2(0f, 110f));
            _musicSlider = CreateVolumeRow(panel.transform, "Music", "音乐音量", new Vector2(0f, 40f));
            _sfxSlider = CreateVolumeRow(panel.transform, "Sfx", "音效音量", new Vector2(0f, -30f));

            UIFactory.CreateButton(
                "CloseButton",
                panel.transform,
                "关闭",
                Hide,
                new Vector2(160f, 48f),
                new Vector2(0f, -140f)
            );
        }

        private Slider CreateVolumeRow(Transform panel, string name, string label, Vector2 position)
        {
            UIFactory
                .CreateText($"{name}Label", panel, label, 18, TextAnchor.MiddleLeft, Color.white)
                .rectTransform.anchoredPosition = position + new Vector2(-110f, 0f);

            return UIFactory.CreateSlider(
                $"{name}Slider",
                panel,
                1f,
                v =>
                    OnVolumePreview?.Invoke(
                        _masterSlider?.value ?? 1f,
                        _musicSlider?.value ?? 1f,
                        _sfxSlider?.value ?? 1f
                    ),
                new Vector2(300f, 24f),
                position
            );
        }
    }
}
