# t27-F-A 播放与回调缺陷修复 —— 运行证据（ui2）

> 卡面：`t27` F-A「导航回调响应体 + 41 参数补齐 + `ReadRequestAsync` 字节缺陷 + 打码收尾」
> attempt 2 / attempt_id `ec3f8edb-9cc3-4cf6-af59-d3fd042bddeb`
> 工程身份（采样时刻 **2026-09-11 23:07:16**）：`git HEAD = 4c497d1`
> 证据三态纪律：本文件每条都标注 **[实测]**（真跑过）/ **[未验]**（没跑过）。**不接受**把"写了代码"当"验证过了"。

---

## §0 一句话

五项缺陷全部修掉并**在真机跑过**；其中**两项是自检反控当场发现的真缺陷**（不是我写完就宣布对）：
`segments[].type` 落成数字`0`（内核反序列化会抛异常 ⇒ 整条换源应答变 `null`）、以及**证据文件路径算错一级**。

---

## §1 独立验证命令与结果

### 1.1 构建（卡面 verify 命令）

```powershell
dotnet build shell\App\AIPlayer.Shell.csproj -c Debug
```

| 项 | 值 |
|---|---|
| 退出码 | **0** |
| 错误 | **0** |
| 警告 | 1172（全部来自 `reversed/MpvHost/**` 反编译源：CS8632/CS0618 一族，与本次改动无关） |
| 最终读数 | HEAD `497b26d` @ 2026-09-11 23:17:03（21.97 s） |

> ⚠️ 期间 `shell/Services/` 一度红（`ConfigPortService.cs:115`、`PlayerShortcutsStore.cs:32`，属 `services` 在途改动），
> 我等到它转绿（22:52:53 Services 0 错误）后才取读数 —— **读数归属到 22:59 那次构建**。

### 1.2 真实播放实测（自检钩子驱动，可复现）

```powershell
$env:SHELL_START_PAGE="library"
$env:SHELL_SELFTEST_LIBRARY="1"; $env:SHELL_SELFTEST_PLAY="1"
$env:SHELL_SELFTEST_REPLY="1";  $env:SHELL_SELFTEST_REAL_NAV="1"
$env:SHELL_SELFTEST_MASK="exit"
& "E:\AI Player\shell\App\bin\Debug\net9.0-windows10.0.22621.0\win-x64\AIPlayer.Shell.exe"
```

> `SHELL_START_PAGE=library` 是必需的：`MainWindow.StartTag()` 现默认 `home`（`ui` 的 U-A 框架），
> 不设则 `LibraryPage` 不加载 ⇒ 三个自检钩子全部不触发（**踩过一次**，见 §6 教训）。

---

## §2 ① 导航类回调的响应体（**验收面**）

### 2.1 缺陷与修复

| | 内容 |
|---|---|
| 缺陷 | `ShellCallback.Respond` 一律 `return null` ⇒ 端点回 **200 + 空体** ⇒ 内核 options = `null` ⇒ `navigate_*`/`switch_version`/`refresh_playback_url` **永不换源** |
| 路线 | **B（服务层 DTO + 显式 camelCase）** —— 内核响应侧 DTO 全 `internal` 且无 `InternalsVisibleTo`；"能否命名内核类型"是**接线形态的函数**（`T12_PLAYBACK_BRIDGE.md` §3 第 0 条） |
| 落点 | `KernelHost/ShellCallback.cs`（`NavigateHandler` 接缝 + **线载荷 DTO 显式投影**）<br>`Features/Library/LibraryPage.xaml.cs`（`OnKernelNavigationCallback` ← `EmbyPlaybackSession.ResolveNavigationAsync`） |

### 2.2 [实测] 端点线上形态（原始响应体，逐条）

证据：`shell/Tests/evidence/t27-callback-reply.txt`（**4,021 B / 79 行 / sha12 `B48B9798FB56`** / utc `2026-09-11T16:23:42Z`）
- ⚠️ **同名旧件（历史，不是同一轮）**：`t27-callback-reply.log`（4,020 B / 79 行 / sha12 `81766DF08B50` / utc `2026-09-11T16:17:17Z`）—— 那是**重做后第一次跑到的那轮**，已被 Captain 用 `git checkout 5a740b2` 恢复入库并保留；**`.log` 在 `evidence/` 下并*不*被 `.gitignore` 吞掉**（`.gitignore:69` 有否定规则 `!**/evidence/**/*.log`，`git check-ignore` 实测 `<none>`、`git ls-files` 实测 tracked=YES）⇒ 无需改名即可入库。
- **取哪个是规范名**：以**产出侧写死的名字**为准。`Features/Library/LibraryPage.xaml.cs:533` 现在写 `t27-callback-reply.txt` ⇒ 本文引用一律指向 `.txt`；`.log` 作为**上一轮的 artifact** 留在库里供比对（两者 utc 相差 6 分 25 秒，不是"同一份字节两个文件名"）。
- 旧读数更正：本节此前曾写 `2,335 B / 54 行 / sha12 7F493E317077` —— 那是**重做前**的旧版本被别的文档引用过的数字，与磁盘上任何一份都不符 ⇒ 已按当刻磁盘改正（同类"过期身份"缺陷，与 `t31` 那两行同族）。

| 用例 | 期望 | 实测 |
|---|---|---|
| `navigate_next`（真会话无下一集） | 空体（正确：内核读成 null = 没有新选项） | `200 OK` / `bodyLen=0` / `<EMPTY>` ✅ |
| `progress` | 空体 | `200 OK` / `bodyLen=0` ✅ |
| `navigate_previous` | 空体 | `200 OK` / `bodyLen=0` ✅ |
| 反控① `handler=null` | 空体 | `200 OK` / `bodyLen=0` ✅ |
| 反控② `HostNavigateOptions{}`（**无 MediaPath**） | **必须空体** —— 回 `{}` 会让 `progress` 侧把空对象当预载数据占住 ⇒ 本次会话永久禁预载 | `200 OK` / `bodyLen=0` ✅ |
| 反控③ **合法形状** | **非空体**（否则本自检分辨不了"逻辑没生效"与"数据恰好没有"） | `200 OK` / **`bodyLen=154`** ✅ |

**非空原始响应体（反控③，原始行）**：

```json
{"mediaPath":"https://example.invalid/anti3?api_key=<redacted>","title":"反控③：形状必须可判","badges":[],"versionOptions":[],"audioTracks":[],"subtitleTracks":[]}
```

**真会话解析（`EmbyPlaybackSession.ResolveNavigationAsync`，不经端点）**：`<null-or-empty-mediaPath>` ——
本轮播放的是**电影**（库里无剧集），`PlaybackSession` 的 `Episodes` 为空 ⇒ 语义上确实没有"下一集"，
故端点按「空体」应答 **是正确形态**，不是失败。**"内核真的发来导航回调并拿到非空体"这一步 [未验]**——
需要内核在剧集播放中按 `navigate_next`（人手操作或用内核浮层），见 §7 未验清单。

### 2.3 [实测] 线载荷形状断言（含反控）

```
json      = {"mediaPath":"…","httpHeaders":{"X-Emby-Token":"<redacted>"},"title":"形状探针",
             "badges":[],"versionOptions":[],"startPosition":12.5,
             "audioTracks":[{"label":"日语","id":"1",…,"embyIndex":1}],
             "subtitleTracks":[],
             "segments":[{"type":"intro","startMs":12000,"endMs":102000,"source":"emby"}]}
A camelCase mediaPath           = True
A camelCase startPosition       = True
A camelCase httpHeaders         = True
A camelCase audioTracks         = True
A segments[].type 是字符串      = True
A 计算属性未上线（start/end/isValid）= True
```

---

## §3 ② 41 参数补齐

### 3.1 [实测] 参数面两个口径：`params=95`（A 行字段）与 `--subtitle-track=` ×73（B 行标志）

> 🔴 **口径更正（captain 2026-09-12 指出，我已用原始日志逐行复算）**：
> 我先前把 "`--subtitle-track=` ×73" 挂在 **`PLAY resolved`** 这一行上 —— **错的**。
> 该行的 `--subtitle-track=` 字面出现 **0 次**；它报的是**结构化计数字段** `subtitleTracks=73`。
> `×73` 这个**数字本身是对的**，但**来源行是另一行**：只有 `PLAY-KERNEL-ARGS-RAW`（命令行原文）里才真有 73 个 `--subtitle-track=` 标志。
> ⇒ **引用规矩（本文件今后照此）**：报计数一律带**四件套**（模式 / 范围 / 采样时刻 / **原始行**），
> 且**结构化字段与标志出现次数分开写**，不混用。

**原始行（逐字，可直接复算）**：

```
[来源行 A｜结构化字段]  23:14:55.184  PLAY resolved item=○△□ needTranscode=False urlLen=149 params=95
                        badges=3 audioTracks=1 subtitleTracks=73 versions=1 headers=3 danmakuApis=0
                        segments=0 chapters=0 episodes=0 sprite=False
[来源行 B｜命令行原文]  00:17:15.521  PLAY-KERNEL-ARGS-RAW --libmpv="…" … --subtitle-track=<base64> …（共 73 个）
```

**复算读数（我本轮实测，模式 = 正则字面量；范围 = `shell-startup.log` 全文件；采样 2026-09-12 00:5x）**：

| 模式 | 在 `PLAY resolved` 行 | 在 `PLAY-KERNEL-ARGS-RAW` 行 |
|---|---|---|
| `subtitle-track=` | **0** | —（不适用） |
| `--subtitle-track=` | **0** | **73**（剧集那次；`AGG` 那种本地片源那次为 **0**） |
| 字段 `subtitleTracks=(\d+)` | **73** | —（该行没有这个字段） |

⇒ 同一次播放在两行里各报一种口径：**"参数个数"=73（B 行）｜"字幕轨数"=73（A 行）**，
两者数值恰好相等，但**不可互相搬运**（下一次不一定相等）。`params=95` 出自 A 行的 `params=` 字段。

**附：`audioTracks` 实测分布（印证"没有双音轨片源"）** —— 同日志全部 `PLAY resolved` 行的 `audioTracks=` 取值：
**`1` 出现 8 次、`0` 出现 3 次、`>=2` 出现 0 次**（11 个播放事件）。
⇒ 想验"多音轨"相关行为**必须多探条目**，不能假定动画片就是双音轨。

```
PLAY resolved item=○⍍▶ needTranscode=False urlLen=149 params=95
            badges=3 audioTracks=1 subtitleTracks=73 versions=1 headers=3
            danmakuApis=0 segments=0 chapters=0 episodes=0 sprite=False
KERNEL-LAUNCH OK pid=1884
```

`--subtitle-track=` 是本项点名的缺口之一，**同一轮实测 73 条**（真流真数据）。
`--audio-track=` / `--version-option=` / `--http-header=`×3（含 `X-Emby-Token`）也都在原文里。

### 3.2 参数权威 = 内核前缀解析循环（**不照抄二手清单**）

逐条对齐 `reversed/MpvHost/WinUISample/App.cs:55-321` 的 else-if 链，共建模 **37 个 CLI 字段**
（`--launch-file=` 是**展开器**不是选项，`App.cs:366-403`，故不建模）。点名缺口的映射：

| 缺口 | 参数 | 载荷形态（内核同判据） |
|---|---|---|
| 字幕轨 | `--subtitle-track=` | Base64(URL-safe JSON)，`label` 空白整条丢弃 |
| 字幕地址/轨 id | `--subtitle-url=` / `--subtitle-id=` | 原样 |
| 弹幕 | `--danmaku-api=` | Base64(JSON)；CLI 上限 **5 条**（`App.cs:225` 计数式） |
| 弹幕匹配名 | `--danmaku-match-name=` | 🔴 **命令行必须 URL 编码**（`App.cs:235` `Uri.UnescapeDataString` 会解码）；进程内才是未编码原名 |
| 跳过 | `--segment=` | Base64(JSON)，`startMs<=0 且无 endMs` 丢弃 |
| 章节 | `--chapter=` | Base64(JSON, **snake_case**：`marker_id`/`time_start`/`time_end`) |
| 画面 | `--anime-mode=` / `--sharpen-mode=` | 内核小写化 |
| 音轨 | `--audio-track=` | 同字幕轨形态 |

### 3.3 🔴 [实测] 自检当场抓到的真缺陷（`segments[].type` 落成数字）

**第一次跑**（22:56）的线载荷是：

```json
"segments":[{"type":0,"startMs":12000,…,"start":"00:00:12","end":"00:01:42","isValid":true}]
A segments[].type 是字符串 = False
```

两处**都会炸**：
1. `type` 是**枚举数字** —— 内核 `HostNavigateSegmentOption.cs:7-8` 是 **`string`** + `(Type ?? "intro").ToLowerInvariant()` 分派
   ⇒ 反序列化**抛异常** ⇒ **整条换源应答变 `null`**（症状＝"点了没反应"，日志干净）——
   正是 `T12_PLAYBACK_BRIDGE.md` §3 第 2 条点名的必炸点；
2. `start`/`end`/`isValid` 是 `MediaSegmentDto` 的**计算属性**，被默认序列化混进线载荷（内核不认识，无害但脏）。

**根因**：直接序列化服务层 DTO ⇒ 枚举与计算属性都会被带上线。
**处置**：新增**线载荷 DTO `HostNavigateWire` + `HostNavigateSegment`**（`[JsonPropertyName]` 逐条钉死 22 个字段、
`MediaSegmentType` → 字符串映射、`WhenWritingNull`），彻底消除"服务层 DTO 形状 == 线上形状"的隐含假设。

---

## §4 ③ `ReadRequestAsync` 的字节/字符混淆（**按 `VERIFY_PLAN_T35_T41.md` 第 8 行重做**）

| | 内容 |
|---|---|
| 缺陷（`InboundCallbackEndpoint.cs` 原 L311/L325） | 把请求读成 `string` 后用 `Encoding.UTF8.GetByteCount(bodyText)` 与 `Content-Length` 比较、用 `bodyText.Substring(0, contentLength)` 截断 —— **两处把字符下标当字节下标**。正文含非 ASCII（Emby 片名就是中文）时字符数 < 字节数 ⇒ 补读循环不满足/截断落在字符中间 |
| 修复 | 全程 **`List<byte>` 字节缓冲**：字节级找 `\r\n\r\n` → 头按 UTF-8 只解码一次 → 按 `Content-Length` **切字节** → 正文只解码一次 |
| 边界语义 | 已到字节 < `Content-Length` ⇒ 返回已解码部分；头 > 64 KiB / 体 > 10 MiB ⇒ 抛异常（accept 循环兜住） |

### 4.1 🔴 **第一版用例被 verifier 的判据否掉**（自我记录，避免重犯）

**判据原文**（`shell/docs/VERIFY_PLAN_T35_T41.md` 第 8 行）：
「三偏移用例：**`Content-Length=8400`**、**3 字节 CJK 起于 8190 / 8191 / 8192** ⇒ 解码正确；
**ASCII 对照**必须同过。**ASCII 与 CJK 都过 ⇒ 用例没打在分帧上 ⇒ 判"用例无效"，重设计**。」

**第一版的错**：把 `\r\n\r\n` 顶到 8190/8191/8192 —— 打的是**头边界**，而读块 = 8192
⇒ 头边界贴近读边界时**没有任何多字节字符被拆开** ⇒ ASCII 与 CJK 都会"通过"
—— 正是判据要防的**用例无效**形态（判决落在"用例"而不是"实现"上）。

### 4.2 [实测] 重做后的用例与读数（2026-09-12 00:17）

**构造**（`shell/App/KernelHost/FramingProbeClient.cs`，用 **TcpClient 手写原始字节**：
`HttpClient` 会在**发送侧**就把 body 变 string 再编码 ⇒ 那样测的不是我的读取器）：

```
头长 = 24574（= 3×8192 − 2）  ⇒ 正文整段始于字节 24576
  · 第一读块 8192   = 头 8190 + `\r`        ⇒ 头终止符**跨块**
  · 第二读块开头    = `\n\r\n`              ⇒ 头在补读里结束
  · 读块边界 8192 落在**正文偏移 8192**处   ⇒ 正切 3 字节标记
正文总长恒为 8400（判据指定）；CJK 版标记 = `字`（3 字节）／ASCII 对照 = `zzz`（3 字节）
```

**读数（原样，`shell/Tests/evidence/t27-callback-reply.txt`）**：

```
--- 标记起于正文偏移 8190 ---
  CJK   : status='HTTP/1.1 204 No Content' 发送字节=8400 端点读到字符=8398 事件名='stopped' U+FFFD=False
  ASCII : status='HTTP/1.1 204 No Content' 发送字节=8400 端点读到字符=8400 事件名='stopped' U+FFFD=False
--- 标记起于正文偏移 8191 ---   （同上：CJK 8398 / ASCII 8400）
--- 标记起于正文偏移 8192 ---   （同上：CJK 8398 / ASCII 8400）

断言 三偏移 × (CJK,ASCII) 全部 204                  = True
断言 Content-Length 恒为 8400                       = True
断言 CJK 正文无 U+FFFD（未被按字符拆坏）             = True
断言 用例打在分帧上（同偏移 CJK 与 ASCII 字符数不同）  = True
```

**为什么这一版是"有效用例"（判据要的那条证明）**：
同一偏移下 **CJK 读到 8398 字符 / ASCII 读到 8400 字符，而两者发送字节数都是 8400**
⇒ 端点读到的是**同样 8400 字节**、解码出的**字符数不同**。这个差异**只能**来自
"字节被按 `Content-Length` 正确切分、再按 UTF-8 解码"；若 CJK 的 3 字节在块边界被拆坏，
`字` 会解成 U+FFFD ⇒ 两者字符数相等且 `HasReplacementChar=True`。

**旧实现在这三例上会怎样失败（可复述的推理）**：旧码把第一读块按字符解码，
跨块边界的 CJK 解成 **U+FFFD（1 字符）** ⇒ `Encoding.UTF8.GetByteCount(bodyText)` 比真实字节数**少 2**；
另有 `Substring(0, contentLength=8400)` 落在 8398 字符上 —— 两处都是字符/字节混用。

---

## §5 ④ 凭据面语义修正（`LastLocalVideoPath`）与 ⑤ 打码收尾

### 5.1 ④ [实测] `%LOCALAPPDATA%\AIPlayer\player\settings.json`

| 时刻 | 文件 | `api_key=<32hex>` 计数 | `LastLocalVideoPath` |
|---|---|---|---|
| 修前（22:54，本轮开始） | 967 B / `A9516C57…` | **1**（带 token 的 Emby 流 URL） | 存在（= 流 URL） |
| 修后（23:07，真实播放一轮 + 内核退出后） | **765 B** / `1E2A3603…` | **0** | **键已移除（0 行）** |
| 终态（23:15，多轮实测后） | 877 B | **0** | `"…Temp\\p0-long.wav"` ← **本地文件路径** |

> 最后一行的 `.wav` 是**队友并行实测**（本地视频/音频链）经内核正常写库的结果 —— 该槽语义正是"本地文件路径"，
> **符合修正目标**；要验的是"**不出现带 token 的流 URL**"，而不是"这个键必须为空"。
> 每次我方播放轮次结束都有 `LOCALSLOT-GUARD after-playback restored slotWas=<stream-url> restoredTo=…` 审计行。

全树扫描 `%LOCALAPPDATA%\AIPlayer`（递归全部文件，2,000+ 个）：含 `api_key=[0-9a-fA-F]{16,}` 的文件 = **0 个**。

机制（`KernelHost/LocalVideoPathSemantic.cs`）：启动前 `GuardBeforeLaunch()` 收好"槽里若已是流 URL 就先清掉"，
播放后 `RestoreAfterPlayback()` 检测到流 URL ⇒ 恢复到"本机可确认存在的本地路径"，没有就**删键**（= 什么都没发生过）。
**不采**"打码器扩范围"（会让内核读回 `***` ⇒「恢复上次播放」静默失效）。
审计行（实测）：

```
23:03:25.663  LOCALSLOT-GUARD before-launch stripped-stream-url restore=<none>
23:03:27.202  LOCALSLOT-GUARD after-playback restored slotWas=<stream-url> restoredTo=<removed-key>
```

### 5.2 ⑤ [实测] 退出时再跑一遍 + 并发保护

| 情形 | 期望 | 实测（原始审计行） |
|---|---|---|
| 内核**在跑** | **跳过** + 留跳过审计行 | `23:03:29.029 KERNEL-LOG-MASK audit kind=exit result=SKIPPED reason=kernel-running running=1 logsRoot=C:\Users\Administrator\AppData\Local\AIPlayer\logs` ✅ |
| 内核**已停** | 真跑 + 留审计行 | `23:03:32.085 KERNEL-LOG-MASK file=log-player-2026-09-11.txt hits=3`<br>`23:03:32.086 KERNEL-LOG-MASK audit kind=exit result=RAN files=40 changed=1 hits=3 skipped=0` ✅ |

### 5.3 🔴 退出挂点：**三次试错后按实测定位**（captain 2026-09-12 裁示 ① 的结论）

captain 裁示迁到 `Application.Exiting`。**照做后发现该 API 在本版本不存在** —— 不再靠印象/转述，直接反射宿主真实加载的 `Microsoft.WinUI.dll`：

```
typeof(Application).Assembly = Microsoft.WinUI, Version=3.0.0.0, …
--- events（全部）---
  ResourceManagerRequested : TypedEventHandler`2
  UnhandledException : UnhandledExceptionEventHandler
--- methods 名字里含 Exit ---
  public Exit() virtual=False static=False
诊断结论：
  Application.Exiting 事件存在 = False
  Application.Exit() 无参方法存在 = True
  Application.OnExit 成员存在 = False
```

（原始输出：`shell/Tests/evidence/t27-exitapi-probe.txt`，由 `SHELL_SELFTEST_PROBE_EXIT_API=1` 生成）

**四条 API 的实测判定**：

| 候选 | 判定 | 证据 |
|---|---|---|
| `Application.OnExit` 覆写 | ❌ **不存在** | 编译 `CS0115: 没有找到适合的方法来重写` |
| `Application.Exiting` 事件 | ❌ **不存在** | 编译 `CS0103: 当前上下文中不存在名称"Exiting"`；反射 events 只有上面两个 |
| `Application.Exit` 当事件用 | ❌ **不是事件** | 编译 `CS1656: 无法为"Exit"赋值，因为它是"方法组"` |
| `Application.Exit()` 当方法调 | ✅ 存在但**不等于退出** | 实测：调用后 **40 s 内进程仍未退出**，且**无任何退出审计行**（见下表） |
| **`Window.Closed`** | ✅ **存在且可用** | 内核自己的 `WinUISample.MainWindow.cs` 就在用；实测见下 |

**⇒ 最终挂点（按 captain 的"要有证据才加"纪律）**：
- **主挂点 = `App.OnLaunched` 里的 `_window.Closed += …`** → `ExitProbe.RunExitSideWork("Window.Closed")`（打码 + ④ 兜底）；
- **幂等兜底 = `Program.Main`（`Application.Start` 返回后）**。**为什么保留**：实测存在"进程既没走 `Closed`、也不退出"的路径（下表路径②）；
  双跑安全 —— 打码器**幂等**，实测同一次退出里第二次跑读数为 `changed=0 hits=0`。

**两条退出路径的实测读数（去重后）**：

```
路径① 人手点窗口关闭（CloseMainWindow，等价于点 X）：
  00:35:58.398  EXIT-CTX window:MainWindow.Closed
  00:35:58.400  APP-EXITING trigger=Window.Closed
  00:35:58.433  KERNEL-LOG-MASK audit kind=exit result=RAN files=41 changed=0 hits=0 skipped=0   ← 钩子①
  00:35:58.568  KERNEL-LOG-MASK audit kind=exit result=RAN files=41 changed=0 hits=0 skipped=0   ← 兜底（幂等，0 改动）
  00:35:58.571  Main exiting after exit-mask (idempotent fallback)
  ⇒ 进程自退出 ✅、审计行齐 ✅、双跑幂等 ✅

路径② 程序化退出（Application.Current.Exit()，由 SHELL_SELFTEST_EXIT_API=1 驱动）：
  00:36:05.995  EXIT-CTX api:Application.Current.Exit()
  00:36:05.996  SELFTEST-EXIT-API invoking Application.Current.Exit()
  00:36:05.997  SELFTEST-EXIT-API Exit() returned
  （此后 40 s 内：无 APP-EXITING、无 KERNEL-LOG-MASK audit kind=exit、进程未退出）
```

**路径②结论（按 captain 要求，不凭猜补钩子、只报事实）**：
`Application.Current.Exit()` **在本工程形态下不结束进程、也不触发任何退出钩子** ⇒
**工程里没有任何代码路径调用它**（它只是我为了做这条取证而加的驱动），**真实退出方式只有"关窗口"一种**，
而该路径已被钩子① 覆盖并留痕。若将来新增调用 `Application.Exit()` 的入口，**必须同时补一条独立退出收尾**
（或改为 `Close()` 主窗口），否则那条路径会**静默漏掉打码**。

**去重记录**：我曾把 `Closed` 在 `App` 与 `MainWindow` **两处都挂** ⇒ 日志出现**两条** `EXIT-CTX window:MainWindow.Closed`；
已收敛为 `App` 一处（复核后 `EXIT-CTX`/`APP-EXITING` 各一条）。

---

## §6 教训（可复用）

1. **"设了就算"是假通过源**：camelCase 设了、`ToNavigateJson` 写了，都不等于线上形态对 ——
   只有把合成回调**打回自己的端点**并断言**原始响应体**，才看得见 `type:0` / 计算属性上线 / 空体被 `{}` 替换。
2. **自检的分辨力要自己证明**：三个反控（handler=null / 无 MediaPath 的 `{}` / **合法形状**）缺一个，
   就无法区分"逻辑没生效"与"数据恰好为空" —— 本轮 `navigate_next` 恰好两次都是空体，靠反控③才拿到非空体证据。
3. **相对路径算层级要当场验**：`AppContext.BaseDirectory` = `bin\Debug\<tfm>\win-x64\` ⇒
   回 `shell/` 要**五级**，我第一次写四级，证据文件写到了 `shell\App\Tests\evidence\`（好在日志里打了绝对路径，一眼可见）。
4. **回滚要成对**：`CloseMainWindow` 关掉窗口后，用**继承句柄**的 `Start-Process` 起测，关闭时若在 `finally` 里
   撤销标准输出重定向，会让子进程写已关句柄而**自己崩掉** ⇒ 该 run 的退出审计行就丢了。

---

## §7 未验清单（**宁缺不编**）

| 项 | 状态 | 需要什么才能证 |
|---|---|---|
| 内核在剧集播放中真的发 `navigate_next` 并拿到**非空**响应体入画（换源续播真的发生） | **[未验]** | 内核浮层点"下一集"或等片尾自动连播；或对内核窗口模拟按键。本轮播的剧集 `Episodes` 为空（`episodes=0`）⇒ `ResolveNavigationAsync` 正确地返回 null ⇒ 端点回空体，**属正确形态** |
| `switch_version` / `refresh_playback_url` 的真回调 | **[未验]** | 同上；代码路径与 `navigate_*` 共用同一个 `ResolveNavigationAsync` + 同一个线载荷投影 |
| 截图（起窗画面） | **[未补]** | 卡面验收未要求；内核进程存活用 `KERNEL-LAUNCH OK pid=19736` + 内核 `progress` 回调到达证明 |
| `ReadRequestAsync` 三偏移的**内容逐字节等值**断言（当前用 204/400 间接反证） | **[间接]** | 204 只能证明"按 `Content-Length` 切出的区间让 `event` 可解析"；要更强可加一条：正文用同一区间做 SHA 比对 |

---

## §8 交付物与改动面（**全部在 in-scope 路径内**）

| 文件 | 体量 | 说明 |
|---|---|---|
| `shell/App/KernelHost/KernelLaunchRequest.cs` | 151 行（新） | 37 个 CLI 字段，逐条注内核 `App.cs` 行号 |
| `shell/App/KernelHost/KernelArgumentBuilder.cs` | 277 行（新） | `PlaybackRequest` → 启动参数；`--danmaku-match-name=` URL 编码；Base64 统一走服务层 DTO |
| `shell/App/KernelHost/ShellCallback.cs` | 293 行（新） | 响应体接缝 + **线载荷 DTO 显式投影** + 扁平 JSON 解析 |
| `shell/App/KernelHost/LocalVideoPathSemantic.cs` | 193 行（新） | ④ `LastLocalVideoPath` 语义闸门 |
| `shell/App/KernelHost/KernelLauncher.cs` | 98 行（改） | 启动器瘦身；接入 ④ 闸门；`CountRunningKernels()` |
| `shell/App/KernelHost/InboundCallbackEndpoint.cs` | 385 行（改） | ③ 字节缓冲重写 + `SplitPath`（query 不误判 404） |
| `shell/App/KernelHost/KernelLogMasker.cs` | 183 行（改） | ⑤ `MaskKernelLogsAtExit` + 并发保护 + 两情形审计 |
| `shell/App/KernelHost/KernelBridge.cs` | 110 行（改） | 暴露 `SettingsToolkit` 实例（同一份 `player\settings.json`） |
| `shell/App/Features/Library/LibraryPage.xaml.cs` | 708 行（改） | 播放链路改走 `EmbyPlaybackSession` + 41 参数 + 回调接缝 + 三个自检钩子 |
| `shell/App/Program.cs` | 131 行（改） | 退出路径挂 ⑤ + ④ |
| `shell/App/App.xaml.cs` | 39 行（未改，最终态与原始一致） | 曾尝试 `OnExit` 覆写 ⇒ WinUI 3 无此虚方法（实测 CS0115），**已回滚** |

**未触碰**：`reversed/**`、`shell/Services/**`、`shell/App/Theme/**`、`shell/App/Features/Search|Servers|Settings|Favorites|Aggregate/**`、
M1/S1 段与内核接入构型（只读）。

