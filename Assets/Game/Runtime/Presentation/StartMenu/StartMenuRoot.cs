#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.UI;
using Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>
    /// 开始菜单界面根：监听场景激活事件，动态创建 Canvas 与主按钮组.
    /// </summary>
    /// <remarks>
    /// C03-A：按钮接入 Flow 服务，实现“开始/继续”、“设置”、“退出”占位流程.
    /// 代码程序化构建（方案 A），美术介入后迁移 Prefab 时仅替换 View 实现.
    /// </remarks>
    public sealed class StartMenuRoot : MonoBehaviour
    {
        private readonly System.Collections.Generic.List<IDisposable> _subscriptions =
            new System.Collections.Generic.List<IDisposable>();

        private IGameFlowService _flow = null!;
        private ISettingsService _settings = null!;
        private ILocalizationService? _localization;
        private StartMenuView? _view;

        /// <summary>装配依赖并订阅场景激活事件，由组合根调用.</summary>
        public void Initialize(
            IGameFlowService flow,
            IDomainEventBus eventBus,
            ISettingsService settings,
            ILocalizationService? localization = null
        )
        {
            if (flow == null)
                throw new ArgumentNullException(nameof(flow));
            if (eventBus == null)
                throw new ArgumentNullException(nameof(eventBus));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            _flow = flow;
            _settings = settings;
            _localization = localization;

            _subscriptions.Add(eventBus.Subscribe<SceneActivatedEvent>(OnSceneActivated));
        }

        private void OnSceneActivated(SceneActivatedEvent evt)
        {
            if (evt.SceneName != SceneNames.StartMenu)
                return;

            if (_view != null)
                return;

            _view = new StartMenuView(transform, key => _localization?.Resolve(key) ?? key);
            _view.Render(
                new StartMenuViewModel(hasProfile: false, playerNickname: string.Empty, canOpenSettings: true)
            );

            _view.OnStartClicked += AsyncFireAndForget(() => _flow.StartOrContinueAsync(CancellationToken.None));
            _view.OnSettingsClicked += OpenSettingsDialog;
            _view.OnQuitClicked += AsyncFireAndForget(() => _flow.QuitGameAsync(CancellationToken.None));
        }

        /// <summary>
        /// 打开设置弹窗：初始值取当前设置，关闭时应用变更.
        /// </summary>
        private void OpenSettingsDialog()
        {
            var dialog = SettingsDialog.Create(transform, "设置");
            dialog.OnClosed += () =>
            {
                _ = _settings.ApplyAsync(SettingsDraft.FromSnapshot(_settings.Current), CancellationToken.None);
                Destroy(dialog.gameObject);
            };
            dialog.Show();
        }

        private static Action AsyncFireAndForget(Func<Task> asyncAction) =>
            () =>
            {
                _ = asyncAction();
            };

        private void OnDestroy()
        {
            foreach (var sub in _subscriptions)
            {
                sub.Dispose();
            }

            _subscriptions.Clear();
        }
    }

    /// <summary>
    /// 开始菜单视图：程序化构建标题 + 三个主操作按钮.
    /// </summary>
    public sealed class StartMenuView : IView<StartMenuViewModel>
    {
        /// <summary>开始/继续按钮点击事件.</summary>
        public event Action? OnStartClicked;

        /// <summary>设置按钮点击事件.</summary>
        public event Action? OnSettingsClicked;

        /// <summary>退出按钮点击事件.</summary>
        public event Action? OnQuitClicked;

        private readonly Canvas _canvas;
        private readonly Text _title;
        private readonly ListEntryHandler _startButton;
        private readonly Func<string, string> _resolveText;

        public StartMenuView(Transform parent, Func<string, string> resolveText)
        {
            _resolveText = resolveText ?? (key => key);

            _canvas = UIFactory.CreateCanvas("StartMenuCanvas");
            _canvas.transform.SetParent(parent, false);

            _title = UIFactory.CreateText(
                "Title",
                _canvas.transform,
                _resolveText("ui.start_menu.title"),
                44,
                TextAnchor.MiddleCenter,
                Color.white
            );
            _title.rectTransform.anchoredPosition = new Vector2(0f, 180f);

            _startButton = new ListEntryHandler(
                UIFactory.CreateButton(
                    "StartButton",
                    _canvas.transform,
                    _resolveText("ui.start_menu.start"),
                    () => OnStartClicked?.Invoke(),
                    new Vector2(280f, 56f),
                    new Vector2(0f, 60f)
                )
            );

            _ = new ListEntryHandler(
                UIFactory.CreateButton(
                    "SettingsButton",
                    _canvas.transform,
                    _resolveText("ui.start_menu.settings"),
                    () => OnSettingsClicked?.Invoke(),
                    new Vector2(280f, 56f),
                    new Vector2(0f, -10f)
                )
            );

            _ = new ListEntryHandler(
                UIFactory.CreateButton(
                    "QuitButton",
                    _canvas.transform,
                    _resolveText("ui.start_menu.quit"),
                    () => OnQuitClicked?.Invoke(),
                    new Vector2(280f, 56f),
                    new Vector2(0f, -80f)
                )
            );
        }

        public void Render(StartMenuViewModel viewModel)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            _startButton.Button.gameObject.SetActive(true);
            _startButton.Text.text = viewModel.HasProfile
                ? _resolveText("ui.start_menu.continue")
                : _resolveText("ui.start_menu.start");
        }
    }

    /// <summary>
    /// 按钮-文本绑定辅助（提取按钮内 Label 引用）.
    /// </summary>
    public sealed class ListEntryHandler
    {
        public Button Button { get; }
        public Text Text { get; }

        public ListEntryHandler(Button button)
        {
            if (button == null)
                throw new ArgumentNullException(nameof(button));

            Button = button;
            Text = button.GetComponentInChildren<Text>();
        }
    }
}
