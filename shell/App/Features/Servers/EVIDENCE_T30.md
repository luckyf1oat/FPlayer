# t30（U-D 添加服务器 + 服务器右键菜单 + 设置）交付与运行证据

> 作者 = `ui3`｜卡 = t30（P5）｜规格 = `shell/docs/UI_SPEC_SHELL.md` **§8 添加服务器** / **§3.1 右键菜单 11 项裁定** / **§9 设置（只做非账号项）**
> 参照实测 = `HILLSLITE_UI_ANALYSIS.md` §4.3（五字段占位与必填性）/ §4.5（菜单底 `#1F1F1F`、悬停 `#2C2C2C`）

## 0. 三态声明（逐条分开写）

| 三态 | 到哪一步 | 证据 |
|---|---|---|
| **逆向到了** | 五字段的占位文案/必填性取自 §4.3；菜单 11 项原文与色值取自 §4.5；设置 7 行取自 §9 与 §2 逐屏表 | 本文引用处标章节 |
| **重建实现了** | 5 个新源文件 + 1 处扩充（`ServersPage` 的「添加」入口）在盘；宿主与外壳均编译通过 | §2 构建读数 |
| **运行验证过了** | 三屏各自真起窗：**设置页**跑全流程自检（读→改→读回→备份→还原→反控制）；**对话框**真立起来并截图；**菜单**真构造并逐项读 `IsEnabled` | §3–§5 |

## 1. 交付物

| 文件 | 字节 | sha12 | 作用 |
|---|---|---|---|
| `Features/Servers/AddServerDialog.xaml` | 3,396 | `89AC747BC228` | 屏 A 界面：类型 / 显示名称 / 地址 / 端口 / 根路径(WebDAV) / 用户名 / 密码+显示切换 / 登录·取消 |
| `Features/Servers/AddServerDialog.xaml.cs` | 22,929 | `44777DDD7477` | 屏 A 逻辑：纯校验 + 归一化 + 逐 Kind 真登录 + 落盘（凭据经 DPAPI）+ 校验表自检 + 真加一台自检 |
| `Features/Servers/ServerRowContextMenu.cs` | 19,431 | `8F8CA057D3B2` | 屏 B：11 项菜单 + 7 个 handler + 4 项置灰理由 + 只读条目纪律 + `AttachTo` 契约 |
| `Features/Servers/ServerPrompts.cs` | 6,555 | `38B601CE5F60` | 文本/口令/确认/多选/单选 五个小对话框（深色主题自带） |
| `Features/Settings/SettingsPage.xaml` | 13,380 | `FBBE50DF3E79` | 屏 C 界面：§9 七行 + 重置，卡片列表 + 分组标题 + 右控件 |
| `Features/Settings/SettingsPage.xaml.cs` | 20,210 | `5CB77B3F2F5E` | 屏 C 逻辑：逐行真绑定 + 备份/还原 + 自检钩子 |
| `Features/Servers/ServersPage.xaml.cs`（**扩充**） | 16,330 | `47349A2665D4` | 「添加」按钮改为走 §8 对话框（原 t8 是塞占位服务器） |

**给外壳的三个挂载点（ui 已留好签名，见 `MainWindow.xaml.cs`）**
```csharp
// SEAM①（:226）侧栏服务器行右键
ServerRowContextMenu.AttachTo(args.OriginalSource as FrameworkElement, row.Server, OnServersChanged);
// :263 设置页
ContentFrame.Navigate(typeof(Features.Settings.SettingsPage));
// :269 添加服务器
var ok = await Features.Servers.AddServerDialog.ShowAsync(ContentFrame.XamlRoot);
if (ok) { OnServersChanged(); }
```

## 2. 构建读数（命令 + HEAD + 时刻 + 退出码）

```
[1] dotnet build shell\App\AIPlayer.Shell.csproj -c Debug -p:OutputPath=E:\t30-verify\bin\
    HEAD=858975c  at=2026-09-12 00:04:49  EXIT=0  →  0 个错误      ← ★ **最终源码**（含全部 t30 交付物与取证钩子）
[2] dotnet build C:\Users\Administrator\AppData\Local\Temp\t30-host\t30host.csproj -c Debug
    HEAD=a38a7fb  at=2026-09-12 00:03:36  EXIT=0  →  0 个警告 0 个错误   ← 宿主（链接同一份源码）
[3] 期间的红逐条归因（全部**不是**本卡的错，按纪律不改他人文件、不杀他人进程）：
    23:12 KernelHost/InboundCallbackEndpoint.cs（ui2 t27 在途）· 23:30/23:31/23:40 输出目录被他人运行实例锁定
    （MSB3027/MSB3021/CS2012）· 00:00:03 shell/Services/Storage/SwrSnapshotCache.cs（services t44 在途，缺 using）
    · 00:00:42 shell/Services/SelfCheck/ServiceSelfCheck.cs（services 在途，CS0103 fastHit）
[4] 我这一侧的自捉错误（都在 30 s 内修掉）：
    CS0246 EmbyAuthResult 缺 using · CS1739 ServerConfig.With 没有 kind 参数 · CS8120 ToggleMenuFlyoutItem 派生自
    MenuFlyoutItem ⇒ case 顺序 · CS0103 File 缺 using System.IO · CS1039/CS1073 宿主里转义引号写错
```
- 说明：`[1]` 用 `-p:OutputPath` 重定向输出目录，避开别人运行实例对 `bin\` 的锁定（t28 已踩过同一坑）。

## 3. 屏 A：添加服务器（§8）

**校验表自检（18/18 命中，纯函数、不驱动输入；口径 = `services` 2026-09-12 经 captain 转达）**
```
ADDSERVER-VALIDATE url=""                       expect=fail:地址必填        actual=fail:服务器地址必填。      match=True
ADDSERVER-VALIDATE url="not-a-url" / "ftp://x/y" expect=fail:格式           match=True ×2
ADDSERVER-VALIDATE user=""                       expect=fail:用户名必填      match=True
ADDSERVER-VALIDATE port="0" / "70000"            expect=fail:端口范围        match=True ×2
ADDSERVER-VALIDATE Emby 正常输入                 expect=pass                match=True
★ ADDSERVER-VALIDATE url="https://fx.example.com/emby" kind=Emby user="fx" pwdLen=0  expect=pass:空口令放行  actual=pass  match=True
★ ADDSERVER-VALIDATE url="https://jf.example.com"      kind=Jellyfin        pwdLen=0  expect=pass:空口令放行  actual=pass  match=True
  ADDSERVER-VALIDATE kind=Navidrome      pwdLen=0 expect=fail:Navidrome 密码必填  actual=fail:Navidrome 的密码必填（当刻不支持空口令）。 match=True
  ADDSERVER-VALIDATE kind=Navidrome      pwdLen=1 expect=pass                    match=True
  ADDSERVER-VALIDATE kind=Audiobookshelf pwdLen=0 expect=fail:ABS 密码必填       match=True；pwdLen=1 → pass
  ADDSERVER-VALIDATE kind=WebDav user=""          expect=fail:WebDAV 用户名必填   match=True
  ADDSERVER-VALIDATE kind=WebDav pwdLen=0         expect=fail:WebDAV 密码必填     match=True
  ADDSERVER-VALIDATE kind=WebDav root=""          expect=pass:rootPath 可空       match=True
  ADDSERVER-VALIDATE kind=WebDav root="dav"       expect=fail:rootPath 不以 / 开头 match=True
  ADDSERVER-VALIDATE kind=WebDav root="/dav"      expect=pass                    match=True
ADDSERVER-NORMALIZE  url+port -> "https://emby.example.com:8443"｜url-only -> "https://emby.example.com/emby"｜尾斜杠 -> "http://127.0.0.1:8096"
```
**逐类型必填口径（照 `services` 落地）**：五类**用户名都必填**（含 WebDAV —— 我们恒发 Basic；早期版本对 WebDAV 放过空，已收紧）；口令**只有 Emby/Jellyfin 允许空**（🔴 实测：本机存在空口令服务器，拦它 = 把用户可用服务器挡死），Navidrome / AudioBookshelf **必填**、WebDAV **选填**（凭据随请求发出，认证失败会当场回显）；`Port` **选填且并入 `BaseUrl`**（服务层无独立端口字段）；ABS `libraryId` 由登录后 `/api/libraries` 发现（不设手工输入）；WebDAV `rootPath` **可空**（默认用 BaseUrl 路径）。**对话框只按 Kind 切换提示文案与必填标记，字段集不换**（提示随 Kind 变化：密码框的 Header 与 Placeholder 会写「必填」或「选填（Emby/Jellyfin 空口令账号；WebDAV 凭据随请求发出）」）。
> ⚠️ 与 §8 的一处口径张力（已按 captain 口径执行并留档）：`services` 的覆盖表只列了 4 个字段（BaseUrl/UserName/Password/Name），而 §8 的**五字段流程**是 t30 验收第 2 条的原话、其中**「端口」是参照物实测字段**。故本屏**保留端口输入**（选填、归一化进 BaseUrl，服务层无独立字段），既满足 §8 五字段与验收措辞，也不与服务层口径冲突。若 captain 要求删掉该输入框，一行即可去。
**视觉**：`evidence/t30-dialog-screen.png`（1440×759，sha12 见 §5）——居中模态 + 背景压暗 + 标题「添加服务器」+ 五字段 + `登录`（主按钮）/`取消`；密码右侧「显示」切换。

**真加一台并落盘（验收第 2 条，对服务层 `MockEmbyServer` 的真 HTTP 端点，含前后 diff + 反控制）**
```
ADDSERVER-MOCK started baseUrl=http://127.0.0.1:63606 port=63606
ADDSERVER-ADD-BEGIN file=…\AIPlayer\servers.json bytesBefore=21 id=srv_1789142619587 url=http://127.0.0.1:63606
ADDSERVER-ADD-OK name="t30 自检(Mock Emby)" kind=Emby userId=user-1 tokenLen=22 detail=
ADDSERVER-ADD-DIFF bytes 21->386 containsId=True containsUrl=True
ADDSERVER-ADD-CREDENTIALS id=srv_1789142619587 hasPassword=True hasToken=True
ADDSERVER-ADD-CLEANUP bytes=21 identicalToBefore=True          ← 反控制：不给用户留脏数据
ADDSERVER-MOCK requests login=1 views=1 unauthorized=0          ← 真打了两条 HTTP：AuthenticateByName + Views
```
**如实标注**：认证对端是**服务层自带的 MockEmbyServer**（本地 127.0.0.1 真 HTTP 进程），不是远端真机 Emby —— 远端登录路径待 §1 的三个挂载点接好后在真实外壳里跑（§7-4）。

**口径修订（captain 最终裁定）**：WebDAV 的密码为**选填** —— 凭据随每次请求发出（提交即认证），留空而服务端要求认证会当场失败并回显，不会静默写入坏配置；用户名仍必填（决定认证形态）。`RunValidationSelfCheckAsync` 的期望值已同步改为 `pass:WebDAV 密码选填`；上文那次运行日志记录的是修订前的旧口径，按原样留档不改写。

**运行验证（2026-09-12 01:15:16 @HEAD 06153c1，宿主 exe 01:15:11 重编译）**：`evidence/t30-webdav-optional-host.txt`（4526 B，sha256:476E0DAB1A0F）—— 18/18 `match=True`、`match=False` 0 条；关键行 `kind=WebDav user="u" pwdLen=0 expect=pass:WebDAV 密码选填 actual=pass match=True`，同轮真 HTTP 加服务器 `login=1 views=1 unauthorized=0` + `ADD-CLEANUP identicalToBefore=True`。说明文案定稿（captain 措辞）后即复跑本矩阵，故读数对应当前源码。**作废读数留痕**：01:09 那次取自 01:04:38 的旧二进制（期望值还是旧口径），该文件已删、不作证据 ⇒ 宿主 exe 的 mtime 必须晚于源码改动。

**实现语义（逐条对上 §8）**：显示名称选填（Emby 登录成功后用 `EmbyAuthResult.ServerName` 回填）· 地址必填且带格式校验 · 端口独立并拼回 `BaseUrl` · 用户名必填（五类都对凭据有要求）· 密码选填（Emby/Jellyfin 空口令账号、WebDAV 凭据随请求发出；Navidrome/ABS 必填）· **提交即认证**（Emby/Jellyfin 走 `LoginAsync` 换 token 后再 `GetViewsAsync`；Navidrome/ABS/WebDAV 各走自身探测）· 成功才 `Add/Update + Save + PasswordStore.Save`，**失败不落盘**。
**状态机**：Idle → Invalid（行内红字）→ Submitting（两按钮禁用 + 「正在连接 …」，输入不清空）→ Probing → Persisting → Success（关闭并返回 true）｜失败三分：**401/403 = 可达但认证失败**、超时、连不上（`DescribeFailure` 分开说）。

## 4. 屏 B：服务器行右键菜单（§3.1，11 项）

**机器读数（真构造 MenuFlyout 后逐项读 `IsEnabled`）**
```
MENU-INVENTORY id=vU9cHAh8ZJ5Xv7El name="ServerA" readOnly=True total=11 on=5 ::
  OFF|服务器线路|待定：当刻 ServerConfig 只有一个 BaseUrl，要做同服多地址回落需先扩模型（§3.1）
  ON |修改图标|-                              ON |修改备注|-
  OFF|设为私密|待定：语义未定 —— 是「不参与聚合视界」还是「隐藏名称」尚未裁定（§3.1），不猜
  ON |媒体库|-                                OFF|媒体库统计|待定：依赖统计接口，收益低（§3.1）
  OFF|strm直链播放|待定：原版特性；与「播放走内核」的关系需先核（§3.1）
  ON |禁用预加载|checked=False
  OFF|修改密码|该条目来自原版 accounts.json（只读）：我们对它永不回写，如需管理请重新添加
  OFF|编辑|（同上）                            OFF|删除|（同上）
```
- **7 项做 / 4 项待定**，与 captain 的更正（含「媒体库统计」）逐项一致；4 项**可见但置灰**且各带理由 tooltip —— 不静默省略。
- 🔴 **实测发现（要 captain 知悉）**：本机 16 台服务器**全部**来自原版 `accounts.json` 兼容读 ⇒ `IsReadOnlyServer` 全为 true ⇒ 用户在这台机器上看到的「修改密码/编辑/删除」**三项常态置灰**。这是 t8 既定纪律（我们对 `accounts.json` 条目永不回写）的必然结果，不是本卡缺陷；tooltip 已写明出路（重新添加一台可写条目）。**若 captain 认为体验不可接受，需单独裁一条"外壳自有关联表"的口径**（t30 不在本卡内自选）。
- **视觉截图未取到**：`MenuFlyout` 是 light-dismiss 的**独立弹窗 HWND** —— 抢前台截图会把它关掉；不抢前台则抓到的是**用户桌面**（我实测抓到了一帧含用户私人窗口的画面，**已立即删除且不再采用该通道**）。⇒ 菜单以**机器读数**作证，视觉留白如实登记。§3.1 的观感依据仍是 `HILLSLITE_UI_ANALYSIS.md` §4.5 的逐像素实测值 + 已入 `Tokens.xaml` 的 `MenuBgColor/MenuHoverColor`。

## 5. 屏 C：设置（§9）

**自检读数（真文件）**
```
SETTINGS-SELFCHECK-BEGIN settingsFile=…\AIPlayer\settings.json exists=True serversHash12=C25F66B5FFCA
SETTINGS-SELFCHECK-BASELINE themeMode="light" libraryPageSize=60 mergeServerLibraries=False proxyEnabled=False
                             proxyUrl="http://127.0.0.1:7890" minimizeToTrayOnClose=False
SETTINGS-SELFCHECK-WRITE libraryPageSize 60->77 readBack=77 match=True
SETTINGS-SELFCHECK-WRITE minimizeToTrayOnClose False->True readBack=True match=True
SETTINGS-SELFCHECK-WRITE proxyUrl->http://127.0.0.1:9/ readBack="…:9/" match=True proxyEnabled=true readBack=True
SETTINGS-SELFCHECK-BACKUP dir=…\AIPlayer\backups\20260911-235753 files=[credentials.bin=230B, MANIFEST.txt=108B, servers.json=21B, settings.json=1668B]
SETTINGS-SELFCHECK-RESTORE dirty=123 restored=True readBack=77 restoredToBackupValue=True
SETTINGS-SELFCHECK-BASELINE-RESTORED … identical=True
SETTINGS-SELFCHECK-SERVERS-UNTOUCHED before=C25F66B5FFCA after=C25F66B5FFCA identical=True   ← 反控制
SETTINGS-SELFCHECK-ROWS languageEnabled=False themeEnabled=True pageSizeEnabled=True mergeEnabled=True
                        exportEnabled=True restoreEnabled=True syncEnabled=False proxyEnabled=True
                        trayEnabled=True resetEnabled=True themeValue="light"
```
**逐行绑定真值**：语言=**置灰**（服务层无 i18n）｜主题=`themeMode` **三态**（system/dark/light，**实测踩过坑**：只映射两态会把基线里的 `light` 静默改写成 `system`）｜媒体库=`libraryPageSize`+`mergeServerLibraries`｜备份与还原=真导出/真还原到 `%LOCALAPPDATA%\AIPlayer\backups\`（不弹选择器 ⇒ 可自检复现）｜同步=**置灰**（Trakt 账号属被排除范围 / §6.1）｜网络=`proxyEnabled/proxyUrl` + `ConfigPortService` 端口显示｜托盘=`minimizeToTrayOnClose`｜重置=`SettingsService.ResetToDefaults`（规格外补充的出口，真绑定）。
**视觉**：`evidence/t30-settings-screen.png` —— 7 行卡片 + 分组标题「通用」+ 左图标 + 灰字副说明 + 右控件；置灰两行各写明原因；截图里的数（60 / 直连 / 关 / 浅色）与文件逐项一致（自检收尾会 `Load()` 刷 UI，避免截图上留着哨兵值）。

## 6. 证据文件与哈希

| 文件 | 字节 | sha12 |
|---|---|---|
| `evidence/t30-dialog-screen.png` | 46,917 | `D554DB2F4E5B` |
| `evidence/t30-dialog-renderfull.png` | 45,168 | `641A5E13901B` |
| `evidence/t30-dialog-host.txt` | 4,713 | `C22A497D68C1` |
| `evidence/t30-settings-screen.png`（🔻**§9.2 之前的历史证据**：旧抓屏通道已按隐私纪律退役 ⇒ **不作当前视觉证据**） | 79,236 | `075197366E5C` |
| `evidence/t30-settings-renderfull.png`（🔻**同上：历史证据**，不作当前视觉证据） | 62,086 | `82006F584441` |
| `evidence/t30-settings-host.txt` | 3,561 | `B8F7228E0788` |
| `evidence/t30-menu-host.txt` | 1,554 | `E8605185A905` |
| `evidence/t30-settings-icons-zoom.png`（emoji 整改核验：图标列 5× 放大，覆盖 8 行） | 9,547 | `9F277B74E752` |
| `evidence/t30-adopt-service-selfcheck.txt`（`SEAM⑤` 接管式写入 (a)(b)(c)(d) 断言，见 §10） | 43,895 | `140604EB9C89` |
| `evidence/t30-settings-render8.png`（**通道 = 进程内 `RenderTargetBitmap`**（非抓屏）；§9.2 瘦身 + 版面微调之后的整页渲染，图标 8/8 见 §11） | 42,020 | `2BBA91A8DC67` |
| `evidence/t30-settings-window.png`（**通道 = 窗口级 `PrintWindow`**，主窗口 1440×759、含标题栏；两通道对照见 §11.2） | 55,152 | `9835863D0626` |
| `evidence/t30-settings-window.txt`（上图的来源断言侧车：hwnd/pid/class/rect/焦点前后/PNG bytes+sha12） | 1,335 | `5D31560DA8E0` |
| `evidence/t30-bundle-link.txt`（§③ 配置包 `Servers` 段联动**隔离取证**：断言链 + 反控，见 §12） | 938 | `426EA383B732` |
| `evidence/t64-servers-before.png`（**通道 = 窗口级 `PrintWindow`**；`t64` 改前整页，含 rail 与"URL↔Emby 大片空白"） | 60,908 | `5B22F81F2B97` |
| `evidence/t64-servers-before.txt`（上图的来源断言侧车） | 1,342 | `C12C19C8BBFA` |
| `evidence/t64-servers-after.png`（**通道 = 窗口级 `PrintWindow`**；`t64` **改前最近一代**整页 = 提交 `5c96022` 的行形态（56px 行距 + 8 条 1px 分隔线 + 类型紧跟左组），**最右列仍空**；见 §15） | 59,148 | `E12246A8B96B` |
| `evidence/t64-servers-after.txt`（上图的来源断言侧车） | 1,332 | `6B5D3680561C` |
| `evidence/t64-servers-fixed.png`（**通道 = 窗口级 `PrintWindow`**；`t64` **改后**整页 = 最右列填成真实状态读数（`启用`×8 可见行）；见 §15.3） | 60,554 | `E9775EC1BB92` |
| `evidence/t64-servers-fixed.txt`（上图的来源断言侧车：hwnd/pid/class/rect/焦点前后/PNG bytes+sha12） | 1,332 | `EDF5C99BC064` |
| `evidence/t64-servers-fixed-audit.txt`（§15.8 设计合规工装原始输出：`AUDIT` 头 + 36 行 `RESULT`/`SUMMARY`） | 14,394 | `C3B80BAEF507` |
| `evidence/t30-servermenu-flyout.png`（**通道 = 窗口级 `PrintWindow`**（`class=Microsoft.UI.Content.PopupWindowSiteBridge`，164×378）；右键菜单 11 项；见 §13） | 12,440 | `EA2A488F79CF` |
| `evidence/t30-servermenu-flyout.txt`（上图侧车：`CODE-IDENTITY`/`PROVENANCE`/`POST-CAPTURE-IDENTITY`/`GUARD`/独立像素复验） | 2,536 | `54071CF16FEC` |
| `evidence/t30-settings-isolation.txt`（§14 设置页自检隔离收口：补要求 1/2 + **第 4 条验收=可失败控制**的逐字读数；行内容逐字取自 `%LOCALAPPDATA%\AIPlayer\logs\aiplayer.log`；含 §14.1 的 G 代际完整锚行块 + **写入面全貌/opt-in 风险边界**） | 23,973 | `504DEBA14A02` |

> ⚠️ **本表与磁盘逐行对齐**（2026-09-12 01:14 重算）：**sha12 只对「那一次运行」有效**，重跑必变 ⇒ 每次重跑后须同步本表。
> ⚠️ **重跑陷阱（本卡踩过两次）**：驱动 `t30-run.ps1 -Tag <t>` 会先删同前缀 `t30-<t>-*` ⇒ 用 `-Tag settings|dialog` 重跑会连带删掉**已入档**的 `t30-<t>-screen.png` / `-renderfull.png`（本次 4 张被删，已 `git checkout` 从 HEAD 恢复）。重跑前先确认 PNG 不落在该前缀里。
> ⚠️ **设置页两行按 captain 裁定 (a) 降级标注**（2026-09-12 01:5x）：`t30-settings-screen.png` / `t30-settings-renderfull.png` 是 **§9.2 之前**的渲染、出自**已按隐私纪律退役的旧抓屏通道** ⇒ **不删、不退休**（删了会丢证据链），但**不作当前视觉证据**；**当前唯一有效的设置页视觉证据 = `t30-settings-render8.png`（进程内 `RenderTargetBitmap`）+ `t30-settings-window.png`（窗口级 `PrintWindow`）**，两者通道声明见 §11.2。标注**不改变身份判据**（bytes/sha12 仍按磁盘）。`t34` 参照面请用 `render8`。

复现：宿主 `%TEMP%\t30-host\t30host.csproj`（链接仓库同一份源码）+ 驱动 `t30-run.ps1 -Mode settings|dialog|menu -Tag <t>`，输出落在本 evidence 目录、文件名形如 `t30-<tag>-host.txt`。**本卡自带的截图脚本已按隐私纪律删除**（全屏拷贝一律禁止）⇒ 图只能由窗口级取证工装产出：弹窗走 `shell\tools\popup-capture.ps1`（§7-2 的通道），整屏走接线后的整屏取证。**宿主机 exe 的 mtime 必须晚于源码改动**才可采信读数（本卡踩过：读到 01:04:38 旧二进制的旧期望值）。

## 7. 未闭合 / 交接

1. **三个挂载点待 ui 接**（§1 的三行）—— 本卡证据走的是"宿主里跑同一份源码"，**外壳内的整屏截图待接线后补**（届时按 §14 仍不驱动用户输入，只做导航级取证）。
2. **菜单视觉截图缺失**（原因见 §4）—— 若 captain 要求必须有图，需要"能冻结 light-dismiss 弹窗"的通道（例如窗口级 `PrintWindow` + `PW_RENDERFULLCONTENT` 对 popup HWND 单抓），属取证工装问题，非实现问题。
3. **只读条目导致三项常态置灰**（§4 末段）—— 需 captain 裁是否需要"外壳自有关联表"的新口径。
4. **添加服务器尚未做真机登录取证**：登录属网络 + 用户输入路径；校验/归一化已 12/12 断言，**真登录成功路径待接线后在真实外壳里跑一次**（那时可用既有的 Emby 服务器做正向样本）。
5. `themeMode` 的**全窗口**生效需 ui 在 `MainWindow` 根节点接一行（t34 视觉收口范围）；本页只保证本页即时生效并在副说明里如实标注。

## 9. 裁决落地补丁（captain 五条裁决到得晚于我结单 ⇒ 按裁决改到同一批文件）

> t30 任务卡已是终态（completed 不可改），本节是**结单后的按裁决修订记录**。修订后外壳构建：**HEAD `ec93f67` @2026-09-12 00:15:19 `EXIT=0 / 0 个错误`**。

| captain 裁决 | 原实现（结单时） | 改后 | 证据 |
|---|---|---|---|
| **E2 主题：不做第二套令牌** | 三态 ComboBox，可写 `themeMode` 并本页即时生效 | ✅ **置灰标待定**：`ThemeBox.IsEnabled=False`、整行 tooltip「待定：仅暗色主题（参照物只有暗色实测值，浅色未设计）」、副说明只读展示 `themeMode` 现值（**不再改写**）；删掉 `OnThemeChanged`/`ApplyThemeLocally` 两个写入点 | `SETTINGS-SELFCHECK-ROWS … themeEnabled=False … themeValue="light"`（基线里的 light 被原样保留） |
| **E4 语言：置灰标待定** | 已置灰 | 不变（tooltip 文案已含「服务层无 i18n」） | `languageEnabled=False` |
| **E5-备份与还原：做「导出/导入配置包」** | 文件级拷贝（servers.json + settings.json + credentials.bin + MANIFEST） | ✅ 改为 **`BackupBundle` 配置包**（`JsonStorage` 落 `AppDataDir.BackupDir\bundle-<ts>.json`），打 **Servers + Settings + Icons + SkipCache** 四段；**凭据不进包**；按钮文案 = 导出配置包 / 导入最近配置包 | `SETTINGS-SELFCHECK-BUNDLE parts=[Servers(servers)=0,Settings(settings)=43,Icons(icons)=0,SkipCache(skipCache)=0]`；`bundle-restore parts=[settings=43]`；`RESTORE dirty=123 restored=True restoredToBackupValue=True` |
| **E5-同步：不是 Trakt，绑 `CrossServerSyncService`** | 置灰标待定（写成 Trakt 属排除范围） | ✅ 改为**真绑定**：「立即同步」按钮 → 对全部已启用 Emby/Jellyfin 抓「继续观看」→ `CrossServerSyncService.MergeByServer` → 报三个各自具名的数（台数 / 合并后条目数 / 多源条目数）；Trakt 仍不做（副说明写明） | `SETTINGS-SELFCHECK-SYNC line="跨服同步：7 台成功 / 9 台失败 · 合并后条目数 0 · 多源条目数 0"`；`syncEnabled=True` |
| **托盘：等 services 加键，之前置灰** | 直接用 `MinimizeToTrayOnClose` | 不变 —— **前置条件已满足**：services 已把该键加进 `AppSettings`（`:187` 属性 / `:266` ToJson / `:316` FromJson / `:367` `With(...)` 参数 / `:407` 赋值），**未走旁路文件** | `trayEnabled=True` + 自检 `minimizeToTrayOnClose False->True readBack=True` |

**补丁过程中自捉的两处真缺陷**
1. **同步曾被自己的哨兵代理挡死**：自检第 ① 步把 `proxyUrl` 设成 `http://127.0.0.1:9/`（内存态），而同步原本排在基线还原**之前** ⇒ 16 台全部失败，失败原文里明写 `(由于目标计算机积极拒绝，无法连接。 (127.0.0.1:9))`。已把同步移到基线还原之后。**顺带得到一条正面事实**：`ServiceRegistry.ApplyProxyFromSettings()` 确实按设置生效（这是它的活证据）。
2. **配置包的 Servers 段读法错**：`servers.json` 的实际形态是 `{"servers":[...]}`（对象包数组），第一版只认裸数组 ⇒ 包成空壳。已改为两种形态都吃（含键名大小写探测）。
   ⚠️ 另如实记一条语义：**本机 16 台服务器全部来自原版 `accounts.json` 兼容读，不在我们自己的 `servers.json` 里**（后者是 `{"servers":[]}`）⇒ 配置包的 Servers 段 = 0 条是**正确行为**：原版文件只读，不该被打进我们的包。

**已落盘路径**：配置包 `%LOCALAPPDATA%\AIPlayer\backup\bundle-<yyyyMMdd-HHmmss>.json`（本次 `bundle-20260912-001315.json` = 1,970 B，四段：servers 0 / settings 43 / icons 0 / skipCache 0；`icons.json`、`skip_segments.json` 本机尚未产生 ⇒ 空段，不编数据）。


- **只写领地**：改动 = `Features/Servers/*`（4 个新文件 + `ServersPage.xaml.cs` 一处入口）+ `Features/Settings/*`（2 个新文件）；**未碰** `MainWindow.*` / `Theme/` / `App.xaml` / `Services/` / `reversed/`。
- **并发**：构建前查 `Get-Process dotnet,MSBuild,XamlCompiler`；读数带 HEAD + 时刻；两次红逐条归因到 services 在途文件；未杀他人进程（改用重定向输出目录）。
- **凭据**：密码只经 `PasswordStore`（DPAPI）；日志经 `Program.MaskSecrets` 打码。
- **不抢前台**：截图会使窗口短暂前台化（< 4 s）；发现"不抢前台会抓到用户桌面"后**立即停用该通道并删除含私人内容的帧**。
- **不驱动用户输入**（§14）：三屏取证全部靠程序自身构造 + 自检钩子，未模拟任何键鼠输入。

## 10. `SEAM⑤` 接管式写入的 (a)(b)(c)(d) 证据（captain 收紧判据后补，2026-09-12 01:23:43）

**命令（可复现；服务层隔离式自检 —— 合成 `accounts.json` 3 条落在临时目录，不碰用户数据、不碰用户 `servers.json`）**
```
dotnet test shell\Tests\Tests.csproj -c Debug --filter ServiceSelfCheckTests --logger "console;verbosity=detailed"
```
⇒ `self-check: 64 passed / 0 failed / 64 total  => PASS`，`EXIT=0` @HEAD `3a54d03` @01:23:43（采样时 `dotnet` 进程数 = 0）。
**留档**：`evidence/t30-adopt-service-selfcheck.txt`（43,895 B，sha12 `140604EB9C89`）。

| 判据 | 断言原文（节选，全绿） | 结论 |
|---|---|---|
| **(d) 接管确认上选「取消」⇒ 无任何写入** | `S9①反控(d) … Update 被拦=True Remove 被拦=True｜名字未被改=True｜imp-3 仍在=True｜servers.json 未含 imp-1/imp-3=True｜accounts.json sha 不变=True` | PASS |
| **(a) 编辑导入服务器 ⇒ 接管后写进我们自己的 servers.json** | `S9② Adopt 生效=True｜只读=False｜**落盘 name=改过的名字**｜血缘 IsOriginalAccountEntry=True 已接管标记=True｜accounts.json sha 不变=True` | PASS |
| **(b) 接管后的写操作重启仍在、不出现第二份** | `S9③ 重启后 imp-1 名字=改过的名字｜同 id 条数=1（期望 1）｜仍只读=False` | PASS |
| **(c) 删除 = 外壳侧墓碑，原版 `accounts.json` 一个字节不动** | `S9④ 墓碑(内存)=True/(重载后)=True｜servers.json 含 hiddenOriginalIds=True｜重载后可见=2 台｜accounts.json sha 不变=True｜原件仍 3 条=True` | PASS |

**UI 代码路径说明（这几条为什么能代表本屏行为）**
- 三个写入 handler（修改密码 / 编辑 / 删除）都先 `await EnsureAdoptedAsync(row, server)`：仅当 `IsReadOnly(server) && !IsAdoptedEntry(server)` 时弹**显式接管确认**；用户点「取消」⇒ 该函数 `return false` ⇒ handler **直接 return，不触碰任何 store 写 API**（`ServerRowContextMenu.cs:81-100`）。
- 点「确认」⇒ `Store.Adopt(id)`（**唯一**会写我们自己 `servers.json` 的一步）⇒ 编辑/改密码走 `Store.AdoptAndUpdate(server)`、删除走 `Store.AdoptAndRemove(id)`；服务层对**未接管**条目的 `Update`/`Remove` 另有一道拒绝（`InvalidOperationException`）—— 这正是 `S9①` 断言的路径 ⇒ **即使有人绕过确认，也写不进去**。
- 运行时读数（真实外壳 `SHELL_SELFTEST_SERVERMENU=1`，2026-09-12 01:19:00）：`MENU-INVENTORY … total=11 on=8`，其中修改密码/编辑/删除三项 `ON`，tooltip = 「该条目来自原版 accounts.json（导入）：点下去会先确认『接管到外壳管理』——原文件不会被改动，接管后即可自由编辑/删除」。
- **证据类型如实标注**：`S9①` 的「取消」= **代码路径证据 + 服务层拒绝断言**，**不是**"真的点了取消"（点击属用户输入，本卡不驱动任何键鼠）；`MENU-INVENTORY` = "菜单项可控状态"证据，不是"点击后落盘"证据。
- **未覆盖（诚实边界）**：用户在真机上点「接管并继续」的端到端交互（含 `ContentDialog` 实际点击）待接线后在真实外壳里做，见 §7-4。

## 11. 设置页图标 **8/8** 复核（进程内渲染，2026-09-12 01:26 前后）

**为什么换通道**：本卡原先那两张设置页图由已删除的抓屏脚本产出，而抓屏通道按隐私纪律**永久停用** ⇒ 改用**进程内 `RenderTargetBitmap`**（只读本进程可视树，**不抓屏、不抢前台、不驱动输入**），在临时宿主里把设置页放进 1440×1500 的容器（比窗口视口更高）后整页渲染。
- 命令：`%TEMP%\t30-host\t30host.csproj`（链接仓库同一份源码）→ `SHELL_T30_MODE=render`、`SHELL_T30_OUT=<png 路径>` → 起宿主；宿主自己写 PNG 并记 `RENDER settings out=… <W>x<H> bytes=…`。
- 产物：`evidence/t30-settings-render8.png`（1440×1500，65,117 B，sha12 `C9C285453AB5`；宿主 exe 01:26:08 重编译后产出）。
- **读数**：图标列 **8 行全部渲染为字形**，其中「关闭时最小化到托盘」（`E7E8`）与「重置设置」（`E7A7`）**这次在画面内**（此前那张被视口裁掉）⇒ 证明 `Segoe MDL2 Assets` 在本机（Win10 19045）渲染正常、**无豆腐块**；8 个字形的"各不相同"也可肉眼核（语言/主题/媒体库/备份与还原/同步/网络/托盘/重置）。
- **同一张图如实暴露的问题（属 §9.2/§9.3 待改，不是本节的通过项）**：副说明长到撞进右列控件（备份与还原行含**绝对路径**、网络行含长括号说明）、媒体库行的 ▲▼/× 三个游离控件与右侧文字**重叠**、页头仍显示绝对路径。
- **仍缺**：「修改图标」6 字形的 `ContentDialog` 视觉证据（同一渲染通道待扩展，见 §7-2 交接）。

### 11.1 重截与并排比对（§9.1 判据⑤，2026-09-12 01:33:31）

**★ 先记一次我自己踩的坑（同族纪律）**：01:31 我第一次重截出的图**仍是长文案** —— 因为宿主 exe 的 mtime（01:26:08）**早于** `SettingsPage.xaml.cs` 的改动（01:30:11），跑的是旧编译产物。⇒ 重编译宿主（01:33:26）后再截（01:33:31）才是当刻文件。
**可用图**：`evidence/t30-settings-render8.png`（1440×1500，42,020 B，sha12 `2BBA91A8DC67`，**源 01:30:11 < exe 01:33:26 < png 01:33:31**）。
**读数**：8 行副说明全部变成用户话（暂不支持切换语言 / 暂不支持切换主题 / 每页显示条数；多服合并 / 导出或导入配置备份 / 合并多台服务器的同一部影片 / 代理与回调端口 / 关窗不退出，留在托盘 / 恢复默认设置）；图标列 8/8 渲染为字形。

**与参照物 `refs/original-ui/hl-05-settings-printwindow.png`（1419×802，44,411 B，sha12 `0F651A02551A`）逐条比对**

| 维度 | 参照物（Hills Lite 原版） | 本页 | 判定 |
|---|---|---|---|
| 卡与分隔 | 通宽卡、行间 1px、无行边框 | 同 | ✅ 一致 |
| 图标列 | 每行左侧线性图标、列宽固定 ⇒ 标题左边缘齐 | 同（列宽 40、`FontSize=20`） | ✅ 一致 |
| 副说明 | **一句话、单行**、用户话（如「修改与媒体库相关设置」） | 同（最长 13 汉字，单行） | ✅ 一致 |
| 页头 | 无配置路径 | 「配置已加载」 | ✅ 一致（我方曾显示绝对路径，已按 §9.2⑥ 改掉） |
| 行右侧 | **chevron `›`**（该行可展开/进入子面板）＋少数行内联控件（语言 Auto、托盘开关） | **内联控件/按钮**（语言、主题、条数+合并、导出/导入、立即同步、代理、开关、恢复默认） | ⚠️ **已知形态差异**：参照物是"折叠行 → 子面板"，本页把可改项直接摊在行上（功能等价、少一次点击；不属 §9.1 六条判据项，登记为**有意差异**，如需改成折叠行请裁定） |
| 顶部两条 | 「Hills Lite Pro」「账号」 | **无**（按裁定不做账号/Pro） | ✅ 有意差异 |
| 媒体库行 | 无游离控件 | 🔴 ▲▼ + `×` 三个控件与右侧文案**重叠**（图内可见） | ❌ **§9.3① 未修**，下轮定位并出"清掉后"的图 |
| 底部 | — | 状态行「已加载」位置待核（本图裁到 1500 px 高、未含页脚反馈） | ⏳ §9.3② 待核 |

### 11.2 两条取证通道**分别声明**（captain 批 `RenderTargetBitmap` 时的条件之二）

| 图 | 通道 | 能证明 | **不能**证明 |
|---|---|---|---|
| `t30-settings-render8.png` | **进程内 `RenderTargetBitmap`**：只读本进程可视树，**不抓屏、不抢前台、不驱动输入**（1440×1500 容器 ⇒ 视口不再裁切） | 整页布局/文案/图标 **8/8**（含此前被窗口视口裁掉的 `E7E8`/`E7A7` 行） | OS 层 chrome / DPI / 标题栏；「真实窗口长什么样」 |
| `t30-settings-window.png` + `.txt` | **窗口级 `PrintWindow`**：`shell/tools/popup-capture.ps1 -Pid <shell> -Hwnd <main> -ExpectClassLike 'WinUIDesktop*'`，`channel=printwindow`；护栏侧车原文 `noScreenCopy=true`、`noFullScreenCapture=true`、`focusChanged=False`、目标覆盖 **52.71% < 95%**；`EXIT=0` | 真实窗口像素（1440×759，`nonBlackPct=98.114`）+ 可复核来源断言 | 弹层内容（`ContentDialog`）；被窗口视口裁掉的行 |

⇒ 两通道**分开命名、分别声明**（读者一眼可辨"这张不是屏幕像素"）；窗口级通道**不**用来证明弹层，进程内通道**不**代替窗口级证据。
⇒ 本卡历史遗留的 `t30-settings-screen.png` / `t30-settings-renderfull.png` 是 **§9 整改之前**的渲染，**不作为当前视觉证据**（保留为历史；是否从表退休待 captain 裁定）。

## 14. 设置页自检隔离的收口（captain 补要求 1/2 + 第 4 条验收「可失败控制」，2026-09-12 03:44→03:51）

> 本节是**结单后**按 captain 两条补要求 + 一条新增验收做的收口，被测文件 = `Features/Settings/SettingsPage.xaml.cs`（本卡领地，未碰他人文件）。
> 原始读数**逐字**落在 `evidence/t30-settings-isolation.txt`（111 行 / 12,770 B / `1DAAA20C056F`，表 §6 第 158 行起）。
> HEAD 全程 `252c507`；四次构建均 `EXIT=0 / 0 个错误 / 1172 个警告`，构建前 `Get-Process dotnet` = 0。

| 项 | 落地 | 运行读数（`SETTINGS-SELFCHECK-*`，取自 `%LOCALAPPDATA%\AIPlayer\logs\aiplayer.log`） |
|---|---|---|
| **补要求 1**（自检确实写真实 `settings.json` ⇒ 必须证明"内容已还原"） | 断言拆成两个**具名**读数：`bytesEqual`（字节级 sha12）＋ `contentEqual`（**规范化内容**哈希 = 解析后 `JsonNode.ToJsonString()` 再 sha256_12）；新增唯一 helper `CanonicalHashOf` | `SETTINGS-SELFCHECK-SETTINGS-RESTORED beforeSha=5F43A301A0C6 afterSha=5F43A301A0C6 bytesEqual=True beforeCanon=029F12CDF8B8 afterCanon=029F12CDF8B8 contentEqual=True`（03:50:51.955）；他方写过字节的那一跑 ⇒ `beforeSha=E250E15FCB14 afterSha=5F43A301A0C6 bytesEqual=False …contentEqual=True`（03:50:27.459）⇒ **内容还原成立**，同时不掩盖"字节不是我们写的"。旧文案「语义：内容已还原」与 `identical=False` 的自相矛盾已消除 |
| **补要求 2**（临时隔离根清理的存在性读数） | 自检收尾 `Directory.Delete(selfCheckRoot.Root, recursive:true)`（类型化 catch + 原因注释）+ 存在性读数行 | `SETTINGS-SELFCHECK-TEMPROOT exists-after=False path=C:\Users\Administrator\AppData\Local\Temp\t30-setcheck-95b34f8c`；配套目录名清单「跑前 = 跑后」（本次新建的根**未**留在 `%TEMP%`） |
| **第 4 条验收 = 可失败控制** | 把 `ExportAsync(selfCheckRoot)` / `RestoreAsync(selfCheckRoot, bundlePath)` 两处注入根**临时**去掉（= 回到修复前写面）→ 重编译（`dll 85BCEA3F5C34`，输出 `E:\ui3-probe-bin\`，**跑完整目录已删除**防误跑）→ 重跑 → 回滚重编译 | 控制跑：`SETTINGS-SELFCHECK-SERVERS-UNTOUCHED before=C2DA9E57742F after=6476F1F789B1 identical=False`（03:47:43.680，`servers.json` 被 1,627 B 产品形态 → 1,592 B 裸数组）；回滚后同一断言 `before=6476F1F789B1 after=6476F1F789B1 identical=True`（03:50:51.954）⇒ **断言可失败、非恒真**。源码已逐字节复原（sha12 回到 `925FD5773296`，`FAILABILITY-CONTROL` 残留 = 0），复现两行原文/改法写在证据文件内 |

**真实数据面的诚实交代**
1. 控制跑前对 `%LOCALAPPDATA%\AIPlayer\{servers.json,settings.json}` 做了**整文件字节备份**（`%TEMP%\ui3-failctl-backup-034609`）；收尾 `servers.json` 与备份**逐字节相等**（`6476F1F789B1 / 1,592 B`）。
2. 控制跑在真实根 `backup\` 留下两个自造包（`bundle-20260912-034702.json` / `bundle-20260912-034743.json`）⇒ 已**按文件名精确删除**，跑前 8 个历史包一个未动。
3. 🔴 `settings.json` 在 **03:47:44.728** 被**他方写入者**改写（2,271 B / `D4EE002180A0` / `"键":  值` + 4 空格递进 = PowerShell `ConvertTo-Json` 风格 / `proxyUrl=127.0.0.1:9/`）。当刻场内：`AIPlayer.Shell` pid 18956 = `E:\cap-bin7\`（**非我的**）、`powershell.exe` pid 20592 起于 03:47:40（= 控制跑前 1 s）、我的进程 03:47:44.6 已退出 ⇒ 判定为**他人在途实验**，我**没有改回去**（并发纪律：不碰别人的在途写入），该外部写入被 E 跑如实捕捉为 `bytesEqual=False`。
4. 控制跑第 1 次（未扰动 `servers.json`）给的是 `identical=True` —— 因为当时 `servers.json` **已经是导入路径的字节不动点**；写动作由 `mtime`（03:20:46.717 → 03:47:02.579）与 `bundle-restore path=…\backup\…` 两行证明。⇒ 只用"identical 值"当证据会漏掉这一层，故第 2 次控制**先把文件摆成产品形态**（`ServerConfigStore.Save()` 写的是 `{"servers":[…],"hiddenOriginalIds":[…]}`，导入路径写裸数组）。

**未跑的反控（不隐瞒）**：`contentEqual` 目前**只有 True 实例**，没有 True→False 的实测对照。造 False 必须让第 ③ 步基线还原不执行 ⇒ 会把哨兵值（`libraryPageSize=77` / 反向托盘 / `127.0.0.1:8/`）留在真实 `settings.json` 上，而该文件当刻正被**他人在途实验**读写 ⇒ 为避免互踩**有意未跑**，登记为"未跑的反控"。

**边界**：本批隔离面 = **注入根**（`ExportAsync` / `RestoreAsync` 的 `dataOverride`）；自检对真实 `settings.json` 的**写入面本轮未改**（承诺 = 内容还原，不是"一个字节都不写"）。若 captain 要求"一字不写"，需改哨兵机制（结构性收口），属 `t75` 范围。

**🔴 写入面全貌（captain 2026-09-12 裁决要求写进交付）**
1. `servers.json` = **本轮修掉**（隔离根，控制跑可失败）；`icons.json` / `skip_segments.json` 只出现在包恢复那一步，已随注入根隔离。
2. `settings.json` = **当时**必然被写且不是隔离根 —— **旧代行号**（`SettingsPage.xaml.cs` = 43,920 B / `4FF241D1D43C` / 826 行）：真实 `Patch` 落点 = `:590`/`:596`/**`:603`**（`:604` 是紧随的 `Reload()`，**不是写**）+ ② 步 **`:690`** 写 `123` + ③ 步 **`:710-715`** 用内存快照 `s0` 还原 ⇒ **共 5 次 `Patch`，其中 ① 一步占 3 次**（本节旧文写"三次写"指的是 ① 那一步；写成"三次文件写"会被下一个人误读）。
   ⚠️ **reviewer 2026-09-12 复核更正（我原写 `:678` / `:703-709` 是误指）**：`:678` 实为包内四段 JSON 的 **camelCase 回退查找（只读）**；`:703` 是 `RESTORE-TEMP` 日志、`:705-708` 是注释。**行号会漂**（本文件随 t75 又改到 881 行）⇒ 本文件此后一律以**锚文本**为准（`_settings.Patch(s => s.With(libraryPageSize: 123))` / `_settings.Patch(s => s.With(` 等），行号只作参考。
   承诺口径 = **只能写 `contentEqual`**（`bytesEqual` 只在"**这一次**"成立）：同一份 evidence 内就有反例 —— `evidence/t30-settings-isolation.txt:88`（03:50:27）`beforeSha=E250E15FCB14 afterSha=5F43A301A0C6 bytesEqual=False + contentEqual=True`，更早 `:79` 直接 `identical=False`；源码注释也写明外部书写者会重排字节（旧代 `:576-577`）。
   🔴 **本段已被 `t75` 取代**：自 t75 起三处哨兵写搬到**隔离根作用域实例**（`SettingsService.At(%TEMP%\…\settings.json)`），真实 `settings.json` **只被读** ⇒ 现行判据见 **§15.11**（`REAL-UNTOUCHED`：sha256 与 mtime **都不变**）。
3. **风险边界**：本自检是 **opt-in**（只在 `SHELL_SELFTEST_SETTINGS` 被设时武装，`:566` 判空即 return）⇒ **用户正常使用永不执行它**；"哨兵写与基线还原之间被 kill ⇒ 真实 `settings.json` 残留哨兵值"这一风险**只落在我们自己的测试跑上**。
   ⇒ **`t75` 范围（低优先、可选）**：把哨兵与还原也纳入隔离根（改成对临时根副本操作）。**本批不做**（captain 裁定不扩大范围）。

## 14.1 captain 锚行纪律落地（G 代际 = 当前源码，2026-09-12 04:11–04:18）

> 代际身份：`SettingsPage.xaml.cs` = 43,920 B / `4FF241D1D43C` / mtime 04:09:10.727；读数二进制 `AIPlayer.Shell.dll` = `BD3DC7C899D5` / 1,616,896 B / 04:11:01.564；回滚后重编 = `DA9C65A146E4` / 04:17:49.026（**源码同 sha ⇒ 源码级等价**）。构建 `EXIT=0 / 0 个错误 / 1172 个警告`。
> 跑法：`SHELL_START_PAGE=settings` + `SHELL_SELFTEST_SETTINGS=1`（PID 16420，04:11:32.129）。逐字读数 = `evidence/t30-settings-isolation.txt` 的 G 段（[18750]–[18807]）。

**锚行块（四要素全齐，逐字见证据文件）**

| 要素 | 读数 |
|---|---|
| `SETTINGS-SELFCHECK-BEGIN` | `settingsFile=…\AIPlayer\settings.json exists=True serversHash12=6476F1F789B1` |
| `…BUNDLE` + `…RESTORE` | `BUNDLE file=bundle-20260912-041133.json=5900B parts=[Servers(servers)=1,Settings(settings)=97,Icons(icons)=0,SkipCache(skipCache)=0]`；`RESTORE dirty=123 restored=True readBack=123 restoredToBackupValue=False`；**另加** `RESTORE-TEMP tempRootReadBack=77 expected=77 match=True tempFile=settings.json` |
| `SERVERS-UNTOUCHED`（期望由 False 变 True） | `before=6476F1F789B1 after=6476F1F789B1 identical=True`（04:11:33.643）；控制跑同断言 = `before=C2DA9E57742F after=6476F1F789B1 identical=False` |
| `SETTINGS-SELFCHECK-END` | `04:12:08.9087622`（前一行 `ROWS …`，再前一行 `SYNC line="跨服同步：9 台成功 / 7 台失败 · 合并后条目数 71 · 多源条目数 1"`） |
| 真实 `servers.json` 前后 | `6476F1F789B1 / 1,592 B / mtime 03:47:43.642` → **sha12、字节数、mtime 三者全同**（没被写） |
| 真实 `settings.json` 前后 | `6FD05EA70B06 / 3,764 B` → **sha12 与字节数"这一次"全同**；mtime `04:11:17.525 → 04:11:33.638`（**变了**，如实）｜⚠️ `bytesEqual` 不是可承诺的性质（同文件 `:88` 有 `bytesEqual=False + contentEqual=True` 的反例）；且该身份**已过期**（现盘 = `E6220105E7CC / 3,769 B`） |
| 临时根存在性**三读** | `TEMPROOT exists-before=False exists-during=True writable=True path=…\Temp\t30-setcheck-65fc66f6`（:634）；`TEMPROOT exists-after=False`（:743 后）；`%TEMP%` 目录名清单跑前=跑后 |
| 可失败反控 | 见下（同代、便携根版） |

**🔴 captain 那一问的正面回答（旧代 `settings.json` 的写面）**：落点全是 `_settings`（**单例、绑真实文件**）——**旧代行号**（43,920 B / `4FF241D1D43C` / 826 行）：`:590` 哨兵 `libraryPageSize`、`:596` 哨兵 `minimizeToTrayOnClose`、**`:603`** 哨兵 `proxyUrl`（`:604` = `Reload()`），② 步 **`:690`** 再写 `libraryPageSize=123`，③ 步 **`:710-715`** 用进函数时抓的 `s0` **还原** ⇒ 共 **5 次 `Patch`**（① 一步占 3 次）。⇒ 结论 = **旧代是"写后还原"、不是走临时根**；证据 = `contentEqual=True`（`6FD05EA70B06→6FD05EA70B06`、`51CD10DFD798→51CD10DFD798`）+ `BASELINE-RESTORED identical=True`；`bytesEqual=True` **只在那一次成立**（反例见上条 `:88`）；**mtime 必然变**，如实记。走临时根的只有**包往返那一段**（旧代 `:619-702`）。
🔴 **上述写面自 `t75` 起不再存在**（哨兵写全部搬到隔离根 ⇒ 真实文件的 `Patch` 次数 = **0**，`REAL-UNTOUCHED` 四件套 sha+mtime 全同，见 §15.11）。本段保留为**旧代留痕**，不得再当作现行行为引用。

**自捉并修掉的一处断言退化（G 代际）**：隔离刚落地时，配置包是从**空临时根**构的 ⇒ `parts=[Servers=0,Settings=0,Icons=0,SkipCache=0]`，"四段联动"退化为**恒真**（04:08:42 实测）。修法 = `:610-659` 构包前把真实四段**复制**进临时根（`SEED seeded=[servers.json,settings.json]`，只读来源、只写副本）⇒ 现读数 `5900B / Servers=1 / Settings=97`。同理 `RESTORE readBack=123 restoredToBackupValue=False` 是"隔离生效"的正常表现（它读真实根+内存单例）⇒ 另加读**临时根文件**的判据 `RESTORE-TEMP … match=True`。

**同代可失败控制（新做法：跑在一次性便携根上，全程不碰真实数据根）**
1. `E:\ui3-ctl-bin\data\`（`AppDataDir.Resolve()` 的便携根）内放 `servers.json` 副本并摆成产品形态 `{"servers":[…],"hiddenOriginalIds":[…]}` ⇒ `C2DA9E57742F / 1,627 B`（未扰动 = `6476F1F789B1 / 1,592 B`）；
2. 临时去掉两处注入根（`ExportAsync(selfCheckRoot)`→`ExportAsync()`；`RestoreAsync(selfCheckRoot, bundlePath)`→`RestoreAsync(bundlePathOverride: bundlePath)`）重编 ⇒ dll `E7E830AC0FC4` @04:12:57.216；
3. 从 `E:\ui3-ctl-bin\` 起跑（日志落便携根 `data\logs\aiplayer.log`）⇒ `SETTINGS-SELFCHECK-SERVERS-UNTOUCHED before=C2DA9E57742F after=6476F1F789B1 identical=False`（04:13:06.346，同跑 `END` 在 04:13:41.411）；便携根 `servers.json` 被改写为 `6476F1F789B1 / 1,592 B`。
4. **真实数据根全程未变**：`servers.json = 6476F1F789B1 / 1,592 B / mtime 03:47:43.642`（控制前后逐项相同）。回滚后源码 sha 回到 `4FF241D1D43C`（`FAILABILITY-CONTROL` 残留 = 0），重编绿，`E:\ui3-ctl-bin\` 整目录删除。

**Σ 三项（一行收口）**：`SERVERS-UNTOUCHED identical=True` ｜ `SETTINGS contentEqual=True`（**`bytesEqual` 仅"这一次"为真**，不是可承诺的性质）｜ `TEMPROOT 三读 False→True→False`；**可失败性**：同代控制跑 `identical=False`。
> 🔴 **本节（§14.1）与 §14 的写面描述是旧代留痕**：自 `t75` 起哨兵写全部落在隔离根，真实数据根**只被读**（§15.11）。此外本节的 `settings.json` 身份（`6FD05EA70B06 / 3,764 B`）**已过期** —— 现盘 = `E6220105E7CC / 3,769 B @06:29:23.380`。

## 15. t64 服务器管理页视觉缺陷收口（2026-09-12 06:05–06:30）

> 用户报障原文：「**很多边框很奇怪 还有地方有大片空余**」（服务器管理页）。本节 = 卡面三件事（① 奇怪边框定位 ② 大片空余量测 ③「已加载」与绝对路径清除）的逐条收口。
> **三态分开写**：**逆向到了** = 参照物的行结构（`UI_SPEC_SHELL.md:318`「一张连续卡片 + 通宽 1px 细分隔线 + 左右两栏：左图标固定列宽 / 中间标题白+副标题灰堆叠 / **右侧控件右对齐成一列**」、`:328`「行 `MinHeight="56"`、`Padding="16,0"`；卡 `Padding="0"`，由行自己撑高」）+ 参照物图 `refs/original-ui/hl-05-settings-printwindow.png`（设置页行式，肉眼核对：左组 + 右端列 + 中间留白）；🔴 **服务器管理屏在参照物里没有对应图**（`HILLSLITE_UI_ANALYSIS.md:93` 把「服务器管理（齿轮）」列入未覆盖面）⇒ 本屏是 **[设计]** 屏，形态依据只能是 §9.1 的行式；**重建实现了** = §15.1 两处改动在盘、源码 mtime 早于产物、构建 `EXIT=0`；**运行验证过了** = §15.3 改后图 + §15.4 三态逐像素读数 + §15.6 机器日志行。

### 15.1 改动面（仅两处，都在卡面 inScope 内）

| 文件 | 改动 | diff | 依据 |
|---|---|---|---|
| `Features/Servers/ServersPage.xaml` | ① 卡片 `Border` 加 `VerticalAlignment="Top"`（`:24`）；② 把 `:44` 注释里"预留"的最右列填成**真实状态读数** `TextBlock Grid.Column="3" HorizontalAlignment="Right" … Loaded="OnRowStatusLoaded"`（`:66-67`） | `+11/−2` | ① `UI_SPEC_SHELL.md:328`「卡 `Padding="0"`，**由行自己撑高**」；② captain 裁定 (甲)＝最右列填真实读数且右对齐、**不删该列** |
| `Features/Servers/ServersPage.xaml.cs` | `OnRowStatusLoaded`：`ServerConfig.Enabled ⇒ 启用 / 停用`，取不到值 ⇒ `—`（**绝不留空**） | `+21/−0` | 同上；🔴 **不引入** `{StaticResource}` / converter —— `DataTemplate` 内查资源正是崩溃代际的形态（探针 B：该形态在场即崩 `0xC000027B`），故走与 `:47-48` 同一套 code-behind `Loaded` 赋值 |

`:59-60`（`Text="{Binding Kind}"` 那行）本批**只被编辑器重排了空格**：`Grid.Column="2"`、`HorizontalAlignment="Left"`、`Margin="12,0,0,0"`、`Opacity="0.6"` 与 HEAD 逐项相同（`git diff` 原文见 §15.2 命令）。

### 15.2 身份与构建读数

```
命令    dotnet build shell\App\AIPlayer.Shell.csproj -c Debug -p:OutputPath=E:\ui3-search-bin\
        （两代**都未加** `-t:Rebuild` ⇒ 增量构建）
窗口    G1 2026-09-12 06:05:31.115 → 06:06:17.077（构建前 Get-Process dotnet = 0）
        G2 2026-09-12 06:23:45.811 → 06:24:32.447
HEAD    G1 构建时 3526ac3；G2 构建时的工作区 = 随后入库为提交 12f19be 的那棵树；取证时 HEAD = abb751e（06:12:08.613 实测）
结果    EXIT=0 ｜ error CS 行数 = 0 ｜ 1172 个警告 ｜ 0 个错误（两代读数相同）
产物    G1 = **E:\ui3-search-bin\AIPlayer.Shell.dll** ｜ 1,675,776 B ｜ sha256_12 847A6C685505 ｜ mtime 2026-09-12 06:06:10.535
        G2 = **E:\ui3-search-bin\AIPlayer.Shell.dll** ｜ 1,676,800 B ｜ sha256_12 B4D4C3056617 ｜ mtime 2026-09-12 06:24:24.617
        G3 = **E:\ui3-search-bin\AIPlayer.Shell.dll** ｜ 1,728,000 B ｜ sha256_12 92F0A93600FE ｜ mtime 2026-09-12 06:57:57.299
             （G3 = `t75` 那次构建，见 §15.11；**当刻盘上只有 G3**）
源码    G1: ServersPage.xaml = 6,117 B ｜ D63EB130445E ｜ mtime 06:05:12.438
        G1: ServersPage.xaml.cs = 25,266 B ｜ AA32EE7823D5 ｜ mtime 06:05:22.767
        ⇒ 两者 mtime 都早于产物（无竞态；产物确实包含本批改动）
**代际取代（三代不得并列引用为同一对象的身份；同一绝对路径）**：**G3 ⊃ G2 ⊃ G1**，**当刻盘上只有 G3**（`92F0A93600FE`），G2/G1 已被覆盖、**不可在盘上复钉**（`verifier` 2026-09-12 把 `E:\` 顶层递归 depth≤2 与仓库内 12 份 `AIPlayer.Shell.dll` 全量 sha12 扫过，`B4D4C3056617` / `847A6C685505` **0 命中**）⇒ 它们只作为"**某张图的绑定代际**"存在（§15.3）：图 ↔ G1（05:57 的改前图另绑 `17881AB30CD7`）。G1→G2 的差异 = **仅两处注释**里的 `🔴`(U+1F534) → ASCII `[!]`（H7 门禁整改，见 §15.9）：
        G2: ServersPage.xaml = 6,116 B ｜ 8E358B09F1FD ｜ mtime 06:23:40.585（−1 B）
        G2: ServersPage.xaml.cs = 25,265 B ｜ 45FBFC1FEB5B ｜ mtime 06:23:40.604（−1 B）
        ⚠️ G2 的重编同时含**他人当刻在途文件**（共享仓库），故 §15.3 那张图**仍绑定 G1**；图与 G2/G3 不是同一 sha，本条即声明（细节见 §15.9）。
**构建隔离的真实边界（reviewer 问过，如实写）**：`-p:OutputPath=E:\ui3-search-bin\` 只改**输出目录**，**不隔离 `obj/`** —— 实测 `E:\ui3-search-bin\AIPlayer.Shell.dll` 与 `shell\App\obj\Debug\net9.0-windows10.0.22621.0\win-x64\AIPlayer.Shell.dll` **逐字节同件**（同 sha12 + 同 bytes + 同 mtime），而与 `shell\App\bin\Debug\…\AIPlayer.Shell.dll`（1,632,256 B ｜ F049909B6A6E ｜ 05:30:28.915）**不同**。⇒ "隔离输出"防的是**输出面互踩**，不防 `obj/` 争用：多人并行时仍须先看 `Get-Process dotnet`（本卡两次构建前都是 0）。
**四件套口径（reviewer 2026-09-12 06:5x 提，已按此写）**：产物身份 = **绝对路径** + bytes + sha12 + mtime + 采样时刻 + HEAD + `EXIT`/`error CS` 数 + 是否 `-t:Rebuild`；只说 sha12 时第三方无法判断指的是哪一份（同一晚已因"路径不同、sha12 不同"核过 `libmpv` 与 `SettingsPage.xaml` 两处）。
```

### 15.3 取证通道与三张窗口图（通道必须声明）

| 图 | 通道 | hwnd / pid | sampled | bytes | sha12 | 守卫字段（侧车逐字） |
|---|---|---|---|---|---|---|
| `shell/App/Features/Servers/evidence/t64-servers-before.png` | 窗口级 `PrintWindow` | `hwnd=3017916 / pid=16404` | 01:55:43.798 | 60,908 | `5B22F81F2B97` | `fgChanged=False` |
| `shell/App/Features/Servers/evidence/t64-servers-after.png`（改前最近一代） | 窗口级 `PrintWindow` | `hwnd=71108296 / pid=5552` | 05:57:01.752 | 59,148 | `E12246A8B96B` | `fgChanged=False` |
| `shell/App/Features/Servers/evidence/t64-servers-fixed.png`（改后） | 窗口级 `PrintWindow` | `hwnd=72681682 / pid=20856` | 06:16:07.377 | 60,554 | `E9775EC1BB92` | `printwindowOk=True / noScreenCopy=true / noFullScreenCapture=true / focusChanged=True / stolen=False`（`fgAfterPid=6804` **不属于目标 pid** ⇒ 按 G5 修正版**记录不拒拍**） |

- **跨通道声明**：三张都是**窗口级 `PrintWindow`**（`shell/tools/popup-capture.ps1`，`flags=0x2`），**不是** `t30-settings-render8.png` 那种**进程内 `RenderTargetBitmap`**——两通道的读数不可互搬。
- **取证前置（如实记）**：抓改后图时单实例闸门（`Local\AIPlayer.Shell.SingleInstance.v1`）被**用户正在跑的发布版**持有（pid 7208 = `E:\AI Player\dist\AIPlayer\AIPlayer.Shell.exe`，start 06:06:42；`list-instances.ps1 -Samples 2 -IntervalMs 1200` 两采 `shells=1 / free=False`，06:12:07.033 与 06:12:08.363）。我没有改 `dist/`、也没有杀用户进程（`SingleInstance` 无 env 旁路；备选宿主 `shell/Tests/t30-host` 的 5 个模式无一承载 ServersPage，且按 captain 裁定"不改 t30-host"）⇒ 改为**闸门释放即自动接管**的轮询器：每 20 s 采样、连续 2 次为空才起我那份隔离构建、按 `-List` 选 `class=WinUIDesktopWin32WindowClass + size=1440x759` 的 hwnd、抓图后**只杀自己那个 pid**。日志 `%TEMP%\t64-ui3\poller.txt`：`06:13:35.496 POLLER-START` → `06:16:07` 抓图 → `06:16:08.951 ATTEMPT 1 killed pid=20856` → `06:16:09.147 POLLER-DONE png=… bytes=60554 sha12=E9775EC1BB92 dllSha12=847A6C685505 dllMtime=06:06:10.535`。

### 15.4 ② 大片空余：三态逐像素量测（四个数）

工装 `%TEMP%\t64-ui3\measure-servers-shot.ps1`（v3.1，单文件；一次运行 3.5–4.8 s；`EXIT=0`）。**定义（写死在脚本头部，故三态是同一个对象）**：`bg`=整帧众数色；`plate`=最外侧两条"稀有色的长竖直 1px 线"+它们共同的 y 跨距（= 卡片四边）；`inner`=plate 内缩 1px；`plateFill`=`inner` 众数色；`ink`=与 `plateFill` 的 `|dR|+|dG|+|dB| ≥ 24`；`colRun`=一段连续含 ink 的列（间隙 ≤6px 合并）；`maxInnerGap`=同一行内相邻 `colRun` 之间的**最大空白**；`rightBlank`=`inner` 右边界 − 该行最右 ink；`bottomBand`=`inner` 下边界 − 最下含有 ink 的行。

| 形态（图 / 代际） | 卡片矩形 | 行距 / 分隔线 | 每行内容分段（x） | **行内最大空白** | 行尾空白 | 底部带 |
|---|---|---|---|---|---|---|
| **01:55**（`t64-servers-before.png`；`5c96022` 之前） | x=288..1407 / y=167..631（1120×465，边框 `#1F1F1F`） | 40 px / **0 条**（阈值 0.9×1118 一条都检不出） | `[310-369 图标][482-675 名称+地址][1357-1389 类型]` | **681–724 px**（地址末尾 → 右对齐类型起点） | 17 px | 0 px（12 行填满） |
| **05:57 = 改前最近一代**（`t64-servers-after.png`；提交 `5c96022` 的行形态，`dll 17881AB30CD7`） | x=288..1407 / y=171..627（1120×457） | 56 px / **8 条 1px `#3E3E3E`** | `[315-338][354-519][545-577]` | 25–26 px | **824–859 px**（占卡内宽 1118 的 **74–77%**） | 11 px（第 9 行残条） |
| **06:16 = 改后**（`t64-servers-fixed.png`；本批两处改动） | x=288..1407 / y=171..627（1120×457） | 56 px / **8 条 1px `#3E3E3E`** | `[315-338][354-524][545-582][1363-1386 启用]` | **780–815 px**（类型 → 状态之间） | **20 px** | 11 px（第 9 行残条） |

**四个数（改后，直接回答卡面"大片空余量测"）**：卡内宽 **1118 px** ｜ 每行内容总跨 **1072 px**（首 ink 315 → 末 ink 1386）｜ 行尾空白 **20 px** ｜ 底部带 **11 px**。
**🔴 必须同时读的一条反证**：行内**最大空白**在改后是 **780–815 px**（类型 `…582` 与状态 `1363…` 之间）—— 即"大片空余"**没有消失，只是从行尾（824–859）挪到了行中（780–815）**；三个代际的这个数分别是 681–724 / 824–859 / 780–815，**从来都在 680–860 px 量级**。按 §9.1 的行式（左组 + 右端列），中间留白是参照物本身就有的版面语言（设置页参照图同形）；**要再缩小需要改行式（例如把类型也移进右端列、或让地址列吃满 `*`），超出本卡裁定 (甲)，已在 §15.7 挂 captain 裁定**。

### 15.5 ① 奇怪边框：逐条定位与裁定

| 屏上实际存在的"边框" | 实测（改后图） | 裁定 |
|---|---|---|
| 卡片四边 1px `#1F1F1F` | 竖：x=288、x=1407（`y=171..627` 各 457 px）；横：y=171、y=627（`MEASURE-PLATE` + `MEASURE-VLINES`） | §9.1「整页**一张通卡**」⇒ **保留**（`ServersPage.xaml:23-28`，`CardStrokeColorDefaultBrush`） |
| 行间通宽 1px `#3E3E3E` × **8** | y=223 / 279 / 335 / 391 / 447 / 503 / 559 / 615，每条 ink=1074 px，**间距恒 56 px** | §9.1「行与行之间是**通宽的 1px 细分隔线**（不是一行为一张卡）」+ `:328` 行 `MinHeight=56` ⇒ **保留**（`ServersPage.xaml:71-72`） |
| 行**内**独立边框 | 无 —— 行模板只有一条 `Grid.ColumnSpan="4"` 的底部 1px 分隔线；`BorderThickness="0"`（`:34`） | 符合 §9.1 纪律 |
| 窗口外框 7px `#000000` | x=0..6 / x=1433..1439（`MEASURE-SCANX`） | 窗口边框，**非本页绘制** |
| 列表底部第 9 行被裁成 **11 px 残条** | `innerBottomY=626`、最后一条分隔线 y=615 ⇒ 616..626 可见 | 16 台 > 8 行可见 ⇒ `ListView` 内部滚动裁切，**不是缺陷**（行高与分隔线都按 56 px 对齐） |
| **反例（更早代际）** | 01:55 形态：行距 40 px、**一条分隔线都检不出**（阈值 0.9×1118）、`x=847` 处 `y=168..630` 是**一段 463 px 的连续 `#222222`**（`MEASURE-SCANY`） | ⇒ 用户说的"边框很奇怪"里，至少含"行与行之间**没有**分隔线、只有 13 px 高的文字一条条贴在一起"这一支；该支在 `5c96022`（03:19:31）已改成 56 px 行距 + 通宽分隔线，**不是本批新增的边框** |

### 15.6 ③④「已加载」与绝对路径（已实测不存在 + 反例）

| 项 | 读数 | 反例（证明匹配器有效） |
|---|---|---|
| ③ 状态栏 `已加载` | `shell/App/**` 的 `.cs`/`.xaml` **0 命中**；本页唯一的状态栏 `StatusText` 在装载路径上被显式清空（`ServersPage.xaml.cs:220 StatusText.Text = string.Empty;`） | 同一 needle `已加载` 在 `shell/docs/*.md` 命中 **5** 处（`DESIGN.md:804`、`DESIGN.md:975`、`REVIEW_RV1.md:142`、`SPIKE.md:488`、`UI_SPEC_SHELL.md:391`）⇒ 扫描器能命中该串 |
| ④ 可见文案里的绝对路径 | `shell/App/Features/Servers/` 的**产品源码**（`*.cs`/`*.xaml`）中 `[A-Z]:\` **0 命中**；本页写 `StatusText` 的 15 处文案全是用户词（`「名称」来自原版配置，只读…` / `正在连接 {BaseUrl} …` / `连接成功：{Name}（{Kind} @{BaseUrl}）` …），`BaseUrl` 是用户自己的服务器地址，不是本机文件路径 | 同一 needle 在**同一目录**的取证文档与侧车里命中 85 处（`evidence/t30-settings-isolation.txt` 等）⇒ 扫描器有效；另有 `AppContext.BaseDirectory`（`ServersPage.xaml.cs` 的图标路径构造，**落盘/日志用，不进可见文案**） |
| 机器读数（与图同一跑） | 当跑日志 `2026-09-12T06:15:58.0127597+08:00  ServersPage loaded: 16 servers; enabled=16; originalAccounts=15; legacy=0; readOnly=15; file=C:\…\servers.json`（pid 20856，即本图那一跑；`file=` 只进**日志**） | 图上 8 个可见行的右端读数均为 `启用`，与 `enabled=16` 一致（状态列数据源 = `ServerConfig.Enabled`） |

### 15.7 未闭合 / 需 captain 裁定

1. **行内空白仍在**（改后 780–815 px；三态 681–724 / 824–859 / 780–815）—— 按裁定 (甲) 已把最右列填成真实读数、行尾空白降到 20 px，但"大片空余"的**主体是左组与右端列之间的版面留白**。可选收口（都需 captain 点头，我不自行改行式）：**(a)** 接受 = 与 §9.1 参照物同形（参考设置页同留白）；**(b)** 把「类型」也移进右端列（两段右对齐读数，留白视觉重量下降、宽度不变）；**(c)** 行改"密集表格"（名称 `Auto` / 地址 `*`+省略号 / 类型 `Auto` / 状态 `Auto`），留白被地址列吸收，但**违背"类型紧跟左组"**。
2. **`VerticalAlignment="Top"` 在本跑形态下未观测到差异**：本机 16 台 × 56 px = 896 px > 可用 455 px ⇒ 卡片两态都被行高夹住、`bottomBandPx` 两态都是 11 px。该改动只在"服务器数少到内容高 < 可用高"时生效（`:328` 的"由行自己撑高"），**本卡未取证该形态**；要补证需①一个 HEAD 代际二进制（现工作区源码已改）②一个短列表数据根（`servers.json` 只放 2–3 台），二者都需一段独立的构建 + 沙箱窗口。
3. **用户原图不在仓内** ⇒ 本节的比对基准 = **与卡面描述文字逐条比对 + 与 §9.1 参照物结构比对**，**未**与用户那张截图逐像素比对（缺前提，如实标注）。
   - 补：captain 在 `a533268`（06:3x）入库了用户当刻截图 `shell/Tests/evidence/user-shell-20260912-detail-and-rail.png`（用 760,617 B / 1,424×720，提交信息写"详情屏 + 左栏服务器图标 —— 供 t105/t102/**t64** 对照"）。**我逐像素看过：该图里是详情屏 + 左侧栏，不含服务器管理屏**（rail 行是图标+名称+「N 天前看过」，那是 §1/§3 的侧栏行式 = `ui` 领地）⇒ 本卡要对照的"服务器管理页整页"**仍缺用户原图**，第 3 条前提不成立这一点**不变**。
4. 15.4/15.5 的全部数字出自**我自己的量测脚本**（`%TEMP%\t64-ui3\`，非入库工装）；脚本头部的判据定义与三张图的 sha12 一起构成可复算面 —— 复核者可用同定义独立复算（脚本未入库，只在本节与本记录里给出定义）。

### 15.8 设计合规工装：显式声明通道后跑 `ui-design-audit.ps1`

```
命令  powershell -NoProfile -ExecutionPolicy Bypass -File shell\tools\ui-design-audit.ps1 `
        -Image shell\App\Features\Servers\evidence\t64-servers-fixed.png -Channel printwindow -WindowKind xaml -Origin 0,0
窗口  2026-09-12 06:20:26.963 → 06:20:42.446（14.5 s）；工具退出码 0
输出  evidence/t64-servers-fixed-audit.txt = 14,394 B ｜ sha256_12 C3B80BAEF507 ｜ 36 行
读数  AUDIT|…|channel=printwindow|windowkind=xaml|origin=0,0|sampled=2026-09-12 06:20:30.012
      RESULT|group=stats|item=NonBlackPct|verdict=PASS|measured=98.114% (1072352/1092960)
      RESULT|group=channel|item=ChannelWindowPair|verdict=PASS|measured=printwindow + xaml|note=R2: PrintWindow is valid for pure XAML windows
      SUMMARY|verdict=INCONCLUSIVE|pass=16|fail=0|inconclusive=18|exit=0
```

**0 FAIL / 18 INCONCLUSIVE 的逐条原因（工具自报，非本页缺陷）**：18 条 INCONCLUSIVE 全部来自工具自带的"探针集参考版面 `1403x794` ≠ 本图 `1440x759`"与"未传 `-TokenProbes` / `-PosterRect` / `-CardRects`"，工具 note 逐字为 `probe set reference 1403x794 != image 1440x759; pass -TokenProbes for this layout` 与 `geometry is layout-locked to the reference layout …; pass -ForceGeometry to judge anyway`。
**本页要判的几何我另有自有读数**（§15.4/§15.5）：卡片 `x=288..1407 / y=171..627`、行距恒 **56 px**、通宽分隔线 **8** 条、行尾空白 **20 px**。另有一条与 §1 令牌表对得上的读数：rail 底色 `#202020` 覆盖 `x=8..263` ⇒ **侧栏宽 = 256 px**，落在 §1 的 `256±2` 内（`MEASURE-SCANX|y=626` 行）。

### 15.9 H7 门禁红→绿（本批自捉自修一处）+ 身份代际 G1→G2

**红（06:23:21.849）**：**[工具自产]** `CHECK|H7-emoji-in-ui-copy|verdict=FAIL|scope=shell/App|ext=.cs,.xaml|…|emojiAstral=55|baselineAstral=53|newAstral=2|newFe0f=0|byOwner=ui2=18f/19+29,ui3=11f/21+11,UNMAPPED=12f/15+11`。那 **2** 处新 astral emoji **是我加的两处注释里的 `🔴`（U+1F534）**：`ServersPage.xaml:63`（新增注释行）与 `ServersPage.xaml.cs:61`（新增 XML 文档注释行）。按门禁自报的仓内 ASCII 约定改成 `[!]` —— **只动这两行**；`ServersPage.xaml.cs:137` / `:204` 里那两处 `🔴` 是 HEAD 的**基线库存**，不动（棘轮只允许减，不许我顺手改别人的在途文件）。

**绿（复跑 06:24:49.621 → 06:26:49.392）**：**[工具自产]** `CHECK|H7…|verdict=PASS|…|emojiAstral=53|baselineAstral=53|newAstral=0|newFe0f=0|byOwner=…ui3=11f/19+11…`；**[工具自产]** `SELF-CHECK|checks=10|pass=8|fail=0|inconclusive=2|sum=10|other=0|equal=True|source=checks=count(results);sum=count(PASS)+count(FAIL)+count(INCONCLUSIVE);other=checks-sum|at=2026-09-12 06:26:49.286`；**[工具自产]** `SUMMARY|verdict=INCONCLUSIVE|checks=10|pass=8|fail=0|inconclusive=2|scannedFiles=202|citedNames=135`；**工具退出码 0**。那两条 inconclusive 不是本卡面：`H2b`（某文档引用了不存在的 `t79-provenance-kernel2-20260912-042700.txt`）与 `H3`（他人文件里的 typed empty catch，工具自报 report-only）。

> **`SELF-CHECK` 行的出处自查（2026-09-12 06:4x，`verifier` 提醒后复跑）**：`Select-String 'SELF-CHECK' shell\tools\evidence-hygiene-check.ps1` = **2 处**（**写下时** `:900` 注释 + `:909` emitter；**当刻 `:905` / `:914`** —— `verifier` 2026-09-12 复核指出又漂了，正好再证一次"行号会漂、锚文本不会"）⇒ 自提交 **`9405ae4`（06:13:55）** 起该行是**门禁工具自产**，我引它时**连 `source=` / `other=` / `at=` 一起引**（那三列就是防"谁数谁"）。这与 `WORKSPACE.md` 事实 272 的原话不冲突：那句"该行不在门禁输出里"**在它写下的时刻为真**，此后已不再真（captain 已被告知需补一行）。另：事实 272 那句"`Σ三项`(设置页自检=3) 与 `checks`(门禁) 是不同对象、不得互比"**仍然成立**，本节两处引用分属不同对象。

**身份代际（如实标注，不做静默等价）**：§15.3 的改后图绑定 **G1**（`ServersPage.xaml D63EB130445E` / `ServersPage.xaml.cs AA32EE7823D5` / `dll 847A6C685505` @06:06:10.535）；H7 修完后的当刻源码是 **G2**（`8E358B09F1FD` / `45FBFC1FEB5B`），**G1→G2 的差别只有那两处注释里的 `🔴`→`[!]`**（`git diff` 可核：XAML 侧只多 1 行注释、CS 侧只多 `OnRowStatusLoaded` 的 21 行 + 1 行注释；`Grid.Column="3"` / `Loaded="OnRowStatusLoaded"` / `Enabled ⇒ 启用|停用` 这些**判据一字未改**）。因为差异落在注释层（不参与 XAML 渲染、不进 IL 语义），我**没有**为此再抢一次单实例闸门重拍：G2 的重编里同时含他人当刻在途文件，作为"那张图的产物"反而不如 G1 干净。**图与 G2 不是同一 sha，本条即声明。**

**他人当刻在途造成的红（06:31:16.977 复跑，逐条归属，不是本卡文件）**（以下三条均为门禁 **[工具自产]** 行的摘引）：`H2a=FAIL`（`shell/Tests/imgcache-bounded-probe/README.md:5` 引用的 `imgcache-bounded-awaits.txt` 未入库）｜`H2b=INCONCLUSIVE`（缺 `t79-provenance-kernel2-20260912-042700.txt`）｜`H7=FAIL`（`newAstral=1`，`byOwner` 显示 **ui2** 的命中数由 19 变 20 ⇒ 新 emoji 落在 ui2 领地；我的 `ui3=11f/19+11` 与绿跑逐字相同）。

### 15.10 量测判据 v1→v2（captain 裁定四）+ 卡片填充色逐代读数 + 发布可见性切分

**判据 v1（作废）**：`卡片填充色 = 满足「占整帧 ≥1% 且 bbox 宽 ≥ w/4 且 bbox 实心率 ≥0.80」的最大非众数色`。
**作废理由（实测）**：在 01:55 代图上它判出 `MEASURE-CARD|fillColor=NONE`（该代卡内众数色 `#222222` **就是整帧众数色 = 页底色** ⇒ 没有任何色块把卡片区域标出来，卡片只由 1px `#1F1F1F` 描边界定）。⇒ **它不是普适判据**：同一 UI 换一代际就判不出（改后一代的 `#2D2D2D` 反而能满足 v1 的三条）⇒ **判据不能押在"卡片有独立填充色"上，必须与结构绑定**。

**判据 v2（现行；定义写死在 `%TEMP%\t64-ui3\measure-servers-shot.ps1` 头部）**：先用「**稀有色的长竖直 1px 线**」定出卡片四边（`plate`，并排除 `x<8`/`x>w-8` 的全高窗口边框列），再相对 `plate` 内众数色算 `ink`，其余读数（分隔线、行内容段、右侧留白、底部带）全部相对这张卡片矩形；**行内空白** = 同一行内相邻 ink 列段之间的最大空隙（`maxInnerGap`，间隙 ≤6px 合并）。**v2 在三代图上都稳定出读数**（§15.4 表；三跑均 `EXIT=0`，3.5–4.8 s/张）。

**卡片填充色逐代读数（captain 要求"把这件事本身当读数"）**

| 代际 | 卡内众数色 / 占卡内 | 整帧众数色 / 占整帧 | 卡片外 gutter（`x=264..287` 实测） | 结论 |
|---|---|---|---|---|
| 01:55（`t64-servers-before.png`） | `#222222` / **97.04%** | `#222222` / 72.71% | `#222222` | **卡内色 == 页底色** ⇒ 无独立填充色，只有 1px `#1F1F1F` 描边（`#2D2D2D` 全帧仅 10,866 px = 1.0%） |
| 05:57（`t64-servers-after.png`） | `#2D2D2D` / 95.89% | `#2D2D2D` / 45.45% | `#222222`（292,438 px = 26.8%） | **卡内有独立填充色** `#2D2D2D` |
| 06:16 改后（`t64-servers-fixed.png`） | `#2D2D2D` / 95.65% | `#2D2D2D` / 45.34%（495,538 px ≈ 卡内面积 1118×455 = 508,690 的 **97.4%**） | `#222222` | 同上，有独立填充色；与 §1 令牌表 `RowSelectedBg #2D2D2D` **同值** |

⇒ **对 captain 那条推断的实测更正（只改事实，不改裁定）**：「卡内像素 = 页底色、只有描边没有色块」**只对 01:55 那一代成立**；当前代（05:57 起）卡片**有**独立填充色 `#2D2D2D`，它与页底 `#222222` 每通道只差 11/255 —— 正是这个极小的差让"大片空余"看上去是**一整片同色的空区**。**裁定结论不变**：改后读起来"很空"的原因是**没有内容占据那块宽度**（780–815 px），不是"没有色块"。

**发布可见性切分（captain 裁定的兜底行，逐字落地）**

> 修正体已于 `E:\ui3-search-bin`（`AIPlayer.Shell.dll` = `847A6C685505` / mtime `2026-09-12 06:06:10.535`）验证；**发布可见性 = 另立卡**（把 band + 状态列修复并入 `dist` 发布，由 Captain 在下一轮重发布时一并处理）。

（本卡实际走的是 **① 主路径**：闸门一释放就起隔离构建、抓它**真实窗口** 1440×759 的窗口级 `PrintWindow` 图，见 §15.3；用户当刻在跑的 `dist` 发布版**不含本批修复** ⇒ 他现在看不到这个改动，这一条即上面那句"另立卡"的由来。`dist/` 我全程未动 —— `kernel2` 的 `t98` 正在那里换内核产物。）

### 15.11 后续（`t75`）对 §14/§14.1 的取代：真实数据根的写面 = 0（2026-09-12 07:00）

> §14/§14.1 描述的是**旧代行为**（哨兵写落真实 `settings.json`、`mtime` 必变）。`t75` 把那三处哨兵写搬到**根作用域实例**上之后，本节的两条旧判据被下面两条取代。**原始读数**：`shell/Tests/evidence/settings-selftest-isolation-ui3-p6988-20260912-070012.txt`（9,223 B / `FB3A7D64FF45` / 103 行）。

| 面 | §14/§14.1（旧代） | §15.11（现行，`t75`） |
|---|---|---|
| 哨兵写落点 | `_settings`（单例，绑真实 `settings.json`） | **根作用域实例** `SettingsService.At(%TEMP%\t30-setcheck-<8hex>\settings.json)` ⇒ 真实文件 **0 次 `Patch`** |
| 基线核对 | 写回 `s0`（一次真实写） | **退化为只读**（`_settings.Reload()` + 逐字段比对，`BASELINE-UNTOUCHED identical=True`） |
| `settings.json` 判据 | `contentEqual=True`（**`mtime` 必变**） | **`REAL-UNTOUCHED`：sha256 与 `mtime` 都不变** —— 实测 `E6220105E7CC@2026-09-12 06:29:23.380` 在两臂 + 两条通道下逐字相同 |
| 四件套 | 只给了 `servers.json` 三元组 | 四件套各给前后戳（`servers.json`/`settings.json`/`icons.json`/`skipCache.json`）；**`icons.json`/`skipCache.json` 在本机不存在 ⇒ 这两条是空真，如实标注** |
| 可失败性 | 旧代用"临时打回真实根"的重编臂 | **同一二进制**的负控：`SHELL_SELFTEST_SETTINGS_NEG=1`（故意不导入）⇒ `RESTORE-TEMP match=False` + `ASSERT arm=negative verdict=PASS(断言可失败)` |

代际：该跑的产物 = **G3**（`92F0A93600FE` / 1,728,000 B / 06:57:57.299）；因本批又改了 `SettingsPage.xaml.cs`（+97/−42），**§14/§14.1 里的行号全部再次漂移**（826 → 881 行）⇒ 引用一律改用**锚文本**。

### 15.12 captain 对 §15.7 三条待裁的裁定（2026-09-12 07:1x，逐条落账）

| # | 裁定 | 本文档的对应状态 |
|---|---|---|
| **§15.7-1 行式** | **(a) 接受，不做结构改动** —— 依据 = §9.1 参照图（设置页）**同形**："名称/地址在左、状态在右、中间留白"是列表行的常态；(b)（类型也移进右端列）与 (c)（密集表格，违"类型紧跟左组"）**都先不做**。**(b) 作为"下一档"备选由用户决定**，不由我们悄悄改 | 反证**原样保留**且必须一起交付：**行内最大空白改后 = 780–815 px（类型与状态之间）；三态 681–724 / 824–859 / 780–815 px；"大片空余"没有消失、只是从行尾挪到行中**（§15.4 末段与本节） |
| **§15.7-2 底带** | **不必专门排窗口**：`VerticalAlignment="Top"` 在"16 台 × 56 = 896 > 可用高 455"形态下读数**不可观测** ⇒ 按 **"改法已落地 + 前置不可满足"** 结单，**是合法的 INCONCLUSIVE-with-precondition，不是未完成**；若下次顺手有短列表沙箱（2–3 台启用）补一次即可，**不额外排窗口、不计欠账** | 两个读数已在 §15.7-2 写明：**896 > 455** 与 **两代际 `bottomBandPx` 都是 11 px** |
| **§15.7-3 用户原图** | 我判得对：`a533268` 入库的那张**确实是详情屏 + 左侧栏、不含服务器管理屏** ⇒ "与用户原图逐像素比对"**前提不成立**，captain **不再要求**；他已**再次向用户索要"服务器管理页"那张图**，拿到后转我**追加一轮（不重写）** | §15.7-3 原文保留；本条为追加 |

> 追加执行：本条为 **append-only**（不改上面任何既有行）。captain 同时说明：**用户已授权「窗口该杀就杀」**（故 ⑤ 那条"占用闸门 12 s"的报备已被接受）；我对此的自我约束不变且更严：**仍先等/轮询**，只有在**确实阻塞**时才处置，且**处置前留 pid + path + start 三者**、**只针对阻塞我的那一支**（对队友的实例优先先对表 —— 杀掉别人在跑的一支会毁掉他当刻的读数）。

### 15.13 《身份不可复核声明》+ 源根换根 + 残余 emoji 存量（2026-09-12 07:2x，append-only）

> 起因：`reviewer` 2026-09-12 对账时实测 —— **本文档与同批证据文件里引用的产物身份，除当刻盘上那一支以外，全部已不可在盘上复核**（重编覆盖 / 目录已删）。**逐条点名如下**；本节只作**声明**，不改任何既有行、不改任何既有读数。

**(a) 引用过的 `AIPlayer.Shell.dll` 产物身份与当刻可核性**

| 身份 | 出处 | 当刻状态（reviewer 全盘扫描 + 我复查） |
|---|---|---|
| `847A6C685505` | 本文档 §15.2 G1 | **已不在盘**（同一路径被后续构建覆盖） |
| `B4D4C3056617` | 本文档 §15.2 G2 | **已不在盘** |
| **`92F0A93600FE`** | 本文档 §15.2 **G3** / §15.11 | **在盘**：`E:\ui3-search-bin\AIPlayer.Shell.dll` = 1,728,000 B / mtime 2026-09-12 06:57:57.299（当刻唯一可核的产物身份） |
| `17881AB30CD7` | §15.3 的 05:57 改前图绑定代际 | **推断已覆盖**（同一路径被 G1→G3 相继覆盖，未单独扫描，如实标注"推断"） |
| `BD3DC7C899D5` / `DA9C65A146E4` | §14.1 代际身份行 | **均已不在盘** |
| `85BCEA3F5C34` / `E7E830AC0FC4` | §14 可失败控制的控制支 | **均已不在盘**（控制跑目录 `E:\ui3-ctl-bin\` 整目录已删） |
| `A72CD762B3A0` / `3733D183EDAE` / `618C068D01A0` | 同批证据文件（`evidence/t30-probeB-icon-source.txt`、`evidence/t30-settings-isolation.txt`）引用 | **均已不在盘**；`E:\ui3-probe-bin\` **整个目录不存在** ⇒ 探针那两代无处可核 |

⇒ **口径**：以上身份一律只能当"**当时凭据**"读；**复核者要复核行为，只能按同一源码代重建（重建产物自带新 sha12，不得声称与旧代同支）**。下次重跑时在本节**追加**新身份与时刻（不改旧行）。

**(b) 🔴 源根换根（`t110`，影响"产物 vs 源码"这类判据的基线）**

`a60188a`（2026-09-12 06:43:09）`feat(shell+kernel): t110 —— 内核 fork 并入外壳（编译源根 reversed/MpvHost/** -> kernel/src/**）`；当刻 `shell/App/AIPlayer.Shell.csproj:206` = `<Compile Include="…\kernel\src\**\*.cs" …>`。
⇒ **本卡 §15.2 的 G3（06:57:57）与 `t75` 的产物都在这次换根之后构建** ⇒ 它们**含 fork 内核代码**，不再是"原版内核源码"那一支。
⇒ 后果：`t30-probeB-icon-source.txt` 里 `dllNewerThanSource=True` 这类判据的"源码基线"**已换根**；**任何重跑前必须先声明基线**（reversed 还是 kernel/src），否则新旧两代产物在该轴上**不可比**。本卡不重跑，只声明。

**(c) 残余 emoji 存量（本卡两个源文件并非"无 emoji"）**

`reviewer` 实测 + 我复算一致：`ServersPage.xaml` **astral=0 / U+FE0F=1**；`ServersPage.xaml.cs` **astral=2 / U+FE0F=5**。这些都是 **H7 基线存量**（门禁当刻 `newAstral=0 / newFe0f=0` ⇒ 本卡合规），**但若将来有人下调基线，它们首当其冲** —— 记在这里，免得后人读 §15.9 以为"已清干净"。

**(d) 量测工装入库（`reviewer` ③：判据必须能被第二人复算）**

原先四项量测的工装只在 `%TEMP%\t64-ui3\` 下（**一清即无** ⇒ 别人只能看输出、无法复算判据）。现已**入库**：
```
shell/App/Features/Servers/evidence/measure-servers-shot.ps1 = 17,532 B / sha256_12 DA614DBB7067 / 287 行
  判据正文 = 文件头部的中文定义块（bg=全帧众数色｜plate=最外 1px 长竖线矩形｜inner=plate 内缩 1px｜plateFill=inner 众数色｜
  ink=|dR|+|dG|+|dB| >= 24 vs plateFill｜sepRow=ink 跨 >=90% inner 宽｜band=0<ink<90% 的极大连续行段(gap<=3 合并)｜
  rightBlank / leftBlank / bottomBand / maxInnerGap）
  调用（对已落盘 PNG 复算，不需要窗口/实例/场地）：
  powershell -NoProfile -ExecutionPolicy Bypass -File shell\App\Features\Servers\evidence\measure-servers-shot.ps1 `
      -Png shell\App\Features\Servers\evidence\t64-servers-fixed.png -Tag fixed [-Dump <out.txt>]
  自查：只读像素，**无屏幕拷贝、无按名批量结束进程**（门禁 H1/H6 适用面）。
```
⇒ **§15.4 的三态数字现在可由第二人用同一条命令独立复算**（三张图的 sha12 都在 §15.3/§15.6）。`t64-servers-fixed-audit.txt` 是 `ui-design-audit.ps1` 的**自产物**，我不往里面追加（保持"工具产物不被改写"），其**实际支撑面**（structural + contrast PASS；token/geometry 组全 INCONCLUSIVE）已在 §15.8 写明 —— **t64 收口必须按这个支撑面写，不得写成"全判据通过"**。

**(e) 更正 §15.9 里对 `H3` 的描述（判据后来收紧了；append-only 更正）**

§15.9 写"两条 inconclusive 不是本卡面：`H2b` … 与 `H3`（他人文件里的 typed empty catch，工具自报 report-only）"—— 那句**在它写下时成立，现在已过期**：
```
（`services` 2026-09-12 07:2x 提出；我自己 07:30:15.346 @HEAD 49b9cd6 复跑同一句）
CHECK|H3-empty-catch|verdict=**PASS**|measured=csFiles=177 emptyCatch=0 (untyped=0 typed=0)
  ⇒ 那 4 处（`t61/t65/t70` 三处 + 另一处）已在提交 `7141126` 按"窄类型 + 一行理由"改完，emptyCatch 归 0；
    且 `verifier` 后来把该判据收紧 ⇒ **它不再是 report-only 的 INCONCLUSIVE**
CHECK|H2a…=PASS(citedNames=158 untracked=0) ｜ CHECK|H2b…=PASS(missing=0)
CHECK|H7…=PASS(emojiAstral=52 / baselineAstral=53 / newAstral=0 / newFe0f=0) ｜ byOwner=…**ui3=11f/19+11**（与本卡修复后逐字相同）
SELF-CHECK|checks=10|pass=10|fail=0|inconclusive=0|equal=True|at=2026-09-12 07:30:15.346
```
⇒ 口径更正：**当刻"红只来自 H7/H2a"不成立**（两格都 PASS、全场 10/10）。我早前那三句（06:2x–07:0x 的 H2a/H3/H7 状态描述）一律按**各自采样时刻**读，**不要跨时刻搬运**。
另：本节写完后我用 `git status --porcelain -- shell/App/Features/{Servers,Settings,Search}` 复核实为**空**；同一次门禁的 `H3b` 顺带暴露 `newestScannedMtime=ServersPage.xaml.cs@2026-09-12 07:26:12.351` ⇒ **那两处"他人的在途注入态"当刻仍在**（与本卡无关，我一字节未碰）。

**(f) 更正 §15.8 / §15.13(d) 的"支撑面"数字（我转述了 `reviewer` 的误报而没自查 —— 现两路自证更正，append-only）**

先前那两句写的是"`token` **11** 条与 `geometry` 6 条全 INCONCLUSIVE；唯一 PASS 面 = structural + `contrast` **4** 条"。那是 `reviewer` 首轮**只读审计文件前 25 行**得到的数，**我转述时没有复算**（责任两端：他误报，我未核）。现按**两条独立路径**自证：
```
路径A（我逐行分组，正则 ^RESULT\|group=([a-z]+)\|item=([^|]+)\|verdict=([A-Z]+)）：
  34 条 RESULT = channel/PASS 1 ｜ contrast/PASS **12** ｜ stats/PASS 3 ｜ geometry/INCONCLUSIVE 6 ｜ token/INCONCLUSIVE **12**
  ⇒ PASS=16 / INCONCLUSIVE=18
路径B（同一文件自带的 SUMMARY 行）：SUMMARY|verdict=INCONCLUSIVE|**pass=16**|fail=0|**inconclusive=18**|exit=0 ⇒ 与路径A 逐数相符
```
⇒ **正确口径**：t64 当刻的支撑面 = `ui-design-audit.ps1` **自产物**侧 **stats 3 + channel 1 + contrast 12 = 16 条全 PASS**；**token 12 + geometry 6 = 18 条全 INCONCLUSIVE**（参考布局 `1403x794` ≠ 图 `1440x759`、采样点未钉、Poster/Card/CornerRadius 需 UIA）；**structural 面**（卡片 `1120×457`、**8** 条 1px 分隔线、`rightBlank 20 px`、`bottomBand 11 px`）由入库工装 `measure-servers-shot.ps1` 提供（见 (d)/(g)）。**收口时不得写成"全判据通过"。**

**(g) 第二人复算留痕（`reviewer` 2026-09-12，署名）—— 这正是 (d) 入库工装的目的**

```
命令：powershell -NoProfile -ExecutionPolicy Bypass -File shell\App\Features\Servers\evidence\measure-servers-shot.ps1 `
      -Png …\t64-servers-fixed.png -Tag fixed -Dump %TEMP%\rv-t64-recompute.txt
输入自查：MEASURE-INPUT|bytes=60554|sha12=E9775EC1BB92|mtime=2026-09-12 06:16:07.264 ⇒ 与我 r1 核过的抓图**同件**
关键读数：MEASURE-PLATE|#1F1F1F|x=288..1407|y=171..627|1120x457 ｜ MEASURE-INNER|plateFill=#2D2D2D|95.65%
        MEASURE-SEPARATORS|count=8（y=223/279/335/391/447/503/559/615，每行 ink=1074）
        MEASURE-BAND i=0..7 全 h=33 ｜ leftBlankPx=26 ｜ **rightBlankPx=20** ｜ colRuns=4
        MEASURE-BOTTOMBAND|innerBottomY=626|lastInkRowY=615|**bottomBandPx=11**
        MEASURE-SUMMARY|widestRightBlankPx=20(band 0) ｜ **widestInnerGapPx=815**(band 2 at x=548)
dump 身份：4,857 B / `82ED918CF710`
```
⇒ **"判据可被第二人复算"成立**（他用自己的机器、对已落盘 PNG、不需要窗口/场地，得到的数与 §15.4 逐项相同）；他同时**独立看到**修复效果本身：每行 `rightBlankPx=20`（最右列不再是大片空白）、8 条分隔线与 8 个内容带一一对应、底部仅余 11 px。

**(h) 抓图驱动链的**输入凭据**入库（`reviewer` 建议；驱动器本身不入库）**

```
新增：shell/App/Features/Servers/evidence/t64-capture-poller-log.txt = 1,410 B / sha256_12 12412F21A9F5 / 11 行
逐字内容（工具行）：POLLER-START 06:13:35.496 → POLLER-GATE-FREE 06:15:55.766 → ATTEMPT 1 launched pid=20856 06:15:55.943
  → ATTEMPT 1 hwnd=72681682 capturing → CAPTURE exit=0 → cap| CAPTURE|channel=printwindow|flags=0x2|size=1440x759
  → cap| PNG|bytes=60554|sha256_12=E9775EC1BB92 → cap| GUARD|focusChanged=True|focusStolenFromTarget=False|printwindowOk=True|noScreenCopy=true|noFullScreenCapture=true
  → ATTEMPT 1 killed pid=20856 → POLLER-DONE … sha12=E9775EC1BB92 dllSha12=847A6C685505
```
**驱动器脚本不入库**（按 `reviewer` 的理由）：它是**驱动器**（等闸门 / 起自己的进程 / 只杀自己 pid），不是**读数器** —— 不绑 `.md` 引用、判据与读数已写进 §15.3；若将来 `臂 3` 需要把"闸门等待 + 自起 + 只杀自己 pid"做成**可复用方法**，那应当是 `shell/tools/` 下的新工具，**冻结期由 captain 定**。

**(i) 更正 (e) 末句：那两处注入态"仍在"是误读 —— 实际 `07:26:12.350/.351` 就是复原时刻**

(e) 末句说"他人的在途注入态当刻仍在"：**不成立**。我 2026-09-12 `07:31:28.231` 只读实测 = `git diff -- <两条路径>` **0 行（空）**、`ServersPage.xaml` = 6,116 B / `8E358B09F1FD`、`ServersPage.xaml.cs` = 25,265 B / `45FBFC1FEB5B` ⇒ **与 §15.2 记的"当刻源码"逐字相同（漂移为零）**。注入期完整窗口 = `xaml 07:20:46.580 → 07:26:12`、`cs 07:21:22.081 → 07:26:12`（另 `07:24:32.514` 动过一次）；注入态身份 = `xaml 5,151 B / 5378C81D5D53`、`cs 24,361 B / 7A6B2944E310`（与探针代 `5,100 B/DE652FDA9CAA`、`24,220 B/34CCA087243C` **并列、不替代**，结论只写"在该（新）源码代下"）。

**(j) 形态判据升级（`reviewer` 2026-09-12 提出；我从"只有 EVIDENCE_T30.md 不同"升到一条事前可判的规律）+ 入库读数器**

**规律（我自己用 `git check-attr` 逐件复核，与 `.gitattributes` 原文一致）**：
```
两形态是否相同 ⟺ 该路径的 `text` 属性是否被 unset
· text: set   （第 3 行 `* text=auto eol=lf` + 第 5/9/10/14/24 等逐扩展名 `text eol=lf`：*.cs/*.md/*.txt/*.xaml/*.ps1…）
              ⇒ 工作区含 CR 时入库必归一化 ⇒ 两形态不同，差 = CR 个数
· text: unset （第 47 行 **/evidence/** -text 覆盖；其理由见 :42-46 的注释：
              证据身份由 sha256_12 认定，归一化会让"已验证"静默变假，实测反控见 WORKSPACE.md 事实 152）
              ⇒ 永不归一化 ⇒ 两形态逐字节相同（哪怕工作区是 CRLF）
⚠️ 防误读：`eol=` 在 `text: unset` 时是**装饰性**的（实测 `evidence/rail-probe.ps1` 报 `eol: crlf` 仍逐字节同存）。
⚠️ 反锚（会推翻本条）：出现「`text: set` + 工作区含 CR 但两形态相同」或「`text: unset` 但两形态不同」。
```
⇒ 所以"那份清单里只有 `EVIDENCE_T30.md` 两形态不同"**不是巧合**：它是清单里**唯一"非 `evidence/` 子树 + 工作区 CRLF"**的文件。
**本卡三件的当刻形态读数（读数器物化后实测）**：
```
EVIDENCE_T30.md          worktree 88,386 B / 67FFE85BE624 / CR=589 ｜ blob 395e36d3b601 = 87,797 B / EFE0BA0A0BA3 / CR=0 ｜ text=set  ｜ **same=False（差 589 B）**
PROBE_B_PROTOCOL.md      worktree 16,040 B / 23C8CBDCCBEA / CR=0    ｜ blob 531c9a31138d = 16,040 B / 23C8CBDCCBEA      ｜ text=set  ｜ same=True（工作区恰为 LF）
evidence/id-forms.ps1    text=unset ⇒ 两形态必同；evidence/rail-probe.ps1 同上
```
**读数器入库（`reviewer` 建议、由我落在我自己的面）**：`shell/App/Features/Servers/evidence/id-forms.ps1`
（ASCII-only、**只读**：无屏幕拷贝、不驱动/结束任何进程；仅在 `%TEMP%` 建一个临时文件物化 blob 后立即删）。
调用：`powershell -NoProfile -ExecutionPolicy Bypass -File <this> -Files "p1,p2,p3" [-Dump out.txt]`（也接受多个位置参数）。
⇒ 若要把它提升为 `shell/tools/` 下的公共工装，需 captain 建卡 + `verifier` 落（`shell/tools/` 不是我的面）——**本卡不动它**。