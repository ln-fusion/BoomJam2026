# 两天 UI 白盒与剧情编辑器分工计划

## 一、目标和原则

两名程序在两天内把现有功能整理成可在 Unity 中直接运行的 UI 白盒场景，并让正式美术后续只需替换视觉资源即可继续使用。

目标范围：

- `00_Bootstrap`、`01_StartMenu`、`02_MetaHub`、`03_Story`、`04_Gameplay` 五个正式场景可从 Bootstrap 串联运行。
- 场景中默认放置 UI 预制体，不依赖运行时自动生成；保留代码生成作为预制体缺失时的回退。
- StartMenu、Settings、MetaHub、Story、Gameplay 的现有功能都能进行人工测试。
- 地图节点能够选中并显示资料卡；已接通的测试节点能够进入剧情，剧情结束后进入 Gameplay 白盒场景。
- `StoryAuthoringWindow` 能完成新建、打开、编辑、保存、校验、编译，并能用生成的剧情进行运行时测试。

工作原则：

- 程序 A 只负责剧情域：`03_Story`、剧情运行时 UI、剧情编辑器和剧情数据验证。
- 程序 B 只负责游戏壳和流程域：Bootstrap、StartMenu、Settings、MetaHub、Gameplay、场景跳转。
- 不互相修改对方负责的 UI 层级和预制体；需要联调时通过约定的绑定字段、事件和稳定 ID 交接。
- 不新增公共接口、不改变存档格式、不重写已有业务系统。地图节点到剧情的接线只允许复用已有 `EnterLevelAsync`/`PlayStoryAsync`。

## 二、模块边界和交接契约

### 程序 A：剧情模块

独占以下内容：

- `03_Story` 场景中的对白面板、选项、继续、跳过、历史记录白盒 UI。
- `StoryScenePresenter`、`StoryDialoguePanel` 的预制体引用和显示状态验证。
- `StoryAuthoringWindow` 的白盒编辑流程测试。
- `StoryDefinitionValidator` 校验用例和最小剧情样例。
- 剧情文本 Key、分支结构、运行时剧情文件的生成和验证。

交接给程序 B 的内容：

- 可运行的 Story UI 预制体或场景实例。
- 测试剧情 ID：`official.story.c06_branch`。
- 剧情完成、跳过后的返回行为说明。
- 剧情编辑器白盒测试记录和生成文件位置。

### 程序 B：游戏壳和流程模块

独占以下内容：

- `00_Bootstrap` 的 GameRoot、全局 Canvas、EventSystem 和场景加载顺序。
- `01_StartMenu` 的新建档案、继续档案、昵称输入和退出按钮。
- `SettingsModalUI` 的打开、取消、恢复默认、应用和保存。
- `02_MetaHub` 的页面导航、地图节点、资料卡和页面恢复。
- `04_Gameplay` 的进入、返回、暂停/结算占位白盒 UI。
- 地图节点到现有 `GameFlowService` 的最小接线。

交接给程序 A 的内容：

- 节点点击传入的稳定 `LevelId`。
- 剧情开始时使用的 `StoryReturnTarget`。
- 已确认的 Story 场景加载和返回路径。

### 两人共同遵守的预制体契约

- 不删除 `UiPrefabRoot`、`StartMenuUiBindings`、`MetaHubUiBindings`、`SettingsUiBindings` 等现有绑定组件。
- 预制体中的绑定节点名和字段类型保持不变；美术只替换图片、字体、颜色、动画和布局数值。
- 预制体登记统一写入 `Assets/Game/Content/ContentAssetRegistry.asset`。
- 每次交接只提交自己负责的目录和场景，避免同时修改同一个预制体文件。

## 三、第一天：各自完成模块白盒

### 程序 A：剧情 UI 和剧情编辑器

1. 检查 `03_Story.unity` 是否能被 Bootstrap 加载，确认 `SceneUiInstaller` 可以创建 `StoryScenePresenter`。
2. 在场景中搭建粗糙 Story 白盒层级：对白文本区、角色名占位、Continue 按钮、Choice 按钮容器、Skip 按钮、History 区域。
3. 将 Story UI 的控件与现有 `StoryDialoguePanel`/`StoryScenePresenter` 绑定，禁止在本模块新增运行时自动生成的替代层级。
4. 用现有测试剧情验证对白节点、选择节点、Goto 节点、End 节点均能显示或推进。
5. 打开 `Game > Story Authoring Window`，逐项验证 New、Open、Save、Compile、Story ID、Start Node、Nodes、Add Dialogue Node、Add Choice、Remove Node。
6. 新建最小分支剧情并保存为 `*.story.authoring.json`，关闭窗口后重新打开，确认节点和跳转关系保留。
7. Compile 到 `Assets/Game/Content/Generated/<StoryId>.story.runtime.json`，验证非法 ID、重复节点、缺失起始节点、未知跳转目标会被拦截。


### 程序 B：游戏壳、预制体和场景

1. 检查 `00_Bootstrap` 到 `04_Gameplay` 的 Build Settings 顺序、GameRoot、GlobalCanvasLayer 和 EventSystem。
2. 将 `StartMenuUI.prefab` 放入 `01_StartMenu`，确认昵称输入、开始/继续、设置、退出均能触发原有 Presenter。
3. 将 `SettingsModalUI.prefab` 作为设置弹窗来源，验证滑块、下拉框、Toggle 和应用/取消按钮引用完整。
4. 将 `MetaHubUI.prefab` 放入 `02_MetaHub`，确认地图、档案、人员、休息室页面及底部导航存在。
5. 在 MetaHub 地图页搭建五个粗糙节点和一个资料卡区域，先保证节点可见、可选中、可拖动/缩放预览。
6. 为 `04_Gameplay` 搭建最小白盒：关卡标题、开始/继续、暂停、返回、成功、失败、结算占位区域。
7. 将 UI 预制体登记到 Registry，执行 `Game/UI/Validate Registered UI Prefabs`，修复所有阻断错误。


### 第一天共同验收

- 程序 A 可以单独打开 Story 场景并运行测试剧情。
- 程序 A 可以单独完成剧情编辑器 New -> Edit -> Save -> Reopen -> Compile。
- 程序 B 可以单独打开 StartMenu、Settings、MetaHub、Gameplay 场景并看到预制体白盒。
- 五个场景均无缺失脚本、缺失绑定或重复 Canvas 的阻断错误。
- 两人只需交换稳定 ID、预制体根对象和返回目标，不需要互相修改模块内部代码。

## 四、第二天：模块联调和完整验收

### 程序 A：剧情模块最终联调

1. 使用程序 B 提供的测试 `LevelId` 和 `StoryReturnTarget` 验证从地图进入 Story 的参数没有丢失。
2. 验证对白继续、两个选择分支、分支汇合、Skip、History 和 End 状态。
3. 验证剧情结束后按返回目标回到 `02_MetaHub` 或进入 `04_Gameplay`，不重复创建 Story UI。
4. 检查文本 Key 缺失时至少显示稳定 Key，不出现空白对白面板。
5. 用剧情编辑器重新生成一份最小剧情运行时文件，确认 Story 场景可以读取并运行。
6. 只修复剧情 UI、剧情数据和剧情返回行为相关问题，不修改 StartMenu、MetaHub 的视觉层级。

### 程序 B：游戏壳和流程最终联调

1. 在现有 MetaHub 节点点击回调中接入 `GameFlowService.EnterLevelAsync(LevelId, ...)`，只复用已有接口和场景流转。
2. 确认节点首次点击进入 Story，Story 结束后进入对应 Gameplay；重复点击不会重复加载场景。
3. 验证 StartMenu 新档/继续、Settings 保存、MetaHub 页面恢复、Gameplay 返回。
4. 检查预制体优先逻辑：场景已有预制体时不再创建第二套运行时 UI；预制体缺失时回退逻辑仍可用。
5. 在目标分辨率检查所有白盒文字、按钮、面板不重叠、不越界，保证美术替换时不会改变交互区域。
6. 只修复场景、预制体、流程接线和 Gameplay 白盒相关问题，不修改 Story 编辑器内部实现。

### 第二天共同验收流程

完整游戏流程至少重复两次：

```text
00_Bootstrap
-> 新建或继续档案
-> 01_StartMenu
-> 打开 Settings，修改并应用
-> 02_MetaHub
-> 切换页面并进入地图
-> 点击已解锁地图节点
-> 03_Story
-> 完成对白、选择分支或 Skip
-> 04_Gameplay
-> 查看白盒状态并返回 MetaHub
-> 返回 StartMenu
-> 重启后确认档案和设置仍可读取
```

剧情编辑器白盒流程至少重复一次：

```text
打开 Story Authoring Window
-> New
-> 编辑 Story ID、Start Node、对白和选择节点
-> 保存 authoring JSON
-> 关闭并重新打开
-> Compile
-> 进入 Story 场景
-> 验证对白、分支、汇合和结束
```

## 五、交付物和问题处理规则

程序 A 交付：

- Story 白盒预制体/场景实例。
- 剧情编辑器白盒验收记录。
- 最小剧情 Authoring 文件和编译后的 Runtime 文件。
- Story ID、文本 Key、返回目标说明。

程序 B 交付：

- 五个正式场景中的游戏壳 UI 预制体实例。
- StartMenu、Settings、MetaHub、Gameplay 流程验收记录。
- LevelId 映射和节点点击接线说明。
- UI Registry 校验结果。

问题归属规则：

- Story 面板不显示、分支不推进、编辑器保存/编译失败：程序 A 处理。
- 场景不加载、按钮无响应、档案/设置失败、节点不触发流程、Gameplay 返回失败：程序 B 处理。
- 预制体字段缺失：由拥有该预制体的程序处理；另一人只提供复现步骤和 Console 信息。
- 需要跨模块修复时，先由问题所属程序提出最小改动点，另一人只验证，不直接修改对方模块。

本计划不包含完整 Gameplay 建造玩法、正式美术资源制作和正式剧情内容录入；两天交付目标是可运行、可测试、可替换资产的 UI 白盒和剧情编辑器白盒验证。
