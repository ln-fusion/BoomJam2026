# BoomJam2026

这是项目根目录，也是唯一的 Unity 工程。当前使用 Unity `2022.3.62f3`。

## 当前实现

仓库已包含工程骨架、场景流程、开始菜单、设置、本地化、音频服务、UI 预制体导出/校验和本地 JSON 存档基础实现。剧情、关卡编辑器、完整玩法、Steam 接入和正式内容仍按开发计划推进；不要把设计文档中的验收项当作已完成清单。

## 文档入口

- [完整技术设计](Documents/Unity2D建造解谜游戏-完整技术设计文档.md)：模块边界、运行流程和后续系统设计。
- [双人开发计划](Documents/Unity2D建造解谜游戏-双人开发计划.md)：周期、里程碑和验收条件。
- [本地存档架构](Documents/Unity本地存档架构设计.md)：存档方案；其中“当前代码”小节以 `Assets/Game/Runtime/Persistence/` 为准。
- [UI 预制体指南](Documents/UI预制体配置与功能指南.md)：导出、绑定和验收 UI 预制体。

## 打开工程

- Open the repository root in Unity Hub. The root directory containing `Assets/`,
  `Packages/`, and `ProjectSettings/` is the only Unity project.
- Use Unity `2022.3.62f3`.
- Do not create or open a nested `Change and Run/` Unity project.
