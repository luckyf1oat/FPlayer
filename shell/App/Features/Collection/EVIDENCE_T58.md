# t58 合集屏（U-L 分片二：Emby 合集 / BoxSet）—— 证据

> 归属：`ui2`。文件领地：`shell/App/Features/Collection/**`。取数一律经既有 `EmbyService`（`GetItemsAsync(parentId: …)`），
> 缓存复用**同一个** `MediaSnapshotSource` 的 SWR 入口（`t54` 口径），**未新建第二条取数通道**。

## 0. 三态（**不许混说**）

| 态 | 内容 |
|---|---|
| **逆向到了** | 海报几何（`HILLSLITE_UI_ANALYSIS.md` §4.4/§4.5、`UI_SPEC_SHELL.md` §7.3 实测）：**166×249**、列间距 **24**、右上角**紫色圆形**角标、标题白 + 副标题灰、无封面 `#FF333333` 占位 |
| **重建实现了** | 合集屏本体：`CollectionPage.xaml(.cs)` + `CollectionSelfTest.cs` + `CollectionEvidence.cs` |
| **运行验证过了** | 真起窗 + 真实 Emby 数据：合集 `76918`（**动画**，ServerB）→ **199 个条目**（Episode 196 / Movie 3），`175/199` 有封面 URL；**全部断言 True**（见 §2） |

> ⚠️ **本屏在参照图里没有对应物** ⇒ 整体构图维度一律 `INCONCLUSIVE(无参照)`（见 §4 差异表）。

## 1. 构建与运行读数

```
命令：dotnet build shell\App\AIPlayer.Shell.csproj -c Debug -v m -nologo
HEAD=6e858bc   采样：2026-09-12 03:11:29 → 03:12:0x（22.9 s）
EXIT=0   0 个错误   1172 个警告（全部来自 reversed/ 反编译源；本卡改动 0 警告）
日志：shell/Tests/evidence/ui2-t58-buildG1.txt（606,354 B / 2,363 行 / sha12 9A9A5B08C0D4）

运行：$env:SHELL_START_PAGE=collection; $env:SHELL_SELFTEST_COLLECTION=1; $env:SHELL_AGG_MAX_SERVERS=4;
      $env:SHELL_AGG_IMAGE_WINDOW=8; $env:SHELL_AGG_IMAGE_LIMIT=12  →  AIPlayer.Shell.exe
```

## 2. 自检读数（证据文件 `evidence/t58-collection-selftest.txt`）

```
证据：9803 B / 54 行 / sha12 762BBE08B22F
合集来源 = 自动（未设 SHELL_COLLECTION_ID）⇒ id=76918（动画）
服务器   = ServerB（id=lW46MFbH5dK8HqzE）
条目卡片数 = 199   类型分布 = Episode=196 / Movie=3   有封面 URL = 175/199   带角标 = 1/199（角标 ✓）
```

### 2.1 「找不到合集」是可归因的（**探测矩阵**，这是本卡特意加的反控）

第一轮自检时"自动找一个合集"返回 **未找到**。**一个 0 条读数无法归因**（可能这台服务器没有合集，也可能是我的查询形态不对）
⇒ 加了**遍历多台服务器 × 两种查询形态**（全局 `IncludeItemTypes=BoxSet` ＋ 逐个库 `ParentId={viewId}`）的探测矩阵：

```
ServerA/global=0 | ServerA/view:播放列表=0 | ServerA/view:电视 - 日本=0 | …（ServerA 的 18 个库全 0）
ServerB/global=0   | ServerB/view:00 新番连载=0 | … | **ServerB/view:播放列表=2**   ← 命中
```

⇒ 结论是**可判 False 的**：`ServerA` 上确实没有合集（18 个库 + 全局全 0），`ServerB` 的「播放列表」库里有 **2** 个合集；
选中 `76918 动画`。**这不是"我查不到"，是"那台没有"。**

### 2.2 断言（逐条原文）

| 断言 | 结果 |
|---|---|
| `断言 合集 id 非空` | **True** |
| `断言 取数成功（刷新已发起）` | **True** |
| `断言 条目卡片 ≥ 1` | **True**（实际 199） |
| `断言 每张卡都有封面 URL 或占位` | **True** |
| `断言 角标只可能是「数字 / ✓ / 空」` | **True** |
| `断言 0 条可归因（有内容时无需归因；0 条时必须两读数为 0）` | **True**（对照：加类型过滤 **199** / 不加过滤 **199** ⇒ 过滤没有漏内容） |
| `断言① 占位 → 封面 真的发生` | **True**（窗口 8：0 → 12） |
| `断言② 缓存态每张卡都取不到真条目` | **True**（打断网 ⇒ 起播门必拦 + 可见失败行） |
| `断言② 真刷新后至少一张卡可起播` | **True** |
| `断言③ 前提（刷新成功 且 两组 id 相同）` / `断言③ T0 的首张卡片实例未被换` | **True / True**（T1 就地替换） |
| `断言 文案纪律 命中总数 = 0` | **True**（4 条模式各 0 命中；口径 = 剥 XML 注释 + 取 `Text/Content/Header` 字面值 + ToolTip 豁免） |

## 3. 截图级取证（通道 = `popup-capture.ps1` + `-Hwnd` 单窗 `PrintWindow`）

| 文件 | bytes | sha12 | 说明了什么 |
|---|---|---|---|
| `evidence/t58-collection-window.png` | 294,849 | `021813057B6C` | **真起窗**：返回箭头 + 「动画」+「合集 · ServerB · **条目 199 个**」+ 摘要「条目 199 个 · 缓存=命中（Fresh）」；海报网格（标题 + 副标题）；**未加载封面的卡片是 `#FF333333` + 胶片字形占位**（人眼确认不是豆腐块）；底部状态条 `当前页: collection` |
| `evidence/t58-collection-ui-audit.txt` | 7,206 | `BC6073F08F09` | `ui-design-audit.ps1` 原始输出（见 §5） |

> ⚠️ **截图必须抢在"自动切库"之前**（实测把这条写进纪律）：外壳在启动后约 **4–16 s** 会做一次 `SRV-SWITCH … → Nav -> library`，
> 把 `SHELL_START_PAGE` 指定的屏顶掉。第一次抓图因此拍到了**媒体库屏**（不是本屏）⇒ 改为「轮询窗口出现即抓」（约 8 s 内完成）后拿到本屏画面。
> 这一条同时是**给 `ui` 的观察**：启动期的 `SRV-SWITCH` 会覆盖 `SHELL_START_PAGE`（与他 `t63`/导航族相关）。

## 4. 差异表（对照物里没有这一屏）

| 维度 | 判定 |
|---|---|
| 整体构图（头部/标题位置/列表排布） | **`INCONCLUSIVE(无参照)`** —— `refs/original-ui/` 里没有合集详情屏 |
| 海报几何（166×249 / 列间距 24 / 右上角紫色圆形角标 / 标题白 + 副标题灰） | 逆向到了（§7.3、§4.5 实测值） |
| 像素级位置比对 | **未验**（留给 `t34`） |

## 5. 视觉量测工装读数（`shell/tools/ui-design-audit.ps1`）

```
命令：powershell -NoProfile -ExecutionPolicy Bypass -File shell/tools/ui-design-audit.ps1 `
        -Image shell/App/Features/Collection/evidence/t58-collection-window.png -Channel printwindow -WindowKind xaml
SUMMARY|verdict=INCONCLUSIVE|pass=16|fail=0|inconclusive=18|exit=0
```

同人物屏：`INCONCLUSIVE` 全部来自**令牌/几何探针**（`probe set reference 1403x794 != image 1440x759`，`note` 原文可复算），
**不是**"错组合当 PASS"，也不是 FAIL；要闭合需为该尺寸另出一套探针坐标（`-TokenProbes`），留给 `t34`。

## 6. 本屏抓到并修掉的真缺陷

1. **副标题长时间显示「条目 0 个」**：与人物屏同源（赋值在封面串行加载之后）⇒ 抽 `UpdateSubtitleText()` 跟着渲染走。
2. **入口自动被顶掉**（不是本屏的代码缺陷，但影响本屏可达性）：见 §3 的 `SRV-SWITCH → Nav -> library` 观察。

## 7. 未验 / 边界（宁缺不编）

1. **像素级**（网格首列 x/y、行距、占位底色逐像素）—— 未做，留给 `t34`。
2. **真实鼠标点击起播** —— 只证门的判据，未做点击注入。
3. **`合集` 的入口**：从搜索结果点一个《合集》条目进本屏**尚未接线**（搜索页属 `ui3` 的 `t28`/`t32`）⇒ 本屏目前经 `SHELL_START_PAGE=collection` 可达（路由 case 由 `ui` 在 02:29:55 落地）。
4. **199 条只画前若干行**：真实数据下网格滚动正常，但"滚动到底部"未逐屏取证。

## 8. 交付物（全部在领地内）

```
源码
  shell/App/Features/Collection/CollectionPage.xaml
  shell/App/Features/Collection/CollectionPage.xaml.cs
  shell/App/Features/Collection/CollectionEvidence.cs
  shell/App/Features/Collection/CollectionSelfTest.cs

报告
  shell/App/Features/Collection/EVIDENCE_T58.md      （本文）

证据（各自身份见 §3 表）
  shell/App/Features/Collection/evidence/t58-collection-selftest.txt
  shell/App/Features/Collection/evidence/t58-collection-window.png + .txt sidecar
  shell/App/Features/Collection/evidence/t58-collection-ui-audit.txt
```

**共用既有文件（只做加法）**：`Features/Aggregate/MediaSnapshotSource.cs`、`AggregateDiagnostics.cs`（见人物屏证据 §8）。
**未触碰**：`reversed/**`、`shell/Services/**`、`Theme/**`、`MainWindow.*`、`App.xaml(.cs)`、他人 Features。
