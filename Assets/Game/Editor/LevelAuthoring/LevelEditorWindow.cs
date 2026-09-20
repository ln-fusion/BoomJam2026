using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private ListView _levelList;
        private LevelViewportElement _viewport;
        private TextField _levelIdField;
        private TextField _mapIdField;
        private TextField _displayNameKeyField;
        private IntegerField _capacityField;
        private Label _statusLabel;
        private Label _dirtyLabel;
        private VisualElement _inspectorContainer;
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
            var split = new TwoPaneSplitView(0, 260f, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1f;
            split.Add(BuildLevelPane());
            split.Add(BuildEditorPane());
            root.Add(split);
            root.Add(BuildStatusBar());

            ReloadLevels();
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
            pane.Add(_viewport);

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
            bar.Add(new Label("左键拖拽平移 · 滚轮缩放 · 左键点击选中 · Esc 取消选中"));
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
            LevelDefinition definition = data?.Definition;
            _levelIdField.SetValueWithoutNotify(definition?.LevelId ?? string.Empty);
            _mapIdField.SetValueWithoutNotify(definition?.MapId ?? string.Empty);
            _displayNameKeyField.SetValueWithoutNotify(definition?.DisplayNameKey ?? string.Empty);
            _capacityField.SetValueWithoutNotify(definition?.CapacityLimit ?? 0);
            _viewport.SetData(data);
            _viewport.ApplyViewState(data?.EditorViewState);
            RebuildInspector();
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

        /// <summary>对视口变化做出响应, 标记脏状态。</summary>
        private void OnViewportChanged()
        {
            if (_current == null)
                return;
            _viewport.CaptureViewState(_current.EditorViewState);
            MarkDirty();
        }

        /// <summary>对视口选中变化做出响应, 重建属性面板。</summary>
        /// <param name="objectId">新选中的对象稳定 ID。</param>
        private void OnViewportSelectionChanged(string objectId) => RebuildInspector();

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

        /// <summary>重建底部属性面板, 显示当前选中对象的信息。</summary>
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
            var header = new Label("选中对象");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            _inspectorContainer.Add(header);

            string selected = _viewport?.SelectedObjectId ?? string.Empty;
            if (string.IsNullOrEmpty(selected))
            {
                _inspectorContainer.Add(
                    new Label("(未选中) 提示: C20 只提供视口浏览与元数据编辑, 对象增删由 C21/C22 提供。")
                );
                return;
            }
            if (string.Equals(selected, LevelViewportElement.SpawnPointObjectId, StringComparison.Ordinal))
            {
                _inspectorContainer.Add(
                    new Label(
                        $"起点: ({definition.StartPoint?.PositionX:0.###}, {definition.StartPoint?.PositionY:0.###})"
                    )
                );
                _inspectorContainer.Add(new Label($"朝向: {definition.StartPoint?.RotationZ:0.###}°"));
                return;
            }
            if (string.Equals(selected, LevelViewportElement.GoalPointObjectId, StringComparison.Ordinal))
            {
                _inspectorContainer.Add(
                    new Label(
                        $"终点: ({definition.GoalPoint?.PositionX:0.###}, {definition.GoalPoint?.PositionY:0.###})"
                    )
                );
                _inspectorContainer.Add(
                    new Label($"尺寸: {definition.GoalPoint?.Width:0.###} × {definition.GoalPoint?.Height:0.###}")
                );
                return;
            }
            StageObjectData target = definition.Objects?.FirstOrDefault(item =>
                string.Equals(item?.ObjectId, selected, StringComparison.Ordinal)
            );
            if (target == null)
            {
                _inspectorContainer.Add(new Label("选中的对象已不存在。"));
                return;
            }
            _inspectorContainer.Add(new Label($"对象 ID: {target.ObjectId}"));
            _inspectorContainer.Add(new Label($"预制体: {LevelPaletteCatalog.GetDisplayName(target.PrefabId)}"));
            _inspectorContainer.Add(new Label($"位置: ({target.PositionX:0.###}, {target.PositionY:0.###})"));
            _inspectorContainer.Add(new Label($"旋转: {target.RotationZ:0.###}°"));
            _inspectorContainer.Add(new Label($"缩放: {target.ScaleX:0.###} × {target.ScaleY:0.###}"));
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
