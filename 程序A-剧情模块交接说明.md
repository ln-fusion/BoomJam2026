# 程序 A：剧情模块交接说明

## 1. 交接范围

本文档说明程序 A 已完成的剧情 UI、剧情运行时表现器、剧情编辑器和测试数据，供程序 B 进行场景流程联调。

程序 A 负责：

- `03_Story` 场景中的剧情表现 UI。
- 对白、选项、跳过、历史记录和设置按钮的运行时显示。
- Story Authoring Window 的剧情编辑、保存、校验和编译测试。
- 剧情节点数据和最小测试剧情。

程序 A 不负责：

- MetaHub 地图节点的视觉和点击布局。
- StartMenu、Settings、Gameplay 的 UI 层级。
- 地图节点到 `LevelId` 的业务接线。
- 关卡成绩提交、解锁计算和完整 Gameplay 玩法。

## 2. 资源与代码位置

### 场景和预制体

| 用途 | 路径 |
| --- | --- |
| 剧情场景 | `Assets/Scenes/03_Story.unity` |
| 剧情正式白盒预制体 | `Assets/Game/Prefabs/UI/Story/StoryUI.prefab` |
| 剧情白盒备用预制体 | `Assets/Game/Prefabs/UI/Story/StoryUI_Whitebox.prefab` |
| 场景事件系统 | `03_Story.unity` 内的 `EventSystem` |

### 运行时代码

| 文件 | 职责 |
| --- | --- |
| `Assets/Game/Runtime/Presentation/StoryDialoguePanel.cs` | 绑定手工 UI，显示对白、选项、历史和按钮行为 |
| `Assets/Game/Runtime/Presentation/StoryScenePresenter.cs` | 启动剧情、驱动节点、处理分支和剧情结束返回 |
| `Assets/Game/Runtime/Presentation/StoryUiBindings.cs` | 手工预制体的序列化绑定契约 |
| `Assets/Game/Runtime/Bootstrap/SceneUiInstaller.cs` | 场景加载时复用已有 `StoryUI`，没有时创建回退根节点 |
| `Assets/Game/Runtime/Story/StoryRunner.cs` | 与 UI 无关的剧情节点执行器 |
| `Assets/Game/Runtime/Content/StoryDefinitionValidator.cs` | 校验 Story ID、节点 ID、跳转目标和选项 |

### 编辑器和数据

| 用途 | 路径 |
| --- | --- |
| 剧情编辑器 | `Assets/Game/Editor/StoryAuthoringWindow.cs` |
| 已有编辑器样例 | `Assets/Game/Content/story.new.story.authoring.json` |
| 最小分支测试剧情 | `Assets/Game/Content/story.simple_test.story.authoring.json` |
| 编译后的测试剧情 | `Assets/Game/Content/Generated/story.simple_test.story.runtime.json` |

## 3. StoryUI 节点约定

运行时按 `StoryUiBindings` 字段或节点名称查找控件。以下名称不要随意修改：

| 节点名 | 类型 | 用途 |
| --- | --- | --- |
| `StoryUI` | Canvas 根节点 | 剧情 UI 根对象 |
| `Speaker` | Legacy `Text` | 角色名 |
| `DialogueText` | Legacy `Text` | 对白正文 |
| `ContinueButton` | `Button` | 补全文本或推进节点 |
| `HistoryButton` | `Button` | 打开/关闭历史记录 |
| `SettingsButton` | `Button` | 请求打开全局设置弹窗 |
| `SkipButton` | `Button` | 第一次确认、第二次跳过 |
| `Choices` | `GameObject`/`RectTransform` | 运行时选项父节点 |
| `ChoiceButtonTemplate` | `Button` | 复制生成运行时选项的模板 |
| `History` | `GameObject` | 历史记录面板 |
| `HistoryText` | Legacy `Text` | 历史记录正文 |

`ChoiceButtonTemplate` 必须保留。它应设置为非激活对象，并具有非零高度；`Choices` 应使用 `Vertical Layout Group`，避免多个选项重叠。

## 4. 当前运行行为

### 直接测试 `03_Story`

在 `03_Story.unity` 中，`StoryUI` 上可以挂 `StoryScenePresenter`。运行时会自动调用 `Initialize()`，并使用 Inspector 中的 `Test Story Asset`：

- 未指定 `Test Story Asset`：播放内置 `official.story.c06_branch`。
- 指定有效 JSON：播放该 JSON 中的 `StoryId`。
- JSON 校验失败：Console 输出错误，并回退到内置测试剧情。

当前场景已经可以直接拖入：

```text
Assets/Game/Content/Generated/story.simple_test.story.runtime.json
```

### 节点行为

```text
Dialogue -> 显示对白 -> Continue -> Advance
Choice   -> 隐藏 Continue -> 复制模板生成多个选项
选择选项 -> 清理旧选项 -> 进入目标节点
Goto     -> 自动跳转，不显示额外 UI
End      -> 清空剧情面板，并按返回目标执行流程返回
```

对白打字机显示期间，第一次点击 `ContinueButton` 只会补全文本；再次点击才会推进到下一个节点。

## 5. 测试剧情

`story.simple_test` 的流程为：

```text
start
-> choice
-> inspect_room 或 leave_room
-> merge
-> end
```

两个选项分别是：

- `检查房间`：进入 `inspect_room`。
- `直接离开`：进入 `leave_room`。

测试步骤：

1. 打开 `Assets/Scenes/03_Story.unity`。
2. 选择 Hierarchy 中的 `StoryUI`。
3. 确认存在 `StoryScenePresenter`。
4. 将 `story.simple_test.story.runtime.json` 拖到 `Test Story Asset`。
5. 点击 Play。
6. 点击两次 Continue，确认出现两个选项。
7. 选择任意分支，确认旧选项消失并显示分支对白。
8. 继续到 `merge`，确认剧情结束后面板清理。

## 6. 剧情编辑器测试流程

打开 Unity 菜单：

```text
Game > Story Authoring Window
```

验收流程：

```text
New
-> 编辑 Story ID、Start Node 和对白
-> Add Dialogue Node
-> Add Choice
-> Remove Node
-> Save
-> 关闭窗口
-> Open 重新打开
-> Compile
```

编译文件输出到：

```text
Assets/Game/Content/Generated/<StoryId>.story.runtime.json
```

编辑器必须拦截以下错误：

- 空 Story ID。
- 不存在的 Start Node。
- 重复 Node ID。
- 不存在的 Next Node。
- 不存在的 Choice Target。
- Choice 节点没有有效选项。
- Dialogue 节点没有文本键。

正式剧情正文建议使用 Localization Key，例如：

```text
story.c06.start
story.c06.choice.inspect
story.c06.choice.leave
story.c06.merge
```

正文应放入 Unity Localization String Table，JSON 只保存键和节点关系。

## 7. 给程序 B 的流程交接契约

程序 B 从地图节点进入剧情时应调用已有流程接口：

```csharp
PlayStoryAsync(
    storyId,
    StoryReturnTarget.ToLevel(levelId),
    cancellationToken);
```

关前剧情的预期流程：

```text
MetaHub 节点点击
-> 传入 LevelId
-> PlayStoryAsync(preStoryId, ToLevel(levelId), token)
-> 03_Story
-> 剧情 End
-> 返回 04_Gameplay
```

资料剧情或关后剧情返回地图时：

```csharp
StoryReturnTarget.ToMetaPage(MetaPageId.Map)
```

对应流程为：

```text
03_Story
-> 剧情 End
-> 返回 02_MetaHub
-> 打开地图页并刷新节点状态
```

程序 B 需要提供给程序 A 的信息：

- 实际地图节点 `LevelId`。
- 关前 `PreStoryId`。
- 关后 `PostStoryId`。
- 进入 Story 的调用时机。
- 剧情结束后的 `StoryReturnTarget`。

## 8. 当前限制与联调注意事项

- `GameFlowService.EnterLevelAsync` 当前白盒实现固定使用 `official.story.c06_branch`，尚未按每个关卡读取 `PreStoryId`。
- `StoryScenePresenter` 的自定义 JSON 入口主要用于编辑器和场景白盒测试，不代表正式内容注册流程。
- `PlayStoryAsync` 当前主要记录返回目标并加载 `03_Story`，正式剧情内容注册仍需接入官方内容目录。
- `story.simple_test` 不是正式剧情 ID，只用于验证编辑器和 UI。
- 不要删除 `EventSystem`，否则按钮无法接收点击。
- 不要在 `StoryUI` 下再添加第二个 `StoryScenePresenter`，否则可能重复启动剧情。
- 修改预制体后需要保存，并确认场景实例没有覆盖旧布局。

## 9. 交接验收清单

程序 A 自检：

- [ ] `03_Story` 能单独运行。
- [ ] Continue 能补全文本并推进节点。
- [ ] 两个或多个选项能同时显示且不重叠。
- [ ] 选择后旧选项消失。
- [ ] History 能显示本次会话记录。
- [ ] Skip 两次点击能结束剧情。
- [ ] Settings 打开时剧情输入被冻结。
- [ ] Authoring JSON 能 Save、Open、Compile。
- [ ] Runtime JSON 出现在 `Assets/Game/Content/Generated/`。

程序 B 联调：

- [ ] 地图节点传入正确 `LevelId`。
- [ ] 首次进入关卡播放关前剧情。
- [ ] 剧情结束后进入正确 Gameplay 或 MetaHub 页面。
- [ ] 重复点击不会重复加载 Story 场景。
- [ ] 剧情结束后地图节点状态刷新。
- [ ] 完整流程重复测试至少两次。

## 10. 交付结论

程序 A 当前交付的是可运行的剧情白盒表现层、剧情编辑器测试链路和最小分支剧情数据。正式美术只需要替换图片、字体、颜色、动画和布局数值，不应改变上述节点名称、按钮类型和剧情流程接口。
