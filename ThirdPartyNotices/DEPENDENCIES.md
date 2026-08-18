# Third Party Notices

> 依据：《Unity2D建造解谜游戏-完整技术设计文档.md》§10.3 许可策略。
>
> - 记录名称、版本、来源、许可证、引入日期和用途；
> - 保存许可证正文于本目录；
> - 版本固定（不跟随浮动分支），升级需单独评审；
> - 只接受 MIT、BSD、Apache-2.0 等宽松许可证（Unity 官方包适用 Unity Companion License）。

| 依赖 | 版本 | 来源 | 许可证 | 引入日期 | 用途 |
|---|---|------|--------|---------|------|
| Unity Input System | 1.7.0 | Unity Registry | Unity Companion License | 2026-08-18 | 鼠标/滚轮/快捷键输入抽象（C25 部署 Input Action Map） |
| Unity Localization | 1.4.5 | Unity Registry | Unity Companion License | 2026-08-18 | String Table 与语言切换（C04 起） |
| Unity Test Framework | 1.1.33 | Unity Registry | Unity Companion License | 2026-08-18 | EditMode/PlayMode 测试 |
| Newtonsoft Json for Unity | 3.2.1 | Unity Registry | MIT | 2026-08-18 | 版本化 DTO JSON（内容/存档） |
| Steamworks.NET | 2025.164.1 | GitHub (git tag 锁定) | MIT | 2026-08-18 | Steamworks C# 包装（C53 起，IPlatformService） |
| Unity 2D Feature | 2.0.1 | Unity Registry | Unity Companion License | 2026-08-18 | 2D 开发组件集 |
| Unity UI (uGUI) | 1.0.0 | Unity Registry | Unity Companion License | 2026-08-18 | uGUI 界面 |
| Unity TextMeshPro | 3.0.7 | Unity Registry | Unity Companion License | 2026-08-18 | 文本渲染 |
| Unity Timeline | 1.7.7 | Unity Registry | Unity Companion License | 2026-08-18 | 时间线（预留） |
| Unity Visual Scripting | 1.9.4 | Unity Registry | Unity Companion License | 2026-08-18 | 可视化脚本（预留） |
| Unity IDE 集成 (Rider/VS) | 3.0.36 / 2.0.22 | Unity Registry | Unity Companion License | 2026-08-18 | 编辑器集成 |
| Unity Collab Proxy | 2.12.4 | Unity Registry | Unity Companion License | 2026-08-18 | 版本控制集成（预留） |

## 许可证正文索引

| 许可证 | 文件 |
|---|---|
| Unity Companion License | [licenses/UNITY_COMPANION_LICENSE.md](licenses/UNITY_COMPANION_LICENSE.md) |
| MIT (Steamworks.NET) | [licenses/STEAMWORKS_NET_LICENSE.txt](licenses/STEAMWORKS_NET_LICENSE.txt) |
| MIT (Newtonsoft.Json) | [licenses/NEWTONSOFT_JSON_LICENSE.txt](licenses/NEWTONSOFT_JSON_LICENSE.txt) |

## 锁定说明

- `com.rlabrecque.steamworks.net` 通过 `manifest.json` git URL `?path=/com.rlabrecque.steamworks.net#2025.164.1` 锁定 tag；
- 其余 Unity 官方包在 `manifest.json` 中写死精确版本，并在 `packages-lock.json` 中解析锁定；
- 升级任何一项必须重新评审此处记录。
