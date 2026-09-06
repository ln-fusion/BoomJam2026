using System;
using System.Globalization;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Game.Contracts;
using Game.Contracts.Meta;
using Game.Content;
using Game.Meta;
using Game.Foundation;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace Game.Presentation
{
    /// <summary>
    /// MetaHub 主界面壳：固定上栏、下栏、侧栏和页面容器，页面切换不更换 Scene。
    /// </summary>
    public sealed class MetaHubShell : MonoBehaviour, IView<MetaHubViewModel>
    {
        private Canvas _canvas;
        private Text _nicknameText;
        private Text _clockText;
        private Text _pageTitle;
        private Text _sidebarText;
        private MetaPageRouter _router;
        private ILocalizationService _localizationService;
        private GameRuntimeServices _runtimeServices;
        private GlobalCanvasLayer _globalCanvasLayer;
        private GameObject _mapPage;
        private GameObject _archivePage;
        private GameObject _characterPage;
        private GameObject _loungePage;
        private CancellationTokenSource _lifetime;
        private bool _initialized;

        [SerializeField, Tooltip("白盒地图稳定 ID；节点按该地图的 SortOrder 顺序绑定。")]
        private string mapId = "official.map.test_01";
        private readonly List<Button> _mapButtons = new List<Button>();
        private readonly List<UnityAction> _mapActions = new List<UnityAction>();
        private IMetaMapQuery _mapQuery;
        private IReadOnlyList<LevelNodeViewModel> _mapNodes;
        private Text _levelDetails;
        private Button _levelStart;
        private LevelId _selectedLevelId;
        private bool _enteringLevel;

        /// <summary>注入运行时服务并构建壳层。</summary>
        /// <param name="runtimeServices">Bootstrap 创建的运行时服务容器。</param>
        /// <param name="globalCanvasLayer">全局 UI 层。</param>
        public void Initialize(GameRuntimeServices runtimeServices,
            GlobalCanvasLayer globalCanvasLayer)
        {
            if (_initialized)
                return;

            _initialized = true;
            _runtimeServices = runtimeServices ??
                throw new ArgumentNullException(nameof(runtimeServices));
            _globalCanvasLayer = globalCanvasLayer;
            _lifetime = new CancellationTokenSource();
            _localizationService = _runtimeServices.Localization;
            _router = new MetaPageRouter();
            _router.PageChanged += OnPageChanged;
            BuildView();
            BindMapView();
            if (_localizationService != null)
                _localizationService.LocaleChanged += OnLocaleChanged;

            MetaPageId restoredPage = _router.Restore(_runtimeServices.CurrentProfile == null
                ? "map" : _runtimeServices.CurrentProfile.LastMetaPageId);
            Render(new MetaHubViewModel(restoredPage,
                _runtimeServices.CurrentProfile?.PlayerNickname ?? string.Empty,
                "Chapter 01", _runtimeServices.Clock.LocalNow));
        }

        /// <summary>在编辑器导出临时预制体时创建默认壳层。</summary>
        public void BuildPreview()
        {
            if (_canvas == null)
                BuildView();
        }

        /// <summary>每秒刷新驾驶舱本地时间。</summary>
        private void Update()
        {
            if (_clockText == null || _runtimeServices == null)
                return;

            _clockText.text = FormatClock(_runtimeServices.Clock.LocalNow,
                _localizationService == null ? null : _localizationService.CurrentLocaleCode);
        }

        /// <summary>销毁时取消路由、Locale 订阅和页面存档异步操作。</summary>
        private void OnDestroy()
        {
            if (_levelStart != null)
                _levelStart.onClick.RemoveListener(OnLevelStartRequested);
            for (int index = 0; index < _mapButtons.Count; index++)
                if (_mapButtons[index] != null)
                    _mapButtons[index].onClick.RemoveListener(_mapActions[index]);
            if (_router != null)
                _router.PageChanged -= OnPageChanged;
            if (_localizationService != null)
                _localizationService.LocaleChanged -= OnLocaleChanged;
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
        }

        /// <summary>渲染当前页面和档案昵称。</summary>
        /// <param name="viewModel">MetaHub 模型。</param>
        public void Render(MetaHubViewModel viewModel)
        {
            if (viewModel == null)
                return;

            if (_nicknameText != null)
                _nicknameText.text = viewModel.Nickname;
            if (_router != null)
                _router.Navigate(viewModel.Page);
            OnPageChanged(viewModel.Page);
        }

        /// <summary>绑定已有地图按钮与资料卡，初始化拖动内容；不改动预制体布局。</summary>
        private void BindMapView()
        {
            Transform card = _mapPage.transform.Find("LevelCard");
            _levelDetails = card == null ? null : FindText(card, "Details");
            _levelStart = card == null ? null : FindButton(card, "Start");
            if (_levelStart != null)
                _levelStart.onClick.AddListener(OnLevelStartRequested);
            var controller = _mapPage.GetComponentInChildren<MetaMapInteractionController>(true);
            if (controller != null)
                controller.BuildPreview();

            for (int index = 0; ; index++)
            {
                Button button = FindButton(_mapPage.transform, "MapNode_" + (index + 1));
                if (button == null)
                    break;
                int nodeIndex = index;
                UnityAction action = () => SelectMapNode(nodeIndex);
                _mapButtons.Add(button);
                _mapActions.Add(action);
                button.onClick.AddListener(action);
            }
            if (_mapButtons.Count == 0 || _levelDetails == null || _levelStart == null)
                Debug.LogWarning("地图白盒缺少 MapNode_N 或 LevelCard/Details、Start，请检查预制体层级。", this);
            RefreshMapView();
        }

        /// <summary>从当前档案重新计算节点状态，隐藏未解锁或未配置节点；初始化、打开地图和选择节点时刷新。</summary>
        private void RefreshMapView()
        {
            if (_runtimeServices == null)
                return;
            _mapQuery = new MetaMapQuery(new OfficialContentService(
                OfficialTestMapCatalog.CreateProvider()), _runtimeServices.ProgressQuery);
            _mapNodes = _mapQuery.GetLevels(new MapId(mapId));
            for (int index = 0; index < _mapButtons.Count; index++)
            {
                Button button = _mapButtons[index];
                bool available = index < _mapNodes.Count;
                bool visible = available && _mapNodes[index].IsInteractable;
                button.gameObject.SetActive(visible);
                button.interactable = visible && !_enteringLevel;
                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.text = available
                        ? Text(_mapNodes[index].DisplayNameKey) + "\n" + MapStateText(_mapNodes[index].State)
                        : "未配置关卡";
            }
            RenderLevelCard();
        }

        /// <summary>选择当前排序下已解锁的节点；拒绝未解锁节点的选择请求。</summary>
        /// <param name="index">从零开始的节点序号。</param>
        private void SelectMapNode(int index)
        {
            if (_enteringLevel)
                return;
            RefreshMapView();
            if (_mapNodes == null || index < 0 || index >= _mapNodes.Count ||
                !_mapNodes[index].IsInteractable)
                return;
            _selectedLevelId = _mapNodes[index].LevelId;
            RenderLevelCard();
        }

        /// <summary>显示选中关卡的名称、状态与成绩；清除已失效或未解锁的选择，进入期间禁用开始按钮。</summary>
        private void RenderLevelCard()
        {
            LevelCardViewModel card = _selectedLevelId == null || _mapQuery == null
                ? null : _mapQuery.GetLevelCard(_selectedLevelId);
            if (card == null || !card.Node.IsInteractable)
            {
                _selectedLevelId = null;
                card = null;
            }
            if (_levelStart != null)
                _levelStart.interactable = !_enteringLevel && card != null && card.Node.IsInteractable;
            if (_levelDetails == null)
                return;
            if (card == null)
            {
                _levelDetails.text = "请选择关卡";
                return;
            }
            string score = card.BestScore == null ? "暂无成绩"
                : card.BestScore.ElapsedTicks + " ticks";
            _levelDetails.text = Text(card.Node.DisplayNameKey) + "\n状态：" +
                MapStateText(card.Node.State) + "\n最佳成绩：" + score;
        }

        /// <summary>响应资料卡开始请求；已有进入请求时忽略重复点击。</summary>
        private void OnLevelStartRequested()
        {
            if (!_enteringLevel && _runtimeServices != null && _selectedLevelId != null)
                _ = EnterSelectedLevelAsync();
        }

        /// <summary>重新校验当前关卡的解锁状态并交给现有流程服务；使用独立于被卸载地图的取消令牌，异常时显示反馈。</summary>
        /// <returns>进入请求结束时完成的任务；场景是否加载成功仍由流程服务记录。</returns>
        private async Task EnterSelectedLevelAsync()
        {
            _enteringLevel = true;
            try
            {
                RefreshMapView();
                LevelCardViewModel card = _mapQuery.GetLevelCard(_selectedLevelId);
                if (card == null || !card.Node.IsInteractable)
                    return;

                // 地图卸载会取消 _lifetime；导航必须持续到目标场景加载完成。
                await _runtimeServices.Flow.EnterLevelAsync(card.Node.LevelId, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                // 流程取消后，若地图仍存在则由 finally 恢复交互。
            }
            catch (Exception exception)
            {
                if (_globalCanvasLayer != null)
                    _globalCanvasLayer.ShowFeedback(exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                if (this != null)
                {
                    _enteringLevel = false;
                    RefreshMapView();
                }
            }
        }

        /// <summary>取得当前白盒使用的中文节点状态名称。</summary>
        /// <param name="state">查询计算出的节点状态。</param>
        /// <returns>用于节点与资料卡的状态文字。</returns>
        private static string MapStateText(LevelNodeState state)
        {
            return state switch
            {
                LevelNodeState.Locked => "未解锁",
                LevelNodeState.Current => "当前关卡",
                LevelNodeState.Completed => "已完成",
                _ => "已解锁"
            };
        }

        /// <summary>创建 MetaHub 壳层和四个页面占位。</summary>
        private void BuildView()
        {
            if (TryBindConfiguredView())
            {
                RefreshTexts();
                return;
            }

            if (GetComponent<UiPrefabRoot>() != null)
                Debug.LogWarning("MetaHubUI 预制体契约不完整，回退到代码生成界面。", this);

            _canvas = UiFactory.CreateCanvas("SceneCanvas", transform, 0);
            var background = UiFactory.CreatePanel("Background", _canvas.transform, UiTheme.Background);
            UiFactory.Stretch(background.rectTransform, Vector2.zero);

            var header = UiFactory.CreatePanel("HeaderView", _canvas.transform, UiTheme.Panel);
            Place(header.rectTransform, new Vector2(0f, 0.88f), Vector2.one);
            _pageTitle = UiFactory.CreateText("PageTitle", header.transform, string.Empty, 28, UiTheme.Text,
                TextAnchor.MiddleLeft);
            Place(_pageTitle.rectTransform, new Vector2(0.04f, 0.1f), new Vector2(0.42f, 0.9f));
            _nicknameText = UiFactory.CreateText("Nickname", header.transform, string.Empty, 22,
                UiTheme.Muted, TextAnchor.MiddleRight);
            Place(_nicknameText.rectTransform, new Vector2(0.62f, 0.1f), new Vector2(0.82f, 0.9f));
            _clockText = UiFactory.CreateText("Clock", header.transform, string.Empty, 20,
                UiTheme.Muted, TextAnchor.MiddleRight);
            Place(_clockText.rectTransform, new Vector2(0.82f, 0.1f), new Vector2(0.96f, 0.9f));

            var sidebar = UiFactory.CreatePanel("SidebarView", _canvas.transform,
                new Color(0.055f, 0.075f, 0.12f, 1f));
            Place(sidebar.rectTransform, new Vector2(0f, 0.12f), new Vector2(0.22f, 0.88f));
            _sidebarText = UiFactory.CreateText("SidebarInfo", sidebar.transform, string.Empty, 20,
                UiTheme.Muted, TextAnchor.UpperLeft);
            Place(_sidebarText.rectTransform, new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));

            var content = UiFactory.CreatePanel("PageContainer", _canvas.transform,
                new Color(0.07f, 0.09f, 0.14f, 1f));
            Place(content.rectTransform, new Vector2(0.24f, 0.12f), new Vector2(1f, 0.88f));
            _mapPage = CreatePage("MapPageView", content.transform, UiTextKeys.PageMap);
            _archivePage = CreatePage("ArchivePageView", content.transform, UiTextKeys.PageArchive);
            _characterPage = CreatePage("CharacterPageView", content.transform, UiTextKeys.PageCharacter);
            _loungePage = CreatePage("LoungePlaceholderView", content.transform,
                UiTextKeys.LoungeUnavailable);

            var footer = UiFactory.CreatePanel("FooterView", _canvas.transform, UiTheme.Panel);
            Place(footer.rectTransform, Vector2.zero, new Vector2(1f, 0.1f));
            AddNavigationButton(footer.transform, "Map", MetaPageId.Map, 0.04f, 0.22f,
                UiTextKeys.MetaMap);
            AddNavigationButton(footer.transform, "Archive", MetaPageId.Archive, 0.25f, 0.43f,
                UiTextKeys.MetaArchive);
            AddNavigationButton(footer.transform, "Character", MetaPageId.Character, 0.46f, 0.64f,
                UiTextKeys.MetaCharacter);
            AddNavigationButton(footer.transform, "Lounge", MetaPageId.Lounge, 0.67f, 0.85f,
                UiTextKeys.MetaLounge);
            Button settings = UiFactory.CreateButton("Settings", footer.transform, string.Empty);
            Place(settings.GetComponent<RectTransform>(), new Vector2(0.87f, 0.1f),
                new Vector2(0.98f, 0.9f));
            settings.onClick.AddListener(() => _globalCanvasLayer?.OpenSettings());
            RefreshTexts();
        }

        /// <summary>绑定画师预制体中的 MetaHub 约定节点。</summary>
        /// <returns>预制体包含完整必需节点时返回 true。</returns>
        private bool TryBindConfiguredView()
        {
            Transform canvas = transform.Find("SceneCanvas");
            if (canvas == null)
                return false;

            _canvas = canvas.GetComponent<Canvas>();
            var configured = GetComponent<MetaHubUiBindings>();
            if (configured != null && configured.IsComplete)
            {
                _mapPage = configured.MapPage;
                _archivePage = configured.ArchivePage;
                _characterPage = configured.CharacterPage;
                _loungePage = configured.LoungePage;
                WireConfiguredNavigation(canvas);
                return true;
            }
            _pageTitle = FindText(canvas, "PageTitle");
            _nicknameText = FindText(canvas, "Nickname");
            _clockText = FindText(canvas, "Clock");
            _sidebarText = FindText(canvas, "SidebarInfo");
            _mapPage = FindObject(canvas, "MapPageView");
            _archivePage = FindObject(canvas, "ArchivePageView");
            _characterPage = FindObject(canvas, "CharacterPageView");
            _loungePage = FindObject(canvas, "LoungePlaceholderView");
            Transform footer = FindObject(canvas, "FooterView")?.transform;
            Button map = FindButton(footer, "Map");
            Button archive = FindButton(footer, "Archive");
            Button character = FindButton(footer, "Character");
            Button lounge = FindButton(footer, "Lounge");
            Button settings = FindButton(footer, "Settings");
            if (_canvas == null || _pageTitle == null || _nicknameText == null || _clockText == null ||
                _sidebarText == null || _mapPage == null || _archivePage == null ||
                _characterPage == null || _loungePage == null || map == null || archive == null ||
                character == null || lounge == null || settings == null)
                return false;

            map.onClick.AddListener(() => Navigate(MetaPageId.Map));
            archive.onClick.AddListener(() => Navigate(MetaPageId.Archive));
            character.onClick.AddListener(() => Navigate(MetaPageId.Character));
            lounge.onClick.AddListener(() => Navigate(MetaPageId.Lounge));
            settings.onClick.AddListener(() => _globalCanvasLayer?.OpenSettings());
            return true;
        }

        /// <summary>从契约绑定的底栏按钮连接页面路由事件。</summary>
        /// <param name="canvas">界面 Canvas 根节点。</param>
        private void WireConfiguredNavigation(Transform canvas)
        {
            _pageTitle = FindText(canvas, "PageTitle");
            _nicknameText = FindText(canvas, "Nickname");
            _clockText = FindText(canvas, "Clock");
            _sidebarText = FindText(canvas, "SidebarInfo");
            Transform footer = FindObject(canvas, "FooterView")?.transform;
            FindButton(footer, "Map")?.onClick.AddListener(() => Navigate(MetaPageId.Map));
            FindButton(footer, "Archive")?.onClick.AddListener(() => Navigate(MetaPageId.Archive));
            FindButton(footer, "Character")?.onClick.AddListener(() => Navigate(MetaPageId.Character));
            FindButton(footer, "Lounge")?.onClick.AddListener(() => Navigate(MetaPageId.Lounge));
            FindButton(footer, "Settings")?.onClick.AddListener(() => _globalCanvasLayer?.OpenSettings());
        }

        /// <summary>按名称查找文本控件。</summary>
        /// <param name="parent">查找根节点。</param>
        /// <param name="name">节点名称。</param>
        /// <returns>找到的文本；否则为 null。</returns>
        private static Text FindText(Transform parent, string name)
        {
            return FindObject(parent, name)?.GetComponent<Text>();
        }

        /// <summary>按名称查找按钮控件。</summary>
        /// <param name="parent">查找根节点。</param>
        /// <param name="name">节点名称。</param>
        /// <returns>找到的按钮；否则为 null。</returns>
        private static Button FindButton(Transform parent, string name)
        {
            return FindObject(parent, name)?.GetComponent<Button>();
        }

        /// <summary>按名称递归查找节点。</summary>
        /// <param name="parent">查找根节点。</param>
        /// <param name="name">节点名称。</param>
        /// <returns>找到的节点；否则为 null。</returns>
        private static GameObject FindObject(Transform parent, string name)
        {
            if (parent == null)
                return null;
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name)
                    return child.gameObject;
            return null;
        }

        /// <summary>创建一个页面占位容器。</summary>
        /// <param name="name">页面对象名称。</param>
        /// <param name="parent">父节点。</param>
        /// <param name="textKey">占位文本键。</param>
        /// <returns>页面 GameObject。</returns>
        private GameObject CreatePage(string name, Transform parent, string textKey)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);
            var rect = page.GetComponent<RectTransform>();
            UiFactory.Stretch(rect, Vector2.zero);
            var text = UiFactory.CreateText("Placeholder", page.transform, Text(textKey), 32,
                UiTheme.Muted);
            UiFactory.Stretch(text.rectTransform, new Vector2(32f, 32f));
            text.raycastTarget = false;
            if (name == "MapPageView")
            {
                var mapController = page.AddComponent<MetaMapInteractionController>();
                mapController.BuildPreview();
                var cardPanel = page.AddComponent<LevelCardPanel>();
                cardPanel.BuildPreview();
                for (int index = 0; index < 5; index++)
                {
                    Button nodeButton = UiFactory.CreateButton("MapNode_" + (index + 1),
                        mapController.Content, "Node " + (index + 1));
                    RectTransform nodeRect = nodeButton.GetComponent<RectTransform>();
                    nodeRect.sizeDelta = new Vector2(180f, 64f);
                    nodeRect.anchoredPosition = new Vector2(-480f + index * 240f, 0f);
                }
                Button storyButton = UiFactory.CreateButton("TestStory", page.transform, "Test Story");
                RectTransform storyRect = storyButton.GetComponent<RectTransform>();
                storyRect.anchorMin = new Vector2(0.04f, 0.84f);
                storyRect.anchorMax = new Vector2(0.24f, 0.94f);
                storyButton.onClick.AddListener(() =>
                    _ = _runtimeServices.Flow.PlayStoryAsync(
                        new StoryId("official.story.c06_branch"),
                        StoryReturnTarget.ToMetaPage(MetaPageId.Map), CancellationToken.None));
            }
            return page;
        }

        /// <summary>添加底部页面导航按钮。</summary>
        /// <param name="parent">父节点。</param>
        /// <param name="name">按钮名称。</param>
        /// <param name="page">目标页面。</param>
        /// <param name="minX">归一化左侧。</param>
        /// <param name="maxX">归一化右侧。</param>
        /// <param name="textKey">按钮文本键。</param>
        private void AddNavigationButton(Transform parent, string name, MetaPageId page,
            float minX, float maxX, string textKey)
        {
            Button button = UiFactory.CreateButton(name, parent, Text(textKey));
            Place(button.GetComponent<RectTransform>(), new Vector2(minX, 0.1f),
                new Vector2(maxX, 0.9f));
            button.onClick.AddListener(() => Navigate(page));
        }

        /// <summary>路由到页面并异步保存最后页面。</summary>
        /// <param name="page">目标页面。</param>
        private void Navigate(MetaPageId page)
        {
            _router.Navigate(page);
            if (page == MetaPageId.Map)
                RefreshMapView();
            _ = PersistPageAsync(page);
        }

        /// <summary>保存页面变更，不阻塞页面显隐。</summary>
        /// <param name="page">页面。</param>
        private async Task PersistPageAsync(MetaPageId page)
        {
            try
            {
                if (_runtimeServices == null)
                    return;

                CancellationToken cancellationToken = _lifetime == null
                    ? CancellationToken.None : _lifetime.Token;
                await _runtimeServices.SaveLastMetaPageAsync(page, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // 场景生命周期结束时取消属于正常路径。
            }
            catch (Exception exception)
            {
                if (this != null)
                    _globalCanvasLayer?.ShowFeedback(exception.Message);
            }
        }

        /// <summary>路由变更回调。</summary>
        /// <param name="page">当前页面。</param>
        private void OnPageChanged(MetaPageId page)
        {
            _mapPage.SetActive(page == MetaPageId.Map);
            _archivePage.SetActive(page == MetaPageId.Archive);
            _characterPage.SetActive(page == MetaPageId.Character);
            _loungePage.SetActive(page == MetaPageId.Lounge);
            UpdatePageText(page);
            if (page == MetaPageId.Map)
                RefreshMapView();
        }

        /// <summary>Locale 变化回调。</summary>
        /// <param name="localeCode">新 Locale。</param>
        private void OnLocaleChanged(string localeCode)
        {
            RefreshTexts();
            UpdatePageText(_router.CurrentPage);
            RefreshMapView();
            if (_clockText != null && _runtimeServices != null)
                _clockText.text = FormatClock(_runtimeServices.Clock.LocalNow, localeCode);
        }

        /// <summary>按当前 Locale 的区域格式显示时钟，避免固定中文日期格式。</summary>
        /// <param name="value">待显示的本地时间。</param>
        /// <param name="localeCode">BCP-47 Locale 代码。</param>
        /// <returns>区域化日期时间文本。</returns>
        private static string FormatClock(DateTimeOffset value, string localeCode)
        {
            CultureInfo culture = CultureInfo.InvariantCulture;
            if (!string.IsNullOrWhiteSpace(localeCode))
            {
                try
                {
                    culture = CultureInfo.GetCultureInfo(localeCode);
                }
                catch (CultureNotFoundException)
                {
                    // 配置中的 Locale 可能来自未来扩展；使用不变区域保证 UI 仍可显示。
                }
            }

            return value.ToString("g", culture);
        }

        /// <summary>刷新壳层固定文本。</summary>
        private void RefreshTexts()
        {
            if (_sidebarText != null)
                _sidebarText.text = Text(UiTextKeys.MetaMap) + "\n\n" + Text(UiTextKeys.MetaArchive)
                    + "\n" + Text(UiTextKeys.MetaCharacter) + "\n" + Text(UiTextKeys.MetaLounge);

            SetButtonText("Map", Text(UiTextKeys.MetaMap));
            SetButtonText("Archive", Text(UiTextKeys.MetaArchive));
            SetButtonText("Character", Text(UiTextKeys.MetaCharacter));
            SetButtonText("Lounge", Text(UiTextKeys.MetaLounge));
            SetButtonText("Settings", Text(UiTextKeys.Settings));
            SetPagePlaceholder(_mapPage, Text(UiTextKeys.PageMap));
            SetPagePlaceholder(_archivePage, Text(UiTextKeys.PageArchive));
            SetPagePlaceholder(_characterPage, Text(UiTextKeys.PageCharacter));
            SetPagePlaceholder(_loungePage, Text(UiTextKeys.LoungeUnavailable));
        }

        /// <summary>刷新当前页面标题。</summary>
        /// <param name="page">页面。</param>
        private void UpdatePageText(MetaPageId page)
        {
            if (_pageTitle == null)
                return;

            _pageTitle.text = page switch
            {
                MetaPageId.Archive => Text(UiTextKeys.MetaArchive),
                MetaPageId.Character => Text(UiTextKeys.MetaCharacter),
                MetaPageId.Lounge => Text(UiTextKeys.MetaLounge),
                _ => Text(UiTextKeys.MetaMap)
            };
        }

        /// <summary>读取当前本地化文本。</summary>
        /// <param name="key">稳定键字符串。</param>
        /// <returns>本地化文本。</returns>
        private string Text(string key)
        {
            return _localizationService == null
                ? key
                : _localizationService.Get(new Game.Foundation.LocalizationKey(key));
        }

        /// <summary>设置指定按钮文本。</summary>
        /// <param name="buttonName">按钮名称。</param>
        /// <param name="text">文本。</param>
        private void SetButtonText(string buttonName, string text)
        {
            Transform button = _canvas == null ? null : _canvas.transform.Find("FooterView/" + buttonName);
            Text label = button == null ? null : button.GetComponentInChildren<Text>();
            if (label != null)
                label.text = text;
        }

        /// <summary>更新页面占位文本，确保切换 Locale 后当前内容立即刷新。</summary>
        /// <param name="page">页面对象。</param>
        /// <param name="text">占位文本。</param>
        private static void SetPagePlaceholder(GameObject page, string text)
        {
            if (page == null)
                return;

            Text label = page.transform.Find("Placeholder")?.GetComponent<Text>();
            if (label != null)
                label.text = text;
        }

        /// <summary>设置壳层控件的归一化矩形。</summary>
        /// <param name="rectTransform">目标矩形。</param>
        /// <param name="min">归一化左下角。</param>
        /// <param name="max">归一化右上角。</param>
        private static void Place(RectTransform rectTransform, Vector2 min, Vector2 max)
        {
            rectTransform.anchorMin = min;
            rectTransform.anchorMax = max;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }

    /// <summary>MetaHub 壳层的不可变渲染模型。</summary>
    public sealed class MetaHubViewModel
    {
        /// <summary>当前页面。</summary>
        public MetaPageId Page { get; }
        /// <summary>玩家昵称。</summary>
        public string Nickname { get; }
        /// <summary>Current chapter placeholder shown until chapter content is available.</summary>
        public string ChapterPlaceholder { get; }
        /// <summary>Local time supplied by the application clock.</summary>
        public DateTimeOffset LocalTime { get; }

        /// <summary>创建 MetaHub 模型。</summary>
        /// <param name="page">页面。</param>
        /// <param name="nickname">昵称。</param>
        /// <param name="chapterPlaceholder">当前章节的占位文字。</param>
        /// <param name="localTime">用于显示的本地时间；未提供时取当前本地时间。</param>
        public MetaHubViewModel(MetaPageId page, string nickname,
            string chapterPlaceholder = "Chapter 01", DateTimeOffset? localTime = null)
        {
            Page = page;
            Nickname = nickname ?? string.Empty;
            ChapterPlaceholder = chapterPlaceholder ?? string.Empty;
            LocalTime = localTime ?? DateTimeOffset.Now;
        }
    }
}
