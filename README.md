# AIPlayer（重建版）

把原版 **AIPlayer**（Dart / Flutter）的 Windows 桌面端，用 **C# / .NET 9 / WinUI 3** 重建为功能等价的版本。

- **定位**：逆向 + 还原。可逆向的部分还原；不可逆的部分（Dart AOT、Windows x64 无可用反编译器）用 C# 实现同等功能。
- **验收口径**：**行为等价**，不要求源码级复刻。
- **范围**：用户已收窄为「**只要 Emby 一端完整可用**」，其他服务端（Navidrome / AudioBookshelf / WebDAV 等）不做。

---

## 一、快速开始

直接运行：

```
dist\AIPlayer\AIPlayer.Shell.exe
```

非打包、自包含的 WinUI 3 应用；首次运行会在 `%LOCALAPPDATA%\AIPlayer\` 建立数据目录（设置、凭据、日志、图片缓存）。

**当前出厂版本**：`v10`（2026-09-13 01:56）

| 文件 | 字节数 | sha256_12 |
|---|---|---|
| `AIPlayer.Shell.exe` | 291,328 | `60B2144FBADC` |
| `AIPlayer.Shell.dll` | 1,733,632 | `B0E3B7DB327B` |
| `AIPlayer.Shell.Services.dll` | 819,200 | `E5CDD9150DA3` |
| `player\AIPlayer.MpvHost.dll` | 1,030,656 | `8133AC29AC6B` |
| `player\libmpv-2.dll` | 117,549,568 | `02FA97CBDB32` |

---

## 二、架构

```
┌──────────────────────────────┐        ┌───────────────────────────────┐
│  外壳 AIPlayer.Shell.exe      │        │  内核 AIPlayer.MpvHost.exe     │
│  C# / .NET 9 / WinUI 3       │  命令行 │  C#（WinUI 应用）              │
│  · 导航 / 首页 / 媒体库 / 搜索 │ ─────▶ │  · 复用原版内核（fork）         │
│  · 详情页 / 设置 / 服务器     │  参数   │  · 播放器 UI、字幕/音轨、弹幕   │
│  · 服务层（Emby 客户端）      │        │                               │
│  · 取图缓存 / 状态中枢        │ ◀───── │                               │
└──────────────────────────────┘  回调   └───────────────┬───────────────┘
        │  HTTP（Emby API）                             │ P/Invoke
        ▼                                              ▼
   Emby Server                                   libmpv-2.dll（mpv）
```

**三层要点**

- **外壳**：`shell/App`（WinUI 3 界面）+ `shell/Services`（Emby 客户端、缓存、凭据、设置）。
- **内核**：`kernel/` 是原版播放内核（上游 `Richasy.MpvKernel.WinUI`）的 **fork**，重建后替换原版 DLL；已验证「重建版 DLL 能被原版 exe 加载并真正放片」。
- **播放引擎**：**mpv**（`libmpv-2.dll`，版本 `v0.41.0-724-g71ebd0840`），内核通过 P/Invoke 直连。
- **壳 ↔ 内核**：壳把全部播放参数当**命令行**传给内核（`--open`/`--title`/`--episode-list`/`--subtitle-track`/`--http-header` …），内核通过 `--callback-url`（默认 `http://127.0.0.1:49153/callback`）把**播放状态与导航事件**回推给壳。

---

## 三、目录结构

```
shell/App              外壳 UI（WinUI 3）与内核接线
shell/Services         服务层：Emby 客户端、图片缓存、凭据、设置
shell/Tests            自检宿主与证据件
shell/tools            工装：门禁（evidence-hygiene-check）、dist 审计、截图等
shell/docs             规格、设计、评审、台账（WORKSPACE.md 为唯一 canon）
kernel/                播放内核 fork（控制端点、状态回推、优化项）
reversed/              原版产物与逆向基线（只读）
data/player            内核运行时文件（被 csproj 引用；从 worktree 构建需 junction）
dist/AIPlayer          出厂件（可运行）
```

> 从 worktree / archive 构建外壳时，**必须先把 `data` 目录 junction 进去**（`.gitignore` 排除了 `/data/`，而 `AIPlayer.Shell.csproj` 的引用面全在其下）。否则会出现 111 条 `CS0246` 假错误。

---

## 四、已实现（相对原版）

- **服务层**：Emby 三端 API、凭据与设置迁移、图片/弹幕磁盘缓存（含容量上限与负缓存）、代理、User-Agent、备份/歌词/跳过/章节。
- **界面**：导航壳（首页 / 收藏 / 媒体库 / 聚合视界）、服务器管理、搜索（聚合 + 逐源增量）、媒体库（滚动加载）、详情页、设置、占位页治理、Fluent 视觉对齐。
- **播放**：进程内/独立进程内核、播放中实时控制（音轨 / 字幕轨 / 版本切换）、状态回推单一真相源、连播。
- **体验**：启动期服务器并发探测、取图并发预算（全壳单点）、缓存淘汰、日志打码。

**用户报障修复记录（2026-09-12 → 09-13）**

| # | 现象 | 处置 |
|---|---|---|
| ① | 启动服务器状态检测慢 | 并发化：**22,743 → 9,632 ms（−57.6%）**，失败逐台可见 |
| ② | 搜索默认全是「集」 | 改为**剧优先** |
| ③ | **搜索点击结果无反应** | 根因：事件**零订阅者**，已接线 |
| ④ | 图片糊、图种太少 | 按图种取图（搜索 / 聚合视界 / 媒体库） |
| ⑤ | 左上角导航项与落点 | rail = 首页 / 收藏 / 媒体库 / 聚合视界；**点服务器落该服务器首页** |
| ⑥ | 媒体库要滚动加载 | **200 → 617 全量**（7 页、零重复、显式「已全部加载」） |
| ⑦ | 图片加载慢 / 并发 | 取图闸门收敛为**全壳单点**（预算 8） |
| ⑧ | 16:9 图片被做成竖屏 | 单集改取**主剧集竖海报**；首页媒体行裁切 **62.5% → 0%** |

---

## 五、已知问题（在修）

1. **播放器左上角显示媒体 URL 而不是片名**（且 URL 里带 `api_key`，属**用户可见**信息）。
   根因：服务层取了 `/Items/{id}/PlaybackInfo` 的响应体，而该响应**没有 `Name` 字段** ⇒ 传给内核时 `--title` 为空 ⇒ 内核回落显示 `--open` 的原始 URL。
   止血已落地（`--title` 一定非空、URL 形态被闸门拦下），补名在修。
2. **播放器里切换集数无效**。
   根因（静态定位）：解析导航回调的处理器只在**媒体库页**与**聚合页**注册，**详情页从未注册** ⇒ 从详情页起播时，回调虽回 200 但**回空体**、没有新地址。

---

## 六、数据与凭据

- 数据根：`%LOCALAPPDATA%\AIPlayer\`（`source=default`）；可通过 `AIPLAYER_APPDATA_ROOT` 覆盖（隔离测试用）。
- 登录态在 `accounts.json`（`encryptedToken`）+ `credentials.bin`，**不在** `servers.json` 的明文 token 里。
- 日志：`%LOCALAPPDATA%\AIPlayer\logs\aiplayer.log`；内核另有 `player\` 侧日志。

---

## 七、构建与发布（维护者）

```powershell
# 外壳
dotnet build shell\App\AIPlayer.Shell.csproj -c Debug

# 内核 fork
dotnet build kernel\src\AIPlayer.MpvHost.csproj -c Release -r win-x64
```

发布（在独立 worktree 里 pin 到目标提交后）：

1. 清 `shell\**\obj|bin`、`kernel\src\**\obj|bin`、`kernel\build`
2. `dotnet publish shell\App\AIPlayer.Shell.csproj -c Release -r win-x64 --self-contained true -o <out>`
3. `robocopy data\player <out>\player`（419 件）
4. 构建内核 fork 并覆盖 `<out>\player\AIPlayer.MpvHost.dll`
5. 换代 `dist\AIPlayer`（旧树改名为 `AIPlayer.bak-<HHmmss>`）
6. `shell\tools\dist-audit.ps1 -Dist <tree> -KernelForkSha12 <12> -OriginalStockSha12 8D73526C2C07`

> `dotnet publish` 单独产出的树**不可播放** —— `player\` 必须按第 3、4 步手工装配。

---

## 八、说明

本项目为**个人自用**的行为等价重建，不含原版任何源码；界面/交互口径以实测与逆向分析为准，全部规格与验收记录见 `shell/docs/`（台账 `WORKSPACE.md` 为唯一事实来源）。
