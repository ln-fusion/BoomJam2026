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

当前运行时使用 Bootstrap 引用的 `Assets/Game/Content/OfficialContentCatalog.asset`。代码测试目录仅供测试和独立场景兼容预览。`MetaHubShell` 的 Inspector 字段 **Map Id** 默认是 `official.map.test_01`，该地图包含 `official.level.test_01_01` 到 `official.level.test_01_05`。节点按内容的 `SortOrder` 排序，对应 `MapNode_1` 到 `MapNode_5`，不使用按钮文字作为关卡 ID。

- 修改关卡 ID、数量、顺序、名称 Key 和前置条件：在 `OfficialContentCatalog.asset` 的 Levels 列表中编辑。默认首关无前置，后续关卡要求上一关完成；`UnlockRequirement.Mode` 的 `All` 表示全部前置完成，`Any` 表示任一前置完成，前置列表是 `RequiredLevelIds`。
- 修改关卡名称：配置对应 `DisplayNameKey` 的本地化文本；缺少翻译时显示稳定 Key。节点状态、资料卡格式、成绩提示和剧情复播按钮均通过 `Assets/Localization/UI.csv` 配置，执行 **Boom Jam → Localization → Import UI CSV** 后更新运行时 String Table。
- 修改布局：在 `MetaHubUI.prefab` 中调整 Rect Transform、字体和颜色。保留节点连续命名 `MapNode_1`、`MapNode_2` 等，以及 `MapPageView/LevelCard/Details`、`Start`。新增节点需要同时增加内容记录和预制体按钮。
- 节点状态包括“未解锁、当前关卡、已解锁、已完成”。未解锁节点和没有对应内容的多余节点隐藏；只有当前关卡、已解锁和已完成节点显示并允许选择，进入请求期间暂时禁用交互。隐藏通过停用按钮对象实现，保留节点原位置和排序映射；刷新后满足解锁条件的节点重新显示。若当前选择已失效或变为未解锁，则清空资料卡选择并禁用开始按钮。
- 初始化、打开地图、点击节点和切换语言时，使用当前档案重新生成进度快照并计算状态。选中后显示名称、状态和最佳成绩；没有选中时显示“请选择关卡”。没有实现逐帧监听外部进度变更。

资料卡开始按钮已连接现有 `EnterLevelAsync`：点击时重新校验解锁状态，等待进入期间禁用节点和开始按钮，并阻止重复请求。无需在 Inspector 的 OnClick 中重复绑定。每关通过 `PreludeStoryId` 配置独立关前剧情；剧情结束或跳过后先把剧情完成事实写入当前档案，保存成功后进入已有 `04_Gameplay`。同一关卡后续进入时读取 `CompletedStoryIds` 并直接进入 Gameplay，重启应用后仍然有效。

`04_Gameplay` 当前是无实际玩法的占位场景，已保存左上角 `GameplayCanvas/LevelId`、`SimulateSuccess`、`SimulateFailure`、`Settings`，以及默认隐藏的 `SuccessPanel`、`FailurePanel`。`SceneUiInstaller` 向 `GameplayReturnButton` 注入服务后显示当前选关 `LevelId` 并启用交互；字段分别引用关卡文字、模拟按钮、结果面板、面板操作按钮和设置按钮，均由脚本绑定，无需添加 Inspector OnClick。设置弹窗打开时若 Gameplay 正在运行，会保存当前 `Time.timeScale` 并设为 `0`；取消或应用时恢复打开前的值；弹窗左上角按钮在 Gameplay 中显示“返回地图”并前往 MetaHub 地图页，在其他功能场景中仍显示“返回主菜单”。修改位置和尺寸时调整场景中控件的 Rect Transform，修改静态文字时编辑其 Label、Title 或 Message。单独运行 Gameplay 未注入服务时模拟和提交按钮保持禁用。

- **设置中返回地图**：不应用当前设置草稿，恢复暂停前时间速度，调用 `OpenMetaHubAsync(MetaPageId.Map, ...)`，并且不写入完成事实。
- **模拟成功**：只显示 `SuccessPanel` 并禁用成功、失败模拟入口，不立即写档。点击面板内 `SubmitSuccess` 后，使用地图选关时记录的 LevelId 和本次白盒会话提交 ID，重新检查解锁资格，在独立档案副本中更新 `CompletedLevelIds`、对应 `LevelRecords.Completed` 和 `AppliedCompletionRunIds`。使用已有 `SaveReason.ProgressCommitted` 保存成功后才替换当前档案，首次通关播放已配置关后剧情，结束或跳过后返回地图；没有配置或重复通关则直接返回地图。地图依据完成事实和关卡前置条件显示新解锁的关卡。保存失败时留在 Gameplay，保留旧进度并允许再次提交。
- **模拟失败**：只显示 `FailurePanel`，不调用通关提交或档案保存。点击 `Retry` 关闭结果面板并恢复两个模拟入口，继续当前白盒关卡。
- 模拟通关会写入玩家当前档案，但不会伪造最佳成绩、统计或剧情完成记录，关后剧情的完成状态由现有剧情流程另行保存。它仅用于白盒验收，尚不是正式 Gameplay 结算事务。没有经地图选关创建会话时，模拟通关按钮不可用。

运行时应从 `00_Bootstrap` 开始。新档选择第一关并进入 Gameplay 后，从设置返回地图仍只显示第一关；模拟失败并重新尝试也不改变进度。模拟成功后还需点击 `SuccessPanel/SubmitSuccess`，保存成功并返回地图后第一关显示“已完成”，第二关出现且可进入，后续未满足条件的关卡继续隐藏。重启后继续档案，第二关仍应显示。解锁完全由地图前置条件和已保存完成事实计算，不直接把“下一关”写入存档。

### SettingsModalUI（`ui.settings-modal`）

`SettingsPanel` 下必须保留 `Title`、`ui.master_volume`、`ui.music_volume`、`ui.sfx_volume` 三个 Slider 及同名 `Label`，以及 `Language`、`Resolution`、`Fullscreen`、`Feedback`、`RestoreDefaults`、`Cancel`、`Apply`。

- 三个 Slider 的范围必须是 0～1，分别对应主音量、音乐和音效。
- `SettingsUiBindings.IsComplete` 会校验三个 Slider 的范围和所有必需控件引用。
- `Language` 当前包含 `zh-CN`、`en-US`；`Resolution` 由运行时设备分辨率填充。
- `Language` 和 `Resolution` 的 `Template/Content` 使用 Dropdown 自带的选项排版，不添加 `VerticalLayoutGroup` 或 `ContentSizeFitter`。
- `Template/Viewport` 的 Image 保持不透明，Mask 关闭 **Show Mask Graphic**，以便隐藏遮罩图形并正常裁切选项文字。
- `Apply` 校验并持久化草稿，成功后关闭；`Cancel` 丢弃草稿；`RestoreDefaults` 只恢复当前草稿。

设置面板左上角复用同一个 `SettingsPanel/ReturnToStartMenu` 按钮。在主菜单中打开设置时隐藏；Gameplay 中运行时文字改为“返回地图”并打开 MetaHub 地图页；其他功能场景中显示“返回主菜单”并调用已有主菜单流程。两种行为都会丢弃尚未应用的设置草稿，且不记录关卡完成事实。应用设置或恢复默认操作进行中暂时禁用该按钮。位置和尺寸在该节点的 Rect Transform 调整；运行时文案由 `SettingsModalPresenter` 根据场景覆盖，本地化留待后续文案整理。无需在 Inspector OnClick 中重复接线。

## 配置检查

进入 Play 后，`GameRoot` 会按 Registry 的稳定 ID 实例化 UI 预制体。画师可在保持契约组件和必需控件引用的前提下替换层级中的图片、字体、材质和布局。替换完成后应检查按钮可点击、设置三条滑块均能保存、两个下拉框均能选择、语言切换后文本刷新，并运行 EditMode/PlayMode 测试。

校验器会在 Console 列出每个预制体的契约状态。进入验收前，`UiPrefabRoot` 界面类型、稳定 ID 和对应 Bindings 的必需控件清单应全部配置完成。

导出器会把现有占位 uGUI 快照转成 Prefab，用作画师配置正式 UI 资源的基础结构。

### 关后剧情配置（Gameplay 白盒）

在 `Assets/Game/Content/OfficialContentCatalog.asset` 的 Levels 关卡定义中设置 `PostludeStoryId`，方式与 `PreludeStoryId` 相同；为空表示没有关后剧情。三十关均已配置独立的 `official.story.postlude.test_XX_YY`，与关卡编号一一对应。测试剧情暂时复用现有分支对白内容，关前和关后的剧情 ID 不同，因此完成状态互不影响。

配置 ID 时，需同时在内容提供者中登记对应的 `StoryDefinition`；当前在该资源的 Stories 列表登记。只填写一个未登记的 ID 会在提交前提示错误，并留在结果面板。引用已经迁移至可编辑目录，运行时不再从 C# 测试目录生成它们。

成功面板提交后先保存通关进度，保存成功才调用 `PlayStoryAsync`，返回目标为 `StoryReturnTarget.ToMetaPage(MetaPageId.Map)`。剧情结束或跳过复用 `CompletedStoryIds` 写入当前档案，不新增存档文件或格式。自动播放依据本次提交前的 `CompletedLevelIds`：首次通关播放，重复通关直接回地图。播放中退出后，通关事实已保存，之后可在资料卡手动复播；再次通关不会自动播放。失败模拟不触发关后剧情。

验收：第一关模拟成功并提交后进入 Story，完成或跳过后回到 Map；重启并再次通关第一关应直接返回 Map；第二关及后续关卡首次通关也进入 Story，重复通关直接返回 Map。这些新增行为尚待本轮 Unity 人工验收。
### LevelCard 剧情复播入口

`MetaHubUI.prefab` 的 `LevelCard` 新增 `PreludeReplay`（关前剧情复播）和 `PostludeReplay`（关后剧情复播）。两个按钮由 `MetaHubShell` 自动绑定，无需配置 Inspector OnClick。布局、字体和文字可直接修改预制体节点；资料区域下沿已上移，为复播按钮留出空间。

复播权限由 `MetaMapQuery` 合并内容与存档后提供：关前剧情完成或跳过并保存后解锁关前复播；通关保存成功后解锁关后复播，无需先完成关后剧情。未配置或未解锁的入口隐藏，导航期间禁用。两种复播结束均回到 Map，不再次进入 Gameplay，不提交通关进度。

关后自动播放使用 `PostludeStoryId` 和提交前的关卡通关事实判断；所有三十关已配置独立测试关后剧情。已通关旧档也可使用该关的关后复播按钮。重播只记录剧情完成事实，不生成重复通关提交。
### 官方目录配置操作

1. 在 Project 选择 `Assets/Game/Content/OfficialContentCatalog.asset`。
2. 展开 **Levels**，第一项是 `official.level.test_01_01`。修改 **Prelude Story Id** 或 **Postlude Story Id** 可分别替换关前、关后剧情；留空表示没有该剧情。
3. 剧情 ID 必须对应本资源 **Stories** 中登记的 Story Id。当前保留原有三十二段白盒剧情记录，内容仍是测试分支对白；正式剧情编辑器生成文件的自动导入不在本次改动范围。
4. **Unlock Requirement / Mode** 为 None、All 或 Any；**Required Level Ids** 填前置关卡 ID。使用 All 表示全部完成，Any 表示任一完成。
5. **Map Id** 决定所属地图，**Sort Order** 决定顺序。Maps 列表配置地图 ID、名称 Key 和顺序；其 Levels 不需要手动维护，运行时从顶层 Levels 重建摘要。当前地图 UI 仍有五个节点，超过五关时需增加预制体节点。
6. 通过资源 Inspector 上下文菜单执行 **Validate Catalog**。重复 ID、未知地图/剧情/前置关卡、非法剧情和前置环会被拒绝；Bootstrap 启动也执行相同校验，错误时停止启动并在 Console 提示。
7. 停止运行后修改资源，再从 `00_Bootstrap` 启动验证。运行时不提供热更新。Bootstrap 的 **Content Catalog** 已绑定此资源。

迁移保留原有关卡与剧情稳定 ID，继续使用已有存档事实。已有存档使用的 ID 不宜随意重命名；修改对白但保留剧情 ID 会保留观看记录。地图状态、资料卡格式和关卡名称均由 `Assets/Localization/UI.csv` 维护。
