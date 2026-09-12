# SEAM② 接线规格：聚合搜索「逐源增量」UI 落地（出题方 `ui2` ⇒ 落地方 `ui3`）

> 归属裁定（captain 2026-09-12，第三次明确）：**SEAM② = (C) 分工** —— `ui2` 只出「接线规格 + 四条反控清单」（本文件），`ui3` 在 `t32` 内落地，`ui2` 独立复核。
> **`ui2` 不授权改 `Features/Search/**`**（那是 `ui3` 的 `t28` 领地）；本文件是**规格**，不是实现。
> 服务层（`services`，`t44`）已交付并过自检：`shell/Services/AGGREGATION_INCREMENTAL.md`、`shell/Services/Aggregation/AggregatedSearchService.cs`。

## 0. 三态（本文件自己的状态）

| 态 | 内容 |
|---|---|
| **逆向到了** | 用户原话（`AGGREGATION_INCREMENTAL.md:3-4`）：「查看收藏和聚合搜索时，不是每次都要测全部服务器，可以同时并发，然后按照得到结果先后顺序，一个个显示，后得到的结果在下面」 |
| **重建实现了** | 服务层 `SearchIncrementalAsync(...)` + `AggregatedSearchProgress`（`t44` 已交付、`ServiceSelfCheck` S7①–④ 全绿） |
| **运行验证过了** | **服务层**：S7① 实测 5 源（5/20/300 ms + 坏源）回调 5 次、首个回调 = 5 ms 源。**UI 侧：未接** —— 当刻 `SearchRunner.cs:264` 仍调 `SearchAsync`（一次性 `Task.WhenAll`）⇒ 屏幕上仍是"全跑完才出结果"，与用户要求的那一条**尚未生效** |

## 1. 当刻接线点（实测，行号会漂、锚文本不会）

`shell/App/Features/Search/SearchRunner.cs`：

- `:155` `public async Task<SearchRunResult> RunAsync(string term, SearchChip chip, CancellationToken cancellationToken = default, bool injectBadSource = false)`
- `:264` `var aggregated = await service.SearchAsync(query, AggregatedSearchService.DefaultLimit, cancellationToken).ConfigureAwait(false);`
- `:267-294` 一次性把终值写进 `result`（四个计数 + `FailedServers` + `Cards`）—— **这就是"必须等全部源回来"的那一处**
- `SearchPage.xaml.cs` 消费 `SearchRunResult` 渲染网格与摘要行

## 2. 最小改动接线（不改签名语义；`SearchAsync` 路径必须原样保留）

**规则：`SearchIncrementalAsync` 是纯加法 ⇒ 接线也必须是纯加法。** 参数缺省（`progress == null`）时走**原** `SearchAsync` 分支 ⇒ 既有回归（`t28` 的四路真跑）逐字不变。

```
// SearchRunner.RunAsync：新增一个**可选**参数（放在最后，不动既有调用点）
public async Task<SearchRunResult> RunAsync(
    string term, SearchChip chip, CancellationToken cancellationToken = default,
    bool injectBadSource = false,
    IProgress<AggregatedSearchProgress> progress = null)      // ← 新增

// RunAggregatedAsync 内：
if (progress == null)
{
    var aggregated = await service.SearchAsync(query, DefaultLimit, cancellationToken);   // 原路径，一字不改
    ...填 result（现有代码）
}
else
{
    // 每次某源返回 ⇒ progress.Report(p)：UI 立刻用 p.Items 重画
    var aggregated = await service.SearchIncrementalAsync(query, DefaultLimit, progress, cancellationToken);
    ...填 result（同一段赋值逻辑，**终值必须还是这一个返回值**）
}
```

UI 侧（`SearchPage.xaml.cs`）：`new Progress<AggregatedSearchProgress>(p => Render(p))` —— `Progress<T>` 自带 `SynchronizationContext` 捕捉 ⇒ 回调直达 UI 线程，**不要**自己 `DispatcherQueue.TryEnqueue` 再包一层（会乱序）。

**渲染必须就地替换，不得整屏重建**：`p.Items` 是**已合并**的当前快照（后到源的新条目排在下面）。渲染时只做「补齐新增 / 更新已有」，不要清空再填（清空=闪屏+丢滚动位置）。

## 3. 四条反控（逐条**可判 False**；缺原始输出即判未验）

> 每条都要**原始日志行 + 时刻**；判据写在断言里，写"看着对"不算。

### 反控 ① 增量真的"一个个显示"（不是全跑完才显示）
- **做法**：`t41-probe`/夹具造 **5 源**（延迟 5 / 20 / 300 ms + 1 坏源），或本机真实多源。
- **判据（可 False）**：渲染**次数 ≥ 2**；**首次**渲染的时刻 **早于** `ElapsedMs`（终值耗时）。若渲染次数 == 1 且时刻 == 终值 ⇒ **FAIL**（等于没接）。
- **注意**：`Progress<T>` 在 UI 线程排队，若一次 `await` 内所有源都已返回，回调仍会连发 ⇒ 判据必须比**时刻**，不能只数次数。

### 反控 ② 同名片不出现第二张卡（就地合并）
- **做法**：造**同名同类型同年**两条（跨两源），或本机真实同名片。
- **判据**：卡片数**不增**（不是 2 张）；该卡 `Hits=2`、`Sources=2`、`HasAlternatives=true`；`MergedCount` 与 `Items.Count` **分别打印**。
- **反例形态**：屏幕上出现两张标题相同的卡 ⇒ FAIL（`KeyOf` 合并被绕过）。

### 反控 ③ 计数口径逐字取 §7.6，**不写"三数一致"**
- **判据**：四个数各自具名、分别出现在证据里，且**取自回调字段**（`TotalHits` / `MergedCount` / `MergedHitCount` / `Items.Count`），**不得**由 UI 自己重算或用另一个名字。
- **终值等价**：`IsFinal=true` 那一发的四个数 == `SearchAsync` 终值（同源同词逐字段一致，服务层 S7② 已证；UI 侧只需证明"用的就是那一发"）。
- 🔴 判据行里**禁止**出现"三数一致"这类合并表述（`SourceCount` / `MergedCount` / `TotalHits` 按设计本就不相等）。

### 反控 ④ 坏源不清空已到达结果
- **做法**：注入 1 台不可达源（`SearchRunner.BadSourceUrl` 已有）或命中本机真实坏源。
- **判据**：该源那一发（`FailedThisTime=true`）到达时，**先前已渲染的卡片仍在**（数量不减）；失败源名出现在 `FailedServers` 与摘要行；终态 `IsFinal=true`。
- **反例形态**：坏源到达把网格清空 / 整屏只显红字 ⇒ FAIL。

### 反控 ⑤ 到达序 vs 终值序（设计边界，必须显式处置）
- `AGGREGATION_INCREMENTAL.md:32`：增量顺序 = **到达序**（用户要的），终值 `SearchAsync` 顺序 = 相关度 → 名称 → 服务器名。
- **判据**：`IsFinal=false` 时按到达序；`IsFinal=true` 后**要么**切到终值序、**要么**在证据里写明"不切换（保持到达序，避免用户已在看的卡片跳位）"并给出理由。**沉默不写 = FAIL**（这是设计选择，不是可忽略细节）。

## 4. 边界与纪律（照抄，别自行放宽）

1. `Features/Search/**` 是 `ui3` 的；`MainWindow.*` / `Theme/Tokens.xaml` / `App.xaml` 是 `ui` 的；`shell/Services/**` 是 `services` 的。**要改就发消息请求，不要自己动。**
2. **不得新建第二条取数通道**：一律经 `AggregatedSearchService`（`SearchAsync` 或 `SearchIncrementalAsync`，二者同源）。
3. 证据文件 `.txt`（`.log` 现已可入库，但手写报告仍用 `.txt`）；引用到的文件名必须**真实存在**（门禁 `H2b`）。
4. 报告三态分开写；做不出写 `INCONCLUSIVE` + **缺的前提**（例如"本机只有 1 台 Emby 可用 ⇒ 无法造多源到达序"）+ 阻塞卡号。

## 5. 我方（`ui2`）复核口径（独立复核，不代他改代码）

收到 `ui3` 的 `t32` 结单后，我按下列四步**独立**复核，结论只有三档：`pass` / `needs_revision(带行号)` / `INCONCLUSIVE(缺前提)`：

1. **读源码**：`SearchRunner.cs` 里 `progress == null` 分支是否仍逐字等于原 `SearchAsync` 路径；`SearchIncrementalAsync` 是否**只在** progress 非空时被调。
2. **读原始证据**：四条反控各自的原始行（渲染时刻、四个计数、坏源那一发、`IsFinal`）。
3. **复算**：至少对反控 ②④ 用证据里的数自行复算一遍（不信结论行，信原始行）。
4. **门禁**：`shell/tools/evidence-hygiene-check.ps1` 的 `H2b`（引用存在）+ `H3b`（新增裸 catch = 0）。

---

## 6. 落地口径补充（2026-09-12 与 `ui3` 对齐；**本节即裁决，聊天里不再另立口径**）

### 6.1 行前缀与字段（点名，不再由落地方自定）

证据文件里每条原始行都用**固定前缀 + 具名字段**（`|` 分隔，值里不得含 `|`）：

```
SEAM2-CALLBACK|seq=<n>|t=<ms>|server=<显示名>|total=<TotalHits>|merged=<MergedCount>|mergedHit=<MergedHitCount>|items=<Items.Count>|isFinal=<true|false>|failedThisTime=<true|false>
SEAM2-RENDER  |seq=<n>|t=<ms>|renders=<累计渲染次数>|cards=<屏上卡片数>|first=<true|false>
SEAM2-FINAL   |t=<ms>|total=<…>|merged=<…>|mergedHit=<…>|items=<…>|elapsedMs=<SearchAsync 终值耗时>
SEAM2-FAIL    |t=<ms>|server=<显示名>|failedThisTime=true|cardsBefore=<…>|cardsAfter=<…>
SEAM2-ORDER   |decision=<switch|keep>|reason=<一句话理由>|arrivalSeq=<…>|finalSeq=<…>
```
- `<n>` 从 1 起；`t` 一律是**自检索开始单调递增**的毫秒（`Stopwatch`，保留 1 位小数），**禁止用 `DateTime`**（会被时钟调整骗）。
- 四个计数**必须逐字取自回调字段**并以这四个名字出现（`TotalHits` / `MergedCount` / `MergedHitCount` / `Items.Count`）；**不得**改名、不得 UI 自算、不得写"三数一致"。

### 6.2 「一个个显示」的判据（比时刻，不只数次数）

- 可 False 判据：`SEAM2-RENDER` 行 **≥ 2 条**，且**首条** `t(first) < SEAM2-FINAL.t`（差值 ≥ 100 ms 才算可判；差值 < 100 ms 时**不得**声称增量生效，必须写 `INCONCLUSIVE(同 tick)` 并给出两次的原始 `t`）。
- 夹具延迟要**拉开**：沿用 5/20/300 ms + 1 坏源即可，但**判据只看 first 与 final 的差**（5 ms 与 20 ms 之间不构成证据）。
- 落地方原提的「首次渲染时刻 + 累计渲染次数 + 终值到达时刻 各一行」**采纳**，字段按 6.1 写；`renders=` 必须在每条 `SEAM2-RENDER` 上出现（累计值），防"`Progress<T>` 排队把次数骗过去"。

### 6.3 到达序 vs 终值序（⑤ 的落地形态）

- **推荐 `decision=keep`**（`IsFinal=true` 后保持到达序，就地替换，避免用户正在看的卡跳位）；但**必须**有一条 `SEAM2-ORDER|decision=keep|reason=…`。
- 若选 `switch`，必须给出切换前后**同一个 key 的位置序号**（`arrivalSeq` / `finalSeq`）与"用户可见影响"的一句话说明。**沉默不写 = FAIL**。

### 6.4 证据文件与命名

- 落 `shell/App/Features/Search/evidence/` 下 **新文件**（不要覆盖已入库的 `t28-*`），建议 `seam2-incremental-live.txt`；`.txt`，UTF-8 无 BOM。
- 证据必须**自带四件套**：构建命令 + 时刻 + `HEAD` + 是否 `-t:Rebuild`，并附**被加载产物** `dll sha12 + mtime`（≥ 相关源码 mtime）。
- 引用到的文件名必须真实存在且**与文档同一批入库**（门禁 `H2b`/`H2a`）；新代码**不得**新增裸 catch（`H3b`）与 emoji（`H7`）。

### 6.5 证据强度：UI 输入不可驱动的如实写法（`ui3` ④ 的裁决）

- 采纳"宿主/自检**直调** + 原始行"的形态，但必须**逐字标注**：`证据形态=代码路径 + 直调（未经用户键盘输入）`。
- 直调必须打到**与 UI 同一个入口函数**（`SearchRunner.RunAsync(..., progress)`），渲染侧必须是**真的 `SearchPage` 渲染路径**；若渲染侧只能走替身，则该侧判据一律 `INCONCLUSIVE`，只许声称**回调侧**已验（不许把"回调对了"写成"屏幕对了"）。

### 6.6 复核口径增量（我方 `ui2`，独立复核时照此执行）

1. 先读 `SearchRunner.cs`：`progress == null` 分支与改动前**逐字相同**（要求落地方在证据里贴该分支的 `git diff` 片段或函数原文）；`SearchIncrementalAsync` **只在** `progress != null` 时被调。
2. 用证据里的原始行**自行复算** ②④（同名卡 `Hits=2/Sources=2`、坏源到达前后 `cardsBefore/cardsAfter`），不信结论行。
3. 时刻判据按 6.2 复算（first vs final 的差）。
4. 门禁读 `H2a/H2b/H3b/H7`；任何一条红且属落地方文件 ⇒ `needs_revision(带行号)`。

---

**本文件身份**：由 `ui2` 于 `t54` 内产出，§6 于 2026-09-12 与 `ui3` 对齐后追加（`t54` 边界条款要求 "SEAM② 你只出规格 + 反控清单，由 `ui3` 落地，你独立复核"）。
**落地方**：`ui3`（在 `t32` 内承载，卡归属按其卡面声明写 `t32`）。**复核方**：`ui2`。
**领地**：本文件在 `ui2` 的 `Features/Aggregate/`；`Features/Search/**` 属 `ui3`，`ui2` 不落一行。

---

## 6.7 追加裁决（2026-09-12，回应 `ui3` 三问；与 §6 同级，聊天里不再另立口径）

### 6.7.1 ① `progress == null` 分支 = **走 (b) 显式分支，逐字保留原语句**

**裁决 = (b)**。理由：§5.1 / §6.6.1 的"逐字相同"是**机械可判**判据（`git diff` 可证），而"行为等价"只能靠论证；这条判据存在的唯一目的就是保护 `t28` 四路真跑的回归面 —— 把它降级成论证等于自毁判据。`(b)` 的代价（文件身份前进一次）**本来就已经付过**（三元表达式那次落地即已前进），不构成额外成本。

原语句（改动前一代 `a1e7a40^:shell/App/Features/Search/SearchRunner.cs:264-265`，**含续行与 8 空格缩进，逐字**）：

```
        var aggregated = await service.SearchAsync(query, AggregatedSearchService.DefaultLimit, cancellationToken)
            .ConfigureAwait(false);
```

落地形态（`SearchIncrementalAsync` **只许**出现在 `else`）：

```
if (progress == null)
{
    var aggregated = await service.SearchAsync(query, AggregatedSearchService.DefaultLimit, cancellationToken)
        .ConfigureAwait(false);
    ...现有"填 result"逻辑，一行不改
}
else
{
    var aggregated = await service.SearchIncrementalAsync(query, AggregatedSearchService.DefaultLimit, progress, cancellationToken)
        .ConfigureAwait(false);
    ...同一段"填 result"逻辑
}
```

硬要求：
1. null 分支那**两行**必须与上面引用块**逐字相同**（连缩进）；证据里贴该分支的 `git diff` 片段或函数原文。
2. 两个分支的"填 result"逻辑**只留一份**（各自 await，共用后续赋值；或抽成局部函数）——**禁止复制两份赋值**，复制即两处口径会漂。
3. **不得覆盖**已入库的 `t28-*` 证据；只在文档/证据里追加一句"本次重构：null 分支字面未变、行为未变"。

### 6.7.2 ② 日志行前缀 = **双发**（旧行一行不改 + 新 `SEAM2-*` 新增 + 映射表）

**裁决 = 双发**。旧前缀由**代码**发出、且被已入库证据引用（当刻实测：`SearchPage.xaml.cs:464` `SEARCH-INCREMENTAL-CLOSE`、`:479` `-OVERFLOW`、`:576` `-CAPPED`、`:1006` `SELFCHECK-INCREMENTAL`）⇒ 删改会让已入库证据**不可复跑**。新判据一律走 `SEAM2-*`。映射表（落地方证据里落一张，逐字照此）：

| 旧行（原样保留） | 新行（§6.1 判据用） | 关系 |
|---|---|---|
| `SELFCHECK-INCREMENTAL renders=… firstRenderMs=…` | `SEAM2-RENDER\|seq\|t\|renders=<累计>\|cards\|first=<true\|false>` | 新行**逐发**写；旧行保持"一条汇总" |
| `SEARCH-INCREMENTAL-CLOSE rendered=… terminalCards=…` | `SEAM2-FINAL\|t\|…\|elapsedMs=…` | 旧行 = 收尾汇总；新行 = 终值那一发 |
| `SEARCH-INCREMENTAL-OVERFLOW` / `-CAPPED` | 无对应（护栏行） | **原样保留**，不进 `SEAM2-*` |

### 6.7.3 ③ 其余逐条确认（与 §6.1–§6.5 一致，无修改）

- `t` = `Stopwatch` 单调毫秒（保留 1 位小数），**禁 `DateTime`**（§6.1）。
- 四计数逐字 `TotalHits` / `MergedCount` / `MergedHitCount` / `Items.Count`；UI 不自算、**禁写"三数一致"**（§6.1、§3③）。
- `SEAM2-RENDER` 每条都带 `renders=<累计>`（§6.1、§6.2）。
- 可判性：`SEAM2-RENDER` ≥ 2 条 **且** `t(first) < SEAM2-FINAL.t`，**差 ≥ 100 ms**；差 < 100 ms ⇒ `INCONCLUSIVE(同 tick)` + 贴两次原始 `t`（§6.2）。夹具 5/20/300 ms + 1 坏源，**5 与 20 之间不作证据**（§6.2）。
- 顺序默认 `decision=keep`，**必写** `SEAM2-ORDER|…|reason=…`；沉默 = FAIL（§6.3）。
- 证据形态逐字标注 `证据形态=代码路径 + 直调（未经用户键盘输入）`；直调入口 = `SearchRunner.RunAsync(..., progress)`；渲染侧若非真 `SearchPage` 渲染路径 ⇒ 该侧 `INCONCLUSIVE`，**只许**声称回调侧已验（§6.5）。
- 落**新文件**（放在 `Features/Search/` 的 evidence 目录下，文件名 `seam2-incremental-live.txt` —— 此处**故意不写目录前缀**：门禁 `H2b` 的书写规则是"只提及不引用"的件名必须写成裸名或反引号裸名，写成带目录前缀的形态会被当作**引用**，而该件此刻尚未存在 ⇒ 会当场把门禁打成 `INCONCLUSIVE`）。要求：`.txt`、UTF-8 无 BOM；带四件套（构建命令 + 时刻 + `HEAD` + 是否 `-t:Rebuild`）与被加载 `dll sha12 + mtime`；**不覆盖** `t28-*`；文档与证据**同一批**入库；不新增裸 catch（H3b）与 emoji（H7）（§6.4、§6.6.4）。

### 6.7.4 两处**收紧**（§6.1/§6.2 的落地歧义，先钉死免得返工）

1. `SEAM2-FINAL|elapsedMs=` 在增量路径上 = **本次增量调用返回（终值）的耗时**。**禁止**为了填这个字段再跑一次 `SearchAsync`（违反 §4.2"不得新建第二条取数通道"，还会让服务器负载翻倍、`t` 口径混乱）。确需 `SearchAsync` 终值耗时作对照时，另开一条**明确命名**的对照臂行（如 `SEAM2-CTRL-SYNC|elapsedMs=…`）并在证据里写明"这是第二次请求"。
2. `SEAM2-FINAL.t` = **`IsFinal=true` 那一发回调**的 `t`（最后一次到达），**不是**宿主打印汇总行的时刻；两者不同时以回调为准，并在证据里同时贴出两个值。

**本节身份**：`ui2` 于 2026-09-12 追加（回应 `ui3` 三问）；`§6` 仍是唯一口径，本节为其落地细则。


