# Unity 单机游戏本地存档架构设计

> 文档性质：方案说明。当前代码以 `Assets/Game/Runtime/Persistence/` 为准；本文件中使用“建议”“未来”的段落是尚未全部落地的设计，不是实现清单。

## 1. 文档目的

本文档用于说明一套适用于 **Unity + Windows 单机解谜游戏** 的本地存档架构。

当前代码已经保存的数据包括：

- 玩家设置
  - 音量
  - 屏幕分辨率
  - 全屏状态
  - 语言
- 玩家档案：昵称、档案 ID、页面 ID、完成事实、解锁事实、本地统计和通关提交记录。


项目中的地图和谜题内容完全由开发者预设生成，玩家不能改变地图结构，因此地图本身不属于玩家存档数据。

设计稿还预留了这些数据：

- 收集品
- 成就
- 每关完成状态
- 每关最佳成绩
- 每关最佳时间
- 星级
- 其他全局或关卡独立数据

这些预留字段不能当作当前功能。新增字段时需要配套迁移器和测试。

### 当前实现与本方案的差异

- 运行时入口是 `ISaveRepository`，默认实现为 `JsonSaveRepository`；没有名为 `SaveSystem` 的类型。
- 设置和玩家档案分别写入 `settings.json`、`profile.json`。设计稿早期使用的 `progress.json`、`SettingsRepository` 和 `ProgressRepository` 仍是方案名称，不对应当前类。
- `SettingsService.ApplyAsync` 在点击 Apply 时应用并保存设置，不是每次拖动滑块都写盘。
- `ProfileLifecycleService` 负责新档案和继续游戏的启动判断；通关进度服务尚未接入。

下面的章节保留方案层面的取舍，新增实现时应先更新接口和测试，再回写本文件。

---

## 2. 设计结论

方案采用：

```text
JSON 文件存储
+ 设置与游戏进度分文件
+ 内存数据对象
+ 统一存档管理入口
+ 每个文件独立版本号
+ 临时文件安全写入
+ 设置修改后自动保存
+ 通关后自动保存进度
+ 文件损坏时只重置对应数据
```

整体结构如下：

```text
SaveSystem
├── SettingsRepository
│   └── settings.json
└── ProgressRepository
    └── progress.json
```

其中：

- `settings.json` 只负责玩家设置。
- `progress.json` 负责关卡解锁进度及未来新增的游戏数据。
- 地图布局、谜题配置和关卡预设不进入存档文件。
- 不再使用 `PlayerPrefs` 保存核心进度数据。

---

## 3. 为什么将设置与进度分开保存

设置数据和游戏进度的生命周期不同。

设置数据的特点：

- 玩家修改后需要立即保存。
- 文件损坏时只需要恢复默认设置。
- 不应影响玩家的游戏进度。

游戏进度的特点：

- 当前主要在通关后保存。
- 文件损坏时只重置游戏进度。
- 未来会扩展更多与玩法相关的数据。

因此建议使用两个文件：

```text
settings.json
progress.json
```

这样可以实现：

```text
settings.json 损坏
→ 只重置音量、分辨率、全屏和语言
→ 不影响关卡进度
```

```text
progress.json 损坏
→ 只重置游戏进度
→ 不影响玩家设置
```

---

## 4. 总体职责划分

### 4.1 SaveSystem

`SaveSystem` 是游戏中所有存档操作的统一入口。

建议负责：

- 初始化存档系统
- 确定存档路径
- 加载设置
- 加载游戏进度
- 保存设置
- 保存游戏进度
- 数据序列化与反序列化
- 文件安全写入
- 数据合法性校验
- 存档版本迁移
- 文件损坏后的重置
- 恢复默认设置
- 清除游戏进度

游戏中的其他系统不应该直接操作 JSON 文件。

推荐的逻辑接口：

```text
SaveSystem
├── LoadAll()
├── LoadSettings()
├── LoadProgress()
├── SaveSettings()
├── SaveProgress()
├── ResetSettings()
├── ResetProgress()
├── GetSettings()
└── GetProgress()
```

---

### 4.2 SettingsRepository

负责 `settings.json` 的读取、写入、校验、迁移和重置。

它只处理设置相关数据，不了解关卡进度。

---

### 4.3 ProgressRepository

负责 `progress.json` 的读取、写入、校验、迁移和重置。

它只处理游戏进度，不了解分辨率、语言等设置。

---

### 4.4 业务系统

业务系统通过 `SaveSystem` 访问数据。

例如：

```text
设置菜单
→ 修改内存中的 SettingsData
→ 应用设置
→ 调用 SaveSettings()
```

```text
关卡完成系统
→ 更新 ProgressData
→ 调用 SaveProgress()
```

```text
关卡选择界面
→ 查询 ProgressData
→ 判断关卡是否解锁
```

这样可以避免文件读写逻辑散落在 UI、关卡和音频代码中。

---

## 5. 设置数据设计

方案数据结构：

```text
SettingsData
├── version
├── masterVolume
├── resolutionWidth
├── resolutionHeight
├── fullscreen
└── language
```

字段含义：

| 字段 | 含义 |
|---|---|
| `version` | 设置文件的数据结构版本 |
| `masterVolume` | 主音量 |
| `resolutionWidth` | 分辨率宽度 |
| `resolutionHeight` | 分辨率高度 |
| `fullscreen` | 是否全屏 |
| `language` | 当前语言标识 |

语言建议保存稳定标识，而不是界面显示文字。

例如：

```text
zh-CN
en-US
ja-JP
```

不建议保存：

```text
简体中文
English
日本語
```

稳定标识更适合程序判断，也更利于后续扩展。

---

## 6. 设置保存时机

玩家修改设置后自动保存。

方案流程：

```text
玩家修改设置
→ 更新内存中的 SettingsData
→ 玩家点击保存，然后应用设置
→ 校验数据
→ 保存 settings.json
```

不同设置可采用略有差异的触发时机。


## 7. 当前游戏进度设计

当前关卡是严格线性的，因此不需要为每一关保存一个解锁布尔值。

方案只保存：

```text
ProgressData
├── version
└── highestUnlockedLevel
```

例如：

```text
highestUnlockedLevel = 4
```

表示第 1 至第 4 关已经解锁。

关卡是否解锁可以直接推导：

```text
levelNumber <= highestUnlockedLevel
→ 关卡已解锁
```

这种方式优于为每一关保存：

```text
level1Unlocked = true
level2Unlocked = true
level3Unlocked = true
level4Unlocked = false
```

原因包括：

- 数据更少。
- 不容易出现前后关卡状态矛盾。
- 添加新关卡时更容易处理。
- 更符合当前严格线性的解锁规则。

---

## 8. 通关后的进度更新

当玩家通关第 `N` 关时，下一关应解锁。

方案逻辑：

```text
nextLevel = N + 1

highestUnlockedLevel =
    max(highestUnlockedLevel, nextLevel)

保存 progress.json
```

使用 `max` 很重要。

例如玩家已经解锁第 8 关，之后重新游玩并通关第 3 关：

```text
原最高解锁关卡 = 8
本次计算得到下一关 = 4
```

如果直接赋值，会错误地把进度降到第 4 关。

正确逻辑是：

```text
max(8, 4) = 8
```

因此旧关卡重玩不会导致进度倒退。

---

## 9. 为未来扩展预留结构

虽然当前只保存最高解锁关卡，但不建议把进度文件设计成一个裸整数。

从一开始就应使用完整的数据对象：

```text
ProgressData
├── version
├── highestUnlockedLevel
├── globalProgress
└── levelProgress
```

当前阶段可以只实际使用：

```text
version
highestUnlockedLevel
```

其他字段可以暂时为空、使用默认值，或在未来版本中加入。

---

### 9.1 全局进度

未来可以加入：

```text
GlobalProgress
├── collectedItemIds
├── unlockedAchievementIds
├── completedEndingIds
└── otherGlobalValues
```

适合保存：

- 全游戏共享的收集品
- 成就
- 已解锁结局
- 不属于单个关卡的数据

---

### 9.2 每关独立进度

未来可以加入：

```text
LevelProgress
├── levelId
├── completed
├── bestScore
├── bestTime
```

适合保存：

- 是否完成该关
- 最佳成绩
- 最佳通关时间

---

## 10. 关卡标识设计

每关数据建议使用稳定的 `levelId`，不要只依赖数组下标。

例如：

```text
level_001
level_002
level_003
```

关卡定义可以是：

```text
LevelDefinition
├── levelId
├── sceneName
├── mapPreset
└── puzzleConfiguration
```

其中：

- `levelId`：稳定且唯一的关卡标识。
- `sceneName`：对应 Unity 场景。
- `mapPreset`：地图预设。
- `puzzleConfiguration`：谜题配置。

即使未来显示顺序调整，存档仍可以通过 `levelId` 找到正确关卡。

虽然当前预计不会插入、重排或删除关卡，使用稳定标识仍是一种成本很低的安全设计。

---

## 11. 地图数据不进入存档

当前地图满足以下条件：

- 玩家不能改变地图结构。
- 地图完全由开发者预设。
- 同一关每次生成结果相同。
- 游戏是解谜游戏，地图本身属于关卡内容。

因此地图属于静态配置数据，而不是玩家存档数据。

可以这样区分：

```text
关卡配置负责：
- 地图是什么
- 谜题是什么
- 物体放在哪里
- 关卡如何生成
```

```text
玩家存档负责：
- 玩家解锁了什么
- 玩家完成了什么
- 玩家获得了什么
- 玩家在某关取得了什么成绩
```

当前不需要保存：

- 地图格子
- 地图结构
- 地图物体位置
- 谜题初始布局
- 关卡生成结果

这些内容应随游戏安装包发布，并由关卡配置或预设加载。

---

## 12. 文件格式选择

方案使用 JSON。

原因：

- Unity 中容易序列化和反序列化。
- 开发阶段便于调试。
- 文件结构容易直接检查。
- 方便添加字段。
- 容易进行版本迁移。
- 当前数据量很小，不需要数据库。

本项目不需要复杂加密。

加密无法真正阻止有意修改存档，只会提高：

- 调试成本
- 版本迁移成本
- 故障排查成本
- 开发复杂度

当前更重要的是防止文件因写入中断而损坏，而不是防作弊。

---

## 13. 建议的文件位置

在 Windows 上，应将存档写入 Unity 提供的持久化数据目录。

逻辑上使用：

```text
Application.persistentDataPath
```

文件结构示例：

```text
Application.persistentDataPath
├── settings.json
└── progress.json
```

不要将存档放在：

- `Assets` 目录
- 游戏安装目录
- `StreamingAssets`
- 可执行文件旁边

这些位置可能受到权限、更新或打包方式影响。

---

## 14. 游戏启动加载流程

方案启动顺序：

```text
1. 初始化 SaveSystem

2. 加载 settings.json
   ├── 成功
   │   → 校验设置数据
   │   → 执行版本迁移
   │   → 使用保存的设置
   └── 失败
       → 创建默认设置
       → 保存新的 settings.json

3. 应用音量、语言、分辨率和全屏设置

4. 加载 progress.json
   ├── 成功
   │   → 校验进度数据
   │   → 执行版本迁移
   │   → 使用保存的进度
   └── 失败
       → 创建默认进度
       → 保存新的 progress.json

5. 进入主菜单

6. 根据 ProgressData 刷新关卡按钮
```

设置数据应尽早加载。

否则游戏启动时可能先使用错误语言或分辨率，进入菜单后再突然切换。

---

## 15. 内存数据与磁盘文件

运行过程中，应先将数据加载到内存。

方案中的关系：

```text
磁盘 JSON 文件
→ 启动时读取
→ 转换为内存数据对象
→ 游戏运行期间使用内存对象
→ 在指定时机写回磁盘
```

不建议每次 UI 查询设置或判断关卡解锁时都读取文件。

正确方式：

```text
主菜单查询最高解锁关卡
→ 读取内存中的 ProgressData
```

错误方式：

```text
每刷新一个关卡按钮
→ 打开 progress.json
→ 重新读取文件
```

文件只应在以下时机读取：

- 游戏启动
- 主动重新加载存档
- 调试工具明确要求重新加载

文件只应在以下时机写入：

- 设置修改完成
- 关卡通关
- 未来其他明确的进度变化
- 用户执行重置操作
- 版本迁移完成

---

## 16. 文件安全写入

不要直接清空正式文件后重新写入。

如果在写入过程中出现：

- 游戏崩溃
- 系统断电
- 磁盘异常
- 程序被强制关闭

正式文件可能只写入一部分并损坏。

方案使用临时文件写入流程：

```text
1. 将完整数据序列化为 JSON

2. 写入临时文件
   settings.tmp
   或
   progress.tmp

3. 确认临时文件写入成功

4. 用临时文件替换正式文件

5. 删除残留临时文件
```

逻辑示例：

```text
settings.json
settings.tmp
```

写入时：

```text
新的设置数据
→ 写入 settings.tmp
→ 写入成功
→ 替换 settings.json
```

这种方式能减少写入中断造成的损坏。

---

## 17. 数据校验

JSON 能成功解析，不代表数据一定合法。

加载后仍应进行字段校验。

### 设置数据校验

建议检查：

```text
version 是否为支持的版本
masterVolume 是否在允许范围
resolutionWidth 是否大于 0
resolutionHeight 是否大于 0
language 是否为支持的语言
fullscreen 是否为有效布尔值
```

例如：

```text
masterVolume < 0
或
masterVolume > 1
→ 使用默认音量或执行修正
```

如果保存的分辨率不在当前显示器支持列表中：

```text
使用安全默认分辨率
或
选择最接近的可用分辨率
```

### 游戏进度校验

建议检查：

```text
version 是否为支持的版本
highestUnlockedLevel 是否至少为 1
highestUnlockedLevel 是否超过合理上限
关卡 ID 是否存在
成绩、时间、星级等数值是否合理
```

当前默认进度可以是：

```text
highestUnlockedLevel = 1
```

表示初始只解锁第一关。

---

## 18. 文件损坏处理

项目当前要求：

```text
settings.json 损坏
→ 只重置设置
```

```text
progress.json 损坏
→ 只重置游戏进度
```

方案处理流程：

```text
读取文件
├── 文件不存在
│   → 创建默认数据
│   → 写入新文件
├── JSON 解析失败
│   → 创建默认数据
│   → 覆盖损坏文件
├── 版本无法识别
│   → 创建默认数据
│   → 覆盖旧文件
└── 数据校验失败且无法修复
    → 创建默认数据
    → 覆盖异常文件
```

按照当前需求，不需要备份恢复。

不过安全写入仍然应该保留，因为它可以减少文件损坏发生的概率。

---

## 19. 存档版本设计

设置文件和进度文件应分别拥有自己的版本号。

例如：

```text
SettingsData.version
ProgressData.version
```

不建议共用一个版本号。

原因：

- 设置结构可能升级，但进度结构不变。
- 进度结构可能升级，但设置结构不变。
- 两类文件的迁移逻辑互不相关。

---

## 20. 版本迁移流程

加载文件后：

```text
读取 JSON
→ 检查版本号
→ 执行逐版本迁移
→ 校验迁移后的数据
→ 保存为最新版本
```

例如，旧版进度：

```text
Progress V1
├── version
└── highestUnlockedLevel
```

未来增加全局与每关进度：

```text
Progress V2
├── version
├── highestUnlockedLevel
├── globalProgress
└── levelProgress
```

加载 V1 时：

```text
读取 V1
→ 保留 highestUnlockedLevel
→ 创建默认 globalProgress
→ 创建空 levelProgress
→ 更新 version 为 2
→ 保存
```

方案采用逐级迁移：

```text
V1 → V2 → V3 → V4
```

不推荐为每一个旧版本单独编写到最新版的跳跃迁移：

```text
V1 → V4
V2 → V4
V3 → V4
```

逐级迁移更容易维护和测试。

---

## 21. 默认数据

系统应明确提供默认设置和默认进度。

### 默认设置示例

```text
masterVolume = 1.0
resolutionWidth = 推荐宽度
resolutionHeight = 推荐高度
fullscreen = true
language = 系统语言或默认语言
```

### 默认进度示例

```text
highestUnlockedLevel = 1
globalProgress = 默认值
levelProgress = 空
```

默认数据应由程序统一创建，不要把默认值分散写在多个 UI 或系统脚本中。

---

## 22. 重置功能

设置菜单应提供两个独立入口。

### 22.1 恢复默认设置

流程：

```text
玩家点击“恢复默认设置”
→ 弹出确认提示
→ 创建默认 SettingsData
→ 更新内存设置
→ 应用默认设置
→ 覆盖 settings.json
```

不影响 `progress.json`。

---

### 22.2 清除游戏进度

流程：

```text
玩家点击“清除游戏进度”
→ 弹出明确警告
→ 玩家确认
→ 创建默认 ProgressData
→ 更新内存进度
→ 覆盖 progress.json
→ 刷新关卡选择界面
```

不影响 `settings.json`。

清除进度属于不可逆操作，建议使用比普通设置重置更明显的确认提示。

---

## 23. 推荐事件流程

### 设置修改

```text
Settings UI
→ 修改 SettingsData
→ 应用新设置
→ SaveSystem.SaveSettings()
→ SettingsRepository 安全写入 settings.json
```

### 关卡通关

```text
Level Complete System
→ 计算下一关编号
→ 更新 highestUnlockedLevel
→ 更新未来的每关数据
→ SaveSystem.SaveProgress()
→ ProgressRepository 安全写入 progress.json
```

### 进入关卡选择界面

```text
Level Select UI
→ 获取内存中的 ProgressData
→ 比较关卡编号与 highestUnlockedLevel
→ 更新关卡按钮状态
```

### 恢复默认设置

```text
Settings UI
→ 玩家确认
→ SaveSystem.ResetSettings()
→ 应用默认设置
→ 保存 settings.json
```

### 清除游戏进度

```text
Settings UI
→ 玩家确认
→ SaveSystem.ResetProgress()
→ 保存 progress.json
→ 刷新关卡选择界面
```

---

## 24. 不建议的做法

### 不建议继续使用 PlayerPrefs 保存核心进度

`PlayerPrefs` 更适合：

- 简单偏好
- 非关键的小型数据
- 快速原型

不适合作为可扩展的完整进度架构。

主要问题：

- 数据结构能力弱。
- 缺乏统一版本管理。
- 不适合保存复杂对象。
- 后期扩展和迁移不方便。
- 容易让键名散落在多个脚本中。

---

### 不建议每关保存一个解锁布尔值

例如：

```text
level1Unlocked
level2Unlocked
level3Unlocked
```

当前严格线性的游戏只需要一个最高解锁关卡。

---

### 不建议保存静态地图结构

地图由关卡预设确定，玩家不能修改，因此重复保存地图会：

- 增加文件体积
- 增加加载复杂度
- 增加版本兼容问题
- 产生配置与存档不一致的风险

---

### 不建议让业务系统直接写文件

例如：

```text
音量 UI 直接写 settings.json
关卡脚本直接写 progress.json
主菜单直接读取 progress.json
```

这样会导致：

- 文件路径重复
- 错误处理重复
- 迁移逻辑分散
- 后续修改存储格式困难

所有文件操作应集中在存档系统中。

---

### 不建议为了防修改进行复杂加密

当前项目不关心玩家手动修改存档，因此复杂加密没有必要。

可选的简单措施包括：

- 基本校验
- 合法范围限制
- 文件格式检查

重点应放在稳定写入和版本兼容上。

---

## 25. 方案最终结构

```text
SaveSystem
├── 当前 SettingsData
├── 当前 ProgressData
├── LoadAll()
├── SaveSettings()
├── SaveProgress()
├── ResetSettings()
└── ResetProgress()

SettingsRepository
├── settings.json 路径
├── Load()
├── Save()
├── Validate()
├── Migrate()
└── CreateDefault()

ProgressRepository
├── progress.json 路径
├── Load()
├── Save()
├── Validate()
├── Migrate()
└── CreateDefault()

SettingsData
├── version
├── masterVolume
├── resolutionWidth
├── resolutionHeight
├── fullscreen
└── language

ProgressData
├── version
├── highestUnlockedLevel
├── globalProgress
└── levelProgress

LevelDefinition
├── levelId
├── sceneName
├── mapPreset
└── puzzleConfiguration
```

---

## 26. 最终结论

对于当前 Unity Windows 单机解谜游戏，最合适的方案是：

1. 使用 JSON 作为本地存档格式。
2. 将设置和游戏进度保存为两个独立文件。
3. 设置在玩家修改并应用后自动保存。
4. 游戏进度在玩家通关后自动保存。
5. 线性关卡只保存一个 `highestUnlockedLevel`。
6. 地图与谜题预设属于静态配置，不进入存档。
7. 未来通过 `globalProgress` 和 `levelProgress` 扩展更多数据。
8. 每个文件拥有独立版本号和迁移流程。
9. 使用临时文件替换正式文件，降低写入损坏风险。
10. 文件损坏时只重置对应文件。
11. 提供“恢复默认设置”和“清除游戏进度”两个独立入口。
12. 所有文件操作统一通过 `SaveSystem` 完成。

这套架构对当前项目不会造成明显的过度设计，同时可以平稳支持后续的收集品、成就、提示次数、每关成绩和其他进度数据。
