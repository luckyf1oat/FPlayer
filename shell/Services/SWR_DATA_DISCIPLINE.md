# 通用数据加载纪律（SWR）· 服务层交付说明（对应 `UI_SPEC_SHELL.md §12`）

> 用户原话：「刚才说的那个缓存逻辑**各功能都可以复用**，就是先读取缓存、同时向服务器请求内容，**能避免空窗期**。」
> 本文件 = 服务层对 §12 的**唯一入口**与取证方式；**消费（渲染/滚动位置/选中态）归 `ui`/`ui2`/`ui3`**。

## 唯一入口（§12.6：一个以「缓存键 + 取数委托」为参数的通用件，各屏**禁止**自写一套）

```csharp
// shell/Services/Storage/SwrSnapshotCache.cs
var cache = new SwrSnapshotCache<List<EmbyItem>>(dir, TimeSpan.FromMinutes(10));   // 一个实例可服务多屏多键
SwrLoadResult<List<EmbyItem>> r = await cache.LoadAsync(
    key,                                    // 缓存键（调用方用 serverId 等前缀区分作用域）
    fetch:  ct => LoadFromServer(ct),       // 取数委托（真实网络/服务调用）
    onCacheValue: lookup => Render(lookup.Value),     // T0 同步渲染旧内容（有缓存时）
    idsOf:  items => items.Select(i => i.Id),         // §12.4④：两组 id 分别打印
    cancellationToken: ct);
```

**A 部分（逐源增量 `SearchIncrementalAsync`）与本节（缓存优先）是两件事，不是同一个 API**（§12.6 明令）；
两者可叠加使用：先 `LoadAsync` 出缓存，再由 UI 用增量回调往里并新结果。

## §12.1 三个时刻（实测取证 S8①）

| 时刻 | 服务层做什么 | UI 做什么 |
|---|---|---|
| **T0** | `LoadAsync` **同步**读盘（零网络）；命中 ⇒ **立即**回调 `onCacheValue`；**同一时刻**已把 `fetch` 发出（先起请求再回调渲染） | 直接渲染缓存内容，**不等网络** |
| **T1** | `fetch` 成功 ⇒ 落盘 + 返回新内容；`SwrLoadResult.Value` / `RefreshedIds` 给 UI | **就地替换/增量并入**：不闪屏、不重置滚动、不丢选中态（这三点**属 UI 判据**，服务层只保证"给了你可比对的旧内容 + 新内容 + 两组 id"） |

证据（S8① 原文）：`T0 渲染时 fetch 已发出=True｜fetchStartedAt=16:03:41.957 ≤ renderAt=16:03:41.958`（同一毫秒级时刻，**不是**渲染完再发）。

## §12.2 三态可分辨（禁止把缓存呈现为"这就是最新的"）

`SwrLoadResult` 上的**可取证读数**（每一条都进日志，行首 `SWR-LOAD`）：

| 态 | 字段 / 日志 |
|---|---|
| 缓存态（可能过期） | `CacheHit` / `CacheFreshness`(Fresh\|Stale) / `CacheCachedAtUtc` / `CacheAgeSeconds` → `SWR-LOAD key=… cache=hit age=3.2s ids=[…]` |
| 刷新中 | `RefreshStarted` → `SWR-LOAD key=… refresh=started（与 T0 渲染并发）` |
| 已刷新 | `RefreshSucceeded` + `RefreshedIds` → `SWR-LOAD key=… refresh=done ids=[…]` |
| 失败 | `Error` + `CachePreserved` → `SWR-LOAD key=… refresh=failed …（**保留缓存** cache-preserved=True）` |

`SwrLoadResult.Evidence` 一行可直接进日志/证据：
`key=… cache=hit age=0.0s refresh=done cachedIds=[旧A,旧B] refreshedIds=[旧A,旧B,新增C]`。

## §12.3 失败语义（🔴 禁止清空界面或删缓存）

`fetch` 抛异常 / 返回 `null` / 被取消 ⇒ **一律保留缓存**：`RefreshSucceeded=false`、`Error` 非空、`Cached` 原样返回、
`CachePreserved=true`、**缓存文件不删**（实测 S8④：`cache=hit ✅ refresh=started/failed ✅ cache-preserved=True 缓存内容=2 条 缓存文件仍在=True`）。
UI 必须显示**可见**失败提示（文案素材 = `Error`）；**不得**清空列表。

## §12.4 四条反控（全部实测可失败，S8①–④）

| # | 反控 | 实测 |
|---|---|---|
| ① | 冷启动（无缓存）功能仍可用、退化为骨架屏 | S8③：`cache=miss ✅ Cached=null（骨架屏）｜缓存渲染回调调用次数=0（期望 0）｜refresh=done ✅` |
| ② | 清缓存后热路径**必须退化**（证明内容真来自缓存，而非内存副本） | S8②：`Clear 删除 2 条 ⇒ cache=miss ✅ cachedIds=[] ✅ refresh=done（功能仍可用）` |
| ③ | 坏源 ⇒ **缓存仍在** + 失败提示出现 | S8④：见上（**异步**失败路径：`refresh=started/failed`） |
| ④ | 缓存 id 集合与刷新后 id 集合**分别打印** | S8①：`cachedIds=[旧A,旧B]` 与 `refreshedIds=[旧A,旧B,新增C]` 各打一行（`SWR-LOAD … cachedIds=[…] refreshedIds=[…]`） |

## §12.5 适用范围与一行接线

适用范围（各屏自己决定装哪几屏）：首页（Hero/继续观看/媒体库分区）· 媒体库页 · 详情页 · 收藏 · 聚合视界 · 聚合搜索结果 · 搜索历史 · 服务器状态列表。

```
SEAM④（各屏通用，一行）:
  var r = await _swr.LoadAsync(key, ct => FetchAsync(ct), lookup => Render(lookup.Value), idsOf: x => IdsOf(x), ct);
  if (r.RefreshSucceeded) Render(r.Value);            // T1 就地替换
  else if (r.CacheHit) ShowFailureStrip(r.Error);     // §12.3：保留缓存 + 可见失败
  else ShowSkeleton();                               // 冷启动骨架屏
```

## 其它约束（本轮未被触碰）

- `SearchAsync` 既有语义/签名/`Task.WhenAll` 行为**一字未改**（t44 的 S7② 回归实测：增量终值与它逐字段一致）。
- 去重键仍是 `AggregatedSearchHit.KeyOf`（`ProviderIds → 类型|名称|年份`），靠**就地合并**保住（S7① 同键双源 ⇒ 1 条 `Hits=2 Sources=2 HasAlternatives=True`）。
- 四个计数 `TotalHits`/`MergedCount`/`MergedHitCount`/`Items.Count` 语义未改。
- 磁盘缓存复用 t33 `DiskCacheStore`（原子写 / LRU / 条数+字节双上限）；快照写入时间戳在信封内（不用 mtime）。
