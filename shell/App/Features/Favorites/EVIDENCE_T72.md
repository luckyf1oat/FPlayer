# t72 证据：收藏页作用域（当前服务器）+ 三类型分组

> 归属：`ui2`。**运行验证过了**（真起窗 + 本机真实 Emby 数据）。只动卡面声明的三个文件 + 自检，未碰 `AggregatePage`。

## 0. 三态

| 态 | 内容 |
|---|---|
| **逆向到了** | 用户原话「主页和收藏页面都是当前服务器的收藏和首页」；三组形态 = `UI_MAP.md` §2.2（**收藏的电影 / 收藏的剧 / 收藏的集**，数据 = `includeItemTypes:"Movie,Series,Episode", isFavorite:true`，**只查主源一台**） |
| **重建实现了** | ① `FavoritesPage.ReloadAsync` 只解析主源；② `MediaAggregator.FavoritesItemTypes` = `Movie,Series,Episode`（两屏共用）；③ `SnapshotRenderer.ToFavoriteSections` 输出三分区 |
| **运行验证过了** | 见 §2/§3：两份真跑证据（换台反控 + 逐类型交叉核对），**失败断言 0** |

## 1. 逐文件改动（卡面声明面内）

| 文件 | 改动 |
|---|---|
| `Features/Favorites/FavoritesPage.xaml.cs` | 取数作用域改主源（`ResolveMainServer()`，口径同 `HomePage.ResolveServer`：`LastServerId` 命中者，否则第一台已启用 Emby；**解析不出就走空态，绝不回退"全部服务器"**）；新增 `FAVORITES scope server=… id=… count=…` 日志行；三分区计数入摘要；新增只读访问面 `MainServer` |
| `Features/Aggregate/MediaAggregator.cs` | `FavoritesItemTypes`：`"Series,Season"` ⇒ `"Movie,Series,Episode"`（**收藏屏与聚合视界的收藏过滤器共用一个常量**） |
| `Features/Aggregate/SnapshotRenderer.cs` | `ToFavoriteSections`：两个分区 ⇒ **三个分区**（`Movie`→收藏的电影 / `Series`→收藏的剧 / `Episode`→收藏的集；三类之外的条目归入「收藏的剧」而**不静默丢弃**）；出参改 `movieCount/seriesCount/episodeCount` |
| `Features/Aggregate/T31SelfTest.cs` | 断言改三条：**恰有三个分区且标题逐字相等** / **作用域 = 只有主源一台** / **三分区张数之和 = 卡片总数**；逐类型反控改 `Movie/Series/Episode` |

## 2. 构建读数

```
命令：dotnet build shell\App\AIPlayer.Shell.csproj -c Debug -p:OutputPath=E:\ui2-bin\ -v m -nologo
HEAD=798d4f8 @2026-09-12 04:39:42  EXIT=0  0 个错误  1172 个警告（全来自 reversed/）
是否 -t:Rebuild：否（增量）；我领地（Aggregate/Favorites/Person/Collection/KernelHost/Infrastructure）警告 = 0
```

## 3. 证据（两份，分别对应"不换台 / 换台"两个前提）

| 文件 | bytes / 行 | sha12 | utc |
|---|---|---|---|
| `shell/Tests/evidence/t72-favorites-scope.txt` | 8,261 / 99 | `8474F29B2EE1` | `2026-09-11T20:40:04Z` |
| `shell/Tests/evidence/t72-favorites-scope-switch.txt` | 6,702 / 92 | `B9FF3CE67127` | `2026-09-11T20:41:30Z` |

### 3.1 主前提（`lastServerId` = ServerA）
```
⑤ 摘要行原文   = 收藏：1 台有内容 · 共 30 条（电影 1 / 剧 18 / 集 11） · T0=hit（Stale）
分区数 = 3      分区标题（原样）= 收藏的电影 / 收藏的剧 / 收藏的集
断言 恰有三个分区（电影 / 剧 / 集）= True
断言 作用域 = 只有主源一台         = True（参与取数 1 台，主源=ServerA）
断言 三个分区张数之和 = 卡片总数   = True（30 vs 30）
  · [收藏的电影] 卡片=1  带角标=1（数字=0 ✓=1）
  · [收藏的剧]   卡片=18 带角标=18（数字=6 ✓=12）
  · [收藏的集]   卡片=11 带角标=10（数字=0 ✓=10）
```

### 3.2 换台反控（**可判 False**：把 `lastServerId` 换成另一台已启用服务器 ServerB）
```
⑤ 摘要行原文   = 收藏：1 台有内容 · 共 10 条（电影 0 / 剧 6 / 集 4） · T0=hit（Fresh）
断言 恰有三个分区（电影 / 剧 / 集）= True
断言 作用域 = 只有主源一台         = True（参与取数 1 台，主源=ServerB）
```
⇒ 屏内容随"当前服务器"整体变化（30 → 10），**证明"只查主源"不是"恰好只有一台有数据"**。
`settings.json` 跑前备份、跑后还原（`match=True`）；两次运行都**自己起窗自己停**（pid 11548 / 12888），残留 0。

### 3.3 逐类型交叉核对（同一次运行内）
```
本屏查询 Movie,Series,Episode：ServerA = 30（Episode=11,Series=18,Movie=1）｜ServerB = 10（Episode=4,Series=6）
                              ｜ServerF…= ERR:ShellHttpException（本机既有坏源）｜步兵营地 = 2（Movie=2）
类型 Movie   收藏数 = 3   （ServerA=1 ServerB=0 ServerF=ERR 步兵营地=2）
类型 Series  收藏数 = 27  （ServerA=21 ServerB=6 …）
类型 Episode 收藏数 = 16  （ServerA=12 ServerB=4 …）
```
⇒ ServerB 的复合读数 `剧 6 / 集 4 / 电影 0` 与"逐类型查询里 ServerB 的 Series=6 / Episode=4 / Movie=0" **逐值吻合**；换台后屏上的 10 条正是这一台的真实收藏 ⇒ **作用域与类型两个口径都被独立数据源验证过**。

## 4. 🔴 本轮发现（**新问题，需裁示，不在本卡范围内修**）：收藏屏每台被截断在 30 条

- 现象：ServerA 的复合查询返回 **恰好 30 条**（= `MediaAggregator.ResumeLimitPerServer`），而逐类型查询（`limit=200`）在同一台上是 **Movie 1 + Series 21 + Episode 12 = 34 条** ⇒ **屏上少 4 条**；三组张数 `1/18/11` 与真值 `1/21/12` 对不上，差额正好是被 limit 截掉的那些。
- 影响：**用户收藏超过 30 的剧会被静默截断**（"收藏的剧"看起来只有 18 部，实际 21 部）。这是**用户可见的静默丢失**，不是显示风格问题。
- 归属与建议：`ResumeLimitPerServer = 30` 是**为了"继续播放"那种横向行**定的上限；收藏屏是**列表/网格**，不应共用它。建议**单开一小卡**：收藏屏改用自己的上限（或分页/滚动加载），并配一条反控"逐类型计数 ≥ 屏上张数时，差额必须为 0"。
- **本卡处置**：按卡面"不回退今晚已合入的改动、不扩范围"的原则，我**不改**它，只如实登记并上报（三态：`重建实现了` 的是"按 30 条上限取数"，`未验证/待裁` 的是"该不该有上限"）。

## 5. 未验 / 边界（宁缺不编）

1. 聚合视界的**节内类型子分组**未做（按 captain 未反对的 (甲)：本轮不碰 `AggregatePage`）。
2. 像素级（三分区标题字色/间距）未比对，留给 `t34`。
3. 缓存语义回归：本轮沿用 `t54` 的四条 + ⑤/⑥ 反控代码路径未改（`MediaSnapshotSource` / `LoadAsync` / 起播门均未动）——**但本次两份证据里跑的就是同一套自检**（含 SEAM③ 全段），**失败断言 0** ⇒ 未回退。
4. `FAVORITES scope` 那行日志落在 `shell-startup.log`（不落在证据文件里），本卡证据靠"摘要台数=1 + 断言 作用域"两条读数，不依赖它。

## 6. 交付物

```
源码
  shell/App/Features/Favorites/FavoritesPage.xaml.cs
  shell/App/Features/Aggregate/MediaAggregator.cs
  shell/App/Features/Aggregate/SnapshotRenderer.cs
  shell/App/Features/Aggregate/T31SelfTest.cs

报告
  shell/App/Features/Favorites/EVIDENCE_T72.md          （本文）
  shell/App/Features/Favorites/t72-PRE-IMPLEMENTATION-CHECKLIST.md

证据（身份见 §3 表）
  正文两份：
  shell/Tests/evidence/t72-favorites-scope.txt
  shell/Tests/evidence/t72-favorites-scope-switch.txt
```
