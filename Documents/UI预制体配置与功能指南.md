# UI 预制体配置与功能指南

> 本指南描述当前导出器和运行时绑定器的行为。预制体契约不完整时，运行时会回退到代码生成界面；这不是验收通过的状态。

## 目标

运行时脚本定义 UI 的职责、控件契约和事件；预制体只承载画师绘制的层级、尺寸、字体、图片和颜色。这样替换视觉资源不会改变设置保存、页面路由或按钮流程。

运行时入口分别位于 `StartMenuView.cs`、`MetaHubShell.cs`、`SettingsModalPresenter.cs`；预制体类型由 `UiScreenId.cs` 和 `UiPrefabRoot.cs` 定义，控件清单分别位于 `StartMenuUiBindings.cs`、`MetaHubUiBindings.cs` 和 `SettingsUiBindings.cs`，资源登记由 `ContentAssetRegistry` 完成。

## 导出当前 UI

在 Unity 菜单执行 **Game → UI → Export Current UI To Prefabs**。工具会把当前 C# 动态生成结果保存到 `Assets/Game/Prefabs/UI/`，并创建或更新 `Assets/Game/Content/ContentAssetRegistry.asset` 的 UI 预制体条目。该 Registry 就是项目现有官方 Prefab 资源管理入口。

导出或画师替换资源后，执行 **Game → UI → Validate Registered UI Prefabs**。该工具会检查 Registry 中每个 UI 预制体的 `UiPrefabRoot` 标记、界面类型和必需控件绑定，并在 Console 列出契约校验结果。

校验器也会拒绝空的或重复的稳定 ID；稳定 ID 是运行时查找预制体的唯一键，不能按显示名称随意修改。

在 `00_Bootstrap` 的 `GameRoot` 组件上把生成的 Registry 拖到 **Content Asset Registry**。Registry 用于运行时按稳定 ID 加载已配置的 UI 预制体。

## 稳定 ID 与必需节点

预制体根节点使用 `RectTransform`，并带有 `UiPrefabRoot` 契约标记和对应的绑定组件（如 `SettingsUiBindings`）；运行时脚本定义必需控件，子节点名称是脚本与画师之间的契约。节点可以重新排版，也可以替换 Image、字体和材质，但不要删除必需控件或更改名称。

### StartMenuUI（`ui.start-menu`）

`SceneCanvas` 下必须有 `Title`、`Feedback`、`Start`、`Settings`、`Quit`、`NicknameModal`。弹窗下必须有 `Prompt`、`Input`、`Error`、`Cancel`、`Confirm`。

- `Start`：开始或继续档案，触发流程导航。
- `Settings`：打开全局设置弹窗。
- `Quit`：请求退出应用。
- `Input`：昵称输入，确认后由档案服务校验。

### MetaHubUI（`ui.meta-hub`）

`SceneCanvas` 下必须有 `PageTitle`、`Nickname`、`Clock`、`SidebarView/SidebarInfo`、`PageContainer` 和 `FooterView`。页面容器内保留 `MapPageView`、`ArchivePageView`、`CharacterPageView`、`LoungePlaceholderView`；底栏保留 `Map`、`Archive`、`Character`、`Lounge`、`Settings` 按钮。

- 页面按钮只改变页面显隐并异步保存最后页面。
- `Settings` 打开全局设置弹窗；时钟由运行时每秒刷新。

### 地图白盒的状态与资料卡配置

当前地图使用 `OfficialTestMapCatalog.CreateProvider()` 的测试内容。`MetaHubShell` 的 Inspector 字段 **Map Id** 默认是 `official.map.test_01`，该地图包含 `official.level.test_01_01` 到 `official.level.test_01_05`。节点按内容的 `SortOrder` 排序，对应 `MapNode_1` 到 `MapNode_5`，不使用按钮文字作为关卡 ID。

- 修改关卡 ID、数量、顺序、名称 Key 和前置条件：编辑 `Assets/Game/Runtime/Content/OfficialTestMapCatalog.cs`。默认首关无前置，后续关卡要求上一关完成；`UnlockRequirement.Mode` 的 `All` 表示全部前置完成，`Any` 表示任一前置完成，前置列表是 `RequiredLevelIds`。
- 修改关卡名称：配置对应 `DisplayNameKey` 的本地化文本；缺少翻译时显示稳定 Key。节点状态和资料卡提示目前使用中文白盒文字，集中在 `MetaHubShell.MapStateText`、`RenderLevelCard` 中，尚未接入多语言表。
- 修改布局：在 `MetaHubUI.prefab` 中调整 Rect Transform、字体和颜色。保留节点连续命名 `MapNode_1`、`MapNode_2` 等，以及 `MapPageView/LevelCard/Details`、`Start`。新增节点需要同时增加内容记录和预制体按钮。
- 节点状态包括“未解锁、当前关卡、已解锁、已完成”。未解锁节点和没有对应内容的多余节点隐藏；只有当前关卡、已解锁和已完成节点显示并允许选择，进入请求期间暂时禁用交互。隐藏通过停用按钮对象实现，保留节点原位置和排序映射；刷新后满足解锁条件的节点重新显示。若当前选择已失效或变为未解锁，则清空资料卡选择并禁用开始按钮。
- 初始化、打开地图、点击节点和切换语言时，使用当前档案重新生成进度快照并计算状态。选中后显示名称、状态和最佳成绩；没有选中时显示“请选择关卡”。没有实现逐帧监听外部进度变更。

资料卡开始按钮已连接现有 `EnterLevelAsync`：点击时重新校验解锁状态，等待进入期间禁用节点和开始按钮，并阻止重复请求。无需在 Inspector 的 OnClick 中重复绑定。当前流程在本次应用运行中首次进入某个 LevelId 时先播放测试剧情，剧情结束或跳过后进入已有 `04_Gameplay`；同一 LevelId 后续直接进入 Gameplay。该记录仅在内存中，不代表通关或持久化的剧情完成进度。

`04_Gameplay` 当前是无实际玩法的占位场景，已保存 `GameplayCanvas/ReturnToMap` 返回按钮。`SceneUiInstaller` 向按钮上的 `GameplayReturnButton` 注入服务后启用按钮；点击复用 `OpenMetaHubAsync(MetaPageId.Map, ...)` 返回地图，不记录通关或解锁。修改位置和尺寸时调整场景中按钮的 Rect Transform，修改文字时编辑其 Label；无需添加 Inspector OnClick。单独运行 Gameplay 未注入服务时按钮保持禁用。

通关完成事实写入存档和实时解锁通知仍待后续接入。运行时应从 `00_Bootstrap` 开始，单独运行 MetaHub 不会自动加载档案或注入服务。验证入口时，从地图选择已解锁节点并点击开始，完成测试剧情或跳过后，确认已进入 `04_Gameplay`，再点击返回地图。

### SettingsModalUI（`ui.settings-modal`）

`SettingsPanel` 下必须保留 `Title`、`ui.master_volume`、`ui.music_volume`、`ui.sfx_volume` 三个 Slider 及同名 `Label`，以及 `Language`、`Resolution`、`Fullscreen`、`Feedback`、`RestoreDefaults`、`Cancel`、`Apply`。

- 三个 Slider 的范围必须是 0～1，分别对应主音量、音乐和音效。
- `SettingsUiBindings.IsComplete` 会校验三个 Slider 的范围和所有必需控件引用。
- `Language` 当前包含 `zh-CN`、`en-US`；`Resolution` 由运行时设备分辨率填充。
- `Language` 和 `Resolution` 的 `Template/Content` 使用 Dropdown 自带的选项排版，不添加 `VerticalLayoutGroup` 或 `ContentSizeFitter`。
- `Template/Viewport` 的 Image 保持不透明，Mask 关闭 **Show Mask Graphic**，以便隐藏遮罩图形并正常裁切选项文字。
- `Apply` 校验并持久化草稿，成功后关闭；`Cancel` 丢弃草稿；`RestoreDefaults` 只恢复当前草稿。

## 配置检查

进入 Play 后，`GameRoot` 会按 Registry 的稳定 ID 实例化 UI 预制体。画师可在保持契约组件和必需控件引用的前提下替换层级中的图片、字体、材质和布局。替换完成后应检查按钮可点击、设置三条滑块均能保存、两个下拉框均能选择、语言切换后文本刷新，并运行 EditMode/PlayMode 测试。

校验器会在 Console 列出每个预制体的契约状态。进入验收前，`UiPrefabRoot` 界面类型、稳定 ID 和对应 Bindings 的必需控件清单应全部配置完成。

导出器会把现有占位 uGUI 快照转成 Prefab，用作画师配置正式 UI 资源的基础结构。
