---
description: "Use when: 在 Unity 项目中创建、移动、重命名文件或目录，需要确定文件放置位置、遵守程序集分层与目录约定。覆盖 Assets 目录结构、脚本分层放置、asmdef 依赖方向、美术资源分类、测试程序集、命名约定。"
name: "Change and Run 目录结构约定"
applyTo: ["Assets/**", "**/*.cs", "**/*.asmdef"]
---

# Change and Run 目录结构约定（硬性规则）

> 本项目为 Unity 2022.3 LTS 2D 建造解谜游戏（模块化单体 + 分层 + 组合根）。新文件的放置位置必须遵循以下规则，不确定时先查此文件，再查 [README](../../../README.md) 与 [技术设计文档](../../../Documents/Unity2D建造解谜游戏-完整技术设计文档.md)（**文档 4.1/4.2 节是目录与程序集的权威来源，本节为磁盘现状快照**）。

> 目录基准快照日期：2026-08-25。反应的是磁盘现状，新建目录时优先参考设计文档目录树，本快照随项目演进需同步更新。

## 目录树（磁盘现状 2026-08-25）

```
Assets/
├── Game/                       # 全部游戏代码与资源（模块化单体）
│   ├── Runtime/                # 运行时程序集（每个子目录一个 asmdef）
│   │   ├── Foundation/         #   Game.Foundation：ID、Result、校验、通用集合 + 日志/取消子目录
│   │   │   ├── Cancellation/
│   │   │   └── Logging/
│   │   ├── Contracts/          #   Game.Contracts：跨模块接口/命令/事件/只读模型（稳定边界）
│   │   │   ├── Content/        #     内容相关契约
│   │   │   ├── Lifetime/       #     取消/生命周期契约
│   │   │   ├── Persistence/    #     存档相关契约（ISaveRepository、SaveData 等）
│   │   │   ├── Progression/    #     进度相关契约
│   │   │   └── UI/             #     UI 相关契约（IView、ViewModel 等）
│   │   ├── Flow/               #   Game.Flow：启动、返回栈、完成事务协调
│   │   ├── Content/            #   Game.Content：官方内容目录与 Provider
│   │   ├── Persistence/        #   Game.Persistence：Repository、迁移、原子写入（本地存档）
│   │   │   └── Properties/     #     程序集属性
│   │   ├── Platform/           #   Game.Platform：Steam/离线适配器（当前空，待实现）
│   │   ├── Progression/        #   Game.Progression：解锁事实、规则、最佳成绩（锚点占位已有代码）
│   │   ├── Story/              #   Game.Story：StoryRunner、节点执行器、历史（锚点占位）
│   │   ├── Meta/               #   Game.Meta：地图/档案/人员查询服务（锚点占位）
│   │   ├── Gameplay/           #   Game.Gameplay：部署、物理、能力、条件、结算（锚点占位）
│   │   ├── Localization/       #   Game.Localization：Unity Localization 适配服务
│   │   ├── Audio/              #   Game.Audio：音频服务（UnityAudioService、NullAudioAssetResolver）
│   │   ├── Presentation/       #   Game.Presentation：uGUI View/Presenter/Input Adapter
│   │   │   ├── Common/         #     UIFactory 等通用 UI 设施
│   │   │   ├── MetaHub/        #     主界面/局内中心（MetaHubRoot、MetaPageRouter）
│   │   │   ├── Settings/       #     设置对话框
│   │   │   └── StartMenu/      #     开始菜单
│   │   └── Bootstrap/          #   Game.Bootstrap：组合根与全局生命周期（GameRoot 所在）
│   ├── Editor/                 # 编辑器程序集（Game.Editor.Backbone）
│   │   └── AssemblyValidation/ #     AssemblyDependencyValidator.cs、SetupLocalization.cs
│   ├── Localization/           # 本地化资产（LocalizationSettings、Locale、String Table）
│   └── Tests/
│       └── EditMode/           # Game.Tests.EditMode：编辑器模式测试
│           ├── Content/        #     内容服务测试
│           ├── Contracts/      #     契约层测试
│           ├── Foundation/     #     基础设施测试
│           ├── Fakes/          #     测试替身
│           └── Persistence/    #     存档相关测试
├── Scenes/                     # 场景（00_Bootstrap / 01_StartMenu / 02_MetaHub / 03_Story / 04_Gameplay）
└── SoundManager.cs             # 旧版全局单例（Assets 根，遗留，新代码禁止扩展）
```

> AddressableAssetsData/ 已存在（指向 Assets/AddressableAssetsData/），当前仅初始化未使用，资源加载暂用常规引用。

## 程序集分层（依赖方向是硬性约束）

以 asmdef `references` 实测为准（2026-08-18 快照）：

```
                       ┌── Game.Foundation（无依赖，最底层）
                       ▼
                Game.Contracts ──▶ Game.Foundation
                       │
     ┌─────────┬───────┼─────────┬──────────┐
     ▼         ▼       ▼         ▼          ▼
  Game.Flow  Content Persistence Platform  ...
  (业务/表现模块统一只依赖 Contracts + Foundation)
     │
     ▼
  Game.Bootstrap ──▶ 所有运行时程序集（组合根，唯一例子）
     │
     ▼
  Game.Tests.EditMode ──▶ Foundation + Contracts + Flow + TestRunner（独立测试程序集，autoReferenced=false）
```

依赖关系速查（**新增依赖必须同步改 asmdef references，禁止用全名引用**）：

| 程序集 | 引用（references） |
| --- | --- |
| Game.Foundation | （无） |
| Game.Contracts | Game.Foundation |
| Game.Flow / Content / Persistence / Platform / Progression / Story / Meta / Gameplay / Audio | Game.Foundation + Game.Contracts |
| Game.Persistence / Content | 另引 Unity.Newtonsoft.Json |
| Game.Localization | Game.Foundation + Game.Contracts + Unity.Localization |
| Game.Presentation | Game.Foundation + Game.Contracts + Game.Flow + UnityEngine.UI |
| Game.Bootstrap | Game.Foundation + Game.Contracts + Game.Flow + Game.Presentation + Game.Localization + Game.Audio + Game.Persistence + Game.Progression + Unity.Localization + Unity.InputSystem |
| Game.Editor.Backbone | Game.Foundation + Game.Contracts + Unity.Localization.Editor + Unity.Localization（includePlatforms=Editor） |
| Game.Tests.EditMode | Game.Foundation + Game.Contracts + Game.Flow + Game.Content + Game.Persistence + Game.Progression + Game.Presentation + UnityEngine.TestRunner + UnityEditor.TestRunner（includePlatforms=Editor，autoReferenced=false，defineConstraints=UNITY_INCLUDE_TESTS） |

## 新文件放置决策表

### 代码脚本

| 文件类型 | 放置位置 | 所属程序集 |
| --- | --- | --- |
| 跨模块接口/命令/事件/只读模型 | `Runtime/Contracts/` | Game.Contracts |
| 基础设施（ID、Result、日志、取消、通用集合） | `Runtime/Foundation/` | Game.Foundation |
| 启动/返回栈/事务协调 | `Runtime/Flow/` | Game.Flow |
| 内容目录与 Provider | `Runtime/Content/` | Game.Content |
| 存档 Repository/迁移/原子写入 | `Runtime/Persistence/` | Game.Persistence |
| Steam/离线适配器 | `Runtime/Platform/` | Game.Platform |
| 解锁/最佳成绩规则 | `Runtime/Progression/` | Game.Progression |
| 剧情/节点执行器 | `Runtime/Story/` | Game.Story |
| 地图/档案/人员查询 | `Runtime/Meta/` | Game.Meta |
| 玩法规则（部署/物理/能力/条件/结算） | `Runtime/Gameplay/` | Game.Gameplay |
| 本地化服务/表驱动翻译 | `Runtime/Localization/` | Game.Localization |
| 音频服务/音频加载 | `Runtime/Audio/` | Game.Audio |
| uGUI View/Presenter/Input Adapter | `Runtime/Presentation/` | Game.Presentation |
| 组合根/全局生命周期/启动装配 | `Runtime/Bootstrap/` | Game.Bootstrap |
| 编辑器工具/菜单/校验/生成器 | `Game/Editor/` 下对应子目录 | 各 Editor 程序集（当前 Game.Editor.Backbone） |
| EditMode 测试 | `Game/Tests/EditMode/`（按被测层分子目录） | Game.Tests.EditMode |

### 场景与资源（当前尚未建立 Art/Prefabs 等资源目录，落地时按设计文档 4.1 创建）

| 文件类型 | 放置位置（设计文档规划） |
| --- | --- |
| 场景 | `Assets/Scenes/`，命名 `NN_场景名`（00_Bootstrap/01_StartMenu/02_MetaHub/03_Story/04_Gameplay） |
| 美术资源 | `Assets/Game/Art/`（Textures/Models/Materials/Animations 按需求分子目录） |
| 音频 | `Assets/Game/Audio/`（BGM / SFX 子目录，配合 SoundManager 命名） |
| 预制体 | `Assets/Game/Prefabs/` |
| 编辑源数据/编译产物 | `Assets/Game/Content/Authoring/` 与 `Content/Generated/`（运行时只读 Generated） |
| 本地化 | `Assets/Game/Localization/` |
| 设置 | `Assets/Game/Settings/` |

## 硬性规则

1. **依赖单向**：业务/表现/基础设施模块**只能引用 `Game.Contracts` + `Game.Foundation`**；`Game.Bootstrap` 是唯一允许依赖全部运行时程序集的位置（组合根）。禁止反向依赖；违反时应先询问。
2. **Contracts 是稳定边界**：只有跨模块的类型进 `Game.Contracts`；模块内部 DTO、辅助类、MonoBehaviour 必须留在模块内部，不得塞进 Contracts。
3. **跨程序集引用**：新增依赖必须同步修改对应 `.asmdef` 的 `references`，不得依赖程序集全名（禁用覆盖全部源码的单个 `Assembly-CSharp`）。
4. **Runtime vs Editor**：运行时脚本只放 `Runtime/` 下；纯编辑器代码（`#if UNITY_EDITOR` 包裹的除外）只放 `Game/Editor/`，避免被编进发布包。
5. **禁止 Runtime 引用 UnityEditor**：任意 Runtime 程序集不得引用 `UnityEditor`；Editor 程序集例外。
6. **测试隔离**：测试程序集只引用被测层 + 测试框架，测试之间不互相引用；`autoReferenced=false` + `UNITY_INCLUDE_TESTS` 约束。
7. **命名**：脚本/类 PascalCase，命名空间 = asmdef `rootNamespace`（如 `Game.Foundation`、`Game.Bootstrap`）；场景用 `NN_场景名`；稳定内容 ID 用小写命名空间形式（如 `official.level.factory_001`），运行时引用必须用强类型 ID 而非裸 string/显示名/数组下标。
8. **meta 文件**：所有 `.meta` 纳入版本控制（项目已启用 Visible Meta Files），不要删除或手动编辑。
9. **新目录**：新增顶层功能目录时，如一时无文件，放 `.gitkeep` 占位以保持 git 可跟踪。
10. **业务模块不直接触碰外部 IO**：不直接读 JSON、调 Steamworks API、跳 Scene；通过 Contracts 接口与用例协调器完成。UI 不直接改存档 DTO；关键事务用显式协调器，不用全局事件担主流程。
11. **禁止全局静态单例**：业务代码中不使用 `Manager.Instance` 或运行时 Service Locator（`Assets/SoundManager.cs` 是已存在遗留，新代码不允许扩展它）。

## 反例（禁止这样做）

- ❌ 把 UI 逻辑塞进 `Runtime/Gameplay/`（违反模块分层）
- ❌ 在 `Game.Foundation` 或 `Game.Contracts` 里写文件 IO、Steam API、uGUI、SceneManager 等具体实现
- ❌ 业务模块直接引用 `Gameplay`/`Story` 等其它模块的具体实现（只能引用 Contracts）
- ❌ 运行时脚本放入 `Game/Editor/`（会被编进编辑器专用程序集，打包缺失）
- ❌ Runtime 程序集引用 `UnityEditor`
- ❌ 使用盘存全名引用其它程序集而不改 asmdef references
- ❌ 把跨模块类型以外的东西塞进 `Game.Contracts`（杂物化稳定边界）
- ❌ 直接使用 `Resources/` 目录加载资源（项目未启用，优先用常规引用或后续 Addressables）
- ❌ 代码里硬编码播放音效而不走音频管理（Audio 模块落地前沿用现有 SoundManager）
