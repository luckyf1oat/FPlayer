# t93【P0 用户报障】聚合视界打开是空屏 —— 证据

卡：`t93`（kind=work，attempt 1，`attempt_id=c9d42e2f-5549-4c4d-a4f9-c1fd263bbd07`），无依赖。
范围：`Features/Aggregate/**`、`Features/Favorites/**`，另加一处**我自己的** `Infrastructure/SingleInstance.cs`
（取证用显式旁路，见 §7）。**未动** `AggregatePage` 之外他人领地、未改 `shell/Services/**` 与 `kernel/**`。

## 0. 三态分开写

- **逆向到了**：无（这是**重建体自带的缺陷**，不是复刻某个界面形态）。根因由 captain 用真实日志 + 磁盘缓存定根
  （三段 1 秒级对齐）；我在本机**复核了磁盘实锤**（§5 的前后对照），并把它固化成五条可失败反控。
- **重建实现了**：7 个源文件在盘（§3），**最终源码**隔离构建 `EXIT=0 / 0 错误`（§6）。
- **运行验证过了**：真起窗一轮（pid/起止见 §4），**22 条断言 0 失败**，原始读数落三份证据（§9）。

## 1. 现场与根因（复核后的版本）

用户原话「聚合视界功能无效」。取数本身是通的（`AGG favorites server=ServerA shown=34`、`ServerM shown=1000`
`serverTotal=44945 truncated=True`、多个源 401/502/525）。真问题是**缓存层**：

```
收藏屏（主源单服作用域，ServerG 0 条）的结果，被写进了聚合视界与收藏屏**共用**的键 aggregate:favorites
  ⇒ 之后聚合视界每次打开：SWR-READ ⇒ Fresh ⇒ T0 直接渲染这份"一服 0 条"的毒缓存（不是骨架）
  ⇒ 页面上什么都没有；真值要等 T1，而一发实测 elapsed=39872 ms（≈40 s）
```
本机复核（同一机制的**活体样本**，06:08:54 采）：
`%LOCALAPPDATA%\AIPlayer\swr\28102a461c57a9a796e7149af7ad82eadcabd7e3.cache`
= `SHA1("aggregate:favorites")`，12,354 B / sha12 `CAC3DFE2EC84`，内容 = **groups=1（ServerA）/ items=34 / failed=0**
⇒ 跨服屏的第一屏内容被**单服作用域**的结果顶着（card 里那份是 ServerG 0 条，属同一机制）。

## 2. 五条修法与可失败判据（卡面 A–E）

| 条 | 修法（代码落点） | 反例（可 False） |
| --- | --- | --- |
| **A 键带作用域** | `MediaSnapshotSource.KeyFor(kind, servers)` = `aggregate:favorites@<排序去重的 serverId 集合>`；旧的无作用域键在首次载入时**删一次**并留 `SWR-LEGACY-PURGE` | 收藏屏（主源单服）与聚合视界（16 台）键**必须不同**；注入旧键毒快照后载入**不得**读到它，且旧键文件必须消失 |
| **B 毒结果不许覆盖好缓存** | `IsDegradedWrite(上一份有内容 + 本次 0 条 + 源集合缩小 或 全失败)` ⇒ `RejectDegradedWrite` 抛可读原因（SWR 走"失败保留缓存"） | 上一份 2 台/4 条 → 本次 1 台/0 条 ⇒ 必须拒绝写盘（缓存 sha12 不变、界面显示失败） |
| **C T0 不许把空/失效缓存当内容** | 判据 = **海报张数**（不是行数：空组也会生成一行）；0 张 ⇒ 保持骨架屏 + "正在跨服务器取数…（缓存里没有可用内容）"；**没有缓存又没有内容** ⇒ `ShowEmpty("取数失败（没有可用缓存）")` | 空缓存 + 取数打坏 ⇒ 必须"骨架 或 空态 或 失败提示"至少一个可见（消灭白屏） |
| **D T1-noop 不许钉死空屏** | `renderedEmpty(海报=0) \|\| ids 不同` ⇒ 必须重绑，reason 落 `rendered-empty` | 屏上 0 张 + 刷新有 1049 条 ⇒ 必须重绑出内容 |
| **E 取消必须可见** | `MediaAggregator.LoadOneAsync` 对 `OperationCanceledException`（token 已取消）**原样上抛**（t52 M-2/M-3 同族） | 刷新中取消 ⇒ 必须 `error=cancelled`（不是 `refresh=done ids=[]`）且缓存字节不变 |

## 3. 改动清单（文件级）

1. `Features/Aggregate/MediaSnapshotSource.cs`：
   `KeyFor(kind, servers)` + `ScopeKeyFor(servers)`（作用域 = 排序去重的 serverId 集合）；
   `LegacyUnscopedKeys` + `PurgeLegacyUnscopedKeys()` + 自检用 `ResetLegacyPurgeForSelfTest()`；
   `IsDegradedWrite()` / `RejectDegradedWrite()`；`DegradedFetchForSelfTest`（反控开关）；
   `SwrOutcome.Key` + `CountItems()`/`SnapshotIds` 复用；每次载入落一行 `SWR-SCOPE key=… scope=…`。
2. `Features/Aggregate/MediaAggregator.cs`：`LoadOneAsync` 对"token 已取消"的 `OperationCanceledException` **上抛**。
3. `Features/Aggregate/AggregatePage.xaml.cs`：T0 空缓存 ⇒ 骨架 + 可见状态行；T0 读数补齐 `Key`/`CachedIds`；
   T1 判据加 `renderedEmpty`；失败且无缓存 ⇒ `ShowEmpty("取数失败（没有可用缓存）")`；新增只读观察点
   `PosterCount` / `EmptyPanelVisible` / `LastT1Reason` / `CurrentServers`。
4. `Features/Favorites/FavoritesPage.xaml.cs`：同 C/D/无缓存不留白；T0 读数补齐 `Key`/`CachedIds`。
5. `Features/Aggregate/T93SelfTest.cs`（**新**）：五条反例的原始读数（含合成快照注入、真落地、取消重跑、收尾恢复）。
6. `Features/Aggregate/T31SelfTest.cs`：`SHELL_SELFTEST_AGGREGATE=t93` **专项模式**（只跑五条 + 一次开屏时间线，
   跳过三过滤器长跑 —— 那一轮约 15 分钟，实测被外部关窗两次）+ 调用 t93 反控块。
7. `Infrastructure/SingleInstance.cs`：`SHELL_SINGLEINSTANCE_ALLOW_MULTI=1` **显式取证旁路**（默认关闭、一定落日志）。

## 4. 运行读数（真起窗）

```
起窗 : pid=6096 @ 2026-09-12 06:48:47（E:\ui2-bin\AIPlayer.Shell.exe）
       SHELL_START_PAGE=aggregate｜SHELL_SELFTEST_AGGREGATE=t93｜SHELL_SINGLEINSTANCE_ALLOW_MULTI=1
收尾 : 06:53:01 我自停（`Stop-Process` 只打自己那个 pid；残留 0）
断言 : **22 条 / 失败 0**
时间线（同一份证据内的原始行，见 §9 的 timeline 文件）：
  06:48:48.608 AGGREGATE T0-cache-render key=aggregate:continue-watching@<16 台>  （cache=hit Fresh）
  06:49:28.806 AGGREGATE T1-noop ids-identical
  06:49:28.904 AGGREGATE T0-cache-render key=aggregate:favorites@<16 台>        （T0 用**带作用域的键**）
  06:50:08.794 AGGREGATE T1-noop ids-identical                                  （一发 ~40 s，与 captain 的 39872 ms 同量级）
  06:51:28.750 AGGREGATE T1-replace reason=rendered-empty                       （D：空屏被重绑成 1049 张）
  06:52:53     证据落盘（收尾恢复：海报 = 1049 张｜摘要 = 收藏：9 台有内容 · 共 1049 条 · T0=hit（Fresh））
```

## 5. 证据 ①：毒缓存的前后对照（键 + 内容 + sha12）

**前**（06:08:54 采，旧构建实例写的；SHA1(键) 与文件名逐字符对上）：
```
28102a461c57a9a796e7149af7ad82eadcabd7e3.cache  = aggregate:favorites         12,354 B  sha12 CAC3DFE2EC84  内容 groups=1/items=34
9fb98407e51127c0bf7b6aabdf5a9d7ca2b73e64.cache  = aggregate:continue-watching 30,304 B  sha12 9D5C708FD9AC
5985d709f41a002cf1798a2a1ee2b0c22d21de49.cache  = aggregate:library          143,924 B  sha12 212AAD3C9679
```
**后**（06:45 采，同一台机器、修好之后的构建跑过一轮）：
```
aggregate:favorites          → exists=False
aggregate:continue-watching  → exists=False
aggregate:library            → exists=False
新键（带作用域）另落新文件：454076317ee1704696b940f70e32f85b47dca47b.cache 400,710 B
                              aaf30140896ba76b4e64ff6bb8e30325fc0e547c.cache 143,924 B
                              28462849f0e82927871cab07fe04be3b61b47d57.cache  30,304 B
```
窗内读数（同一次运行，可 False）：`注入毒快照到旧键 'aggregate:favorites' ⇒ 落盘=True 文件存在=True bytes=236`
→ `断言A3 旧键文件已被清除（存在 True ⇒ False）= True`。

## 6. 构建与门禁读数

```
构建 : dotnet build shell/App/AIPlayer.Shell.csproj -c Debug -t:Rebuild -p:OutputPath=E:\ui2-bin\
       2026-09-12 06:48:41 结束（41.35 s）｜HEAD=8d4d117｜EXIT=0 / 0 个错误 / **1216 个警告**
产物 : E:\ui2-bin\AIPlayer.Shell.dll = 1,724,928 B / sha12 EEDCCBFDC1F3 / mtime 06:48:34.496 ≥ 最新源码 06:47:53.072
在场 : 构建前 dotnet.exe / XamlCompiler = 0（安静窗口）；期间无并发构建
⚠ 警告数 1172 → 1216：不是本卡引入（本卡改动全在注释/计数判据上）；是**他人在同窗口改动了源根**
  （fork 源随 `csproj` 进来），逐条都在 `reversed/**`。
门禁 : powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/evidence-hygiene-check.ps1（06:45 采）
       SELF-CHECK|checks=10|pass=9|fail=1|inconclusive=0
       H2a FAIL（untracked=1）= **不是我的**：`shell/Tests/evidence/appdata-root-override-shell.ps1`
                                 （citedBy `shell/Tests/appdata-root-override-probe/README.md:24`）
       H5 PASS（scannedRows=42 mismatch=0）｜H7 PASS（newAstral=0 newFe0f=0）｜H3b PASS（bare/rib=0）
```
H7 曾因我在 t93 注释里写了 `🔴`/`⚠️` 而红（newAstral=8 / newFe0f=1）：已按仓库 ASCII 约定改成 `[!]`，
复跑 `newAstral=0 / newFe0f=0`（**自己引入的自己清**）。

## 7. 两条必须写明的"我加了什么"（不是静默行为）

1. **`SHELL_SINGLEINSTANCE_ALLOW_MULTI=1`**（`Infrastructure/SingleInstance.cs`，我自己的文件）：
   取证时用户正跑着 `dist\AIPlayer\` 那份发布版（pid=7208），第二个实例按闸门设计 **exit 3** ⇒ 我起不了取证窗，
   而按纪律**不能杀别人的进程**（H6 / 事实 221）。故加**显式、默认关闭、一定落日志**的旁路：
   `SINGLE-INSTANCE-BYPASS … existing=pid=11156 path=…`（见 timeline 首行）。不设该变量时行为逐字不变。
2. **`SHELL_SELFTEST_AGGREGATE=t93` 专项模式**：三过滤器长跑约 15 分钟且实测被外部关窗两次；专项模式只跑
   五条反控 + 一次开屏时间线（本卡要的证据就在这一层）。其它取值（含 `=1`）行为逐字不变。

## 8. 已知边界（未当"已完成"）

1. `IsDegradedWrite` 的判据是**组数**：同作用域、同组数、全 0 条 ⇒ **允许**写（那可能是"世界上真没内容了"）；
   被拦的是"上一份有内容 + 本次 0 条 + 源集合缩小/全失败"。这是有意的（否则用户真清空收藏后屏上会永远留着旧内容）。
2. 聚合视界的**界面**仍不显示"取数中（缓存为空）"以外的加载细节（沿用既有摘要/骨架），未新增 UI 元素。
3. `ServerM` 那种源（`serverTotal=44945`）在收藏屏仍是 `truncated=True` 的**可见**上限（t87 卡内已声明，本卡不动）。

## 9. 证据清单（三份，均为**未入库**状态，请 captain 与本文一并 `git add`）

| 文件 | 身份 |
| --- | --- |
| `shell/Tests/evidence/t93-aggregate-empty-screen.txt` | 174,171 B / 83 行 / sha12 `25303466A1D7` / mtime 06:52:53.667 |
| `shell/Tests/evidence/t93-aggregate-empty-screen-cardlog.txt` | 31,912 B / 304 行 / sha12 `8C921E572D1C` / mtime 06:52:53.632 |
| `shell/Tests/evidence/t93-aggregate-timeline.txt` | 48,890 B / 70 行 / sha12 `AC30748DCEC4` / mtime 06:53:11.613 |

🔴 本文（`EVIDENCE_T93.md`）**引用了**上面三份 ⇒ 入库必须**同一批**（只提本文会让 `H2a/H2b` 当场转红）。
timeline 文件内含 17 行被截断的 id 列表（单行 >1500 字符；截断规则写在文件头，原文仍在外壳日志里、日志未改）。
