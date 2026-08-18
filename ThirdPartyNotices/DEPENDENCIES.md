# 第三方依赖台账

> **用途**：项目内部依赖管理记录，追踪每个第三方组件的引入日期、用途与版本锁定方式。
> **合规声明**（面向外部，发布/上架随包提供）见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。
> **依据**：《Unity2D建造解谜游戏-完整技术设计文档.md》§10.3 许可策略。

## 准入原则

1. 优先 Unity 官方 Released/Verified 包。
2. 开源库仅接受许可证明确且允许商业使用的 MIT、BSD、Apache-2.0 等宽松许可证；GPL/LGPL/SSPL、自定义限制条款需法务/负责人单独确认。
3. 付费 Asset Store 插件必须由团队账号购买、确认席位/组织授权并保存发票和 EULA 版本。
4. 固定确切版本和来源，不跟随浮动分支；升级单独评审。

## 依赖清单

| 依赖 | 版本 | 来源 | 许可证 | 引入日期 | 用途 | 锁定方式 |
|---|---|------|--------|---------|------|---------|
| Unity Input System | 1.7.0 | Unity Registry (`com.unity.inputsystem`) | Unity Companion License v1.4 | 2026-08-18 | 鼠标/滚轮/快捷键输入抽象（C25 部署 Input Action Map） | manifest 精确版本 + packages-lock.json |
| Unity Localization | 1.4.5 | Unity Registry (`com.unity.localization`) | Unity Companion License v1.4 | 2026-08-18 | String Table 与语言切换（C04 起） | manifest 精确版本 + packages-lock.json |
| Unity Test Framework | 1.1.33 | Unity Registry (`com.unity.test-framework`) | Unity Companion License v1.4 | 2026-08-18 | EditMode/PlayMode 测试 | manifest 精确版本 + packages-lock.json |
| Newtonsoft Json for Unity | 3.2.1 | Unity Registry (`com.unity.nuget.newtonsoft-json`) | MIT (Copyright © 2007 James Newton-King) | 2026-08-18 | 版本化 DTO JSON（内容/存档） | manifest 精确版本 + packages-lock.json |
| Steamworks.NET | 2025.164.1 | GitHub (git tag 锁定) (`com.rlabrecque.steamworks.net`) | MIT (Copyright © 2013–2022 Riley Labrecque) | 2026-08-18 | Steamworks C# 包装（C53 起，IPlatformService） | manifest git URL `?path=/...#2025.164.1` |
| Unity 2D Feature | 2.0.1 | Unity Registry (`com.unity.feature.2d`) | Unity Companion License v1.4 | 2026-08-18 | 2D 开发组件集 | manifest 精确版本 + packages-lock.json |
| Unity UI (uGUI) | 1.0.0 | Unity Registry (`com.unity.ugui`) | Unity Companion License v1.4 | 2026-08-18 | uGUI 界面 | manifest 精确版本 + packages-lock.json |
| Unity TextMeshPro | 3.0.7 | Unity Registry (`com.unity.textmeshpro`) | Unity Companion License v1.4 | 2026-08-18 | 文本渲染 | manifest 精确版本 + packages-lock.json |
| Unity Timeline | 1.7.7 | Unity Registry (`com.unity.timeline`) | Unity Companion License v1.4 | 2026-08-18 | 时间线（预留） | manifest 精确版本 + packages-lock.json |
| Unity Visual Scripting | 1.9.4 | Unity Registry (`com.unity.visualscripting`) | Unity Companion License v1.4 | 2026-08-18 | 可视化脚本（预留） | manifest 精确版本 + packages-lock.json |
| Unity IDE 集成 (Rider/VS) | 3.0.36 / 2.0.22 | Unity Registry (`com.unity.ide.rider` / `com.unity.ide.visualstudio`) | Unity Companion License v1.4 | 2026-08-18 | 编辑器集成 | manifest 精确版本 + packages-lock.json |
| Unity Collab Proxy | 2.12.4 | Unity Registry (`com.unity.collab-proxy`) | Unity Companion License v1.4 | 2026-08-18 | 版本控制集成（预留） | manifest 精确版本 + packages-lock.json |
| Unity Built-in Modules | 1.0.0 | Unity Registry (`com.unity.modules.*` 共 27 项) | Unity Companion License v1.4 | 2026-08-18 | 引擎内置模块（物理/动画/UI/网络请求等） | manifest 精确版本 + packages-lock.json |

## 许可证正文索引

许可证正文（原文完整版本）存放在 `licenses/` 子目录，合规声明引用如下：

| 许可证 | 文件 | 覆盖组件 |
|---|---|---|
| Unity Companion License v1.4 | [licenses/UNITY_COMPANION_LICENSE.txt](licenses/UNITY_COMPANION_LICENSE.txt) | 所有 `com.unity.*` 包 |
| MIT (Newtonsoft.Json) | [licenses/NEWTONSOFT_JSON_LICENSE.txt](licenses/NEWTONSOFT_JSON_LICENSE.txt) | Newtonsoft Json for Unity |
| MIT (Steamworks.NET) | [licenses/STEAMWORKS_NET_LICENSE.txt](licenses/STEAMWORKS_NET_LICENSE.txt) | Steamworks.NET |

## 版本锁定说明

- `com.rlabrecque.steamworks.net` 通过 `Packages/manifest.json` git URL `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1` 锁定 tag；许可证正文与 tag `2025.164.1` 内 `com.rlabrecque.steamworks.net/LICENSE.md` 一致。
- 其余 Unity 官方包在 `Packages/manifest.json` 中写死精确版本，并在 `Packages/packages-lock.json` 中解析锁定。
- **升级任何一项必须重新评审**：同步更新 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md) 的合规声明、本台账的版本/日期，以及若许可证正文变化则同步 `licenses/` 内对应文件。

## Steamworks SDK 附加说明

Steamworks.NET 是 Valve Steamworks SDK 的 C# 包装。其 MIT 许可证仅覆盖包装层代码；
随包分发的 Steamworks SDK 二进制由 Valve Steamworks SDK Agreement 单独约束，
最终用户受 Steam Subscriber Agreement 约束。详见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md) 中 Steamworks.NET 条目附注。
