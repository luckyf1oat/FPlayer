# 探针 B 协议（`ui3`，`t32` 内；captain 结论口径已钉）

> 目的：裁决"崩溃代际"里那处 `{StaticResource ServerIconSource}`（含 `.cs` 的 `Resources[...]` 写入）**是否单独即可致崩**。
> 本文件 = **执行清单**；跑完的原始读数落 `evidence/t30-probeB-icon-source.txt`，本文件只负责"怎么跑、怎么读、怎么收尾"。

## 0. 三态与结论口径（captain 2026-09-12 钉死，逐条照写）

| 态 | 本探针能写到什么 |
|---|---|
| 逆向到了 | 崩溃代际的 dll 是**唯一记录**（判据**必须限定到页面源文件**：`git log -S 'ServerIconSource' -- 'shell/App/Features/Servers/ServersPage.xaml' 'shell/App/Features/Servers/ServersPage.xaml.cs'` = **0 笔**；`91e4c28`/`a45218a`/`176fa9d` 三笔的 `ServersPage.xaml.cs` 命中各 = **0**；当刻工作区 `.xaml`/`.cs` = **0**）⇒ **不可从 git 回放** |
| 重建实现了 | 本探针的两臂是**重建体，不是回放体**（按记录重写那两处） |
| 运行验证过了 | 见 §3 的读数；**结论措辞按 captain 裁定**：<br>· 臂 1 **崩** ⇒ 该形态是"**本次重建下的充分条件**"；<br>· 臂 1 **不崩** ⇒ **仅**排除"该形态单独即可致崩"这一**强假设**；<br>· 🔴 **不得**写成"复现"或"证伪"。 |

**🔴 上表第 1 行的自指说明（`verifier` 2026-09-12 06:5x 复跑发现，写入前为 0 实测；**本句落盘后该串自带命中，属预期**）**

- **不限定路径时**：`git log -S 'ServerIconSource' -- shell/App/Features/Servers/` = **3 笔**（`4fe99ff` / `0787d89` = 本探针证据文件，`5285b0e` = **本文件**）；`git grep -n 'ServerIconSource' HEAD -- shell/App/Features/Servers/` = **10 处命中，全部在这两份文档里**（本文件 `:3/:10/:33/:34` + 证据文件 `:1/:7/:19/:21/:46/:80`）。
- ⇒ **成因 = 自指**：这两份文档自己写着 `ServerIconSource`，一落盘该串就进了 `Features/Servers/`，于是"全仓 0 命中"这句立刻不再成立（与 2026-09-12 04:49/04:50 修过的同族缺陷同型）。
- ⇒ **口径**：该主张的正确写法是"**写入前** `git log -S` 为 0；本文件落盘后该串自带命中，属预期"，且命令**一律限定到页面源文件**（上表第 1 行那条）。**"崩溃代际的页面源码从未入库"这个结论在限定后成立**（0 笔 + 三笔老提交的页面文件 0 命中 + 当刻工作区 0）。
- ⚠️ 同一句**旧措辞**也出现在 `evidence/t30-probeB-icon-source.txt` 的三态块（`:7`）。该文件的身份被 `shell/docs/WORKSPACE.md` 事实 291/296 以 **`E5211758243B` / 7,862 B / 88 行**钉住 ⇒ **本次不改该文件**（改了会让 captain 账本的身份行过期），**口径以本行为准**；若 captain 要连它一起改，需同一提交里同步刷新他那三处身份行。

## 1. 开工前置（缺一不跑）

0. **隔离输出的自锁检查（`ui2` 实测补）**：隔离目录里的 dll 会被**自己遗留的实例**锁住 ⇒ 重建前先
   `Get-Process AIPlayer.Shell | ? { $_.Path -like 'E:\ui3-probe-bin\*' }`，**只停自己那一支**（路径匹配我的隔离目录），再构建；
   报错形态是 `MSB3027 … 文件被 AIPlayer.Shell (<pid>) 锁定`。这与"绝不结束别人的进程"是同一条纪律的两面。
0b. **产物必须比源码新（假阴性第一来源）**：构建后先比 `dll mtime` 与 `ServersPage.xaml/.cs mtime`；**源码更新 ⇒ 二进制里没有这次改动**，任何行为反控都会"莫名其妙失败"。
0c. **两条扫针纪律**：① ASCII 扫不到 .NET 用户字符串（**UTF-16 堆**）⇒ 必须 even+odd 双向 UTF-16 一起扫，**否定结论须多扫一起报**；② 命中/未命中与"偏移相同"是两层，引用偏移必须带那一代的 `dll sha12`。
0d. **证据落地**：本次探针的证据我按仓库内路径写（`Features/Servers/evidence/t30-probeB-icon-source.txt`）；若后续要跑"控制态"且不想刷新库内被引用证据的身份，用 `ui2` 加的 `SHELL_EVIDENCE_DIR` 把它改道到 `%TEMP%`（库内一字节不动）。

1. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\shell\tools\list-instances.ps1 -Samples 10 -IntervalMs 3000`
   ⇒ 贴 `allFree` / `spanSec`；**他人正在构建（`dotnet` 进程存在）则等静默**再编。  ← **[2026-09-12 裁定「改」]** `spanSec` 只作**窗口声明**（引用须并列 `Samples`/`IntervalMs`）；空场判据见文末 **§10-D**（`allFree`+`maxShells`+`distinctPids`）。
   ⚠️ 口径（captain 统一）：**单次读数不算"场地空"** —— 必须引 `-Samples N` 的 `allFree=true` 且**同时给 `spanSec`**；`ownerDir` 只是线索；退出码恒 0，不作判据。  ← **[2026-09-12 裁定：本条"必须同时给 `spanSec`"降级]** `spanSec ≈ Samples × IntervalMs` 结构上恒被满足 ⇒ 对"场地空"**零信息量**；判据换成 §10-D 的那几格，`spanSec` 仅答"这个结论覆盖了多长的窗口"。
2. 记 `HEAD`、`git status --porcelain -- shell/App/Features/Servers/`（须干净）。
3. **注入面基线（verifier 要求）**：`shell\App\Assets\Icons\emby.png` 的 `bytes + sha256_12 + 前 4 字节`
   ⇒ 实测 `2,094 B / 3B716E0B523B / 89 50 4E 47`（真 PNG 魔数）。臂 1 不动该文件；**臂 2 才替换**，替换前必须另存原件到 `%TEMP%`。

## 2. 两臂定义

**臂 1（必做）** —— 重建那两处：
- `ServersPage.xaml`：行模板里 `Image` 的 `Source="{StaticResource ServerIconSource}"`（并保留既有事件接线）；
- `ServersPage.xaml.cs`：`Resources["ServerIconSource"] = <BitmapImage(Assets/Icons/emby.png)>` 的写入（与 xaml 那条引用成对）。
  🔴 **上面那行是"转述形"，照它写会编译不过**（`verifier` 2026-09-12 实测：裸 `BitmapImage` 报 **`CS0246`**）。**可编译的精确形**（他 EXIT=0 的那一版）：
  ```csharp
  Resources["ServerIconSource"] = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
      new System.Uri("ms-appx:///Assets/Icons/emby.png"));
  ```
  ⇒ 复现者请注意：**类型全限定**（`Microsoft.UI.Xaml.Media.Imaging.BitmapImage` + `System.Uri`）才与探针当时的编译面等价；引用形（xaml 那行）不受影响。

**臂 2 —— ✅ **已做**（captain 追加批准"两臂都做"；读数见证据文件 §7，2026-09-12 05:45，同一代际）** —— 把 `Assets/Icons/emby.png` 临时替换为**真不可解码**字节（同路径、同扩展名），再跑一次：
⇒ 用来区分"崩因是**资源键机制**"还是"崩因是**图片解码**"。读数：干净源码 + 损坏图标（`ORIG 2,094 B/3B716E0B523B/89 50 4E 47` → `CORRUPT 64 B/F5A5FD42D16A/00 00 00 00`，dll `3733D183EDAE`）两臂 `ALIVE-after-22s`。
🔴 **口径收紧（`verifier` 2026-09-12 提出，captain 钉的措辞需相应收窄）**：**"图片解码不是崩因"这一否证面尚未闭合，暂不得这样写**。理由 = **配对不严**：臂 1（注入源码 + 有效 PNG ⇒ 崩）与臂 2（干净源码 + 坏字节 ⇒ 存活）**差两个变量**（源码形态、资产可解码性）⇒ 臂 2 只证明"**正常 `Loaded` 赋值路径对坏字节是稳的**"，**不能**否证"崩与图片解码有关" —— **注入形态下遇到坏字节会怎样，还没测**。**现在的合法措辞 = "在干净源码路径下，坏字节不崩"**；只有**臂 3** 才能把资产变量单独摘出来（见下）。证据文件 §7 里那句"图片解码不是崩因"属**收紧之前**的措辞，按本条读。🔴 **本条已被 §3.2.1 取代**：腿 B（注入 + 坏字节）已跑 ⇒ **仍崩 `-1073741189` @1.329 s**（同一源码代 + 同一重建产物）⇒ **措辞升回"图片解码不是崩因"**（限定语照旧 = 该源码代 + 该重建产物；前置 = 形态在场 + ≥1 行被实体化）。
🔴 **旧的"可选/未做"措辞作废**：曾有一条消息按"未获批准、未做"报臂 2（并把它记成 `5,486 B / B49B97CA4F85` 的旧代证据），**那条是旧稿**；以证据文件 §7 的 88 行版为准（`7,862 B / E5211758243B`，mtime 05:46:27.717）。

**臂 3（曾记作"剩余的一条腿"）** —— ✅ **已跑 = `verifier` 的腿 B（§3.2.1，2026-09-12 07:31–07:32）⇒ "还缺这一格"的措辞作废**；定义保留如下（= **臂 1 的两处注入 + 臂 2 的坏字节**，2×2 矩阵的最后一格）：
⇒ 用来判定"坏字节在场时，注入形态是否仍致崩" —— 仍崩 ⇒ 与"形态即充分条件"一致、与图片字节无关；不崩 ⇒ 说明臂 1 那次崩另有依赖。**产物没有"预期 sha"**（这一格从没构建过），只要求**同一代际语义 + 起跑当刻现钉**。
🔴 **代际 pin（reviewer 2026-09-12 指出，必须遵守）**：本探针的配方 pin 的两个源码身份 = `ServersPage.xaml 5,100 B / DE652FDA9CAA`、`ServersPage.xaml.cs 24,220 B / 34CCA087243C`（= 探针当时的 HEAD `9b886b9`/`590a688` 的 blob）。**工作区早已不是这两个身份**（`t64` 把它们改成了 `6,116 B / 8E358B09F1FD` 与 `25,265 B / 45FBFC1FEB5B`，提交 `12f19be`）⇒ **直接拿今天的工作区重编 = 新代际，与本探针的读数不可互证**。忠实复跑必须用 `git show 9b886b9:<path>`（或 `590a688`）取回那两个 blob，在**临时树**里构建，**不碰工作区**。

### 2.1 字节级同支的**可复算**注入原文（`ui3` 2026-09-12 立；08:3x 按 `verifier` 复算差异**更正落点语义**）

> 为什么有这一节：`verifier` 指出"你只记了注入**后**的哈希、没记插入原文 ⇒ 无法从旧 blob 复算"（= §3.2 混杂项 ② / §3.2.1 的 ③）。
> 本节把**插入原文与落点写死**，并把他的配方**跑成可核身份**：复现者照抄即可得到与他逐字节相同的两个源 —— **构建之前**就能判"是不是同支"。

**落点（只在历史 blob 上做，不碰工作区）**：`cmd /c "git cat-file blob 9b886b9:<path> > <tmp>"` 取回下面两个 blob（重定向保原字节）：

| 源 | `9b886b9` 的 blob 身份 | 行尾 |
|---|---|---|
| `shell/App/Features/Servers/ServersPage.xaml` | 5,100 B / `DE652FDA9CAA` | LF（`CR=0`） |
| `shell/App/Features/Servers/ServersPage.xaml.cs` | 24,220 B / `34CCA087243C` | LF（`CR=0`） |

**插入 1（xaml）**：**行级**插入 —— 在**下面这一整行之前**新起一行（**不是**"在某个子串之前"，理由见下方 🔴 根因）。锚行逐字（**前导 35 个空格**，整行 62 字符）：

```xml
                                   Loaded="OnRowIconLoaded" />
```

新增的那一行（前导 8 空格 + 正文 42 字符 = **整行 50 字符**）：

```xml
        Source="{StaticResource ServerIconSource}"
```

**插入 2（cs）**：插在 `InitializeComponent();` **之后一行**。逐字（前导 8 空格 + 正文 132 字符 = **整行 140 字符**）：

```csharp
        Resources["ServerIconSource"] = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new System.Uri("ms-appx:///Assets/Icons/emby.png"));
```

> 🔴 类型**必须全限定**（`Microsoft.UI.Xaml.Media.Imaging.BitmapImage` + `System.Uri`）：裸 `BitmapImage` 报 `CS0246`（§2 臂 1 那个框就是他首版照"转述形"写栽的坑）。

**复算读数（2026-09-12 08:3x **更正版**；触发 = `verifier` 08:1x–08:2x 报"按 §2.1 复算得 `4F11560FA868`，不是 `5378C81D5D53`"）**：

| 源 | 基底（`9b886b9` blob） | **注入后（canonical）** | 行数 | 注入后 git-blob |
|---|---|---|---|---|
| `ServersPage.xaml` | 5,100 B / `DE652FDA9CAA` | **5,151 B / `4F11560FA868`** | 83 | `dafb5058f220` |
| `ServersPage.xaml.cs` | 24,220 B / `34CCA087243C` | **24,361 B / `7A6B2944E310`** | 491 | `a0c6b98979a7` |

差分：5,100 + 50 + 1 = 5,151 ｜ 24,220 + 140 + 1 = 24,361（**整行字符数** + LF；两处各只加一行）。

**字节级 fixture（免解释复钉）**：`evidence/probeB-injected-ServersPage.xaml.txt`（5,151 B / `4F11560FA868`）、`evidence/probeB-injected-ServersPage.xaml.cs.txt`（24,361 B / `7A6B2944E310`）—— 内容与上表**逐字节相同**，仅扩展名取 `.txt`：`.cs` 会被 SDK 隐式 Compile 通配吃进去（与 `ServersPage` 分部类冲突），`.txt` 同时避开 H7/H3 的 `.cs`/`.xaml` 扫描面，且 `csproj:365-367` 把 `**\evidence\**` 挡在产物外。

🔴 **一条真实差异 + 根因（我错、他纠正；留痕不抹）**：本节初版记的是 `5378C81D5D53` 并写"与他 §1 记录逐字节相同" —— **那是两边用同一种错误写法得到的同一个伪不动点**，不是配方的真值。根因：
- xaml 的锚**不在行首**：锚行 = **35 个前导空格** + `Loaded="OnRowIconLoaded" />`（整行 62 字符）；而插入指令里的锚串是 `        Loaded="OnRowIconLoaded"`（8 空格）⇒ 它在**行内第 28 个字符**处命中 ⇒ **`String.Replace`（子串替换）把这一行从中间劈开**，产物变成"27 空格 / 换行 / 新行 / 换行 / 余下部分"。
- 两种语义产物**字节数相同（都 5,151）、行数也相同（都 83）**，只有内容不同：
  · `4F11560FA868` = **行级插入**（在整行之前新起一行）= 文档字面"之前一行"的**真值**；
  · `5378C81D5D53` = **子串替换**（劈开锚行）= 我初版（以及他 §1）的实际做法。
  ⇒ **长度 / LF 计数 / 行数这类尺寸判据分辨不了这两者，只有 sha256_12 / git-blob 能分辨**（"身份判据不得退化成尺寸判据"的又一例）。
- cs 那边**两种语义重合**（锚行 `        InitializeComponent();`，锚串紧接行首内容）⇒ `7A6B2944E310` 两种做法都得到 —— 这正是初版误判"方法没问题、两边都对得上"的原因：**一边重合、一边不重合，只看 cs 会得出错误结论**。
- ⇒ **canonical = 行级插入的 `4F11560FA868`**；**本卡 2×2 结论不受影响**（他四格用的就是他这一代）。

- ⇒ **canonical 注入支 = 本节的这一支**（可复算、可先验后编）。
- ⇒ 我先前那支（xaml 5,178 / `951CC886A2CA`、cs 24,680 / `836F82920539`）**没有插入原文入账 ⇒ 不可复算**［🔴 **xaml 半边已被 §9 更正**：原支**可复算** —— 插入正文就是那 42 字符，我只是把**缩进**当成了常量；改 35 空格即得 `5,178 B / sha256_12 951CC886A2CA`（§9 有我独立重跑的 8 行缩进表）。**cs 半边仍成立**（原文 0 命中对象库与磁盘 ⇒ 永久"仅历史引用"，见 §9）］，只能作为"曾观察到的**在途注入态**"引用，**不得**当作配方使用。
  - **现状（2026-09-12 08:23 我实测 + `services` 08:2x 独立复核，两读一致）**：工作区两文件**已回到 `12f19be` 代** —— `ServersPage.xaml` 6,116 B / `8E358B09F1FD`、`ServersPage.xaml.cs` 25,265 B / `45FBFC1FEB5B`；**两形态 blob 逐字相等**（`8c06d1f29725` / `372e1da2b22b`）、`git status --porcelain` **空**。
  - ⇒ 上面那句"在途注入态"指的是 **07:2x–07:3x 的历史观测**，**不是当刻文件态**；连带"有人裸 `git commit -a` 会把该形态带进提交"的风险当刻**不存在**（本队纪律本就是**只显式 pathspec 提交**，无裸 commit）。
- ⇒ 术语（同 `de3033a` 的提交说明）：他跑的是"**当刻树 + 探针时代注入形态**"——把这两个文件临时还原成历史 blob、构建、跑完再 `git checkout --` 还原（收尾两文件 porcelain 空、`worktree == HEAD`）；**不是**"整树同代"，因为 `9b886b9` 那代含大量**未跟踪**依赖，临时树根本编不出来。

**跑法（两臂相同）**：
```
dotnet build shell\App\AIPlayer.Shell.csproj -c Debug -p:OutputPath=E:\ui3-probe-bin\
set SHELL_START_PAGE=servers   → 起壳、冒烟 ≥20 s（解码异步 + 行实体化在首屏之后）
set SHELL_START_PAGE=home      → 第二次起壳作**落点对照**（同支产物、不同落点）
```
- **窗口 ≥20 s**（不是 9 s）；**脚本不驱动滚动** ⇒ 证据里如实标"未滚动"（滚不滚得动取决于工装，且不驱动用户输入是本组纪律）。
- 读数：每次起壳的 `pid` / `path` / 起止时刻 / `HasExited` / `ExitCode`（非 0 时附事件日志里的异常码，如 `0xC000027B`）+ 产物 `dll sha12` + `mtime` + `HEAD` + 时刻 + 是否 `-t:Rebuild`。

## 3. 判据（每条都要能 False）

| # | 判据 | 反例形态 |
|---|---|---|
| ① | **注入面命中了**：该资源键在两处都在位（xaml 引用 + cs 写入），且 `SHELL_START_PAGE=servers` 那次**服务器列表被实体化**（日志/截图可见行） | 只在 dll 里存在字符串、页面没走到 ⇒ `alive=True` 可能只是"路径没命中"（verifier 原话） |
| ② | **臂 1 崩**：进程非 0 退出（`HasExited=True` 且 `ExitCode != 0`）或 `home` 对照活而 `servers` 死 | 两次都活 ⇒ 只能写"仅排除强假设"；两次都死 ⇒ 是环境问题，不是该形态 |
| ③ | **臂 2（若做）**：替换后 `emby.png` 前 4 字节 **≠ `89 50 4E 47`**（自证"确实不可解码"） | 忘替换 / 替换了没生效 ⇒ 读数无意义 |

### 3.1 臂 1 的"注入面被消费"正控（2026-09-12 06:4x 从共享日志复核，**不需要任何输入**）

臂 1（注入体 `A72CD762B3A0`，pid 860，起 `05:34:39.344`）当刻的原始行（取自共享外壳日志目录 `%LOCALAPPDATA%\AIPlayer\logs\` 的当日运行日志，该日志不入库；只贴该 pid 的时间窗）：

```
05:34:40.1443718  SERVERS-SHAPE bare-array-fallback count=1 path=…\servers.json
05:34:40.4204422  RAIL loaded 16 servers; originalAccounts=15; legacy=0
05:34:40.4803287  Nav -> servers
05:34:40.5189365  NAV-RAIL-SYNC tag=servers selected=<none>
05:34:40.7565449  ServersPage loaded: 16 servers; enabled=16; originalAccounts=15; legacy=0; readOnly=15; file=…\servers.json
05:34:41.5693308  Main entered; args=0        ← 下一条即 home 对照臂（pid 20912）起 ⇒ pid 860 已在 ~05:34:41.43 退出
```

- **判据 ① 的正控成立**：`ServersPage loaded: …` 是页面自己写的（`ServersPage.xaml.cs:226`，紧跟 `:213 ServerList.ItemsSource = servers;`）⇒ 当刻**页面走到了装载完成、且 16 台服务器进了列表**；`Nav -> servers` 证明落点正确。**全程零输入、零滚动**（本探针从不驱动输入 —— 与 `verifier` 口径一致：不为了逼实体化去发滚动/点击）。
- **机制层归因（标注为推断，不是证明）**：那条日志在 `ItemsSource` 赋值之后立刻写，而 `DataTemplate` 的实例化（以及模板内 `{StaticResource …}` 的查表）发生在**其后的布局/实体化过程**；崩溃时刻 ≈ 起后 2.09 s = `05:34:41.43`，比那条日志**晚 0.68 s** ⇒ 与"崩在行模板实体化阶段"一致。
- **限制（如实写）**：本跑**没有**"具体哪一行被实体化"的直接读数（`ListView` 容器实体化没有自己的日志行），且 14 台在首屏视野之外 ⇒ 按 `verifier` 口径，**这一点不用输入去逼**，只作为**限制**列出：本臂能证明"页面装载 + 列表有 16 条 + 注入面在位"，**不能**证到"某一行模板实例化成功后才崩"。臂 2 与臂 1 走**同一通道、同样不发输入**，两跑可比。

### 3.2 独立复跑（`verifier` 2026-09-12，6 腿）+ 🔴 一条前置条件（空数据根 ⇒ 不崩）

**结论级复现成立**（他的证据 `shell/Tests/evidence/probeB-independent-repro.txt` = 8,477 B / `66AE30AEA791` / 82 行，提交 `49b9cd6`；门禁 10/10 PASS `EXIT=0` @07:29:21）：
```
ARM A  |injected EDCB26A17408|servers|pid=18652|verdict=DIED exitCode=-1073741189|afterSec=1.216
ARM A' |injected EDCB26A17408|servers|pid=3628 |verdict=DIED exitCode=-1073741189|afterSec=1.229   ← 独立第二次观测
ARM B  |injected EDCB26A17408|home   |pid=6076 |verdict=ALIVE-after-22s
ARM C  |clean    0038ADDBC7F8|servers|pid=13480|verdict=ALIVE-after-22s
ARM D  |clean    0038ADDBC7F8|home   |pid=19836|verdict=ALIVE-after-22s
ARM A''|injected（同产物同落点，唯一差别 = 数据根里**有没有服务器**）⇒ ALIVE
⇒ 按 captain 钉死措辞：**"重建体下崩溃复现 ⇒ 该形态是本次重建下的充分条件"** 在他这边成立；
  我那次被单实例门污染的 `home` 腿，他 ≥4 s 间隔重跑为 ALIVE-22s ⇒ 有效对照补上了。
```
**🔴 前置条件（比"充分条件"更精确的一条，必须一起读）**：他第一轮把数据根隔离成**空沙箱** ⇒ **两臂全 ALIVE**（差点报"复现不出来"）；日志给出原因 = `ServersPage loaded: 0 servers`。
⇒ **注入写在行模板里，0 台服务器 ⇒ 模板不实例化 ⇒ 注入的 XAML 侧根本不在路径上**。把真实 `servers.json`（16 台）播种进沙箱后，**同一支产物 1.216 s 崩**。
⇒ **通用教训（今后任何"隔离数据根"的取证都适用）：隔离根必须把"被测路径要消费的输入"一起播种**；否则得到的"存活"是**假阴性**（这正是 `t75` 那条"SEED 之后再构包"的同族纪律）。
⇒ 因此本文档 §0 的措辞应读作：**该形态在场**（且**行模板被实际实例化**）时，本次重建体崩。

**两条混杂项（他写清了，我不掩盖）**：① 他跑的树是"**当刻树 + 探针时代注入形态**"（`t110` 换源根、`t64` 改过 `ServersPage`）⇒ 两臂**被加载的 Services 也不是同一支**（注入 `46DD2F45D233` vs 干净 `63EBCDD343E7`）；② 他的注入**不是我的字节**：我只记了**注入后哈希**（xaml 5,178 / `951CC886A2CA`、cs 24,680 / `836F82920539`），没记那两行原文［🔴 **xaml 半边已被 §9 更正**：那一行**可复算**（35 空格 + 正文 42 字符 ⇒ `5,178 / 951CC886A2CA`）；**cs 半边仍不可复算**（≈458 字符原文不可恢复）］⇒ 他按 §2 描述重建得 xaml 5,151 / `5378C81D5D53`、cs 24,361 / `7A6B2944E310`（= 我在工作区观察到的**在途注入态**同一对象）。⇒ 这是**结论级复现，不是产物级**；`A72CD762B3A0` 无法重建（与同批证据文件 `EVIDENCE_T30.md §15.13(a)` 同族的"身份不可复核"声明）。
**`emby.png` 他未动**（本轮只跑 captain 裁的四臂）⇒ **臂 2（坏字节）的结论仍归本文件 §7 那份证据**。

**3.2.1 臂 3 已跑（`verifier` 2026-09-12 07:31–07:32）⇒ 否证面闭合，措辞按 captain 口径升回（限定代际 + 产物）**

我**从盘上他的证据文件逐行核过**（`shell/Tests/evidence/probeB-independent-repro.txt` = **13,373 B / `A221B1CD4A69` / 131 行 / mtime 07:33:32.388**）：
```
ARM|page=servers|pid=13644|start=07:31:42.560|verdict=DIED|exitCode=-1073741189|afterSec=1.277     ← 注入 + 坏字节（第 1 次）
ARM|page=servers|pid=9592 |start=07:31:54.262|verdict=DIED|exitCode=-1073741189|afterSec=1.329     ← 注入 + 坏字节（第 2 次）
ARM|page=servers|pid=18616|start=07:32:06.355|verdict=ALIVE-after-22s    ｜ ARM|page=servers|pid=8940|start=07:32:40.706|verdict=ALIVE-after-22s
ASSET|B-injected-corrupt-servers|corrupt|64 B / F5A5FD42D16A / 00 00 00 00
```
**手法（值得记）**：他换的是**构建输出目录里的** `Assets\Icons\emby.png` —— **不改工作区源、不重编** ⇒ 同一支产物、只换资产。有效资产 = `2,094 B / 3B716E0B523B / 89 50 4E 47`。

**⇒ 2×2 矩阵现在是完整的**（同一源码代 + 同一重建产物内）：

| | 有效 PNG | 坏字节 |
|---|---|---|
| **注入形态** | 崩（A/A'，1.216/1.229 s） | **崩（3.2.1，1.277/1.329 s）** |
| **干净源码** | 活（C/D，22 s） | 活（臂 2，22 s） |

⇒ **资产变量被单独摘出**：注入形态在场时，**崩与图片能否解码无关** ⇒ 按 captain 判读口径，措辞**升回**为：
**"在该源码代 + 该重建产物下，图片解码不是崩因"**（并保留 §3.2 的前置：**该形态在场且行模板被实际实例化**）。
⇒ 本文档 §2 里那句"暂不得这样写 / 否证面尚未闭合"**自本条起作废**（它当时成立，因为臂 3 还没跑）。

**3.2.2 触发条件收紧为"**至少一行被实体化**"（`verifier` 2026-09-12 自纠 + 我读盘复核）**

他的 `ARM A'`（注入支 `EDCB26A17408`，pid 3628 start 07:26:42.205）沙箱日志（我从盘上逐行核）：
```
07:26:42.828  SERVERS-SHAPE bare-array-fallback count=1
07:26:43.079  RAIL loaded **1** servers; originalAccounts=0; legacy=0
07:26:43.161  Nav -> servers
07:26:43.376  ServersPage loaded: **1** servers; enabled=1; originalAccounts=0; legacy=0; readOnly=0
（此后无新行）⇒ 1.229 s 退出、0xC000027B
```
⇒ **触发条件 = "至少有一行被实体化"，不是"行多"**：我那次 16 台（页面加载后 **0.68 s** 崩），他这次 **1 台（0.058 s** 崩）——**同一形态、两代产物、同一个崩码**。
⇒ **连带更正 §3.1 的那条"限制"**：该节把"14 台在首屏视野之外"当成了一个解释项；按本条它只是"**没被实体化**"的**结果**，不是原因 ⇒ **机制解释里把它摘掉**；§3.1 的正控结论（`ServersPage loaded: …` 由页面自身写出、全程零输入零滚动）在他那一代**同样成立**。
⇒ 前置条件最终写法：**"该形态在场 + 行模板被实际实例化（≥1 行）"**（0 行 ⇒ 不崩，见 §3.2 的空数据根对照 `A''`）。

**证据文件身份（我核过的两代，供对账）**：他的 `shell/Tests/evidence/probeB-independent-repro.txt` ——
追加"§6 更正 1"后的那代 = `9,846 B / 84056E7254F5 / 97 行`（提交 `a49f116`）；**当刻（含 §7 的 2×2 矩阵）= `13,373 B / A221B1CD4A69 / 131 行`**（`text: unset` ⇒ 两形态逐字节相同；我 §3.2.1 引的就是这一代）。
**另附一条对账要用的核对**：`shell/docs/WORKSPACE.md` 里钉住 `E5211758243B` 的行当刻是 **5 行**（`:3989` / `:4032` / `:4037` / `:4274` / **`:4563`**）—— `verifier` 报的是 4 行，他读数之后账本又长了 ⇒ 所以"改证据文件必须同一提交刷新那几行身份"的**行数要现读**，这也是"**口径以协议为准、不动被钉住的证据文件**"这条路线的直接理由。

## 4. 收尾（无论结果，缺一即视为未收尾）

1. `ServersPage.xaml` / `ServersPage.xaml.cs` **逐字节回滚**：贴 `git hash-object <file>` == `git rev-parse HEAD:<file>` + `git status --porcelain -- shell/App/Features/Servers/` **空**；
2. 若动过 `emby.png`：从 `%TEMP%` 还原件并贴 `bytes + sha256_12 + 前 4 字节` 对基线；
3. 删 `E:\ui3-probe-bin\`（整目录），并贴删后 `Test-Path` = False；
4. 证据文件 `shell/App/Features/Servers/evidence/t30-probeB-icon-source.txt`（`.txt`、带 pid + 时刻 + HEAD + 两臂原始行 + §3 判定 + 本条收尾读数）；`git add` 后跑门禁贴 `EXIT / Σ三项 / checks / 时刻 / HEAD`。

## 5. 明确的"不做"

- 不改 `Theme/`、`App.xaml`、`MainWindow.*`（不是我的领地）；
- 不为本探针动任何**产品路径**的默认行为；探针改动**不留在工作区**；
- 不抢前台、不驱动输入、不结束任何不是我自己起的进程；
- 不把本探针结论外推为"崩溃根因已定"——它只回答 §0 里那两句。

## 6. 附：§2.1 注入复算的**可复现写法**（append-only；`verifier` 2026-09-12 08:1x 报"复算不出来"触发）

> 本节**只追加、不改 §2.1 原文一字**（`t28`/`t30` 类文件一律 append-only 的裁令）。
> 一句话结论：§2.1 初值 `5378C81D5D53` **不可复算**［🔴 **§9 更正**：此"不可复算"**只在「行级插入族」内成立**；**「子串替换族」内它可复算** = 5,151 B / `5378C81D5D53` / git-blob `fd7718b646d0`（我 2026-09-12 独立复现，两族定义见 §9）］（`verifier` 用 3,990 个候选 = 82 行位 + 缩进/尾空格变体 + 正文单字符替换，外加 4 种行尾变体全扫，命中者 **0**）；**可复现的真值 = 5,151 B / sha256_12 `4F11560FA868`**。

**复算时刻 + 命令原文（照抄即可）**：

```powershell
# 0) 取基底（cmd 重定向保原字节；PS 文本 cmdlet 会重编码 ⇒ 不得用 Set-Content/Out-File）
cmd /c "git cat-file blob 9b886b9:shell/App/Features/Servers/ServersPage.xaml > base.xaml"
#    基底核验：5,100 B / sha256_12 DE652FDA9CAA / CR=0 / 82 行 / 锚行 = 第 48 行
# 1) 行级插入：在【锚行整行】之前新起一行
#    锚行逐字 = 35 个空格 + Loaded="OnRowIconLoaded" />            （整行 62 字符）
#    新行逐字 = 8 个空格 + Source="{StaticResource ServerIconSource}"（整行 50 字符）+ LF ［🔴 §9：**缩进是身份变量** —— 同一正文换缩进即换身份（35 空格 ⇒ 5,178 / `951CC886A2CA`）］
# 2) 落盘后核验（三条都要对）
Get-FileHash .\injected.xaml -Algorithm SHA256     # sha256_12 = 4F11560FA868
(Get-Item .\injected.xaml).Length                  # bytes     = 5,151
git hash-object .\injected.xaml                    # git-blob  = dafb5058f220…
```

**复算读数（我 2026-09-12 08:27 实测）**：注入后 xaml = **5,151 B / sha256_12 `4F11560FA868` / git-blob `dafb5058f220`**；行数 = **83**（口径 = PowerShell `-split` 计数）/ **82**（口径 = `ReadAllLines`，见 §9 行数口径）（= `verifier` 当刻得到的值）。

**更硬的复钉方式（免解释、已入库）**：`evidence/probeB-injected-ServersPage.xaml.txt` = 5,151 B / `4F11560FA868` / git-blob `dafb5058f220`；`evidence/probeB-injected-ServersPage.xaml.cs.txt` = 24,361 B / `7A6B2944E310` / git-blob `a0c6b98979a7` ⇒ 直接 `git hash-object <fixture>` 即可核，**不需要重跑任何插入逻辑**。

**两语义差异（供核对；不影响任何卡结论）**：把"在**整行**之前"误做成为"在**锚子串**之前"（= `String.Replace` 的语义）会把锚行从**行内第 28 个字符**处劈开 ⇒ 得到 **5,151 B / sha256_12 `5378C81D5D53` / 83 行 / git-blob `fd7718b646d0`** —— 与真值**字节数相同、行数也相同**，只有 sha256_12 / git-blob 能分辨。cs 侧两语义**重合**（锚行 `        InitializeComponent();`，锚串紧接行首内容）⇒ 24,361 B / `7A6B2944E310`，**故"cs 对得上"不能推出"xaml 也对得上"**。

**本节与 §2.1 的关系（如实登记）**：§2.1 正文在 `d4cd6ce`（2026-09-12 08:29，早于本裁令到达）已被我改过一次（把"插入 1"改成行级语义、把复算读数换成整文件身份表）；该次改动的**前态**可在提交 `db0414e` 处取回（协议 23,672 B / `00641B1786AA`）。若 Captain 要严格的"正文不动 + 全量追加"，我可把 §2.1 恢复成 `db0414e` 的正文而把更正全留本节 —— 等他一句话，我不自行再动。

**代号收口（`verifier` 2026-09-12 建议的写法，逐字采纳；此后全队引用不必再往返）**：
- **canonical 注入支 = `4F11560FA868`**（xaml；cs 侧 canonical = `7A6B2944E310`）—— 可复算：`git cat-file blob 9b886b9:<path>` + 两行插入（落点语义见 §2.1）。
- **`5378C81D5D53` = 在「行级插入族」内不可复算的在途代；在「子串替换族」内可复算**（= 5,151 B / git-blob `fd7718b646d0`；🔴 §9 更正，两族定义见 §9） —— 依据：同基底（与 `git cat-file blob 9b886b9:` **全字节 diffBytes=0**）+ 同插入文本/落点 ⇒ 复算得 `4F11560FA868`；`5378C81D5D53` 经 **82 个行位 + 缩进/尾空格变体 + 正文单字符替换 3,990 候选 + 4 种行尾变体全扫 = 0 命中**（`verifier` `t123` 复算）。本文件内它的其余出现（§2.1 的更正段与根因分析）**均为历史留痕**，一律按本代号读、**不得当配方**。
- **口径分离（`verifier` ④）**：前置条件对照 `A''`（空数据根 ⇒ `ServersPage loaded: 0 servers` ⇒ 不崩；见本文件 §3.2 / §3.2.2）与卡面要的**元判据**（"同时改动『源码形态』与『资产可解码性』的两格之差，不得写成单变量结论"，见 `verifier` 证据 `shell/Tests/evidence/probe-b-2x2.txt` §5）是**两条**，引用时分开写、互不替代。

## §7 臂 3 的独立复跑升级：2×2 四格「一次跑完」+ 两代数字的隔离（append-only，2026-09-12）

**性质**：本条**纯追加、不改任何既有结论** —— §2 / §3.1 / §3.2 / §3.2.1 / §3.2.2 与 §6 的措辞与限定语**照旧有效**。

**新增的更强证据（`verifier` `t123`）**：在**临时树**内把 2×2 四格**一次跑完**（一操作者 / 一源码代 / 一重建产物），落在
`shell/Tests/evidence/probe-b-2x2.txt`：

- **初代**（提交 `365c2ce`，`t123` 入库）：23,690 B / 252 行 / git-blob `17897d5655c2`。
- **当刻**（我 2026-09-12 17:30:34 实测；提交 `b687b45` 之后，worktree-blob == HEAD-blob `a69173ef5954` ⇒ 已入库）：
  **27,429 B / `sha256_12 300FFF9A2943` / 277 行 / CR=0 / 末字节 0x0A**。
  ⇒ 引用该件**必须写当刻那一行**；`365c2ce` 的 23,690 B/252 行属历史代际。

**四格读数（`verifier` 原文）**：臂 1 `@1.581 s` 崩 ｜ **臂 3 `@1.562 s` 仍崩（决定性格）** ｜ 臂 2、臂 4 均 ALIVE-22s ｜ 两格 `home` 亦 ALIVE-22s。

**与 §3.2.1 的关系（同结论、更严配对）**：§3.2.1 的依据是 07:31–07:32 的**单臂**腿 B；本条给出的是**四格同代一起跑**的矩阵 ⇒
结论相同（"图片解码不是崩因"，限定语 = 该源码代 + 该重建产物；前置 = 形态在场 + ≥1 行被实体化，见 §3.2.1/§3.2.2），
但**配对更严**（四格共享同一源码代与同一重建产物 ⇒ 排除跨代拼读）。

🔴 **两代数字不得混读（`verifier` 明确要求）**：
- 本条矩阵那一代：注入 dll `6601665129F7` / clean dll `ABA8EE27EB07`（提交 `365c2ce`）。
- §3.2.1 引的那一代：注入 dll `EDCB26A17408` / 被加载 Services `46DD2F45D233`（07:31 支）。
⇒ 两代**不是同一代**；引用时各带代际标注，禁止把一格的 dll 与另一格的崩溃时刻并列。

**改动追溯（本条留痕纪律）**：本条为纯追加。追加前该文件 = **30,545 B / `sha256_12 8E8EDC9C7622` / 256 行 / git-blob `ca6fbe93d6f3`**（提交 `3218702`）；需要逐字节取回时 `git show 3218702:shell/App/Features/Servers/PROBE_B_PROTOCOL.md`。
更早一代（`16d1168`）= 29,302 B / 251 行 / git-blob `a72522505b16` —— **`verifier` 2026-09-12 17:07 引的就是这一代**（当刻成立，其后被 `3218702` 推走）。

## §8 blob 口径：**原始字节** vs **过滤器后**（`verifier` 2026-09-12 提出；本节封住「同一条断言可同时被读成 True 与 False」）

**规则（写 "blob" / "worktree-blob == HEAD-blob" 必须带口径，二选一）**：
- **原始字节口径** = 磁盘上的字节（含工作区行尾）⇒ **一切 `bytes` 与 `sha256_12` 只按这个口径给**（门禁 `H5` 也是拿盘上文件比 hash）。
- **过滤器后口径** = `git hash-object` 的**默认**路径（走 `.gitattributes` 的 clean 过滤器、行尾归一）＝ 版本库内那个 blob 的 id；`git rev-parse HEAD:<path>` 与 `git cat-file -s HEAD:<path>` 属同一口径。
⇒ 两者不等 = **工作区行尾与库内不一致**，差值**恰好等于 CR 数**（⚠ 口径第三变量见 **§10**：`evidence/` 目录例外 + 规则**不追溯** ⇒ 此式只对**不在 `evidence/` 目录下**的文件成立）；⚠️ 此时 **`porcelain` 仍为空**（EOL 差异被过滤器吸收，不显示为改动）—— 「porcelain 空」**不保证**「原始字节 == 库内字节」。

**本仓依据**：`.gitattributes`（47 行）含 `* text=auto eol=lf` 与 `*.md text eol=lf`；`git check-attr text eol filter -- <path>` ⇒ `text: set` / `eol: lf` / `filter: unspecified`。

**实证（我 2026-09-12 17:37 复算，对象 = `shell/App/Features/Servers/EVIDENCE_T30.md`）**：
```
原始字节                      : 90,966 B / sha256_12 A9D16A7C9D91 / CR=614 / LF=614
git hash-object（默认，经过滤器）= eebd8c4d071d    ← 等于 HEAD:<path>
git hash-object --no-filters  = 37bca62570c0    ← 不等于 HEAD:<path>
库内那份（git cat-file blob 取回实测）= 90,352 B / CR=0 / sha256_12 2085CAA6D327
⇒ 默认==HEAD True ｜ --no-filters==HEAD False ｜ 原始差 90,966−90,352 = 614 = CR 数
```
**反例对照（工作区 CR=0 的文件不产生歧义）**：`PROBE_B_PROTOCOL.md`（CR=0）默认 = `28baeef194cf` == `--no-filters`；`shell/Tests/evidence/cache-const-single-source.txt`（CR=0）⇒ `6771d830363a` 两者同值。

**引用规范（与 §2.1 的身份三件套并列）**：三件套 = `bytes` / `sha256_12` / `blob`，
前两项**永远是原始字节口径**；第三项**不写口径即不可判**，须回问。缺 `sampled=`（采样时刻）时同理 —— 本仓一天内同一路径已实测到多代（§7 即例），**时刻与被验对象必须成对出现**。

## §9 更正 4（append-only，2026-09-12 17:5x）：原支 xaml **可复算** —— 缩进是身份变量；并列全四种算子族

**触发器**：`verifier` 复算出了我记作"不可复算"的原支（`951CC886A2CA`），并指出差的是**缩进**（35 空格，不是我记的 8 空格）。**我先独立重跑、再改本文件** —— 下表 8 行是**我自己的重跑结果**，不是抄他的。

**基底与四件**（`git cat-file blob 9b886b9:shell/App/Features/Servers/ServersPage.xaml`）：
`5,100 B / sha256_12 DE652FDA9CAA / CR=0 / 无 BOM / 82 行`；锚行 = **35 空格 + `Loaded="OnRowIconLoaded" />`**（整行 62 字符）；锚串偏移 2,889、锚行起始偏移 2,854；插入正文 = `Source="{StaticResource ServerIconSource}"` = **42 字符**。

**缩进扫描（正文 / 落点 / 行尾 三件固定，只变缩进）**
```
indent= 4  → 整行 46  bytes=5,147  sha256_12 02366269CD79
indent= 6  → 整行 48  bytes=5,149  sha256_12 F492D152DE1A
indent= 8  → 整行 50  bytes=5,151  sha256_12 4F11560FA868   ← §2 recipe（canonical #1）
indent=10  → 整行 52  bytes=5,153  sha256_12 82D9ECF309E2
indent=12  → 整行 54  bytes=5,155  sha256_12 475160C08524
indent=28  → 整行 70  bytes=5,171  sha256_12 E379C4EA57ED
indent=32  → 整行 74  bytes=5,175  sha256_12 104C39D1EF46
indent=35  → 整行 77  bytes=5,178  sha256_12 951CC886A2CA   ← **我原支 = canonical #2（可复算）**
```
⇒ **更正两处**：① §3 的"我原支 xaml 不可复算 / 没记原文"对 **xaml 不成立** —— 那一行就是 **35 空格 + 那 42 字符**，我只把缩进当成了常量 ⇒ 照上表即**先验后编**得 `951CC886A2CA`。② **cs 半边仍成立**（见下）。

**算子族并列（🔴 本轮最贵的教训）**：同一基底 + 同一正文，**算子不同或缩进不同 ⇒ 身份不同**；且**"不可复算"只在被枚举的算子族内成立**：
```
族 A 行级插入（"缩进+正文"作为独立一行，插在锚行整行之前；锚行整行匹配）
      缩进 8  → 5,151 / 4F11560FA868    （canonical #1 = §2 recipe）
      缩进 35 → 5,178 / 951CC886A2CA    （canonical #2 = 我原支）
族 B 子串替换（锚串 = 8 空格 + `Loaded="OnRowIconLoaded"` ⇒ 在锚行**行内第 28 字符**处命中，把锚行劈开）
      → 5,151 / 5378C81D5D53            （≡ §2.1 初值；**可复算，但必须写算子族**）
族 C 子串替换 + 插入文本带尾随 ` />`
      → 5,154 / B2DA6B4A1F6C            （本次新扫出的第 4 变体，列出防再踩）
```
（我本次重跑读数：族 A 缩进 8 = `4F11560FA868`、缩进 35 = `951CC886A2CA`；族 B = `5378C81D5D53`；族 C = `B2DA6B4A1F6C`。）

🔴 **两套算子编号的对照（`verifier` 已按 A/B/D 发布，本节按 A/B/C 写；对象同一批，别只引字母）**：`verifier` 的 **A** = 本节**族 A 缩进 8**（`4F11560FA868` / blob `dafb5058f220`）；**B** = 本节**族 B 子串替换**（`5378C81D5D53` / blob `fd7718b646d0`）；**D** = 本节**族 A 缩进 35**（`951CC886A2CA` / blob `5685908b514e`）——他把"缩进 35"单列为算子 D，本节把它当作族 A 内的**参数**。⇒ **引用时一律连配方写**（缩进 + 正文 + 落点 + 行尾），字母编号只在同一篇文章内有意义。三方独立复算现已一致（他的 A/B/D 三算子、我的 8 行缩进表、以及本文件 §2/§2.1 记的两支），本节即收口处。
⇒ 与 `verifier` 最新那条"`5378C81D5D53` 仍复算不出来"**不矛盾**：他的穷尽扫描是**族 A 内的行位/缩进/正文/行尾**，而 `5378C81D5D53` 住在**族 B**；他此前 `b687b45`（"范围过宽更正 —— `5378C81D5D53` 可复算（子串替换语义）"）与本条一致。
⇒ **`5378C81D5D53` = 可复算（族 B）**；§6 那句"不可复算"应按本节限定为"**在族 A 内不可复算**"。

**cs 半边：永久"仅历史引用"（我做了穷尽尝试，不再重试）**
原支 = `ServersPage.xaml.cs` **24,680 B / `836F82920539`**；差额 = 24,680 − 24,220 − 1 = **459 ⇒ 插入正文 ≈458 字符**（`verifier` 那份全限定形只有 140 字符 ⇒ 不是同一份）。［△ **算式订正见 §10 末附注**：Δ 应为 **460 B**，正文长度只能给**区间**（≤459，随缩进/行尾而变）；原句的 "≈458" 是把缩进与 LF 混算出的近似值］
恢复尝试（2026-09-12 17:5x）：`git cat-file --batch-all-objects --batch-check` 过滤 `blob 24680` = **0 命中**（从未入库）；`%TEMP` + `E:\` 全盘 = 24,680 B 的 `.cs/.txt/.xaml` **0 命中** ⇒ **原文不可恢复**。按 `verifier` 建议标 **"仅历史引用"**，**不列为配方**。

**三条口径（采纳 `verifier` 的教训 + 本条新增，写入本文件即生效）**
1. **注入配方四件套 = 缩进 + 正文 + 落点 + 行尾**（缺一件即不可复算）——我原错在把"缩进"当常量。
2. **"不可复算"必须声明算子族**（反证不得越过算子族）；正控方式 = 在该族内给出**唯一可行解**。
3. **整行字符数必须含前导空格**。

**改动追溯**：本条为纯追加；`:111`、`:168`、§2 recipe（:238）三处仅**插入指针**（原文未删，指针即本节入口）。追加前本文件 = `35,198 B / sha256_12 36769A3C1B36 / 304 行 / git-blob 645c820e06a1`（提交 `a4ec3c9`），可按提交逐字节取回。

## §10 口径第三变量：**路径是否匹配 `**/evidence/**`** ＋ 规则**不追溯**（`verifier` 2026-09-12 提出；我复现其主体、**否证其普适形式**）

**三件并报的方法（采纳 `verifier` 建议，写进本节即生效）**：
```
1) 路径是否匹配 **/evidence/**（仓库相对、一律正斜杠）
2) git check-attr text eol filter -- <正斜杠路径>
3) 三值并报 + 当场自证：default / --no-filters / HEAD:<path>
   （三者不等 = 路径或规则没吃到 ⇒ 先怀疑仪器，别先怀疑结论）
```

**规则（`.gitattributes:47 = **/evidence/** -text`；注释 `:41-46` 写明"证据产物字节保真、禁止行尾归一化"）**：`evidence/` **目录**下的文件不做行尾归一化 ⇒ **新入库**内容 blob == 原始字节。⇒ **§8 那句"差值恰等于 CR 数"只对不在 `evidence/` 目录下的文件成立**。

**我的复现（当刻 @HEAD `8c80281`）**：
- `shell/App/Features/Collection/evidence/t58-collection-selftest.txt`：`check-attr` ⇒ `text: unset` / `eol: lf` / `filter: unspecified`；三值全等 = `9bfaf1707996`；盘上原始 `9,803 B / CR=54 / sha256_12 762BBE08B22F`，库内 blob 字节 `9,803` ⇒ **逐字节相同**。
- 反例（我 §8 的对象）：`shell/App/Features/Servers/EVIDENCE_T30.md` 的 `evidence` 在**文件名**、不在**目录** ⇒ `text: set` ⇒ 仍走 `*.md text eol=lf` ⇒ 被归一化（`90,966 → 90,352`，差 614 = CR 数）。

**否证"普适"（我当刻全量扫描：`**/evidence/*.txt` tracked = 281 件）**：`CR=0` = 111 件｜**`CR>0` = 170 件**，其中 **7 件 blob 字节 ≠ 原始字节**（差值**恰等于 CR 数**，且 `text: unset` 确已生效）⇒ 「证据目录下 CR>0 的件，blob 都等于原始字节」**不成立**。  ← **当刻状态见文末 §10-C①：该族已闭合（`a487c62` 后 0 例）**

**这 7 件的机制 = 规则不追溯 ＋ 索引 stat 缓存**（我逐件实测，摘两件）：  ← **机制两半必须分写，见文末 §10-C②（前半=实测因果；后半=观察+推断、未获独立复现）**

| 读数 | `service-selfcheck.txt` | `s1-build-zero.txt` |
|---|---|---|
| 盘上原始 | 4,119 B / CR 25 / sha256_12 `D2EB55A28F64` | 607,233 B / CR 2,374 / `F2EE3DDECF9B` |
| HEAD = index blob | 4,094 B / **CR 0** / `073BC4074FF9` | 604,859 B / **CR 0** / `7E810B471900` |
| `git ls-files --debug` 的 size | **4,119（= 盘上）** | **607,233（= 盘上）** |
| `git status --porcelain` | 空 | 空 |

⇒ 这 7 件入库于 `:47` 规则引入（`bee99ad`，t28）**之前**：blob 在旧规则下已被归一化，而**索引 stat 缓存记的是盘上 size** ⇒ git 信缓存、**不重算 clean 过滤器** ⇒ 对它们显示干净。
⇒ **第三条盲区**（§8 只写了前两条）：「`porcelain` 空」既不保证「raw == blob」（§8 已写），也**不保证「blob == worktree 内容」**（本条新增）。
⇒ **引爆点（已实测）**：touch 一次（mtime 变）⇒ git 重算 ⇒ 该件**立刻变 ` M`**。预测检验：`shell/Tests/evidence/ui-t26-attempt3-clean-evidence.txt` touch 前 `[]` ⇒ touch 后 ` M`；随后按索引 mtime 复原、status 回到 `[]`（**内容一字未改**，该件 sha256_12 = `8956E558B35D`）。  ← **该判据只对 `a487c62` 之前有效，见文末 §10-C②**
⇒ **对克隆的影响**：新克隆得到的是 **blob** 内容 ⇒ 这 7 件克隆后的 `sha256_12` **≠ 台账里按盘上记的值** —— 正是 `:42-45` 注释要防的那件事，在这 7 件上**仍然活着**。**待处置（非本文件作者权限）**：① 重新 `git add` 使 blob 变成原始字节；或 ② 在台账里对这 7 件标注「blob 已被历史归一化（规则不追溯）」。

**仪器坑（`verifier` 提供，我复现）**：`git hash-object -- <path>` 用**反斜杠**路径时 `**/evidence/**` **匹配失败** ⇒ 按 `*.txt text eol=lf` 归一化 ⇒ 三值立刻不等：正斜杠 = `default = --no-filters = HEAD = 9bfaf1707996`；反斜杠 = `default = b4797c7cd810` ≠ `--no-filters = 9bfaf1707996` = `HEAD`。⇒ 与「`rev:path` 必须正斜杠」同族，**但这次连属性匹配都被影响**。  ← **判别条件与措辞订正见文末 §10-C③（该件必须 CR>0 才有判别力；被影响的是 `hash-object` 不是 `check-attr`）**

**改动追溯**：本条纯追加。追加前本文件 = `41,552 B / sha256_12 9A093BC32CEE / 351 行 / git-blob 243a66bd53cd`（= `1e6cdcd` 那代）；另 §8 的"差值"一句只**插入指针**（原文未删）。

**附注（2026-09-12 19:2x，答复 `verifier` 同批复现；三条）**

1. **§9 的 cs 差额算式订正**：Δ = 24,680 − 24,220 = **460 B**（正确第一步）。但**正文长度不能给单值** —— 落点算子为"整行插入（缩进 + 正文 + 1 个 LF）"时，正文 = 460 − 缩进 − 1 ⇒ **上界 459**；原句写成 "−1 = 459 ⇒ 正文 ≈458" 是**把缩进与 LF 混算**出来的近似值（`verifier` 独立复算也落在同一量级、同样带 ±1 摆动）。⇒ 改为**区间表述**：正文 ≤ 459 字符，缩进越大越短。**结论不变**（那半 24,680 B 的对象 0 命中对象库 ⇒ 永久"仅历史引用"），但"数字要能复算"这条纪律要求把混算句清掉。
2. **`verifier` 复现名单**：族 A（缩进 8 / 缩进 35）、族 B、族 C **四支逐项命中**（族 C = 我本次新扫出的第 4 变体）；cs 半边的"从未入库 ⇒ 不可恢复"由他**独立穷举**（`cat-file --batch-all-objects --batch-check` 过滤 `blob 24680` ⇒ 0 命中）⇒ 我的处置从"软表述"升级为**穷尽判定**。
3. **记一支他的构造（非 canonical，仅登记防再踩）**：他把族 C 的尾随 ` />` 再补一份 ⇒ 5,157 B / `802606BB8DA1` / blob `55c6c78e73ee`。**这是他自报的构造，不在 §9 的三族表内**，引用时**必须标"非 canonical / 构造支"**，否则会被后读者当成配方。⇒ 本条同时印证 §9 那条最贵的教训：**"不可复算"只在被枚举的算子族内成立，族本身是开放的**。

## §10-C 当刻状态与机制分级（2026-09-12 21:0x append-only；`verifier` 复现提请 + 我方复核；上文一字未删）

**① 那 7 件已闭合 ⇒ §10 正文的"待处置 ①/②"作废（对象 = `a487c62`）**
- `a487c62` 用 **`git add --renormalize`**（普通 `git add` 无效）把 7 件重登记为「blob 字节 == 盘上原始字节」。判据（双方同向）：旧代 blob 字节 = raw − CR，新代 blob 字节 = raw；7 件 `porcelain` 全空、`lastCommit` 全 = `a487c62`。
- `verifier` 当刻全量复扫：**脸** = tracked ∧ 正则 `(^|/)evidence/[^/]*\.txt$` ⇒ **301 件**（我 19:13 那次同 pattern 为 281 件，期间树长出 20 件）；`CR=0` **130** ｜ `CR>0` **171** ｜ `blob ≠ raw` **3**，而这 3 件**全是在制件**（`CR=0`、porcelain ` M`、差值来自属主追加）⇒ **§10 原报的族（`CR>0` ∧ `blob≠raw` ∧ `porcelain 空`）= 0 例**。
- 我当刻独立复核（**脸不同，已标注**：tracked `shell/**/evidence/*.txt` = **233 件**；`CR=0` 115 ｜ `CR>0` 118）⇒ `blobSize ≠ raw` **1 件** = `shell/App/Features/Home/evidence/t196-resume-card-16x9-backdrop.txt`（盘 15,523 / HEAD blob 10,339 / `CR=0` / porcelain ` M` = 在制件）⇒ **族 = 0 例**（与 `verifier` 同结论；两处差异全来自**脸**，非判据）。
- ⇒ `:383` 的「克隆后的 sha256 ≠ 台账」**只作历史**：当刻克隆得到的字节与台账一致。
- ⇒ 仍成立且更重要：**`porcelain` 空既不保证 `raw == blob`，也不保证 `blob == worktree`**（`verifier` 建议把这条写进门禁 H5 的 note；按 captain 收紧条款，门禁写入须走一张 `kind=work` 小卡 ⇒ **双方都不裸改门禁**）。

**② 机制分两半写（`:371`/`:382`）**
- 前半「**规则不追溯**」= **实测因果**：同一批文件在 `4934651` 是归一化 blob、在 HEAD 是原始字节，中间只隔 `a487c62` 一次 `--renormalize` 重登记。
- 后半「**索引 stat 缓存旧值 ⇒ 不重算 clean 过滤器**」= **现场观察 + 推断，未获独立复现**：`verifier` 的 scratch 仓（`core.autocrlf=true` 提交 CRLF ⇒ 随即置 false）**立刻**就是 ` M`，且 `git ls-files --debug` 的 `size: 0`（索引里没有可信 stat 缓存）⇒ 造不出"显示干净"态。⇒ 引用时**必须两半分写**，后半不得当实测引用。
- 连带：`:382` 的 **`touch ⇒ M` 判据只对 `a487c62` 之前有效**（它同时证明当时确实是缓存态）；`a487c62` 之后该可复现性消失，**不得再作现行判据**。

**③ 反斜杠那条（`:385`）成立，但必须带判别条件 —— 今日双方各误判过一次，故补正控/反控**
- **判别条件 = 该文件必须含 CR（`CR>0`）**：归一化只在"有 CR 可删"时才改变字节 ⇒ **`CR=0` 的件上"反斜杠 vs 正斜杠"两值恒等，该试验零判别力**（这正是双方各自试过的那一件）。
- **正控（`CR>0`，当刻逐位复现 `:385` 的值）**：`shell/App/Features/Collection/evidence/t58-collection-selftest.txt`（`CR=54`）——`hash-object --` **正斜杠** ⇒ `default = --no-filters = HEAD = 9bfaf1707996`；**反斜杠** ⇒ `default = b4797c7cd810` ≠ `--no-filters = 9bfaf1707996`。第二件独立重复：`t58-collection-ui-audit.txt`（`CR=36`）正斜杠 `1600a9e55082`（三值同）／反斜杠 `38dc6b00459d`（≠）。
- **反控（`CR=0` = 无判别力，且双方都试过它）**：`shell/App/Features/Search/evidence/t28-img-byte-channel.txt`（`CR=0`）两形态均 `default = --no-filters = HEAD = 30ba98131c8b`。
- **措辞订正**：`:385` 写「`**/evidence/**` **匹配失败**」不够准 —— `git check-attr` 用反斜杠形态**照样解析**（只把路径加引号打印，仍是 `text: unset` / `eol: lf` / `filter: unspecified`）；失败的是 **`hash-object` 在该路径形态下不吃这条属性**（于是回落到 `*.txt text eol=lf` 归一化）。⇒ 仪器纪律：**报属性面读数的是 `check-attr`，决定字节的是 `hash-object`**，且两者必须用**同一路径形态**才有可比性。

**④ 本次未改项**：§1-1 的 `spanSec` 措辞**不动**（`ui2` 已用同 spanSec、相反结论证伪其判别力；改判待 captain 发话，届时 append 精化并**同轮刷新引用该协议的证据行**）。  ← **本条已作废：captain 2026-09-12 裁定「改」，见文末 §10-D**

## §10-D 空场判据精化：`spanSec` 降为窗口声明（2026-09-12 captain 裁定「改」；append-only，§1-1 原句一字未删，仅加前向指针）

**判据（照此执行）**
> **空场判据 = `allFree` + `maxShells` + `distinctPids`（+ `freeSamples`）**；「空」判定 = **`allFree=true` 且 `maxShells=0`／`distinctPids=0`**。
> **`spanSec` 降为「窗口声明」**：它只回答"这个结论覆盖了多长的窗口"；引用它**必须并列写出 `Samples` 与 `IntervalMs`**（否则连"窗口多长"都不可复核）。

**为什么改（证伪本身）**：`spanSec ≈ Samples × IntervalMs`（`-Samples 5 -IntervalMs 1500` ⇒ 必然 ≈6.0–6.35 s）⇒ **结构上恒被满足**，只证明"确实采了 N 次"，对"场地空"**零信息量**。反例（双方各自实测、同刻现成）：
- 我：`allFree=True / spanSec=6.35`；
- `ui2`：`allFree=False / spanSec=6.35 / maxShells=1 / distinctPids=1`（占位者非其所有）。
⇒ **同一个 `spanSec`、相反的占位结论** ⇒ 旧措辞"必须同时给 `spanSec`"被证死（该句已在 `:31`/`:32` 就地标前向指针，原文保留）。

**协议级纪律（新增，适用全篇）**：**恒真／结构性满足的字段不得单独作为判据** —— 判据必须是"两种真实状态会给出不同值"的字段；只有一值的字段只能当**声明**。同族实例（今晚）：
- `最长行=709` 在两种状态下同值；`files=42/changed=9` 恒定而 `hits` 才变；
- `porcelain 空` 既不保证 `raw == blob`，也不保证 `blob == worktree`（§10-C①）；
- ⇒ 报判据时必须写清「**它能取哪些值、各值对应什么状态**」。

**引用刷新（本轮同步核查，结论 = 刷新集为空）**：`grep -rn 'PROBE_B_PROTOCOL' -- shell kernel` 命中 41 行，逐条看**都在声明自身日期/代际**（`WORKSPACE.md`/`VERIFY_S1.md` 台账行、`EVIDENCE_T30.md:609` 的 16,040 B 那一代、`t137-…audit.txt:39` 的 `16d1168`/29,302 行、`probe-b-2x2.txt` 的 § 引用等）⇒ 属**历史读数**，不在"刷新当刻值"的义务内；**没有任何一行声明本文件的当刻身份** ⇒ 本轮**无需刷新任何引用行**（这也是我把本文件写成"历史行带日期、当刻值只在本文件末尾追记"的原因）。

**改动追溯**：本条**纯追加** + 三处**仅加同行前向指针**（`:31`/`:32`/`:415`，原文未删、**行数不变** ⇒ `:58`/`:171`/`:190` 三锚零位移）。追加前本文件 = **52,340 B / `sha256_12 E6C6A42612A6` / RAL 415 行 / git-blob `d9330c93d420`**（提交 `3cfa253`，21:10:04）；需要逐字节取回：`git cat-file blob d9330c93d420`。

## §10-E 两条口径落盘（2026-09-12 captain **书面授权"卡外直落"**；判据 = 已书面约定 ＋ append-only ＋ 不越面 ＋ 不改判定逻辑，先例 `t213` §7 头段）

**① 附注 2 升为正式条（"改规则 ⇒ 回溯"义务）**：**凡改动影响 blob 编码的规则（`.gitattributes` 等）⇒ 必须对受影响面做 `renormalize` 回溯**；**`-text` 类规则不追溯**已入库对象。
- 判据（可复算）：改规则后对**受影响面**跑 `git add --renormalize -- <面>`（**普通 `git add` 无效** —— 索引 stat 缓存会让 git 认为"没变"），再逐件核对 `hash-object == HEAD blob`。
- 本仓实例：`**/evidence/**` 的 `-text`（`.gitattributes:47`）**不追溯**已入库对象 ⇒ 7 件历史件被历史归一化（diff 恰等于 CR 数），由 **`a487c62`**（`--renormalize`）重登记闭合；当刻该族 **0 例**（口径与两面读数见 §10-C①）。

**② 两层写法（不变量 ＋ 采样件数，正式口径）**
> **不变量**：`tracked evidence/*.txt` 里「**blob 无 CR ∧ 盘上有 CR**」= **0 例**（采样点 **20:26 / 20:37 / 20:39** 全部成立）；
> **件数**：只作**采样代**（**302 / 302 / 306**，随入库增长）⇒ 引用必**重采**并写 `sampled=`。
- **为什么必须分两层**：不变量的**真值不随树长大而变**，件数会变（本轮一小时内长出 4 件）⇒ 把"件数"当判据，就是在重演 §10-D 立的那条纪律（**恒真/易变字段不得单独作为判据**）。
- **面（两个，勿混，报数必带面）**：`verifier` 面 = tracked ∧ 正则 `(^|/)evidence/[^/]*\.txt$`（该次 301）；我面 = tracked `shell/**/evidence/*.txt`（当刻 233）。⇒ 两面差异**全来自面，不来自判据**。

**改动追溯**：本条**纯追加**，历史读数一字未删、无同行指针改动。追加前本文件 = **55,557 B / `sha256_12 3B66BAAD2526` / RAL 435 / CR 0 / LF 435 / loneCR 0 / git-blob `2c88c014ebc4`**（提交 `b675122`，21:26:12）；需要逐字节取回：`git cat-file blob 2c88c014ebc4`。

---

## §11 G1 冻结窗口执行卡（2026-09-12 22:3x append-only；**只写执行清单与自指陷阱**，不改任何判定逻辑）

**用途**：`t260 → t257 → G1 → v7` 这条冻结链里，G1 由我执行 ⇒ 开冻广播一到就要**零准备**开跑。本节把命令、四件快照、两维报告与**自指陷阱**一次写死；本节**不授权任何人提前跑门禁**。

### §11-1 三条前置（缺一不得开跑）
1. `t260`（Home 填充归一）与 `t257`（详情 hero 冷启）**均已结单**，且其属主 `porcelain` 空；
2. **captain 开冻广播**已发出（含顺序与四件快照口径）；
3. **清场**：`Get-Process dotnet` = **0** ∧ `AIPlayer*` / `MpvHost*` = **0**（并发构建会写 `obj/**`，会把门禁扫描面与 H3 计数污染成假红/假绿）。

### §11-2 命令块（逐字可粘；**针必须由拼接构造**，理由见 §11-5）
```powershell
# ②-1 清场判据（三条都要 0）
@(Get-Process dotnet -ErrorAction SilentlyContinue).Count
@(Get-Process -Name 'AIPlayer*','MpvHost*' -ErrorAction SilentlyContinue).Count

# ②-2 门禁本体（唯一正确调用形态；-File + -ExecutionPolicy Bypass；绝不用 -Command）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "shell/tools/evidence-hygiene-check.ps1"

# ②-3 进程面快照（自指安全版：针拼接构造 + 自身祖先链剔除 + visited 去环）
$needle = 'evidence-hygiene' + '-check' + '.ps1'          # <- 不写完整字面，避免"观测者自己命中自己"
$esc    = [regex]::Escape($needle)
$strictRe = '-ExecutionPolicy\s+Bypass\s+-File\s+(?:"[^"]*' + $esc + '"?|''[^'']*' + $esc + '''?|[^\s"'']*' + $esc + ')'
$notC   = '\s-Command\s'
$chain  = New-Object System.Collections.Generic.List[int]
$seen   = New-Object System.Collections.Generic.HashSet[int]
$cur    = [int]$PID
while($cur -gt 0 -and $seen.Add($cur)){ $chain.Add($cur)
  $p = Get-CimInstance Win32_Process -Filter "ProcessId=$cur" -ErrorAction SilentlyContinue
  if(-not $p){ break }; $cur = [int]$p.ParentProcessId }
$all     = @(Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -match $esc })
$foreign = @($all | Where-Object { -not $chain.Contains([int]$_.ProcessId) })
$strict  = @($foreign | Where-Object { $_.CommandLine -match $strictRe -and $_.CommandLine -notmatch $notC })
$rawPpid = @($foreign | Select-Object -ExpandProperty ParentProcessId -Unique)
"sampled=" + (Get-Date).ToString('HH:mm:ss.fff') + " @HEAD=" + (git rev-parse --short HEAD)
"含针=$($all.Count) 剔自身链=$($foreign.Count) strict本体=$($strict.Count) raw去重ppid=$($rawPpid.Count) 差(壳)=$($foreign.Count-$strict.Count)"
$strict | ForEach-Object { 'BODY pid=' + $_.ProcessId + ' ppid=' + $_.ParentProcessId + ' name=' + $_.Name + ' start=' + $_.CreationDate.ToString('HH:mm:ss.fff') }

# ②-4 门禁临时名（t258 后 per-RUN 唯一；确认没有别人的残留体）
@(Get-ChildItem $env:TEMP -Filter ('gate-h7-head' + '*.tmp') -ErrorAction SilentlyContinue).Count

# ②-5 工具身份（每跑现采，与窗口同代）
$p = 'shell/tools/evidence-hygiene-check.ps1'
"blob=" + (git rev-parse --verify "HEAD:$p") + " size=" + (git cat-file -s "HEAD:$p")
```
> ⚠ ②-3 里的 `'gate-h7-head' + '*.tmp'` 与 `$needle` 同样**刻意拼接**：命令块自身不得出现可被判据命中的完整字面（同族纪律见 `CODE_STANDARD §9.9(B)`「表达改一下、结论静默变」）。

### §11-3 四件标准快照（报告四件齐，缺一不可引）
1. **窗口**：`GATE-START|…|time=…|head=…|pid=…|runId=…` 与 `SUMMARY|verdict=…|pid=…|runId=…` 两行（**整行粘**，不切片）；外加 `headA`（跑前）与 `headB`（跑后）。
2. **工具身份**：**脚本 blob + size**（§11-2 ②-5；本协议 v 已知代例：`7a578e26b419` / 81,213 B，22:34 采）。
3. **进程面双口径**：`strict`（本体，操作性）｜`raw`（含壳，诊断性，**按 ppid 去重**）｜**差 k 个壳**；每条都给 `pid/ppid/name/start/flags`。
4. **A/B 正控**：A = 该判据应命中的样本命中；B = 相邻/反例不命中（两格都要有，且**同面同算子**）。

### §11-4 H7 两维报告（`t258` 入库后**方可核**）
- **实体面**（共享资源）：`H7-TEMP|path=…|pid=…|runId=…|unique=per-process` ⇒ 临时名 per-RUN 唯一 ⇒ "两个体互相覆盖/读半成品"是否已闭合。
- **声明面**（能否断言无并发）：须 `runId` 自述**且**起止快照 `strict` 为 0 且输出含唯一 `H7-TEMP|…` ⇒ 才能写"该窗口无并发"。
- **两格独立给**，禁止合并成一句"可核/不可核"；任一格不足时写明**不足在哪一件**。

### §11-5 🔴 自指陷阱（本节是 22:34–22:35 实测出来的，不是推想）
观测者的**自身工具链**会满足"命令行里出现过脚本串"这一粗谓词，来源有三个，都能造成 ±k 误计：
1. **runner 内嵌整段脚本文本**（`node.exe` 承载工具调用正文）；
2. **观察者自己的命令正文含脚本字面** —— 本机工具调用把正文写成临时 `.ps1` 再以 `-ExecutionPolicy Bypass -File <temp.ps1>` 启动 ⇒ 一行命令里同时出现"标准调用形态 + 脚本字面"；🔴 **机制描述已更正 → 见 §12-6**（本机入场形态实为 `-Command`，不是"临时件 + `-File`"；真正触发点是**正文里的举例串**）
3. **`-File` 实参 vs 正文提及** 不可区分（粗谓词只看"出现过"）。
⇒ **精化谓词**：把判据从"命令行出现过脚本名"改为"**`-File` 的实参本身是那个脚本**"（§11-2 ②-3 的 `$strictRe`）+ **自身祖先链剔除** + **针拼接构造**。三项缺一，快照就会把观察者自己算成本体。

**合成的正/负控（22:35:38 实测，7/7 与预期一致）**：正控 3 例（单引号相对路径 / 双引号含空格绝对路径 / 无引号相对路径）全 True；负控 4 例（`-Command` 壳形态 / `-File` 是观察者临时件 / 临时件正文再提脚本 / 观察者链自身）全 False。

**当刻实测（示例读数，带采样时刻）**：`22:35:53.006 @HEAD f2bb0df` ⇒ `含针=6 ｜ 剔自身链=6 ｜ strict 本体=2 ｜ 差(壳)=4`，两个本体 `pid=17528(start 22:33:43.750)` 与 `pid=17648(start 22:35:43.597)` ⇒ **有他人正在跑门禁** ⇒ 该窗口**不是静默窗口** ⇒ G1 **不得**在此刻开跑（这正是本节要挡的事）。

### §11-6 禁写清单（防把结论写坏）
- 不写"该窗口无并发"，除非 §11-4 两块都足；否则写「**实体面=已闭合/未闭合** ＋ **声明面=可核/不可核（缺哪件）**」。
- 不许只贴 `EXIT`（退码非判据）：必须同贴 `GATE-START` 与 `SUMMARY|verdict=` 整行。
- `porcelain` / "在途 vs 已入库" 必须与 `bytes/sha12` **同一采样时刻**；引"在库"按「该路径在 `X` 代入库 + 三元组（代 + 采样时刻 + HEAD）」写。
- 本协议件自身若在窗口内被改，则**该次窗口读数作废**（判据面必须静止）。

**改动追溯**：本条**纯追加**，历史读数一字未删、无同行指针改动。追加前本文件 = **57,654 B / RAL 449 / CR 0 / LF 449 / loneCR 0 / git-blob `223b68f69959`**（22:33 采、`porcelain` 空；该代 `sha256_12` 本次未采，需要时按 blob 取回）；取回：`git cat-file blob 223b68f69959`。

---

## §12 G1 就绪件（`t266`，2026-09-12 22:3x append-only）—— 四条命令块 + 四件快照字段 + 工具身份 + H7 两维模板 + 「0 命中」三件组

**定位**：`§11` 是**执行卡**（开跑那一刻粘什么），本节是**就绪件**（每条命令"预期读数长什么样"写全）。两者同口径；**冲突时以本节为准**。**本节不授权任何人提前跑门禁** —— G1 仍由 captain 广播后由我执行。

### §12-1 四条命令块（逐字可粘 ｜ 绿色/红色读数形态）
| # | 面 | 命令（逐字） | ✅ 绿色读数形态 | ❌ 红色/非绿读数形态 |
|---|---|---|---|---|
| 1 | Debug 构建 | `dotnet build shell/App/AIPlayer.Shell.csproj -c Debug` | `EXIT=0` ∧ `0 个错误` ∧ 警告数**落在当刻基线带** | `error CS` 行 ≥1，或 `EXIT≠0` |
| 2 | Release 构建 | `dotnet build shell/App/AIPlayer.Shell.csproj -c Release` | 同上（**Release 基线另记，勿与 Debug 混比**） | 同上 |
| 3 | 门禁 | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/evidence-hygiene-check.ps1` | **`GATE-START|…|time=…|head=…|pid=…|runId=…` 与 `SUMMARY|verdict=PASS|checks=10|pass=10|fail=0|inconclusive=0|…|pid=|runId=` 两行齐** ∧ `EXIT=0` | `SUMMARY|verdict=FAIL╱INCONCLUSIVE`；**只看 `EXIT` 不算**（退码非判据） |
| 4 | 格式 | `dotnet format shell/App/AIPlayer.Shell.csproj --verify-no-changes --severity warn --exclude reversed` | `EXIT=0`（无 finding 输出） | `EXIT=2` + 逐条 `WHITESPACE`/`IMPORTS`/… |

⚠ 三条纪律：① 构建读数**必须写清 `-t:Rebuild` 与否**与**输出目录**（默认输出会与他人实例争锁 ⇒ 需要时用 `-p:OutputPath=<私有目录>`，并把它写进读数）；② **警告基线随代** —— 引用只写"当刻基线 = N（采样时刻 + HEAD）"，不得写成固定值（历史读数示例 ≈1,216–1,230，各自带采样时刻）；③ 格式命令**必须带 `--exclude reversed`**（`reversed/**` 不在 G1 面内）。

### §12-2 四标准快照字段清单（**缺一不可引**）
1. **raw / strict 双口径**：谓词原文 + 枚举面 + `raw`（含壳，**按 ppid 去重**）+ `strict`（本体）+ **差 k 个壳**；
2. **进程表**：逐条 `pid / ppid / name / start / flags`（`-Command` 或 `-File`）；
3. **A/B 正控**：**同面同算子**；A = 该判据应命中的样本命中；B = 相邻/反例**不**命中（两格都要给，且必须能被合成命令行复现）；
4. **采样时刻 + HEAD**：**同一行**写出（任何读数脱离它即不可引）。
⇒ 采集命令与**自指三件套**（针拼接构造 / 剔除自身祖先链 / 判据只认 `-File` 实参）见 `§11-2 ②-3`、陷阱成因见 `§11-5`。

### §12-3 工具身份采集（**读数必须与工具同钉**）
```powershell
$p = 'shell/tools/evidence-hygiene-check.ps1'
"blob=" + (git rev-parse --verify "HEAD:$p") + " size=" + (git cat-file -s "HEAD:$p") + " worktreeSize=" + (Get-Item $p).Length + " porcelain=" + ((git status --porcelain -- $p) -join '')
```
- **记录位置**：**紧跟 `GATE-START` 行之后**；窗口读数、工具身份、四件快照三者**同段**（禁止分散到不同段落后再"拼接"）。
- 已知代例（22:34 采）：`blob=7a578e26b419…` / `size=81,213` / `worktreeSize=81,213` / `porcelain` 空。
- 🔴 若 `worktreeSize ≠ size` 或 `porcelain` 非空 ⇒ **该次窗口读数作废**（判据面未静止）。

### §12-4 H7 两维报告模板（**两格独立给，禁合并成一句「可核/不可核」**）
```
H7 实体面（共享资源）    = 已闭合 / 未闭合          依据：H7-TEMP|path=…|pid=…|runId=…|unique=per-process ＋ 当刻实采
H7 声明面（无并发断言）  = 可核 / 不可核（缺 <哪一件>） 依据：runId/pid 自述 ∧ 起止快照 strict=0 ∧ 唯一 H7-TEMP 行
```
- 只写"不可核"而**不写缺哪一件** = 不合格；**缺任一格时不得写"该窗口无并发"**。
- 两格的依据必须**分开列**（同一行里并列写两条依据，等于把两格悄悄合并）。

### §12-5 「0 命中」三件组模板（今晚实证：**空输入与"命令没跑"不可分**）
```
结论形态：<面> 上 <模式> 命中 = 0   ⇒ 三件必须齐：
  ① EXIT=<码>        （git grep 无命中 = 1；argv 错 = 129、缺 rev/路径 = 128 —— 见 CODE_STANDARD §9.9(A) 第 5 条）
  ② 行数=<n>（方法=…）（**只认 `[IO.File]::ReadAllLines(<path>).Count` 或字节层 LF 计数**；裸 `(Get-Content).Count` 与
                        `| Measure-Object -Line` **不可作判据** —— 后者跳过空行、且**对空输入也输出 0**，与"命令没跑"不可分）
  ③ 同面正控=命中 <m>  （同一面 + 同一算子 + 一个**已知存在**的样本 ⇒ 证明"面 + 算子"这条链会命中）
```
- **没有 ③ 的"0 命中"一律降级为不可引**（与 `CODE_STANDARD §9.7/§9.8` 的四件套同一条腿：本节给的是"命中数"这一 **N 类读数**的最小可引形态）。
- 正控实例（**引用必带面 + 采样时刻**）：`api_key=` —— ① captain 采信读数 **638 行**（面以他当刻为准）；② 我当刻可复算的另一面：tracked `shell/**` 六类（`*.md/*.cs/*.ps1/*.txt/*.xaml/*.json`）`git grep -F 'api_key='` = **674 行**（22:36 采 @HEAD `efbf117`）；③ 同族反例面：真实根 `aiplayer.log` 上 `api_key=` 行 = **135**（同刻）⇒ 三个面三个数 ⇒ **正控也必须写面**，否则又会造出"同一断言两个真值"。

**改动追溯**：本条**纯追加**，`§11` 与历史读数一字未删、无同行指针改动。追加前本文件 = **64,853 B / `sha256_12 32BCFD2F73D9` / RAL 527 / CR 0 / LF 527 / loneCR 0 / git-blob `39ccbc93de0a`**（= 提交 `efbf117` 代，22:37:17.031 采、`porcelain` 空）；取回：`git cat-file blob 39ccbc93de0a`。

### §12-6 更正 §11-5 ② 的机制描述 + 判据优先级重排（2026-09-12 22:39；`reviewer` 实证 + 我复采；**append-only**，§11-5 原句一字未删、仅加前向指针）

**① 错在哪**：§11-5 ② 把自指机制写成"本机把正文写成临时 `.ps1`，再以 `-ExecutionPolicy Bypass -File <temp.ps1>` 启动" ⇒ **不成立**。两侧独立实测（逐级读 `Win32_Process`，`COMMAND` = 含 `\s-Command\s`、`FILEflag` = 含 `\s-File\s`）：
```
reviewer 2026-09-12 22:36:56.677 @HEAD e6c84d6 ：
  1880(powershell, COMMAND=True  FILEflag=False) > 1324(node) > 22096(node bin.js web) > 17184(cmd.exe)
我       2026-09-12 22:38:39.867 @HEAD fba0a9e ：
  15836(powershell, COMMAND=True FILEflag=False) > 22320(node runner) > 22096 > 17184
⇒ 入场形态 = `-Command "…"`（不是 `-File`）；公共祖先是 `22096 / 17184`（多成员共享的 harness 进程）
```
**② 真正的触发点** = **命令正文里的"举例串"本身**（正文写下标准形态时，那段**文本**会被粗谓词当成"`-File` 形态"）。它污染的**不是**合取式判据，而是**单条件命中**：我 22:35 的打印逐条给的是 `$strictRe` 的**单独**匹配值（未与 `¬\s-Command\s` 取合取），于是**我自己链上的成员**在那一列显示 `True` —— 那是"只判一半条件"的**显示口径**问题，合取式判据本身没失效。
**③ 判据优先级重排（本条为最新口径，覆盖 §11-5 的排序）**：
1. **`¬\s-Command\s` 合取项 = 第一必要项**（被 `-Command` 包裹的正文一律不算本体）；
2. **剔除自身祖先链（visited 去环）= 第二必要项**（两种入场形态都靠它兜住；只剔 `$PID` 不够）；
3. **`-File` 实参判别**（只认 `-File` 后紧邻实参 = 门禁脚本）⇒ 能挡"正文提及"，**挡不住"正文举例标准形态"**；
4. **针拼接构造 = `raw` 面必要项**（否则观测者自身进入候选集，把 `raw` 与"差 k 个壳"算歪）。
**④ 对我 22:35:53 那条读数的自我更正（三态纪律）**：「`strict` 本体 = 2（`pid=17528` / `pid=17648`）⇒ 当时有 2 个外来本体」—— 该读数用的是**合取式**（两条都 `-File` 形态、无 `-Command`），**"真本体"的方向仍成立**，但我**当场未核对**它们的 `-File` 实参是否真指向门禁脚本、也未核父链形态 ⇒ 按本节新口径**降级为"待复核"**，**不得**作为"当时确有 2 个外来本体"的结论引用。
⇒ **要下这类结论，必须补三件**：`-File` 实参**逐条打印** + **父链形态** + **我方入场形态自述**。
**⑤ 报告新增要求（`reviewer` 提出、我采纳）**：窗口/快照报告必须自述**本方入场形态**（`-Command` 或 `-File`）+ **自身祖先链 pid 清单** ⇒ 让下一位能独立判断"有没有把自身算进 `strict`"。

**改动追溯（§12-6 自身）**：本条纯追加。追加前本文件 = **70,762 B / `sha256_12 D0F72FD276B5` / RAL 580 / CR 0 / LF 580 / loneCR 0 / git-blob `89774b9afb7e`**（= 提交 `fba0a9e` 代，22:39:14.742 采、`porcelain` 空；含 `§11-5 ②` 的**前向指针**一处，属同笔）。

### §12-7 🔴 工具面在途 ⇒ **窗口读数一律作废**（2026-09-12 captain 立；我落为前置条与作废条件）

**新纪律（captain 原句口径）**：**禁止在 `shell/tools/**` 有在途改动时跑门禁并把读数入账/入结论**；G1 的**前置追加**「`t261` 结单 **＋** 工具 blob 稳定」。

**判据（与 §12-3 的作废条件同源，合成两条，缺一即作废）**：
1. `git status --porcelain -- shell/tools/` **必须为空**（工具面没有在途改动）；
2. 工具身份三值必须自洽：`worktreeSize == size`（HEAD 侧字节数）**且** `worktree blob == HEAD blob`。
⇒ 任一不满足 ⇒ **该次窗口的全部读数（含 `SUMMARY|verdict=`）不得入账、不得作为结论引用**，只可留作"观察到某现象"的过程记录。

**立此条的实证（我自己的两次窗口，逐秒可对）**：
```
工具面当刻：工作区 83,359 B / sha256_12 C8C196DAA668 / mtime 22:50:44.637 ｜ HEAD 81,213 B / blob 7a578e26b419 ｜ porcelain=' M'  ⇒ **在途**
我的窗口 A（"改前"）22:48:24.209 → 22:50:44.128  ⇒ 结束时刻比该 mtime **早 0.509 s** ⇒ 无法证明其用的是已提交代
我的窗口 B（"改后"）22:54:32.363 → 22:56:56.260  ⇒ 落在该 mtime **之后** ⇒ **用的是在途脚本**
⇒ 结论：A 与 B **两条读数按本纪律一律作废**（B 尤其：它报出的 PASS 10/10/0/0 是在途工具代的产品，不得引用）
```
**为什么必须这么严**：本次实证里，同一脚本的**同一处改动**在其作者自曝中有两版假读数（`broad-ignored 16→281`、`emptyCatch 0→311`）—— 工具面在途时，"被测对象"与"量具"同时是移动目标，读数的**归因**不成立（`t261` 结单里作者自己把 `:490` 的 311 记为 first cut 的中途态）。

**与既有条的关系**：`§12-3` 已要求"身份三值自洽 + 记录位置紧跟 `GATE-START`"；本条把**工具面是否在途**升为**前置闸门**（在途 ⇒ 连记录都不发生，谈不上入账）。§12-1 的"清场"再加一项：`shell/tools/` 的 porcelain 为空 ∧ `t261` 已结单。

**改动追溯（§12-7 自身）**：本条纯追加，历史与 §11/§12/§12-6 一字未删。追加前本文件 = **74,109 B / `sha256_12 C5559F3A2890` / RAL 602 / CR 0 / LF 602 / loneCR 0 / git-blob `f91c153b2cf8`**（= 提交 `94bb9ca` 代；取回：`git cat-file blob f91c153b2cf8`）。

### §12-8 「语料静止」是**时点值**：G1 窗口必须**逐观测点重采**（2026-09-12 captain 指出缺口 + 我落为前置四件）

**问题**：`§12-1` 的三条前置里只写了"清场"（进程面），而 **"全仓 `porcelain` 空"是时点值** —— 台账/交付件/证据件仍在被其它成员写入（`t260`/`t271`/`t280` 等在任何时刻都可能落在途件）。把某一次的"空"当成整个窗口的"空"，会让窗口读数建立在一个**已经变化的语料面**上。

**纪律（前置四件，缺一不得开跑；且**每个观测点前重采**）**：
1. **工具面稳定**：`shell/tools/**` 的 `porcelain` 空 ∧ `worktreeSize == HEAD size` ∧ `worktree blob == HEAD blob`（`§12-7`）；
2. **语料静止（重采）**：`git status --porcelain`（**全仓**）为空 —— **在窗口开始、以及每个观测点之前**各采一次，读数必须写成 **`porcelain=空@<时刻>@HEAD=<sha>`**（只写"空"不给时刻/HEAD 即不可引）；
3. **清场**：`dotnet` = 0 ∧ `AIPlayer*`/`MpvHost*` = 0（`§12-1`）；
4. **窗口三元组**：采样时刻 + `headA`/`headB` + 工具身份（`§12-2/§12-3`）。

**实证（"静止"确实会在一分钟内失效）**：
```
2026-09-12 23:29:02.974 @HEAD 4d4ebba  ⇒ 全仓 porcelain 空（ui2 采）
2026-09-12 23:30:53.714 @HEAD 7ed1847  ⇒ 全仓 porcelain 仍空，但**111 秒内落了 3 笔提交**
                                        （4d4ebba → a688cb3 23:29:10 → c5a79b8 23:30:14 → 7ed1847 23:30:34）
⇒ 两组读数**并列就是一对"同时刻不同代"的样本**：都真，但**只有带上时刻/HEAD 才是可引的**
```
**报告形态（与 `§12-3` 同段，不另起段）**：
```
GATE-START|…|time=<t0>|head=<sha>|pid=<p>|runId=<r>
工具身份：blob=<…> size=<…> worktreeSize=<…> porcelain=<空/非空>
语料静止：porcelain=<空>@<t0>@HEAD=<sha>   （每个观测点前重采一次，逐次贴）
清场：dotnet=0 AIPlayer=0 MpvHost=0
```
**为什么必须逐点重采**：G1 的结论是"**该窗口内**（该语料代上）各判据的读数"；语料面一变，窗口内的读数就属于**另一个代**，混用即 `§9.6`（身份四元组）禁止的"跨代拼读数"。

**改动追溯（§12-8 自身）**：本条纯追加，历史与 §11/§12/§12-6/§12-7 一字未删。追加前本文件 = **76,549 B / `sha256_12 74898CDA825B` / RAL 624 / CR 0 / LF 624 / loneCR 0 / git-blob `f2a75246b3a8`**（= 提交 `c2dd471` 代；取回：`git cat-file blob f2a75246b3a8`）。

### §12-9 eol 归一路径的「worktree == HEAD」判据（2026-09-12 captain 指出 + 我实测复核；**修正 §12-7 前置① 的表达**）

**问题（实测）**：`git check-attr` 对本仓 `*.ps1` 报 **`eol: crlf` + `text: set`** ⇒ 工作区文件按属性**应为 CRLF**，而版本库里存的是 **LF** ⇒ **`worktreeSize == size` 对这类路径恒不成立**（本次表现为"前置①永远不满足"）。逐项读数（对象 = 门禁脚本，`23:36` 采）：
```
git check-attr eol text filter ⇒ eol: crlf ｜ text: set ｜ filter: unspecified
HEAD       size = 83,359 ｜ blob = 540d0bb2cb2b（LF）
worktree   bytes = 84,505 ｜ LF = 1,146 ｜ CR = 1,146 ｜ 差值 = **+1,146 = CR 数 = 行数**（纯 EOL 归一，不是内容变更）
git hash-object -- <path>（**走 clean 过滤器**）        = 540d0bb2cb2b == HEAD blob ✅
git hash-object --no-filters -- <path>（原始字节）      = 68d607152d10
porcelain = 空 ｜ git diff -- <path> = 0 行 ｜ LF 归一后 sha256_12 = C8C196DAA668 ｜ 归一化 bytes = 83,359
本仓同族面：**38 个 `*.ps1` 带 `eol: crlf`**（不是一个文件的问题）
```
**判据（eol 归一路径；三件齐即可判"worktree == HEAD"）**：
1. **`git status --porcelain -- <path>` 干净** ∨ **`git diff -- <path>` 为空**；
2. **归一化内容哈希**：`git hash-object -- <path>`（默认走 clean 过滤器）**== `HEAD:<path>` 的 blob**（等价说法：LF 归一后的内容与 HEAD 内容一致）；
3. **`git check-attr eol -- <path>` 的读数**必须随读数一起给出（`crlf` / `lf` / `unspecified`）。
⇒ **`worktreeSize == size` 只适用于无 eol 归一的路径**（`eol: unspecified` 或声明 `-text`）；**对归一路径把它当必要条件 ⇒ 造出恒红的假闸门**（本次即如此）。

**与 `§12-7`/`§12-3` 的关系（改写，不是新增并行条）**：
- `§12-7 前置①` 现读作：«**porcelain 干净 ∨ diff 空**» ∧ «**归一化 blob == HEAD blob**» ∧（**仅当 `eol: unspecified` / `-text` 时**再加 `worktreeSize == size`）；
- `§12-3` 的"工具身份三值"同此改写；`§12-3` 里"`worktreeSize ≠ size` ⇒ 读数作废"一句**只对无 eol 归一的路径生效**。

**采集命令（逐字可粘）**：
```powershell
$p = 'shell/tools/evidence-hygiene-check.ps1'
"attr=" + ((git check-attr eol text -- $p) -join ' | ')
"porcelain=[" + ((git status --porcelain -- $p) -join '') + "]  diffLines=" + (@(git diff -- $p).Count)
"normBlob=" + (git hash-object -- $p) + "  headBlob=" + (git rev-parse --verify "HEAD:$p")
"rawBytes=" + (Get-Item $p).Length + "  headSize=" + (git cat-file -s "HEAD:$p")
```

**附：工作区文件安全纪律**（来源 = captain 事实 604 §2 的自曝）：**要覆盖任何工作区文件之前，先复制到 `%TEMP%` 留档** —— 哪怕只是"行尾修复"。本次他的行尾修复覆盖掉了别人 `23:33:08` 的 10 行在途编辑（`84,289 / 711E5DDEEB06`，无副本），只能靠属主重落。

**改动追溯（§12-9 自身）**：本条纯追加，历史与 §11/§12/§12-6/§12-7/§12-8 一字未删。追加前本文件 = **79,105 B / `sha256_12 CEE91E4D850F` / RAL 652 / CR 0 / LF 652 / loneCR 0 / git-blob `a9f5d19cf8e8`**（= 提交 `526356d` 代；本路径属性 = `eol: lf` + `text: set`，故其 `worktreeSize == size` **成立**；取回：`git cat-file blob a9f5d19cf8e8`）。
