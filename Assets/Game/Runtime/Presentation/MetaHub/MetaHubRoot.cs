#nullable enable
using System;
using System.Collections.Generic;
using Game.Contracts;
using Game.Contracts.Progression;
using Game.Contracts.UI;
using Game.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>
    /// 主界面壳层根：上栏/下栏/侧栏占位 + 四页面路由（C05-A）.
    /// </summary>
    /// <remarks>
    /// 页面切换不换 Scene，只替换页面可见性；最后页面 ID 写入 Profile 供恢复.
    /// </remarks>
    public sealed class MetaHubRoot : MonoBehaviour
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        private IDomainEventBus _eventBus = null!;
        private IProgressQuery _progress = null!;
        private IClock _clock = null!;

        private MetaHubShellView? _shell;
        private readonly Dictionary<MetaPageId, GameObject> _pages = new Dictionary<MetaPageId, GameObject>();
        private float _clockTimer;

        /// <summary>装配依赖并订阅场景事件，由组合根调用.</summary>
        /// <remarks>
        /// UIRootManager 是在 SceneActivatedEvent 发布后才创建本组件,
        /// 订阅会错过当前事件, 因此订阅后立即核对激活场景并补发一次.
        /// </remarks>
        public void Initialize(IGameFlowService flow, IDomainEventBus eventBus, IProgressQuery progress, IClock clock)
        {
            if (flow == null)
                throw new ArgumentNullException(nameof(flow));
            if (eventBus == null)
                throw new ArgumentNullException(nameof(eventBus));
            if (progress == null)
                throw new ArgumentNullException(nameof(progress));
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));

            _eventBus = eventBus;
            _progress = progress;
            _clock = clock;

            _subscriptions.Add(eventBus.Subscribe<SceneActivatedEvent>(OnSceneActivated));

            // 补发: 组件创建晚于事件发布时, 直接用激活场景初始化视图
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == SceneNames.MetaHub)
            {
                OnSceneActivated(new SceneActivatedEvent(SceneNames.MetaHub, default));
            }
        }

        private void Update()
        {
            // 下栏时间每秒刷新一次（设计文档 §8.3）；系统时间仅做显示，不参与进度逻辑
            _clockTimer += Time.deltaTime;
            if (_clockTimer >= 1f)
            {
                _clockTimer = 0f;
                _shell?.RenderClock(_clock.LocalNow);
            }
        }

        private void OnSceneActivated(SceneActivatedEvent evt)
        {
            if (evt.SceneName != SceneNames.MetaHub || _shell != null)
                return;

            _shell = new MetaHubShellView(transform);
            _shell.OnPageSelected += SwitchPage;
            BuildPages(_shell.ContentRoot);

            // 默认页：优先事件携带的页面，其次 Map
            var page = evt.MetaPage != default ? evt.MetaPage : MetaPageId.Map;
            SwitchPage(page);

            _shell.Render(
                new MetaHubShellViewModel(
                    page,
                    _progress.GetSnapshot() != null ? "玩家" : string.Empty,
                    lastPageId: page.ToString().ToLowerInvariant()
                )
            );
        }

        private void BuildPages(Transform contentArea)
        {
            _pages[MetaPageId.Map] = CreatePlaceholderPage(contentArea, "地图页面占位");
            _pages[MetaPageId.Archive] = CreatePlaceholderPage(contentArea, "档案页面占位");
            _pages[MetaPageId.Character] = CreatePlaceholderPage(contentArea, "人员页面占位");
            _pages[MetaPageId.Lounge] = CreatePlaceholderPage(contentArea, "暂未开放");
        }

        /// <summary>切换到指定页面（显隐切换，不销毁）.</summary>
        private void SwitchPage(MetaPageId page)
        {
            foreach (var kvp in _pages)
            {
                bool active = kvp.Key == page;
                kvp.Value.SetActive(active);
            }

            // 页面切换事实：组合根订阅后写入 Profile.LastMetaPageId（页面恢复用）
            _eventBus.Publish(new MetaPageChangedEvent(page));
        }

        private static GameObject CreatePlaceholderPage(Transform parent, string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.12f, 0.13f, 0.18f, 1f);

            UIFactory.CreateText("PageLabel", go.transform, label, 26, TextAnchor.MiddleCenter, Color.white);
            return go;
        }

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
    /// 主界面壳视图：页面上栏/下栏/侧栏占位 + 内容区.
    /// </summary>
    public sealed class MetaHubShellView : IView<MetaHubShellViewModel>
    {
        private readonly Canvas _canvas;
        private readonly Text _headerTitle;
        private readonly Text _footerTitle;

        /// <summary>下栏导航点击事件（页面切换）.</summary>
        public event Action<MetaPageId>? OnPageSelected;

        /// <summary>内容区根节点（页面 Presenter 挂载点）.</summary>
        public Transform ContentRoot { get; }

        public MetaHubShellView(Transform parent)
        {
            // 按 1920x1080 参考分辨率缩放：小窗口整体缩小，元素比例与相对关系稳定
            _canvas = UIFactory.CreateCanvas("MetaHubCanvas", scaleWithScreenSize: true);
            _canvas.transform.SetParent(parent, false);

            _headerTitle = CreateBar("HeaderBar", "上栏占位");
            _footerTitle = CreateFooterBar();

            // 侧栏：贴左垂直居中（锚点相对布局，不写死绝对像素）
            var side = UIFactory.CreatePanel(
                "Sidebar",
                _canvas.transform,
                new Vector2(220f, 640f),
                new Vector2(90f, 0f),
                new Color(0.18f, 0.2f, 0.3f, 0.9f)
            );
            var sideRect = side.rectTransform;
            sideRect.anchorMin = new Vector2(0f, 0.5f);
            sideRect.anchorMax = new Vector2(0f, 0.5f);
            sideRect.pivot = new Vector2(0f, 0.5f);
            sideRect.anchoredPosition = new Vector2(90f, 0f);
            _ = UIFactory.CreateText(
                "SidebarLabel",
                side.transform,
                "侧栏占位",
                22,
                TextAnchor.MiddleCenter,
                Color.white
            );

            var content = new GameObject("ContentRoot");
            content.transform.SetParent(_canvas.transform, false);
            var rect = content.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.18f, 0.08f);
            rect.anchorMax = new Vector2(0.96f, 0.92f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            ContentRoot = content.transform;
        }

        /// <summary>创建上/下栏（贴顶/贴底，宽度跟随屏幕）.</summary>
        private Text CreateBar(string name, string label)
        {
            var bar = UIFactory.CreatePanel(
                name,
                _canvas.transform,
                new Vector2(1000f, 64f),
                Vector2.zero,
                new Color(0.13f, 0.15f, 0.22f, 1f)
            );
            var barRect = bar.rectTransform;
            if (name == "HeaderBar")
            {
                // 上栏：顶边锚点拉伸，高 64，贴顶
                barRect.anchorMin = new Vector2(0f, 1f);
                barRect.anchorMax = new Vector2(1f, 1f);
                barRect.pivot = new Vector2(0.5f, 1f);
                barRect.offsetMin = new Vector2(0f, -64f);
                barRect.offsetMax = Vector2.zero;
            }
            else
            {
                // 下栏：底边锚点拉伸，高 64，贴底
                barRect.anchorMin = new Vector2(0f, 0f);
                barRect.anchorMax = new Vector2(1f, 0f);
                barRect.pivot = new Vector2(0.5f, 0f);
                barRect.offsetMin = Vector2.zero;
                barRect.offsetMax = new Vector2(0f, 64f);
            }

            return UIFactory.CreateText($"{name}Label", bar.transform, label, 18, TextAnchor.MiddleLeft, Color.white);
        }

        /// <summary>下栏：Logo + 四个导航按钮.</summary>
        private Text CreateFooterBar()
        {
            var bar = UIFactory.CreatePanel(
                "FooterBar",
                _canvas.transform,
                new Vector2(1000f, 64f),
                Vector2.zero,
                new Color(0.13f, 0.15f, 0.22f, 1f)
            );
            var barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.offsetMin = Vector2.zero;
            barRect.offsetMax = new Vector2(0f, 64f);

            var label = UIFactory.CreateText(
                "FooterLabel",
                bar.transform,
                "下栏",
                18,
                TextAnchor.MiddleLeft,
                Color.white
            );
            label.rectTransform.anchoredPosition = new Vector2(-860f, 0f);

            CreateNavButton(bar.transform, "地图", MetaPageId.Map, -420f);
            CreateNavButton(bar.transform, "档案", MetaPageId.Archive, -240f);
            CreateNavButton(bar.transform, "人员", MetaPageId.Character, -60f);
            CreateNavButton(bar.transform, "休息室", MetaPageId.Lounge, 120f);
            return label;
        }

        /// <summary>创建导航按钮（下栏内，固定间隔）.</summary>
        private void CreateNavButton(Transform parent, string label, MetaPageId page, float x)
        {
            UIFactory.CreateButton(
                $"Nav-{page}",
                parent,
                label,
                () => OnPageSelected?.Invoke(page),
                new Vector2(150f, 48f),
                new Vector2(x, 0f)
            );
        }

        public void Render(MetaHubShellViewModel viewModel)
        {
            if (viewModel == null)
                throw new ArgumentNullException(nameof(viewModel));

            _headerTitle.text = $"上栏占位 - {viewModel.PlayerNickname}";
            _footerTitle.text = $"下栏 - 当前页: {viewModel.LastPageId}";
        }

        /// <summary>刷新下栏时间显示（每秒由 MetaHubRoot 调用）.</summary>
        public void RenderClock(DateTimeOffset localNow)
        {
            _footerTitle.text = localNow.ToString(
                "yyyy-MM-dd HH:mm:ss",
                System.Globalization.CultureInfo.InvariantCulture
            );
        }
    }
}
