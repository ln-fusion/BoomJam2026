---
description: "Use when: 在 Unity 项目中创建、移动、重命名文件或目录，需要确定文件放置位置、遵守程序集分层与目录约定。覆盖 Assets 目录结构、脚本分层放置、asmdef 依赖方向、美术资源分类、测试程序集、命名约定。"
name: "Change and Run 目录结构约定"
applyTo: ["Assets/**", "**/*.cs", "**/*.asmdef"]
---

# Change and Run 目录结构约定（硬性规则）

> 本项目为 Unity 2022.3 LTS 2D 建造解谜游戏。新文件的放置位置必须遵循以下规则，不确定时先查此文件，再查 [README](../../../README.md) 与 [Documents](../../../Documents/)

## 目录树（当前基准）

```
Assets/
├── Scenes/                 # 场景（每个场景一个同名子目录放烘焙缓存，已 gitignore）
├── Scripts/
│   ├── Runtime/
│   │   ├── Core/           # 框架层：日志、单例、SoundManager 等基础设施
│   │   ├── Systems/        # 玩法层：建造系统、解谜逻辑、关卡推进
│   │   └── UI/             # 表现层：UI 逻辑与绑定
│   └── Editor/             # 编辑器工具（仅编辑器编译）
├── Prefabs/                # 预制体
│   ├── UI/                 #   UI 预制体
│   └── World/              #   场景物件预制体（建造砖块、道具等）
├── Art/                    # 美术资源
│   ├── Textures/           #   贴图
│   ├── Models/             #   模型（FBX 等）
│   ├── Materials/          #   材质
│   ├── Animations/         #   动画片段/控制器
│   ├── Audio/              #   音频（BGM/SFX，配合 SoundManager 命名）
│   └── Fonts/              #   字体
├── Plugins/                # 原生插件（dll / so / aar，按平台分子目录）
├── ThirdParty/             # 第三方 C# 代码库
├── StreamingAssets/        # 原样拷贝、运行时只读的文件
└── Tests/
    ├── EditMode/           # 编辑器模式测试（不进 Play）
    └── PlayMode/           # 播放模式测试
```

## 程序集分层（依赖方向是硬性约束）

```
UI ──▶ Systems ──▶ Core
  └────────┴─────────┘
            ▼
       Editor（仅编辑器平台）
  Tests.EditMode / Tests.PlayMode（独立测试程序集）
```

## 新文件放置决策表

| 文件类型 | 放置位置 | 所属程序集 |
| --- | --- | --- |
| 框架基础设施（日志/单例/音效管理等） | `Scripts/Runtime/Core/` | Core |
| 玩法系统（建造/解谜/关卡） | `Scripts/Runtime/Systems/` | Systems |
| UI 逻辑与数据绑定 | `Scripts/Runtime/UI/` | UI |
| 编辑器工具/菜单/校验/生成器 | `Scripts/Editor/` | Editor |
| 贴图 | `Art/Textures/`（按用途再分子目录） | — |
| 模型 | `Art/Models/` | — |
| 材质 | `Art/Materials/` | — |
| 动画 | `Art/Animations/` | — |
| 音频 | `Art/Audio/`（BGM / SFX 子目录） | — |
| 字体 | `Art/Fonts/` | — |
| UI 预制体 | `Prefabs/UI/` | — |
| 场景物件预制体 | `Prefabs/World/` | — |
| 场景 | `Scenes/` | — |
| 原生插件 | `Plugins/<平台>/` | — |
| 第三方 C# 代码 | `ThirdParty/` | — |
| 运行时原样拷贝的只读文件 | `StreamingAssets/` | — |
| 编辑器测试（不进 Play） | `Tests/EditMode/` | Tests.EditMode |
| 播放模式测试 | `Tests/PlayMode/` | Tests.PlayMode |

## 硬性规则

1. **依赖单向**：`UI → Systems → Core`。禁止 Core 引用 Systems/UI，禁止 Systems 引用 UI；违反时应先询问。
2. **跨程序集引用**：新增对其它层的依赖，必须同步修改对应 `.asmdef` 的 `references`，不得依赖程序集全名。
3. **Runtime vs Editor**：运行时脚本只放 `Scripts/Runtime/`；纯编辑器代码（`#if UNITY_EDITOR` 包裹的除外）只放 `Scripts/Editor/`，避免被编进发布包。
4. **测试隔离**：测试程序集只引用被测层，测试之间不互相引用。
5. **命名**：脚本/类 PascalCase，命名空间 = asmdef `rootNamespace`；音频资源名与 `SoundManager` 声音库条目名一致（如 `DoorOpen`、`BGM_Main`）；场景用 `章节号_场景名`。
6. **meta 文件**：所有 `.meta` 纳入版本控制（项目已启用 Visible Meta Files），不要删除或手动编辑。
7. **新目录**：新增顶层功能目录时，如一时无文件，放 `.gitkeep` 占位以保持 git 可跟踪。
8. **音效管理**：新音频文件在 Inspector 配置进 `SoundManager` 的 `sounds` 列表（设置 name/type/volume/loop），代码侧统一通过 `SoundManager.Play("名字")` 调用，禁止直接挂 AudioSource 硬编码播放。

## 反例（禁止这样做）

- ❌ 把 UI 逻辑塞进 `Scripts/Runtime/Systems/`（违反分层）
- ❌ 在 Core 层引用 `UnityEngine.UI` 或其它项目程序集
- ❌ 运行时脚本放入 `Scripts/Editor/`（会被编进编辑器专用程序集，打包缺失）
- ❌ 直接使用 `Resources/` 目录加载资源（项目未启用，优先用常规引用或后续 Addressables）
- ❌ 把场景烘焙缓存（`Scenes/<场景名>/` 子目录）提交进版本控制（已 gitignore）
- ❌ 代码里硬编码 `GetComponent<AudioSource>().Play()` 播放音效（必须走 SoundManager 声音库）