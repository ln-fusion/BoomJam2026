# 第一阶段程序 A 侧实施：Unity 编辑器操作清单

> 代码已全部就绪（C03-A ~ C05-A），以下步骤需在 Unity 中完成。

## 0. 编译错误修复记录（2026-08-25）

已修复 Unity 编译报错：

- `AudioContracts.cs`：补 `using UnityEngine;`（AudioClip 找不到）
- `AssemblyDependencyValidator.cs`：`CompilationPipeline.GetAssemblyDefinitionAssets()` 返回的
  `AssemblyDefinitionAsset` 类型不可引用 → 改用 `AssetDatabase.FindAssets("t:AssemblyDefinitionAsset")` + 读取 JSON 文本
- `UIRootManager.cs`：补 `using Game.Foundation;`（LevelId/StoryId 找不到）
- 全部 `#nullable` 警告（CS8618/CS8625/CS8603/CS1998）已清零：
  - 可空参数/返回值/事件/字段改为 `?` 或 `null!`
  - `SetLocaleAsync` 从 async 改为 `Task.FromResult`（无 await 的 CS1998）

## 1. 打开 Unity 让程序集生效

**必须做**。新增了 3 个 asmdef（`Game.Localization`、`Game.Audio`、`Game.Editor.Backbone`），
Unity 会：

- 为新程序集生成 csproj（`dotnet build` 才能编译）；
- 重新编译全部脚本，检查 Reference 是否缺失（缺失时 Console 有红色报错）。

## 2. 执行本地化搭建（一次性）

1. 菜单 `Tools → Change and Run → Setup Localization`
2. 生成内容：`Assets/Game/Localization/`
   - `zh-CN.asset`、`en-US.asset`（Locale）
   - `UI.asset`（String Table Collection）
3. 打开 `Assets/Scenes/00_Bootstrap.unity`
4. 选中挂 `GameRoot` 的 GameObject，把 `Localization Table` 字段拖入 `UI.asset`
   （GameRoot 会用它初始化 `UnityLocalizationService`）
5. 保存场景

> 若菜单不可见：先等编译完成；若报 `UnityEditor.Localization` 缺失，确认包已安装
> （`com.unity.localization: 1.4.5`，manifest.json 已有 ✓）。

## 3. 添加 UI 条目（可选，暂时直接显示 Key）

String Table 可以先用 Key 作为默认文本（`ILocalizationService.Get` 对缺失 Key 回退 Key 本身）。
正式中文文本录入由内容流程承担。

## 4. 验证运行

1. Play 模式下 `00_Bootstrap` 场景：
   - 自动加载 `01_StartMenu`（若 Build Settings 已含 01/02）
   - 看到"开始游戏 / 设置 / 退出"三按钮
   - 点"设置"出现音量滑条弹窗，点"关闭"销毁
   - 点"开始游戏"切入 `02_MetaHub`（MetaHubShell：上栏/下栏/侧栏占位 + 四页）
2. 从 `02_MetaHub` 点下栏占位暂无法交互（后续 C05 收口）

## 5. Build Settings 检查（编辑器）

`File → Build Settings → Scenes In Build` 应包含：
`00_Bootstrap`、`01_StartMenu`、`02_MetaHub`、`03_Story`、`04_Gameplay`
（顺序前四必选；`03/04` 为后续周期占位，可暂不加载）。
