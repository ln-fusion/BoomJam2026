using System;
using System.Collections.Generic;
using System.IO;
using Game.Contracts.Content;
using Game.Foundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor.Level
{
    /// <summary>
    /// 官方关卡编辑器窗口; C20 提供关卡列表、新建、打开、保存、脏标记与 2D 视口骨架。
    /// </summary>
    /// <remarks>
    /// 本窗口是关卡数据的唯一真相来源, Unity Scene 不参与关卡编辑。视口是
    /// <see cref="LevelAuthoringData"/> 的投影, 保存时回写 Authoring DTO。
    /// 关闭窗口或切换关卡前若存在未保存修改, 会弹出确认对话框。
    /// <para>
    /// C20 尚未实现的能力: 区域顶点编辑、对象拖拽、白名单参数 Inspector、
    /// 能力与条件配置、解锁前置图、一键校验与 Compile &amp; Play。这些在 C21-C32 补齐。
    /// </para>
    /// </remarks>
    public sealed class LevelEditorWindow : EditorWindow
    {
        /// <summary>关卡 Authoring 文件的默认根目录; 相对于项目根。</summary>
        public const string DefaultLevelsRootPath = "Assets/Game/Content/Authoring/Levels";

        /// <summary>左侧关卡列表的初始宽度; 像素。</summary>
        public const float LevelPaneWidth = 240f;

        /// <summary>右侧属性栏的初始宽度; 像素。</summary>
        public const float InspectorPaneWidth = 340f;

        /// <summary>关卡列表的行高; 每行两段文本需要比默认行高更高的取值。</summary>
        public const float LevelRowHeight = 34f;

        private readonly List<LevelAuthoringData> _levels = new List<LevelAuthoringData>();
        private ILevelAuthoringRepository _repository;
        private LevelEditController _controller;
        private ListView _levelList;
        private LevelViewportElement _viewport;
        private Label _listHeaderLabel;
        private HelpBox _listEmptyHint;
        private VisualElement _levelContainer;
        private VisualElement _paletteContainer;
        private VisualElement _inspectorContainer;
        private VisualElement _validationContainer;
        private Label _levelIdLabel;
        private Label _levelStatsLabel;
        private TextField _mapIdField;
        private TextField _displayNameKeyField;
        private IntegerField _capacityField;
        private Label _statusLabel;
        private Label _dirtyLabel;
        private LevelAuthoringData _current;
        private bool _isDirty;

        /// <summary>打开关卡编辑器窗口。</summary>
        [MenuItem("Game/Level Editor Window")]
        public static void Open() => GetWindow<LevelEditorWindow>("Level Editor");

        /// <summary>创建窗口界面骨架并加载关卡列表。</summary>
        /// <remarks>
        /// 布局是三栏: 左关卡列表、中视口、右属性栏。属性栏自身可滚动, 因为它承载关卡的
        /// 全部属性、对象参数与校验结果, 高度不可预期; 视口不放进滚动容器, 否则滚轮缩放
        /// 与拖动平移会被滚动容器截走。
        /// </remarks>
        public void CreateGUI()
        {
            _repository = new FileLevelAuthoringRepository(GetLevelsRootPath());
            VisualElement root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            root.Add(BuildToolbar());

            var outer = new TwoPaneSplitView(0, LevelPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
            outer.style.flexGrow = 1f;
            outer.Add(BuildLevelPane());
            var inner = new TwoPaneSplitView(1, InspectorPaneWidth, TwoPaneSplitViewOrientation.Horizontal);
            inner.style.flexGrow = 1f;
            inner.Add(BuildViewportPane());
            inner.Add(BuildInspectorPane());
            outer.Add(inner);
            root.Add(outer);
            root.Add(BuildStatusBar());

            BuildPaletteSection();
            ReloadLevels();
        }

        /// <summary>构建顶部工具栏: 文件操作、视图取景与脏标记。</summary>
        /// <returns>工具栏元素。</returns>
        private VisualElement BuildToolbar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.paddingLeft = 6f;
            bar.style.paddingRight = 6f;
            bar.style.paddingTop = 4f;
            bar.style.paddingBottom = 4f;
            bar.Add(MakeButton("新建关卡", OnCreateLevel));
            bar.Add(MakeButton("保存", OnSaveLevel));
            bar.Add(MakeButton("重新加载", ReloadLevels));
            bar.Add(MakeSeparator());
            bar.Add(MakeButton("缩放到适应", () => _viewport?.ZoomToFitContent()));
            bar.Add(MakeButton("定位到内容", () => _viewport?.FocusOnContent()));
            bar.Add(MakeButton("重置视图", () => _viewport?.ResetView()));
            _dirtyLabel = new Label();
            _dirtyLabel.style.marginLeft = 8f;
            _dirtyLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _dirtyLabel.style.color = new Color(1f, 0.75f, 0.3f);
            bar.Add(_dirtyLabel);
            return bar;
        }

        /// <summary>构建左侧关卡列表面板。</summary>
        /// <returns>列表面板元素。</returns>
        private VisualElement BuildLevelPane()
        {
            var pane = new VisualElement();
            pane.style.flexDirection = FlexDirection.Column;

            _listHeaderLabel = new Label();
            _listHeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _listHeaderLabel.style.paddingLeft = 6f;
            _listHeaderLabel.style.paddingTop = 4f;
            pane.Add(_listHeaderLabel);

            _listEmptyHint = new HelpBox("还没有关卡。用工具栏「新建关卡」创建第一个关卡。", HelpBoxMessageType.Info);
            _listEmptyHint.style.display = DisplayStyle.None;
            pane.Add(_listEmptyHint);

            _levelList = new ListView(_levels, LevelRowHeight, MakeLevelRow, BindLevelRow);
            _levelList.selectionType = SelectionType.Single;
            _levelList.style.flexGrow = 1f;
            _levelList.selectedIndicesChanged += OnLevelSelected;
            pane.Add(_levelList);
            return pane;
        }

        /// <summary>构建中栏视口; 中栏只放视口, 保证滚轮与拖动不会被其它控件截走。</summary>
        /// <returns>视口面板元素。</returns>
        private VisualElement BuildViewportPane()
        {
            var pane = new VisualElement();
            pane.style.flexDirection = FlexDirection.Column;
            pane.style.flexGrow = 1f;
            pane.style.minWidth = 160f;

            _viewport = new LevelViewportElement();
            _viewport.style.flexGrow = 1f;
            _viewport.style.backgroundColor = new Color(0.13f, 0.13f, 0.15f);
            _viewport.style.marginLeft = 4f;
            _viewport.style.marginRight = 4f;
            _viewport.style.marginTop = 4f;
            _viewport.style.marginBottom = 4f;
            _viewport.ViewChanged += OnViewportChanged;
            _viewport.SelectionChanged += OnViewportSelectionChanged;
            _viewport.ItemDragged += OnViewportItemDragged;
            _viewport.DeleteRequested += OnViewportDeleteRequested;
            _viewport.VertexInsertRequested += OnViewportVertexInsertRequested;
            pane.Add(_viewport);
            return pane;
        }

        /// <summary>构建右侧属性栏: 关卡、添加、选中项与校验四个可折叠分区。</summary>
        /// <returns>属性栏元素。</returns>
        /// <remarks>
        /// 分区容器在窗口生命周期内只创建一次, 之后只清空并填充分区内容:
        /// 这样折叠状态与滚动位置不会因为一次编辑而复位, 也不会把正在输入的光标顶掉。
        /// </remarks>
        private VisualElement BuildInspectorPane()
        {
            var scroll = new ScrollView();
            scroll.style.minWidth = 220f;
            scroll.style.paddingLeft = 6f;
            scroll.style.paddingRight = 6f;
            scroll.style.paddingTop = 4f;
            scroll.style.paddingBottom = 6f;

            _levelContainer = new VisualElement();
            _paletteContainer = new VisualElement();
            _inspectorContainer = new VisualElement();
            _validationContainer = new VisualElement();

            scroll.Add(MakeSection("关卡", _levelContainer, true));
            scroll.Add(MakeSection("添加", _paletteContainer, true));
            scroll.Add(MakeSection("选中项", _inspectorContainer, true));
            scroll.Add(MakeSection("校验", _validationContainer, true));
            return scroll;
        }

        /// <summary>创建一个带标题的可折叠分区。</summary>
        /// <param name="title">分区标题。</param>
        /// <param name="body">分区内容容器。</param>
        /// <param name="expanded">是否默认展开。</param>
        /// <returns>分区元素。</returns>
        private static Foldout MakeSection(string title, VisualElement body, bool expanded)
        {
            var foldout = new Foldout { text = title, value = expanded };
            foldout.style.marginBottom = 2f;
            foldout.Add(body);
            return foldout;
        }

        /// <summary>填充"添加"分区: 区域与官方对象调色板。</summary>
        /// <remarks>
        /// 新增内容落在视口中心, 随后可直接拖动; 这样无需实现拖放式调色板也能完成放置。
        /// 新内容落在视口中心而不是世界原点, 因此无论视图平移到哪里都能立刻看到结果。
        /// </remarks>
        private void BuildPaletteSection()
        {
            _paletteContainer.Clear();
            var zoneRow = new VisualElement();
            zoneRow.style.flexDirection = FlexDirection.Row;
            zoneRow.Add(MakeButton("+可部署区", () => AddZoneAtViewCenter(ZoneKind.Deployable)));
            zoneRow.Add(MakeButton("+禁放区", () => AddZoneAtViewCenter(ZoneKind.Forbidden)));
            _paletteContainer.Add(zoneRow);

            var objectGrid = new VisualElement();
            objectGrid.style.flexDirection = FlexDirection.Row;
            objectGrid.style.flexWrap = Wrap.Wrap;
            foreach (PaletteEntry entry in LevelPaletteCatalog.GetEntries())
            {
                PaletteEntry captured = entry;
                objectGrid.Add(MakeButton("+" + captured.DisplayName, () => AddPaletteEntryAtViewCenter(captured)));
            }
            _paletteContainer.Add(objectGrid);
        }

        /// <summary>构建底部状态栏: 操作提示与最近一次操作结果。</summary>
        /// <returns>状态栏元素。</returns>
        private VisualElement BuildStatusBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.paddingLeft = 6f;
            bar.style.paddingBottom = 3f;
            bar.style.flexShrink = 0f;
            var hint = new Label(
                "左键选中并拖动 · 空白处左键或中键或 Alt+左键平移 · 滚轮缩放 · 双击区域边插顶点 · Delete 删除 · Esc 取消选中 · F 缩放到适应 · Home 重置视图"
            );
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.flexShrink = 1f;
            bar.Add(hint);
            _statusLabel = new Label();
            _statusLabel.style.marginLeft = 12f;
            _statusLabel.style.color = new Color(1f, 0.8f, 0.4f);
            _statusLabel.style.whiteSpace = WhiteSpace.NoWrap;
            bar.Add(_statusLabel);
            return bar;
        }

        /// <summary>创建工具栏的分组分隔符。</summary>
        /// <returns>分隔符元素。</returns>
        private static VisualElement MakeSeparator()
        {
            var separator = new Label("│");
            separator.style.marginLeft = 6f;
            separator.style.marginRight = 6f;
            separator.style.color = new Color(0.4f, 0.4f, 0.45f);
            return separator;
        }

        /// <summary>创建一个工具栏按钮。</summary>
        /// <param name="text">按钮文本。</param>
        /// <param name="onClick">点击回调。</param>
        /// <returns>按钮元素。</returns>
        private static Button MakeButton(string text, Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.style.marginRight = 4f;
            return button;
        }

        /// <summary>关卡列表行内关卡 ID 标签的名称; 用于绑定行数据时定位子元素。</summary>
        private const string LevelIdRowPart = "level-id";

        /// <summary>关卡列表行内内容统计标签的名称; 用于绑定行数据时定位子元素。</summary>
        private const string LevelStatsRowPart = "level-stats";

        /// <summary>创建关卡列表的一行; 两段文本分别是关卡 ID 与内容统计。</summary>
        /// <returns>行元素。</returns>
        private static VisualElement MakeLevelRow()
        {
            var row = new VisualElement();
            row.style.paddingLeft = 4f;
            row.style.paddingTop = 2f;
            row.style.justifyContent = Justify.Center;

            var levelId = new Label { name = LevelIdRowPart };
            levelId.style.unityFontStyleAndWeight = FontStyle.Bold;
            var stats = new Label { name = LevelStatsRowPart };
            stats.style.fontSize = 10f;
            stats.style.color = new Color(0.6f, 0.63f, 0.7f);

            row.Add(levelId);
            row.Add(stats);
            return row;
        }

        /// <summary>绑定关卡列表行数据; 统计与校验状态让列表本身就能反映关卡是否可用。</summary>
        /// <param name="element">行元素。</param>
        /// <param name="index">数据下标。</param>
        private void BindLevelRow(VisualElement element, int index)
        {
            if (element == null || index < 0 || index >= _levels.Count)
                return;
            LevelAuthoringData data = _levels[index];
            LevelDefinition definition = data?.Definition;
            Label levelId = element.Q<Label>(LevelIdRowPart);
            if (levelId != null)
                levelId.text = definition?.LevelId ?? "(无法解析的关卡文件)";
            Label stats = element.Q<Label>(LevelStatsRowPart);
            if (stats == null)
                return;
            if (definition == null)
            {
                stats.text = "定义缺失, 无法编辑";
                return;
            }
            int issueCount = LevelEditValidator.Validate(data).Count;
            string state = issueCount == 0 ? "校验通过" : $"⚠ {issueCount} 个问题";
            stats.text =
                $"对象 {definition.Objects?.Count ?? 0} · 区 {definition.DeployableZones?.Count ?? 0}"
                + $"/{definition.ForbiddenZones?.Count ?? 0} · {state}";
        }

        /// <summary>获取关卡 Authoring 根目录; 由项目根拼接相对路径。</summary>
        /// <returns>绝对目录路径。</returns>
        private static string GetLevelsRootPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            return Path.Combine(projectRoot, DefaultLevelsRootPath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>重新枚举磁盘上的关卡源数据。</summary>
        private void ReloadLevels()
        {
            if (!ConfirmDiscardChanges())
                return;
            IReadOnlyList<LevelAuthoringData> loaded = _repository.GetAllLevels();
            _levels.Clear();
            _levels.AddRange(loaded);
            _levelList?.Rebuild();
            RefreshLevelPane();
            if (_levels.Count > 0)
                _levelList.selectedIndex = 0;
            else
                ApplyCurrent(null);
            SetStatus($"已加载 {_levels.Count} 个关卡");
        }

        /// <summary>刷新关卡列表的标题、空列表提示与列表可见性。</summary>
        /// <remarks>
        /// 没有关卡时隐藏列表并显示提示, 而不是留一片空白: 否则首次打开编辑器
        /// 会看到三个空栏, 无法判断是没有关卡还是加载失败。
        /// </remarks>
        private void RefreshLevelPane()
        {
            if (_listHeaderLabel != null)
                _listHeaderLabel.text = $"关卡 ({_levels.Count})";
            if (_listEmptyHint != null)
                _listEmptyHint.style.display = _levels.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_levelList != null)
                _levelList.style.display = _levels.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>处理关卡列表选中项变化。</summary>
        /// <param name="indices">新选中的下标集合。</param>
        private void OnLevelSelected(IEnumerable<int> indices)
        {
            int index = -1;
            if (indices != null)
            {
                foreach (int candidate in indices)
                {
                    index = candidate;
                    break;
                }
            }
            if (index < 0 || index >= _levels.Count)
                return;
            if (_levels[index] == _current)
                return;
            if (!ConfirmDiscardChanges())
            {
                _levelList.selectedIndex = _levels.IndexOf(_current);
                return;
            }
            ApplyCurrent(_levels[index]);
        }

        /// <summary>把指定关卡设为当前编辑对象并刷新界面。</summary>
        /// <param name="data">关卡源数据; 为 null 时清空编辑区。</param>
        /// <remarks>
        /// 视口取景优先沿用保存值, 但两种情况改为缩放到适应: 从未保存过取景（新建关卡首次打开）,
        /// 以及保存的取景看不到完整世界边界（关卡尺寸变大后, 旧取景会把整个关卡留在视野之外）。
        /// 否则打开关卡会停在空视野上, 表现为"缩放与平移都没反应"。
        /// </remarks>
        private void ApplyCurrent(LevelAuthoringData data)
        {
            _current = data;
            _isDirty = false;
            _controller = data?.Definition == null ? null : new LevelEditController(data);
            _viewport.SetData(data);
            bool appliedSavedView = _viewport.ApplyViewState(data?.EditorViewState);
            if (data?.Definition != null && (!appliedSavedView || !_viewport.IsContentVisible))
                _viewport.ZoomToFitContent();
            RebuildLevelSection();
            RebuildInspector();
            RefreshValidation();
            RefreshDirtyLabel();
        }

        /// <summary>创建新关卡源数据并写入磁盘。</summary>
        private void OnCreateLevel()
        {
            if (!ConfirmDiscardChanges())
                return;
            string levelId = PromptForNewLevelId();
            if (string.IsNullOrWhiteSpace(levelId))
                return;
            var levelIdValue = new LevelId(levelId);
            if (_repository.TryLoad(levelIdValue, out _))
            {
                EditorUtility.DisplayDialog("关卡已存在", $"关卡 {levelId} 已存在。", "确定");
                return;
            }
            LevelAuthoringData created = LevelAuthoringFactory.CreateNew(levelId);
            Result saved = _repository.Save(levelIdValue, created);
            if (!saved.IsSuccess)
            {
                SetStatus("新建失败: " + saved.Message);
                return;
            }
            AssetDatabase.Refresh();
            ReloadLevels();
            SelectLevelById(levelId);
            SetStatus($"已新建关卡 {levelId}");
        }

        /// <summary>保存当前关卡到磁盘。</summary>
        private void OnSaveLevel()
        {
            if (_current?.Definition == null)
                return;
            string levelId = _current.Definition.LevelId;
            if (string.IsNullOrWhiteSpace(levelId))
            {
                SetStatus("保存失败: 关卡 ID 不能为空");
                return;
            }
            _viewport.CaptureViewState(_current.EditorViewState);
            _current.ContentRevision++;
            Result saved = _repository.Save(new LevelId(levelId), _current);
            if (!saved.IsSuccess)
            {
                SetStatus("保存失败: " + saved.Message);
                return;
            }
            AssetDatabase.Refresh();
            _isDirty = false;
            RefreshDirtyLabel();
            SetStatus($"已保存 {levelId} (修订 {_current.ContentRevision})");
        }

        /// <summary>弹出输入框询问新关卡稳定 ID。</summary>
        /// <returns>输入的关卡 ID; 取消时为空。</returns>
        private static string PromptForNewLevelId()
        {
            return PromptWindow.Show("新建关卡", "关卡稳定 ID (如 official.level.test_01_01)", "official.level.");
        }

        /// <summary>按关卡稳定 ID 选中列表项。</summary>
        /// <param name="levelId">关卡稳定 ID。</param>
        private void SelectLevelById(string levelId)
        {
            int index = _levels.FindIndex(data =>
                string.Equals(data.Definition?.LevelId, levelId, StringComparison.Ordinal)
            );
            if (index >= 0)
                _levelList.selectedIndex = index;
        }

        /// <summary>对视口变化做出响应: 保存视口状态, 但不标记脏。</summary>
        /// <remarks>
        /// 平移、缩放与选中都写进 <see cref="EditorViewStateData"/>, 但它们属于编辑器辅助状态,
        /// 不参与内容编译（见该类型的说明）。若把它们算作关卡修改, 打开关卡后
        /// <c>FocusOnContent</c> 会立刻把窗口标成"未保存", 于是每次打开都会弹放弃确认、
        /// 每次保存都会写入实际未变的内容, 脏标记也就失去指示作用。
        /// 视口状态改为在显式保存时顺带写入。
        /// </remarks>
        private void OnViewportChanged()
        {
            if (_current == null)
                return;
            _viewport.CaptureViewState(_current.EditorViewState);
        }

        /// <summary>对视口选中变化做出响应, 重建属性面板。</summary>
        /// <param name="selection">新的选中项。</param>
        private void OnViewportSelectionChanged(LevelSelection selection) => RebuildInspector();

        /// <summary>执行一次编辑操作并按结果刷新界面。</summary>
        /// <param name="operation">执行编辑的委托; 返回操作结果。</param>
        /// <param name="refreshSelectionPanel">是否重建"选中项"分区。</param>
        /// <remarks>
        /// 成功时标记脏状态并同步视口与校验面板; 失败时只把消息写到状态栏, 不改变脏状态,
        /// 使"被拒绝的编辑"不会伪装成已修改。
        /// <para>
        /// <paramref name="refreshSelectionPanel"/> 只在编辑改变了选中项集合时才需要:
        /// 改动单个字段（位置、旋转、参数值）时重建分区会销毁正在编辑的输入框, 使连续输入中断;
        /// 新增或删除对象、区域、参数时才需要重建。
        /// </para>
        /// </remarks>
        private void ApplyEdit(Func<Result> operation, bool refreshSelectionPanel = false)
        {
            if (_controller == null || operation == null)
                return;
            Result result = operation();
            if (!result.IsSuccess)
            {
                SetStatus("操作被拒绝: " + result.Message);
                return;
            }
            MarkDirty();
            _viewport.SetData(_current);
            if (refreshSelectionPanel)
                RebuildInspector();
            RefreshLevelStats();
            RefreshValidation();
            SetStatus(string.Empty);
        }

        /// <summary>响应视口拖动: 把拖动映射为对应编辑操作。</summary>
        /// <param name="update">本帧拖动更新。</param>
        /// <remarks>
        /// 区域整体拖动使用绝对坐标重算而不是逐帧累加位移, 避免多次增量累加产生漂移;
        /// 因此这里用 <see cref="ViewportDragUpdate.Delta"/> 之外的绝对位置配合拖动起点快照。
        /// </remarks>
        private void OnViewportItemDragged(ViewportDragUpdate update)
        {
            if (_controller == null || update.Selection.IsNone)
                return;
            switch (update.Selection.Kind)
            {
                case LevelSelectionKind.StageObject:
                    ApplyEdit(() => _controller.SetObjectPosition(update.Selection.ObjectId, update.Position));
                    break;
                case LevelSelectionKind.SpawnPoint:
                    ApplyEdit(() =>
                        _controller.SetStartPoint(update.Position, _controller.Definition.StartPoint?.RotationZ ?? 0f)
                    );
                    break;
                case LevelSelectionKind.GoalPoint:
                    ApplyEdit(() =>
                        _controller.SetGoalPoint(
                            update.Position,
                            _controller.Definition.GoalPoint?.Width ?? 1f,
                            _controller.Definition.GoalPoint?.Height ?? 1f
                        )
                    );
                    break;
                case LevelSelectionKind.ZoneVertex:
                    ApplyEdit(() =>
                        _controller.SetZoneVertex(
                            update.Selection.ZoneKind,
                            update.Selection.ZoneIndex,
                            update.Selection.VertexIndex,
                            update.Position
                        )
                    );
                    break;
                case LevelSelectionKind.Zone:
                    ApplyEdit(() =>
                        _controller.TranslateZone(update.Selection.ZoneKind, update.Selection.ZoneIndex, update.Delta)
                    );
                    break;
                case LevelSelectionKind.WorldBoundsCorner:
                    ApplyEdit(() => _controller.MoveWorldBoundsCorner(update.Selection.Corner, update.Position));
                    break;
                default:
                    break;
            }
        }

        /// <summary>响应视口删除请求。</summary>
        /// <param name="selection">待删除的选中项。</param>
        private void OnViewportDeleteRequested(LevelSelection selection)
        {
            if (_controller == null)
                return;
            switch (selection.Kind)
            {
                case LevelSelectionKind.StageObject:
                    ApplyEdit(() => _controller.RemoveObject(selection.ObjectId), true);
                    break;
                case LevelSelectionKind.Zone:
                    ApplyEdit(() => _controller.RemoveZone(selection.ZoneKind, selection.ZoneIndex), true);
                    break;
                case LevelSelectionKind.ZoneVertex:
                    ApplyEdit(
                        () =>
                            _controller.RemoveZoneVertex(
                                selection.ZoneKind,
                                selection.ZoneIndex,
                                selection.VertexIndex
                            ),
                        true
                    );
                    break;
                default:
                    SetStatus("该目标不能被删除。");
                    break;
            }
        }

        /// <summary>响应视口双击区域边请求, 在边上插入顶点。</summary>
        /// <param name="request">插入请求。</param>
        private void OnViewportVertexInsertRequested(ViewportVertexInsertRequest request)
        {
            if (_controller == null)
                return;
            LevelSelection inserted = LevelSelection.None;
            ApplyEdit(
                () =>
                {
                    Result result = _controller.InsertZoneVertex(
                        request.ZoneKind,
                        request.ZoneIndex,
                        request.InsertIndex,
                        request.Position,
                        out LevelSelection selection
                    );
                    inserted = selection;
                    return result;
                },
                true
            );
            if (!inserted.IsNone)
                SelectInViewport(inserted);
        }

        /// <summary>在当前视口中心新增一个矩形区域。</summary>
        /// <param name="kind">区域种类。</param>
        private void AddZoneAtViewCenter(ZoneKind kind)
        {
            if (_controller == null)
            {
                SetStatus("请先选择或新建一个关卡。");
                return;
            }
            Vector2 center = _viewport.ViewCenter;
            const float halfWidth = 4f;
            const float halfHeight = 3f;
            LevelSelection created = LevelSelection.None;
            ApplyEdit(
                () =>
                {
                    Result result = _controller.AddRectZone(
                        kind,
                        center.x - halfWidth,
                        center.y - halfHeight,
                        center.x + halfWidth,
                        center.y + halfHeight,
                        out LevelSelection selection
                    );
                    created = selection;
                    return result;
                },
                true
            );
            if (!created.IsNone)
                SelectInViewport(created);
        }

        /// <summary>按调色板条目在视口中心放置起点、终点或官方对象。</summary>
        /// <param name="entry">调色板条目。</param>
        private void AddPaletteEntryAtViewCenter(PaletteEntry entry)
        {
            if (entry == null)
                return;
            if (_controller == null)
            {
                SetStatus("请先选择或新建一个关卡。");
                return;
            }
            switch (entry.Kind)
            {
                case PaletteEntryKind.SpawnPoint:
                    ApplyEdit(() => _controller.SetStartPoint(_viewport.ViewCenter, 0f), true);
                    SelectInViewport(LevelSelection.SpawnPoint());
                    break;
                case PaletteEntryKind.GoalPoint:
                    ApplyEdit(() => _controller.SetGoalPoint(_viewport.ViewCenter, 2f, 2f), true);
                    SelectInViewport(LevelSelection.GoalPoint());
                    break;
                default:
                    LevelSelection created = LevelSelection.None;
                    ApplyEdit(
                        () =>
                        {
                            Result result = _controller.AddObject(
                                entry.PrefabId,
                                _viewport.ViewCenter,
                                out LevelSelection value
                            );
                            created = value;
                            return result;
                        },
                        true
                    );
                    if (!created.IsNone)
                        SelectInViewport(created);
                    break;
            }
        }

        /// <summary>重建"关卡"分区: 只读标识、可编辑元数据与内容统计。</summary>
        /// <remarks>
        /// 关卡 ID 是内容稳定标识, 会进入存档与解锁记录, 因此只读; 改 ID 等价于新建关卡。
        /// 本方法只在切换关卡时调用, 元数据编辑走 <see cref="Mutate"/>, 否则每次按键都会丢掉输入焦点。
        /// </remarks>
        private void RebuildLevelSection()
        {
            if (_levelContainer == null)
                return;
            _levelContainer.Clear();
            LevelDefinition definition = _current?.Definition;
            if (definition == null)
            {
                _levelContainer.Add(new Label("未打开关卡。"));
                return;
            }

            _levelIdLabel = new Label(definition.LevelId ?? string.Empty);
            _levelIdLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _levelContainer.Add(MakeCaptionRow("关卡 ID", _levelIdLabel));

            _mapIdField = new TextField("所属地图");
            _mapIdField.value = definition.MapId ?? string.Empty;
            _mapIdField.RegisterValueChangedCallback(evt => Mutate(d => d.MapId = evt.newValue));
            _levelContainer.Add(_mapIdField);

            _displayNameKeyField = new TextField("显示名称键");
            _displayNameKeyField.value = definition.DisplayNameKey ?? string.Empty;
            _displayNameKeyField.RegisterValueChangedCallback(evt => Mutate(d => d.DisplayNameKey = evt.newValue));
            _levelContainer.Add(_displayNameKeyField);

            _capacityField = new IntegerField("容量上限");
            _capacityField.value = definition.CapacityLimit;
            _capacityField.RegisterValueChangedCallback(OnCapacityChanged);
            _levelContainer.Add(_capacityField);

            _levelStatsLabel = new Label();
            _levelStatsLabel.style.fontSize = 10f;
            _levelStatsLabel.style.color = new Color(0.6f, 0.63f, 0.7f);
            _levelStatsLabel.style.marginTop = 2f;
            _levelContainer.Add(_levelStatsLabel);
            RefreshLevelStats();
        }

        /// <summary>处理容量上限变化; 负容量无意义, 夹取到零并回写显示值。</summary>
        /// <param name="evt">数值变化事件。</param>
        private void OnCapacityChanged(ChangeEvent<int> evt)
        {
            int clamped = Mathf.Max(0, evt.newValue);
            if (clamped != evt.newValue && _capacityField != null)
                _capacityField.SetValueWithoutNotify(clamped);
            Mutate(d => d.CapacityLimit = clamped);
        }

        /// <summary>刷新"关卡"分区的内容统计行。</summary>
        private void RefreshLevelStats()
        {
            if (_levelStatsLabel == null)
                return;
            LevelDefinition definition = _current?.Definition;
            if (definition == null)
            {
                _levelStatsLabel.text = string.Empty;
                return;
            }
            int vertexCount = CountZoneVertices(definition.DeployableZones);
            vertexCount += CountZoneVertices(definition.ForbiddenZones);
            _levelStatsLabel.text =
                $"修订 {_current.ContentRevision} · 对象 {definition.Objects?.Count ?? 0}"
                + $" · 区域顶点 {vertexCount} · 容量 {definition.CapacityLimit}";
        }

        /// <summary>统计一组合法区域的顶点总数。</summary>
        /// <param name="zones">区域集合; 可为 null。</param>
        /// <returns>顶点总数。</returns>
        private static int CountZoneVertices(List<ZoneData> zones)
        {
            if (zones == null)
                return 0;
            int total = 0;
            foreach (ZoneData zone in zones)
                total += zone?.Vertices?.Count ?? 0;
            return total;
        }

        /// <summary>创建一行"标题 + 内容"的只读信息行。</summary>
        /// <param name="caption">左侧标题。</param>
        /// <param name="content">右侧内容元素; 会随行宽拉伸。</param>
        /// <returns>信息行元素。</returns>
        private static VisualElement MakeCaptionRow(string caption, VisualElement content)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var label = new Label(caption);
            label.style.width = 70f;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            content.style.flexGrow = 1f;
            row.Add(label);
            row.Add(content);
            return row;
        }

        /// <summary>刷新校验分区; 无问题时显示通过提示。</summary>
        /// <remarks>
        /// 每条问题都是按钮, 点击后把视口选中项跳到对应目标, 与开发计划 C31A 的
        /// "从编辑器错误跳到对应配置"一致。
        /// </remarks>
        private void RefreshValidation()
        {
            if (_validationContainer == null)
                return;
            _validationContainer.Clear();
            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(_current);
            if (_current?.Definition == null)
            {
                _validationContainer.Add(new Label("未打开关卡, 无校验结果。"));
                return;
            }
            if (LevelEditValidator.IsValid(issues))
            {
                var ok = new Label("✔ 校验通过");
                ok.style.color = new Color(0.5f, 0.85f, 0.5f);
                _validationContainer.Add(ok);
                return;
            }
            var header = new Label($"✖ 校验发现 {issues.Count} 个问题");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(1f, 0.6f, 0.5f);
            _validationContainer.Add(header);
            foreach (LevelValidationIssue issue in issues)
            {
                LevelSelection target = issue.Target;
                var button = new Button(() => SelectInViewport(target)) { text = "→ " + issue.Message };
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.whiteSpace = WhiteSpace.Normal;
                button.style.marginBottom = 1f;
                _validationContainer.Add(button);
            }
        }

        /// <summary>在原位修改当前关卡数据并标记脏状态。</summary>
        /// <param name="mutate">修改委托。</param>
        /// <remarks>
        /// 只刷新统计与校验, 不重建"关卡"分区: 重建会销毁正在输入的字段, 使光标在每次按键后丢失。
        /// 容量上限会立即参与校验（放置费用不得超过容量）, 所以校验必须跟着刷新。
        /// </remarks>
        private void Mutate(Action<LevelDefinition> mutate)
        {
            if (_current?.Definition == null)
                return;
            mutate(_current.Definition);
            MarkDirty();
            RefreshLevelStats();
            RefreshValidation();
        }

        /// <summary>标记当前关卡存在未保存修改。</summary>
        private void MarkDirty()
        {
            if (_current == null)
                return;
            _isDirty = true;
            _current.EditorViewState.IsDirty = true;
            RefreshDirtyLabel();
        }

        /// <summary>刷新标题栏的脏标记显示。</summary>
        private void RefreshDirtyLabel()
        {
            if (_dirtyLabel == null)
                return;
            _dirtyLabel.text = _isDirty ? "● 未保存" : string.Empty;
        }

        /// <summary>重建底部属性面板, 显示并可编辑当前选中项。</summary>
        private void RebuildInspector()
        {
            if (_inspectorContainer == null)
                return;
            _inspectorContainer.Clear();
            LevelDefinition definition = _current?.Definition;
            if (definition == null)
            {
                _inspectorContainer.Add(new Label("请选择或新建一个关卡。"));
                return;
            }
            var header = new Label("选中项");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            _inspectorContainer.Add(header);

            LevelSelection selected = _viewport?.Selection ?? LevelSelection.None;
            switch (selected.Kind)
            {
                case LevelSelectionKind.StageObject:
                    BuildObjectInspector(definition, selected);
                    break;
                case LevelSelectionKind.SpawnPoint:
                    BuildSpawnInspector(definition);
                    break;
                case LevelSelectionKind.GoalPoint:
                    BuildGoalInspector(definition);
                    break;
                case LevelSelectionKind.ZoneVertex:
                    BuildZoneVertexInspector(selected);
                    break;
                case LevelSelectionKind.Zone:
                    BuildZoneInspector(selected);
                    break;
                case LevelSelectionKind.WorldBoundsCorner:
                    BuildWorldBoundsInspector(definition);
                    break;
                default:
                    _inspectorContainer.Add(
                        new Label("(未选中) 在「添加」分区新增区域或对象, 或点击视口中的已有内容。")
                    );
                    break;
            }
        }

        /// <summary>构建静态对象的属性面板。</summary>
        /// <param name="definition">关卡定义。</param>
        /// <param name="selection">对象选中项。</param>
        private void BuildObjectInspector(LevelDefinition definition, LevelSelection selection)
        {
            if (_controller == null || !_controller.TryGetObject(selection.ObjectId, out StageObjectData target))
            {
                _inspectorContainer.Add(new Label("选中的对象已不存在。"));
                return;
            }
            _inspectorContainer.Add(new Label($"对象 ID: {target.ObjectId}"));
            _inspectorContainer.Add(new Label($"预制体: {LevelPaletteCatalog.GetDisplayName(target.PrefabId)}"));
            _inspectorContainer.Add(
                MakeVector2Field(
                    "位置",
                    target.PositionX,
                    target.PositionY,
                    (x, y) => ApplyEdit(() => _controller.SetObjectPosition(target.ObjectId, new Vector2(x, y)))
                )
            );
            _inspectorContainer.Add(
                MakeFloatField(
                    "旋转(°)",
                    target.RotationZ,
                    value => ApplyEdit(() => _controller.SetObjectRotation(target.ObjectId, value))
                )
            );
            _inspectorContainer.Add(
                MakeVector2Field(
                    "缩放",
                    target.ScaleX,
                    target.ScaleY,
                    (x, y) => ApplyEdit(() => _controller.SetObjectScale(target.ObjectId, x, y))
                )
            );
            BuildParameterEditor(target);
        }

        /// <summary>构建对象白名单参数的编辑器。</summary>
        /// <param name="target">目标对象。</param>
        private void BuildParameterEditor(StageObjectData target)
        {
            var header = new Label("白名单参数");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 4f;
            _inspectorContainer.Add(header);
            if (target.Parameters == null || target.Parameters.Count == 0)
            {
                _inspectorContainer.Add(new Label("(无参数)"));
            }
            else
            {
                foreach (ParameterData parameter in target.Parameters)
                {
                    ParameterData captured = parameter;
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    var key = new TextField { value = captured.Key };
                    key.style.flexGrow = 1f;
                    key.RegisterValueChangedCallback(evt =>
                    {
                        string next = evt.newValue;
                        ApplyEdit(() =>
                        {
                            // 改键名等价于删除旧键再写入新键, 保持键唯一。
                            Result removed = _controller.RemoveObjectParameter(target.ObjectId, captured.Key);
                            if (!removed.IsSuccess)
                                return removed;
                            Result added = _controller.SetObjectParameter(target.ObjectId, next, captured.Value);
                            if (added.IsSuccess)
                                captured.Key = next;
                            return added;
                        });
                    });
                    var value = new TextField { value = captured.Value };
                    value.style.flexGrow = 1f;
                    value.RegisterValueChangedCallback(evt =>
                        ApplyEdit(() => _controller.SetObjectParameter(target.ObjectId, captured.Key, evt.newValue))
                    );
                    var remove = new Button(() =>
                        ApplyEdit(() => _controller.RemoveObjectParameter(target.ObjectId, captured.Key), true)
                    )
                    {
                        text = "×",
                    };
                    row.Add(key);
                    row.Add(value);
                    row.Add(remove);
                    _inspectorContainer.Add(row);
                }
            }
            _inspectorContainer.Add(
                MakeButton(
                    "新增参数",
                    () =>
                    {
                        string key = PromptForParameterKey();
                        if (string.IsNullOrWhiteSpace(key))
                            return;
                        ApplyEdit(() => _controller.SetObjectParameter(target.ObjectId, key, string.Empty), true);
                    }
                )
            );
        }

        /// <summary>构建起点属性面板。</summary>
        /// <param name="definition">关卡定义。</param>
        private void BuildSpawnInspector(LevelDefinition definition)
        {
            if (_controller == null)
                return;
            if (definition.StartPoint == null)
            {
                _inspectorContainer.Add(new Label("当前关卡没有起点。"));
                _inspectorContainer.Add(
                    MakeButton(
                        "在此创建起点",
                        () => ApplyEdit(() => _controller.SetStartPoint(_viewport.ViewCenter, 0f), true)
                    )
                );
                return;
            }
            SpawnPointData start = definition.StartPoint;
            _inspectorContainer.Add(
                MakeVector2Field(
                    "位置",
                    start.PositionX,
                    start.PositionY,
                    (x, y) => ApplyEdit(() => _controller.SetStartPoint(new Vector2(x, y), start.RotationZ))
                )
            );
            _inspectorContainer.Add(
                MakeFloatField(
                    "朝向(°)",
                    start.RotationZ,
                    value =>
                        ApplyEdit(() => _controller.SetStartPoint(new Vector2(start.PositionX, start.PositionY), value))
                )
            );
            _inspectorContainer.Add(
                MakeButton("删除起点", () => ApplyEdit(() => _controller.RemoveStartPoint(), true))
            );
        }

        /// <summary>构建终点属性面板。</summary>
        /// <param name="definition">关卡定义。</param>
        private void BuildGoalInspector(LevelDefinition definition)
        {
            if (_controller == null)
                return;
            if (definition.GoalPoint == null)
            {
                _inspectorContainer.Add(new Label("当前关卡没有终点。"));
                _inspectorContainer.Add(
                    MakeButton(
                        "在此创建终点",
                        () => ApplyEdit(() => _controller.SetGoalPoint(_viewport.ViewCenter, 2f, 2f), true)
                    )
                );
                return;
            }
            GoalPointData goal = definition.GoalPoint;
            _inspectorContainer.Add(
                MakeVector2Field(
                    "中心",
                    goal.PositionX,
                    goal.PositionY,
                    (x, y) => ApplyEdit(() => _controller.SetGoalPoint(new Vector2(x, y), goal.Width, goal.Height))
                )
            );
            _inspectorContainer.Add(
                MakeVector2Field(
                    "尺寸",
                    goal.Width,
                    goal.Height,
                    (w, h) =>
                        ApplyEdit(() => _controller.SetGoalPoint(new Vector2(goal.PositionX, goal.PositionY), w, h))
                )
            );
            _inspectorContainer.Add(MakeButton("删除终点", () => ApplyEdit(() => _controller.RemoveGoalPoint(), true)));
        }

        /// <summary>构建区域顶点属性面板。</summary>
        /// <param name="selection">顶点选中项。</param>
        private void BuildZoneVertexInspector(LevelSelection selection)
        {
            if (
                _controller == null
                || !_controller.TryGetZone(selection.ZoneKind, selection.ZoneIndex, out ZoneData zone)
            )
            {
                _inspectorContainer.Add(new Label("选中的区域已不存在。"));
                return;
            }
            List<PointData> vertices = zone.Vertices;
            if (vertices == null || selection.VertexIndex >= vertices.Count)
            {
                _inspectorContainer.Add(new Label("选中的顶点已不存在。"));
                return;
            }
            PointData vertex = vertices[selection.VertexIndex];
            _inspectorContainer.Add(new Label($"区域 {zone.ZoneId} 顶点 #{selection.VertexIndex}"));
            _inspectorContainer.Add(
                MakeVector2Field(
                    "坐标",
                    vertex.X,
                    vertex.Y,
                    (x, y) =>
                        ApplyEdit(() =>
                            _controller.SetZoneVertex(
                                selection.ZoneKind,
                                selection.ZoneIndex,
                                selection.VertexIndex,
                                new Vector2(x, y)
                            )
                        )
                )
            );
            _inspectorContainer.Add(
                MakeButton(
                    "删除该顶点",
                    () =>
                        ApplyEdit(
                            () =>
                                _controller.RemoveZoneVertex(
                                    selection.ZoneKind,
                                    selection.ZoneIndex,
                                    selection.VertexIndex
                                ),
                            true
                        )
                )
            );
        }

        /// <summary>构建区域整体属性面板。</summary>
        /// <param name="selection">区域选中项。</param>
        private void BuildZoneInspector(LevelSelection selection)
        {
            if (
                _controller == null
                || !_controller.TryGetZone(selection.ZoneKind, selection.ZoneIndex, out ZoneData zone)
            )
            {
                _inspectorContainer.Add(new Label("选中的区域已不存在。"));
                return;
            }
            string kindName = selection.ZoneKind == ZoneKind.Deployable ? "可部署区" : "禁放区";
            _inspectorContainer.Add(new Label($"{kindName} {zone.ZoneId}"));
            int vertexCount = zone.Vertices?.Count ?? 0;
            _inspectorContainer.Add(new Label($"顶点数: {vertexCount}"));
            _inspectorContainer.Add(new Label("拖动区域体可整体平移; 双击边可插入顶点。"));
            _inspectorContainer.Add(
                MakeButton(
                    "删除区域",
                    () => ApplyEdit(() => _controller.RemoveZone(selection.ZoneKind, selection.ZoneIndex), true)
                )
            );
        }

        /// <summary>构建世界边界属性面板。</summary>
        /// <param name="definition">关卡定义。</param>
        private void BuildWorldBoundsInspector(LevelDefinition definition)
        {
            if (_controller == null || definition.WorldBounds == null)
            {
                _inspectorContainer.Add(new Label("当前关卡没有世界边界。"));
                return;
            }
            BoundsData bounds = definition.WorldBounds;
            _inspectorContainer.Add(new Label("世界边界 (拖动四角手柄调整)"));
            _inspectorContainer.Add(
                MakeVector2Field(
                    "最小点",
                    bounds.MinX,
                    bounds.MinY,
                    (x, y) => ApplyEdit(() => _controller.SetWorldBounds(x, y, bounds.MaxX, bounds.MaxY))
                )
            );
            _inspectorContainer.Add(
                MakeVector2Field(
                    "最大点",
                    bounds.MaxX,
                    bounds.MaxY,
                    (x, y) => ApplyEdit(() => _controller.SetWorldBounds(bounds.MinX, bounds.MinY, x, y))
                )
            );
        }

        /// <summary>创建一个提交式二维向量输入行。</summary>
        /// <param name="label">字段标签。</param>
        /// <param name="x">初始 X 值。</param>
        /// <param name="y">初始 Y 值。</param>
        /// <param name="onCommit">提交回调; 仅在两个分量均为有限值时调用。</param>
        /// <returns>输入行元素。</returns>
        private static VisualElement MakeVector2Field(string label, float x, float y, Action<float, float> onCommit)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var caption = new Label(label);
            caption.style.width = 70f;
            caption.style.unityTextAlign = TextAnchor.MiddleLeft;
            var xField = new FloatField("X") { value = x };
            xField.style.flexGrow = 1f;
            var yField = new FloatField("Y") { value = y };
            yField.style.flexGrow = 1f;
            EventCallback<ChangeEvent<float>> commit = evt =>
            {
                if (evt.target != xField && evt.target != yField)
                    return;
                onCommit(xField.value, yField.value);
            };
            xField.RegisterValueChangedCallback(commit);
            yField.RegisterValueChangedCallback(commit);
            row.Add(caption);
            row.Add(xField);
            row.Add(yField);
            return row;
        }

        /// <summary>创建一个提交式浮点输入行。</summary>
        /// <param name="label">字段标签。</param>
        /// <param name="value">初始值。</param>
        /// <param name="onCommit">提交回调。</param>
        /// <returns>输入行元素。</returns>
        private static VisualElement MakeFloatField(string label, float value, Action<float> onCommit)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            var caption = new Label(label);
            caption.style.width = 70f;
            caption.style.unityTextAlign = TextAnchor.MiddleLeft;
            var field = new FloatField { value = value };
            field.style.flexGrow = 1f;
            field.RegisterValueChangedCallback(evt => onCommit(evt.newValue));
            row.Add(caption);
            row.Add(field);
            return row;
        }

        /// <summary>询问新参数键名。</summary>
        /// <returns>键名; 取消或为空时返回空字符串。</returns>
        private static string PromptForParameterKey() => PromptWindow.Show("新增参数", "参数键名", string.Empty);

        /// <summary>在视口中选中一个编辑目标并同步"选中项"分区。</summary>
        /// <param name="selection">要选中的目标。</param>
        /// <remarks>
        /// <see cref="LevelViewportElement.SetSelection"/> 刻意不触发 <c>SelectionChanged</c>,
        /// 以免与窗口内部的选中处理互相回调; 因此窗口主动选中时必须自行重建属性面板,
        /// 否则会出现"视口已选中但属性栏仍显示旧目标"。
        /// </remarks>
        private void SelectInViewport(LevelSelection selection)
        {
            _viewport.SetSelection(selection);
            RebuildInspector();
        }

        /// <summary>设置底部状态栏文本。</summary>
        /// <param name="message">状态文本。</param>
        private void SetStatus(string message)
        {
            if (_statusLabel != null)
                _statusLabel.text = message;
        }

        /// <summary>在存在未保存修改时询问是否放弃。</summary>
        /// <returns>可以继续当前操作返回 true; 用户取消返回 false。</returns>
        private bool ConfirmDiscardChanges()
        {
            if (!_isDirty || _current?.Definition == null)
                return true;
            bool discard = EditorUtility.DisplayDialog(
                "未保存的修改",
                $"关卡 {_current.Definition.LevelId} 有未保存的修改。是否放弃这些修改?",
                "放弃修改",
                "取消"
            );
            if (discard)
            {
                _isDirty = false;
                _current.EditorViewState.IsDirty = false;
                RefreshDirtyLabel();
            }
            return discard;
        }
    }
}
