# AIPlayer.Shell.Services（服务层类库）

> t7 交付物。Dart 服务层（`rebuild/ai_player/lib/core/**`）→ C# 的等价移植；协议语义不改，
> 端点/头/参数以 `reversed/FlutterApp/SERVICE_API.md` 实证为准，字段命名同该文档。
> 工程归属与命名依据：`shell/docs/DESIGN.md` §2 D1（`shell/Services/Services.csproj`，AssemblyName `AIPlayer.Shell.Services`）。

## 1. 构建与运行准入（本机实测结论）

| 项 | 值 | 依据 |
|---|---|---|
| TFM | `net9.0-windows10.0.22621.0` | 内核锁 net9（`reversed/MpvHost/AIPlayer.MpvHost.csproj:5-6,19`，降 net8 会 `NU1201`） |
| 平台 | `x64` | DESIGN §2 D2 |
| 本工程属性 | **纯类库**：不设 `RollForward` / `UseWinUI` / `WindowsAppSDKSelfContained` | 类库不承担进程入口（`shell/docs/WORKSPACE.md` §2 分工） |
| 构建命令 | `dotnet build "E:\AI Player\shell\Services\Services.csproj" -c Debug` | t7 verify 字段 |

> ⚠️ 本机**无 .NET 9 运行时**（仅 8.0.23 / 8.0.25 / 10.0.8）。类库本身不受影响，
> 但**任何运行本服务层的宿主**（测试工程、App、临时控制台）必须带
> `<RollForward>Major</RollForward>`，否则 net9 产物启动即退 `-2147450730`。

### 1.1 宿主工程的属性前提（若宿主还要加载内核类型）

本工程是纯类库，下面这些**与本工程无关**，但 `shell/Tests` 或 `shell/App` 这类宿主一旦要加载内核类型（`WinUISample.*`）就必须齐全，
否则不会失败在「框架没装」那一层，而是更隐蔽的地方：

```xml
<TargetFramework>net9.0-windows10.0.22621.0</TargetFramework>
<Platforms>x64</Platforms>            <!-- 必须显式：Platform 默认 AnyCPU ⇒ GetWindowsAppSDKNativePlatform 挑不到 win10-x64 的 Msix -->
<PlatformTarget>x64</PlatformTarget>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
<RollForward>Major</RollForward>
```

- 依据（外部实证，非本工程结论）：`reversed/DotNet/AIPlayer.MpvHost.reconstructed.csproj:22-26` 同时设了 `Platforms=x64` /
  `RuntimeIdentifiers=win-x64` / `RuntimeIdentifier=win-x64`，其注释指出 `WindowsAppSDKSelfContained` **需要明确架构**
  才能选到 `win10-<arch>` 的 Msix（`Microsoft.WindowsAppSDK.SelfContained.targets` 的 `GetWindowsAppSDKNativePlatform`）。
- 若宿主**不**加载内核类型（例如只跑本服务层的自检），只需 `RollForward`；`Tests` 是 testhost 宿主，**RollForward 必须有**。

**⚠️ 不要依赖 ProjectReference 传递（captain 口径更正 + verifier U11 实跑，三用例）**

| 配置 | 结果 |
|---|---|
| 被引用类库 `SelfContained=true` / Tests `false` | 通过 4 / exit 0，DLL **在**测试输出目录（靠传递生效） |
| 两者都不设（反控制） | 失败 2 / exit 1，DLL **不在** |
| 仅 Tests `true` | 通过 4 / exit 0 |

机制：VSTest 宿主 `testhost.exe` 的 `BaseDirectory` = **测试输出目录**；内核程序集带 `[ModuleInitializer]`，
会从该目录加载 `Microsoft.WindowsAppRuntime.dll`。⇒ **能靠传递获得，但不可见、且只引用纯类库时链就断了**。
因此：**`shell/Tests` 一旦要触达内核/WinUI 程序集，必须自己显式写 `WindowsAppSDKSelfContained=true`**（并保留 `RollForward=Major`）；
本工程（纯类库）保持**不带**该属性。

## 2. 端到端自检（无真服务器也能复现）

服务层是类库、不自带进程入口；自检入口是公开 API：

```
AIPlayer.Shell.Services.SelfCheck.ServiceSelfCheck.RunAsync(Action<string> write)
```

用任意带 `RollForward` 的宿主调用即可（本仓库外的临时宿主示例）：

```powershell
# %TEMP%\aiplayer-selfcheck-host\SelfCheckHost.csproj
#   <OutputType>Exe</OutputType> / net9.0-windows10.0.22621.0 / x64 / <RollForward>Major</RollForward>
#   <ProjectReference Include="E:\AI Player\shell\Services\Services.csproj" />
dotnet build "$env:TEMP\aiplayer-selfcheck-host\SelfCheckHost.csproj" -c Debug
& "$env:TEMP\aiplayer-selfcheck-host\bin\Debug\net9.0-windows10.0.22621.0\win-x64\SelfCheckHost.exe"
```

**覆盖链路**：认证 → 错误口令被拒 → 列库 → 列条目 → 取详情 → 生成可播放流地址 → 实际取到流字节 →
`PlaybackInfo`（含降级路径）→ 媒体段 ticks→ms → 进度上报三连（`/Sessions/Playing[/Progress|/Stopped]`）→
收藏增删 → 鉴权头生效 → 凭据 DPAPI 加密与回填 → 服务器配置持久化/reorder/`passwordOf` →
内核契约 Base64(JSON) 往返 → `AppSettings` 往返 → `SettingsService` 落盘 → 跳过片段容错解析/归一化/三源聚合与缓存 →
TODB 章节与雪碧图归一化。

**诚实边界**：链路走的是内置 **mock 服务器**（`Mock/MockEmbyServer.cs`，端点形态按 SERVICE_API §1 构造）。
**mock 通过 ≠ 真服务器通过**，报告须如实标注（`WORKSPACE.md` §3）。

## 3. 分层约定（重要）

- **服务层不引用内核程序集**（避免把内核的 Windows App SDK 模块初始化器带进类库与测试进程）。
  DESIGN §4.2 要求「复用内核类型」的点（`TodbChapter`/`TodbSprite`/`PlayerShortcuts` 等），
  由 `Models/HostContractModels.cs` 提供**同形契约模型**，`ToJson()`/`ToBase64()`
  产出的就是内核可吃的形态，映射动作留在 App 层。
- HTTP 统一走 `Http/ShellHttpClient.cs`（`System.Net.Http` + `System.Text.Json`，无第三方）。
  **默认直连、不走系统代理**（对齐 Dart 侧仅在显式配置时才设 `findProxy`）。
- 凭据统一走 `Credentials/SecureKvStore.cs`（DPAPI，用户作用域；DPAPI 不可用时降级为可逆混淆并
  **显式暴露** `IsEncrypted`/`DegradedReason`，不静默降级）。
- **大整数一律用 `JsonRead.LongOrNull`**：Emby ticks 极易超过 `int`（100 分钟 = 6e10 ticks）。
  本工程开发期已因 `IntOrNull` 踩过一次静默截断（`3300000000 ticks → 2147483647`），自检第 9/5 步专测此项。
- 组装入口：`ServiceRegistry.cs`（显式手工组装，等价原版 `app_providers.dart` 的 provider 语义）。

### 3.1 服务器配置的构造点：生产唯一 = `Instance`（**红线**）

- **生产代码只允许用 `ServerConfigStore.Instance`** —— 它是「环境解析实例」：数据根按本机解析、叠加原版 `accounts.json` 兼容读、合并旧 Roaming 根（只读）。
- **`ServerConfigStore.At(serversFilePath, credentials)` 是测试/多实例夹具**：它**不读原版 `accounts.json`、不合并旧 Roaming 根**。
  ⇒ **要看到本机原版那 16 台，必须用环境解析实例；显式 `At(path, …)` 不会回落**。实测教训（`shell/App` 侧 21:28 记）：同一个数据根，用 `At(…)` 构造时服务器列表 = **0 台**，改用 `Instance` 后 = **16 台**。
- 该默认值是**刻意的、不得改成 `true`**：`At(…)` 一旦默认回落原版，任何跑在临时根上的测试都会读到本机真实 `accounts.json`，把「夹具」变成**跨上下文泄漏**（本机实测会凭空多出 16 台）。
- 若将来确实需要「**显式路径 且 读原版**」的组合语义，请**新增一个名字里带生产含义的工厂**（例如 `FromOriginalAccounts(path, creds)`），**不要改这个默认值**。
- `Instance` 的路径解析发生在**每次 `Load()`**（构造时传 `originalAccountsFile: null`）：原版文件被移走 ⇒ 0 台；放回 ⇒ 恢复（自检有专测）。
- 代码侧引用点（**用可核对的形态，不引行号**）：`shell/App/Features/Servers/ServersPage.xaml.cs` 的 `_store = ServerConfigStore.Instance;`；`shell/App/Shell/ShellState.cs` 的 `Refresh()`。现状：**`shell/App` 树内 `ServerConfigStore.At(` 的真实调用为 0 处**（仅注释里出现）。

### 3.2 `/segments` 响应面的**大小写红线**（红线**只适用于响应面**）

- 外层键必须是 **PascalCase `Segments`**：内核 `SegmentUpdatePayload` **没有** `[JsonPropertyName]`，且用 `JsonSerializer.Deserialize<SegmentUpdatePayload>(text)` 的**默认（大小写敏感）**选项 ⇒ 写成 `segments` 会**绑定为空**，随后按「Segments 为空」处理。
- 内层条目必须是 **camelCase**：`HostNavigateSegmentOption` 显式标了 `[JsonPropertyName("type")]` / `("startMs")` / `("endMs")` / `("source")`。
- ⇒ **一节之内两种大小写并存是设计如此，不是笔误**：**外层 Pascal、内层 camel**。
- 状态码语义（**不要把它写成「静默」**）：`204` = 成功；`400` = 请求体不合法（空体、JSON 解析失败、或 `Segments` 为空/缺省 —— **明确报错，不是静默忽略**）；`405` = 方法不是 POST；`404` = 路径不是 `/segments`；`500` = 其它异常（兜底）。
- 依据（反编译源码，只读、不可改）：`reversed/MpvHost/WinUISample.Services/PlayerUpdateServer.cs`（外层 payload 定义；`:98` 405、`:104` 404、`:112`/`:119`/`:137` 400、`:134` 204、`:143` 500）与 `reversed/MpvHost/WinUISample.Models/HostNavigateSegmentOption.cs`（四个 `[JsonPropertyName]`）。`reversed/` 是冻结基线，其行号不随本项目演进漂移。

> 🔴 **运行状态更正（2026-09-11，P0 实测）：本节上面写的是「代码意图」，不是「运行事实」——通道存在 ≠ 通道在跑。**
> 内核侧把监听前缀**写死 `http://127.0.0.1:0/`**，而 `HttpListener` **不接受端口 0** ⇒ `AddPrefixCore` 抛 `HttpListenerException (87)`
> （`ERROR_INVALID_PARAMETER`）⇒ `Start()` 失败、`UpdateUrl` 恒为 `null` ⇒ **该通道在此前（原版同样）从未运行过**
> （三次实测同一异常：22:42:54 / 22:56:30 / 22:58:15）。证据：`shell/docs/P0_LIVE_CONTROL_FEASIBILITY.md` §3.1–§3.3。
> 修复归 **E-P1（t38）**：`kernel/` 分支已把 `PlayerUpdateServer.Start()` 改成"探一个空闲回环端口再注册"，
> 并实测 `POST /segments` ⇒ **204**（反控 空 Segments⇒400 / 错路径⇒404 / GET⇒405 全中）。

## 4. 目录 → DESIGN 映射

| 目录 | 对应 DESIGN §4 条目 |
|---|---|
| `Http/` | §4.2 附注（`core/http/http_client.dart`） |
| `Storage/`、`Infra/` | §4.1 #3、§4.2 `json_storage` |
| `Credentials/` | §4.1 #30 password_store（+ `secure_kv_store`/`dpapi`） |
| `Logging/` | §4.1 #16 debug_log（Dart 名 `player_log_service`） |
| `Models/` | §4.2 全表 + §4.1 #6 |
| `Emby/` | §4.1 #19、#31、§4.2 util 三项 |
| `Navidrome/` | §4.1 #29 |
| `AudioBookshelf/` | §4.1 #10、#9、+ `abs_timeline` |
| `WebDav/` | §4.1 #39、#40 |
| `Trakt/` | §4.1 #37、#38 |
| `Segments/`、`Todb/` | §4.1 #32、#36 |
| `Settings/`、`Servers/`、`Icons/` | §4.1 #34、#33、#23 |
| `Lyrics/`、`Update/`、`Search/`、`Aggregation/` | §4.1 #25、#?update、§5 #135、§4.1 #1 |
| `Mock/`、`SelfCheck/` | 验收⑤ 的本地 mock 与可复现自检（非 Dart 平移） |

Deviation/未覆盖清单见下节。

## 5. 覆盖核对与偏差（按 DESIGN r20 口径重算）

**规模**（**快照**：截至 commit `6e851ee`、2026-09-11 实测 —— 后续任何改动都会让它变，引用时请连提交号一起引，或现场重测）：
- **`.cs`：67 个文件 / 19,318 行**（行数口径 = `[IO.File]::ReadAllLines($f).Count` 逐文件求和）
- **非 `.cs`：6 个文件** —— `Services.csproj`、`README.md`、`API_SURFACE.md`、`KERNEL_DEPENDENCY_CLOSURE.md`、`kernel-deps-closure.json`、`kernel-deps-closure.ps1`
- **合计 73 个文件**（统计口径：`Get-ChildItem shell/Services -Recurse -File` 并**排除 `bin/` 与 `obj/`** ——
  obj 下含生成的 `*.AssemblyInfo.cs`/`AssemblyAttributes.cs`，Debug/Release 各一套，不排除会多出 4 文件/56 行）
- ⚠️ **不要用 `Get-Content | Measure-Object -Line`**：它低估（**跳过空行**），本文件早期版本因此把当时的行数报成了 15,582
  （`ab812f9` 的 commit subject 仍写着那个旧数，历史不可变，以本节为准）。
- ⚠️ **也不要裸用 `Get-Content` 数行数**（**第三种坏仪器**，2026-09-11 实测）：PowerShell 5.1 的默认编码是 **ANSI(CP936)**，
  读 UTF-8 中文文件时会**吞掉作为双字节尾字节的 `0x0A`**（机制由 `verifier` 独立复算）⇒ 行数**系统性偏低**，中文同时变 mojibake。
  同一文件、同一时刻实测**本文件**：裸 `Get-Content` = **110 行** ｜ `Get-Content -Encoding UTF8` = **172 行** ｜
  `[IO.File]::ReadAllLines().Count` = **172 行**（后两者一致 = 真值）。偏差随「行尾前一汉字的第二字节 ≥ 0x81」的比例放大：
  同批实测 `WORKSPACE.md` 786 vs 1,928、`VERIFY_S1.md` 1,217 vs 1,777、`T12_CALLBACK_SURFACE.md` 120 vs 204。
  ⇒ **行数一律用 `[IO.File]::ReadAllLines($f).Count`**（或显式 `-Encoding UTF8`）；**字节数**另用 `git cat-file -s` 或
  `[IO.File]::ReadAllBytes` 作独立第二测；**同一份读数必须来自同一条命令、同一条路径、同一时刻**（哈希/字节/行数拼自不同次测量是今晚踩过的坑）。
- ⚠️ **也不要在 PowerShell 里用 `>` 落盘二进制**（**同一个坑的第三个面：编码 → 量纲 → 重定向**）：实测 `git show <blob> > tmp`
  输出的是 **UTF-16LE（BOM `FF FE`）**，同一 blob 得 **33,502 B**，而真值（`git cat-file -s`）是 **24,648 B** ⇒ **非字节精确**；
  **且会把行尾从 LF 改成 CRLF（每行 +1 字符）** —— 所以不要用 PS 的 `>` 落盘，**哪怕你不关心编码**
  （实测账目：PS 副本码元 **16,750** = cmd 的 16,551 **+ 199**（= 行数），CR=199 / LF=199 / CRLF 对=199，字节账 `2×16,750 + 2 = 33,502` 逐字节吻合）。
  必须用 `cmd /c "git cat-file blob <sha> > tmp"`（`>` 在引号内 ⇒ 由 **cmd** 做字节重定向，实测 24,648 B = 真值）。
  反控制（证明这条机制可翻转）：把该 UTF-16LE 副本**只删掉前 2 字节 BOM**、内容一字不动 ⇒ 读侧立刻崩成 **526/520 行**（199 → 526）⇒ 「BOM 决定解码器」成立。
含本轮的四处新增：`Models/PlayerShortcuts.cs`、`Models/TodbModels.cs`、`Settings/MpvConfService.cs`、
`Settings/MpvHostPlayerSettingsStore.cs`。

**DESIGN §4 服务层目标路径覆盖**：目标 **63** 项 → **已建 50 项**（脚本核对：正则抓 `Services/*.cs` 后逐个 `Test-Path`）。
未建的 13 项及归属（**不是遗漏，是按 DESIGN r20 的任务划分**）：

| 未建项 | 归属 | 依据 |
|---|---|---|
| `Infra/ImageCacheManager`、`Infra/DiskCacheStore`、`Infra/CacheStatsService`、`Infra/DanmakuDiskCacheStore`、`Infra/DataMigrationService`、`Infra/DeviceIdService`、`Infra/ConfigPortService`、`Infra/AudioPlaybackStateStore`、`Infra/WindowsProxy`、`ExternalMpv/ExternalMpvIpc`、`ExternalMpv/ExternalMpvService`（11 项） | **t14**（S5 体验设施，服务侧） | 与 t14 任务卡清单逐项对应 |
| `Media/AudioPlayerService`（1 项） | **登记偏差 G2** | DESIGN §2 D8：MVP 音乐走内核 mpv；该项属可选实现 |
| `Models/MusicModels.cs`（1 项） | **t13**（音乐屏） | 原版 `music_models.dart` 在 `rebuild/` 中不存在，凭空造类型违反 DESIGN §1.2；待音乐屏真实取数面定型后落地 |

**r20 口径变化后已跟随调整的落点（本次已做）**：
- `Services/Kernel/*` **不再属服务层**（S-2：`ai_player_host_service` 归 `App/KernelHost/`）——本表不再把它算作服务层缺口。
- `mpv_conf_service` 与 `mpv_host_player_settings_store` 由 `Services/Kernel/` 改归 **`Services/Settings/`**，
  已按新落点实现（纯文本/KV，无内核依赖）：`Settings/MpvConfService.cs`、`Settings/MpvHostPlayerSettingsStore.cs`。
- `player_shortcuts.dart` / `todb_models.dart` 由「复用内核类型」改为**服务层自建同形 DTO**，
  已按新落点落在 `Models/PlayerShortcuts.cs`、`Models/TodbModels.cs`（原 `HostContractModels.cs` 中的同名类型已迁出，避免重复定义）。

**硬规则 S-1 合规自检（可复跑）**：`shell/Services/**/*.cs` 中 `WinUISample` 命中 12 处，**全部为注释**，
**非注释命中 0** —— 服务层不使用任何内核类型。

> 复跑口径（DESIGN §9.6 规则 E「匹配到注释 ≠ 匹配到代码」）：引用闭包/程序集引用类检查**必须解析 XML 或 IL，不得用文本正则**。
> ```powershell
> $xml = [xml](Get-Content shell\Services\Services.csproj -Raw -Encoding UTF8)
> $xml.SelectNodes('//*') | ForEach-Object { $_.Name } | Where-Object { $_ -like '*Reference*' } | Group-Object
> # 期望：只有 PackageReference x1（System.Security.Cryptography.ProtectedData）
> ```
> 反例（**不要这么查**）：`Select-String '<Reference|ProjectReference'` 会命中 csproj 里的**注释**，返回 1，看起来像「有内核引用」。

## 6. t14（S5 体验设施）落地口径 —— 已定，待 t12 解锁后执行

来源：DESIGN r25 §4.1 第 14 项（弹幕缓存裁决）＋ S-1/S-2 ＋ reviewer 2026-09-11 的复核口径。

**交付面**：`Infra/ImageCacheManager`、`Infra/DiskCacheStore`、`Infra/CacheStatsService`、`Infra/DanmakuDiskCacheStore`、
`Infra/DataMigrationService`、`Infra/DeviceIdService`、`Infra/ConfigPortService`、`Infra/AudioPlaybackStateStore`、
`Infra/WindowsProxy`、`ExternalMpv/ExternalMpvIpc`、`ExternalMpv/ExternalMpvService`（11 项，均在本工程内，不另立 csproj）。

**三条硬约束（验收会逐条查）**：
1. **零内核引用（S-1）**：不得 `Reference`/`using` 任何 `WinUISample.*`。弹幕缓存按裁决 **(c)** 落地：
   服务层**自实现**、**磁盘目录与文件格式与内核 `WinUISample.Services.DanmakuDiskCache` 对齐**（比对其磁盘布局，而不是引用其类型）；
   如需复用内核实现，只能在 **App 侧做适配器**（可选接口 `IDanmakuCache`），服务层不引用。
2. **格式一致性要实跑对照**：不能只声称「格式一致」——给出与内核缓存目录/格式的对照实测（文件命名、层级、序列化形态）。
3. **必须有反控制，且必须双向（DESIGN §9.5 推论 + reviewer 加强建议）**：
   - **坏数据侧**：故意喂坏/空缓存文件 → 必须报出**明确错误**，而不是解析 0 条就当通过（§9.5 实例二就是「没读到数据」被判成「校验通过」的假通过）。
   - **好数据侧**：合法数据 → 必须**成功**，且产出的键/条目集合 **⊆ 白名单**（§9.5 追加推论：断言集合边界，而非只锁已知坏值）。
   ⇒ 只做单侧（只验「坏数据失败」）会被「永远报错」的实现骗过；两侧都过才算这条断言成立。



