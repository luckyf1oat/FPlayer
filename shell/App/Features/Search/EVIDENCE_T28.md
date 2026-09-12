# t28（U-B 搜索 + 聚合搜索）交付与运行证据

> 作者 = `ui3`｜卡 = t28（P2）｜规格 = `shell/docs/UI_SPEC_SHELL.md` §7（§7.2 入口 / §7.3 结果面板 / §7.4 芯片 / §7.5 单选 / §7.6 聚合行为）+ §10 P2 判据
> 参照物实测 = `shell/docs/HILLSLITE_UI_ANALYSIS.md` §4.4（芯片行 y 56..88 高 33、芯片间距 8–9、面板自 y≈97 起、海报 166×249、列间距 24、卡片角标）

## 0. 三态声明（逐条分开写，不合并）

| 三态 | 本条到哪一步 | 证据 |
|---|---|---|
| **逆向到了** | 参照物几何/配色取自 `HILLSLITE_UI_ANALYSIS.md` §4.4 与 `UI_SPEC_SHELL.md` §1 令牌表（均为该文档里的逐像素实测值，非本轮重测） | 本文档引用处均标行号 |
| **重建实现了** | 本屏 5 个源文件在盘（`Features/Search/`），外壳工程 `dotnet build` **0 错误** | §2 构建读数 |
| **运行验证过了** | **真实 Emby 服务器**（16 台，来自本机 `accounts.json` 兼容读）上真跑四路：空查询 / 聚合（含反控坏源）/ 类型芯片 / **真实外壳内集成**；截图 + 原始日志 + 命令退出码齐备 | §3–§5 |

---

## 1. 交付物（全部在 `shell/App/Features/Search/`，**未碰任何他人文件**）

| 文件 | 字节 | sha12 | 作用 |
|---|---|---|---|
| `SearchPage.xaml` | 8,284 | `B8EC60D1BDD2` | 屏体：芯片行 / 摘要行 / 结果网格 166×249 / 历史 / 空态 / 加载态 |
| `SearchPage.xaml.cs` | 30,012 | `02F811DAB8D1` | 交互：300 ms 去抖、单选芯片、三态、历史增删清、取证钩子、对外契约 |
| `SearchRunner.cs` | 20,224 | `077CE47C1BDF` | 数据面：建源（按 `ServerKind` 能力）→ `AggregatedSearchService` / 主源类型搜索 → 五个计数 + 请求面日志 |
| `SearchChip.cs` | 5,039 | `66853509F3A0` | 芯片目录（§7.4 映射 + §7.5 单选建模 + 置灰理由） |
| `SearchLog.cs` | 4,309 | `24DBDBB87B13` | 宿主无关日志（**唯一写盘入口做凭据打码**） |
| `evidence/`（12 个 log+png；**本批新增 3 个**见 §9） | — | 见 §3 | 四路取证的原始读数与截图 |

**对外契约（外壳只需一行挂载，已由 `ui` 接好）**：`AIPlayer.Shell.Features.Search.SearchPage`
`NavTag="search"` · `SetQuery(term, immediate)`（300 ms 去抖）· `CommitQuery(term)`（Enter：立即 + 落历史）· `ClearQuery()`（Esc/×）· `event CardActivated`。

---

## 2. 构建读数（命令 + HEAD + 时刻 + 退出码）

```
[1] dotnet build "C:\Users\Administrator\AppData\Local\Temp\t28-host\t28host.csproj" -c Debug
    HEAD=0b83e1b  at=2026-09-11 23:36:44  EXIT=0  →  0 个警告 0 个错误   ← 最终源码（含请求面日志）
[2] dotnet build shell\App\AIPlayer.Shell.csproj -c Debug  -p:OutputPath=E:\t28-verify\bin\
    HEAD=73be587  at=2026-09-11 23:41:54  EXIT=0  →  0 个错误            ← ★ 最终源码上的外壳绿读数
[3] 23:12 首跑（HEAD=9c146af）红：5 个错误全在 KernelHost/InboundCallbackEndpoint.cs = ui2 的 t27 在途文件（CS0103/CS0128）
    23:26（HEAD=cb8eaaa）待该文件修好后首次转绿：EXIT=0 / 0 错误
    23:30–23:41 期间多次重跑被**并发**挡住（非代码错）：MSB3027/MSB3021 文件锁（锁定者 = 别人运行的
    AIPlayer.Shell pid 19956 / 20232 / 20416）、CSC CS2012（obj 中间产物被另一个 XamlCompiler 占用）
    ⇒ 按纪律**不杀他人进程**；[2] 改用**重定向输出目录**（`-p:OutputPath`）避开别人正占用的 bin\，
      这才拿到"最终源码 + 全量编译"的绿读数。
```
- `[1]` = **独立取证宿主**（见 §6），只链接 `Features/Search/*` + `Theme/Tokens.xaml` + `Services.csproj`，不受别人在途文件/进程锁影响。
- **`[3]` 的每次红都逐条归因到"别人在途"或"并发锁"，没有一次是 `Features/Search/` 的错**；`[2]` 是最终源码上 0 错误的直接读数。
- 附注：`ui`/captain 已按本卡的证据目录给 `AIPlayer.Shell.csproj` 加了 `<Content Remove="**\evidence\**" />` + `<None Remove="**\evidence\**" />`（第 305–312 行）—— 否则 WinUI 默认 Content 通配会把 `evidence/*.png` 卷进构建（MSB3030）。**本卡的证据目录正是这个补丁的起因**，特此记明。

---

## 3. 四路运行取证（全部真起窗、真打网络）

| # | 路 | 入口 | 关键读数（原始日志） |
|---|---|---|---|
| A | 空查询 | 宿主 | `SELFCHECK-EMPTY networkRequests=0 historyVisible=Visible historyEntries=0 summaryVisible=Collapsed chipCount=7` |
| B | 聚合 + 反控坏源 | 宿主 | `sourceCount=17 mergedCount=118 mergedHitCount=120 shown=60 totalHits=120 failed=10 cards=60 elapsedMs=40374` |
| C | 类型芯片「电影」 | 宿主 | `sourceCount=1 mergedCount=59 shown=59 cards=59 failed=0 emptyUrl=0 opened=13` |
| D | **真实外壳内集成** | `SHELL_START_PAGE=search` → `MainWindow.NavigateTo("search")`；二进制 = 最终源码 `-p:OutputPath=E:\t28-verify\bin\` 的产物（HEAD 73be587） | 与 B 同读数；窗口 `title='AI Player'` 1440×759，侧栏采样 `#202020`、选中行 `#2D2D2D`、面板 `#282828` |

**截图（CopyFromScreen 通道；`PrintWindow` 对 D3D 合成面是黑帧，实测亮度 luma=48 vs 真帧 61–95）**

| 文件 | 字节 | sha12 | 内容（人眼核对） |
|---|---|---|---|
| `evidence/t28-empty-screen.png` | 33,907 | `3723A476A51C` | 芯片行 7 个（聚合搜索紫色选中）+「搜索历史 / 清空」+ 空历史文案 |
| `evidence/t28-typed-movie-screen.png` | 1,571,506 | `5165185E3C40` | 「电影」芯片选中，真实海报网格（阿凡达：火与烬…），`emptyUrl=0 opened=20` |
| `evidence/t28-agg-bad-screen.png` | 652,696 | `F22E40E94DD7` | 摘要行 + 失败源红字 10 台 + 60 张卡（`2源`/`12`/`10`/`5` 角标） |
| `evidence/t28-shell-agg-screen.png` | 477,723 | `6AFE3FB86B87` | ⭐ **真外壳（最终源码二进制）**：侧栏（首页/收藏/聚合视界 + 16 台服务器 + 绿/粉状态色 + 「N 天前看过」）+ 顶栏搜索框 + 我的芯片行/摘要行/网格 + 状态栏「当前页：search 服务器 16 台（启用 16）」；采样 侧栏 `#202020`、选中行 `#2D2D2D`、面板 `#282828` |

原始日志：`evidence/t28-{empty,typed-movie,agg-bad,shell-agg}-search.txt`（sha12 = `6227095622AC` / `3C907B2FD3CF` / `6C7149354E4D` / `C46D54A8F298`）。

> **扩展名说明（Captain，入账前处理）**：这 4 份原始日志交付时是 `.log`，而被仓库 `.gitignore` 第 65 行 `*.log` 忽略 ⇒ **证据文件当时根本没进仓库**。现统一改名 `.txt`（**内容一字未改，故 sha12 不变**）。项目纪律：**证据产物不得使用 `.log` 扩展名**。

---

## 4. §10 P2 判据 × 逐条对照

| 判据（原文） | 本卡读数 | 判定 |
|---|---|---|
| `dotnet build shell/App/AIPlayer.Shell.csproj -c Debug` exit=0、0 错误 | HEAD `73be587` @23:41:54 `EXIT=0 / 0 个错误`（`-p:OutputPath` 重定向以避开别人运行中实例的输出目录锁；详见 §2） | ✅ |
| 输入词 → 300 ms 内发请求 | `SEARCH-REQ … deltaMs=313` / `297`；`SELFCHECK-DEBOUNCE inputToRequestMs=313 debounceMs=300` | ✅（去抖 300 ms，实测 297–313 ms） |
| **附 URL + 查询词 + 时刻** | 每次搜索前把**每个源的真实地址**连词带时刻落盘（示例）：`SEARCH-REQUEST at=23:36:49.687 term="a" source="ServerA" id="vU9cHAh8ZJ5Xv7El" baseUrl="https://emby.example.com/emby"`；17 条源各一行（含反控源 `http://127.0.0.1:1/`）。失败源另有服务层原话：`聚合搜索：ServerJ 搜索失败（已跳过该源）：HTTP 401 Authorization Required`（同轮出现 401/403/502/525 四种真实状态码） | ✅（URL = 真实 BaseUrl；**不是抓包**，是"我向哪些地址发了什么词"的可核对清单，失败消息再补真实状态码） |
| `聚合搜索` 芯片出跨服去重结果 | `sourceCount=17 mergedCount=118 cards=60` | ✅ |
| 摘要行**三个数各自指名、分别断言** | 面板文本 = `跨 17 台服务器 · 去重后条目数 118 · 面板显示 60 条 · 命中总数 120 · 服务端 TotalHits 120`；五个数**各自有名字**，日志里逐个打印。口径一一写明：`SourceCount`=17 台（成功 7 + 失败 10）｜`M`=`Items.Count`=60（**已被 limit 截断**）｜`T`=**去重后条目数**=118（`MergedCount`，截断前统计）｜`Items.Sum(e=>e.Hits.Count)`=120（`MergedHitCount`，含备用源命中）｜服务端 `TotalHits`=120 | ✅ **未写"三数一致"**（`TotalHits` 在跳过空 Id 前自增、`SourceCount` 是台数，设计上本就不相等）；规格该行同时用了两种 T 口径，本屏**把两个都显示并各自命名**，不替规格二选一 |
| 🔴 不得按"逐源增量刷新"设计 | 无任何增量/逐源回调；只在 `WhenAll` 完成后出一次结果（加载态是一条进度环 + 文案） | ✅ |
| 停掉一台服务器 → 该源名进失败列表且**结果仍在** | 注入不可达反控源 `http://127.0.0.1:1/`（**不改用户配置**）：`failed=10` 含 `坏源(反控)（self）`，同一次 `cards=60` | ✅ **反控通过** |
| 失败源用 `#FFADB6` 列出 | `SummaryFailed` 用 `{StaticResource ServerFailBrush}`；截图像素可见粉红文字 | ✅ |
| 空查询显示历史且**不发网络请求** | `SELFCHECK-EMPTY networkRequests=0`（计数器是**可观测反控制**：跑完全流程仍为 0） | ✅ |
| 同名服务器不得歧义（§7.6） | 服务层只存名字 ⇒ 本屏把 `LastFailedSources` 与建源顺序配对，显示 `ServerD（yzMY）` / `ServerD（ZhNW）`（本机确有两台同名） | ✅ |

---

## 4.1 芯片外观的**像素量测**（验收第 6 条：高 33 / 宽 38·52·80 / 选中 `#9D82C2` 实底 + 深色字）

仪器 = 自写探针（`%TEMP%\t28-host\t28-chip-probe.ps1`，**按列投票**扫芯片带，逐像素 `GetPixel`；单行扫描会被字形像素切断，实测 v1 得 33 段假 run）。对象 = `evidence/t28-agg-bad-screen.png`（1440×759，宿主窗口，内容左沿=客户区 x=0）。

| 量测 | 实测 | 规格 | 判定 |
|---|---|---|---|
| 选中芯片外接框 | `x=16..97  y=85..117` ⇒ **高 33**、宽 82 | 高 33；4 字宽 80 | ✅（宽 82 = 80 + 左右各 1px 描边） |
| 芯片数 | **7 段**连续列 | 聚合搜索+电影+剧+集+视频+合集+演职人员（频道按 §7.4 隐藏） | ✅ |
| 各芯片宽（按列，逐个） | 82 / 54 / 40 / 40 / 54 / 54 / 82 | 80 / 52 / 38 / 38 / 52 / 52 / 80（每字 14 + 左右各 12） | ✅ **逐个** = 规格 + 2（描边） |
| 芯片间距 | `gap=9` × 6 | 8–9 | ✅ |
| 首芯片左沿 | 图像 x=16，窗口隐形边框 ≈8 ⇒ 客户 x≈8 | 内容左沿 + 8 | ✅ |
| 选中芯片文字 | 深色像素 **290**、白色像素 **0** | 选中 = Accent 实底 + **深色字** | ✅ |
| 未选中芯片底色 / 描边 | fill `#2F2F2F`；同带描边像素采样 `#434343`（v1 行扫） | `ControlBg #2F2F2F` + 1px `#434343` | ✅ |

## 5. 有意偏离与"不做死按钮"清单（不静默省略）
| 项 | 处置 | 依据 |
|---|---|---|
| 「频道」芯片 | **不画**（规格授权），并在日志写明原因：无 Live TV 能力判定接口 | §7.4 明文「无该能力时隐藏此芯片」 |
| 音乐（专辑/歌曲/歌手）、有声书芯片 | **画出来但置灰** + tooltip 写明「适配器不在 t28 范围（补屏 t32 接入）」 | 用户硬约束「少项=bug」⇒ 不静默省略 |
| WebDAV 主源 | 芯片行只剩「聚合搜索」+ 明确空态文案（不是空白屏） | §7.4 |
| 结果卡点击 | 抛 `CardActivated` 事件（详情页属 t29）；**未接处理者时落日志 `handler=none`**，不装死 | §6.1 功能绑定 |
| 海报占位 | 无图/加载失败 ⇒ `#FF333333` 占位块（§4.5 实测值），不崩 | §7.3 |
| 角标两义 | `N 源`（§7.6）优先于 `未看集数`/`✓`（§7.3）—— 同屏不叠加 | 两处规格的冲突已显式裁定并写进代码注释 |

---

## 6. 复现方式（宿主与驱动脚本**在 `%TEMP%`，不入库**）

```
宿主工程  C:\Users\Administrator\AppData\Local\Temp\t28-host\t28host.csproj
          （链接仓库同一份源码：Features/Search/*.cs、SearchPage.xaml、Theme/Tokens.xaml + Services.csproj）
驱动      …\t28-run.ps1        -Tag <t> -Mode <词|empty> -Chip <芯片> [-BadSrc]   # 宿主内三路
          …\t28-shell-run.ps1  -Tag <t> -Mode <词> -Chip <芯片> [-BadSrc]         # 真实外壳内集成
截图      …\t28-capture.ps1    -TargetPid <pid> -OutDir <dir> -NamePrefix <t>     # 双通道 + 亮度自证
环境变量  SHELL_SELFTEST_SEARCH / SHELL_SELFTEST_SEARCH_BADSRC / SHELL_SELFTEST_SEARCH_CHIP
          SHELL_SEARCH_LOG（日志落点）/ SHELL_START_PAGE=search（外壳直接进搜索屏）
```
> 宿主**不是交付物**：它存在的唯一理由是外壳当时被在途文件染红（§2 [2]），而卡片要求"必须真跑"。

---

## 7. 未闭合项（交接，不在本卡宣称已完成）

1. **海报加载失败聚集在 `free.lilyemby.com`**：`CARD-IMAGE-OPEN-FAIL … error=E_NETWORK_ERROR`。独立探针（`curl` 匿名请求）实测该主机图片端点返回 **302**，而 `emby.example.com` 返回 200 ⇒ 判为**外部服务器/鉴权面**（且该 URL 由**服务层** `EmbyAggregatedSearchSource` 生成，非本屏拼接）。本屏已按规格退化为占位块，不崩、不静默。**归属建议：服务层/网络面，非 UI 缺陷。** 另 `emptyUrl=13/60` = 这 13 条本身没有 Primary 图（服务层未给 URL）。
2. **`EmbyQueryResult.TotalRecordCount = 0`**（类型搜索路）：面板显示 `服务端 TotalHits 未返回(0)` 而非误导性的 `0`。这是服务层透传，未在本卡改服务层。
3. **卡片点击 → 详情页**：属 t29（`CardActivated` 已留口）。
4. ~~令牌请求~~ **已闭合**：`ui` 已把 `PlaceholderBgColor #FF333333` / `PlaceholderBgBrush` 加进 `Theme/Tokens.xaml`（第 33/48 行），本屏已切到该令牌（不再有字面值）。
5. **深色主题**：宿主已用 `RequestedTheme="Dark"` 对齐外壳（外壳的该修复属 t26/t34 面）。

---

## 8. 纪律自检
- **只写自己的领地**：本轮新增/改动 = `shell/App/Features/Search/*`（5 文件）+ 本次证据文件；`git status` 里他人文件（`MainWindow.*`/`Theme/`/`KernelHost/*`/`Services/*`）**一处未动**。
- **并发**：构建前 `Get-Process dotnet`；两次构建读数都带 HEAD + 时刻；红先归因在途文件（§2）。
- **凭据**：日志在唯一写盘入口统一打码 —— 本卡首跑曾把 `api_key=<32hex>` 写进日志（实测），已修为 `api_key=***` 并重跑全部证据（现存 4 份日志中 `api_key=` 后均为 `***`）。
- **不抢前台的时间窗**：截图工具会短暂前台化窗口（captain 已认可的通道），单次 < 4 s。

---

## 9. 封面通道收口：URI 直连 → 字节通道（captain 2026-09-12 指令；结单后增量）

> 指令原文要点：`SearchPage.xaml.cs:473 new BitmapImage(new Uri(url))` = **URI 直连**（WinUI 自己的栈下载 ⇒ 不经我们的
> User-Agent / 代理策略、也不进磁盘缓存），**换 `HttpClient` 也修不了**；必须改成"先取字节（`ShellHttpClient` + 缓存单点）
> → 再喂 `ImageSourceLoader`"。**本批实现 + 运行验证，`t28` 卡面保持终态、本节为增量记录。**
> ⚠️ §1 表里 `SearchPage.xaml.cs` / `SearchRunner.cs` 的 bytes/sha12 是**结单时点**身份，本批已变；当前身份以本节为准。

| 文件 | 字节 | sha12 | 内容 |
|---|---|---|---|
| `evidence/t28-img-byte-channel.txt` | 11,228 | `E3187A0E4479` | 本批的**逐字**读数：改动清单（文件+行号+改为）、FINAL 跑日志行、抓图读数、A/B 反控、过程如实记录。**身份已刷新（2026-09-12 20:18）**：原 9,447 / `FB7CFAE64281` ⇒ 现 11,228 / `E3187A0E4479`（本次追加"更正块"，把 04:0x 那次"红树"标为历史读数；行内容未删） |
| `evidence/t28-img-byte-window.png` | 1,171,464 | `59385B52E197` | **通道 = 窗口级 `PrintWindow`**（`shell\tools\popup-capture.ps1 -Auto -ExpectClassLike 'WinUIDesktop*'`，`channel=printwindow`，**非屏幕拷贝**）：1440×759，`nonBlackPct=93.8`；人眼核对 = 「电影」芯片 + 摘要行 + **10 张真实海报** + 状态栏 |
| `evidence/t28-img-byte-window.txt` | 1,333 | `CCB9DEAE9812` | 上图侧车：hwnd/pid/class/rect/通道/焦点前后/PNG 哈希/非空判定（`focusChanged=False`、`noScreenCopy=true`） |

**当前身份（本批）**：`SearchPage.xaml.cs` = 37,668 B / `C60D1A456D28`（mtime 04:02:01.356）；`SearchRunner.cs` = 21,486 B / `DCF7F0B4037C`；
FINAL 二进制 `AIPlayer.Shell.dll` = `98D353C9B55B` / 1,614,848 B / mtime 04:04:15.413（**晚于两个源文件** ⇒ 读数出自最终源码）；
构建 `EXIT=0 / 0 个错误 / 1172 个警告`（HEAD `14cb80e`）。

**改动清单（文件 + 行号 + 改为）**
1. `SearchRunner.cs:152` 新增 `public … ImageCacheManager Images => Registry().Images;` —— **图片取字节唯一入口**
   （`ServiceRegistry.Images` = 进程级 `ImageCacheManager.Default`）。App 侧**没有** `new ImageCacheManager(...)`。
2. `SearchPage.xaml.cs:488-491`：原 `ToCardVm` 里的 `new BitmapImage(new Uri(...))` 整段**删除**，替换为"为什么不能这么做"的注释。
3. `SearchPage.xaml.cs:502` 新增 `FillCardImagesAsync`：`Images.GetOrFetchAsync(url, token)` → `ImageSourceLoader.LoadAsync(bytes)`
   → `vm.SetImage(...)`；逐张顺序取；整批预算 25 s（`:90`）；失败只计数、保持占位底色；顶层 `catch (Exception ex)` 兜底。
4. `SearchPage.xaml.cs:416-417`：渲染路径 `_lastCardVms = vms; _imagesFill = FillCardImagesAsync(vms);`（**不 await**）。
5. `SearchPage.xaml.cs:840` 新增 `CardVm.SetImage`（赋值 + `Raise(nameof(Image))`；`{Binding Image}` 是 OneWay，不通知就不刷新）。
6. `SearchPage.xaml.cs:794-801`：删掉无条件的 `Task.Delay(2500)`，改为"事件面追上取数面 / 1.5 s 不再变 / 上限 8 s"的等待；
   读数扩为四口径：`opened/failed`（控件事件面）、`byteRequested/byteFetched/byteNull`（取数面）、`boundNow`（交棒面）、
   `bytePartitionOk` + `openedLEFetched`（两条可失败结构不变量）。

**读数（`%LOCALAPPDATA%\AIPlayer\logs\t28-search.log`，逐字）**
- `SELFCHECK-IMAGES cards=59 emptyUrl=0 opened=0 failed=0 byteRequested=59 byteFetched=59 byteNull=0 boundNow=59/59 bytePartitionOk=True openedLEFetched=True renderWaitMs=8047`
- `SEARCH-IMG-BYTE requested=59 fetched=59 null=0 cards=59 emptyUrl=0 partitionOk=True abortedByBudget=False`（两次搜索各一次，第二次同 59/59）
- 判定：**取数面 59/59**、**交棒面 59/59**、`bytePartitionOk=True`；渲染面见上面的 PNG。

**A/B 反控（关键，直接决定"用哪个数当判据"）**：同一环境/同页面/同服务器，只换二进制 ——
旧代际 `14C51D30D6A8`（URI 直连版）`opened=23`；本批 `98D353C9B55B`（字节通道版）`opened=0` **而截图里 10 张海报清晰可见**。
⇒ 字节通道喂进去的 `BitmapImage` **不触发 `Image.ImageOpened`** ⇒ `opened` **不能**当本通道的渲染判据（代码里**当时版本** `:768-780` 已就地写明）。⚠️ **行号会漂、锚文本不会**：当刻该两处注释在 `shell/App/Features/Search/SearchPage.xaml.cs:975 / :981`（该文件 = 51,272 B / `913B48616CB1` / 1,092 行，mtime 2026-09-12 04:32:37.014）；`:768-780` 是本节写下时那一版的坐标，**引用请用锚文本**（"不触发 `Image.ImageOpened`…不能当判据" / "位图交出去 ≠ 已渲染…实测 opened=0"）。
⇒ 同时说明旧代际的 `opened=23/59` 是"23 张被 WinUI 自己的栈取到了、其余 36 张静默空白"，正是本次要修的东西。

**诚实交代**
1. 04:02:30 的构建曾**红 3 个错误**，全部在**他人文件** `Features/Aggregate/T31SelfTest.cs(407,40)/(435,36)`（命名实参尾随逗号），
   该文件 mtime 04:02:42 / `git status=M` ⇒ 在途编辑；我**未改他人文件**，只告警（ui2 04:03:47 修好），绿后重编重跑。
   > ⚠️ **更正（2026-09-12 20:18 append，`ui2` 提请）**：上面这条是**凌晨 04:0x 的历史读数，不是当刻状态**。
   > `Features/Aggregate/T31SelfTest.cs` 之后被两笔提交重写（`3969d9e` 06:56:28、`50ef886` 07:24:47）⇒ 当刻 = **41,595 B / `25EE45F101FF` / 691 行 / porcelain 空**；
   > `:407` 现为 `sb.AppendLine("  分区标题（原样）= " + titles);`、`:435` 为**空行**，**命名实参尾随逗号形态已不存在**。
   > **当刻树是绿的（本件复核）**：`dotnet build shell/App/AIPlayer.Shell.csproj -c Debug -p:OutDir=E:\t28-check\` ⇒ **EXIT=0 / 0 错误 / 1230 警告 / `CS1525` 命中 0**（`20:17:52.690 @HEAD a02092a`，产物 `AIPlayer.Shell.dll` = 1,849,856 B / `4130DA9158D5` @20:18:12）。
   > ⇒ **不存在"当前有一棵红树"**；本条只应被读作"04:02 那次的历史过程记录"。
2. 04:02:36 抓到的中间代际图（`F72F80DF2146`）出自上一条绿构建 `590EDE728F8E`，已被 FINAL 抓图**同名覆盖**；本表只记 FINAL 身份。
3. 本轮实跑会真实写入图片磁盘缓存 `%LOCALAPPDATA%\AIPlayer\cache\images`（属产品行为）；日志 URL 一律 `key=<sha1 前 12>` 打码。
4. 本通道**只覆盖搜索结果页**；`Features/Aggregate/Shared/ImageSourceLoader.cs` 是共用件（非我领地），**只调用未修改**。
   `SEAM②`（hint：逐源增量接线）若也要重绘图片，按同一字节通道走（同 `Images` + `ImageSourceLoader`）。

---

## 10. SEAM② 落地：聚合搜索「逐源增量」（`t32`，规格出处 `ui2`，2026-09-12 04:22–04:29）

> 规格 = `shell/App/Features/Aggregate/SEAM2-SEARCH-INCREMENTAL-WIRING.md`（出题 `ui2` ⇒ 落地 `ui3`，`ui2` 独立复核）。
> 服务层件 = `t44` 的 `AggregatedSearchService.SearchIncrementalAsync` + `AggregatedSearchProgress`。**本卡修改了 `t28` 的终态产物**
> （`SearchRunner.RunAsync` 追加可选参数、`SearchPage` 增量渲染），按 Captain 授权承载；`t28` 卡面保持终态，本节为增量记录。
> 身份：`SearchRunner.cs` 22,143 B / `66C5AA72F295`；`SearchPage.xaml.cs` 49,611 B / `91EE4C3FD0A4`；
> 读数二进制 `AIPlayer.Shell.dll` = 1,627,648 B / `AFCADFF22371` / mtime 04:27:14.983（**晚于两个源文件**）；构建 `EXIT=0 / 0 个错误 / 1172 个警告`（HEAD `a659980`）。

| 文件 | 字节 | sha12 | 内容 |
|---|---|---|---|
| `evidence/t28-seam2-incremental.txt` | 19,914 | `294822C8E866` | 改动清单（文件+行号+改为）、跑 A（聚合 17 源）与跑 B（类型单源回归）逐字读数、**五条反控逐条判定**、76>60 语义与界面标注、**captain 三条裁定落地 + 跑 C 读数**、诚实边界；**文末追加更正块**（append-only，2026-09-12）：① 全部读数属 `a659980`/04:2x 代 + 当刻两源身份；② 76>60 **已裁定并落地**（`SearchPage.xaml.cs:468-482`）⇒ `:88` 引的界面话是裁定前形态、当刻盘上已不存在；③ `failed=9` 须拆为 1 注入反控 + 8 自然失败；④ 门禁 `checks=9` 为旧版（当刻 10 项） |
| `evidence/t28-seam2-midflight-window.png` | 1,084,360 | `A2834C5A90D1` | **通道 = 窗口级 `PrintWindow`**（非屏幕拷贝）：**搜索进行中那一帧**（抓取 04:37:35.256）；人眼核对 = 「聚合搜索」选中 + `正在搜索「a」 · 已到 15/17 台服务器 · 去重后条目数 178 · 去重后命中总数 180 · 面板显示 60 条 · 命中总数 180` + `失败 7 台：…` + 真实海报网格（含角标 `2 源` 的合并卡） |
| `evidence/t28-seam2-midflight-window.txt` | 1,344 | `3B8BACC0FE4D` | 上图侧车：hwnd/pid/class/rect/通道/焦点前后/PNG 哈希（`focusChanged=False`、`noScreenCopy=true`） |

**五条反控（每条可 False，原始行在证据文件里）**

| # | 判据 | 读数 | 判定 |
|---|---|---|---|
| ① 一个个显示 | `renders>=2 且 firstRenderMs < terminalMs` | `renders=17 firstRenderMs=47 terminalMs=40302`（差 40,255 ms） | ✅ |
| ② 同键不出现第二张卡 | 同键最大卡数 == 1 | `sameKeyMaxCards=1`（178 个去重条目 / 17 发滚动） | ✅ |
| ③ 四数各自具名、取自回调字段 | 终值那一发的四数 == 运行结果四数 | `final…=180/178/180/60` vs `result…=180/178/180/60`，`finalEqualsResult=True` | ✅ |
| ④ 坏源不清空已到达结果 | 坏源那一发后卡片数不减、坏源进失败列表 | 坏源 04:27:2x 到达后 `cardsRendered` 继续增至 76，终态 `isFinalSeen=True`，`failed=9`（含 `坏源(反控)（self）`） | ✅ |
| ⑤ 到达序 vs 终值序 | 必须显式处置、不得沉默 | `finalOrderKept=arrival`（理由：切序会把用户正在看的卡片挪位；重排会打断就地更新） | ✅ |

**`progress == null` 分支的回归面**：跑 B（类型搜索，单源）`renders=0` + `incrementalMode=False` + `wholeSetPartitionOk=True` +
`byteRequested/byteFetched=59/59` + `boundNow=59/59`，摘要行与 `cards=59` 与 `t28` 结单时同形 ⇒ **原路径逐字未变**
（该路 `finalEqualsResult=False` 是**不适用**——没有回调 ⇒ `final*` 全为 −1，不是失败）。

**新发现并已处置的语义（请 captain/ui2 裁定口径）**：到达序累计**会多于**终值窗口（本跑 `rendered=76 / terminalCards=60`）。成因：每发快照各自按 `limit=60`
截断，而"当时的 60 条"组成随后到源变化 ⇒ 早期进过屏、后来掉出窗口的卡仍留在屏上。处置 = 屏上保留 + **界面把差异写清楚**
（摘要行已带「到达序累计显示 76 条：…最终窗口按 60 条截断；屏上保留已出现过的卡片，不抽走」）+ 日志 `SEARCH-INCREMENTAL-OVERFLOW`。
若要"屏上严格等于终值窗口"，属另一种语义（收口时抽卡、可能跳位），**本批不擅自实施**。

**未证 → 已补像素（captain 2026-09-12 裁定后）**：增量途中"画面在动"原先只有日志侧证据；现已补一张**搜索进行中**的窗口级 `PrintWindow` 单帧
（`evidence/t28-seam2-midflight-window.png`，摘要行逐字 `正在搜索「a」 · 已到 15/17 台服务器 · …`）⇒ 日志面 + 像素面都有。
**裁定落地**：界面话改 `（列表里共 N 条）`（8 字、用户话、零术语；理由进 ToolTip/注释/日志）；到达序累计加硬上限 `IncMaxRenderedCards=240`
（超限落 `SEARCH-INCREMENTAL-CAPPED`、界面数字仍如实）；读数行带 `capped/dropped/cap`。新代际：`SearchPage.xaml.cs` 51,272 B / `913B48616CB1`；`dll 618C068D01A0` @04:33:14.031。
