# t31（U-E 收藏 + 聚合视界）—— 运行证据

> 卡面：`t31` U-E「收藏 + 聚合视界（P4）：两分区 + 跨服分组 + 进度条」
> attempt 1 / attempt_id `03ecac8a-736f-4543-b137-f3fed8e982d7`
> 证据三态：**[实测]** 真跑过 / **[未验]** 没跑过。**不把"写了代码"当"验证过了"。**

---

## §0 一句话

两屏都实现并在**本机 4 台真实 Emby 服务器**上跑出数据：聚合视界三过滤器全过（收藏 42 条 / 媒体库 180 条，
跨服分组 + 进度条 + 失败源红字 + 占位图均按 §10 P4 判据成立）；收藏屏 42 张卡带来源角标、可取消收藏。
自检反控 4 组全 True。过程中**又抓到一个真缺陷**（服务层 `ItemFields` 不含 `ImageTags` ⇒ 整屏无封面），已在本卡范围内绕开。

---

## §1 命令与读数

```powershell
dotnet build shell\App\AIPlayer.Shell.csproj -c Debug
```

| 项 | 值 |
|---|---|
| 退出码 | **0** |
| 错误 | **0** |
| 读数时刻 | 2026-09-11 23:27:51 / 23:31（HEAD `cb8eaaa`） |
| 警告 | 1172（全部来自 `reversed/MpvHost/**` 反编译源 CS8632/CS0618 一族） |

运行自检（可复现）：

```powershell
$env:SHELL_START_PAGE="aggregate"        # 或 favorites
$env:SHELL_SELFTEST_AGGREGATE="1"        # 或 SHELL_SELFTEST_FAVORITES=1
$env:SHELL_AGG_MAX_SERVERS="4"           # 只取前 4 台（本机 accounts.json 有 16 台）
$env:SHELL_AGG_IMAGE_LIMIT="12"          # 封面取证预算（生产不设 ⇒ 全量加载）
$env:SHELL_AGG_IMAGE_WINDOW="8"          # 封面加载窗口（证明"占位→封面"真的发生）
& "shell\App\bin\Debug\net9.0-windows10.0.22621.0\win-x64\AIPlayer.Shell.exe"
```

原始读数文件（**按屏分名：同一次运行的字节不得有两个文件名**，两屏各自整体覆盖写）：

- `shell/Tests/evidence/t31-aggregate-selftest.txt` —— 聚合视界（三个过滤器 + 封面转换 + 反控）
- `shell/Tests/evidence/t31-favorites-selftest.txt` —— 收藏屏（分区/角标 + SEAM③ 四条缓存反控）

两份均为 UTF-8 无 BOM，由自检钩子（`T31SelfTest`）写入；文件名由 `AggregateDiagnostics.SetEvidenceSuffix(screen)` 决定。

---

## §2 [实测] 聚合视界：三个过滤器

| 过滤器 | 台数 | 有内容 | 失败 | 条目总数 | 耗时 | 摘要行原文 |
|---|---|---|---|---|---|---|
| `ContinueWatching` | 4 | 0 | 1 | **0** | 1468 ms | `继续播放：跨 4 台服务器 · 有内容 0 台 · 失败 1 台 · 共 0 条 · 耗时 1468 ms` |
| `Favorites` | 4 | 3 | 1 | **42** | 6030 ms | `收藏：跨 4 台服务器 · 有内容 3 台 · 失败 1 台 · 共 42 条 · 耗时 6030 ms` |
| `Library` | 4 | 3 | 1 | **180** | 11067 ms | `媒体库：跨 4 台服务器 · 有内容 3 台 · 失败 1 台 · 共 180 条 · 耗时 11067 ms` |

> **三个数各自指名**（卡片纪律）：台数 / 有内容台数 / 失败台数 / 条目总数**分别断言**，
> **不写"三数一致"**（它们按设计本就互不相等）。摘要行按这四项具名输出，自检逐项核过 `True`。

**跨服分组逐行**（`Favorites` 过滤器，原样）：

```
· [ServerA（vU9cHA）] items=30 hasImage=27 placeholder=3 progressBars=30 progressMax=1
· [ServerB（lW46MF）]   items=10 hasImage=6  placeholder=4 progressBars=10 progressMax=0.103
· [ServerF～(∠・ω< )⌒☆（4Qq2CB）] items=0 FAILED=ShellHttpException: HTTP 403 Forbidden
· [步兵营地（5qqE5P）] items=2  hasImage=2  placeholder=0 progressBars=2  progressMax=0.768
```

- **分组名带 ServerId 短号** ⇒ 两台同名服务器可区分（`UI_SPEC_SHELL.md` §7.6 同纪律）。
- **进度条**：`Favorites` 过滤器实测 `progressMax=1`（片尾）、`0.768`、`0.103` ⇒ 进度条真的按观看进度取值，不是常量。
- **失败源**：该源条目为 0、行内红字（`ServerFail`），**其余 3 组照常显示** ⇒ "单源失败不拖垮整屏"。
- `ContinueWatching` 三台返回 0 条：这 4 台的收藏账号在该服务器上**没有续播记录**（不是取数失败 —— 失败只有 1 台且已被单列）。

### 2.1 空态 / 失败态（§10 P4「空源显示占位图不崩」的两种"空"）

| 情形 | 实测表现 |
|---|---|
| 该源**取数失败** | 行内 `ServerFail` 红字 + 条目 0；其余行不受影响 |
| 该源**取数成功但 0 条** | 保留该组 + 灰字"该服务器没有匹配的条目"（`EmptyVisibility`，同一行同时给出。`ContinueWatching` 三行即此形态） |

---

## §3 [实测] 收藏屏（**两个分区 + 未看集数角标**）

规格（`HILLSLITE_UI_ANALYSIS.md` §2 行 2，参照图 `hl-02-favorites-printwindow.png` **已人眼核对**）：
**两个分区**「收藏的剧 / 收藏的季」（区标题带 `›`）；海报**右上角紫色圆形数字徽章** = **未看集数**，
`✓` = 已看完（§4.5「单帧放大判读」+ §4.6 数据源 = `UserData.UnplayedItemCount` / `Played`）。

| 读数 | 实测 |
|---|---|
| 服务器台数 / 有收藏 / 失败 | 4 / 2 / 1 |
| 收藏卡片数 | **27** |
| 摘要行 | `收藏：4 台服务器 · 有收藏 2 台 · 失败 1 台 · 共 27 条（剧 27 / 季 0） · 耗时 1804 ms` |
| **分区数** | **2** ✅（`收藏的剧` 27 张 / `收藏的季` 0 张） |
| **角标覆盖** | 27/27 张**全部带角标**（**数字 14 张 / `✓` 13 张**） |
| 角标取值样例 | `96 / 7 / 1 / ✓ / 13 / ✓ / ✓ / ✓ / ✓ / ✓ / ✓ / ✓` |

逐条样例（原样）：
```
· [收藏的剧]
    - 刀剑神域 | 角标='96' | 剧集 · 4 集 | hasImageUrl=True
    - 和班上第二可爱的女孩成为朋友 | 角标='7' | 剧集 · 1 集
    - 辉夜大小姐想让我告白 通往大人的阶梯 | 角标='✓' | 剧集 · 1 集
    - 堀与宫村 | 角标='13' | 剧集 · 1 集
· [收藏的季]  卡片=0
```

断言（原样）：`恰有两个分区 = True`｜`卡片都有来源角标 = True`｜`收藏屏不画进度条 = True`｜
`角标形如「未看集数 / ✓」 = True`。封面加载后 27 张里 24 张有图（其余 3 张服务端无封面 ⇒ 占位）。

### 3.1 「收藏的季 = 0」是**数据如此**，不是查询漏（附反控）
分区是按条目 `Type` 分的，所以"季 0 张"必须能自证。**逐台 + 逐类型**各查一次收藏：

```
本屏查询 Series,Season ServerA = 21   （Series=21）      ← 只返回 Series，无 Season
本屏查询 Series,Season ServerB   = 6    （Series=6）
本屏查询 Series,Season ServerF～(∠・ω< )⌒☆ = ERR:ShellHttpException（403）
本屏查询 Series,Season 步兵营地 = 0    （）

类型 Series  收藏数 = 27   （ServerA=21 ServerB=6 …）
类型 Season  收藏数 = 0    （ServerA=0 ServerB=0 …）      ← **真实为 0**
类型 Movie   收藏数 = 3    （ServerA=1 步兵营地=2）
```
⇒ 4 台上「收藏的季」**确实一张都没有**（`Season` 类型收藏数 = 0），故该分区为空态而非查询漏。
**同时证伪了一个可能的"漏类型"缺陷**：`EmbyService.DefaultSearchItemTypes` **不含 `Season`**，
若照抄它做收藏查询将**永远出不了"收藏的季"** ⇒ 本屏改用 `MediaAggregator.FavoritesItemTypes = "Series,Season"`。

### 3.2 ⚠️ [未解释] 一处计数不一致（如实记录，不编解释）
`Movie` 类型收藏 = **3**（ServerA=1、步兵营地=2），但**本屏那条查询（`Series,Season` + `IsFavorite=true`）返回里 0 个 Movie**
（逐台明细 21+6=27，类型分布只有 `Series=21` / `Series=6`）。
⇒ 服务端在"复合 `IncludeItemTypes` + `IsFavorite`"下的行为与"单类型 + `IsFavorite`"不一致。
**对本卡要求无影响**（规格只要"剧 / 季"两分区），但**结论未查实**，记在此处供 `t35` 决定是否追。


---

## §4 [实测] 封面「占位 → 封面」转换（**逐屏独立测量**）

```
--- 过滤器：Favorites ---
  封面：有 URL=37/40 窗口=8 加载前=0 加载后=35 本轮新增=35 失败=2
  断言 占位 → 封面 转换真的发生      = True
  断言 剩余未加载的仍是占位（不崩）= True
--- 过滤器：Library ---
  封面：有 URL=54/120 窗口=8 加载前=0 加载后=54 本轮新增=54 失败=2
  断言 占位 → 封面 转换真的发生      = True
  断言 剩余未加载的仍是占位（不崩）= True
```

- **逐屏各测一次**（窗口逐屏重开）：早期版本只在最后一屏测 ⇒ 全局预算被前两屏吃光 ⇒ 第三屏必然
  `withImage=0`，那是**测量伪影**（既不代表缺陷也不代表正常）。改成逐屏测量后，两屏各自都证明转换真的发生。
- 自检模式下**不自动加载封面**（`SHELL_SELFTEST_AGGREGATE=1` 时跳过自动补齐），否则"这一跳"在测量前就发生了
  —— 实测踩到过：`加载前=35 加载后=35 本轮新增=0`，那是**仪器坏了不是能力没有**。生产（不设该变量）行为不变。
- `有 URL=54/120`：另 66 条服务端确实没有封面（`ImageTags` 无 `Primary` 且无 `SeriesPrimaryImageTag`）
  ⇒ 走占位，符合 §10 P4「空源显示占位图不崩」。
- `失败=2`：`ServerB` 服务器上 2 张封面取字节时 `HttpRequestException`（网络抖动）⇒ 该卡保持占位、列表不受影响。

---

## §4.5 图标体系合规（`UI_SPEC_SHELL.md` §1.1 新纪律，captain 2026-09-12 派）

**规则要点**：禁用 emoji 当 UI 图标；禁用文本符号（`▤ ⇄ ⌵ ⟲ ★ ☁`）当图标；统一 `Segoe Fluent Icons` + 令牌着色 + 尺寸 16/20；
⭐ 硬条件 = `♥`/`♡`/`✓` 后**绝不能追加 `U+FE0F`**（否则渲染成彩色 emoji，正是用户抱怨的"丑"）。

**本卡两个屏的判定（captain 已逐个看过并放行）**：
- 三个过滤器标签 `▶ 继续播放` / `♡ 收藏` / `▤ 媒体库` = **参照物本身的文字标签**（`HILLSLITE_UI_ANALYSIS` §4.4）⇒ **属文案不属图标，保留不改**；
- 占位/分区/收藏态用的 `♥`/`♡`/`✓` = **单色文本符号**（`U+2665`/`U+2661`/`U+2713`），跟随前景色 ⇒ 允许保留。

**我做的扫描（可复现；脚本 = `%TEMP%\t31-*-scan.ps1`，ASCII-only）**：

| 检查 | 结果 |
|---|---|
| `U+FE0F` / `U+FE0E` 变体选择符 | ✅ **0**（10 个文件全部） |
| `U+2605` / `U+2606`（★☆ 文本符号） | ✅ **0** |
| emoji 区段（`U+1F000–U+1FAFF`） | 🔴 起始 **12 处** `U+1F534`（🔴 红圆）⇒ **已全部替换为 `[!]`**，复扫 0 |
| 现有符号（单码位文本字形） | `U+2661`♡ 11 ／ `U+2665`♥ ／ `U+2713`✓ ／ `U+26A0`⚠（**全部在注释里或经 captain 放行**） |
| 编码未被改动 | 10 个文件 **BOM=False、CR=0**（扫描前后一致） |

**⚠️ 一处需登记的细节**：修复前 7 处 `U+FE0F` **全部跟在 `U+26A0`(⚠) 之后、且全部在注释里**（`AggregatePage.xaml.cs:128`、`MediaAggregator.cs:15/:133`、`AggregateDiagnostics.cs:9`、`T31SelfTest.cs:6/:52/:300`）——
即**没有一处**违规地跟在 `♥`/`♡`/`✓` 后面。但 `FE0F` 在多数字典排序/grep 里不可见、且这条规则面向全仓，**仍已全部删除**（连带 `🔴` emoji 一起清掉），现在 10 个文件里 `U+FE0F` = 0。

**一条给全仓的建议**：`⚠`+`U+FE0F` 这类组合**不在"UI 图标"字面范围内**（它在注释里），但 robots 难查、且与"防 FE0F"的规则精神一致
⇒ 建议 captain 把该纪律的执行器做成**源码级扫描**（不只扫 XAML），并把 `U+FE0F` 单列一条硬门禁；`verifier` 的 hygiene 工装已具备同类能力，可直接挂。

---

## §4.6 SEAM③「缓存优先 + 并发刷新」四条反控（**实测，全 True**）

接线对象：`Features/Aggregate/MediaSnapshotSource.cs`（唯一 SWR 入口）+ `SnapshotItem.cs`（渲染面白名单快照）+ `SnapshotRenderer.cs`（缓存态与刷新态**同一渲染器**）。
`services` 侧的 "运行验证" 只到服务层（其自检 S7③/S7④）；**UI 侧的四条反控由本轮真跑取证**（起壳 + 真实数据，证据 `shell/Tests/evidence/t31-aggregate-selftest.txt`）。

| 反控 | 判据 | 实测读数 |
|---|---|---|
| **① 冷启动** | 清缓存后该屏**仍可用**、退化为骨架/冷取 | `ClearAll() 删除条数=1 剩余=0` ⇒ `cache=miss freshness=Miss refresh=done`；卡片数仍 **27**、分区 **2** ⇒ 断言 True |
| **② 热路径必须退化** | 缓存已在时把取数打坏 ⇒ **只能靠缓存**显示 | 预热后 `缓存条数=1`；断网后 `cache=hit freshness=Fresh age=0.0s refresh=started/failed cache-preserved=True`，**屏上仍有 27 张卡**（内容只可能来自缓存）⇒ 4 条断言全 True |
| **③ 坏源** | 失败提示出现 + **缓存内容仍在** | 注入不可达源 ⇒ `Failed≥1 且含"不可达"`、`NonEmpty≥1 且 TotalItems>0`、屏上仍 27 张卡 ⇒ 3 条断言全 True |
| **④ 两组 id 分别打印** | 缓存读出的 id 与刷新后的 id **分别打印** | `cachedIds (27)` 与 `refreshedIds (27)` **逐项相同**（同一批 27 个 id）⇒ 断言 True |

**失败态可见性（§12.3）** 原文：
```
失败提示原文 = 刷新失败：InvalidOperationException: SELFTEST-FETCH-DISABLED（反控②：证明内容真来自缓存）｜**已保留缓存内容**（未清空、未删缓存）
```
⇒ **不清空界面、不删缓存**这条有原文可核。

**两态区分（§12 硬要求）**：`HasCache=false` ⇒ 骨架屏；`HasCache=true` 且 `Groups` 为空 ⇒ **空态**（缓存记录的就是"没有内容"）。
反控里给了**可执行样本**（空组快照 `Groups=0` ⇒ 断言 True），不靠嘴说。

**⚠️ 一处自我更正（保留记录）**：③ 首次跑出 `False` —— 根因是我**断言口径写错**（写成"失败恰好 1 台"，而这一路前提里本来就带 1 台真实坏源 ServerF-403，加上我注入的合成源 ⇒ 天然是 2 台）。已把断言改成它真正要证的三件事（**坏源被登记 / 其它源不受影响 / 缓存卡片仍在**），改后全 True。**这不是把标准放松，是把断言改为"前提里包含既有坏源"下可成立的形式**。

**⚠️ 与文档示例的两处有意偏离（理由见代码头）**：
1. 缓存**不是** `List<EmbyItem>`，而是渲染面白名单 DTO ⇒ **缓存卡片不可直接起播**；起播改走 `LiveItemFor()` 取刷新后的真条目，取不到就**明确提示**而非静默起播残缺数据。
2. 快照**按组落盘**（`{serverId, serverName, items[]}`）以还原"按服务器分组"；键 = `aggregate:{kind}`、scope = `"all"`。

**摘要行口径更正**：快照只保存**有内容的组**（失败源另存 `FailedServers`）⇒ 摘要里 `N 台服务器` 会与聚合读数（成功+失败）不等。已把措辞改为 **`N 台有内容`** 并附 `T0=hit/miss（Freshness）`，避免把两种口径混成一句话。

---

## §5 🔴 自检抓到 / 查实的真缺陷（**不在本卡领地，已绕开并上报**）

**现象**：媒体库过滤器整屏**一张封面都没有**；日志实测单轮 **47 条** `poster-image-skip-no-url`（标题可复算，如"死神"、"碧蓝之海"）。

**根因（逐行核对，非推测）**：`shell/Services/Emby/EmbyService.cs:35-38` 的 `ItemFields` 常量**不含 `ImageTags`**：

```csharp
public const string ItemFields =
    "Overview,MediaSources,MediaStreams,ProviderIds,ChildCount,RecursiveItemCount,PrimaryImageAspectRatio," +
    "SeriesPrimaryImageTag,DateCreated,Path,Studios,People,Genres,Taglines,ProductionYear,PremiereDate," + …;
```

而 `GetItemsAsync`（`:265`）、`GetLatestAsync`（`:314`）、`GetResumeAsync`（`:297`）都把它当默认 `Fields` 发出去
⇒ 响应的 `ImageTags` 为空 ⇒ `PrimaryImageTag` 为空 ⇒ 外层 `MediaAggregator.PrimaryImageUrl` 判定"无封面" ⇒ 画占位。

**处置（**不越界**）**：服务层已给 `GetItemsAsync(fields:)` 这个注入点 ⇒ 本卡**在自己的取数处**补上字段
（当时用 `MediaAggregator.CoverFields = ItemFields + ",ImageTags,SeriesPrimaryImageTag"` 在领地内补字段，零服务层改动）。**后续状态（2026-09-12 04:0x）**：services 已把 ImageTags/SeriesPrimaryImageTag 补进 EmbyService.ItemFields ⇒ 本屏那份拼接成了**第二处定义**，已随代码风格整改删除（取数一律不传 ields:，由服务层默认值决定）。
**遗留缺口（上报 captain）**：`GetResumeAsync` 与 `GetLatestAsync` **没有 `fields` 入参**
⇒ "继续播放"与"最新添加"两条路仍然拿不到 `ImageTags`；**根治 = 服务层把 `ImageTags` 加进 `ItemFields`**（一行）。

**附带发现（同一路径）**：合成配置**不给凭据**时，请求路径会变成 `Users//Items`（`UserId` 为空串）——
服务层不做凭据存在性校验，缺凭据表现为 **404/异常**而不是明确的前置拒绝。本屏只把形状记进报告，不改服务层行为。

---

## §6 反控（4 组，全 True；证明这套取数/呈现能分辨"空""失败""无封面"）

```
① 注入不可达源：Groups=2 Failed=1 NonEmpty=1 TotalItems=30
   失败源显示名 = (反控)不可达服务器（t31-co）
   断言 失败源被登记且不影响其它源 = True
② 零服务器：Groups=0 NonEmpty=0 Failed=0
   断言 零服务器不抛且计数为 0 = True
③ 无封面条目：HasImageUrl=False PlaceholderVisibility=Visible ProgressWidth=40
   断言 无封面 → 画占位        = True
   断言 进度条宽度按比例        = True
④ 进度换算：PlayedPercentage=42.5 → 0.425 ｜ ticks 25% → 0.25 ｜ 无数据 → 0
   断言 三种来源各自正确        = True
```

- ① 注入的是**合成** `ServerConfig`（`http://127.0.0.1:9/emby`，端口 9 = discard）⇒ 真实连接失败，不是 mock。
- ④ 覆盖进度条的**三种来源**：服务端 `PlayedPercentage` > `ResumeTicks/RunTimeTicks` 自算 > 无数据为 0。

---

## §7 未验清单（**宁缺不编**）

| 项 | 状态 | 需要什么才能证 |
|---|---|---|
| 点卡片**起播**（跨屏复用 M3 链路） | **[未验]** | 需在窗口里真点一张卡；本轮为自动化自检，未做点击注入 |
| **取消收藏**写回服务端并持久 | **[未验]** | 需真点一次 ♥（会改用户真实收藏数据）⇒ 需用户/队长授权 |
| 像素级取证（海报 166×249 / 进度条位置 / 占位灰底 `#FF333333`） | **[未验]** | 需截图 + 全像素统计（本队现成工具 `tools\win-capture-pid.ps1` + 像素审计脚本）；本轮只做了数据面与对象面断言 |
| 键盘/无障碍（`Ctrl+F` 等） | **[未验]** | §7.7 只针对搜索页，本屏不在该节范围内 |
| 16 台全量（本轮 `SHELL_AGG_MAX_SERVERS=4`） | **[未验]** | 全量会拖长取证窗口；单台行为已由 4 台覆盖（含 1 台失败） |

---

## §8 交付物（全部在领地内）

| 文件 | 说明 |
|---|---|
| `shell/App/Features/Aggregate/MediaAggregator.cs` | 跨服并发取数 + 分组 + 失败源登记 + 进度换算 + 封面 URL |
| `shell/App/Features/Aggregate/AggregatePoster.cs` | 卡片视图模型（封面异步加载 / 占位态 / 进度宽 / 收藏态） |
| `shell/App/Features/Aggregate/AggregateFilters.cs` | 三过滤器 + 分组行模型 |
| `shell/App/Features/Aggregate/AggregateDiagnostics.cs` | 证据写盘（**含日志脱敏**：`?` 后查询串整体省略，避免把带凭据 URL 落盘） |
| `shell/App/Features/Aggregate/AggregatePage.xaml(.cs)` | 聚合视界屏（三过滤器 + 按服务器分组 + 进度条 + 空/失败/加载三态） |
| `shell/App/Features/Favorites/FavoritesPage.xaml(.cs)` | 收藏屏（整体列表 + 来源角标 + 取消收藏） |
| `shell/App/Features/Aggregate/Shared/ImageSourceLoader.cs` | 字节 → `BitmapImage`（一处失败兜底） |
| `shell/App/Features/Shared/AggregatePlayback.cs` | 起播入口（复用 t27 的 41 参数链路，不复制第二套） |
| `shell/App/Features/Aggregate/T31SelfTest.cs` | 自检钩子（环境变量默认关闭） |
| `shell/App/MainWindow.xaml.cs` | 路由 `favorites` / `aggregate` 两个 tag |

**未触碰**：`reversed/**`、`shell/Services/**`、`shell/App/Theme/**`、他人 Features。

---

## §9 t54 收口：SEAM③ 接线 + 四条反控（+ 第五条 T1 就地性）+ 门禁两笔

> 本节是 `t54`（`kind=work`，`U-I SEAM③ 缓存优先接线`）的**收口证据**。三态分开：

| 态 | 内容 |
|---|---|
| **逆向到了** | 用户原话（`Services/AGGREGATION_INCREMENTAL.md:3-4`）：「收藏就可以加一个缓存，先显示缓存，然后再查询」｜§12.3「失败保留缓存 + 可见提示」 |
| **重建实现了** | `Features/Aggregate/MediaSnapshotSource.cs`（唯一 SWR 入口）+ `SnapshotItem.cs`（渲染面白名单 DTO）+ `SnapshotRenderer.cs`；两屏 `ReloadAsync` 接 `SwrSnapshotCache.LoadAsync`（T0 缓存渲染 → T1 就地替换 → 失败保留） |
| **运行验证过了** | 见 §9.2 两次真跑（本机 3 台真实 Emby + 1 台真实坏源 ServerF 403），24 + 24 条断言**全 True** |

### 9.1 构建读数（独占安静窗口）

```
命令：dotnet build shell\App\AIPlayer.Shell.csproj -c Debug -v m -nologo
HEAD=cfd69d5   窗口：2026-09-12 02:10:38.xxx → 02:10:56.xxx（18.4 s）
EXIT=0   0 个错误   1172 个警告（全部来自 reversed/ 反编译源；本卡改动 0 警告）
日志：shell/Tests/evidence/ui2-t54-buildC2.txt（606,291 B / 2,363 行 / sha12 B2FE88C2E0BC）
```
（更早一次同 HEAD 的绿读数：01:53:48 @ `8fa2d4a`，31.9 s，0 错误 / 1172 警告；两次都能复现同一读数。）

⚠️ 同一时段另有 3 次红，**全部是并发混合态**，不是代码问题，逐条归因：① `CS2012 … intermediatexaml\AIPlayer.Shell.dll` 被 **另一个 `XamlCompiler.exe`(13628)** 锁（对方构建与我的同秒启动）；② `MSB3027/MSB3021 … AIPlayer.Shell.Services.dll` 被**在跑的外壳窗口**锁（pid 13140/18892 —— 别人的取证窗口，**未杀**）；③ `CS0234 Services.Player`（`Features/Detail/DetailPage.xaml.cs:10`）是 **`ui` 在途文件**，其 01:44:06 已自行改成 `AIPlayer.Shell.Player`。⇒ 三次均**等待安静窗口后重跑**取得上表绿读数。

### 9.2 两屏自检（同一份源码构建的 exe，真起窗）

| 屏 | 命令（环境变量） | 证据文件 | bytes / 行 | sha12 | utc |
|---|---|---|---|---|---|
| 收藏 | `SHELL_START_PAGE=favorites` `SHELL_SELFTEST_FAVORITES=1` `SHELL_AGG_MAX_SERVERS=4` | `shell/Tests/evidence/t31-favorites-selftest.txt` | 6,618 / 85 | `C565252A8DBD` | `2026-09-11T20:05:00Z` |
| 聚合视界 | `SHELL_START_PAGE=aggregate` `SHELL_SELFTEST_AGGREGATE=1` `SHELL_AGG_MAX_SERVERS=4` `SHELL_AGG_IMAGE_WINDOW=8` `SHELL_AGG_IMAGE_LIMIT=12` | `shell/Tests/evidence/t31-aggregate-selftest.txt` | 7,446 / 88 | `DC1C0B0F3823` | `2026-09-11T19:43:54Z` |

⚠️ **身份会随重跑变化**：上表两行的 `sha12` 是**各自那一轮运行**的落盘值。收藏屏那轮之后我又跑过一次（`CoverFields` 第二构造点删除后的能力反控：`封面加载后：total=27 withImage=12`）⇒ 该文件被重写一次、**字节数相同但 sha12 变**（`C8AB14D320B9` → `C565252A8DBD`），表内已按最新一轮更新。**引用身份前先跑 `shell/tools/evidence-hygiene-check.ps1` 的 `H5`**，不要凭记忆。

**证据文件名按屏分开**（`AggregateDiagnostics.SetEvidenceSuffix(screen)` ⇒ `t31-{aggregate|favorites}-selftest.txt`）：早期两屏共用一个文件名，两屏各自**整体覆盖写** ⇒ 后跑的会把先跑的整份读数盖掉；`t31-favorites-swr.txt` 是同一份运行字节的第二个文件名，**已删除**（同一次运行的字节不得有两个文件名，门禁 `H2b` 口径）。

### 9.3 SEAM③ 四条反控（逐条给出原始行；`t31-favorites-selftest.txt:53-76`）

| # | 反控 | 前提装置 | 原始读数（逐字） | 判定 |
|---|---|---|---|---|
| ① | 冷启动仍可用（不崩、退化为冷取） | `ClearAll()` 真删 | `① 冷启动：ClearAll() 删除条数=3 剩余缓存条数=0`；`cache=miss freshness=Miss refresh=done`；`冷启动后 卡片数=27 分区数=2`；`断言① 冷启动命中缓存 = False ⇒ True`＋`断言① 冷启动不崩且刷新已发起 ⇒ True` | **True** |
| ② | **内容真来自缓存**（不是"网络恰好也快"） | `FailFetchForSelfTest`（**打断网**） | `cache=hit freshness=Fresh age=0.0s refresh=started/failed`、`error=InvalidOperationException: SELFTEST-FETCH-DISABLED`、`cache-preserved=True`；屏上仍有 27 张卡；失败提示原文含「**已保留缓存内容**（未清空、未删缓存）」；四条断言全 True | **True** |
| ③ | 坏源 ⇒ 失败登记 + 缓存内容仍在 + 其它源不受影响 | 注入不可达源 `127.0.0.1:9` | `③ 坏源：Groups=4 Failed=2 NonEmpty=2 TotalItems=27`；失败源显示名 = `(反控)不可达服务器（t31-sw） / ServerF～(∠・ω< )⌒☆（4Qq2CB）`；三条断言 True | **True** |
| ④ | `CachedIds` 与 `RefreshedIds` **分别打印**（不合并成一个数） | 热缓存 + 真刷新 | `cachedIds (27) = [229045,…,87723]`、`refreshedIds (27) = [229045,…,87723]`（逐项一致）；两条断言 True | **True** |
| ⑥ | 缓存卡起播门：缓存态**必拦**、刷新态**必放行** | `FailFetchForSelfTest`（缓存态）／真刷新（正向） | `断言⑥ 缓存态下每张卡都取不到真条目（点卡必被拦）⇒ True（卡片数 27，LiveAggregate=<null>）`；`断言⑥ 真刷新后至少一张卡能取到真条目（起播门不是一律拒绝）⇒ True` | **True** |

**⑤ 新增：T1「就地替换」的可判形式**（`t31-favorites-selftest.txt:79-83`、`t31-aggregate-selftest.txt:83-87`）。原卡面反控③要求"记录滚动位置/选中项 ⇒ T1 替换后不变"；直接读 `ScrollViewer` 位移在本工程里要遍历可视树且易得假读数，故改用**实例身份**这一等价且可判 False 的形式：同一发里 T0 渲染的分区列表实例与首张卡片实例，在刷新落地后**必须是同一批对象**（一旦整屏重建，两个引用都会变）。

```
收藏屏：断言⑤ 前提成立（刷新成功 且 两组 id 相同 ⇒ 走 T1-noop）⇒ True
        断言⑤ T0 的分区列表实例未被换 ⇒ True
        断言⑤ T0 的首张卡片实例未被换 ⇒ True
聚合视界：断言 T1 前提（… ⇒ T1-noop）⇒ True ｜ 断言 T0 的分组行实例未被换 ⇒ True ｜ 断言 T0 的首张卡片实例未被换 ⇒ True
```

### 9.4 本卡内抓到并修掉的真缺陷（**摘要行永远缺 T0 状态** + **真条目残留**）

**缺陷 1：摘要行永远缺 T0 状态**

- **现象**：SEAM③ 之后聚合视界三个过滤器的 `断言 摘要含具名数字与 T0 状态 = False`（摘要原文 `继续播放：2 台有内容 · 共 0 条`，**没有** `T0=` 段）。
- **根因**：`AggregatePage.ReloadAsync` 的 T0 回调先 `_lastOutcome = null;` 再渲染 ⇒ 摘要行构造时拿不到 T0 读数；而 T1 走 **no-op**（两组 id 相同）时**不再重新渲染** ⇒ 摘要行**永远不会**补上 T0 状态。收藏屏同类：T0 渲染时 `_lastOutcome` 还是**上一发**的旧值（显示的是过期状态）。
- **修法（改产品面，不是改断言）**：两屏都在 T0 回调里用当刻 `SwrLookup` 构造 `_lastOutcome` **再**渲染；抽出 `BuildSnapshotSummary()` / `UpdateSummaryText()` —— T1 落地后**只改摘要文字、不重绑列表**（重绑会破坏 §9.3⑤ 的就地性）。
- **修复后读数**：`继续播放：2 台有内容 · 共 0 条 · T0=hit（Fresh）`／`收藏：2 台有内容 · 共 27 条 · T0=hit（Fresh）`／`媒体库：2 台有内容 · 共 120 条 · T0=hit（Fresh）`，三屏 `断言 摘要含具名数字与 T0 状态 = True`。收藏屏 `⑤ 摘要行原文 = 收藏：2 台有内容 · 共 27 条（剧 27 / 季 0） · T0=hit（Fresh）`。

**缺陷 2：刷新失败时"真条目"会残留（起播门因而失效）**

- **现象**（对照 §9.3⑥ 断言取反时的形态）：`FavoritesPage` 每次重载**不清空** `LiveAggregate`，而该字段只在取数成功时被回填 ⇒ 一次成功的取数之后，后续**刷新失败**的那几发里，`LiveItemFor()` 仍会命中**上一次**的真条目 ⇒ 缓存卡会被**放行**起播，而这正是 `t54` 契约第 6 条要拦的形态（"缓存 DTO 不含 `Raw`/`MediaSources`"）。
- **修法**：`ReloadAsync` 开头 `LiveAggregate = null;`（该字段只代表**本次**取数拿到的真条目）。
- **修复后读数**（`t31-favorites-selftest.txt:66 / :84`）：
  - `断言⑥ 缓存态下每张卡都取不到真条目（点卡必被拦）⇒ True（卡片数 27，LiveAggregate=<null>）`
  - `断言⑥ 真刷新后至少一张卡能取到真条目（起播门不是一律拒绝）⇒ True`
  两条一负一正：既证明"缓存态必拦"，也证明"门不是一律拒绝"（否则这一门会退化成功能不可用）。

### 9.5 门禁两笔（`H2b` / `H3b`+`H3`，均在 `ui2` 名下）

- **`H2b`（引用存在）**：本节 §1 早前引用的是一份 **`.log` 名**的聚合证据，那份**不在盘上**（早期用 `.log` 写、后统一改 `.txt`）⇒ 已改为按屏分名的两个 `.txt`（两者都在盘上，见 §9.2 表）。此处**刻意不再写出旧文件名**，免得被引用扫描当成一条引用。
- **`H3b` 裸 `catch` / `H3` 空 `catch`（7 处，全部本人文件）**：`MediaAggregator.cs:279`、`KernelLauncher.cs:103`、`ShellToolkits.cs:56/102`、`InboundCallbackEndpoint.cs:135/144/184`（后 3 处同时是 `H3`）。处置：**全部点名异常类型**；关停路径（`Stop()`）与原"吞掉一切"的鲁棒性**逐字保留**，但**不再静默** —— 改为落一行日志（`CALLBACK-ENDPOINT cancel-failed / stop-failed / close-failed`）。

### 9.6 本卡改动文件（逐条，都在领地内）

```
shell/App/Features/Aggregate/MediaSnapshotSource.cs   (t31 已交付，本卡核对)
shell/App/Features/Aggregate/SnapshotItem.cs
shell/App/Features/Aggregate/SnapshotRenderer.cs
shell/App/Features/Aggregate/AggregatePage.xaml.cs    T0 读数入摘要 / UpdateSummaryText / T1 观察点
shell/App/Features/Favorites/FavoritesPage.xaml.cs    同上 + 分区计数入摘要
shell/App/Features/Aggregate/T31SelfTest.cs           反控⑤（两屏）+ 收藏屏证据名分屏
shell/App/Features/Aggregate/AggregateDiagnostics.cs  SetEvidenceSuffix / EvidenceFileName
shell/App/Features/Aggregate/t31-U-E-EVIDENCE.md      本文
shell/App/Features/Aggregate/SEAM2-SEARCH-INCREMENTAL-WIRING.md  SEAM② 接线规格（归属 (C)：ui3 落地，我复核）
shell/App/Features/Aggregate/MediaAggregator.cs       H3b
shell/App/KernelHost/KernelLauncher.cs                H3b
shell/App/KernelHost/ShellToolkits.cs                 H3b ×2
shell/App/KernelHost/InboundCallbackEndpoint.cs       H3b ×3（同时 H3）
```

### 9.7 本卡仍**未验**（宁缺不编）

1. **缓存卡起播的成功路径**：`LiveItemFor` 门在"只有缓存、真条目未到"时**可见拒绝**（`FAVORITES/AGGREGATE play-blocked no-live-item` + 屏上提示），但"真条目到达后点卡能起播"需要**真实点击注入**，本卡未做（t31 亦标未验）。
2. **T1 的"不同 ids ⇒ 真替换"分支**：本卡测到的是 **no-op 分支**（两组 id 逐项相同）；`ids-changed` 分支需要构造"缓存与服务器不一致"的前提，未做。
3. **像素级**：摘要行/角标/进度条的**位置与配色**未做像素比对（留给 `t34` 视觉收口，captain 已裁定截图级取证后置）。
4. **16 台全量**：本轮 `SHELL_AGG_MAX_SERVERS=4`（本机 `accounts.json` 16 台），未跑全量。

### 9.8 2026-09-12 03:4x 身份刷新（`H5` 两行过期 → 已对磁盘重算）+ 一次本人的过失与工具修复

**起因**：`verifier` 修好自己门禁的记账缺陷（`H5` 原先不计入 `SUMMARY`/退出码）后暴露本文 `:321/:322` 两行身份过期（`CHECK|H5=FAIL|mismatch=2`、`EXIT=1`）。

**过失（如实报）**：我先用一条重跑脚本去刷这两行，**脚本里 `SHELL_SELFTEST_FAVORITES=1` 设在了调用之后** ⇒ 三次重跑都没写文件，而脚本每轮开头 `Remove-Item` 旧文件 ⇒ **`t31-favorites-selftest.txt` 被我删掉了**（`H5` 之外又多了 `H2b` 的"引用不存在"）。同时 `t31-aggregate-selftest.txt` 被**追加污染**到 18,901 B / 217 行 —— 其中 117 行是 `poster-image-skip-budget` 之类的**卡片级日志**（某个跑到聚合视界的长驻实例一路 append 到 03:36:06）。根因是**设计缺陷**：证据写盘把"报告（整体覆盖）"与"卡片级日志（追加）"混在同一个文件里。

**工具修复（本次一并落地）**：
1. `AggregateDiagnostics`：卡片级日志改写到 **`t31-{aggregate|favorites}-cardlog.txt`**（追加），报告仍写 `t31-{screen}-selftest.txt`（覆盖）⇒ 两者不再互相污染（本轮已可见 `t31-favorites-cardlog.txt` 4,856 B / `t31-aggregate-cardlog.txt` 16,550 B 与两份报告**并存**）。
2. `AggregateDiagnostics` / `PersonEvidence` / `CollectionEvidence`：新增 **`SHELL_EVIDENCE_DIR`**（绝对路径）覆盖证据目录 —— 隔离输出（`-p:OutputPath=E:\ui2-bin\`）时 `AppContext.BaseDirectory` 上溯 5 级会落到 `E:\`，必须能显式指回仓内。

**重跑读数（隔离输出，不是主线 bin）**：

```
构建：dotnet build shell\App\AIPlayer.Shell.csproj -c Debug -p:OutputPath=E:\ui2-bin\ -v m -nologo
      HEAD=6db8efc @ 03:40:30 → EXIT=0 / 0 错误 / 1172 警告；E:\ui2-bin\AIPlayer.Shell.exe
      291,328 B / sha12 4E78174F99BA / mtime 03:40:57
运行：$env:SHELL_EVIDENCE_DIR='E:\AI Player\shell\Tests\evidence' + 各自的自检 env（见 §9.2 表）
      → 每屏各自起窗、跑完**自己停**（pid 12180/6932 → 收藏；13976/4524 → 聚合视界），残留 0
```

| 文件 | bytes / 行 | sha12 | utc（文件内） | 失败断言 |
|---|---|---|---|---|
| `t31-favorites-selftest.txt`（**历史轮次，非当刻身份**：其字节已被 04:05:28 那轮覆盖，当刻盘上 = `C565252A8DBD`；见 §9.8.1 与 §9.9） | 6,618 / 85 | `C8AB14D320B9` | `2026-09-11T19:41:50Z` | **0** |
| `t31-aggregate-selftest.txt` | 7,446 / 88 | `DC1C0B0F3823` | `2026-09-11T19:43:54Z` | **0** |

⇒ `:321/:322` 两行已按**当刻磁盘**重算并更新（大写 sha12 + 字节 + 行 + utc）；`:326/:336/:358` 三处的**行号引用**也随新文件重排同步更新（旧引用指向的行已经不是那句话）。
⚠️ **逐轮数据会有差异**（同一屏不同轮：收藏本轮是 `3 台有内容`，早前一轮是 `2 台有内容`）——**不是回归**，是服务器可用性随轮变化；引用具体数值时以**该轮 artifact** 为准。

### 9.8.1 2026-09-12 16:3x 追加：§9.8 表格行是**历史 artifact** 读数 + 一条 `H5` 覆盖面缺口（append-only，上文一字未改）

**起因**：`verifier` 复报本文 `:321/:322` 身份过期（其引文为 `6,517 / 84 + F123F7B97D2A` 与 `6,118 / 83 + E97ABFAAB1AD`）。**当刻实测：这两个引文已经不在本文里** —— 它们是 §9.8 那次刷新**之前**的一代读数；:321/:322 已在 §9.8 按磁盘重算（见本节末表）。当刻当刻读数（`16:37:03.417 → 16:38:51.185 @HEAD 8ee0bc2`，命令 `powershell -NoProfile -ExecutionPolicy Bypass -File .\shell\tools\evidence-hygiene-check.ps1`）：

```
CHECK|H5-evidence-identity|verdict=PASS|measured=scannedRows=51 mismatch=0 absent=0
SELF-CHECK|checks=10|pass=10|fail=0|inconclusive=0|sum=10|equal=True|at=2026-09-12 16:38:51.131
SUMMARY|verdict=PASS|checks=10|pass=10|fail=0|inconclusive=0|scannedFiles=213|citedNames=183     EXIT=0
```

**本节要补的是另一件事（比那两行更值得修）**：§9.8 的表（上行 `| 文件 | bytes / 行 | sha12 | utc | 失败断言 |`）里 `t31-favorites-selftest.txt` 那一行的 `C8AB14D320B9` 是**该轮（03:4x）artifact** 的读数，而该 artifact 已在 **04:05:28** 被后续轮次覆盖 ⇒ 行内身份**在盘上已无对应物**（`t31-aggregate-selftest.txt` 那行仍与盘一致）。该行应读作"**历史轮次读数**"，不是"当刻身份"。

为什么它没被 `H5` 扫出来（工具覆盖面缺口，**已报 `verifier`**）：`evidence-hygiene-check.ps1` 的 `H5` 行识别（`:773`）要求该行含 `evidence[/\\]<文件名>`；§9.8 的表用的是**裸文件名**（`| \`t31-favorites-selftest.txt\` | …`）⇒ 这类行**不进 H5 的 51 行扫描面**。⇒ 结论：**带 `evidence/` 全路径的行会被校验，裸文件名行不会**；引用身份时一律写全路径，否则"文档写了过期身份"这件事可以长期不被门禁发现。

**当刻有效身份（全路径写法，供 `H5` 校验；实测于 16:37）**：

| 证据文件 | bytes / 行 | sha12 | 磁盘 mtime |
|---|---|---|---|
| `shell/Tests/evidence/t31-favorites-selftest.txt` | 6,618 / 85 | `C565252A8DBD` | 2026-09-12 04:05:28.395 |
| `shell/Tests/evidence/t31-aggregate-selftest.txt` | 7,446 / 88 | `DC1C0B0F3823` | 2026-09-12 03:44:24.274 |

⚠️ 这两个证据文件当刻在**工作区是脏的**（`git status` = ` M`，未提交）⇒ 本节身份行跟的是**工作区字节**；提交前不要拿 HEAD 版复算这两行。

## §9.9 证据写盘模式（覆盖式 / 追加式）与身份口径（2026-09-12 18:4x 追记，供 `verifier` 的 H5 取稳定身份）

**为什么补这一节**：同一目录里两类文件的写盘语义**相反**，混写就会制造"任何追加都让身份行立刻过期"的假红（本队事实 232 的根因）。本节把四件逐一钉死。

| 文件 | 模式 | 生产者 | 当刻身份（实测） | 可否按身份引用 |
|---|---|---|---|---|
| `shell/Tests/evidence/t31-aggregate-selftest.txt` | **覆盖式报告**（`AggregateDiagnostics.WriteEvidence` → `File.WriteAllText`，自检结束时整体写一次） | 聚合屏自检 | 7,446 B / `DC1C0B0F3823` / 88 行 / tracked · porcelain 空 | ✅ 可（每轮换代，引用须带采样时刻） |
| `shell/Tests/evidence/t31-favorites-selftest.txt` | **覆盖式报告**（同上，屏后缀 = `favorites`） | 收藏屏自检 | 6,618 B / `C565252A8DBD` / 85 行 / tracked · porcelain 空 | ✅ 可（同上） |
| `t31-aggregate-cardlog.txt` | **追加式日志**（`AppendToFile` → `File.AppendAllText`，渲染路径逐行） | 聚合屏 | 随运行增长（不列 bytes） | ❌ **不得**按 bytes/sha12 引用 |
| `t31-favorites-cardlog.txt` | **追加式日志**（同上） | 收藏屏 | 随运行增长（不列 bytes） | ❌ **不得**按 bytes/sha12 引用 |

三条口径：
1. **名字即模式**：报告 = `*-selftest.txt`，日志 = `*-cardlog.txt`（实现见 `AggregateDiagnostics.cs` 的 `EvidencePath` / `CardLogPath` 与 `:124-134` 的派生规则；`PersonEvidence.cs` / `CollectionEvidence.cs` 同构）。
2. **追加式文件永远不进身份行**：要引它们只写"存在 + 模式 = 追加式"。上表对两个 cardlog 刻意只写**裸文件名、不写 `evidence/` 全路径、不列 bytes** —— 裸名不进 H5 扫描面，写全路径反而会把它变成一条必然过期的身份行。
3. **订正 §9.8 末行的过期注记**：那句"这两个证据文件当刻在**工作区是脏的**（` M`，未提交）"**已过期** —— 当刻实测四件全部 `tracked`，`git status --porcelain` **空**（干净），已随入库提交收进去了。
