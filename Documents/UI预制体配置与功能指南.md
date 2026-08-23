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
