# t87 收藏屏单台上限（不再共用 `ResumeLimitPerServer=30`）—— 证据

卡：`t87`（kind=work，attempt 3，`attempt_id=1af1b559-a5ec-41fb-8ace-1fc93bf84a20`），无依赖。
范围：**收藏屏的取数上限**。文件面 = `Features/{Aggregate,Favorites}` 六个文件；**未动** `AggregatePage.xaml(.cs)`、
未改 `MediaAggregator.FavoritesItemTypes` 语义、未碰 `Services/**` 与他人领地。

## 0. 三态分开写

- **逆向到了**：无（本卡不是"复刻某个界面形态"，而是修一个**重建体自带的缺陷**）。
  参照物侧只用到一条既有判据：收藏是**完整列表**（`UI_SPEC_SHELL.md §10 P4`、`HILLSLITE_UI_ANALYSIS.md` 行 2 的
  收藏屏是"分区 + 全部海报"，没有"每台只显示 N 条"的设计）⇒ 上限只能是**安全阀**，且命中时必须可见。
- **重建实现了**：6 个源文件在盘，**最终源码**隔离构建 `EXIT=0 / 0 错误`（§2）。
- **运行验证过了**：两次**真起窗**（收藏屏 / 聚合视界各一次，真实 Emby 服务器，窗户起止时刻与 pid 见 §3），
  原始读数落 `shell/Tests/evidence/t87-*.txt`（§4）。**不是静态检查**。

## 1. 缺陷与根因（t72 实测，本卡修掉）

```
ServerA 收藏：复合查询 limit=30            ⇒ 屏上 30 条
              同一台逐类型合计 1+21+12 = 34 ⇒ 实际 34 条
⇒ 少 4 条（「收藏的剧」显示 18，实际 21）= **用户可见的静默丢失**
根因：ResumeLimitPerServer=30 是给"继续播放"那种**横向行**定的展示上限，被收藏族共用
```
判定：收藏屏要的是**完整列表**，所以
① 用**自己的分页大小**（`FavoritesPageSizePerServer=100`）**翻页取尽**；
② 保留一个**安全硬上限**（`FavoritesMaxPerServer=1000`）只为让翻页有终止条件；
③ **命中硬上限必须可见**：`Truncated` 随快照落盘 → 界面 `MoreText` 出现"还有更多：已显示 X / 共 Y 条（已达每台 1000 条安全上限）"，
   并且取数日志有一行 `favorites server=… shown=… serverTotal=… pageSize=… cap=… truncated=…`。

## 2. 逐文件改动（全部在卡面文件面内）

| 文件 | 改了什么 / 为什么 |
| --- | --- |
| `Features/Aggregate/MediaAggregator.cs` | 新增 `FavoritesPageSizePerServer=100` / `FavoritesMaxPerServer=1000`；`Favorites` 分支改为**翻页取尽**（`startIndex=items.Count`，直到"服务端说没有了 / 不足一页 / 撞上硬上限"）；`MediaGroupResult` 增 `TotalCount`/`Truncated`；`MediaAggregate` 增 `ServerTotalItems`/`TruncatedGroups`/`AnyTruncated`；新增一行取数诊断 |
| `Features/Aggregate/MediaSnapshotSource.cs` | `SnapshotGroup` 增 `TotalCount`/`Truncated`；`MediaSnapshot` 增 `TruncatedServers`（随信封落盘 ⇒ 缓存态与刷新态同一观感）；新增 `TruncationNotice()` = 「还有更多」文案的**单点构造** |
| `Features/Aggregate/T31SelfTest.cs` | 逐类型探针重写（**每类具名**，防"空集合让合计恒真"）；加 **t87 差额反控**（旧口径 vs 本屏口径）；加 **「还有更多」可见性反控**（合成快照，A/B 两组） |
| `Features/Aggregate/AggregateDiagnostics.cs` | 新增 `SHELL_EVIDENCE_NAME`（新卡的读数落**新文件名**：覆盖已入库文件名会让身份 sha12 对不上，而"同一次运行不得有两个文件名"又不允许事后改名拷贝）；`CardLogPath` 对自定义名派生 `…-cardlog.txt`（默认命名逐字不变） |
| `Features/Favorites/FavoritesPage.xaml` | 摘要区新增 `MoreText`（默认 `Collapsed`；只有"屏上不是全部"时才可见） |
| `Features/Favorites/FavoritesPage.xaml.cs` | `UpdateMoreText()` 挂在**两条**渲染路径（T0 缓存渲染 / T1 就地替换、含 T1-noop 的只改文字路径）；只读访问点 `MoreTextValue`；自检渲染入口 `RenderSnapshotForSelfTest()` |

> 同轮另一件事（**不属于本卡**、已单独报 captain）：`KernelHost/KernelLauncher.cs` + `KernelLogMasker.cs` 的 **H6 合规整改**
> （退出打码自检原先按进程名批量 `Kill` 内核 ⇒ 改为只停本进程 `Process.Start` 返回的那个对象）。

## 3. 构建与运行读数

### 3.1 构建（隔离输出，全量重建）

```
命令 : dotnet build shell/App/AIPlayer.Shell.csproj -c Debug -t:Rebuild -p:OutputPath=E:\ui2-bin\
时刻 : 2026-09-12 05:38:40 结束（39.96 s）｜HEAD=9b886b9
读数 : EXIT=0 ｜ 0 个错误 ｜ 1172 个警告（警告全在 reversed/）
在场 : 构建前 dotnet.exe / XamlCompiler = 0（当轮第一次尝试撞上**他人并发构建** ⇒ XamlCompiler 写出 0 字节
       .g.cs ⇒ MSB3073；等安静窗口后重跑即绿，**未杀任何进程**）
产物 : E:\ui2-bin\AIPlayer.Shell.dll = 1,641,984 B / sha12 B6AEF6B5BD8E / mtime 05:38:33.220
前提 : 产物 mtime ≥ 全部相关源码 mtime（最新源码 T31SelfTest.cs 05:37:17.345）
```

### 3.2 两次真起窗

```
① 收藏屏（本卡主读数）
   START pid=13000 @ 2026-09-12 05:38:55 → STOP @ 05:43:22（我自己的实例，收尾已关；残留 0）
   环境  : SHELL_START_PAGE=favorites / SHELL_SELFTEST_FAVORITES=1
           SHELL_EVIDENCE_DIR=E:\AI Player\shell\Tests\evidence / SHELL_EVIDENCE_NAME=t87-favorites-limit.txt
   证据  : shell/Tests/evidence/t87-favorites-limit.txt = 10,399 B / 122 行 / sha12 F643420BADE3 / mtime 05:42:51.574
           shell/Tests/evidence/t87-favorites-limit-cardlog.txt = 7,966 B / 69 行 / sha12 76C7E698B6A1

② 聚合视界（回归：同一取数面被"收藏过滤器"复用）
   START pid=14892 @ 2026-09-12 05:45:15 → STOP @ 05:55:21（收尾已关）
   环境  : SHELL_START_PAGE=aggregate / SHELL_SELFTEST_AGGREGATE=1 / SHELL_EVIDENCE_NAME=t87-aggregate-regression.txt
   证据  : shell/Tests/evidence/t87-aggregate-regression.txt = 14,898 B / 100 行 / sha12 292C293E3213 / mtime 05:47:57.512
           shell/Tests/evidence/t87-aggregate-regression-cardlog.txt = 5,084 B / 47 行 / sha12 99D982F63BDF
```

## 4. 关键原始读数（逐行摘自上面的证据文件）

### 4.1 屏上不再少条目（本卡主判据）

```
④ 收藏卡片数   = 34
⑤ 摘要行原文   = 收藏：1 台有内容 · 共 34 条（电影 1 / 剧 21 / 集 12） · T0=hit（Stale）
  · [收藏的电影] 卡片=1
  · [收藏的剧]   卡片=21      ← 修前为 18
  · [收藏的集]   卡片=12
  断言 三个分区张数之和 = 卡片总数 = True（34 vs 34）
本屏取数 Movie,Series,Episode ServerA shown=34 serverTotal=34 truncated=False pageSize=100 cap=1000 类型[Episode=12,Movie=1,Series=21]
```

### 4.2 差额反控（两条读数同文件前后相邻）

```
主源 ServerA 逐类型：Movie=1 Series=21 Episode=12  合计=34
屏上分区：电影=1 剧=21 集=12  合计=34
反控A 旧口径（复合查询 limit=30）= 30　差额 = 4　（>0 ⇒ 旧口径确实**静默丢条目**）
反控B 本屏口径（翻页取尽，屏上实有）= 34　差额 = 0　⇒　断言 差额为 0 = True
断言 逐类型与三个分区逐项相等        = True
```
⇒ 反控 A 用**旧口径**在真机上复现了 4 条丢失（这是"这条断言可失败"的证明），反控 B 用**本屏口径**差额为 0。
`逐类型` 每台服务器各自一行、每类各自具名（`Movie=` / `Series=` / `Episode=`），不用"合计"代替逐类。

### 4.3 「还有更多」可见性反控（合成快照，可失败）

```
控制组A（TotalCount=42 / 屏上 2 条）提示原文 = 还有更多：已显示 2 / 共 42 条（已达每台 1000 条安全上限）
断言A 提示出现且含总数 42 = True
控制组B（TotalCount=2  / 屏上 2 条）提示原文 = <none>
断言B 完整时**不显示**提示 = True
还原后 卡片数=34 提示=<none>
更多提示（t87 可见性观察点）        = <none>（屏上就是全部）
```
⇒ 真机 ServerA 只有 34 条、够不着硬上限，所以"提示恒不显示"也能看起来对；A/B 两组把这条绑定钉成**可失败**。

### 4.4 硬上限真的会被命中（不是死代码）

```
（t87-aggregate-regression-cardlog.txt）
05:44:34.052  favorites server=ServerM shown=1000 serverTotal=44945 pageSize=100 cap=1000 truncated=True
05:44:57.460  favorites server=ServerA shown=34    serverTotal=34    pageSize=100 cap=1000 truncated=False
```
⇒ 真实服务器 `ServerM` 收藏 **44,945 条**：屏上取 1000 条后**明确标记 `truncated=True`**，
界面会出现"还有更多：已显示 1000 / 共 44945 条（已达每台 1000 条安全上限）"（不是静默截断）。

### 4.5 回归：聚合视界（同一取数面）

```
--- 过滤器：Favorites ---
② 有内容的台数 = 5 ｜ ③ 失败的台数 = 7 ｜ ④ 条目总数 = 1049 ｜ ⑤ 耗时 = 39748 ms
  · [ServerA] items=34   ← 修前 30（同一口径源的连带效果，两屏口径同源是本文件的既有纪律）
  · [ServerB]   items=10   · [步兵营地] items=2 · [ServerK] items=3 · [ServerM] items=1000
  断言 分组数>0 / 摘要含具名数字与 T0 状态 / 每张卡都有占位或封面 = 全 True
--- 过滤器：ContinueWatching --- ⑤ 耗时 39781 ms，8 台有内容、共 72 条，断言全 True
--- 过滤器：Library ---          ⑤ 耗时 39601 ms，9 台有内容、共 471 条，断言全 True
--- SEAM③/T1 就地替换（聚合视界）--- 断言 T0 分组行实例未被换 = True，首张卡片实例未被换 = True
```
⇒ 三个过滤器 + T0/T1 就地替换全绿：改的是取数上限，**没有**破坏分组/进度条/占位/失败源。

## 5. 已知边界与残留（如实写，不当成"已完成"）

1. **`ServerM` 这类源（44,945 条）屏上只显示 1000 条** —— 按卡面口径这是**可见**上限（读数 + 界面提示都在），
   若要做到"取尽 / 下拉加载更多"，需要**独立卡**（性能：一次渲染 44k 张海报不现实）。
2. **聚合视界没有可见提示**：`AggregatePage` 按卡面明令**未动** ⇒ 在那里是"**读数可见、界面无提示**"
   （`favorites … truncated=True` 在 cardlog 里）。若要界面提示，属 `t34` 视觉收口面，请另立卡。
3. **自检探针的上限**：逐类型探针用 `limit=FavoritesMaxPerServer=1000` ⇒ `ServerM` 的 `1000/1000/1000`
   是**探针上限命中**（真值未测，**INCONCLUSIVE**），不得读成"该源恰好 1000 条"。
4. **`ServerF～(∠・ω< )⌒☆` / `ServerD` ×2 / `ServerH` / `server-hash-id` / `ServerJ` / `ServerL`**
   在逐类型探针里是 `ShellHttpException`（不可达/凭据），与本卡无关，也不是本卡引入。

## 6. 证据清单（待 captain 入库）

| 文件 | 身份 |
| --- | --- |
| `shell/Tests/evidence/t87-favorites-limit.txt` | 10,399 B / 122 行 / sha12 `F643420BADE3` |
| `shell/Tests/evidence/t87-favorites-limit-cardlog.txt` | 7,966 B / 69 行 / sha12 `76C7E698B6A1` |
| `shell/Tests/evidence/t87-aggregate-regression.txt` | 14,898 B / 100 行 / sha12 `292C293E3213` |
| `shell/Tests/evidence/t87-aggregate-regression-cardlog.txt` | 5,084 B / 47 行 / sha12 `99D982F63BDF` |

🔴 本文（`EVIDENCE_T87.md`）**引用了**上面这四个文件 ⇒ 入库时必须**同一批 `git add`**：
只提交本文而漏掉证据，门禁 `H2a/H2b` 会当场转红（citied-but-untracked 是本队踩过的坑）。
本文档与四个证据文件均为**未入库**状态，等 captain 处理（成员不自作 `git add` 他人面；本卡证据由 captain 一并入库）。
