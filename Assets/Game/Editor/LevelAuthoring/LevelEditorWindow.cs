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

        private readonly List<LevelAuthoringData> _levels = new List<LevelAuthoringData>();
        private ILevelAuthoringRepository _repository;
        private LevelEditController _controller;
        private ListView _levelList;
        private LevelViewportElement _viewport;
        private TextField _levelIdField;
        private TextField _mapIdField;
        private TextField _displayNameKeyField;
        private IntegerField _capacityField;
        private Label _statusLabel;
        private Label _dirtyLabel;
        private VisualElement _inspectorContainer;
        private VisualElement _validationContainer;
        private LevelAuthoringData _current;
        private bool _isDirty;

        /// <summary>打开关卡编辑器窗口。</summary>
        [MenuItem("Game/Level Editor Window")]
        public static void Open() => GetWindow<LevelEditorWindow>("Level Editor");

        /// <summary>创建窗口界面骨架并加载关卡列表。</summary>
        public void CreateGUI()
        {
            _repository = new FileLevelAuthoringRepository(GetLevelsRootPath());
            VisualElement root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            root.Add(BuildToolbar());
            root.Add(BuildEditBar());
            var split = new TwoPaneSplitView(0, 260f, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1f;
            split.Add(BuildLevelPane());
            split.Add(BuildEditorPane());
            root.Add(split);
            root.Add(BuildStatusBar());

            ReloadLevels();
        }

        /// <summary>构建编辑工具栏: 区域增删与官方对象调色板。</summary>
        /// <returns>编辑工具栏元素。</returns>
        /// <remarks>
        /// 新增对象落在视口中心, 随后可直接拖动; 这样无需实现拖放式调色板也能完成放置。
        /// </remarks>
        private VisualElement BuildEditBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.paddingLeft = 6f;
            bar.style.paddingRight = 6f;
            bar.style.paddingBottom = 4f;
            bar.Add(new Label("区域"));
            bar.Add(MakeButton("+可部署区", () => AddZoneAtViewCenter(ZoneKind.Deployable)));
            bar.Add(MakeButton("+禁放区", () => AddZoneAtViewCenter(ZoneKind.Forbidden)));
            bar.Add(new Label("  对象"));
            foreach (PaletteEntry entry in LevelPaletteCatalog.GetEntries())
            {
                PaletteEntry captured = entry;
                bar.Add(MakeButton("+" + captured.DisplayName, () => AddPaletteEntryAtViewCenter(captured)));
            }
            bar.Add(new Label("  "));
            bar.Add(MakeButton("校验", RefreshValidation));
            return bar;
        }

        /// <summary>构建顶部工具栏: 新建、保存、刷新、定位。</summary>
        /// <returns>工具栏元素。</returns>
        private VisualElement BuildToolbar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.paddingLeft = 6f;
            bar.style.paddingRight = 6f;
            bar.style.paddingTop = 4f;
            bar.style.paddingBottom = 4f;
            bar.Add(MakeButton("新建关卡", OnCreateLevel));
            bar.Add(MakeButton("保存", OnSaveLevel));
            bar.Add(MakeButton("重新加载", ReloadLevels));
            bar.Add(MakeButton("定位到内容", () => _viewport?.FocusOnContent()));
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
            pane.style.borderRightWidth = 1f;
            pane.style.borderRightColor = new Color(0.2f, 0.2f, 0.2f);
            var title = new Label("关卡列表");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.paddingLeft = 6f;
            title.style.paddingTop = 4f;
            pane.Add(title);

            _levelList = new ListView(_levels, 20, MakeLevelRow, BindLevelRow);
            _levelList.selectionType = SelectionType.Single;
            _levelList.style.flexGrow = 1f;
            _levelList.selectedIndicesChanged += OnLevelSelected;
            pane.Add(_levelList);
            return pane;
        }

        /// <summary>构建右侧编辑面板: 元数据字段与 2D 视口。</summary>
        /// <returns>编辑面板元素。</returns>
        private VisualElement BuildEditorPane()
        {
            var pane = new VisualElement();
            pane.style.flexDirection = FlexDirection.Column;
            pane.style.flexGrow = 1f;

            var metadata = new VisualElement();
            metadata.style.paddingLeft = 6f;
            metadata.style.paddingRight = 6f;
            metadata.style.paddingTop = 4f;
            _levelIdField = new TextField("关卡 ID");
            _levelIdField.SetEnabled(false);
            _mapIdField = new TextField("所属地图");
            _mapIdField.RegisterValueChangedCallback(evt => Mutate(d => d.MapId = evt.newValue));
            _displayNameKeyField = new TextField("显示名称键");
            _displayNameKeyField.RegisterValueChangedCallback(evt => Mutate(d => d.DisplayNameKey = evt.newValue));
            _capacityField = new IntegerField("容量上限");
            _capacityField.RegisterValueChangedCallback(evt => Mutate(d => d.CapacityLimit = evt.newValue));
            metadata.Add(_levelIdField);
            metadata.Add(_mapIdField);
            metadata.Add(_displayNameKeyField);
            metadata.Add(_capacityField);
            pane.Add(metadata);

            _viewport = new LevelViewportElement();
            _viewport.style.flexGrow = 1f;
            _viewport.style.backgroundColor = new Color(0.13f, 0.13f, 0.15f);
            _viewport.style.marginLeft = 6f;
            _viewport.style.marginRight = 6f;
            _viewport.style.marginTop = 4f;
            _viewport.style.marginBottom = 4f;
            _viewport.ViewChanged += OnViewportChanged;
            _viewport.SelectionChanged += OnViewportSelectionChanged;
            _viewport.ItemDragged += OnViewportItemDragged;
            _viewport.DeleteRequested += OnViewportDeleteRequested;
            _viewport.VertexInsertRequested += OnViewportVertexInsertRequested;
            pane.Add(_viewport);

            _validationContainer = new VisualElement();
            _validationContainer.style.paddingLeft = 6f;
            _validationContainer.style.paddingRight = 6f;
            pane.Add(_validationContainer);

            _inspectorContainer = new VisualElement();
            _inspectorContainer.style.paddingLeft = 6f;
            _inspectorContainer.style.paddingRight = 6f;
            _inspectorContainer.style.paddingBottom = 6f;
            pane.Add(_inspectorContainer);
            return pane;
        }

        /// <summary>构建底部状态栏。</summary>
        /// <returns>状态栏元素。</returns>
        private VisualElement BuildStatusBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.paddingLeft = 6f;
            bar.style.paddingBottom = 3f;
            bar.Add(
                new Label(
                    "左键选中/拖动 · 双击区域边插顶点 · Delete 删除 · 中键或 Alt+左键平移 · 滚轮缩放 · Esc 取消选中"
                )
            );
            _statusLabel = new Label();
            _statusLabel.style.marginLeft = 12f;
            bar.Add(_statusLabel);
            return bar;
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

        /// <summary>创建关卡列表的一行。</summary>
        /// <returns>行元素。</returns>
        private static VisualElement MakeLevelRow()
        {
            var label = new Label();
            label.style.paddingLeft = 4f;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            return label;
        }

        /// <summary>绑定关卡列表行数据。</summary>
        /// <param name="element">行元素。</param>
        /// <param name="index">数据下标。</param>
        private void BindLevelRow(VisualElement element, int index)
        {
            if (element is Label label && index >= 0 && index < _levels.Count)
                label.text = _levels[index].Definition?.LevelId ?? "(无效关卡)";
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
            if (_levels.Count > 0)
                _levelList.selectedIndex = 0;
            else
                ApplyCurrent(null);
            SetStatus($"已加载 {_levels.Count} 个关卡");
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
        private void ApplyCurrent(LevelAuthoringData data)
        {
            _current = data;
            _isDirty = false;
            _controller = data?.Definition == null ? null : new LevelEditController(data);
            LevelDefinition definition = data?.Definition;
            _levelIdField.SetValueWithoutNotify(definition?.LevelId ?? string.Empty);
            _mapIdField.SetValueWithoutNotify(definition?.MapId ?? string.Empty);
            _displayNameKeyField.SetValueWithoutNotify(definition?.DisplayNameKey ?? string.Empty);
            _capacityField.SetValueWithoutNotify(definition?.CapacityLimit ?? 0);
            _viewport.SetData(data);
            _viewport.ApplyViewState(data?.EditorViewState);
            RebuildInspector();
            RefreshValidation();
            RefreshDirtyLabel();
            if (definition != null)
                _viewport.FocusOnContent();
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
        /// <remarks>
        /// 成功时标记脏状态并同步视口与校验面板; 失败时只把消息写到状态栏, 不改变脏状态,
        /// 使"被拒绝的编辑"不会伪装成已修改。
        /// </remarks>
        private void ApplyEdit(Func<Result> operation)
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
            RebuildInspector();
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
                    ApplyEdit(() => _controller.RemoveObject(selection.ObjectId));
                    break;
                case LevelSelectionKind.Zone:
                    ApplyEdit(() => _controller.RemoveZone(selection.ZoneKind, selection.ZoneIndex));
                    break;
                case LevelSelectionKind.ZoneVertex:
                    ApplyEdit(() =>
                        _controller.RemoveZoneVertex(selection.ZoneKind, selection.ZoneIndex, selection.VertexIndex)
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
            ApplyEdit(() =>
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
            });
            if (!inserted.IsNone)
                _viewport.SetSelection(inserted);
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
            ApplyEdit(() =>
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
            });
            if (!created.IsNone)
                _viewport.SetSelection(created);
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
                    ApplyEdit(() => _controller.SetStartPoint(_viewport.ViewCenter, 0f));
                    _viewport.SetSelection(LevelSelection.SpawnPoint());
                    break;
                case PaletteEntryKind.GoalPoint:
                    ApplyEdit(() => _controller.SetGoalPoint(_viewport.ViewCenter, 2f, 2f));
                    _viewport.SetSelection(LevelSelection.GoalPoint());
                    break;
                default:
                    LevelSelection created = LevelSelection.None;
                    ApplyEdit(() =>
                    {
                        Result result = _controller.AddObject(
                            entry.PrefabId,
                            _viewport.ViewCenter,
                            out LevelSelection value
                        );
                        created = value;
                        return result;
                    });
                    if (!created.IsNone)
                        _viewport.SetSelection(created);
                    break;
            }
        }

        /// <summary>刷新校验面板; 无问题时清空显示。</summary>
        private void RefreshValidation()
        {
            if (_validationContainer == null)
                return;
            _validationContainer.Clear();
            IReadOnlyList<LevelValidationIssue> issues = LevelEditValidator.Validate(_current);
            if (LevelEditValidator.IsValid(issues))
            {
                var ok = new Label("校验通过");
                ok.style.color = new Color(0.5f, 0.85f, 0.5f);
                _validationContainer.Add(ok);
                return;
            }
            var header = new Label($"校验发现 {issues.Count} 个问题");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(1f, 0.6f, 0.5f);
            _validationContainer.Add(header);
            foreach (LevelValidationIssue issue in issues)
            {
                LevelSelection target = issue.Target;
                var button = new Button(() => _viewport.SetSelection(target)) { text = "→ " + issue.Message };
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.marginBottom = 1f;
                _validationContainer.Add(button);
            }
        }

        /// <summary>在原位修改当前关卡数据并标记脏状态。</summary>
        /// <param name="mutate">修改委托。</param>
        private void Mutate(Action<LevelDefinition> mutate)
        {
            if (_current?.Definition == null)
                return;
            mutate(_current.Definition);
            MarkDirty();
            RebuildInspector();
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
                    _inspectorContainer.Add(new Label("(未选中) 用上方按钮新增区域或对象, 或点击视口中的已有内容。"));
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
                        ApplyEdit(() => _controller.RemoveObjectParameter(target.ObjectId, captured.Key))
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
                        ApplyEdit(() => _controller.SetObjectParameter(target.ObjectId, key, string.Empty));
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
                        () => ApplyEdit(() => _controller.SetStartPoint(_viewport.ViewCenter, 0f))
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
            _inspectorContainer.Add(MakeButton("删除起点", () => ApplyEdit(() => _controller.RemoveStartPoint())));
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
                        () => ApplyEdit(() => _controller.SetGoalPoint(_viewport.ViewCenter, 2f, 2f))
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
            _inspectorContainer.Add(MakeButton("删除终点", () => ApplyEdit(() => _controller.RemoveGoalPoint())));
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
                        ApplyEdit(() =>
                            _controller.RemoveZoneVertex(selection.ZoneKind, selection.ZoneIndex, selection.VertexIndex)
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
                    () => ApplyEdit(() => _controller.RemoveZone(selection.ZoneKind, selection.ZoneIndex))
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
