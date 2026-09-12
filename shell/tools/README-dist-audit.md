# `dist-audit.ps1` —— v4 发布面核对工装

一条命令给定 `dist` 树，输出「四对象点名 + 三计数 + fork/诱饵判别 + 零字节生成件检查」，
末行恰一行 `SUMMARY|verdict=`，退出码 0/1。

**只读**：本工装不在 `dist` 树内写任何文件；反控夹具写在 `%TEMP%`。

---

## 1. 用法

```powershell
# 推荐：先现采再传（换发布面自动跟随本工具，不必改本文）
$Dist = "E:\AI Player\dist\AIPlayer"
$fork = (Get-FileHash "$Dist\player\AIPlayer.MpvHost.dll" -Algorithm SHA256).Hash.Substring(0,12)
powershell -NoProfile -ExecutionPolicy Bypass -File shell/tools/dist-audit.ps1 `
    -Dist $Dist -KernelForkSha12 $fork -OriginalStockSha12 8D73526C2C07

# 字面样例（**限时真值**，仅供对照；取值来源 = `dist\AIPlayer\player\AIPlayer.MpvHost.dll`，
#          采样时刻 = 2026-09-12 18:2x，**换发布面即失效** —— 请优先用上面的现采式）
powershell -NoProfile -ExecutionPolicy Bypass -File shell/tools/dist-audit.ps1 `
    -Dist "E:\AI Player\dist\AIPlayer" -KernelForkSha12 29469DC45F1D -OriginalStockSha12 8D73526C2C07

# 内核阶段树（无外壳件，fork 就在顶层）；同样建议现采后再传
powershell -NoProfile -ExecutionPolicy Bypass -File shell/tools/dist-audit.ps1 `
    -Dist E:\fork-player-stage-t130 -KernelForkSha12 29469DC45F1D

# 自证：本文件纯 ASCII + Parser 零错误
powershell -NoProfile -ExecutionPolicy Bypass -File shell/tools/dist-audit.ps1 -SelfCheck
```

| 参数 | 必填 | 缺省 | 含义 |
|---|---|---|---|
| `-Dist` | 是 | — | 被核对的 `dist`（或内核阶段树）根 |
| `-KernelForkSha12` | 是 | — | **期望的** fork 内核件 sha256 前 12 位；传错即 FAIL（反控①） |
| `-OriginalStockSha12` | 否 | 空 | 原版内核件 sha12；填了才能把顶层同名件的角色判成 `original-stock` 而不是 `not-expected-fork` |
| `-ProjectObjDir` | 否 | 本仓 `shell/App/obj` | **纳入门禁**的工程 obj 目录（可多个）；零字节 `*.g.cs` 逐个列路径 |
| `-SourceAssetsDir` | 否 | 本仓 `shell/App/Assets` | 资产对齐比对的源树 |
| `-MaxZeroByteGcs` | 否 | `0` | 零字节生成件预算 |
| `-SettleMs` | 否 | `2000` | 生成件面两次采样之间的间隔 |
| `-SelfCheck` | 否 | — | 只做自身 ASCII/解析自证后退出 |

**为什么 sha12 要传入而不是写死**：**样例值随代际替换（当前 `29469DC45F1D`）** —— 上面两条命令里的 sha12 只是"这一版的样例值"，
换代后必须换成当刻实测值（写死等于每次换代都静默失去判据，照抄旧样例还会得到「fork 缺失」的假警报）。
> 代际沿革（供对照，不要照抄旧值）：`42C92A60DE67` → `CF0BFC9145C5` → `B3EB8C9DBBA5` → **`29469DC45F1D`**（当刻）；原版诱饵恒为 `8D73526C2C07`。

**「当前代际」的口径（引用时照此写）**：**当前代际 = 当刻发布面上现采的值**（发布面 = `dist\AIPlayer\player\` 或内核阶段树）；
**构建输出、暂存面都不是「当前」**（例如内核侧刚构建出的新 dll 在发布前不算「当前」）。凡声称「当前」，
必须同时给**路径 + 值 + 采样时刻** —— 否则「当前」是个事后无法复核的断言。

**证据书写纪律（同族事故，别重犯）**：给证据件写"我改过哪些工装件"时，**不要**把身份写成
「字节数 + 十二位哈希」同行的表格行 —— 门禁 `H5` 会把这种行配对成身份行并因数字格错位报 FAIL（本工具作者
在 `t195` 里踩过：判 FAIL 后改成"引身份就给提交号"的写法才转绿）。要引身份就**给提交号**或按门禁口径另起一节。

---

## 2. 输出行前缀

| 前缀 | 含义 |
|---|---|
| `DIST-AUDIT` | 采样头：被测根、期望 sha12、原版 sha12、settle、预算 |
| `SHAPE` | 树的形状：`release`（有 `AIPlayer.Shell.exe`）/ `kernel-stage`（只有内核 exe）/ `unknown` |
| `OBJECT` | 四对象逐条：`bytes` + `sha12` + `mtime`（内核件按解析结果给 `role=kernel-fork`） |
| `KERNEL-FORK` | fork 解析结果：`resolved` / `source=player\|top` / `expected` / `actual` / `match` |
| `DECOY-STANDING` | 顶层同名件的**地位**：`countsAsPass=False`、`credit=sha12-match-only` |
| `DECOY` | 顶层同名件不是期望 fork 时，单独一行标注「诱饵，不得被加载」 |
| `TOPOLOGY` | 三计数**分别**打印：`topFiles` / `recursiveFiles` / `playerFiles`（另给 `newestDistMtime`） |
| `KERNEL-TRIO` | 内核件齐备性逐项：`dll` / `native`（`libmpv-2.dll`）/ `pri`；缺哪项写哪项 |
| `BUILD-INFLIGHT` | 采样当刻是否**有构建在飞**（`XamlCompiler`/`MSBuild`/`dotnet`），带 pid |
| `BUILD-HYGIENE` | 生成件面：两次采样各自时刻与计数、`stable=`、`newestGcsMtime=`、本面结论 |
| `XAML-SAVE-STATE` | `XamlSaveStateFile.xml` 是否在盘 + 字节 + mtime（**不判红**，见 §5） |
| `NOTE\|hygiene-elsewhere` | 兄弟工程 obj 也有零字节件时的**披露**（不判红，附把它纳入门禁的原句） |
| `ASSETS` / `ASSETS-MISSING` | 资产静态对齐：dist 与源树逐件比字节；`iconMissing=n/a` 表示该形状不适用 |
| `SUMMARY` | 恰一行：`verdict` + 各计数 + `fail=` + `inconclusive=` + `reasons=` |

---

## 3. 判据表

| 面 | PASS | FAIL | INCONCLUSIVE |
|---|---|---|---|
| fork 解析 | 解析到的件 `sha12 == -KernelForkSha12` | 盘上无同名件，或解析到的件 sha12 不匹配（`reasons=fork-missing`） | — |
| 四对象 | `release` 三大件齐备且非零字节 | `release` 树缺任一外壳件，或任一为 0 字节 | 非 `release` 形状 ⇒ 该组标 `not-applicable` |
| 内核件齐备 | `player\`（或顶层）三件齐 | 缺任一项（`reasons=kernel-trio-incomplete`） | — |
| 生成件面 | **无构建在飞** 且两次采样一致 且零字节数 ≤ 预算 | 无构建在飞 且两次采样一致 且零字节数 > 预算（逐条列路径） | 有构建在飞，或两次采样不一致 |
| 资产对齐 | `release` 形状下源树逐件在 dist 中且字节相等 | `release` 形状下有缺件/尺寸不符（`reasons=icon-missing-in-dist`） | 非 `release` 形状 ⇒ 计数打 `n/a` |
| 形状 | — | `unknown`（既无外壳 exe 也无内核 exe） | — |

`SUMMARY|verdict=PASS` 要求 `fail=0` **且** `inconclusive=0`；`fail=0|inconclusive=1` 读作「没有证明有坏件，但生成件面读不到」，退出码仍为 1 —— 不把「读不到」折叠成绿。

---

## 4. 三臂读数（本节数字均为实测；命令原句记在 `Tests` 侧本卡证据件 `t167-dist-audit.txt`，按仓库既有纪律不写成引用形状）

| 臂 | 对象 | 结果 | 关键读数 |
|---|---|---|---|
| 正控 A | `E:\fork-player-stage-t130`（内核阶段树），`-KernelForkSha12 42C92A60DE67` | **PASS / exit 0** | `shape=kernel-stage`；fork `source=top` `bytes=1020416` `sha12=42C92A60DE67` `match=True`；`topFiles=376 recursiveFiles=419 playerFiles=0`；三件齐 `missing=0`；`zeroByteGcs=0`；`iconMissing=n/a` |
| 正控 B | `E:\AI Player\dist\AIPlayer`（发布树），同 sha12 | **PASS / exit 0** | `shape=release`；fork `source=player` `match=True`；顶层 `sha12=8D73526C2C07 bytes=979456` ⇒ `role=original-stock` + 单独一行「诱饵，不得被加载」；`topFiles=377 recursiveFiles=989 playerFiles=419`；`iconMissing=0` |
| 反控① | 同正控 A，但 `-KernelForkSha12 CF0BFC9145C5`（错值） | **FAIL / exit 1** | `match=False` `expected=CF0BFC9145C5 actual=42C92A60DE67 reason=fork 缺失`，且指名对象路径 |
| 反控② | `%TEMP%\t167-decoy-only`：顶层放**原版** dll + `player\` 为空 | **FAIL / exit 1** | `reasons=unknown-tree-shape;fork-missing;kernel-trio-incomplete`，`fork 缺失` 点名顶层件 `actual=8D73526C2C07` |
| 自证 | `-SelfCheck` | **PASS / exit 0** | `nonAscii=0 parseErrors=0` |

反控② 的正控对照就是正控 B：**同一个顶层诱饵在场**，只因 `player\` 内多了一份匹配 sha12 的 fork，就从 FAIL 翻成 PASS ⇒ 红不是"临时目录不可审"，而是"fork 缺失"。

---

## 5. 与卡面的三处显式偏离（都写在明面上，请按需否决）

1. **形状识别 + 非 `release` 形状不判外壳件缺**：卡面的 verify 第一条指的对象 `E:\fork-player-stage-t130` 是**内核阶段树**，实测 `AIPlayer.Shell.exe/.dll/.Services.dll` 三件全 ABSENT（该树只含内核玩家件）。若按"缺件即红"处理，卡面第一条 verify 永远红。故本工装先判形状，内核阶段树上把外壳组标 `not-applicable` 并**逐条打印**，`unknown` 形状仍判红。
2. **生成件面在"有构建在飞"时给 INCONCLUSIVE 而不是 FAIL**：有了实测（§6），一次采样会把别人的在飞构建读成红。零字节预算**没有放松**：无构建在飞且两次采样一致时，超预算即 FAIL。
3. **`XamlSaveStateFile.xml` 只打印不判红**：现有 runbook 的动作是"删掉它再重建"，所以它在盘不是缺陷；判红会造出常驻假红。零字节 `*.g.cs` 才是那条 MSB3073 的真信号，它仍判红。

---

## 6. 实测：生成件面在构建期会闪断（这条规则的经验依据）

`shell/App/obj/**/*.g.cs`，同一目录、相隔约 40 秒的三次采样（命令原句见证据 §7）：

| 采样时刻 | 方法 | `gcsTotal` | `zeroByteGcs` | 在飞构建 |
|---|---|---|---|---|
| 17:26:51.694（工具首跑） | `Get-ChildItem` 单次 | 34 | **16** | `dotnet:9804` + `XamlCompiler:22060` |
| 17:27:32.810 | 方法 A：`Get-ChildItem -Filter` 单次 | 34 | **0** | 无 |
| 17:27:32.810 | 方法 B：`[IO.Directory]::GetFiles` + `FileInfo` | 34 | **0** | 无 |

⇒ 17:26:51 那次是**在飞构建把 15 个 `*.g.cs` 截成 0 字节**（mtime 聚在 17:20:01.595–.711，`XamlTypeInfo.g.cs` 17:25:59.150）时的读数，属于**转瞬假红**；两次独立方法的稳定读数都是 0。这条与 WORKSPACE.md 记的 MSB3073 现场（并发 WinUI3 构建互踩 `*.g.cs`）同源，故工装把"在飞"与"不稳定"两种情形显式分出来。

---

## 7. 已知限制

1. **静态资产计数 ≠ 运行期 `SERVERS icon-missing` 行数**：本工装比的是"源树声明的资产是否逐件在 `dist` 里且字节相等"；运行期那个计数需要**真起窗 + 左栏铺出服务器**才有意义（空沙箱里的 0 是空读数）。两者名似而物不同，故输出里逐条注 `note=`。
2. **兄弟工程 obj 不在门禁内**：实测 `shell/Spike/obj` 有 **3** 个零字节 `*.g.cs`（`App.g.cs`/`MainWindow.g.cs`/`XamlTypeInfo.g.cs`）。Spike 不在发布面上，故只披露不判红，并把纳入门禁的原句一并打印（`to-gate=-ProjectObjDir <path>`）。
3. **不检查 `dist` 树内容的语义正确性**：版本串、依赖闭包完整性、`assets` 之外的资源都不在本工装判据内。
4. **`player\` 存在但为空**会被判 `fork-missing` + `kernel-trio-incomplete`；**顶层即是 fork**的内核阶段树不会被误标诱饵（顶层件 sha12 等于期望值即 `role=TOP-IS-FORK`）。
5. 工装不结束任何进程、不做截图、不改 `dist`；`BUILD-INFLIGHT` 只读进程列表。
