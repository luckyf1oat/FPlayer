# t58 人物屏（U-L 分片一：Emby 人物）—— 证据

> 归属：`ui2`。文件领地：`shell/App/Features/Person/**`。取数一律经既有 `EmbyService`，缓存复用**同一个**
> `MediaSnapshotSource` 的 SWR 入口（`t54` 口径），**未新建第二条取数通道**。

## 0. 三态（**不许混说**）

| 态 | 内容 |
|---|---|
| **逆向到了** | 头像卡几何与字色（`HILLSLITE_UI_ANALYSIS.md` §4.5 / `UI_SPEC_SHELL.md` §6 第 5 条实测）：**头像 + 人物名（白）+ 角色名（灰）**，无照片用 **`#FF333333`** 占位；海报几何（§7.3 实测）：**166×249**、列间距 **24**、右上角**紫色圆形**角标（数字 = 未看集数 / `✓` = 已看完）、标题白 + 副标题灰 |
| **重建实现了** | 人物屏本体：`PersonPage.xaml(.cs)` + `PersonSelfTest.cs` + `PersonEvidence.cs`；`MediaSnapshotSource.LoadListAsync`（自定义键，仍走同一个 `SwrSnapshotCache`） |
| **运行验证过了** | 真起窗 + 真实 Emby 数据：人物 `18047`（**Gakuto Kajiwara**，ServerA）→ **171 部作品**（Episode 160 / Series 11），`171/171` 有封面 URL；**全部断言 True**（见 §2） |

> ⚠️ **本屏在参照图里没有对应物**（`refs/original-ui/` 无人物详情屏）⇒ **整体构图维度一律 `INCONCLUSIVE(无参照)`**，
> 只有"复用了已实测几何"的子元素才有实测依据（见 §4 差异表）。

## 1. 构建与运行读数

```
命令：dotnet build shell\App\AIPlayer.Shell.csproj -c Debug -v m -nologo
HEAD=6e858bc   采样：2026-09-12 03:11:29 → 03:12:0x（22.9 s）
EXIT=0   0 个错误   1172 个警告（全部来自 reversed/ 反编译源；本卡改动 0 警告）
日志：shell/Tests/evidence/ui2-t58-buildG1.txt（606,354 B / 2,363 行 / sha12 9A9A5B08C0D4）

运行：$env:SHELL_START_PAGE=person; $env:SHELL_SELFTEST_PERSON=1; $env:SHELL_AGG_MAX_SERVERS=4;
      $env:SHELL_AGG_IMAGE_WINDOW=8; $env:SHELL_AGG_IMAGE_LIMIT=12  →  AIPlayer.Shell.exe
```

> ⚠️ 构建期间有多次红，**逐条归因到并发混合态**（非本卡代码）：`CS2012 intermediatexaml` 被他人 `XamlCompiler` 锁、
> `MSB3027/MSB3021` 被**在跑的外壳窗口**锁、以及**`XamlTypeInfo.g.cs` 被并发构建截断为 0 B**（本轮真遇到一次，
> 处置 = **删 0 B 陈旧产物后重建**，与 `t31` 记录同法，**未编辑构建脚本**）。取得绿读数前我等安静窗口，**未杀任何他人进程**。

## 2. 自检读数（证据文件 `evidence/t58-person-selftest.txt`）

```
证据：8469 B / 55 行 / sha12 AABE50F0362E / utc 2026-09-11T19:0xZ
人物来源 = 自动（未设 SHELL_PERSON_ID）⇒ id=18047     ← 从真实数据里取一部剧的 People[0]，**不硬编码 id**
服务器   = ServerA（id=vU9cHAh8ZJ5Xv7El）
作品卡片数 = 171   类型分布 = Episode=160 / Series=11   有封面 URL = 171/171   带角标 = 10/171
```

| 断言（原文） | 结果 |
|---|---|
| `断言 人物 id 非空` | **True** |
| `断言 取数成功（刷新已发起）` | **True** |
| `断言 作品卡片 ≥ 1` | **True**（实际 171） |
| `断言 每张卡都有封面 URL 或占位` | **True** |
| `断言 角标只可能是「数字 / ✓ / 空」` | **True** |
| `断言① 占位 → 封面 真的发生` | **True**（窗口 8：加载前 0 → 加载后 12，有 URL 171/171） |
| `断言② 缓存态每张卡都取不到真条目` | **True**（`FailFetchForSelfTest` 打断网 ⇒ 点卡必被拦 + 可见失败行） |
| `断言② 真刷新后至少一张卡可起播` | **True**（证明起播门不是"一律拒绝"） |
| `断言③ 前提（刷新成功 且 两组 id 相同）` / `断言③ T0 的首张卡片实例未被换` | **True / True**（T1 就地替换：不重绑 ⇒ 不闪屏、不重置滚动） |
| `断言 文案纪律 命中总数 = 0` | **True**（4 条模式各 0 命中；口径见下） |

**文案纪律口径（必须写清，否则判据会假红）**：检查对象 = **可见文案** = ① 先剥 XML 注释（注释里大量引用 `§4.5` 这类规格编号，属证据性说明，**不是**屏上文字）② 取 `Text=``Content=``Header=``PlaceholderText=` 的字面值 ③ `ToolTipService.ToolTip` 按验收原文豁免。**这是对 XAML 源码的近似提取，不是渲染面逐字符取词。**

## 3. 截图级取证（通道 = `popup-capture.ps1` + `-Hwnd` 单窗 `PrintWindow`）

| 文件 | bytes | sha12 | 说明了什么 |
|---|---|---|---|
| `evidence/t58-person-window.png` | 442,544 | `20F92BC019D9` | **真起窗**：`Gakuto Kajiwara` + 「演职人员 · ServerA · **作品 171 部**」+ 摘要「作品 171 部 · 缓存=命中（Fresh）」；海报网格 166×249、列间距 24；**右上角紫色圆形角标**（11 / 1 / 12 / 21 / 25 …）；标题白 + 副标题灰；底部状态条 `当前页: person` |
| `evidence/t58-person-glyph-avatar.png` | 42,445 | `F0B529025834` | **字形反控**：注入不存在的 person id ⇒ `avatar-fail`（证据里有原文）⇒ 头像落在 **`#FF333333` 圆形占位 + 联系人字形 `E77B`**（**人眼确认不是豆腐块**），同时显示空态文案 |
| `evidence/t58-person-ui-audit.txt` | 7,199 | `E77A6B1B42F6` | `ui-design-audit.ps1` 原始输出（见 §5） |

**三个字形均人眼确认**：`E72B`（返回箭头）、`E91B`（无封面占位胶片）、`E77B`（头像占位联系人）—— 均来自 `{StaticResource IconFontFamily}`（= `Segoe MDL2 Assets`），**无 emoji**（`U+FE0F`/`U+1F000–1FAFF` = 0）。

## 4. 差异表（对照物里没有这一屏）

| 维度 | 判定 |
|---|---|
| 整体构图（头部/头像位置/列表排布） | **`INCONCLUSIVE(无参照)`** —— `refs/original-ui/` 里没有人物详情屏 |
| 头像卡几何（卡宽 144 / 间距 16 / 姓名白 + 角色灰 / 无照片 `#FF333333`） | 逆向到了（HILLSLITE §4.5、UI_SPEC §6 第 5 条实测值） |
| 海报几何（166×249 / 列间距 24 / 右上角紫色圆形角标 / 标题白 + 副标题灰） | 逆向到了（§7.3 实测值） |
| 像素级位置比对（头像直径、网格首列 x、行距） | **未验**（留给 `t34` 视觉收口） |

## 5. 视觉量测工装读数（`shell/tools/ui-design-audit.ps1`）

```
命令：powershell -NoProfile -ExecutionPolicy Bypass -File shell/tools/ui-design-audit.ps1 `
        -Image shell/App/Features/Person/evidence/t58-person-window.png -Channel printwindow -WindowKind xaml
SUMMARY|verdict=INCONCLUSIVE|pass=16|fail=0|inconclusive=18|exit=0
```

- **`-Channel` / `-WindowKind` 已显式给出**（`printwindow` / `xaml`），不是"错组合当 PASS"。
- 18 条 `INCONCLUSIVE` 全部是**令牌/几何探针**，原因写在 `note` 里且是**可复算的**：
  `probe set reference 1403x794 != image 1440x759` ⇒ 探针坐标系与本屏截图尺寸不同 ⇒ **按工具设计不判 PASS 也不判 FAIL**。要闭合需为 1440×759 出一套探针坐标（`-TokenProbes`），**本卡不做**（留给 `t34`）。
- 16 条 PASS 是通道/stats 组（与尺寸无关的那些）。

## 6. 本屏抓到并修掉的真缺陷（都是**截图**逼出来的，不是读代码看出来的）

1. **副标题长时间显示「作品 0 部」**：`SubtitleText` 的赋值原本在 `MaybeLoadImagesAsync()` **之后**，而 171 张封面是串行加载的 ⇒ 那段时间屏上一直是 `作品 0 部`（第一次截图**实拍到这个形态**）。修法：抽 `UpdateSubtitleText()`，**跟着渲染走**（`RenderFromSnapshot` 内调用）。
2. **头像取不到时占位被盖住**：头像容器那一层 `Border` 原本带 `SurfaceBgBrush` 底色，压在 `#FF333333` 占位层之上 ⇒ 用户看到的是**一个空圆圈**（第二次截图实拍）。修法：去掉该层底色（占位层与图层的可见性由代码切换）。

## 7. 未验 / 边界（宁缺不编）

1. **像素级**（头像直径、网格首列 x/y、行距）—— 未做，留给 `t34`；`ui-design-audit` 的令牌/几何探针因尺寸不符判 `INCONCLUSIVE`（见 §5）。
2. **真实鼠标点击起播**：本屏只证"起播门的判据"（缓存态全拦 / 刷新态放行），**点击注入后的观感未测**。
3. **入口可达性**：`person` 路由 case 与 `PageTypeFor` 由 `ui` 在 `MainWindow.xaml.cs` 补（我已发消息请求，已于 02:29:55 落地并包含在本次产物里）；**从详情页演职人员点某人的跳转尚未接线**（详情页的演职人员横向行属 `ui` 的 `t29`，当刻该行尚未实现）⇒ 本屏目前经 `SHELL_START_PAGE=person` 可达。
4. **"角色名（灰）"**：参照物里那是**详情页内人物卡**的字段；本屏是独立人物页，没有"角色"这个上下文（角色是"某人在某部剧里演谁"，属剧集/详情面）⇒ 本屏**不画角色名**（不编数据）。

## 8. 交付物（全部在领地内）

```
源码
  shell/App/Features/Person/PersonPage.xaml
  shell/App/Features/Person/PersonPage.xaml.cs
  shell/App/Features/Person/PersonEvidence.cs
  shell/App/Features/Person/PersonSelfTest.cs

报告
  shell/App/Features/Person/EVIDENCE_T58.md          （本文）

证据（各自身份见 §3 表）
  shell/App/Features/Person/evidence/t58-person-selftest.txt
  shell/App/Features/Person/evidence/t58-person-window.png + .txt sidecar
  shell/App/Features/Person/evidence/t58-person-glyph-avatar.png + .txt sidecar
  shell/App/Features/Person/evidence/t58-person-ui-audit.txt
```

**共用的既有文件**（只做加法，未改语义）：`shell/App/Features/Aggregate/MediaSnapshotSource.cs`（新增 `LoadListAsync` / `KeyForPerson` / `KeyForCollection` / `ToListSnapshot` / `ToOutcome` —— 仍只有**一个** `SwrSnapshotCache` 实例）、`AggregateDiagnostics.cs`（新增 `Redirect` 改道钩子，缺省行为逐字不变）。

**未触碰**：`reversed/**`、`shell/Services/**`、`shell/App/Theme/**`、`MainWindow.*`、`App.xaml(.cs)`、他人 Features。
