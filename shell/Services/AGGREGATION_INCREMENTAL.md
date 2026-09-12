# t44 · 增量聚合搜索 + 通用 SWR 快照（服务层交付说明）

> 来源：用户原话「查看收藏和聚合搜索时，不是每次都要测全部服务器，可以同时并发，然后按照得到结果先后顺序，一个个显示，后得到的结果在下面……
> 收藏就可以加一个缓存，先显示缓存，然后再查询。」
> 本文件只描述**服务层 API 与接线方式**；UI 侧改动归 `ui`/`ui2`/`ui3`。

## 1. 聚合搜索逐源增量（`Services/Aggregation/AggregatedSearchService.cs`）

**纯加法**：既有 `SearchAsync(term, limit, ct)` 的签名 / 返回 / `Task.WhenAll` 行为**一字未改**。

```csharp
// 新方法（第 4 个参数是可选 IProgress，传 null 等价于 SearchAsync）
Task<AggregatedSearchResult> SearchIncrementalAsync(
    string term, int limit = 60, IProgress<AggregatedSearchProgress> progress = null, CancellationToken ct = default);
```

`AggregatedSearchProgress`（每次**某个源返回**时回调一发）：

| 字段 | 含义 |
|---|---|
| `ArrivedServerName` / `FailedThisTime` | 本次返回的源（失败源也会到达） |
| `ArrivedSourceCount` / `PendingSourceCount` / `SourceCount` | 已回 / 待回 / 总数（UI 可显示"3/7 已回"） |
| `Items` | **当前**合并条目快照，顺序 = **条目首次到达的顺序**（后到源的新条目排在下面）；已按 `limit` 截断 |
| `FailedServers` | 当前失败源清单（`ServerName`，口径同 `SearchAsync`） |
| `TotalHits` / `MergedCount` / `MergedHitCount` | 四个计数的**当前快照**（边跑边变），口径与 `SearchAsync` **逐字一致** |
| `IsFinal` | true = 最后一发，此时上述计数即终值 |

**🔴 三条不许踩的边界**
1. **去重**：`Items` 传的是**已合并**的条目；后到源命中同一 `KeyOf`（`ProviderIds → 类型|名称|年份`）时**就地合并**（同一条目 `Hits` 追加、`HasAlternatives`/`SourceLabel` 随之更新），**不会**多出第二张卡。
2. **计数**：`MergedCount` 是去重后条目数（**不受 `limit` 影响**），`Items.Count` 已被 `limit` 截断，`TotalHits` 在跳过空 `Id` **之前**自增 —— 别自己再造一套名字。
3. **终值等价**：返回值由**既有** `AggregatedSearchResult.FromItems` 产出 ⇒ 与 `SearchAsync` 在相同源/词下逐字段一致（自检 S7② 实测）。
   增量回调的顺序是「到达序」，与终值的「相关度 → 名称 → 服务器名」排序**不同**，这是设计（`IsFinal=true` 后 UI 可切到终值顺序）。

**App 侧接线（一行）**
```
SEAM②: 搜索页改为 var progress = new Progress<AggregatedSearchProgress>(p => 渲染(p.Items, p.IsFinal, p.PendingSourceCount));
        await _aggregated.SearchIncrementalAsync(term, 60, progress, ct);
```

## 2. 通用 SWR 快照助手（`Services/Storage/SwrSnapshotCache.cs`）

收藏 / 首页 / 聚合视界**共用一件**（不三处各写一遍）；底层复用 t33 的 `DiskCacheStore`（SHA1 文件名 / 原子覆盖写 / LRU / 条数+字节双上限）。

```csharp
var cache = new SwrSnapshotCache<List<EmbyItem>>(dir, TimeSpan.FromMinutes(10));
var lookup = cache.TryRead("favorites:srv-1");           // ① 零网络：Fresh / Stale / Miss
if (lookup.HasValue) Render(lookup.Value);               // ② 先把缓存显示出来
var refreshed = await cache.RefreshAsync("favorites:srv-1", ct => LoadFromServer(ct), ct);  // ③ 后台刷新
if (refreshed.Success) Render(refreshed.Value);          // ④ 刷新完成 ⇒ 面板自动替换（不需用户重进）
```

- **快照落盘位置**：构造函数给的目录（建议 `AppDataDir` 下的独立子目录，如 `…\swr\`）；文件名 = `SHA1(key).cache`。
- **TTL**：构造时给（`SwrSnapshotCache(directory, ttl)`）；`Fresh` = 未过期，`Stale` = 过期但**仍可先显示**（随后必须刷新）。
- **时间戳写在快照信封里**（`{"cachedAtUtc":…,"value":…}`）—— **不用文件 mtime**：`DiskCacheStore.TryRead` 会为 LRU 触碰 mtime，用 mtime 算 TTL 会永远得到 `age≈0`（本轮实测踩到）。
- **失效条件**：① 超 TTL（⇒ Stale）② `Invalidate(key)`/`Clear()` ③ 反序列化失败 ⇒ **Miss**（坏缓存**不得**当 Fresh 显示半截内容）。
- **便捷组合**：`ReadThenRefreshAsync(key, fetch, onStaleValue, ct)` = 「先读缓存（有则同步回调 `onStaleValue`）→ 再刷新」。
- 刷新**失败不抛异常**（`Success=false` + `Error`），**失败不落盘**（不污染旧快照）。

**App 侧接线（一行）**
```
SEAM③: 收藏页进入 ⇒ lookup = cache.TryRead(key)；有值先渲染；随后 await cache.ReadThenRefreshAsync(key, fetch, onStaleValue: l => 渲染(l.Value))
```

## 3. 本轮顺带修掉的服务层缺陷

`Infra/DiskCacheStore.cs` 的 `Clear()` 原来只枚举 `*.tmp` ⇒ **删不掉任何 `.cache` 条目**（实测返回 0、快照仍在；SWR 的"清缓存 ⇒ 退化为冷启动"反控因此失败）。已改为 `*.cache` + `*.tmp` 都删。

## 4. 自检（`ServiceSelfCheck`，48 → 52 步，全绿才有意义）

```
S7① 逐源增量：5 源（5/20/300 ms + 坏源）⇒ 回调 5 次、首个回调 = 5 ms 源、快源#1 < 慢源#4
     终值 条目 3 / 合并 3 / 命中 4；同名片 Hits=2 Sources=2 HasAlternatives=True（无第二张卡）；坏源进 FailedServers 且不影响已到达结果
S7② 回归：增量终值 == 既有 SearchAsync（条目/合并/命中/总计/失败数/顺序 逐字段一致）
S7③ SWR：冷 Miss→网络（计数 1）；热启动回调时网络计数仍为 1（**尚未再发请求**）⇒ 刷新后新值；Clear 后 TryRead ⇒ Miss
S7④ SWR 反控：坏快照 ⇒ Miss（不当 Fresh）
```
