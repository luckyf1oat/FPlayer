# t103【用户报障】收藏/聚合里点击单集也要进详情页 —— 证据

卡：`t103`（kind=work，attempt 1，`attempt_id=e2857c84-e0ba-42ec-bf14-25fd6c7abede`），无依赖。
范围（卡面）：`shell/App/Features/Favorites/`、`shell/App/Features/Aggregate/`、`shell/Tests/evidence/`。**未碰**他人领地。

## 0. 三态分开写

- **逆向到了**：无（这是**重建体的行为缺陷**：用户原话「就算只有单集（收藏里）我也希望点击进入详情页」）。
- **重建实现了**：8 个文件在盘（§2），**最终源码**隔离构建 `EXIT=0 / 0 错误`（§4）。
- **运行验证过了**：真起窗（**重定向沙箱数据根**）一轮，`t103` 自检 **10 条断言全部 True** + 点击后停在详情页的
  截图（§5）；原始读数落三份证据（§6）。

## 1. 用户报障与修法：把两种意图分开

```
用户 → 「就算只有单集（收藏里）我也希望点击进入详情页」
现状 → 点卡片 = 直接起播（`FavoritesPage.OnCardClick` / `AggregatePage.OnCardClick` 都走 `AggregatePlayback.Play`）
修法 → ① **点卡片 = 进详情页**（含 Episode）；② **播放按钮 = 起播**（卡片右下角 ▶，走既有 M3 参数面）；
       ③ 两条意图**共用同一实现** `CardIntents`（两屏各写一份必然漂移）；
       ④ 任何走不通的情形都必须给**可见**理由（"点了没反应"是本卡的判失败情形）。
```
**单集降级**（卡面第 4 条）：条目自身 `Id` 不可用 ⇒ 退到**父剧**（`SeriesId`）详情页并在日志标
`degraded=parent-series`；连父剧都没有 ⇒ `TryOpenDetail` 返回 false + 可见理由（"该集详情不可用…"）。
**快照卡起播**：点播放但拿不到可播放的真条目（只有渲染面快照）⇒ 返回 false + 可见拦因（不静默起播残缺数据）。

## 2. 改动清单（文件级）

| 文件 | 改了什么 |
| --- | --- |
| `Features/Aggregate/CardIntents.cs`（**新**） | **两屏唯一的卡片意图实现**：`TryOpenDetail`（详情，含单集退父剧）/ `TryPlay`（起播，含快照拦因）；观察点 `LastIntent` / `LastDetailReading` |
| `Features/Aggregate/AggregatePoster.cs` | 新增 `PlayGlyph`（`▶` U+25B6，非 emoji 面）/ `PlayTooltip` |
| `Features/Aggregate/AggregatePage.xaml(.cs)` | 卡片右下角 ▶ 播放按钮（`Click="OnPlayClick"`）；`OnCardClick` 改走 `TryOpenDetail`；自检入口 `OpenDetailForSelfTest` / `PlayForSelfTest` / `SelfTestFrame` |
| `Features/Favorites/FavoritesPage.xaml(.cs)` | 同上（同一 `CardIntents`，**两屏同口径**） |
| `Features/Aggregate/AggregatePlayback.cs` | 新增 `SHELL_SELFTEST_NO_LAUNCH=1` 取证开关（走到入口但不启内核，必留日志） |
| `Features/Aggregate/T31SelfTest.cs` | 新增 `SHELL_SELFTEST_AGGREGATE=t103` 专项模式（收藏过滤器 + 调 `T103SelfTest`） |
| `Features/Aggregate/T103SelfTest.cs`（**新**） | ①–⑩ 十条断言（意图分离 / 按钮绑定 / 快照拦因 / 单集退父剧 / 无父剧可见失败 / 收尾停在详情页） |

**两屏同口径的源码级读数**（`CardIntents` 是唯一实现，两处的调用点）：
```
FavoritesPage.xaml.cs:466: if (CardIntents.TryOpenDetail(Frame, "FAVORITES", poster, live, out var reason))
FavoritesPage.xaml.cs:488: if (CardIntents.TryPlay("FAVORITES", poster, live, out var blockReason))
AggregatePage.xaml.cs:479: if (CardIntents.TryOpenDetail(Frame, "AGGREGATE", poster, live, out var reason))
AggregatePage.xaml.cs:501: if (CardIntents.TryPlay("AGGREGATE", poster, live, out var blockReason))
FavoritesPage.xaml:60:  ItemClick="OnCardClick" ＋ :120 Click="OnPlayClick"
AggregatePage.xaml:79:  ItemClick="OnCardClick" ＋ :121 Click="OnPlayClick"
```

## 3. 顺带修掉一个既有缺陷（**是 t54 时期我留下的**，本卡验收第 3 条把它逼出来了）

`AggregatePage.ReloadAsync` 里原来是"取完再清"，而 `onLiveAggregate` 是**在取数途中**回调的 ⇒
刚填进去的真条目立刻被抹掉 ⇒ **聚合视界的播放按钮永远拿不到真条目**（第一轮实测就撞上：断言⑤ = False）。
修法：把 `LiveAggregate = null;` 移到 `LoadAsync` **之前**（与收藏屏同一口径），并在注释里写明原因。
⇒ 复跑后断言⑤/⑥ = True（§5）。

## 4. 构建读数

```
构建 : dotnet build shell/App/AIPlayer.Shell.csproj -c Debug -t:Rebuild -p:OutputPath=E:\ui2-bin\
       2026-09-12 07:19:41 结束（44.59 s）｜HEAD=b7a305d｜EXIT=0 / 0 个错误 / 1216 个警告
产物 : E:\ui2-bin\AIPlayer.Shell.dll = 1,738,240 B / sha12 0309DA15906C / mtime 07:19:33.600 ≥ 源码 mtime
在场 : 构建前 dotnet.exe / XamlCompiler = 0（安静窗口）；1216 警告的归属见 t93 报告（他人在同窗口改源根，逐条在 reversed/**）
```

## 5. 运行读数（重定向沙箱 + 真机操作序列）

```
预检 : shell/tools/list-instances.ps1 -Samples 3 -IntervalMs 1000
       FREE-FIELD|verdict=FREE-OVER-WINDOW|samples=3|freeSamples=3|allFree=True|spanSec=2.22
沙箱 : LOCALAPPDATA=%TEMP%\t103-sandbox\LOCAL ｜ APPDATA=%TEMP%\t103-sandbox\ROAMING
       夹具 = 真实根拷入 servers.json / accounts.json(16 台) / credentials.bin / settings.json / device-id.txt
起窗 : pid=12784 @ 2026-09-12 07:19:49（E:\ui2-bin\AIPlayer.Shell.exe）
       SHELL_START_PAGE=aggregate｜SHELL_SELFTEST_AGGREGATE=t103｜SHELL_SELFTEST_NO_LAUNCH=1
收尾 : 07:21:23 我自停（只打自己那个 pid）；LEFT-MINE=none（残留 0）
断言 : **10 / 10 True**
  样本    : 屏上卡片 1049 张｜其中 Episode 955 张（Episode=955,Series=41,Movie=53）
            取用样本 type=Episode id=232840 serverId=vU9cHAh8ZJ5Xv7El（ServerA）
  ①–④   : 点卡片 ⇒ `tag=detail itemId=232840 serverId=vU9cHAh8ZJ5Xv7El type=Episode`；`ShellState.CurrentTag=detail`；意图=detail
  ⑤–⑦   : 播放按钮 ⇒ 返回 True、意图=play、字形 `▶` + tooltip；**no-launch 生效**：日志 `AGG PLAY selftest-suppressed …`
  ⑧     : 快照卡（live=null）点播放 ⇒ 返回 False + 可见拦因（不静默）
  ⑨     : 单集自身 Id 空 + 有父剧 ⇒ 导航到父剧，读数含 `degraded=parent-series`
  ⑩     : 自身 Id 空 + 无父剧 ⇒ 返回 False + 理由「该集详情不可用（条目缺少 Id 且没有父剧可退）」
截图 : t103-detail-page.png（PrintWindow 单窗抓，1424x720，nonBlack=99.998%，未抢焦点、无屏幕拷贝）
       画面 = **该集的详情页**（Hero + 播放按钮 + 版本/音轨/字幕 + 「季」列表），左下角"当前页: aggregate"是页脚文案
```

## 6. 证据清单（**同一批**入库：只提文档会让 `H2a/H2b` 当场转红）

| 文件 | 身份 |
| --- | --- |
| `shell/Tests/evidence/t103-card-click-intents.txt` | 4,126 B / 65 行 / sha12 `EA0B0E802A37`（十断言原始读数） |
| `shell/Tests/evidence/t103-card-click-timeline.txt` | 5,201 B / 27 行 / sha12 `26AD086F9DF5`（沙箱日志时间线，1 行截断） |
| `shell/Tests/evidence/t103-detail-page.png` | 840,240 B / sha12 `3D57699122A0`（点击后停在详情页） |
| `shell/Tests/evidence/t103-detail-page.txt` | 1,330 B / sha12 `C723BD63C5E6`（抓图 sidecar） |

## 7. 已知边界 / 如实记录

1. 详情页打开后日志里有 `DETAIL LOAD-FAIL … HTTP 500 Internal Server Error`，随后 `DETAIL IMG-OK 3/3`：
   即**该集的某一项取数在该服务器上返回 500**（详情页仍渲染出 Hero/季列表，见截图）。这是**详情页内部**的取数面
   （`ui` 的 `Features/Detail`，不在本卡 inScope），本卡只保证"点卡片一定到得了详情页、且到不了时有可见理由"。
2. `SHELL_SELFTEST_NO_LAUNCH=1` 只用于取证（证明"起播入口仍被走到"而不启内核）；生产不设该变量 ⇒ 行为与设计一致。
3. 页脚"当前页: aggregate"与 `ShellState.CurrentTag=detail` 不一致：详情页不是 rail 目的地（`LibraryPage` 同样处理），
   本卡不改 `MainWindow`（`ui` 的面）。
4. 本卡只改了两屏（收藏 / 聚合视界）；`Features/Library` 的详情导航是它自己的实现（t27 交付面），未动。
