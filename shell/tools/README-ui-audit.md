# `ui-design-audit.ps1` —— UI 设计合规检查工装（用法与判据）

> 归属：`verifier`（t42 交付）。位置：`shell/tools/ui-design-audit.ps1`（**脚本本体 ASCII-only**，PS 5.1 安全）。
> 目的：把"UI 设计合规检查"从**每次即兴**变成**一条命令、可重复、可复算**；供 `t34`（视觉收口）与 `t35`（最终验证）直接调用。
> 本文档是 UTF-8 中文；脚本里**不出现任何非 ASCII 字符**（PS 5.1 会把无 BOM 的 UTF-8 `.ps1` 按 ANSI 解码，中文注释会直接导致解析失败 —— `services` 在 `%TEMP%` 已踩过一次）。

---

## 0. 一条命令

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File shell/tools/ui-design-audit.ps1 `
  -Image shell/Tests/evidence/ui-t26-attempt3-try2.png `
  -Channel printwindow -WindowKind xaml -Origin "0,0"
```

**退出码**：`0` = 本次运行**无 FAIL**（含 `INCONCLUSIVE`）；`1` = 至少一条 `FAIL`；`2` = 用法/IO 错误（文件不存在、参数非法）。
⇒ `INCONCLUSIVE` **从不**导致非 0 退出码，也**从不**崩溃（这是"前提不满足"与"实现缺陷"的分界，见 `VERIFY_PLAN_T35_T41.md` A0-1/C.1）。

**机器可读输出**（stdout，一行一记录）：

```
AUDIT|file=..|bytes=..|sha256_12=..|mtime=..|size=WxH|channel=..|windowkind=..|origin=dx,dy|sampled=<时刻>
RESULT|group=<token|geometry|channel|stats>|item=<名>|verdict=<PASS|FAIL|INCONCLUSIVE>|measured=<实测>|expected=<期望>|note=<说明>
SUMMARY|verdict=<PASS|FAIL|INCONCLUSIVE>|pass=n|fail=n|inconclusive=n|exit=<0|1>
```

- `SUMMARY.verdict` 的算法：**有 FAIL ⇒ FAIL；否则有 INCONCLUSIVE ⇒ INCONCLUSIVE；否则 PASS**。
- `-Json <path>` 额外写一份 UTF-8（无 BOM）JSON：与上面同字段 + `results[]` 明细。

---

## 1. 参数语义

| 参数 | 默认 | 语义 |
|---|---|---|
| `-Image` | **必填** | 被检查的 PNG。文件不存在 ⇒ `ERROR|image not found` + **退出码 2** |
| `-Channel` | `''`（未提供） | `printwindow` / `copyfromscreen` / `unknown`。**未提供 ⇒ 该截图不得作为渲染证据**（判 `INCONCLUSIVE`，见规则 R1） |
| `-WindowKind` | `unknown` | `xaml`（纯 XAML 窗口）/ `kernel`（内核混合合成窗口）/ `unknown` |
| `-Origin` | `0,0` | 探针坐标系相对图像的偏移。**令牌探针坐标是"参照物布局"坐标**（`UI_SPEC_SHELL.md` §1 采样于 1403×794），若截图带窗口边框/DPI 差异，用 `-Origin` 平移对齐 |
| `-TokenProbes` | 内置 12 项 | 覆盖默认探针集，语法见 §2 |
| `-ReferenceSize` | `1403x794` | 探针集的参照尺寸；**图像尺寸与之不符时，令牌/几何默认判 `INCONCLUSIVE`**（不判 FAIL —— 坐标系不可比） |
| `-ForceGeometry` | 关 | 尺寸不符时仍强行判几何（会给出无意义 FAIL，慎用） |
| `-SidebarScanY` / `-SidebarExpected` / `-SidebarTolerance` / `-SidebarRunMin` | `200` / `256` / `2` / `8` | 侧栏宽判据（见 §3） |
| `-ChipScanY` / `-ChipProbeX` / `-ChipHeightExpected` / `-ChipHeightTolerance` | `66` / `380` / `33` / `2` | 芯片行扫描线、选中芯片所在列、芯片高 |
| `-PosterRect` / `-CardRects` | 空 | `x,y,w,h` / `x,y,w,h;...`。**给了才判**（像素无法定位图像块边界 ⇒ 不给就判 `INCONCLUSIVE` + "needs UIA"） |
| `-Tolerance` | `0` | 颜色比较容差（逐通道）。默认 0 = 精确匹配（设计令牌本就要求精确） |
| `-ExpectNonBlackMin` | `-1`（不判） | ≥0 时：非黑占比低于该值 ⇒ `stats.NonBlackPct` **FAIL**（用于"这一屏应该有内容"的场景） |
| `-Json` | 空 | 输出 JSON 路径（UTF-8 无 BOM） |

---

## 2. 四类断言

### ① 令牌（`group=token`，12 项，期望值来源 = `UI_SPEC_SHELL.md` §1）

默认探针（坐标即 §1 的"采样点"列）：

| 令牌 | 期望 | 探针 |
|---|---|---|
| `RailBg` | `#202020` | `pixel` @ (100,300) |
| `PageBg` | `#222222` | `pixel` @ (1000,20) |
| `SurfaceBg` | `#282828` | `pixel` @ (1000,200) |
| `ControlBg` | `#2F2F2F` | `pixel` @ (700,20) |
| `ControlBorderSearch` | `#3E3E3E` | `majority` @ (666,20) 5×1（§1 给的是 666..670 区间） |
| `ControlBorderChip` | `#434343` | `pixel` @ (264,68) |
| `RowSelectedBg` | `#2D2D2D` | `pixel` @ (120,410) |
| `Accent` | `#9D82C2` | `pixel` @ (380,66) |
| `TextPrimary` | `#FFFFFF` | `present` @ (264,56) 38×33（区域内**出现过**即可） |
| `TextSecondary` | `#9E9E9E`–`#B5B5B5` | `present`（**区间**）@ (233,410) 12×1 |
| `ServerOk` | `#50B649` | `present` @ (16,410) 28×28 |
| `ServerFail` | `#FFADB6` | **`unpinned`** —— §1 说它采样于 `hl-zoom-servers.png`，**本参照图上没有采样点** ⇒ 默认判 `INCONCLUSIVE` 并写明原因（**不猜坐标**） |

探针语法（`-TokenProbes`）：

```
name=#RRGGBB@x,y,kind[,w,h];name2=#A0A0A0-#FFFFFF@x,y,present,w,h;name3=#FFADB6@0,0,unpinned
kind = pixel（单点精确） | majority（区域内众数色） | present（区域内出现 ≥1 次） | unpinned（坐标未在规格中钉死 ⇒ INCONCLUSIVE）
颜色可以是单值 #RRGGBB，也可以是区间 #LO-#HI（逐通道比较）
```

**可失败性**：命中 ⇒ `PASS`；未命中 ⇒ `FAIL`；探针矩形越界 ⇒ `FAIL`；未钉死坐标 ⇒ `INCONCLUSIVE`。
**已实测**：合成参照图（12 项全钉）⇒ 12/12 `PASS`；把 `SurfaceBg` 改成 `#2A2A2A` ⇒ 该项 `FAIL`（见 §6 反控矩阵）。

### ② 几何（`group=geometry`）

| 项 | 判据 | 期望 |
|---|---|---|
| `SidebarWidth` | **从 x=0 起扫** `-SidebarScanY` 行，找**首个连续 ≥8 px** 的 `PageBg #222222` ⇒ 其起始 x 即侧栏宽 | **256 ± 2**（`UI_SPEC_SHELL.md` §1 末行，修正了早期"~190px"的目测值） |
| `ChipHeight` | 在 `-ChipProbeX` 列上取 `Accent` 色的**垂直连续段**长度 | **33 ± 2**（§7.3：芯片行 y 56..88） |
| `ChipWidths` | 在 `-ChipScanY` 行上取 `ControlBg`/`Accent` 的**水平连续段**（≥30 px） | 集合中必须同时含 **38 / 52 / 80（±2）**（§7.3：每字 14px + 左右各 12px 内边距） |
| `PosterSize` | **需 `-PosterRect x,y,w,h`** | **166×249**（§7.3） |
| `CardWidths` | **需 `-CardRects x,y,w,h;...`** | 每个宽度必须是 **144 或 216** |
| `CornerRadius` | **永远 `INCONCLUSIVE`** | 控件 ≈8px / 海报 ≈12px（§1）—— 像素行程无法把圆角与抗锯齿分开 ⇒ **必须走 UIA 或设计稿测量** |

- 侧栏找不到 `PageBg` 连续段 ⇒ `INCONCLUSIVE`（"定位不到内容区边界"），不是 FAIL。
- 尺寸与 `-ReferenceSize` 不符时，前四项默认 `INCONCLUSIVE`（坐标系不可比）。

### ③ 通道合规（`group=channel`，规则来自 `VERIFY_PLAN_T35_T41.md` R1/R2）

| `-Channel` × `-WindowKind` | 判定 |
|---|---|
| 未提供 Channel | `INCONCLUSIVE` —— **该截图不得作为渲染证据** |
| `printwindow` × `kernel` | `INCONCLUSIVE` —— PrintWindow 对内核混合合成窗口**形态为全白/全黑**；这条**不是"渲染失败"**，是"证据无效"。`measured` 里附**形态诊断**（all black / all white / not uniform + 非黑占比） |
| `printwindow` × `xaml` | `PASS` —— 对纯 XAML 窗口 PrintWindow 有效 |
| `copyfromscreen` × (`xaml`\|`kernel`) | `PASS` |
| 其它组合 | `INCONCLUSIVE` |

### ④ 全像素统计（`group=stats`）

- `NonBlackPct`：**非黑 = 任一通道 > 12**；报 `百分比 (非黑像素/总像素)`。
- `WhitePct`：纯白 = 三通道 ≥ 250。
- `ContentRows`：**至少含 1 个非黑像素的行数**。
- 🔴 **脚本内不存在任何抽样步长**：统计走 `Bitmap.LockBits` + **逐像素**全扫（`for x/y` 全量，无 step）。
  **为什么必须这样**（我自己的仪器坑，已写进 R4）：用 120×120 网格读 `shell/docs/t38-overlay-screen.png` 得**平均亮度 0.9、非黑 89/15872** ⇒ 差点把只有 0.832% 内容的"细线型浮层"误判成**全黑**；全像素扫才得 **非黑 0.832% / 内容行 147**。

---

## 3. 对已有证据的离线复读（基线，`verifier` 独立复算）

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File shell/tools/ui-design-audit.ps1 -Image shell/Tests/evidence/ui-t26-attempt3-try2.png -Channel printwindow -WindowKind xaml
powershell -NoProfile -ExecutionPolicy Bypass -File shell/tools/ui-design-audit.ps1 -Image shell/docs/t38-overlay-screen.png -Channel copyfromscreen -WindowKind kernel
```

| 文件 | 身份 | 本工装读数 | 与 `verifier` 独立复算 |
|---|---|---|---|
| `shell/Tests/evidence/ui-t26-attempt3-try2.png` | 49,425 B / sha12 `79F598CEB912` / 1440×759 | `NonBlackPct=98.046% (1071603/1092960)`、`ContentRows=752`、channel `PASS` | ✅ 一致（`verifier` 报的 **98.05%** 是 2 位小数四舍五入；本工具给 3 位 = 98.046%，原始比 = 1071603/1092960） |
| `shell/docs/t38-overlay-screen.png` | 21,041 B / sha12 `C552373B7F13` / 1849×1020 | `NonBlackPct=0.832% (15687/1885980)`、`ContentRows=147`、`WhitePct=0.078%` | ✅ **逐位一致**（0.832% / 147 行） |

> ⚠️ 两条读数的**通道含义不同**，引用时必须带通道：`ui-t26` 是 `PrintWindow`+纯 XAML（**有效**）；`t38-overlay-screen` 是 `CopyFromScreen`+内核窗口（**有效**）。若把后者标成 `PrintWindow`，本工具会按 R2 判 `INCONCLUSIVE`（这是**正确的判定**）。
> ⚠️ `t38-overlay-screen.png` 的**采集通道是 `services` 脚本自述**，不是像素本身能证明的；本工具只能证明"该帧不是全白/全黑"。

---

## 4. 哪些项**无法从像素判定**（必须走 UIA / 设计稿）

1. **圆角半径**（控件 ≈8px / 海报 ≈12px）—— 抗锯齿与圆角在像素上不可分。
2. **海报 166×249 与卡片 144/216 的边界** —— 海报是图像内容、卡片边界不是纯色行程；**除非调用方给出 `-PosterRect`/`-CardRects`**。
3. **字体/字号/字重/行高、文本截断（省略号）** —— 需要 UIA 文本属性或与设计稿并排人眼比对。
4. **间距 8 / 16、列间距 24** —— 需要真实元素矩形（UIA `BoundingRectangle`）；像素行程只能给"颜色块"间距，遇到阴影/圆角会偏。
5. **悬停/选中/禁用等交互态** —— 需要驱动输入后另抓一张图（本工装可对每张图各跑一次，但不能自己产生交互）。
6. **图标形状/矢量语义**（返回箭头、齿轮、菱形） —— 需要与设计稿比对或 UIA 名称。
7. **滚动条、动画、图层顺序（z-order）** —— 静态截图不可判。

⇒ 结论：**本工装判"颜色令牌 + 可测几何 + 通道合规 + 内容覆盖率"，剩余项一律显式标 `INCONCLUSIVE (needs UIA)`，不假装能判。**

---

## 5. 与 `verifier` 清单的对接

- **R1**：任何截图证据必须标注「采集通道 + 目标窗口类型」⇒ 本工具把这两个值写进 `AUDIT|` 头行。
- **R2**：内核窗口一律要求 `CopyFromScreen`；`PrintWindow` 抓内核窗口 ⇒ **判证据无效（INCONCLUSIVE）**，不是渲染失败。
- **R3**：离线复算 —— 本工具就是那个"离线复读器"，`t41` 复核渲染截图时用它给出**不依赖实现者自述**的读数。
- **R4**：全像素扫描，禁稀疏网格（§2 ④）。
- **A0-4/A0-5**：`AUDIT|` 头行同时给 **时刻 + 字节 + sha12 + mtime + 尺寸 + 通道 + 窗口类型 + 非黑占比**，满足"凡 `data\player`/证据类断言必须带时刻 + 哈希"的形状要求。

---

## 6. 反控矩阵（每条都实测；仪器必须能失败）

| # | 场景 | 命令要点 | 期望 | 实测 |
|---|---|---|---|---|
| 1 | 正控制（合成参照图 1403×794，12 探针全钉 + 真实 rect） | `-TokenProbes <full> -ChipProbeX 380 -PosterRect 272,113,166,249 -CardRects 272,380,144,200;440,380,216,200` | 令牌 12/12 `PASS`、几何可测项 `PASS`、`CornerRadius INCONCLUSIVE` ⇒ 退出码 **0** | ✅ `pass=21 fail=0 inconclusive=1`，exit=0 |
| 2 | **负控制**（同图：侧栏改 200px、`SurfaceBg` 改 `#2A2A2A`） | 同上 | `SidebarWidth` + `SurfaceBg` 必须 `FAIL` ⇒ 退出码 **1** | ✅ `pass=17 fail=2`，exit=1（**证明判据能失败**） |
| 3 | 错通道反控（`PrintWindow` 谎称内核窗口） | `-Channel printwindow -WindowKind kernel` | 必须 `INCONCLUSIVE`（附形态诊断） | ✅ `channel INCONCLUSIVE`，`measured=printwindow + kernel (not uniform, nonblack=0.832%)`，exit=0 |
| 4 | 未提供通道 | 不给 `-Channel` | `channel INCONCLUSIVE`（"不得作为渲染证据"） | ✅ exit=0，`inconclusive=5` |
| 5 | 全黑图（合成 120×80 纯黑） | 默认 | `NonBlackPct=0%` 且**不崩**，exit=0 | ✅ `0% (0/9600)` |
| 6 | 全黑图 + "这一屏应该有内容" | `-ExpectNonBlackMin 1` | `stats.NonBlackPct` **FAIL** ⇒ exit=1 | ✅ exit=1 |
| 7 | 缺文件 | `-Image <不存在>` | `ERROR|image not found` ⇒ **退出码 2** | ✅ exit=2 |
| 8 | 参照物证据离线复读 | 见 §3 两条命令 | `0.832% / 147`、`98.046%` | ✅ 与 `verifier` 独立复算一致 |

> 合成控制图生成于 `%TEMP%\uiaudit\`（**不入库**）：`ref-ok2.png`、`ref-bad2.png`、`black.png`。

---

## 7. 实现注记（踩过的坑，写给下一个改这脚本的人）

1. 🔴 **不要给 `param()` 里的变量重复赋值**：PowerShell 会保留声明时的类型约束，`[string]$Origin` 被赋一个 Hashtable 时会**静默转成字符串**，后续 `$origin.A` 全为 `$null`。
   实测现象：`$origin = Parse-Pair $Origin` 之后 `$origin.GetType()` 仍是 `System.String` ⇒ 输出里 `origin=` 变成 `origin=,`。
   本脚本的处置：**另起变量名**（`$originPair`）承接函数返回值。
2. **区间色令牌**（`#9E9E9E-#B5B5B5`）要"先判区间、再计入命中"；先按低端做精确匹配会让区间恒不命中。
3. **`Get-Process` 空结果会把 pwsh 退出码置 1** —— 判据读进程列表/计数，**不要读退出码**（否则干净窗口每次假报红）。
4. 管道里 `Select-Object -First N` 会提前结束管道，**拿不到子进程真实退出码**（实测出现 `-1`）⇒ 取退出码时把输出先落变量，再 `$LASTEXITCODE`。
5. 脚本保持 **ASCII-only**：任何中文（哪怕只在注释）都可能在 PS 5.1 下解析失败。
   **第三实例（captain 实测）**：写对比度脚本时把中文写在脚本里、又用 `Set-Content -Encoding ASCII` 保存 ⇒ **中文全变 `?`** ⇒ 与 R4/ASCII-only 同源。
6. 🔴 **逗号运算符比 `/` 绑定更紧**：`@($c.R / 255.0, $c.G / 255.0, $c.B / 255.0)` 会被解析成"除以一个数组" ⇒
   运行时 `Method invocation failed because [System.Object[]] does not contain a method named 'op_Division'`。
   处置：**每个元素各自加括号** `@(($c.R/255.0), ($c.G/255.0), ($c.B/255.0))`。
7. 🔴 **`Get-ChildItem -Recurse -Include *.xaml -LiteralPath <目录>` 会返回"目录下所有文件"**（`-Include` 在非通配符 `-Path/-LiteralPath` 上不生效，且**静默**）。
   实测：本想取 10 个 `.xaml`，实际返回 68 个文件（含 `.cs/.png/.log`）⇒ XAML 模块一度把 `.cs` 也当 XAML 判。
   处置：**枚举全部再按扩展名过滤**（`Where-Object { $_.Extension -eq '.xaml' }`）。

---

## 8. 模块 A：WCAG 对比度审计（零依赖，纯数学）

**何时跑**：默认随主流程跑（`-SkipContrastStatic` 可关）；有真实截图时加 `-ContrastProbes` 做**像素级**测量。

### 8.1 静态令牌对表（期望值来源 = `UI_SPEC_SHELL.md` §1 的十六进制值）

`ratio = (L1+0.05)/(L2+0.05)`，`L` = WCAG 相对亮度。阈值：**正文 4.5**，**大字号/UI 组件 3.0**。

| 组合 | 实测比值 | 阈值 | 判定 |
|---|---|---|---|
| `#FFFFFF` on `RailBg #202020` | **16.29** | 4.5 | PASS |
| `#FFFFFF` on `PageBg #222222` | **15.91** | 4.5 | PASS |
| `#FFFFFF` on `SurfaceBg #282828` | **14.74** | 4.5 | PASS |
| `#B5B5B5` on `#202020` | **7.95** | 4.5 | PASS |
| `#9E9E9E` on `#202020` | **6.08** | 4.5 | PASS |
| `#9E9E9E` on `#282828` | **5.50** | 4.5 | PASS |
| `#9E9E9E` on `RowSelectedBg #2D2D2D` | **5.14** | 4.5 | PASS |
| 禁用字 `#6A6A6A` on `#202020` | **3.01** | 3.0 | PASS（仅 UI 组件级；禁用文本本身豁免 WCAG） |
| `ServerFail #FFADB6` on `#202020` | **9.24** | 4.5 | PASS |
| `ServerOk #50B649` on `#202020` | **6.31** | 3.0 | PASS |
| 深色字 `#202020` on `Accent #9D82C2` | **4.97** | 4.5 | PASS |
| **白字 `#FFFFFF` on `Accent #9D82C2`** | **3.28** | 4.5 | 🔴 **低于正文阈值** ⇒ 据此产出**可断言的规则** |

**规则条目 `contrast/AccentWhiteTextForbidden`**：`measured = white=3.28 dark=4.97`，判据 = **`white < 4.5` 且 `dark >= 4.5`** ⇒ PASS 表示"**紫底必须用深色字**"这条规则成立；
**若哪天白字 ≥4.5（或深色字 <4.5），该条判 FAIL** —— 即规则本身被证伪，而不是"没事"。这是把规格里"选中 = 紫实底 + 深色字"变成**可测依据**。

### 8.2 像素级对比度（`-ContrastProbes`）

语法：`label:#FG@x,y:#BG@x,y:threshold;...`（坐标同样吃 `-Origin` 偏移）

```powershell
# 合成图 accent-white.png（紫底 + 白块）：应 FAIL
... -ContrastProbes "AccentChip:#FFFFFF@20,20:#9D82C2@150,50:4.5"
#   ⇒ RESULT|group=contrast|item=AccentChip|verdict=FAIL|measured=3.28 (fg #FFFFFF on bg #9D82C2)|expected=>= 4.5   EXIT=1
# 合成图 accent-dark.png（紫底 + 深色块）：应 PASS
... -ContrastProbes "AccentChip:#202020@20,20:#9D82C2@150,50:4.5"
#   ⇒ RESULT|group=contrast|item=AccentChip|verdict=PASS|measured=4.97 (fg #202020 on bg #9D82C2)|expected=>= 4.5   EXIT=0
```

⇒ **真正判"某屏紫底用了白字"就靠这条**：给前景/背景各一个坐标，<4.5 且属正文级 ⇒ **FAIL**（这就是"给定坐标取两色算比值"的判据形态）。

---

## 9. 模块 B：XAML 样式**被动**检查（`XamlStyler`，opt-in）

**工具**：`dotnet tool install --global XamlStyler.Console`（实测 v3.2501.8，`%USERPROFILE%\.dotnet\tools\xstyler.exe`）。
**关键参数**：`-p` / `--passive` = **只检查、不修改**；`-f <csv>` 支持逗号分隔文件列表（本工具用它**显式传入文件清单**，从而绕开 `-d -r` 会扫到 `obj\` 的问题）。

```powershell
... -XamlPath shell/App -XamlMustPass "SearchPage.xaml,LibraryPage.xaml"
```

**判据分两档（captain 裁决，2026-09-11 23:3x）**：

| 档 | 规则 | 判定 |
|---|---|---|
| **新增 / 本卡大改的文件** | 必须在 `-XamlMustPass` 里列出（支持通配符，按**文件名**匹配） | 这些文件 **FAIL ⇒ 本项 FAIL**（不允许新代码带格式债） |
| **存量文件** | 允许 FAIL，但**必须出现在"待统一格式化清单"里** | 判 `INCONCLUSIVE` + note 写明"queued for one-shot formatting by `t34`（single commit, easy to review）"；`XamlStylerSummary` 打印 `pass=/queued=/total=` 与清单 |

**为什么只被动、不批量重排**：当刻有 3 人并行写 XAML，立刻格式化会把真实改动埋进**上千行噪声**里（实测真格式化：`Tokens.xaml` 93→103 行、`MainWindow.xaml` **179→315 行**）⇒ 统一动作**推迟到 `t34` 视觉收口**，单独一个 commit。
**不建 `.xamlstyler.json` 就动别人文件**：配置文件可以建，**用不用由 `ui`/`ui2`/`ui3` 各自的领地决定**。

**实测（2026-09-11 23:3x，`-XamlPath shell/App`）**：
```
RESULT|group=xamlstyle|item=XamlStylerSummary|verdict=PASS|measured=pass=0 queued=9 total=10 exit=1
⇒ 10 个 .xaml（已排 bin/obj）全部 FAIL：App / MainWindow / PlaceholderPage / AggregatePage / FavoritesPage /
   LibraryPage / LogsPage / SearchPage / ServersPage / Tokens
⇒ 加上 -XamlMustPass SearchPage.xaml ⇒ SearchPage.xaml 判 FAIL（新/大改文件必须格式干净），其余 9 个入队
```

> ⚠️ 与主流程解耦：**没有 `-XamlPath` 时本模块完全不跑**，主验收入口保持**零外部依赖**（对比度是纯数学，XAML 检查需要全局工具）。

---

## 10. 模块 C：**Popup 单抓 + 隐私护栏**（`popup-capture.ps1`，t47 交付）

### 10.1 为什么有这件工装（真实事故，不是假想）
`ui3` 在 `t30` 取服务器**右键菜单**的视觉证据时撞上硬约束：`MenuFlyout` 是 **light-dismiss 的独立弹窗 HWND** —— **抢前台会把它关掉**；**不抢前台就会抓到整个用户桌面**（他确实抓到过一帧含用户私人窗口的画面，已立即删除并停用该通道）。
⇒ 这是**工装问题**：本模块用 **`PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT=0x2)` + 精确 HWND** 取证，**从不抢焦点**、**从不抓屏**，并在落盘前做**来源断言**。

### 10.2 四条命令（⚠️ 本机执行策略挡 `.ps1` ⇒ **一律** `powershell.exe -NoProfile -ExecutionPolicy Bypass -File <脚本>`）
```powershell
# ① 自建等价 popup 的可复现取证（A1 harness：开一个 WinForms ContextMenuStrip 真弹窗，抓它，再关掉）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/popup-capture-demo.ps1 -Out %TEMP%\t47\popup-demo.png

# ② 抓真实 flyout（先 -List 找 HWND，再按 HWND 抓）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/popup-capture.ps1 -Pid <PID> -List
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/popup-capture.ps1 -Pid <PID> -Hwnd <HWND> -Out <file.png>
# 或让工具自己挑弹窗（优先 WS_POPUP / WS_EX_TOOLWINDOW 的小窗口）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/popup-capture.ps1 -Pid <PID> -Auto -Out <file.png>

# ③ G5 双向反控（独立 harness：必须拒 / 必须过 两个场景，详见 §10.7）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/popup-capture-focus-regression.ps1 -WorkDir %TEMP%\t47-focus -HoldMs 30000 -InjectDelayMs 3800
```
**本模块四个文件**：`popup-capture.ps1`（主工具）、`popup-capture-demo.ps1`（A1 取证 harness）、`popup-capture-focus-regression.ps1`（G5 双向反控）、`popup-capture-target-worker.ps1`（反控用的目标进程）。

### 10.3 参数
| 参数 | 语义 |
|---|---|
| `-TargetPid`（**别名 `-Pid`**） | 目标进程。⚠️ 参数名不能叫 `$Pid`：那是 PowerShell 的**只读自动变量**（绑定会抛 `Cannot overwrite variable Pid because it is read-only`）⇒ 本工具用别名兼容 `-Pid` |
| `-Hwnd` | 明确指定窗口。**必须属于 `-TargetPid`**，否则拒绝 |
| `-Auto` | 自动挑：可见、类名非桌面类、面积 >20×20，优先 `WS_POPUP`/`WS_EX_TOOLWINDOW`，取面积最小者 |
| `-ExpectClassLike` | 配合 `-Auto` 的类名 glob（例：`*WinUI*`） |
| `-List` | 只列候选（`CAND|hwnd=…|class=…|title=…|visible=…|rect=…|size=…|style=…|exstyle=…|popup=…|tool=…`），**不抓任何图** |
| `-Out` | 输出 PNG。**不给就不写任何文件** |
| `-Sidecar` | 自述文件路径；默认 = `<Out 去 .png>.txt`（**永远 `.txt`，绝不用 `.log`**） |

### 10.4 护栏（每条都**实测能失败**）与反控原始输出
| 护栏 | 规则 | 反控实测（原始输出 + 退出码） |
|---|---|---|
| **G1** | **没有 `CopyFromScreen` / `BitBlt` 代码路径** —— 唯一通道是 `PrintWindow` | 代码检索（**排除注释行**）`SetForegroundWindow|SetActiveWindow|BringWindowToTop|ShowWindow|SetWindowPos|AttachThreadInput|SwitchToThisWindow|CopyFromScreen|BitBlt` ⇒ **非注释命中 = 0** |
| **G2** | 目标 HWND 必须属于 `-TargetPid`，且该进程不是 shell/system（`explorer`/`dwm`/`winlogon`/`csrss`/…） | `-Pid 19632 -Hwnd 5114306`（chrome 的 HWND）⇒ `ERROR|refused: hwnd 5114306 belongs to pid 9096, not to -Pid 19632 -- G2`，**EXIT=1**；`-Pid 4744`（explorer）⇒ `ERROR|refused: target process is a shell/system process (explorer pid=4744) -- G2`，**EXIT=1** |
| **G3** | 桌面/壳类窗口拒绝：`Progman`/`WorkerW`/`Shell_TrayWnd`/`Shell_SecondaryTrayWnd`/`Button`/`SysListView32`/`ConsoleWindowClass` | `-Pid 7184 -Hwnd 66164`（RuntimeBroker 的 `WorkerW`）⇒ `ERROR|refused: desktop/shell window class 'WorkerW' -- G3 (no desktop capture)`，**EXIT=1** |
| **G4** | 目标覆盖 ≥95% 虚拟桌面 ⇒ 拒绝（**本工具故意没有全屏能力**） | `-Pid 9096 -Hwnd 5114306`（chrome 1936×1056 = 98.6%）⇒ `ERROR|refused: target window covers 98.6% of the virtual desktop (1936x1056) -- G4`，**EXIT=1** |
| **G5** | **不抢焦点（修订版 2026-09-12）**：运行时记录 `foreground before/after`；**只有当前台变成"属于目标 PID 的窗口"时才拒**（那才是"我们抢了前台"的唯一可观测证据）。**与目标进程无关的前台变化 ⇒ 只记录、不阻断**。静态保证（本文件无任何抢前台 API）仍是主控；运行时这条用来兜回归 | `GUARD|focusGate=refuse-only-if-foreground-moved-to-target-pid|focusChanged=…|focusStolenFromTarget=…|fgAfterPid=…`；✅ **反控 A**（harness 把目标窗口强行抬前台）⇒ `ERROR|refused: foreground moved to a window of the TARGET process (66852 -> 70256306, fgAfterPid=20428 == target pid) -- G5`，**EXIT=1、newFiles=0**；✅ **反控 B**（无关窗口占前台＝"用户在操作电脑"）⇒ **EXIT=0 且出图**（这正是修订掉的误拒） |
| **G6** | 空白帧不落盘（全白/全黑 ⇒ 拒绝） | 先试 `0x2` 再试 `0x0`，两者都空白 ⇒ `ERROR|blank frame under every PrintWindow flag (...) -- G6 (nothing written)`，**EXIT=1** |
| **G7** | **没有参数不产生任何截图** | 无参运行 ⇒ `ERROR|missing -Pid` + 3 行 `USAGE|` + `GUARD|` 行，**EXIT=2**，证据目录前后文件清单一致（**newFiles=0**） |

**所有反控都做了"证据目录前后文件清单对比"**：上表 7 次拒绝**每次 newFiles = 0**（唯一的文件来自 A1/反控 B 的成功取证）。

> **G5 为什么改（真实误拒，2026-09-12）**：本机与用户共用，用户正在操作电脑时前台会**被别人改**。旧的"任何前台变化即拒绝"把 captain 抓**我们自己设置页**的动作拒了（`refused: foreground window changed during capture (6622094 -> 1706860) -- G5`）。修订只改了**判据**，没削弱护栏：静态"无抢前台 API" + 运行时"前台**变成**目标 PID 窗口才拒" 两条都在，且两条都实测能失败（见下方反控 A/B）。
> **观测窗口**：`foreground before` 取在**枚举/取证之前**、`foreground after` 取在**取证之后**（中间含全像素扫描）⇒ 这是一个可被外部注入命中的真实窗口，而不是"两次相邻调用"的假窗口。

### 10.5 来源断言（侧车 `.txt` 的字段，逐行可核对）
```
POPUP-CAPTURE SOURCE ASSERTION (sidecar; .txt on purpose, never .log)
sampled / tool / channel / printwindow flags (含 0x2 与 0x0 的尝试结果)
hwnd / pid / process / window class / window title / window rect / window style+exstyle+popup+tool
virtual desktop（目标占屏比例）
foreground before / foreground after（含 fgAfterPid / changed / belongsToTargetPid）/ focus gate 说明 / printwindow ok
png path / bytes / sha256_12 / size / luminance mean / stddev / nonBlack pct / white pixels
```
**A1 实测（`%TEMP%\t47\popup-demo.png`，2026-09-12 00:09:26.972；修订后复跑 hash 不变）**：
```
PNG|path=…\popup-demo.png|bytes=2144|sha256_12=E57AD327B590|mean=199.285|std=69.099|nonBlackPct=98.364
CAPTURE|channel=printwindow|flags=0x2|hwnd=…|pid=…|class=WindowsForms10.Window.808.app.…|size=227x70
GUARD|focusGate=refuse-only-if-foreground-moved-to-target-pid|focusChanged=False|focusStolenFromTarget=False|fgAfterPid=…|printwindowOk=True|noScreenCopy=true|noFullScreenCapture=true
⇒ 非空白判据：nonBlackPct=98.364% 且 luminance std=69.099 > 1 ⇒ 既非全白也非全黑
```

### 10.7 G5 双向反控（`popup-capture-focus-regression.ps1`，独立 harness）

⚠️ **本机执行策略会挡 `.ps1`**：所有调用一律写成
`powershell.exe -NoProfile -ExecutionPolicy Bypass -File <脚本> …`（否则第一条命令就被策略拦下）。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/popup-capture-focus-regression.ps1 `
  -WorkDir %TEMP%\t47-focus -HoldMs 30000 -InjectDelayMs 3800
```
harness = **一个测试夹具**（它**可以**用抢前台 API，工具**不可以**）：它启动 `popup-capture-target-worker.ps1`（`-Mode form|popup`）造出真窗口，再按下列两个场景驱动工具：

| 场景 | 做法 | 期望 | 实测（原始） |
|---|---|---|---|
| **反控 A（必须拒）** | harness 自己占前台 → 异步启动工具 → 在**工具的观测窗口内**把**目标进程**的窗口强行抬到前台 | `exit≠0`、**newFiles=0** | `ERROR|refused: foreground moved to a window of the TARGET process (66852 -> 70256306, fgAfterPid=20428 == target pid) -- G5`；`CASE-A|exit=1 newFiles=0`；`CASE-A|verdict=PASS` |
| **反控 B（必须过）** | 无关窗口（harness 自己的窗体，**另一个 pid**）全程占前台 | `exit==0` 且**出图** | `CASE-B|exit=0 newFiles=2 pngExists=True`；`CASE-B|verdict=PASS` |
| 汇总 | — | — | `SUMMARY|caseA=PASS|caseB=PASS|pass=2|fail=0|inconclusive=0`（`HARNESS-EXIT=0`） |

**场景可复现性的两个要点（都实测过）**：
1. 目标窗口要**足够大**（harness 用 900×600）：工具一次取证总耗时实测 **6.15 s**（`Add-Type` 编译 P/Invoke ≈2–3 s + 全像素扫描 ≈3 s）⇒ 观测窗口足够宽，`-InjectDelayMs 3800` 稳定命中（先在 3800 ms 注入、再在 +800 ms 二次注入）。
2. 场景**不成立时判 `INCONCLUSIVE`（exit 3）而不是 PASS**：harness 会断言"工具启动时前台 ≠ 目标进程"且"注入后前台 == 目标进程"，任一条不成立就报 `SCENE-NOT-ESTABLISHED`。

### 10.8 本模块新增的四条 PowerShell 坑（同族第 7/8/9/10 条）
1. 🔴 **`Start-Process -ArgumentList` 不给路径加引号**：`E:\AI Player\shell\…` 会在空格处被拆开，`powershell.exe` 报 *"该文件没有 '.ps1' 扩展名"*。⇒ 路径参数必须**调用方自己加引号**（本 harness 里用 `'"' + $p + '"'`）。
2. 🔴 **.NET Framework 的 `ProcessStartInfo` **没有** `ArgumentList`**（那是 .NET Core 2.1+）：给 `$psi.ArgumentList.Add(...)` 会得到 *"You cannot call a method on a null-valued expression"*。⇒ 在 PS 5.1 里要用 `$psi.Arguments = <自己拼好的带引号字符串>`。
3. 🔴 **`Get-Content` 读本仓中文文档会按 ANSI/GBK 解码 ⇒ 乱码 + 静默少行**（同族第 9 条，2026-09-12 实测）：仓库里的 `.md` 是 **UTF-8 无 BOM**，PS 5.1 的 `Get-Content` 走 ANSI 代码页，多字节序列的尾字节会把紧随的 `0x0A` 当 trail byte 吞掉 ⇒ **行数与内容双错**。实测 `shell/docs/VERIFY_S1.md`（真值 1,981 行 / 141,684 字符 / LF=1981 / CR=0）：
```powershell
[IO.File]::ReadAllLines($p).Count   # 1981  ← 真值
(Get-Content $p).Count              # 1348  ← 少 633 行（且首行是 "# V1 鐙珛楠岃瘉鎶ュ憡…"）
```
   ⇒ 读/写本仓中文文档**只用** `[IO.File]::ReadAllText` / `::WriteAllText` 或编辑器工具；**禁止** `Get-Content` / `Add-Content` / `Set-Content`（最后那个 `-Encoding ASCII` 还会把 CJK 打成 `?`）。**对照**：`Select-String -Path` 与 .NET API 同方言 ⇒ 行号可用于引用。危害形状：行数变少极易被误读成"某成员把文档删短了"，实际是仪器侧静默截断 —— 又一次「形状 ≠ 危害」。

   🔬 **机制补正（2026-09-12，我复算了队友的"BOM 假说"，**该假说被否证**）**：
   ```
   文件（全部 BOM=False）                        nonASCII 占比   ReadAllLines / Get-Content   一致?
   shell/App/Features/Servers/EVIDENCE_T30.md      54.5%          241 / 241                    ✅ 一致
   shell/docs/VERIFY_S1.md                         57.1%         2464 / 1692                   ❌
   shell/docs/WORKSPACE.md                         60.1%         2959 / 1109                   ❌
   shell/tools/README-ui-audit.md                  55.3%          743 / 498                    ❌
   合成控制: 30 行纯中文注释 ⇒ 30 / **1**（❌）；30 行纯 ASCII ⇒ 30 / 30（✅）
   ```
   ⇒ **既不是 BOM 决定的**（四个文件都没有 BOM，却只有第一个一致），**也不能用"非 ASCII 占比"预测**（四者密度几乎相同）。
   真正的机制：**GBK 解码器在"待 trail 字节"状态下会把紧随的 `0x0A` 吃掉**，而是否处于该状态取决于**每行多字节序列的字节奇偶/对齐** ⇒ **逐行、逐内容地随机**。
   ⇒ **结论（比原来更强）**：**没有任何便宜的预测量**（BOM、占比、扩展名都不行）⇒ **本仓一律不用 `Get-Content` 出数字**；若必须判断某个文件，
   唯一可信的办法是**逐文件把两种读法的行数摆在一起比**（我上面就是这么比的）。**别把"我试过的那个文件没问题"推广成"这类文件都没问题"。**
4. ⚠️ **`-ExtraScanPath` 必须对 H1/H3/H3b 全部生效**（同族第 10 条）：首版只把额外根喂给 H1 ⇒ 用 `-ExtraScanPath` 造的 H3 反控"看不见"，看起来像判据失灵。凡新增扫描根，**所有**子检查都要遍历同一份 `$scanRoots`。

⚠️ **通道声明**：`printwindow + PW_RENDERFULLCONTENT(0x2)`；**本工具不提供任何全屏/桌面截图能力（故意不做）**。

### 10.6 给 `ui3` 抓真实 flyout 的具体做法
1. 让 flyout **保持打开**（不要点、不要 Alt-Tab）。
2. `popup-capture.ps1 -Pid <AIPlayer.Shell 的 pid> -List` ⇒ 在候选里找**小窗口**（弹窗通常 `popup=True` 或 `tool=True`，尺寸与菜单相近，类名可能含 `WinUI`/`Popup`/`Content`）。
3. 用 `-Hwnd <该 hwnd> -Out <file.png>` 抓（**别用 `-Auto` 之外的抢前台手段**；本工具全程不调用焦点 API ⇒ flyout 不会关）。
4. 侧车 `.txt` 与 PNG 一起提交（PNG 是视觉证据，`.txt` 是"这张图确实是我们窗口"的来源断言）。
5. 若 `-List` 里看不到弹窗：说明该 flyout 与主窗口同一 HWND（不是独立弹窗）⇒ 直接抓主窗口即可，无需本模块。

---

## 10.9 普通顶层窗口（`AIPlayer.Shell` 主窗口）怎么抓 —— **`-TargetPid` 直接支持，不必加开关**（2026-09-12 01:28:28 实测）

**结论（`ui3` 问的那条）**：`popup-capture.ps1` **不是只给弹窗用的**。`-Auto` 才是"弹窗形状"（只在候选里挑小窗口）；**抓任意窗口（含普通顶层窗口）走显式 `-Hwnd`**，`-TargetPid` 照常生效。
```powershell
# 1) 先枚举（拿 hwnd；窗口句柄每次启动都会变）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/popup-capture.ps1 -Pid <AIPlayer.Shell 的 pid> -List
#    主窗口那一行的特征：class=WinUIDesktopWin32WindowClass  title=AI Player  popup=False  尺寸≈1440x759
# 2) 抓主窗口（照抄即用；-ExpectClassLike 把类名钉死，防抓错窗口）
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/popup-capture.ps1 `
  -Pid <pid> -Hwnd <主窗口 hwnd> -ExpectClassLike 'WinUIDesktop*' `
  -Out <目标.png> -Sidecar <目标.txt>          # 例:shell/Tests/evidence/ 下的证据名(占位符写法见下方"引用形态"提醒)
```
**实测读数（pid=18548 / hwnd=132581506，采样 01:28:28.905）**
```
CAPTURE|channel=printwindow|flags=0x2|class=WinUIDesktopWin32WindowClass|title=AI Player|rect=182,182,1622,941|size=1440x759
PNG|bytes=57921|sha256_12=A72321CC553E|mean=38.269|std=26.131|nonBlackPct=98.114     EXIT=0
独立复验（不信工具自报）: dim=1440x759 采样 3825 点 distinctColors=58 lumMin=0 lumMax=255 lumAvg=38.9 ⇒ **NON-BLANK**
```
**四护栏在这一刻如何生效**（全部来自同一次运行的侧车 `.txt`）
```
G1 无抓屏面 : channel=printwindow only；工具内 CopyFromScreen 非注释命中 = 0（门禁 H1 的 search-only 规则同时兜着）
G2 PID 归属 : pid=18548 process=AIPlayer.Shell（抓的是我们自己的进程）
G3 桌面类拒绝: class=WinUIDesktopWin32WindowClass 不在 @('Progman','WorkerW','Shell_TrayWnd','Shell_SecondaryTrayWnd','Button','SysListView32','ConsoleWindowClass')
G4 ≥95% 虚拟桌面: 目标覆盖 1920x1080 的 **52.71%** < 95% ⇒ 放行
G5 焦点门   : refuse-only-if-foreground-moved-to-target-pid；focusChanged=False stolen=False fgAfterPid=9096 ⇒ 不拒
G7 落盘断言 : 侧车 .txt 写 hwnd/pid/class/rect/virtual desktop/焦点前后/PNG 的 bytes+sha12
外加上限    : -ExpectClassLike 'WinUIDesktop*' 把类名钉死（比裸 -Hwnd 更稳）
```
⚠️ **`PrintWindow` 这条路只对"普通窗口"有效**；**内核 D3D 合成窗口**（`MpvHost` 面）走 `PrintWindow` 会全白/全黑 —— 那条只能按窗口矩形 `CopyFromScreen`，且必须前台化（档 B，见 §11.5-1）。两条通道**不许混用**。

🔴 **写示例时的一条硬纪律（我今晚第三次踩）**：**任何文档里的"示例命令 / 示例清单"都不能把证据路径写成"引用形态"**（`evidence/` 紧跟文件名）—— 门禁的 H2 是**纯文本**判据，**分不清"引用"与"例子"**。
实例：我本节首版把 `-Out …` / `-Sidecar …` 两个参数**写成了两个真实证据路径**（该目录下的 `t30-settings-screen` 那张图与它的侧车）⇒ 门禁判 `FINDING|H2b|MISSING|<那个侧车名>|citedBy=shell/tools/README-ui-audit.md:389`（**我自己造了一条假引用**，而 `ui3` 还没重截那张图）。
⇒ 写法：用占位符 `<目标.png>` / `<目标.txt>`，或**打断 `evidence/` 与文件名的相邻关系**（中间插中文说明）。
（同族前两例：`VERIFY_S1 §59` 的探针名、`§11.5-1` 里抄写允许清单正则导致 `.ps1` 被当成证据名 —— 三次都是**我的文档**，不是别人的。）

### 10.9.1 G5 什么时候会拒你的抓取 —— **官方姿势**（`ui3` 实测被拒后定性，2026-09-12）
**拒绝条件（读代码，不给猜测）**：`popup-capture.ps1` 只在
```
($fgChanged -and $fgAfterPid -eq $TargetPid)
```
成立时拒 —— 即**观察窗（entry 采样 → 抓取后采样）内，前台"移动到"了目标 PID 的窗口**。
- **为什么"刚起窗就抓"会被拒**：新起的 GUI 进程常**自己抢前台** ⇒ entry 时前台还在别处、抓的瞬间已变成目标窗口 ⇒ 判为偷焦点（`ui3` 实例：`111084248 -> 60622922, fgAfterPid=20996 == target pid`）。
- **官方姿势（照抄）**：
  1. 起窗（或别人起的窗）之后，**先等前台稳定** —— 连续两次采样 `GetForegroundWindow()`（间隔 ~0.7s）得到同一个 hwnd，再去抓；
  2. 然后 `-List` 拿 hwnd ⇒ `-Hwnd <hwnd> -ExpectClassLike '<类名>*'` 单抓；
  3. **不要**在抓之前调用 `SetForegroundWindow / SetWindowPos / ShowWindow`（本工具全程没有这些调用 —— 那是它的**静态保证**，别把它破坏掉）。
- ⚠️ **G5 不要求目标必须是前台**：只要观察窗内前台**没变**（无论是在目标上、还是在别人的窗口上）就不拒。
  实测反例：我抓 pid 18548 主窗口时 `focusChanged=False / fgAfterPid=9096 ≠ target` ⇒ 通过。
- 我的编排 `capture-shell-flyout.ps1` **已内置该前置**：输出 `FG|sample=…` 与 `FG-SETTLED|stable=…|foregroundPid=…|targetPid=…|isTargetForeground=…`（**只读**前台，绝不设前台）。

---

## 11. 模块 D：证据卫生门禁（`evidence-hygiene-check.ps1`，t48 交付）

**一条命令**（本机执行策略挡 `.ps1`，一律带 `-ExecutionPolicy Bypass`）：
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/evidence-hygiene-check.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/evidence-hygiene-check.ps1 -ExtraScanPath %TEMP%\probe -Json %TEMP%\hygiene.json
# 棘轮反控（裁定 B：度量对象 = production 计数，夹具另有额度）：在 %TEMP% 造一个含裸 catch 的探针
# ① 探针路径映射到某 owner ⇒ newProdBare=1 ⇒ FAIL   ② 不映射（UNMAPPED）⇒ newUnmappedBare=1 ⇒ FAIL
# 参数：-H3bProdBareBaseline / -H3bProdRibBaseline / -H3bUnmappedBareCap / -H3bUnmappedRibCap / -H3bBaselineAt
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/evidence-hygiene-check.ps1 -ExtraScanPath %TEMP%\probe -OwnerMapFile shell/tools/hygiene-owner-map.txt
```
**退出码**：`0` = 无 FAIL（可含 `INCONCLUSIVE`）；`1` = 有 FAIL；`2` = 用法错误。输出为机器可读行：`FINDING|` / `CHECK|` / `NOTE|` / `SUMMARY|`。

### 11.1 门禁 H1：**取证脚本的抓屏面**（隐私，事故已发生两次）
| 项 | 规则 |
|---|---|
| 判据 | 在给定根（默认 `shell`）下的 `.ps1`/`.cs`（排 `bin`/`obj`）中，**非注释行**出现 **大小写敏感的** `CopyFromScreen` / `BitBlt` ⇒ **FAIL**，并打印 `文件:行` + 该行原文 |
| 为什么大小写敏感 | 首版用不敏感匹配 ⇒ `ui-design-audit.ps1` 里作为**通道名**出现的小写 `copyfromscreen` 被误报 6 处（另 1 处是真·注释）⇒ 判据改为 `-cmatch`，误报归零 |
| 为什么"非注释" | 注释/文档里出现这些词不算命中（`popup-capture.ps1` 的 G1 说明行就是例子） |
| 允许清单 | 内置 `^shell/tools/popup-capture.*\.ps1$`（声明安全：PrintWindow + 精确 HWND）；另读 `shell/tools/privacy-allowlist.txt`（`<regex> # 理由`，**必须写理由**）。本工具自身也在清单里 —— 它含该 token 是因为要**搜索**它 |
| 反控（必须能失败） | 合成样例 `%TEMP%\t48-probe\bad-capture.ps1`（含非注释 `CopyFromScreen`）⇒ `FINDING|H1|VIOLATION|C:/…/bad-capture.ps1:6|\$g.CopyFromScreen(...)`、`CHECK|H1-privacy-capture-surface\|verdict=FAIL`、`EXIT=1`；**删掉样例 ⇒ 该命中数 0** |

### 11.2 门禁 H2：**引用了却不在版本控制里的证据**
| 子检查 | 判据 | 说明 |
|---|---|---|
| **H2a（FAIL，captain 指定判据）** | 任何 **已提交 `.md`** 引用为 `evidence/<name>` 的文件，若 **git 未跟踪**（`git ls-files --error-unmatch` 失败，典型原因：`.gitignore` 的 `*.log`）⇒ **FAIL**，打印 `文件 + ignored=True + citedBy=文档:行` | 这就是"被引用却被忽略 ⇒ FAIL" |
| **H2b（INCONCLUSIVE，非本工具职责）** | 引用存在但**磁盘上没有**该文件 ⇒ 打印 `FINDING\|H2b\|MISSING\|<name>\|citedBy=…`，判 **INCONCLUSIVE**（修引用是**该文档作者**的事） | 避免把别人文档的错误算成本门禁的 FAIL |
| 解析顺序 | 引用可能**就在文档旁边**（`ui2`/`ui3` 把证据放 `Features/<屏>/evidence/`）：先试 `<文档目录>/<name>` → `<文档目录>/evidence/<name>` → `<EvidenceDir>/<name>`（默认 `shell/Tests/evidence`） | 首版只查中心目录 ⇒ 误报 10 个"缺失"；补上按文档解析后降到 5–7 个且都是真缺 |
| **引用名的形状（2026-09-12 收窄，修我自己的假阳性）** | `evidence[/\\](...)` 的**第一个字符必须是 `[A-Za-z0-9_]`** —— **点号不再是合法首字符** | 我此前在本文档/`VERIFY_S1` 里**抄写允许清单条目** `^shell/App/tools/t26-evidence\.ps1$` 时，`evidence\` + `.ps1` 被捕获成证据名 **`.ps1`** ⇒ `FINDING\|H2b\|MISSING\|.ps1`（关联 7 行，含 `WORKSPACE.md`）。收窄后 `.ps1` 不再被捕获，而**无扩展名的真实引用**（如 `kernel/evidence/t45-control-typed`）**仍被抓到** |
| 该收窄的反控 | 探针文档同时含四种形态：真实存在（⇒ H2a 未跟踪）/ 不存在（⇒ H2b MISSING）/ 无扩展名（⇒ 仍被捕获且能解析）/ **路径式正则**（⇒ **不出现**） | 实测 `citedNames=3`、`.ps1` 零命中 |
| 反控（必须能失败） | 临时造 `shell/Tests/<evidence>/tmp-hygiene-probe.log`（被 `*.log` 忽略）+ `shell/docs/TMP_HYGIENE_PROBE.md`（引用它）⇒ `FINDING\|H2a\|UNTRACKED\|…\|ignored=True\|citedBy=…:3`、`EXIT=1`；**删掉两个探针 ⇒ 该命中数 0** | 探针用完即删，未留痕（本节示例故意不写成真实引文形态，免得门禁把自己文档里的例子当引用） |


#### 11.2.1 🔴 **"引用 = 承诺，只对已存在的东西承诺"**（captain 2026-09-12 立纪；依据 = 门禁**第一次不是全 PASS** 时抓到的正是"计划里对未来产物的承诺"）
```
现象（我的读数，2026-09-12 05:5x）:
  FINDING|H2b|MISSING|…/evidence/<t72 清单证据>|citedBy=Features/Favorites/t72-PRE-IMPLEMENTATION-CHECKLIST.md:45
⇒ 归因：`ui2` 的"开工前检查清单"里**引用了尚未产出的证据** ⇒ H2b 判 **INCONCLUSIVE**（**不是 FAIL**，也不是噪声 ——
  它是"**现在无法判定**"的正确表达；但**只要它是 INCONCLUSIVE，所有人的"门禁全绿"读数都不成立**）。
⇒ 🆕 **纪律（两种合规写法，选一）**：
   ① **描述而不点名** —— "证据将落 `Features/Favorites/evidence/` 下的新文件"（不写文件名 ⇒ 不构成引用）；
   ② 要点名就**先创建该文件**（哪怕只有一行 `PENDING` 头）再引用。
⇒ 🆕 **原理句**：**"引用 = 承诺，只对已存在的东西承诺"** —— 它在门禁里是**硬语义**：`cited ⇒ tracked + present`（H2a + H2b 两关）。
   与本文件既有那条**并列**：**"证据引用工装 ⇒ 工装必须被跟踪"**（管**工装**）；这一条管**未来产物**。
⇒ 违反的代价是"**全员读数失效**"而不是"你这一步错了"：所以计划类文档（checklist / 前置条件 / 卡面）**尤其**不要点名未来文件。
⇒ **H2 的名字提取器只吃"紧邻 `evidence/` 前缀"的名字**（裸反引号文件名不进引用集）⇒ 有一类假阴性；
   判定：**经 `t30` 与 §100 两例评估，暂不收紧**（captain 2026-09-12 采纳我的建议）——
   理由：真正的风险不是"裸名看不见"，而是"**我们自己会写出引用形状**"（我在 §100 上就吃过一次：连"记录它已退场"也算承诺）；
   收紧提取器会把大量"只是提到名字"的地方算成引用，**收益不确定、代价立刻显现**。**本条不改门禁。**
⇒ 🆕 **常规检查项：`staged ≠ committed`**（captain 2026-09-12 要求列为常规项；依据 = 今晚咬过我们一次）：
   **`git add` 了但没提交 ⇒ 门禁仍判"未入库"**（`H2a` 的 `ignored=False` 那一支）。实例：`ui2` 的 t87 五件长期 `staged` 未提交，
   门禁一直判红，直到 `79cab01` 真提交才消失。核验命令：`git diff --cached --name-only`（索引里有什么）
   **对照** `git log -1 --name-only`（上一次提交真落了什么）——两者不等就说明有"暂存但未提交"的在途件。
⇒ 🆕 **已删／已退场的产物不得再以带扩展名的全名出现**（captain 2026-09-12 采纳我的建议，属全队口径）：
   要么**拆成不可引用形状**，要么**写明"已退场、仅历史可查"**。依据（三个实例）：`ui3` 引的那份 `…-860-20260912-0534.txt` 在
   磁盘全树 / `git log --all` / `git ls-files` **三处都查不到**（他所谓"已 git add"没落地）；`captain` 自己在账本里写过该副本全名 ⇒ 当场把 `H2b` 打成 `MISSING`；
   我自己在 `§100` 里写过已退场旧件的全名 ⇒ 被 `H2b` 抓到。⇒ **canonical 只有一份**：
   `shell/App/Features/Servers/evidence/t30-probeB-icon-source.txt`（7,862 B / `E5211758243B` / 88 行 / tracked）。

### 11.3 本次清理（t48）
**① 先让"被引用却被忽略"归零**：把 5 个被引用却被 `*.log` 忽略的证据文件**强制入库**（`git add -f`，都只有 1.8–6 KB）：
```
（下表用 <evidence> 占位，免得门禁把本文档的"清理清单"又当成引用；真实路径均为 shell/Tests/evidence/）
<evidence>/spike-r5-emby.log             6058 B  F948769F5BEC   -> 已改名 .txt（见 ②）
<evidence>/t11-shell-nav-verifier.log    5239 B  70DB2F75A6E2   -> 已改名 .txt（见 ②）
<evidence>/t11-real-emby-e2e.log         1813 B  9E347FF95BF0
<evidence>/t27-callback-reply.log        4020 B  81766DF08B50
<evidence>/spike-probe-emby-stream.log   6024 B  4DA8BDDE0776
```

**② 再把"我的领地内、且只被我的文档引用"的两个改名 `.log` → `.txt`**（`.txt` 不被 `.gitignore` 吞，比 `git add -f` 稳；内容逐字节不变）。**纪律：改名与"全仓跟改引用"必须同一个动作做完**（今晚这类断链已出现 4 次：`ui3` 3 处、`ui2` 1 处、我 4 处——见 ③ 的教训）：
```
git mv <evidence>/spike-r5-emby.log           <evidence>/spike-r5-emby.txt           6058 B  F948769F5BEC（改名前后同哈希）
git mv <evidence>/t11-shell-nav-verifier.log  <evidence>/t11-shell-nav-verifier.txt  5239 B  70DB2F75A6E2（同上）
同步更新引用者：shell/docs/VERIFY_S1.md（L25 / L449 / L705 / L730）+ 本文档 §11.3（首版漏改本文档 ⇒ 门禁当场报了 4 处 MISSING）
```

**③ 只出清单、不动手（引用者在他人的领地里）**：以下 3 个文件仍以 `.log` 存在（已入库 ⇒ H2a 通过），但**改名需要先改别人的引用**，故交 captain 派单：

| 文件（shell/Tests/evidence/） | 引用它的文档:行 | 建议 |
|---|---|---|
| `t11-real-emby-e2e.log`（1813 B / 9E347FF95BF0） | `docs/EMBY_PLAYBACK_LOGIC.md:89`、`docs/REVIEW_T12_BRIDGE.md:146`、`docs/T12_PLAYBACK_BRIDGE.md:190` | 三处引用改 `.txt` 后我把文件改名（我可以执行改名，只要引用先改） |
| `spike-probe-emby-stream.log`（6024 B / 4DA8BDDE0776） | `docs/VERIFY_S1.md:527`（我的）、`docs/WORKSPACE.md:701`（captain 的） | 同上，等 WORKSPACE 那处先改 |
| `t27-callback-reply.log`（**4,020 B / 79 行 / `81766DF08B50`**，mtime `01:04:10.416`） | 产出侧已改为写 `.txt`（`App/Features/Library/LibraryPage.xaml.cs:533` 写死 `t27-callback-reply.txt`）⇒ 规范名 = **`.txt`**（`4,021 B / 79 行 / `B48B9798FB56``，mtime `00:23:42.999`） | ✅ **已同步**：两个文件**都在库、都在盘**、身份各自自洽（`.log` 与 `.txt` 字节/行数/哈希三者皆不同 ⇒ 是**两轮不同运行**，不是"同一份字节两个名字"）；`.log` 属**历史件**，按 §11.10 的命名口径**保留、不删**（旧引用指向它**不是缺陷**） |

> ⚠️ 余下**未入库/未解析**的引用项落在他人的领地（例：`shell/App/Features/Servers/<evidence>/t30-dialog-host.log` 属 `ui3`，`t31-aggregate-selftest.log` 属 `ui2`），**本工具只出清单、不动手**（`shell/App/**` 不是我的 inScope）。
> ⚠️ **采样是活动靶**：这些计数在多 agent 并行改文档时会漂（我同一次会话内实测 `citedNames` 39→41→40、`missing` 5→7）⇒ 引用读数必须带**采样时刻 + HEAD**。

#### 11.10 🔄 证据文件命名口径**更正**（captain 2026-09-12；本条废止"证据必须 `.txt`"这句话）
```
· `.gitignore:69` = `!**/evidence/**/*.log` ⇒ **证据目录下的 `.log` 不被忽略**（`git check-ignore` = `<none>`，实测三个在库 .log 全是 `<none>`）
· `H4` 恒 `PASS` / **advisory** / **never FAIL**，历史 `.log` 一律 grandfathered
⇒ **正确口径**：**原始运行输出可以保留 `.log`；手写报告用 `.txt`/`.md`**。
⇒ "证据必须 `.txt`"在**事实 120 那个时代成立，现在已过时**；**引用 `.log` 不是缺陷**，不要为改名去动别人的引用（上面那张表因此**只是可选项，不是欠账**）。
⇒ 现场读数（2026-09-12 04:35:20，HEAD `1fa9ea1`）：
   `shell/Tests/evidence/t27-callback-reply.log` check-ignore=`<none>` tracked=是 ｜ `.txt` 同 ｜ `settings-key-reconcile.txt` 同
```

### 11.5 判据细化（captain 2026-09-12 四条裁定，已落地）

1. **H1 改为"来源与边界"判据（不是 token 级禁令）**：允许 `CopyFromScreen` **当且仅当** 矩形来自**显式窗口句柄** + 窗口**属于被点名 PID** + **拒绝** ≥95% 虚拟桌面 / 空 HWND / 桌面类 + **落盘来源断言**（与 `popup-capture.ps1` 的 G2/G3/G4 同款）。实现：脚本若**已具备**这四件套（桌面类拒绝 + 覆盖度判断 + PID 判定 + 断言/sidecar 写入，按非注释文本检测）⇒ 判 `BOUNDARY-OK`；否则必须进 `privacy-allowlist.txt` **逐条写理由**，再不然换用 `popup-capture.ps1`。当刻 2 条 `VIOLATION` 均带 **requiredFix**（甲/乙/丙 三条路）。
   ⚠️ 为什么改：内核 D3D 合成窗口 `PrintWindow` 抓不到（全白/全黑），那类窗口**只能**按窗口矩形 `CopyFromScreen` ⇒ 原 token 级禁令**太钝**。
   **分级落地（captain 2026-09-12 #4，`ui3` 提出）—— 分级必须是机器可查的字段，不是靠读者猜的散文**：
   - 档 A（硬 FAIL）= **未先前台化**就 `CopyFromScreen`；档 B（可登记）= 先 `SetWindowPos(TOPMOST)`/`SetForegroundWindow` **再按 `GetWindowRect` 抓本进程窗口**（`win-capture-pid.ps1:112` 走这档；`PrintWindow` 对 D3D 面是黑帧，这条兜底必须留）。
   - 允许清单条目**必须**带两个字段：**`rect-source=<foreground-window|own-window>`** + **`owner=<name>`**，缺任一 ⇒ `FINDING|H1|VIOLATION-MISSING-FIELD`（**仍 FAIL**）。
     🔴 **两个值是两个不同的前提，不得合并**：`own-window` = 矩形来自脚本**自己的 HWND**、不需要前台化；`foreground-window` = **必须先前台化**，否则 `CopyFromScreen` 抓到的是"上面那个东西"。
   - **`SEARCH-ONLY`（新增的机械豁免，替代自我豁免）**：某文件的**每一处** token 都落在**引号字面量内**（本门禁自己在**搜索**这个词）⇒ 判 `NOTE|H1|search-only-literal|…`，**不需要任何条目**。若有**一处**未被引号包住（真实调用）⇒ 该文件**全部命中**按真实处理。
     ⇒ 门禁此前给自己写的那条 allow-list **自我豁免已删除**：一个隐私允许清单里的自我豁免，正是日后会藏住真违规的形状。
   - **四护栏改为只在非注释行上认**（原来注释里写一句 `Progman` 就算有护栏 = 承诺不是护栏；同"形状≠危害"）。检测仍由门禁做，**理由必须由 owner 写**。
   - **三条反控（`%TEMP%\t54`，跑完即删）**：真实抓屏 + 无护栏 + 无条目 ⇒ `VIOLATION`；条目存在但缺字段 ⇒ `VIOLATION-MISSING-FIELD` + NOTE 打印 `rect-source=<MISSING> owner=<MISSING>`；条目带 `rect-source=own-window owner=verifier` + 四护栏（在代码行上）⇒ **`ALLOWED`**、`registered=1`、`violating` 4→3。另有一条只含引号字面量的探针 ⇒ 3 条 `search-only-literal` NOTE、不计违规。
   - 🔴 **两个独立条件 ⇒ 并列计数，名字必须说清它数的是什么**（captain 2026-09-12 裁定：明细能出 `REGISTERED-NO-BOUNDARY` 而汇总只有 `registered=0` ⇒ 读的人以为"没登记"）。**命名缺陷是长期那条**：旧 `registered=` 数的是 `allowed`，却被读者（含 captain）读成 `entriesMatched` ⇒ 一次根因误判，还可能让 owner 去改**本来正确**的文件（`ui` 差点重新登记）。
     ⚠️ **时间轴（不是"谁误诊"）**：captain 采样时清单里是**无 ` # `** 的写法（`WORKSPACE.md:2776` 原文引用），**旧解析器确实永不匹配** ⇒ 他的 `registered=0` 对**那一刻**成立；`ui` 随后补上 ` # `，我的复核才对**当刻**成立。**两条都真，测的不是同一刻** —— 详见 `VERIFY_S1 §56`。
     ```
     nonCommentHits=      非注释行上的 token 命中总数
     searchOnly=          命中全部落在引号字面量内（本门禁自己在搜索这个词）=> 不算违规
     real=                真实抓屏调用数（= nonCommentHits - searchOnly）
     entriesMatched=      路径匹配到条目（**不考虑字段完整性、不考虑护栏**）← 消歧专用
     allowed=             条目齐备（含 rect-source/owner）**且**脚本自身在非注释行上展示四护栏 ⇒ 唯一"放行"数
     registeredNoBoundary=条目齐备但脚本缺护栏 => tag REGISTERED-NO-BOUNDARY，**仍算违规**（登记这一半成立、脚本那一半没做）
     violating=           既非 searchOnly 也非 allowed 的命中数（= real - allowed）
     旧字段 `registered=` 已删除：它数的是 `allowed`，却被读成 `entriesMatched` —— 一个名字骗了一次根因判断。
     ```
     ⇒ **本项是"可读性"改动，不新增任何门禁政策**（captain 已冻结新增门禁规则；冻结针对的是**加新政策**，不是修可读性）。
     **门禁在代码行上认的四组护栏关键词**（护栏写在注释里 = 承诺不是护栏）：
     ```
     桌面类拒绝      : Progman | WorkerW | Shell_TrayWnd
     ≥95% 虚拟桌面   : 0.95 | 95% | virtualDesktop | screenArea | VIRTUALSCREEN
     PID             : GetWindowThreadProcessId | TargetPid | -Pid | ProcessId
     落盘来源断言    : sidecar | ASSERTION | sha256_12 | SOURCE ASSERTION
     ```
     **当刻实测（repo 态）**：`nonCommentHits=5 searchOnly=3 real=2 entriesMatched=2 allowed=0 registeredNoBoundary=2 violating=2` —— **这行自己就说清了"登记这一半成立、只差脚本侧四护栏"**，不用再去翻明细。
   - **条目分隔符**：偏好 `<regex> # <reason> rect-source=… owner=…`；**不带 ` # ` 也认**（把行尾字段剥掉），但会打 **`NOTE|H1 allow-list entry WITHOUT the ' # ' separator: pattern='…'`** —— 让这种写法**可见**而不是静默（旧实现会把字段文本并进正则 ⇒ **永不命中** ⇒ "看着登记了、实际没登记"；好消息是它 **fail-closed**，没匹配上仍报 `VIOLATION`）。
     反控（`%TEMP%\t58`，合成清单 + 三个带护栏的探针）：` # ` 形式 ⇒ `ALLOWED`；裸字段形式 ⇒ `ALLOWED` **且** 附那条 NOTE；只有 `owner=` 的残条目 ⇒ `VIOLATION-MISSING-FIELD`；清单里交错的空行/注释行被忽略 ✓。
2. **新增 H1b（屏幕级矩形源）**：`PrimaryScreen` / `VirtualScreen` / `SystemInformation` / `GetSystemMetrics(0|1|78|79)` **仅当它喂给取图矩形时**才 FAIL；**用 `GetSystemMetrics` 去算 ≥95% 覆盖度是合法用法**（首版把它也判 FAIL ⇒ 立刻改成"同行有 `CopyFromScreen`/`$rect=` 才算"，并把其余用法降为 `NOTE`）。
3. **H2 口径**：判据维持 **"被引用 ⇒ 必须在库"（`.log`/`.txt` 都算合规）**；`.txt` 只是**新增证据**的强制形态 ⇒ 对仍被引用的 `.log` 发 **`NOTE|cited-evidence-is-log`**，**不 FAIL**（理由：收紧会迫使历史 `git add -f` 的证据再改名，每次改名都打断更多引用 —— 本次 8–11 处缺失里过半就是这么来的）。
4. **新增 H3（空 catch，整文件多行正则）**：判据 = 覆盖**整个文件**的 `catch\s*(\([^)]*\))?\s*\{\s*\}`（**行级正则抓不到跨行 `catch\n{\n}`**）；**例外形态**：窄类型 + 理由在**同句**或**紧邻上一行**（含 `try` 前一行、整条 catch 链共享）判**合规**。当刻读数见下（**report-only，INCONCLUSIVE**：修是各自 owner 的事）。
   ⚠️ 与 captain 的数字**不一致**：他实测 `shell/**` **14**（untyped），我实测 **27（untyped）+ 4（typed，无理由）= 31**（`csFiles=133`，采样见下）⇒ 按"同一判据两把仪器"处理，**逐文件分布已列出**以便对账（Top：`shell/Services/Infra/AppDataDir.cs` 4、`shell/App/Features/Search/SearchLog.cs` 4、`SearchRunner.cs` 3、`KernelHost/InboundCallbackEndpoint.cs` 3、`Program.cs` 2、`JsonStorage.cs` 2、`AddServerDialog.xaml.cs` 2）。**我不据此断言谁对** —— 需要他的命令行才能对齐口径。
   🔴 **我自己的第 2 个假阳性（captain 2026-09-12 #6-1 问出来的，已修）**：回看**从 catch 行的上一行开始**，**从来没看 catch 行本身** ⇒ 理由写成**行尾注释**的形态（`catch (IOException) { }   // 有意忽略：…`）被判违规。实测后果：`Program.cs:41/42/94/95`（理由在行尾）、`Services/Credentials/SecureKvStore.cs:171`（理由在 catch 行、`{` 在下一行）—— **全部合规**，`CODE_STANDARD §5`③ 明文允许。
     修法：先看 catch 行有无 `//` 或 `/*`（同行 = 合规），再看上一行。修后 **typed 13 → 0**，`H3 = 13 untyped`（当刻 01:09:16 / HEAD `b0c4516`，`csFiles=136`）；反控：探针里"同行尾注释"的 catch **不命中**、"无注释"的 catch 命中。
     ⇒ 教训与 §52 同族：**判据的"回看窗口"少一格，就会把一整类合规写法判成违规**（把"我没看到理由"当成"没有理由"）。
   - **`H3` 的例外形态识别，位置口径写死（captain 2026-09-12 派单要求"别留灰区"）**：合规位置 = **catch 行本身（理由在行尾或在花括号内，均算）** 或 **紧邻上一行**（含 `try` 前一行、整条 catch 链共享）。
     | 形态 | 判定 |
     |---|---|
     | `catch (IOException) { }   // 有意忽略：x`（**同行、行尾**） | ✅ **零 FINDING** |
     | `catch (IOException) { /* 有意忽略：x */ }`（**同行、括号内**） | ✅ **零 FINDING** |
     | `catch (IOException) { }`（无任何理由） | 🔴 **必报 FINDING** |
     | `catch (IOException)\n{\n}   // 理由在收尾括号那一行`（理由在 catch **之后**） | 🔴 **报 FINDING** —— **本文写死：不合规**（口径"同一行或紧邻上一行"指的是 **catch 那一行**；理由写在收尾括号行既不是同一行也不是上一行）。修法是一行搬迁，**不为它返工** |
     | `catch (Exception ex) { }`（**宽类型** + 无理由） | 🔴 **必报 FINDING**（§5③ 只允许忽略**窄类型**） |
     | `catch (Exception ex) { }   // 理由`（宽类型 + 有理由） | ⚠️ **不算 FINDING，但发 `NOTE\|H3\|broad-type-ignored`** —— 理由救不了宽类型；**"有类型就一律放过"正是这条要防的陷阱** |
     反控（探针 `%TEMP%\t56`，六例逐个核，跑完即删）：上述六种形态**全部**按表命中；`H3` 当刻（含探针）`emptyCatch=16 (untyped=13 typed=3)` = repo 13 untyped + 探针 3 typed。
   - 🔴 **两个同族但根因不同的仪器缺陷，必须分开记（captain 事实 195 / 199，禁止合并）**：
     - **事实 195 = 分支缺失**：`H3` 压根**没实现** `G2` 承诺的例外形态识别（判据少一条路）。
     - **事实 199 = 窗口边界**：`H3` 实现了回看，但**回看窗口从 catch 行的上一行开始、从不看 catch 行本身** ⇒ **一整类"行尾理由"写法**被误判（判据的路少一格）。
     ⇒ 两者都会让**合规代码被判违规**，但修法不同（补分支 vs 挪窗口一格）；**记成两条**，下次遇到同类先问"是缺分支还是缺窗口"。
   - **`H3` 与 `H3b` 的重叠必须讲清，否则会被读成两个独立问题**：当刻 `H3` 的 13 条**全部 UNTYPED**，且**逐条落在 `H3b` 的 `BARE` 集合里**（`SearchLog.cs:49/67/93/99`、`SearchPage.xaml.cs:187`、`SearchRunner.cs:465/475/489`、`AddServerDialog.xaml.cs:333/629`、`InboundCallbackEndpoint.cs:135/144/184`）⇒ **同一批活、两个判据从两个角度看它**：`H3` 问"体是不是空的"，`H3b` 问"有没有点名类型"。**派单时按 H3b 的 `BARE` 清单走一次即可，别按两个清单派两遍。**

5. **新增 H4（生成端，advisory）**：`**/evidence/**` 下出现 `*.log` ⇒ 报警，但 **永不 FAIL、不影响 SUMMARY 的 verdict**。
   - 分类：**已在 `HEAD` 里**（即"被引用 ⇒ `git add -f` 入库"的历史文件）⇒ `INFO|H4|historical-evidence-log|cited-and-tracked-grandfathered`；**其余**（新出现的）⇒ `NOTE|H4|evidence-log-extension|<path>|advice=.txt is mandatory for NEW evidence`。
   - **为什么是 NOTE 不是 FAIL**：历史 `.log` 已入库且被引用，判 FAIL 会**逼它们再改名**，而今晚已证明每次改名都打断引用（断链 8 处）。⇒ **新出现的报警，历史只提示**。
   - **枚举必须可对账**（今晚两人各犯一次"枚举静默少文件"）：`CHECK|H4…` 的 `measured` 里带 **`scannedEvidenceFiles=<n>`**，便于事后核。
   - **反控（本项唯一形态）**：造一个 `%TEMP%` 样例证据目录（1 个合法 `.txt` + 1 个 `.log`）⇒ **只报那个 `.log`，`.txt` 不报**；实测 `scannedEvidenceFiles 116→120 / logs 23→24 / new 19→20`，`.log` 命中 1、`.txt` 命中 0，删除探针后计数回落。

5. **新增 H3b（真正的危害：无类型 catch）**：判据 = **任何 `catch` 不带异常类型 ⇒ 违规**，不论体是否为空。
   - ⚠️ **口径收窄（captain 2026-09-12 裁定 #2，`services` 问出的自相矛盾）**：**窄类型 + 理由在同一行（理由在 `{}` 内 **或** 行尾，均可）或紧邻上一行 ⇒ 合规**，与 `CODE_STANDARD §5` ③ 的明文允许一致（`catch (XxxException) { /* 有意忽略：<原因> */ }`）。
     **旧措辞"理由写在花括号内不算合规"是错的、已删除** —— 它与 `CODE_STANDARD §5`③ 直接冲突，且导致 **9 处合法窄类型站点被误判**（全在 `shell/Services`）。
   - 两个子类：
     - `FINDING|H3b|BARE|…` = **无类型 `catch`**，体为空或有真实处理（例如 `catch { Log(e); }`）；
     - `FINDING|H3b|REASON-INSIDE-BRACES|…` = **无类型 `catch` 的体只有注释**（`catch { /*忽略*/ }`）—— 抓的是"**看着像解释了、实际仍吞掉一切**"这种**假装**。
     **有类型的 `catch` 一律不归入这两个子类**（只看类型 + 理由位置是否合规）。
   - **合规位置**：理由必须**同一行**或**紧邻上一行**（含 `try` 前一行、整条 catch 链共享）。**行内（花括号内）理由 = 合规**（见上）。
   - **非注释过滤必须保留**：注释里**原样引用** Dart 的 `catch (_) {}` 会被整文件正则命中 ⇒ 扫描前先剥行注释、再剥块注释（`catch` 引用常出现在注释里）。反控样例里就放了这条引用，**不命中**才算过。
   - 🔴 **棘轮（captain 2026-09-12 裁定 B：改"度量对象"，不是加政策）—— 度量对象 = `production` 计数；夹具另有额度**
     ```
     verdict = PASS 当且仅当 四个 new 全为 0：
        newProdBare      = productionBare - -H3bProdBareBaseline   (默认 0)
        newProdRib       = productionRib  - -H3bProdRibBaseline    (默认 0)
        newUnmappedBare  = unmappedBare   - -H3bUnmappedBareCap    (默认 0)
        newUnmappedRib   = unmappedRib    - -H3bUnmappedRibCap     (默认 0)
     total（bareCatchTotal / ribTotal）继续打印**只为完整性，永不作为判据**
     ```
     **为什么改**（钝化的实证，见 §63②）：存量清零后，**total 基准（54→22/9→5）只剩夹具份额** ⇒ 实测"生产新增 1 处裸 catch ⇒ total=1 ≤ 22 ⇒ **PASS**"。
     ⇒ 这是"**度量对象错了**"，不是"阈值松了" ⇒ 改对象。**生产新增任何一处即 FAIL**（prodBaseline=0）。
     🔴 **基线 = 当刻实测（只降不升）；存量清零后基线必须跟着降到 0。** —— 否则"清零了但额度还在"（22 个免费额度），任何新增裸 `catch` 都咬不住。
     🔴 **教材（captain 2026-09-12 要求原样保留）**：**"默认 22/5 放行了一处真实新增"** —— 实测：**同一次注入**（一处裸 `catch`）**两套基线两种结论**：基线上写 `22/5` ⇒ `PASS|new=0`（**真实新增被放行**）；基线写 `0/0` ⇒ `FAIL|new=1`。⇒ 收紧不是"更严的风格"，是**让判据恢复本来的功能**。
     当刻这就是事实：`bare 0 / rib 0`（`production` 与 `unmapped` 两组都是 0）⇒ 两组的**和 ≡ total 基线 0**，故 CHECK 行另打印 `totalBaselineBare/ totalBaselineRib/ newTotalBare/ newTotalRib/ newTotal` 供对账（**不必手算**）。
     ⚠️ **参数名说明（避免下一个人找不到旧名）**：裁定 B 把旧的 `-H3bBareBaseline` / `-H3bRibBaseline`（**total** 口径）**替换**为四个分组参数 `-H3bProdBareBaseline` / `-H3bProdRibBaseline` / `-H3bUnmappedBareCap` / `-H3bUnmappedRibCap`；captain 要的"**total 0/0**"由 `totalBaselineBare/totalBaselineRib` **打印**体现（= 两组之和），**不再保留一个"只打印不判"的死参数**。
   - **三条反控（裁定 B 落地时实测，必须能失败）**：
     ```
     ① 生产侧新增一处裸 catch（%TEMP% 探针 + 归属表映射到 ui2）: FAIL|productionBare=1 prodBaseline=0 **newProdBare=1**  ✓
     ② 同一处但落在夹具额度（不映射 ⇒ UNMAPPED）              : FAIL|unmappedBare=1 unmappedBareCap=0 **newUnmappedBare=1**  ✓
     ③ 删掉探针                                              : PASS|productionBare=0 unmappedBare=0 全部 new=0  ✓
     ```
     ⇒ 判据里**必须能看到是哪条额度超标**（`newProdBare/newProdRib/newUnmappedBare/newUnmappedRib` 四个字段就在 CHECK 行里）。
   - **基线历史（只降不升）**：`bareCatch` **54 @HEAD f1da56e → 22**；`reasonInsideBraces` **9 → 5**；**裁定 B 之后 total 不再作判据**，
     `productionBare/productionRib` 的基线当刻都是 **0**（`baselineAt=3a475bb`）。参数：`-H3bProdBareBaseline` / `-H3bProdRibBaseline` / `-H3bUnmappedBareCap` / `-H3bUnmappedRibCap` / `-H3bBaselineAt`。
   - 理由：存量里相当一部分是**顶层兜底处理器**（进程入口/回调端点/后台计时器），改成 `catch (Exception ex)` + 记录**即算合规**（机械可改，**不要重构控制流**）；⇒ 与 `kernel` 上的 `EnforceCodeStyleInBuild` 同一套"先量后治 + 只禁新增"。
   - **输出必须自带对账字段**（否则接手人无法判断这行数字是"存量"还是"新增"）：
     `baseline=<n>` + **`baselineAt=<HEAD>`**（基线是在哪个提交上量的）+ `new=<n>` + **`byOwner=<owner>=<n>,…`**；**每条 `FINDING|H3b` 行尾带 `owner=<owner>`**，使派单可以直接切行，不必人工归属。
     `byOwner` 覆盖**两个子类合计**（`BARE` + `REASON-INSIDE-BRACES`）—— 因为两类的修复责任人是同一个。
   - 🔴 **`owner` 绝不猜测**：归属**无法从路径推导**，所以放在 `shell/tools/hygiene-owner-map.txt`（`<path-regex> <owner>` 逐行，**人为维护**；captain 改派就改表）。表里查不到的路径打印 **`owner=UNMAPPED`**，宁可显式未知也不猜给错人。
     **反控（本项唯一形态）**：`-OwnerMapFile` 指向不存在的表 ⇒ `byOwner=UNMAPPED=36` 且每条 FINDING 行都是 `owner=UNMAPPED`（证明分桶**承重**、不是装饰）；指回真表 ⇒ 按表分桶（`byOwner=ui2=7,ui3=20`）。
    - 🔴 **时间身份（`services` 2026-09-12 提议，第三次「你采的是修复前那版」之后落地）**：`CHECK|H3b-bare-catch|measured=` 末尾追加
      **`scannedAt=<yyyy-MM-dd HH:mm:ss.fff>`**（这次读取发生在何时）+ **`newestScannedMtime=<文件名>@<mtime>`**（本次扫过的最新一个 `.cs` 是哪个、什么时候改的）；
      **每条 `FINDING|H3b` 行追加 `fileMtime=<mtime>`**。
      理由：`H3b is red` 与 `H3b was red before the owner fixed it` 是**两条不同的信息**，旧输出**分不出来**（同族：`registered=` 那次改名、`capturedAt+head` 那次定名）。
      **这不加任何新判据**（不新增任何 FAIL 条件），只让既有裁决**自带日期** —— 与 captain 事实 216「每条读数自带身份」同一口径。
      **实测**：真仓 02:43:43.375 ⇒ `newestScannedMtime=DetailPage.xaml.cs@2026-09-12 02:40:57.191`（一眼看出"源在两分钟前刚被动过"）。
      **反控（夹具在 `%TEMP%`，cwd 根 = 夹具仓）**：`FINDING|H3b|BARE|shell/Services/Whatever.cs:7|owner=UNMAPPED|scope=unmapped|`**`fileMtime=2026-09-12 02:43:44.091`**`|catch {` + `verdict=FAIL|…|newUnmappedBare=1|…|newestScannedMtime=Whatever.cs@2026-09-12 02:43:44.091` ⇒ 字段真的会印、棘轮也真的仍会响。
   - 当刻（01:0x，见 §52）：`CHECK|H3b-bare-catch|verdict=PASS|bareCatch=22 baseline=22 baselineAt=5b577f2 new=0 reasonInsideBraces=5 baseline=5 new=0 byOwner=ui2=7,ui3=20`
     —— 收窄口径后 `services` 的 9 条**全部消失**（它们是合法窄类型，本就合规），总命中 36→27；`BARE` 22 / `RIB` 5（5 条都是"裸 catch + 只写注释"：`SearchLog.cs:42`、`SearchRunner.cs:373`、`InboundCallbackEndpoint.cs:135/144/184`）；`ui2` 从 8 降到 7（`LocalVideoPathSemantic.cs:167` 已修）、`ui3` 仍 20。**这就是棘轮要的形状：存量只降不升。**
   - ⚠️ **点名类型只是最低要求；OCE 这一层不进 H3b、不是门禁项**（captain 2026-09-12 更正口径）：真正的危害有两层 —— ① **吞掉一切异常（含 `OperationCanceledException`）**：取消被吃掉会让"用户取消"变成"看起来失败了"（本项目已踩过的静默失败同族）；② **无法区分可恢复与致命**。
     > **在 `async`/带 `CancellationToken` 的路径上，`catch` 不得吞掉 `OperationCanceledException`** —— 要么 `when (ex is not OperationCanceledException)`，要么显式 rethrow。
     该句只写进 `CHECK|H3b-bare-catch` 的 `note=` 作**提示**，门禁**不判定**，**也不是 H3b 的子类**。
🔴 **为什么不能机械批量改**（`services` 的反对意见，captain 采纳）：批量加 `when (ex is not OperationCanceledException)` **会改变控制流** —— 原先被吞掉的取消变成**向上抛**，而部分调用方处理不了 OCE ⇒ **未观察异常**（比吞掉更糟）。⇒ **先分类后改**：**A** = 链内已有 OCE 分支（不动）／**B** = 加 `when` 过滤即可（调用方能接）／**C** = 需同时改调用方（单独立项）。

6. **新增 H5（证据身份：bytes + sha12 逐条对哈希）**：扫 `**/*EVIDENCE*.md` 的**表格行**「`evidence/<file>` ｜ `<bytes>` ｜ `` `<sha12>` ``」，逐条与磁盘核对 —— **字节数相等**（允许千分位逗号）、**sha12 相等**（sha256 前 12 位，大写；比较时大小写不敏感，小写值另发 `NOTE`）。
   - 不等 ⇒ `FINDING|H5|HASH-MISMATCH|<doc>:<line>|<path>|expectedBytes=.. actualBytes=.. expectedSha12=.. actualSha12=..`（**FAIL**，同时打印期望与实际）；
   - **文件不在磁盘 ⇒ 这里不 FAIL**（那是 H2b 的职责）⇒ 只发 `NOTE|H5|file-absent|…`，避免两项重复报同一件事；
   - 输出带 **`scannedRows=<n>`** 对账字段（与 H4 的 `scannedEvidenceFiles` 同目的：防枚举静默少扫）。
   - 🔴 **为什么必须有它**：H2a/H2b 只证明"文件存在/在库"，**不证明文件是它自称的那个文件** —— 文档里写死的 bytes/sha12 一旦因重跑覆盖而失真，**评审从表格上看不出来**（数字长得像真的）。
   - **四条反控（`%TEMP%\t53-h5` 合成探针，跑完即删；常驻清单）**：
     | # | 探针行的形态 | 必须的表现 |
     |---|---|---|
     | ① 正样例 | 正确 bytes + 正确**大写** sha12 | **零输出**（连 NOTE 都没有）—— 防"为能失败而把合规行打掉" |
     | ② 写法差异 | 正确 bytes + **小写** sha12 | **只发 `NOTE\|H5\|lowercase-sha12`，绝不 FAIL**（小写只说明写法与项目约定不同；比较是大小写不敏感的） |
     | ③ 负样例 | 错 bytes + 错 sha12 | **FAIL** 且同时打印 `expectedBytes/actualBytes/expectedSha12/actualSha12` |
     | ④ 缺文件 | 引用的文件不在磁盘 | **只发 NOTE**（`H2b` 才是缺文件判定的 owner，避免两项重复报同一件事） |
     实测（01:0x，`-ExtraScanPath %TEMP%\t53-h5`）：①②④ 三条各自只出应有的行，③ 出 `HASH-MISMATCH`；`scannedRows=16 mismatch=5 absent=1`（其中 4 行/1 行来自探针）。
     ⇒ **"写法差异"与"身份不符"在反控里各有位置**：小写值可以放过，**值不符必须失败**。
   - ⚠️ **事实 205（captain 2026-09-12）：哈希一致 ≠ 内容现行** —— `H5 PASS` 只证明"文档声明的 bytes/sha12 == 磁盘上那个文件"，**不证明那张图是当前渲染**（实例：`ui3` 从旧证据里恢复回来的两张设置页图是 §9 之前的渲染，表与磁盘**完全对得上**，但内容已过期）。
     ⇒ **终局读数里必须把两件事分开写**：① **身份**（`H5`，可机械核）② **现行性**（只能靠"重截/重跑"这个动作取得，**任何哈希都替代不了**）。`H5 PASS` 不得被表述为"截图是最新的"。
   - 当刻：`CHECK|H5-evidence-identity|verdict=FAIL|scannedRows=13 mismatch=3 absent=2`（3 处全在 `shell/App/Features/Servers/EVIDENCE_T30.md` 140/141/142，属 `ui3`）。

**当刻读数（采样 00:49:17.318，HEAD 1fa7167）**：
```
CHECK|H1-privacy-capture-surface|verdict=FAIL|scanned=153 nonCommentHits=5 violating=2
CHECK|H1b-no-screen-level-rect|verdict=PASS|screenRectSources=0
CHECK|H2a-cited-evidence-tracked|verdict=FAIL|citedNames=42 untracked=5（他人在途）
CHECK|H2b-cited-evidence-present|verdict=INCONCLUSIVE|missing=5（归文档作者）
CHECK|H3-empty-catch|verdict=INCONCLUSIVE|csFiles=133 emptyCatch=16 (untyped=16 typed=0)
CHECK|H3b-bare-catch|verdict=FAIL|csFiles=133 bareCatch=54 reasonInsideBraces=9
CHECK|H4-evidence-log-extension|verdict=PASS|scannedEvidenceFiles=112 logs=23 historical=4 new=19
CHECK|H5-evidence-identity|verdict=FAIL|scannedRows=13 mismatch=3 absent=2
```

### 11.7 🔴 「形状 ≠ 危害」（元纪律，2026-09-12；一晚出现 4 次同类）
> **立检查时必须先问"危害是什么"，再检查那个危害 —— 不要检查最容易匹配的那个写法。**

| 检查抓的"形状" | 真正的"危害" | 漏掉的邻居 |
|---|---|---|
| 空体 `catch { }` | **无类型 catch 吞掉一切** | `catch { /*注释*/ }`（H3b 补上） |
| 出现 `CopyFromScreen` 这个 token | **抓到桌面 / 别人的窗口** | 按窗口矩形合法取图（内核 D3D 窗口 `PrintWindow` 全白/全黑，只能这样抓） |
| 注释里出现 `§x.y` | **注释不自洽（只有指针 / 过程叙述）** | 自洽 why + 末尾短引用（应保留） |
| 证据用 `.log` 扩展名 | **证据被静默丢弃 / 引用了却不在库** | 已 `git add -f` 入库的历史 `.log`（再改名会打断引用） | 4 |
| **只查"引用是否存在 / 在库"** | **文档里的 bytes/sha12 与磁盘不一致 ⇒ 证据身份是假的** | 无（这一格此前**根本没有检查** ⇒ H5 补上） | **5** |
| `catch` **没有异常类型**（H3b 抓到的形状） | **在该路径上吞掉 `OperationCanceledException`** ⇒ "用户取消"变成"看起来失败了"（本项目已踩的静默失败同族）；以及**无法区分可恢复与致命** | **先分类后改**（A 链内已有 OCE 分支 ⇒ 不动／B 加 `when (ex is not OperationCanceledException)` 过滤／C 需同时改调用方）；**机械批量加 `when` 反而改变控制流 ⇒ 未观察异常** | **6** |

⇒ 元纪律不变，**补一句**：**"存在与在库"只证明文件在，不证明文件是它自称的那个文件** ⇒ 身份必须**逐条对哈希**（`H5`），而不是只对路径（`H2a/H2b`）。第 5 例与前四例的**质**不同：前四例是"检查抓错写法"，这一例是"**根本没有检查**"。

> **第 6 例（captain 2026-09-12 补充）：点名类型只是最低要求，不是判据终点。**
> **在 `async`/带 `CancellationToken` 的路径上，`catch` 不得吞掉 `OperationCanceledException`** —— 要么 `when (ex is not OperationCanceledException)`，要么显式 rethrow。
> 这一层是**代码审查项，不是门禁项，也不归入 H3b 子类**（"该路径是否携带 CT"要看调用链，一个正则裁不了；且**批量加 `when` 会改变控制流** —— 见 §11.5-5）⇒ 只写进 `CHECK|H3b-bare-catch` 的 `note=` 作提示，留给评审人。**危害分级**：H3b 覆盖"吞掉一切异常"的**最宽**形态（可机械枚举），OCE 覆盖"吞掉特定语义异常"的**更深**形态（需读调用链）—— **两层都要，但只有第一层能自动化**。（这条也修正了我自己：**能机械枚举 ≠ 能机械修** —— 判据可以机械，修法必须人审。）

**同族：归属不可推导 ⇒ 不许猜**（第 6 例的仪器面）：`byOwner=`/`owner=` **不得**由路径前缀推断责任人（会派错单），必须读 `shell/tools/hygiene-owner-map.txt`；查不到就打 `UNMAPPED`。反控 = `-OwnerMapFile` 指向不存在的表 ⇒ 全 `UNMAPPED`（证明分桶承重）。**同一条纪律推广**：凡是"只有人知道"的映射（归属、屏↔卡片、契约↔实现），落成**带日期的人维护表 + 显式 UNKNOWN**，而不是塞进代码里的启发式。

**同族第三条（captain 事实 203，2026-09-12）：复核一个"根因结论"之前，先确认被复核对象的当刻状态是否已被改过。**
> 我复核 captain 的"条目没匹配上"时，`ui` **已经**把清单从"无 ` # `"改成"有 ` # `" ⇒ **我测的不是他测的那一份文件**，我的复核**无法否证**他的读数。两条结论都真，只是**隔着一次别人的改动**。
- 落地做法：复核前先取**被复核对象的身份读数**（行数/哈希/mtime 或 `git log -1 -- <path>`），并**先问"这份东西在两次采样之间被谁改过吗"**；改过 ⇒ 把结论**按时间轴分段陈述**，不要写成"谁误诊"。
- 三面同一条纪律：**事实 200**（红了与谁红的之间隔着时间轴）／**事实 202**（活动靶：读数会漂）／**事实 203**（复核前先确认状态）。
- ⚠️ 修订他人结论的记录时必须**同时留两条**（"当时成立"与"当刻成立"），否则库里会留下一条**假的"某某误诊"**记录 —— 那比原来的含糊更坏。

**同族："两把仪器对账"**（同一判据、两次扫描数字不同时必须走完的流程）：要**命令行 + 根 + 正则** → 复算 → 定权威口径 → 入台账更正。今晚三条实例：
- 我 vs captain 的 H3：**14 vs 31** ⇒ 根因是他用 `Get-ChildItem -Include` 配**位置路径**（**静默少枚举文件**）；
- `services` 自报"裸 catch 11 处" ⇒ 他的正则 `^\s*catch\s*(\{|\()` **把带类型的也算进去了**（口径过宽）；
- 我 vs `ui` 的 `-Include` 配非通配 `-Path`（同一坑，`VERIFY_S1 §31①` 已记）。
⇒ **一切枚举/正则结论都要自带"枚举数"**（本工装的 `scannedEvidenceFiles=` / `csFiles=` 就是为此）。

### 11.8 门禁 H6：**不可逆动作 —— 按进程名批量结束实例**（captain 事实 221 授权；2026-09-12 落地）
**为什么有这条**：`shell/App/tools/t26-evidence.ps1:60-66` 默认路径下 `Get-Process -Name 'AIPlayer.Shell' | … Stop-Process -Force` = **按进程名通杀本机所有实例**（实证 `ui-t26-attempt3-evidence.txt:3 KILL-STALE pid=15484`）；而 `§12.5.1` 自己写的口径是"只结束自己起的 pid"。全仓 11 处命中里**仅此一处违反**。captain 的定性：**门禁该管的不是风格，是"不可逆动作"**（杀进程 / 删文件 / 写凭据）—— 这类面必须有一条机械可扫的规则。
> **形状 ≠ 危害** 的反面用法：这一条**不是**从"形状"推出来的 —— 它有一条**真的会杀掉用户正在测的窗口**的实证。

**判据**：`-ScanRoot`/`-ExtraScanPath` 下所有 `.ps1`/`.cs`（非注释行、去掉引号字面量后）**不得结束不是自己起的进程**。四类形状：
| 形状 | 正则（掩掉引号字面量后匹配） | 说明 |
|---|---|---|
| 直接按名杀 | `Stop-Process[^\r\n]*-Name\b` | PowerShell 一行内直接点名 |
| `taskkill` | `taskkill[^\r\n]*/IM\b` | 按镜像名杀 |
| 按名取集合→再终止 | `Get-Process[^\r\n]*-Name\b` **且** 同语句起 **5 行内**出现 `Stop-Process`/`.Kill(`/`taskkill` | **这是本案的真实形状**（`-Name` 在第一行，`Stop-Process` 在块内） |
| .NET 按名取集合→再终止 | `GetProcess(es)?ByName\b` **且** 5 行内有终止 | C#/PS 通用 |
**合规形状**（控制 D 实测）：**只报数、不杀** —— `Get-Process -Name … | Measure` 数出来，打印 `STALE-PRESENT` 行（`path=`/`pid=`/`start=`）后 `exit 5` 拒绝启动；要清陈旧实例时由 owner 自己动手。**只结束自己 `Start-Process`/`Process.Start` 返回的那个 pid** 同样合规（控制 C）。
**SEARCH-ONLY 豁免**：token 落在**引号字面量**里的行是"搜索/打印"，记 `NOTE`（本工具自己就要搜这些串；实测真仓 `searchOnly=14`）。

**四条反控（真跑，夹具在 `%TEMP%`，扫描根 = 夹具仓）**
```
A 原案形状（两行：-Name 在上、块内 Stop-Process）  → FINDING|H6|GET-PROCESS-BY-NAME-THEN-STOP|shell/tools/probe.ps1:2 → verdict=FAIL new=1  EXIT=1
B 只在引号里出现该串                              → NOTE|H6|search-only-literal|count=1 → PASS hits=0
C 只杀自己起的 pid（Stop-Process -Id $p.Id）       → PASS hits=0
D 按名数出来 + 打印 STALE-PRESENT + exit 5（不杀）  → PASS hits=0
```
> ⚠️ **第一版反控 A 没响**（02:54:40）：旧规则要求两个 token 落在**同一行** ⇒ **恰好看不见本案那种跨行形状**。判据因此改成"**按名取得的进程集合** + **同一语句窗内出现终止**"，而不是"`-Name` 旁边有 `Stop-Process`"。**教训同族：控制项必须用"真实形状"，不能用"我以为的形状"** —— 否则门禁会以"全绿"的姿势漏掉它要管的那件事。

**存量与棘轮**：引入时**实测**存量 = **1**（`shell/App/KernelHost/KernelLogMasker.cs:181`，`owner=ui2`，位于 `SHELL_SELFTEST_MASK` 自检分支：`GetProcessesByName(KernelProcessName)` 后 `Kill(entireProcessTree: true)` ⇒ **会杀掉别的同事正在跑的内核**）。captain 裁决写的是 `baseline=1`（当时已知存量是 t26 那处，且已被 owner 修掉）；按"**先量后治 + 基线只降不升**"，这里取**实测存量 1** 作基线并打印 `baselineAt=ad40a00`，**不猜**。⇒ 该处一旦被修，**基线必须跟着降到 0**（`§11.5-5` 的教训："存量到零后基线必须跟着走"）。
> 参数：`-H6BatchKillBaseline` / `-H6BaselineAt`。读数行带 `scannedAt` + `head`（对方要求的口径）。

### 11.6 提交纪律（全队强制，被本次两次失真直接催生）
> **`git commit` 会提交"索引里所有人的暂存项"**（本次已两次把别人的文件卷进我的提交）⇒ **成员一律用显式路径**：`git commit -- <自己的路径…>`；**禁止裸 `git commit` / `git commit -a`**。本文件的作者从 2026-09-12 起照此执行。

⚠️ **补充（captain 事实 192，2026-09-12，我踩过）**：`git commit -- <路径>` **不是"只提交索引"** —— 它是**按工作区做局部提交**（等价于受限的 `git commit -a`）。所以：
- **`--stat` 只能证明"文件级"**：别人在**同一个文件**里留下的未提交改动**会随你的提交一起入库**（实例：captain 改的 `CODE_STANDARD.md:105` 随我的 `80bf30e` 入库）。
- ⇒ 提交前**看 hunk 不看文件名**：`git diff -- <path>` 逐块确认是我写的；新增文件必须先 `git add`（`git commit -- <未跟踪路径>` 会报 *pathspec did not match*）。
- ⇒ 提交后核对**文件数 + 行数**是否等于预期（别只核对文件名）。
- ⇒ pathspec **只用确切文件名清单，永不用目录**（`-- shell/tools/` 会把队友正在写的东西一起卷走）。

⚠️ **共享文件的第二条纪律（captain 事实 201，2026-09-12，我又踩了一次）**：
1. **每个 owner 只写自己那一行**；**表头/结构由 verifier 改**；**任何人改完必须重读全文并报"当刻条目数 + 行数"**（不许只报"我那一行生效了"）。
2. **任何"仓库/文件当刻状态"的陈述（"0 条目"/"已入库"/"不存在"）必须附一条可复算读数**（命令 + 行数/哈希）；**"我刚改完所以现在是 X"不算读数**。⇒ 这是"读数必须带时刻"的**空间版**：**时刻管"什么时候"，行数/哈希管"哪一份"**。
3. 🔴 **对共享文件用 `edit`（定点改），不要用整文件 `write`（整篇替换）**：`write` 会**静默删掉别人在你读取之后写入的内容**。本次实例：我 `write` 了 `privacy-allowlist.txt` 的新表头（我写的那一刻文件里确实 0 条目），随后 `ui` 把自己的 2 条条目加在文件末尾；**我的 `git commit -- <path>` 把这两条一起带走了**（`git show 2bc764d` 里它们是 `+` 行），而我当时报的却是"现在 0 条目"。
   ```
   当刻可复算读数（全文，不是我自己那一行）:
   $l=[IO.File]::ReadAllLines('shell/tools/privacy-allowlist.txt')
   lines=60  bytes=4868  sha12=472FF240C4FA  mtime=2026-09-12 01:14:02.649
   条目（非空、非 # 开头）: :58  ^shell/App/tools/t26-evidence\.ps1$ # rect-source=foreground-window owner=ui
                            :60  ^shell/App/tools/win-capture-pid\.ps1$ # rect-source=foreground-window owner=ui
   ⇒ 正确读数 = **2 条目**（`ui` 的两条）；我说的"0 条目"只描述了我砍掉的**自我豁免**那一半。
   ```
   ⇒ **后果是实打实的**：`ui` 差点据此**重新登记**（会产生重复条目）。**教训**：`write` 的语义是"整篇替换"，它在共享文件上等价于"我认定我知道全文"；**而我只知道自己写的那一段**。

#### 11.6.1 🔴 第三次同类（verifier 本人，2026-09-12 04:06）——**修一个坑会踩进另一个坑**
```
背景: 我用 -m 传提交消息时，消息里的**内层 " **被 PowerShell→原生命令重新解析，
      整条消息被拆成 pathspec ⇒ `error: pathspec 'output' did not match any file(s) known to git`
      ⇒ **COMMIT-EXIT=1，什么都没提交**（HEAD 仍是别人的），而我的文件**仍留在暂存区**。
修法: 消息写进文件 + `git commit -F <file>`（这条本身是对的）——
      **但我用了裸 `commit -F`（无 pathspec）** ⇒ 3 分钟后踩中本节的第 1 条铁律：
      `git commit` 提交的是**索引里所有人的暂存项**，于是把 `ui3` 已暂存的 3 个文件卷进我的提交。
实测（可复算）:
  git show --stat 88c708b
    shell/App/Features/Search/evidence/t28-img-byte-channel.txt |  88 +++
    shell/App/Features/Search/evidence/t28-img-byte-window.png  | Bin 0 -> 1171464 bytes
    shell/App/Features/Search/evidence/t28-img-byte-window.txt  |  25 +++
    shell/docs/VERIFY_S1.md                                     |  58 +++-      ← 只有这一个是我的
  内容无损（三者 sha12 = FB7CFAE64281 / 59385B52E197 / CCB9DEAE9812，与工作区逐字节一致，现已 clean），
  **代价是信号错乱**：ui3 之后为这 3 个文件提交会得到"nothing to commit"，看起来像自己的交付物丢了。
结论（机械修法，不靠记性）:
  1) **消息文件机制必须与显式 pathspec 配对**：`git commit -F <msgfile> -- <确切路径清单>`
     （只用确切文件名，永不用目录 —— 见上条）
  2) 提交**前**打印 `git diff --cached --name-only`：**"我要提交的文件"必须等于"索引里全部文件"**，
     不等就说明有别人的暂存项，必须改用路径清单提交（或先问 owner）
  3) 提交**后**核对 `git show --stat HEAD` 的**文件清单**：本次正是这一步发现的
  ⇒ 一句话: **`-F` 解决引号坑，`-- <路径>` 解决连带坑，两者缺一都会翻车。**
```
#### 11.6.2 🔴 用**标题当锚**的 `edit` 必须把标题原样写回去（2026-09-12，我两次踩同一个坑）
```
现象（都是我自己）：把某小节标题当 `old_string`、新内容里**忘了再写回那个标题** ⇒ 标题消失、正文变成**无标题块**，
  在标题序列里表现为"跳号"（读者会以为整节被删了）。
  第一次: §12.5.2「先抓」的标题被 §12.5.1.3 那次编辑吃掉（正文无标题地挂在 §12.5.3 前）
  第二次: §11.3「本次清理（t48）」的标题被 §11.2.1 那次编辑吃掉（正文无标题地挂在 §11.5 前）
⇒ **机械自检（改完必做，不靠记性）**：`$l=[IO.File]::ReadAllLines(<file>); $l | Where-Object { $_ -match '^#{3,4} ' }`
   —— **改前先存一份标题清单，改后逐行比对**；只要出现"跳号"或数量变化，就是吃掉了标题。
⇒ 更稳的写法：**锚在标题的下一行、或锚在标题前的空行**，把标题本身**排除在 `old_string` 之外**（那样就不可能吃掉它）。
⇒ **第三条同类（同一天、同一个我）**：写 §11.2.1 时我把门禁的 `FINDING` 行**原样抄进文档** ⇒ 那个证据名**立刻变成本文件的一条"引用"**
   （门禁纯文本、分不清"引用"与"转述"）⇒ 只要它未入库/不存在，**我这条诊断记录就把自己变成 citer**、把一次真实红点续命。
⇒ **第二条机械自检（改完必做）**：**改任何 `.md` 之后立刻跑一次门禁，检查 `FINDING|H2*` 的 `citedBy=` 里有没有本文件**；
   若有且不是我有意承诺的东西 ⇒ 按 §12.5.2 的约定**打断相邻关系或改占位符**（本次已改成 `…/evidence/<t72 清单证据>`）。
```
### 11.4 G5 修正后的两条反控（t47 遗留项，t48 一并复述）
见 §10.7：**独立 harness 抢前台 ⇒ 必须被拒**（`EXIT=1 newFiles=0`）、**无关窗口获得焦点 ⇒ 必须成功**（`EXIT=0` 出图）；`SUMMARY|caseA=PASS|caseB=PASS`、`HARNESS-EXIT=0`。

---

### 11.9 🔴 **假绿陷阱：退出码不是判据，锚行才是**（captain 2026-09-12 采纳 `services` 的实测）
**现象**：本机**没有 `pwsh`**（`Get-Command pwsh → NOT-FOUND`；宿主是 Windows PowerShell **5.1 Desktop**），而 `shell/tools/*.ps1` 在直接 `&` 调用时被 **ExecutionPolicy** 拒绝 ⇒ **脚本根本没跑**，调用方看到的却是"没有输出（或只有一行错误）+ 一个**不是裁决**的退出码"。

**✅ 唯一正确调用形式**（本仓所有 `.ps1` 工具，含门禁与抓图编排）
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\shell\tools\evidence-hygiene-check.ps1
```

**❌ 三种错误形式与实测反例**（2026-09-12 03:56–03:57 本机实测，非推测）
| 形式 | 实测 | 有 `GATE-START`? |
|---|---|---|
| `& .\shell\tools\evidence-hygiene-check.ps1`（在本 harness 的 pwsh 里直接跑） | 输出 1 行、`$LASTEXITCODE` **空**（PSSecurityException 走 stderr） | **0** |
| `powershell.exe -NoProfile -Command "& …\evidence-hygiene-check.ps1"` | **EXIT=1**、7 行错误 | **0** |
| `& .\shell\tools\capture-shell-flyout.ps1 -Preflight …`（抓图工具，同一错误形式） | **EXIT=0**、1 行输出、**无 `TOOL-START`** ⇒ **真正的"假绿"** | **0** |

> ⚠️ 退出码是**调用方的**，不是工具的裁决：一旦被接进管道（`… \| Select-String …`），看到的就是**最后一条命令**的状态 ⇒ 被拒这件事**完全消失**。`services` 说的"EXIT=0 + 0 行输出"就是这个机制，我在抓图工具上**原样复现**（`WRONG-EXIT\|0`）。

**🔴 判据（写死，两条）**
```
出现 GATE-START|script=|ps=|time=|head=|cwd=  才说明门禁开始了
出现 SUMMARY|verdict=…|checks=…               才说明门禁跑完了（checks 与三项之和还要自检，见 §11.8 记账面自检）
没有 GATE-START 或 没有 SUMMARY  ⇒  【没跑成】⇒ 不得读退出码，也不得写"全绿"
抓图工具的对应锚行是 TOOL-START|script=|ps=|time=|exe=|mode= ；没有它 ⇒ 同样判"没跑成"
```

**为什么单独立一条**：这与"**控制项自己也会撒谎**"（`-Mode none` 在 app 已死时给假 PASS、`H5` 的 FAIL 不计入 `SUMMARY`）是**同一族的第三次实例** —— **凡是"没跑成"与"跑成了且没问题"在同一个信号上不可区分，就必须再加一条锚行**。区别只在于：前两次是**判据内部**撒谎，这一次是**调用层**撒谎。

## 12. 取证前置检查：**先声明状态，再取证**（不是新门禁，是每条读数前的固定动作）

> 三条同源：`kernel2` 抓到的**缓存换面**、`H5` 的**哈希 ≠ 现行**、`H2a` 的**忽略规则身份**。共同的危害是同一个：**你以为在测 A 面，实际测的是 B 面。**

### 12.1 🔴 缓存把"网络面"的证据变成"磁盘面"的证据（`kernel2` 实测抓出，很贵）
```
路径: %LOCALAPPDATA%\AIPlayer\danmaku-cache\{matches,comments}
症状: K7 的 A3 第一次跑出「假阴性」—— 日志 FetchInternalAsync … source=disk、MatchAsync 连网络请求都不发
      ⇒ 看起来"匹配失败"，实际是**缓存命中**
处置: 取证前清该缓存，并在报告里**声明缓存状态**（已清 / 命中）；不清就必须写"本次是磁盘面读数"
```
**推广纪律（凡涉网络面的证据都必须做）**：先列出**该路径会读的所有缓存面**（HTTP/SWR 快照、弹幕 matches/comments、图片/字幕磁盘缓存…），
逐个给出 **路径 + 当刻条目数 + mtime**，再开始取证；报告里写一句：
> 「缓存状态：`<面>` = 已清 / 命中 N 条（mtime …），故本次读数是**网络面** / **磁盘面**。」

**当刻采样（我 01:31:41 实测，只读）**：`danmaku-cache` **存在且已填充** —— `comments` 1 条、`matches` 1 条，mtime `2026-09-12 01:16:17` ⇒ **此刻做弹幕面取证必然是"磁盘面"读数**（除非先清）。
**为什么立这条**：缓存优先（SWR）的设计让"重复取数"变快，但也**静默把证据换了面** —— 与 `H5 PASS ≠ 图是当前渲染`、`H2a 读数必须带忽略规则身份` 是同一族。

**services 加的一条互校装置（t68，2026-09-12）**：缓存清理现在**必打** `CACHE-CLEAR items=N bytes=M dir=…`（**含 N=0**）⇒ **盘上条数/字节数与日志里的 N/M 可以互相校对**，不再是"点了没反应"。这正是 `§12.1` 要的形态：**"已清"必须是一个可复算的读数，而不是一个动作**。

### 12.2 产物身份：**判据 = sha12 + 采样时刻；字节数可打印、可交叉核对，但不得当身份**
```
kernel2 实测: 同一配置改动后 DLL 哈希 118905535E72 → 8A611F22CFFB，而**字节数 1,008,640 一字不变**
⇒ 同一大小下已存在三个不同 sha12（6E694142C55F / 118905535E72 / 8A611F22CFFB）
⇒ "按大小认身份"作废；内核产物身份 = sha12（+ `KERNEL-FORK rev=N` 日志行）+ **采样时刻**
```
🔴 **措辞精确化（2026-09-12：我自己原文"永远不用字节数"过宽，已改）**
- **身份判定**（"这是哪一个产物"）= **sha12（+ `KERNEL-FORK rev=N`）+ 采样时刻**；**不以字节数为判据**；
- **字节数仍然该打印**：它是廉价、可复算的**交叉核对**量 —— `H5` 判据本身就是 **bytes + sha12 一起比**（两个数都对，身份才成立）；
- 一句话：**字节数是描述与交叉核对，不是身份。** 别删掉它，也别拿它当"就是那个产物"的证明。

#### 12.2.1 🆕 **`提交时刻` 不能替代 `产物身份`**（captain 2026-09-12 立纪；判"某缺陷在某代际是否复现"的唯一有效锚 = 被跑那个二进制的 sha12）
```
活例（我自己 §84 实测）:
  展示缺陷的产物 = 主线 bin A7AFF8579562 @03:24:27.535  ← **比修复提交 836909c（03:25:25）早 58 s**
  修复版产物     = cap-bin7  E4A2A430A95B @03:24:50.187  ← **比它自己的提交早 35 s**
⇒ "修复已提交"**不回溯改变已构建产物**：源码里有了 ≠ 盘上那个产物里有。
⇒ 用**提交时刻**当锚会得出"修复后仍复现"的**错结论**；锚只能用 **产物 sha12**（+ 构建目录 + mtime + 起窗时刻，见 §12.5.1.3 ④）。
⇒ 同族第三证："字节数不是身份"（两个 **1,608,704 B** 的 dll 是不同代际：A7AFF8579562 / E4A2A430A95B）。
⇒ 🆕 **同族第四证（2026-09-12，captain 自纠）**：把**过期读数**当"当刻"引用 ——
  他引今天最早那次门禁跑（约 05:33）的 `FINDING|H3b|BARE|shell/Tests/t86-t80verify-probe/Program.cs:38`
  与账本 `WORKSPACE.md §95` 那句历史记录，对我说"门禁当刻唯一红点是 `t86` 探针"；
  而那条在 **05:30 就被 `services` 修掉**（同一提交还改了这个 `Program.cs` 的 sha12），我 **06:35:43** 实测：唯一 FAIL = H7、门禁输出里 `t86` **0 命中**。
  ⇒ **过期快照不分作者**：captain、reviewer、我、`services` 都各中过一次；**唯一的解法是"引用一律带时刻 + 现场重跑"**（§12.5.1.5 ⑥ 的日志面同族）。
  ⇒ 附带价值：这条复核**避免了一轮白工**（captain 已撤回给 `services` 的"去修那个裸 catch"）。
```

#### 12.2.2 🔴 `.gitattributes` 判了 `text eol=crlf` 的文件：**声明身份必须说清"哪一种形态"**（reviewer 报，我实测复核）
```
现象（实测，2026-09-12 06:07）: shell/Tests/t84-migration-deviceid.ps1（`*.ps1 text eol=crlf` 命中）
  工作区 + index blob 形态 = **17,399 B / sha256_12 92CF11CBFBAA / 裸 LF 行 234 / CRLF 行 0**
  新检出（模拟 LF→CRLF）= **17,633 B / sha256_12 F6472E44D461**   ← 两个都是"真的"，但**是不同的对象**
⇒ 机制: `text` 属性让**入库 blob 归一化为 LF**，而**检出时按 `eol=crlf` 还原成 CRLF** ⇒
  "工作区字节 / index blob / 新检出字节"三者可以两两不同；**只写 sha12 不说形态 ⇒ 引用方按另一种形态算就永远对不上**。
⇒ **纪律**：对这类文件声明身份时写清形态，例如「`…ps1`（index blob 形态）LF 17,399 B / `92CF11CBFBAA`」或
  「（新检出形态）CRLF 17,633 B / `F6472E44D461`」。
⇒ 本门禁 `H5` 比的是**工作区形态** ⇒ 引用方给 index/检出形态时会判 mismatch —— **那不是"文件变了"，是形态不同**（先看属性再判账）。
⇒ 反例（同一份文件里已裁过）：`**/evidence/** -text` ⇒ 证据目录里**不做行尾归一**、字节精确 ⇒ 这也是证据身份能按 sha256 认的前提。
   （所以证据文件的 `index blob bytes == 工作区 bytes`；`.txt` 实测 `text: unset`。）
⇒ 🆕 **反向陷阱（`reviewer` 2026-09-12 报，我复核）**：**属性声明的形态 ≠ 盘上的形态，两个方向都会反过来**
```
① `.ps1 text eol=crlf`（属性说 CRLF）⇒ `shell/Tests/t84-migration-deviceid.ps1` **盘上却是 LF**（17,399 B / CR=0 / 92CF11CBFBAA，我用 `Split("`r")` 数过）
② `.md text eol=lf`（属性说 LF）⇒ `ui3` 的 `EVIDENCE_T30.md` **盘上却是 CRLF**（worktree 75,259 B vs index blob 74,779 B，**差 480 = 480 个 CR**）
⇒ **判定形态只能读字节（数 CR），不能从 `.gitattributes` 推**；报身份时把形态写成"worktree 的 CR 数 / index blob 的 CR 数"两个数。
⇒ 这与 §12.2.5（落盘方式即身份）是同一条在**行尾面**的形态：**任何"形态"都必须实测，属性只是规则、不是事实。**
```
⇒ **本条不改门禁**（未新增规则）；若将来要加，建议只加 **NOTE（advisory，never FAIL）**：对被 `text/eol` 命中的文件在 H5 旁打一行形态提示 —— 要不要加由 captain 定。
#### 12.2.3 🔴 **pin 清单本身的完备性**：必须包含「**被加载的产品程序集**」（captain 2026-09-12 立纪；依据 = t67 夹具那次）
```
现象（实测，2026-09-12）：t67 冻结基线的**四件**（Program.cs 33,908 B / DC1D1AF14B64、probe dll 49,152 B / E8A7CD0C1C61、
  exe 8F55CD214793、classify 12 行 / EXIT=0）**逐项没变**，读数也"12 行不变"——**看起来无懈可击**；
  但该 bin 里**被加载的 `AIPlayer.Shell.Services.dll` 从 `34A9D2CCA97D` 变成了 `1479FCA3BAA0`**
  （04:20:58 重建夹具时把更新的 Services 复制进了 bin）⇒ 行为对象已经换了，那条"12 行不变"只能绑 `1479FCA3BAA0`。
⇒ 纪律：pin/冻结一份基线时，清单里**必须有**「这次真正被加载的产品程序集」这一项（外加 §12.2.1 的 sha12 + 构建目录 + mtime + 起窗时刻）。
  否则会出现本族最坏形态：**pin 的四件全没变、我却报了一个错结论，而且看不出错在哪**。
⇒ 自查问句（写进每张卡的采样段）：**"我这次到底跑了哪几个程序集？其中一个换了，我现在的读数还成立吗？"**
⇒ 与 §12.2.1 同源但不同面：§12.2.1 管"锚只能用产物 sha12"，本条管"**清单不许缺项**"。
⇒ 补充（我 06:47 实测，与 captain 在同日 `FIXTURE_INDEX.md:7` 的裁定并列而不冲突 —— 两者说的是**不同对象**）：
   · **工装身份** = `Program.cs` 哈希 + 构建时刻（captain 裁；dll 只作旁证）；
   · **被加载的产品程序集**（夹具 bin 里的 `AIPlayer.Shell.Services.dll`）是**另一个对象**，必须**单独列** —— 它换掉时工装源码可以一字未改。
   · 实证（我当刻两处实测，同一份 `Program.cs` `B087FD5FB7FC` 在**两个位置**构建）：
       仓库副本 `shell/Tests/t67-hls-probe/bin/…` ⇒ probe dll **`8BBDE19E7825`** / 49,152 B @04:43:28.753，被加载 Services **`1D90C5A5BAD8`** @04:43:28.376
       `%TEMP%` 副本 ⇒ probe dll **`5999C224273E`** / 52,224 B @05:39:07.138，被加载 Services **`DA821BE0FF4E`** @05:38:04.286
     ⇒ 同一源码、同一机器、**两个不同的产物**，连被加载的 Services 也不同 ⇒ 「写清哪一份副本 + 构建时刻」是硬要求。
```

#### 12.2.4 🔴 **换代必须同步更新"所有"现行指针**（2026-09-12 实测：同一夹具在两个 tracked 文件里给出互相矛盾的"现行"）
```
现场（我 06:39 实测）：
  `shell/Tests/evidence/t85-fixture-baseline.txt`（8,991 B / `2A345D0F08E3` / 提交 `4efd208` @04:22:56）
    :2  "t85 … **最终基线，冻结**" ｜ :14 `Program.cs | 33,908 | DC1D1AF14B64` ｜ :26 "…← **现行 / 冻结**"
  但同一夹具在 **05:37:40.380** 已换代（39,481 B / `B087FD5FB7FC` / 683 行，`d4ab097` @05:40:27 入库），
  且新代际**已被另外两处正记录**：`shell/Tests/FIXTURE_INDEX.md`（`:22` 39,481 B / `B087FD5FB7FC`）与
  `shell/Tests/evidence/t91-t67-fixture-mask-and-guard.txt`（`:25` 源码身份、`:34` **冻结声明**、`:115` 新代际）。
⇒ 于是**两个已入库文件对同一夹具声明了两个不同的"现行"**。按"不重写历史"，`t85` 那份的历史读数不动，
  但它的 **"现行/冻结"指针必须被一个后继指针取代**（append 一句"本行所指代际已被 d4ab097 取代，现行见 …"）。
⇒ 纪律：**换代提交 = 一次"指针更新"提交**，清单 = 该产物的所有 tracked 声明（索引 + 各基线/冻结文件 + 台账），
  漏一个就会留下一个"看起来权威的过期现行"。与 §12.2.3（pin 清单不许缺项）互为表里：那条管**钉**，这条管**换**。
  实证补充（`services` 同日报、我复算）：`FIXTURE_INDEX.md` 自己也换代了 —— 他报 `6,853 B / 40 行 / DCC1133923B6`（提交 `14135f6`），
  但该文件在 `02b89d8`（06:34:05）又被改过 ⇒ 当刻 = **7,573 B / `A7192DBE93F4` / 44 行**。**索引本身也是会被后写覆盖的"现行指针"。**
```

#### 12.2.5 🔴 比对哈希时的**落盘方式**本身就是身份变量（`services` 报、我 06:39 复现并定量确认）
```
同一条 blob（`HEAD:shell/Tests/t67-hls-probe/Program.cs`）两种落盘方式：
  `cmd /c "git cat-file -p … > file"`  ⇒ **39,481 B / `B087FD5FB7FC`**（首三字节 47,47,32 = "/", "/", " "）
  PowerShell `Set-Content -Encoding UTF8` ⇒ **40,167 B / `A9281B16F28B`**（首三字节 239,187,191 = **EF BB BF BOM**）
  ⇒ 差 **+686 B = 3（BOM）+ 683（LF→CRLF，每行一个 CR）** —— 与行数 683 完全相同 ⇒ 差异**全部**来自 BOM 与行尾。
⇒ 纪律：**凡比对哈希，落盘方式必须与原始字节一致**（git 内容一律走 `cmd` 重定向或 `git cat-file` 直读；
  PowerShell 文本 cmdlet 会加 BOM/改行尾 ⇒ 造出"假身份"，足以让人报出"仓库那份与工作区不同代"这种错结论）。
  （同族：§12.2.2 的"说清哪一种形态"。）
```

### 12.3 两套账不许合并（`kernel` vs `shell/App` 的警告面）
```
kernel（开了 EnforceCodeStyleInBuild=true，未开 TreatWarningsAsErrors）: bash `dotnet build -c Release` = **0 错误 + ≤101 基线警告**（100 唯一）
  分布 IDE0004×38 / IDE0041×37 / IDE0051×22 / IDE0052×2 / IDE0161×1 ⇒ 口径写成"0 错误 + ≤101 基线警告（棘轮只降不升）"
shell/App: 1172 条，**全部来自 reversed/**（第三方反编译源码）⇒ 与 kernel 那 101 条是**两套账**，不得相加或互相解释
```

### 12.4 `t59`（转码/HLS 补证）的取证口径建议（我给 `services` 的同一份）
```
① 缓存面声明：先列 HTTP/SWR/弹幕 各缓存面的 路径+条目数+mtime，声明本次是"网络面"读数
② 产物身份：内核 DLL 的 sha12 + 采样时刻 + `KERNEL-FORK rev=N` 日志行（禁用字节数）
③ HLS 证据必须落到"三层都取到"：master.m3u8（含 variant 列表）→ 选中 variant 的 media playlist → **至少一个分片**的字节数
   仅"master 返回 200"不算跑过该路径（这正是"至今一次没跑过"的形态）
④ 负反控：让**源站不可达**（或换一个已知 404 的 variant）⇒ 必须出现**可区分**的失败（错误码/日志行），
   且**不能**表现为"缓存命中成功" ⇒ 这才能证明 ③ 的成功来自网络而非磁盘
⑤ 画面声明：本卡**不做像素核对** ⇒ 报告必须显式写"画面未做像素核对"，不得让读者以为看过画面
```

### 12.5 仓内探针夹具：**已裁，本项不再是"未决"**（captain 2026-09-12，第三次重申为准）
```
裁定（照此为准，别再标"未决"）:
  · **不改门禁、不加排除列表、不动基线**
  · **由 services 把那 2 处改成"点名窄类型 + 理由"**（`t59-probe/Program.cs:751 catch { return; }` / `:795 catch { /* … */ }`，机械可改）
  · **夹具可以留在仓内**（`t41-probe` 有先例）· **未来一次性夹具优先放 %TEMP%**
理由一句话: **给门禁开口子会让"夹具里的坏形态"合法化**，而"改两行夹具"零政策成本。
```
已按裁定做的**可读性**改动（**不是新规则**）：
```
CHECK 行新增 productionBare= / unmappedBare= / productionRib= / unmappedRib=
每条 FINDING 行尾新增 scope=production|unmapped
有 UNMAPPED 命中时另发 NOTE|H3b|unmapped-hits-not-in-the-production-stock|<paths>（说明它们仍计入棘轮）
反控 A（默认归属表）: 探针 2 条 ⇒ scope=unmapped、productionBare=0 unmappedBare=1 productionRib=0 unmappedRib=1、NOTE 出现
反控 B（把探针路径映射给 ui2）: 同 2 条 ⇒ scope=production、productionBare=1 unmappedBare=0、byOwner=ui2=2
  ⇒ 分组**承重**（会随归属表变化，不是写死的标签）
当刻: H3b PASS / bareCatch=1 rib=1，**productionBare=0 productionRib=0** ⇒ 生产树无类型 catch 已归零；
      计数里那 1+1 全部来自仓内探针夹具（`services` 改完即各自归零）
```

### 12.5.1 实例归属铁律（2026-09-12，来自一次差点发生的误杀）
```
· `AIPlayer.Shell` 的实例**可能是别人的、也可能是 captain 为用户起的**（例：pid 19704 = 用户正在测的那个，起始页 library）
· ⇒ **任何人不得结束别人的实例**；取证脚本**只 Stop-Process 自己 Start-Process 起的那个 pid**
· ⇒ 起窗口前先看"实例数 + 标题"：**实例数 ≥ 1 就 REFUSED**（不以 pid 为准 —— pid 会变，实例数不会骗人）
· ⇒ 跑完必须自证 **POST-STOP|instances=0**（shell/tools/capture-shell-flyout.ps1 已实现）
· ⇒ 报告里**不许**写"当刻有人（某某）在跑"这类**推断归属**的话；只写"实例数 + pid + 标题 + 采样时刻"
```
**实例归属映射（captain 2026-09-12 给出，用于避免误杀；只写事实，不推断）**
```
E:\ui3-verify-bin\AIPlayer.Shell.exe                                   = ui3 的隔离输出
E:\cap-bin\AIPlayer.Shell.exe                                          = **captain 的**隔离输出（他给用户起的那个也在这一支）
shell\App\bin\Debug\net9.0-windows10.0.22621.0\win-x64\AIPlayer.Shell.exe = 主线构建输出（谁跑谁有；通常是成员构建/用户试用）
⇒ 清理时**只清自己那一支的 Path**；别人的实例一律不动（即使它看起来"没人用了"）
⇒ 该映射随环境变化，**以当刻采样为准**（本条是"避免误杀"的辅助，不是判据；判据仍是"实例数 ≥ 1 就 REFUSED"）
```

### 12.5.1.1 唯一口径命令：`list-instances.ps1`（2026-09-12 新增，因为归属报告"每次口径都不同"）
```
问题: 一晚之间"现在有几个实例/是谁的"被问了很多次，每次都用不同的临时命令行回答 ⇒ 三处相互矛盾的读数，
      两次差点误判（"是不是用户的窗口？"）。captain 立规：起窗口必须用隔离输出目录，且 owner 要能报 pid+path+起止。
命令（宿主无 pwsh，必须走 powershell.exe）:
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\shell\tools\list-instances.ps1
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\shell\tools\list-instances.ps1 -Samples 10 -IntervalMs 3000
  ... -Json $env:TEMP\li.json          # BOM-less UTF-8，可被严格 JSON 读取器直接读
锚行（退出码恒 0，**不是判据** —— 见 §11.9）:
  TOOL-START|...|samples=|intervalMs=|head=|cwd=
  SAMPLE|i/N|time=|shells=|kernels=|free=|shellPids=
  INSTANCE|sample=i/N|pid=|path=|start=|ownerDir=|title=|note=ownerDir 只是线索不是判据
  KERNEL|...            （内核实例单列，它会占窗口但不是 shell）
  SEEN|pid=|path=|exeStart=|firstSeen=|lastSeen=|samplesSeen=i/N   ← 窗口内**起完就死**的实例也留下记录
  BUILDERS|dotnet=|msbuild=|xamlcompiler=|note=构建**不**阻断抓取（软警告，只报不拒）
  FREE-FIELD|verdict=FREE-OVER-WINDOW|OCCUPIED-AT-SOME-POINT|SNAPSHOT-OCCUPIED|samples=|freeSamples=|allFree=|maxShells=|maxKernels=|distinctPids=|spanSec=|note=
```
**为什么必须带 `-Samples`（实测，同一晚）**
```
04:02:33  裸跑 ⇒ shells=0                       （看起来"场地是空的"）
04:03:01  裸跑 ⇒ shells=1  pid=12808  E:\ui3-search-bin\AIPlayer.Shell.exe  start=04:02:36.470
04:03:04  裸跑 ⇒ shells=0                       （同一次窗口内起的，3 秒后已退出）
⇒ 该实例存活约 25~28 秒。**单次读数不是"场地空"的证据**，它只是对"几十秒级抖动"的一次取样。
⇒ 判据: `-Samples N` 的 `allFree=true` 才是一条可用读数；且只覆盖窗口内，不覆盖窗口外任何瞬间。
⇒ 反控已跑: 用 %TEMP% 夹具（把进程名换成常驻进程）验证过占用路径 —— INSTANCE/KERNEL/SEEN/FREE-FIELD=OCCUPIED-AT-SOME-POINT 全触发，
   `distinctPids=3`，JSON 无 BOM 且 `seen[].Hits` 正确；空场地那次 `freeSamples=5/5 spanSec=6.25 allFree=True`。
```

#### 12.5.1.2 🔴 唯一合法的"拿到 `instances=0`"方式：**显式全队冻结窗**（captain 2026-09-12 立法；机会式重试已废）
```
现场噪声（captain 记的 10 次采样，03:39:30 → 03:42:31，我那条机会式重试的窗口）:
  · `instances=0` **一次都没出现**
  · 持场者轮换: `cap-bin7`(pid 8048) → `ui2-bin`(pid 12180/6932)
  · 更早 20 分钟内他数到 **≥8 支实例、来自 5 处以上路径**（主线 bin、cap-bin3/5/6/7、ui2-bin）
⇒ 结论: **单人让窗的存活期 < 20 s ⇒ "机会式重试"在这条现场不可行**（我 03:37-03:39 的三条读数 1/2/2 与本次 10 次采样同向）。
⇒ 🆕 **规则（所有需要 `instances=0` 的取证一律照此，含将来的 `t34` 视觉收口）**:
   1. captain **广播**冻结 →  2. **逐支确认已停** →  3. captain 喊 `instances=0 + 时刻 + head`
   →  4. 执行方在 **60 s 内**跑完 →  5. captain **解冻**
⇒ **不要再发明机会式重试**（本文件上方那条"20 s 采一次、≤10 次"的做法已作废，只保留为"噪声量化"的一次读数）。
⇒ 本工具的正确用法随之固定：**冻结窗内用 `-Samples N` 拿一条 `allFree=true` 读数**（它是"此刻空"的证据），
   冻结窗**外**的单次读数只可用于**事后归因**（谁在跑），不可用于"我现在可以起窗"。
```

### 12.5.1.3 📌 读数与引用四条纪律（captain 2026-09-12 要求"规则住在工具旁边"；这里是权威清单）
```
① 判「场地是否空」= `list-instances.ps1 -Samples N` 读 `FREE-FIELD|verdict=FREE-OVER-WINDOW|allFree=true` **并带 `spanSec`**
   （该行即"场地空"的可用读数）。**单次读数不算证据** —— 实测实例存活 **25~28 s**（04:02:33 shells=0 → 04:03:01 shells=1 → 04:03:04 shells=0）；
   `ownerDir` 只是**线索**、**不是判据**（归属由 owner 用 pid+path+起止确认）；**退出码恒 0，不算判据**（§11.9）。
② 起窗者必须：**用隔离输出目录**（`E:\<name>-bin\`）+ **用完自停** + **报 `pid + path + 起止时刻`**
   （这三件是让下一个人能复算"谁在场"的最小集合；机制与实测见 §12.5.1.1 / §12.5.1.2）。
③ 🆕 **引用别人的读数必须标 `src=引用(<谁>)`** —— 例：`src=引用(captain)`、`src=引用(services)`、`src=引用(reviewer)`
   （`reviewer` 的做法，captain 要求全员照做）。**自己的读数不标 src**；二者不得混写成"实测"。
④ 🆕 **报告必须带代际**：**构建目录 + dll `sha12` + `mtime` + 起窗时刻**（`H5`/身份类判据同理：bytes/sha12 对必须逐条对盘）。
   理由见 §12.2 / §12.11：身份 = `dll sha12 + mtime`（`exe` 只是**倾向**稳定，实测 t59 变过、t65 重建后哈希又恰好未变 ⇒ 两个方向都不能用 `exe` 判代码）。
```
> 为什么放在这里：**能力与它的判据要在一起** —— captain 台账里那份是他个人的账；别人读这份工具文档时才找得到这些规则。

#### 12.5.1.4 🔴 扫描类判据的**枚举完整性**（captain 2026-09-12 立纪；**本条的依据是我自己踩的坑**）
```
现象（我的实测反例，2026-09-12，采样 + 现场读数）:
  Get-ChildItem -Path 'shell\App\Features\Aggregate\*.cs'                        ⇒ **10** 个文件
  Get-ChildItem -Path 'shell\App\Features\Aggregate' -Filter '*.cs' -Recurse    ⇒ **11** 个文件
  被漏掉的 1 个 = shell\App\Features\Aggregate\Shared\ImageSourceLoader.cs      ← **静默少给，不报错**
同类（同一晚第二次，Select-String 形态）:
  Select-String -Path '…\Aggregate\*.cs'                     ⇒ 命中 **6** 个文件
  同一个 pattern 再加 '…\Aggregate\**\*.cs' 一起扫            ⇒ 命中 **7** 个文件（差的就是 Shared/ 那份）
  Get-ChildItem -Path '…\Aggregate' -Include '*.cs'（不带 -Recurse、路径不以 \* 结尾）⇒ **0** 个
  Get-ChildItem -Path '…\Aggregate\*' -Include '*.cs' -Recurse                      ⇒ 10 个
⇒ 根因: `-Path` 里的通配符**不递归**；`-Include` 在不与 `-Recurse`（或尾随 `\*`）同用时**不按预期工作**。两者都**不报错**。
⇒ **凡"扫全量"的判据必须三件套**：
  ① 打印**扫描到的文件数**（门禁里就是 `scannedFiles=` / `csFiles=` / `scannedRows=`）；
  ② 至少用一条**"已知应命中"的反例自证枚举完整**（如上例：先证明递归版能扫到 `Shared/ImageSourceLoader.cs`，再拿它对比非递归版）；
  ③ 引用别人的扫描读数时**必须带上那个计数** —— 没有计数的"我扫了全仓"不可复算、也不可反驳。
⇒ 为什么写在工具文档里：门禁里那三个 `scanned*` 计数是这条纪律的**执行面**，但"**为什么必须带它们**"此前只在 captain 台账里 ——
   下一个写扫描脚本的人才看得见这条。（**本条不改门禁**：门禁已达标。）
⇒ ④ **"面"必须先写死**（captain 2026-09-12 裁决，已在台账）：**面 = 仓库内所有"已入库"文件，排除 `bin/` + `obj/`** ——
   **不是**"仅证据目录"。活例：`ui3` 的两份证据住在 `shell/App/Features/Search/evidence/`（**不在** `shell/Tests/evidence/`）
   ⇒ 按"仅证据目录"取面会**正好漏掉**它们。报扫描类读数时，先写死面、再打印扫描数（本条与 ① 是同一条的两面）。
   ⚠️ 注意作用域：这条定义的是**引用/存量类扫描**的面；本条门禁 `H7` 的 `-EmojiScope` 默认 = `shell/App`（69 文件）、
      `H1/H3` 扫 `shell`（202 文件）、`H5` 扫证据行 —— 各判据的面**各自打印**，**不要**把这条裁决读成"某判据必须扩到全仓"
      （扩面属**放宽**扫描范围，须 captain 单独裁）。
```

#### 12.5.1.5 🔴 计数类读数的两个"**看起来完全正常**"的陷阱（captain 2026-09-12 立纪；两条都是实测踩出来的）
```
① **"键数/条目数"必须带"深度口径"** —— 同一个文件，两种数法能差 12
    naive 正则 `^\s{2,}"k":` 把**嵌套**键也数进去 ⇒ A = **70** / B_bak = **63**
    真值（深度感知**纯词法**扫描 与 `JavaScriptSerializer` **两法逐项一致**）⇒ A = **58** / B_bak = **44**
    ⇒ 报任何"键数/条目数/项数"都必须写清**数到第几层**；只写"我扫到 N 个"不可复算、也不可反驳。
② 🔴 **PowerShell 变量名不区分大小写** ⇒ `$p` 与 `$P` **是同一个变量**
    实测：`foreach ($p in @($fA,$fP,$fB)) { … }` 跑完之后 `$P` **已被覆盖**，随后解析把 `B_bak` 读成了 `B_now`（摘要里出现 `Bbak_keys=97`）。
    ⇒ **脚本里不得出现"仅大小写不同"的变量名**；**读数里要带自检字段**（`A_keys=` / `Bbak_keys=` / `Bnow_keys=` 这类），
      因为**撞车之后报出的数字看起来完全正常** —— 与 §79 的 `$scanNeedles`/`$ScanNeedles` 是同一条。
⇒ 自查结果（2026-09-12 05:29:13，HEAD `fc4b7c9`）：本目录四个工具**零**"仅大小写不同"的变量名 ——
  `capture-shell-flyout.ps1` 207 个变量 / `popup-capture.ps1` 93 / `evidence-hygiene-check.ps1` 230 / `list-instances.ps1` 45。
③ 🔴 **`Get-ChildItem -LiteralPath <目录> -Include *.cs -Recurse` 会静默忽略 `-Include`**（我 2026-09-12 自捉）
   实测（HEAD `9fa1708` 之后，06:37:08）：同一目录树三种写法
     `-Path 'shell' -Filter *.cs -Recurse`                    ⇒ **174** 个（正确）
     `-LiteralPath 'shell' -Include *.cs -Recurse`            ⇒ **578** 个（**全是所有类型**：.cs/.xaml/.png/.md/.ps1/.log…）
     `-Path 'shell\*' -Include *.cs -Recurse`                 ⇒ **0** 个（同样不可用）
   ⇒ `-Include` 只有在与 `-Recurse` **且路径以 `\*` 结尾**（或用 `-Filter`）同用时才生效；**其余写法它不报错、直接当没写**。
   ⇒ **我据此撤回此前报给 captain 的"`-Recurse` 扫了 **507** 个文件"**：那是本陷阱的产物（统计的是**所有类型**文件数），
     不是 .cs 数。**正确的当刻值 = `.cs` 非 `bin/obj` / 174 个（磁盘）· 161 个（已入库）· 未入库 13 个**，
     且 174 与门禁同刻自报的 `csFiles=174` **互相印证**（这是一个"我用两条独立路径数同一个面"的交叉校验样板）。
④ 🔴 **读原生进程退出码不能用 `| Select-Object -First N`**（`services` 报，我 2026-09-12 复现确认）
   实测（t67 夹具）：`& exe list | Select-Object -First 1` ⇒ `$LASTEXITCODE=` **`-1`**（假值）；`-First 3` 同样 `-1`；
   `$null = & exe 2>&1` ⇒ **2**；`& exe list *> <file>` 再从文件读 ⇒ **2**（真值）。
   ⇒ 机制：管道被提前终止时**子进程被杀**，退出码不是它自己的。**规矩**：`& exe <args> *> <file>` 落盘后再读文件 + `$LASTEXITCODE`。
⑤ 🔴 **路径分隔符与 `-like` 模式不一致 ⇒ 分桶计数整列错**（`services` 自捉，2026-09-12；我复算并补了一条自诊断判据）
   现象：one-liner 拿 `shell\App\…`（Windows 反斜杠）去 `-like 'shell/App*'`（正斜杠）比 ⇒ **命中 0**，整列印 0；
   与 `FullName` 用同一种分隔符后才有值。⇒ 按路径分桶前**先把路径规范成一种分隔符**，并**印出各桶之和**：
   **桶之和 ≠ 总数 = 这一次计数自身判错**（与 §12.5.1.4「面先写死 + 打印扫描数」同族）。
   对照实例（`.cs`，非 `bin/obj`，我 06:42:29 两种写法各数一遍）：`-Filter *.cs` 与 `-Path shell -Include *.cs -Recurse`
   **各得 175，逐字相同**；分桶 = App 53 / Services 88 / Spike 6 / Tests **27** / tools 1 ⇒ **和 = 175** ✅。
   而 `services` 当刻报的分桶 = App 52 / Services 87 / Tests 11 / docs 0 / Spike 6 ⇒ **和 = 156，与他报的总数 157 不符**
   ⇒ 他的那次计数**用他自己的分桶就能判错**（他没被我的话误导过，是他自己的 one-liner 问题）。
⑥ 🔴 **"日志"不是单一对象**：同一目录下并存多份活日志 ⇒ 任何"日志里有 N 处 X"必须写**哪一份 + 身份**（我 06:48 实测）
```
%LOCALAPPDATA%\AIPlayer\logs\   （同一目录，同一时刻）
  aiplayer.log                 6,203,812 B @06:43:05.824   `--user-agent=` **0** 处 ｜ `--http-header=` **150** 处
  log-player-2026-09-12.txt    1,259,964 B @06:33:18.864   `--user-agent=` **11** 处 ｜ `--http-header=` **152** 处
  log-player-2026-09-11.txt      502,763 B @23:56:27.308   `--user-agent=` **0** 处 ｜ `--http-header=` **27** 处
  startup-trace.log              609,208 B @06:33:18.576   两者均 0
  （另有 40+ 份 `log-YYYY-MM-DD.txt` / `log-player-YYYY-MM-DD.txt` 归档）
⇒ 同一句"日志里有 11 处 `--user-agent=`"在 `aiplayer.log` 上读出来是 **0**；我 06:0x 的读数与 06:48 的读数**不是同一个文件**。
⇒ 纪律：报日志类计数必须写 **文件全路径 + bytes + mtime（+ 采样时刻）**；像 §12.5.1.4 的"面先写死"一样，
  否则"今天 11 处、明天同一路径 0 处"会被读成"现象消失了"。**行号类断言（"第 9228 行"）同理，必须带日志身份。**
  （同族：§12.2.1 提交时刻不能替代产物身份；本条是它在**日志面**的形态。）
⑦ 🔴 **FAIL 的归属只能从 `CHECK|…|verdict=` 行读，绝不能从 `FINDING|` 行读**（`reviewer` 报、我实测确认，2026-09-12）
   实测：我某次全量跑里 `FINDING|H7|EMOJI|…` 共 **42 行**，而 `CHECK|H7-emoji-in-ui-copy|verdict=PASS|newAstral=0|newFe0f=0` ——
   那 42 行是**既有存量的逐文件清单**（H7 设计如此：每次打印全量存量 + 基线 + byOwner），**不是"新增命中"**。
   ⇒ "这条门禁红了没有"**只由 `CHECK|…|verdict=FAIL` 决定**；看 `FINDING|` 行数会把 PASS 读成红（今晚已有人差点这么读）。
   ⇒ 同类：`FINDING|H1|ALLOWED|…`（允许清单内的合规项）也不是红点；权威汇总仍只有 `SELF-CHECK|` 与 `SUMMARY|` 两行。
   ⇒ 可选项（**输出形状变更 ⇒ 须 captain 裁**）：把那些行的前缀从 `FINDING|H7|` 改成 `STOCK|H7|`，让"存量清单"与"红点"字面上分开。
   ⇒ ✅ **已落地（captain 2026-09-12 一句话授权，本提交内改前缀 + 注明）**：见 §12.5.1.5 ⑨（对照读数 + grep + 逐文件计数表 + 反控）。
⑧ 🔴 **`-p:OutputPath=<dir>` 只改"输出面"，不隔离 `obj/`**（`ui3` 报、我复核，2026-09-12）
```
实测：`E:\ui3-search-bin\AIPlayer.Shell.dll` 与 `shell\App\obj\Debug\…\win-x64\AIPlayer.Shell.dll` **逐字节同件**；
      而同配置的 `shell\App\bin\Debug\…\AIPlayer.Shell.dll` 是**另一件**（1,632,256 B / F049909B6A6E / 05:30:28.915）。
⇒ "隔离输出目录"防的是**输出面互踩**（不同人各写各的 bin），**不防 `obj/` 争用**：
   两个人同时 `dotnet build` 同一工程仍会在 `obj/` 上互相踩（乱序增量、半成品产物）。
⇒ 判据：**构建前先看 `Get-Process dotnet`（含 MSBuild/XamlCompiler）**；非空即等，不是"我有 OutputPath 就安全"。
⇒ 同族：与 §12.2.3（pin 必须含被加载的产品程序集）叠加使用 —— 运行目录里那支 dll 与 obj 同件、与 bin 不同件，所以身份只认**运行目录里那一支**。
```
```

⑨ ✅ **H7 存量行前缀改名：`FINDING|H7|EMOJI|` → `STOCK|H7|EMOJI|`**（captain 2026-09-12 一句话授权；同提交内改前缀 + 本条注明）
   **为什么**：⑦ 已说清那 42 行是**既有存量的逐文件清单**、不是"新增命中"；但只要它还顶着 `FINDING|` 前缀，
   读者（今晚已发生两次）就会把 `H7=PASS` 读成"红了 42 条"。改名让"存量清单"与"红点"在**字面上**分开 —— 改名是"让人不误读"，⑦ 的纪律是"让人读对"，两者都留。
   **对照读数**（同一工作区、同一时段，改前/改后各一次全量跑；原文见 `shell/Tests/evidence/h7-stock-rename-control.txt` —— 该件 = 13,025 B / `441C193AA81D` / 111 行 / CR=0 / mtime 2026-09-12 16:58:37.313，**已入库** `cc3af35`（17:01:23）：当刻 `git ls-files` 追踪=True、`inHEAD`=True、`git status --porcelain` 空 ⇒ H2a/H2b 两条都过。`t156` 报的 `FINDING|H2b|MISSING|…` + `INCONCLUSIVE`（16:58:12.798 @HEAD `52ff437`）是该件**入库之前**的时点读数，不是缺件）：
```
改前 16:49:20.880 -> 16:51:03.622 @head 940f295：SUMMARY|verdict=PASS|checks=10|pass=10|fail=0|inconclusive=0
     SELF-CHECK equal=True ｜ FINDING|H7|EMOJI| 行 = 42 ｜ FINDING| 合计 = 44 ｜ H7 CHECK=PASS（emojiAstral=53|fe0f=51|filesHit=42|baselineAstral=53|baselineFe0f=51|newAstral=0|newFe0f=0）
改后 16:51:45.140 -> 16:53:31.843 @head daecbc0：SUMMARY|verdict=PASS|checks=10|pass=10|fail=0|inconclusive=0
     SELF-CHECK equal=True ｜ STOCK|H7|EMOJI| 行 = 42 ｜ FINDING|H7| = 0 ｜ FINDING| 合计 = 2（两条均为 FINDING|H1|ALLOWED| 允许清单合规项，不是红点）｜ H7 CHECK=PASS（measured 逐字段与改前相同）
逐文件计数表：改前 42 文件 = 改后 42 文件，only-before=0、only-after=0、逐文件 (astral,fe0f,owner) 不一致 = 0
⇒ 只是改名，没有丢行、没有变判据。
```
   **反控（可失败）**：`-EmojiAstralBaseline 0` ⇒ `CHECK|H7-emoji-in-ui-copy|verdict=FAIL`、`STOCK|H7|` 行照打、`SUMMARY` 与 EXIT 转红 ⇒ 改名没有把判决路径一起改掉。
   **grep 全仓（`FINDING\|H7`）**：发射点只有本门禁（`:881`，已改）；其余命中全是**历史引用**（`h7-emoji-gate-controls.txt:22`、`gate-captain-065747.txt`、`gate-captain-074646.txt`、本 README、`shell/docs/WORKSPACE.md:3903/:4338`），**没有任何脚本按该前缀解析** ⇒ 无静默失效；历史证据与 captain 台账按 append-only 一律不动。
   **纪律保留**：红/绿只看 `CHECK|…|verdict=`（⑦）；`STOCK|` 永远不是红点；权威汇总仍只有 `SELF-CHECK|` 与 `SUMMARY|`。

### 12.5.2 captain 裁决：`先抓`（2026-09-12）—— 抓取与前缀改动之间的顺序

```
裁决: **先抓菜单图**，`ui3` 在裁决前**不再动** `ServersPage.xaml(.cs)`
理由(我的建议 + 他采纳): ui3 的 t64 UI 改动**已在**当刻 dll 里（`B156252C3771`，两笔产物逐字节核验：`MENU-SELFTEST ARMED` @1245967、`ServerIconSource` @1245639）
   ⇒ 先抓可得到"UI 改动已含 + ARMED 可诊断"的那一版；他一改 UI，图与 dll 身份会**同时作废**，需再来一轮冻结+重建
窗口: **暂不放行**（当刻跑着的是 captain 给用户起的实例，`E:\cap-bin\`，默认落点=首页）
   ⇒ **他喊"窗口已释放 + 当刻实例数"之后**才跑；我跑前那一刻**再重采一次实例数**（不以旧值为准）
```
**抓取那一刻的命令（预置，一步到位；`-Confirmed` 是硬闸，必须有）**
```powershell
# 起窗前: powershell -File shell/tools/capture-shell-flyout.ps1 -Preflight        （只报条件，绝不起进程）
# 正式:  ⚠️ 先确认 instance=0，再跑下面这条
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/capture-shell-flyout.ps1 `
  -Confirmed -Out <网络路径>.png -Sidecar <网络路径>.txt
# 负反控(同一 exe、同一页、不设钩子 ⇒ 必须无 popup):
powershell.exe -NoProfile -ExecutionPolicy Bypass -File shell/tools/capture-shell-flyout.ps1 `
  -Confirmed -Mode none -Out <临时目录>\t30-servermenu-negative.png
```
🔴 **本节首版把两个 `-Out/-Sidecar` 写成了真实证据路径 ⇒ 门禁 H2b 当场把这两个文件名当成"被引用的证据"并报 `MISSING`**（`citedBy=shell/tools/README-ui-audit.md:804`）—— **我第四次踩同一个坑**（前三次：`VERIFY_S1 §59` 探针名、`§11.5-1` 抄允许清单正则、`§10.9` 示例命令）。
⇒ 纪律重申（**这次连"解释段"也一起改**，因为我上次就是在解释段里又写了一遍才继续命中）：**示例/命令里的证据路径一律用占位符**（`<网络路径>.png` / `<临时目录>\…`），**或打断 `evidence/` 与文件名的相邻关系**；**H2 是纯文本判据，分不清"引用"与"例子"**。

### 12.5.3 抓图判据 v2（captain 2026-09-12 裁决，**取代**「dll 必须 == `B156252C3771`」）
**为什么换**：`SEAM②` 会让 `ui3` **合法地**改 `Features/Search/**` ⇒ 那份 dll 一定会变 ⇒ 固定 hash 判据**自我报废**。新判据证明两件事（「图属于这份 dll」+「这份 dll 的服务器菜单 UI 与工作区源码同源」），比等一个固定 hash **更强**，且不被 `SEAM②` 破坏。

| # | 判据（captain 原口径） | 脚本输出行（`shell/tools/capture-shell-flyout.ps1`） |
|---|---|---|
| 1 | 抓前**重采**实例数，`instances >= 1` ⇒ `REFUSED`；`-Confirmed` 硬闸必须有 | `REFUSED\|…`（退出码 3） |
| 2 | 当刻采 `dllSha256_12 + dllMtime + head + capturedAt`，并对**那一份** dll **双扫** `MENU-SELFTEST ARMED`、`ServerIconSource`，**两串都须命中**（报命中编码；否定结论要两扫读数） | `CODE-IDENTITY\|…`、`CODE-SCAN\|needle=…\|ascii=\|utf16=\|verdict=HIT/MISS\|hitEncoding=`、`CODE-SCAN-SUMMARY\|` |
| 3 | **同源性**：两个 ServersPage 文件的 mtime 必须等于钉住值，不等 ⇒ 停下报差异 | `SOURCE-SYNC\|…\|mtime=\|expectedMtime=\|verdict=OK/MISMATCH` |
| 4 | 其余不变：`FG-SETTLED`、独立像素复验、停进程、`POST-STOP\|instances=0` | `FG-SETTLED\|`、`VERIFY\|independent\|`、`POST-STOP\|` |

**钉住值（captain 给定）**：`ServersPage.xaml.cs` = `2026-09-12 02:13:19.606`、`ServersPage.xaml` = `2026-09-12 01:58:45.761`。
脚本**同时打印这两个文件的 `sha256_12`**，但**判据仍是 mtime**（脚本不会因为 hash 相同就放宽）：mtime 相等**不**证明内容相等，内容相同的一次触碰也会改 mtime ⇒ 两个读数并排给出，**人工可判**，门禁不擅自改口径。

**实测输出（`-Preflight`，02:39:30；dll=`B156252C3771`/1,529,856 B/02:19:48.654，HEAD=`16fc805`）**：
```
SOURCE-SYNC|shell/App/Features/Servers/ServersPage.xaml.cs|mtime=2026-09-12 02:13:19.606|expectedMtime=2026-09-12 02:13:19.606|verdict=OK|bytes=23741|sha256_12=8AA466F0F6B8
SOURCE-SYNC|shell/App/Features/Servers/ServersPage.xaml|mtime=2026-09-12 01:58:45.761|expectedMtime=2026-09-12 01:58:45.761|verdict=OK|bytes=4600|sha256_12=C85AC26D4DF8
CODE-SCAN|needle=MENU-SELFTEST ARMED|dll=AIPlayer.Shell.dll|ascii=-1|utf16=1245967|verdict=HIT|hitEncoding=utf16
CODE-SCAN|needle=ServerIconSource|dll=AIPlayer.Shell.dll|ascii=-1|utf16=1245639|verdict=HIT|hitEncoding=utf16
CODE-SCAN-SUMMARY|needles=2|miss=0|elapsedMs=82|…|dllBytes=1529856
GATES|verdict=PASS|sourceSync=OK|codeScan=HIT
```
（两个 utf16 偏移 `1245967` / `1245639` 与先前独立记录**逐位相同** ⇒ 扫描器与旧读数互校通过。）
新增 `POST-CAPTURE-IDENTITY|dllSha256_12=…|dllMtime=…|identicalToPre=True/False`：抓完重采一次，证明「图所归属的那份 dll 在运行期间没被换掉」——**把假设变成读数**。

**三条反控（刚跑，`instances` 前后均 = 1、未产出任何 PNG、**未动仓内任何文件**；夹具在 `%TEMP%`）**
1. 夹具仓 `%TEMP%\syncprobe2`（工具放在 `shell/tools/` 深度 ⇒ 它的 `$repoRoot` 才算得对）⇒ `SOURCE-SYNC|…xaml.cs|mtime=…02:40:22.128|expectedMtime=…02:13:19.606|verdict=MISMATCH` + `…xaml|verdict=MISSING` + `STOP|source-sync 2/2 mtime mismatch` + **EXIT=3**（两条分支一次覆盖）。
2. 同一夹具加 `-Preflight` ⇒ 仍打印 `STOP` + `STOP-NOTE|preflight never refuses (its contract is exit 0)`，**EXIT=0**。
3. `-Exe C:\Windows\System32\notepad.exe`（⇒ `notepad.dll` 不存在）⇒ 两串 `ascii=-1 utf16=-1`、`miss=2`、`STOP|code-scan 2/2 needle MISS`、**EXIT=3** —— 证明「否定结论带两扫读数」这条真的会走。

**顺带修掉一个只有"真跑"才暴露的字段缺陷**：`PROVENANCE|dirtyList=` 曾**吃掉每条路径的首字符**（`shell/App/App.xaml.cs` 打成 `hell/App/App.xaml.cs`）。成因 = **先 `Trim()` 再 `Substring(3)`**：git porcelain 是 `XY path`（2 状态位 + 1 空格），Trim 去掉前导空格后 `Substring(3)` 就咬到路径本身。**计数是对的、名字是错的** —— 这类字段最危险（数字看着对，将来拿它做归属就错）。修法：`Substring(3)` **之后**再 `Trim()`（02:39:44 复跑，`dirtyList` 恢复 `shell/…`）。
> 教训与 `§12.9/§12.10` 同族：**一个字段的"名字"必须说清它数的是什么、也要说清它印的是哪一段**；而这条**静态读代码看不出来**（我读过那行三遍都没发现），是 `-Preflight` 真跑才暴露的 —— 再次印证「静态通过 ≠ 可用」。

**🔴 2026-09-12 02:45–02:47 实测：抓取目标**自己**崩了 —— 纪律「**先证能跑，再谈抓得到**」**

正抓 ×2 + 负反控 ×1，**三次全失败、零 PNG**（原始读数）

| 运行 | 起始 | 读数 |
|---|---|---|
| `hook` | `02:45:37.807` | 日志止于 `MENU-SELFTEST ARMED 02:45:39.224`；8 次 `-List` 全 `ERROR\|target pid not found` |
| `hook` | `02:47:22.786` | `PROC\|exited=true\|exitCode=-1073741189\|at=02:47:24.067\|listCallsSoFar=0`（ARMED 后 **55 ms**） |
| `none` | `02:47:38.409` | `exitCode=-1073741189\|at=02:47:44.092`；`ServersPage loaded 02:47:39.820` 之后**无任何行**（4.3 s 后死） |

`-1073741189` = **`0xC000027B` = `STATUS_STOWED_EXCEPTION`**（WinRT/XAML 未处理异常）；Application 事件日志里**没有** .NET Runtime / Application Error / WER 记录。
**三条排除**（各自都有读数）：① **钩子不是原因** —— `-Mode none`（不设 `SHELL_SELFTEST_SERVERMENU`）**同码同崩**；② **环境不是原因** —— captain 的 `E:\cap-bin3`（另一支 build）同时段一直正常（`HOME IMG-OK 19/20`、`RAIL-PROBE` 都在刷）；③ **不是缺资源** —— 两个目录都 **199 个文件**、`.pri` 资源集一致。
⇒ 结论只落在一个地方：**`E:\ui3-verify-bin\AIPlayer.Shell.dll = B156252C3771 @02:19:48` 这一支起不来**，而且它此前**从没被谁真正跑过**（owner 报过"未起窗未抓图"）。

> 🔴 **纪律（本轮最贵的一条）**：抓图前**先跑一次无钩子冒烟**（`-Mode none`）证明"**这支二进制能活**"，再谈"能不能抓到弹窗"。把"起不来"读成"抓不到"会让整条证据链建在沙上 —— 而我的旧输出**正是这么写的**（见下条 ②）。

**同一次真跑还暴露了四处工装缺陷（静态读代码看不出来，已全部修掉）**
1. **轮询前加进程存活检查** ⇒ 新增 `PROC|exited=\|exitCode=\|exitAt=\|listCallsSoFar=` —— 正是它把含糊的 "no flyout candidate" 变成 `0xC000027B` 这个硬退出码；
2. **`DIAG|verdict` 拆开 `ARMED` / `BEGIN` / `layout-FAIL`**（旧判据 `-match 'MENU-'` 命中的是 `ARMED`，却打印"hook ran; the flyout HWND was simply not among the enumerated candidates" ⇒ **把人往错误方向引**）；
3. **`-Mode none` 在 app 已死时不再给 PASS**（`02:47:44` 那次它确实打了 `CONTROL-RESULT|PASS`，而 app 当时已崩 ⇒ 我控制项里的**假 PASS**；现为 `INCONCLUSIVE` 且附退出码）；
4. `menuLogLinesTotal` → **`menuLogLinesSinceStart`**（原来对**整个日志文件**计数，而日志是**全局共享**的 ⇒ 会把别人跑出来的 `MENU-` 行算进我这次控制）。
另：`Process.StartInfo.WorkingDirectory` 设为 **exe 自身目录**（`UseShellExecute=false` 且不设它时，子进程继承**调用方** cwd —— 平时大家都是各自输出目录起窗，我这里是仓库根 ⇒ 若有东西按 cwd 相对解析，两次就差在这里）。⚠️ **这是消除变量的动作，不是结论**：它**未**被证明与崩溃有关（要 A/B 才能说）。

**🔴 针的设计纪律（captain 2026-09-12 采纳，来自一次真跑发现）**
1. **选针该问的不是"它现在在不在"，而是"它在**所有目标代际**上都在吗"** —— `ServerIconSource` 的教训：它在崩溃代际 `utf16@1245639` **命中**、在修复代际（`ui3` 把该 XAML 资源键删掉的那次修复）**MISS** ⇒ 一根"只活在某一代"的针会把"**UI 改了**"读成"**这份 dll 不对**"。
2. 现行针集（命令参数，非硬编码）：**`MENU-SELFTEST ARMED`（utf16）+ `ServerRowContextMenu`（ASCII）**；`-ScanNeedles 'A;B'` 可换。**`MENU-INVENTORY` 不用**（captain 独立扫得 MISS；我这里 `ascii=-1`、**`utf16` 命中** —— 分歧本身就说明**否定结论必须两扫一起报**，见 `§12.9`）。
3. **`NEEDLE-RETIRED` 行（新）**：退役的针也要印出来 —— 否则下一个读报告的人看到 `MISS` 会以为"**修复版少了东西**"。格式：
```
NEEDLE-RETIRED|<name>|reason=<为什么退役>|since=<时刻>|dll-ascii=<n>|dll-utf16=<n>|stillInActiveSet=<bool>|note=MISS 是预期的，不得读成"这份构建缺该标记"
```
> 同族纪律：**`MISS` 也必须说清是"不该在"还是"该在却没了"** —— 与 `registered=`→`allowed=`/`registeredNoBoundary=`、`capturedAt`↔`scannedAt`、`dirtyFilesRepo` 分名一次同类：**名字/字段必须说清它数的是什么、以及它为什么是现在这个值。**

### 12.6 取证通道词汇表（captain 2026-09-12 批准 `render-inproc`；**不新增门禁规则**）
| 通道名 | 机制 | 本仓实测能抓到 | 抓不到 | 证据命名要求 |
|---|---|---|---|---|
| `screen-copy` | `CopyFromScreen`（按窗口矩形；档 B 须**先前台化**） | 内核 D3D 合成窗口（`PrintWindow` 对它全白/全黑） | —— | 必须在 `privacy-allowlist.txt` 登记（`rect-source=` + `owner=`）**且**脚本有四护栏；文件名/侧车标 `screen-copy` |
| `printwindow` | `PrintWindow(+PW_RENDERFULLCONTENT)` | 普通顶层窗口（XAML 外壳主窗口、WinForms 探针）—— 01:28:28 实测 `nonBlackPct=98.114` | 内核 D3D 合成窗口 | 文件名/侧车标 `printwindow` |
| `render-inproc` | 进程内 `RenderTargetBitmap`（`ui3` 新通道，captain 已批） | 只读**本进程**可视树；不抓屏、不抢前台、不驱动输入 | **别的进程/窗口**（进程内 ⇒ 原理上取不到）；其它单元格**我未复跑，不写结论** | **必须显式标注 `render-inproc`，并与窗口/屏幕渠道分开命名** |

**`render-inproc` 的四条批准条件（captain 原话要点，逐条落地）**：① **只许进程内**；② **证据必须显式标注通道，且与窗口·屏幕渠道分开命名**；③ **不得代替窗口级证据**；④ **不属抓屏，但不得据此绕过 `H1` 四护栏**。
> ⚠️ 我**没有**在 `H1`/`H1b` 里加"PNG 必须声明通道"的判定 —— 那属新政策，**加之前先问 captain**。本表只是**词汇约定**。

### 12.7 一句话口径（captain 事实 212 的产物，终局读数必须带上）
> **五项 PASS = 证据面自洽；产品面是否达标由 `t34`/`t35` 判。**
- 这是"哈希一致 ≠ 内容现行"的**规格版**：**证据卫生只能证明"证据是真的"，不能证明"东西是对的"**。
- 实例（captain 逐行核出）：`ui3` 报"§9.2 文案 ✅"，而 `SettingsPage.xaml.cs` 里**仍有无条件赋值的长文案**（含 `全仓检索 0 命中` / `themeMode=` / `%LOCALAPPDATA%…` / `CrossServerSyncService` / `§6.1`）—— **他自己的整页渲染图也暴露同一批问题** ⇒ `H5`/`H2a` 全绿也**照不出这件事**。
- ⇒ `t34` 的并排比对与 `ui-design-audit.ps1` 的几何断言是**产品面**判据，与我的证据面判据**互不代替**。

### 12.8 ⏳ 将来若实现 `UI_SPEC_SHELL §9.2` 扫描：**先取"文案值"面，再套模式**（captain 2026-09-12 转达的实测坑，**现在不实现** —— 冻结期）
```
坑: §9.2 判据② 的 camelCase 模式 `[a-z]+[A-Z][A-Za-z]*=` **只能作用于"文案值"，不能作用于整行**
实例: ui3 第一版按字面写（整行套模式）⇒ 在 XAML 上**假报 26 处违规** —— 把 `FontSize=` / `Foreground=` 这类**属性名**
      当成了 camelCase 字段名
要求: 实现时先取**文案值面**（`Text` / `Content` / `Header` / `PlaceholderText` 等的**值**），再套模式；
      并配一条**反控**：**属性名（`FontSize=` / `Foreground=` / `RequestedTheme=` …）不得被判违规**
```
**同族提醒（本文档已第 6 次出现同一形状）**：**先问"我该看哪个面"，再写模式** —— 整行 / 全文件 / 两份副本 / 回看窗口少一格 / 计数器名字 / 哈希当现行性，全部是"面没选对"的子类。

### 12.9 二进制串扫描：**必须 ASCII + UTF-16 双扫**（缺一即不许下"不存在"的结论）
```
坑（services 自报，我另用反控验证）:
  .NET 字符串常量在 **#US 堆里是 UTF-16** ⇒ 只做 ASCII 扫描会得出"二进制里不含某串"的**假阴性**
  services 实例: data\player\AIPlayer.MpvHost.dll 里 'ai-player' 的 ASCII 扫描 = False，UTF-16 扫描 = **True**
🔴 我自己的镜像缺陷（同一错误的另一半，用反控暴露）:
  我的扫描器原来**只有 UTF-16** ⇒ 对**纯 ASCII 存储**的串会假阴性。
  反控: PE 头控制串 '!This program cannot be run in DOS mode' ⇒ ASCII=77、**UTF-16=-1**
⇒ 判据: **`二进制串扫描 = ASCII 扫描 AND UTF-16 扫描`，缺一即不许下否定结论**（肯定结论亦然：要说明是哪种编码命中的）
```
**services 补的两条实操（2026-09-12，我采纳并写死）**
```
① 两种编码的**典型归属**（所以一个二进制里常**同时存在**两种，只用一种必然在另一半上假阴性）:
     **原生 / PE 串**（PE 头、导入表旁的 ASCII 文本）      = **ASCII**
     **.NET 字符串常量**（`ldstr` 指向的 #US 堆）          = **UTF-16LE**
② **否定结论必须把两扫读数一起给**：`ASCII=-1 且 UTF-16=-1` 才算证据；**只报一个 -1 不算**。
   同理**肯定结论要报编码**（"UTF-16 命中 @offset"），否则别人无法复算你查的是哪一半。
③ **ui3 的锐化表述（我采纳为判据原话）**：**"任一编码命中即为'在'；只扫一边只能得'未命中'，不能得'不存在'"。**
   他另把自己上一版的"只比前几字节的弱匹配"**删掉了** —— 那条比两种单扫都差：它会产生**假阳性**（命中一个碰巧相同的前缀）。
   ⇒ 本判据的三档：**弱匹配（假阳性）< 单扫（假阴性）< 双扫 + 报编码（可用）**。
④ **同族命名纪律（ui3 2026-09-12 指出我的一处字段歧义）**：`dirtyFiles` 我是**全仓口径**（`shell/App` + `shell/Services`），他的是**领地口径**（他那一屏的文件）⇒ 两个数不可比。
   ⇒ 已改名 `dirtyFilesRepo=<n>` + 新增 `dirtyScope=shell/App+shell/Services`；**"计数器名必须说清它数的是什么"**（与 `registered=` → `allowed=`/`registeredNoBoundary=` 那次同一族）。
```

### 12.10 内核产物身份：**fork 与原版是两条口径**（services 提边界，我独立验证）
```
原版内核 E:\AI Player\data\player\AIPlayer.MpvHost.dll  bytes=979456  sha12=8D73526C2C07
   'KERNEL-FORK' : ASCII=-1  UTF-16=**-1**   ⇒ **原版不产 `KERNEL-FORK rev=` 行**（services 的边界成立）
fork 产物 %TEMP%\t45\run\AIPlayer.MpvHost.dll : 'KERNEL-FORK' ASCII=-1 **UTF-16=700548** ⇒ 该行是 **fork 专有**
⇒ 口径:
   **fork 内核**   : sha12 + `KERNEL-FORK rev=N` + 采样时刻
   **原版内核**    : sha12 + bytes + 采样时刻（**它不产 rev 行**；别把"没有 rev 行"当成"不是内核"）
   **两种都要**    : 归属必须**对上自己的命令行** —— 内核日志是全局共享的，`KERNEL-FORK rev=1 …\Temp\t45\…` 那行是**别人的进程**写的
```
⇒ 与 §12.2 合并成一句：**身份 = sha12（+ 该产物真正会产出的那一行日志）+ 采样时刻**；**"没有某个字段"必须先证明那个字段根本不存在**。

### 12.11 托管外壳的 `exe` **两个方向都不能用来判代码**（我自己的措辞修正，2026-09-12 实测）
```
同一路径 E:\ui3-verify-bin\AIPlayer.Shell.exe（始终 291,328 B）:
  01:28:32  sha12=B3F962ECD9A4
  01:55:20  sha12=FFC52096BB5D     ┐ 两次构建**同 sha12**，而中间 ui3 改了 ServersPage.xaml(.cs)
  01:59:25  sha12=FFC52096BB5D     ┘ ⇒ **同 exe sha12 ≠ 同代码**
  02:19:48  sha12=10CD5DB7B8A8     ← **exe 自己也变了**（仍 291,328 B），双扫后**仍不含** 'MENU-SELFTEST ARMED'（-1）
⇒ 两个方向都不成立:
   同 exe sha12          ⇒ **不能**推出"代码没变"（代码在 dll）
   不同 exe sha12        ⇒ **也**不能推出"代码变了"（apphost 可因无关原因重写；它本来就不含我们的代码）
⇒ 唯一正确口径: **代码身份 = `AIPlayer.Shell.dll` 的 sha12 + mtime**；exe 字段只作**启动器身份**（信息量：记录用）
   ⇒ 我前一轮写的"exe 是**稳定** stub（字节不变）"**过强**，按上表更正为"**通常**不变，但**不保证**不变"
```



⑩ 🔴 **在"解释/更正/偏差说明"句里提到路径，必须用非引用形状**（`reviewer` 2026-09-12 提出、我复算确认；与 §100/§102 我两次自踩同族，这是第三例）
   成因（比"引用了旧名"更精确）：H2 的取件名正则只认 **"`evidence/` 邻接 + 合法扩展名"** 这个**形状** ⇒ 一句**解释卡面要求落在哪**的话（`kernel/CHANGES.md:938` 的"**偏差**：卡面要求证据落 A；我实际落 B"）会被当成"引用了一件证据"⇒ H2b 报 `missing=1`（**形状假阳性**，不是件不存在，也不是谁引用了退场名）。
   ⇒ 规则：**解释/更正/复述类句子里出现的路径，一律用非引用形状**（目录与件名**拆开**写，或写成裸名），并**就地标注**"仅陈述路径、非引用"。
   ⇒ **不要**为了消 MISSING 去把偏差句改成"盘上真实文件名"——那会把"卡面要求 A、实际落 B"这句**偏差记录改成另一种意思**（A 消失），历史失真。
   **当刻核实（我 17:11:20.859 → 17:13:51.468 @head `e2eeda6`，`-DocRoot kernel` 全量跑）**：
```
SELF-CHECK|checks=10|pass=9|fail=1|inconclusive=0 ｜ SUMMARY|verdict=FAIL|citedNames=29|scannedFiles=214
CHECK|H2a-cited-evidence-tracked|verdict=PASS|untracked=0
CHECK|H2b-cited-evidence-present|verdict=PASS|missing=0        ← 该 MISSING 已由属主按"拆开形状"修掉（曾是 missing=1）
CHECK|H7-emoji-in-ui-copy|verdict=FAIL|emojiAstral=54|baselineAstral=53|newAstral=1   ← 当刻唯一红点，与 H2 无关
  定位（git 逐行对比，不猜）：shell/App/Features/Detail/DetailPage.xaml.cs（在制 ` M`，worktree-blob 200d00d70dfa ≠ HEAD-blob dc3feeeb9242）多了一处 astral emoji ⇒ 归该件属主，已在原位报过
残留（当刻无害、但同族）：kernel/CHANGES.md:1508 把门禁自己的 `FINDING|H2b|MISSING|…` 行**逐字抄了回来**；因该处件名没有 `evidence/` 邻接，当前**不触发**取件名正则 ⇒ 未产生新 MISSING；若将来那行被改形状（例如补上目录），会立刻复活。
```⇒ 🆕 **收口成两句（`reviewer` 2026-09-12 提议，我并核实证）**：
```
① 形态只能**数 CR** 得到（worktree 形态 / index blob 形态 各给一个 CR 数）—— 不能从 `.gitattributes` 推。
② **两形态是否相同只由 `text` 决定，与 `eol=` 无关**：`text: set` ⇒ 入库按 eol 归一 ⇒ **两形态字节差 = CR 数**（预期行为，不是异常）；
   `text: unset`（如 `**/evidence/**`）⇒ 不做归一 ⇒ 两形态逐字节相同是前提。
   证伪条件（至今未观察到）：**`text: set` + worktree 有 CR，而两形态字节相同**。
```
   符合例（我 17:07 实测，`shell/App/Features/Servers/EVIDENCE_T30.md`）：worktree 90,966 B / `A9D16A7C9D91` / 615 行 / `wtCR=614`；
   index blob 90,352 B；字节差 90,966 − 90,352 = **614 == wtCR 614** ✅；`git check-attr` = `text: set` / `eol: lf`。
   ⇒ 该例里"`eol: lf` 而盘上是 CRLF"**确实是陷阱**（会让人误以为 worktree 是 LF）；但"两形态不同"本身是**预期**，不叫陷阱。
### 12.5.4 🔴 身份行的"裸文件名"会滑出 `H5` 扫描面（ui2 报、verifier 复现并落地，2026-09-12）

**现象（判据的覆盖面缺口，不是判据本身算错）**：`evidence-hygiene-check.ps1` 的 H5 行识别要求该行含 `evidence[/\\]<文件名>`（见该脚本 :775）⇒ **用裸文件名写的身份行一律不进扫描面**，于是"文档写了过期身份"可以**长期不被发现**。

**实证（我 17:45 实测；扫描面 = `shell/**` 的 `.md` + `.txt` 共 310 件）**
```
shell/App/Features/Aggregate/t31-U-E-EVIDENCE.md
  :417  | `t31-favorites-selftest.txt` | 6,618 / 85 | `C8AB14D320B9` | … |   ← 裸名；该 sha **已过期**
  :418  | `t31-aggregate-selftest.txt` | 7,446 / 88 | `DC1C0B0F3823` | … |   ← 裸名；恰好仍与盘一致（运气）
盘上：t31-favorites-selftest.txt = 6,618 B / 85 行 / **C565252A8DBD**（bytes 与行数都对得上，只有 sha 过期）
裸名身份行 Σ = 2 （= 上述两条）｜ 带 `evidence/` 前缀的同类行 = 0
⇒ 这类行"看起来完全正常"，正是 H5 存在的理由，却恰好落在它的视野之外。
```

**处置（advisory：只报、不改判绿）**：H5 命中裸名身份行时输出
```
NOTE|H5|bare-name-identity-row|<doc>:<line>|<文件名>|declaredSha12=<sha>|advice=…写全路径…才进 H5 扫描面
NOTE|H5|bare-name-identity-rows|count=<N>|advice=advisory only, nothing fails here；改写为全路径即回到扫描面
```
并把 `bareRows=<N>` 加进 H5 的 `measured=`（PASS 与 FAIL 两个分支都加）。⇒ 不新增检查项、`checks=` 仍 10、不改任何既有 VERDICT。

**对作者的纪律**：证据/台账写身份**一律写全路径**（`…/evidence/<文件名>`）。只写裸名时，即使 bytes 与行数都对得上，**哈希过期也永远不会被门禁发现**；历史遗留的裸名行按 append-only 保留，但应就地把该行改写成全路径（改写后 H5 会真校验，`scannedRows` 随之增加）。

### 12.5.4.1 🔴 H5 身份行解析的**隐含约束**（`t238` 实测落地，2026-09-12）—— 同族已踩两次

**这是 §12.5.4 的续条**：§12.5.4 修的是「裸文件名滑出扫描面」；本条修的是「全路径行进了扫描面，但**字节列被别的格子顶掉**」。

**判据本身**（`shell/tools/evidence-hygiene-check.ps1` 的 H5）：对每个**含** `evidence[/\]<文件名>` 的行，取一个「数字格」当**该行声明的字节数**，再与磁盘实读比较（`expectedBytes` vs `actualBytes`）。取格规则决定了**一条隐含约束**：

> **身份行里，比 bytes 列更早出现的格子不能长得像字节数。**

**两次实测**（同族，都在 2026-09-12；两处读数后来都由 `ui3` 在 `t197` 修掉、队长入库 `cfbe986`）：

```
同族形态（旧取格规则 = 取「首个逻辑格的下一格起、第一个形如 ^[0-9][0-9,]* 且后跟 / 或行尾的格」）：
  ① 裸数字 hwnd 挤位：| <全路径> | 3017916 / 16404 | 60,908 | `5B22F81F2B97` |
     ⇒ 读到的「bytes」= 3017916（hwnd），判 HASH-MISMATCH：
        expectedBytes=3017916 / actualBytes=60908 / expectedSha12==actualSha12=5B22F81F2B97
        出处：shell/App/Features/Servers/EVIDENCE_T30.md:371-373（历史态，t197 前的原样）
        盘上真值：shell/App/Features/Servers/evidence/t64-servers-before.png = 60,908 B / `5B22F81F2B97`
  ② 同一文件同一夜、「整行第一个纯数字段当 bytes」（`hwnd` 抢位）—— `t182` 证据件 :427-428 原样留了 FINDING 行。
```

**两条都不是「测量出错」**：被点名文件的 bytes 与 sha12 在盘上**都**是真的；错的是**书写形态**让解析器挑错了格。

**修法（书写侧，推荐）：把「长得像数字」的格子去数字化。** `hwnd=3017916 / pid=16404` 这种写法在**任何**取格规则下都不会被当成字节数。

**修法（判据侧，`t238` 已落地）：取格规则（当前规则）：**

```
1) 先剔掉 markdown 管道产生的空首格，得到「首个逻辑格」；候选只在它之后找（与旧规则同界，见下）；
2) 候选 = 形如 ^[0-9][0-9,]* 且其后紧跟 / 或行尾的格（⇒ 「6,618 / 85」合法；「01:55:43.798」时钟非法）；
   没有任何候选时：若首个逻辑格本身长得像字节数，就取它（旧规则此情形会返回空）；
3) 若**恰好一个**候选 ⇒ 取它。这一条让 sha12 一旦以数字开头被算成候选，也不会被取错（见下）；
4) 若有多个候选且行内声明了 sha12：取「位于 sha12 格之前」的**最后一个**候选；
   若 sha12 之前没有候选，则取「除 sha12 格本身之外」的第一个候选；
5) 绝不把 sha12 格自己当候选返回。
```

**这条规则与旧规则的实测差异（`t238` 控制语料，逐行一文档，旧=HEAD 当刻二进制、新=工作区）**：

```
语料 12 文档（2 组回归 + 10 个单行形态）｜同批同时刻：旧 scannedRows=74 mismatch=6 fail=1 ｜新 scannedRows=74 mismatch=4 fail=1
差异只有两行（其余逐条相同：X2/X3/X4/X5/X9/X10 与两条负控全一致）：
  X6  | <全路径> | 3017916 / 16404 | 8,510 | `<sha12>` |   旧 expectedBytes=3017916 ⇒ 红（假红：bytes 列真值 8,510）
                                                           新 expectedBytes=8510   ⇒ 绿
  X8  | 4 | <全路径> | 8,510 | `<sha12>` |                 旧 expectedBytes=4      ⇒ 红（假红）
                                                           新 expectedBytes=8510   ⇒ 绿
  X7  | <全路径> | 3017916 / 16404 | `<sha12>` |            旧/新 都 expectedBytes=3017916 ⇒ 都红（= 上面 ① 的形态）
⇒ 两条结论，缺一不可：
   (甲) 旧规则**不是漏检**：它按位置扫也会落到「首个逻辑格」上（X6 旧规则读到了 hwnd ⇒ 命中且报红），
        所以「数字格在最左 ⇒ 整行滑出扫描面」的猜想是**错的**（该猜想在 t238 开工时被写进计划，见 t238 证据件 §0）。
   (乙) 旧规则在 bytes 列**后面**的数字格上会**假红**（X6/X8）；新规则把这个假红消掉，
        代价是 X7 那种「没有 bytes 列、只有裸数字 hwnd」的行仍然读 hwnd —— 因为该形态与合法
        `6,618 / 85` 在字符串层面无法区分（见下方残余风险）。
```

**为什么新规则不会被「sha12 以数字开头」带偏**：12 位 sha12 若首位是数字（如 `3F345754D131`），按 `^[0-9]` 它也会成为候选；此时「sha 之前的最后一个候选」会选中 sha 本身。`t238` 的控制语料正是**先撞上**这一条：把位置规则与「先取 sha」写法合起来后，`| <全路径> | 8,510 | `<sha12>` |` 这类**本来就正确**的行一度变成 `expectedBytes=3`（取到 sha 首字符）。落地的规则用「恰好一个候选 ⇒ 取它」+「绝不返回 sha 格」两条把该假红消掉。⇒ **凡按位置取格，必须显式排除 sha 格**。

**仍存的残余风险**：

- **裸数字 hwnd 且行内无 bytes 列**：`| <全路径> | 3017916 / 16404 | `<sha12>` |`（X7）解析器无法分辨它与合法的 `6,618 / 85`，因此仍会把 hwnd 当 bytes。⇒ 纪律是**去数字化**（`hwnd=… / pid=…`），不是靠判据兜。
- **数字索引在最左、bytes 在其后、sha 之前**：`| 2 | 说明 | 8,510 | `<sha12>` |` 与 `| 8,510 | <全路径> | <sha12> |` 在字符串层面无法区分，按「只报不猜」不处理。

**全仓面上的两个计数（`t238` 扫描实测，扫描面 = `shell/**` 下 `*EVIDENCE*.md` 且行内含 `evidence[/\]<文件名>`）**：

```
带路径身份行 TOTAL = 128（互斥分桶，Σ==TOTAL 已核）
  桶①「首个逻辑格是数字且其后无其他候选」Σ=0   ← 即上条猜想的形态，全仓面不存在
  桶②「行内至少一个候选」Σ=56                  ← 与 H5 的 scannedRows 同族
  桶③「无候选、首个逻辑格非数字」Σ=72           ← 其行内均无 12 位 sha（prose 行），故与桶①同为空
「只报不 FAIL」的计数一律带扫描面与 Σ==TOTAL，不写「应该没有」。
```

**复核口径**：本次结论的每一句都由**门禁整跑**（`-ExtraScanPath <控制语料目录>`，一次一行文档）得出。**不采用**「把函数抽出来单独调」的探针：实测在 PS 5.1 下那种探针会被变量名大小写不敏感撞车（`$shaCell` 被 `$SHAcell` 覆盖 ⇒ 比较恒假），会给出**与真实判据相反**的读数（本卡先按错误探针读数得出过相反结论，随后被端到端反控推翻）。

## 12.5.5 t295 delta：裸 `Write-Output` 行的面与判据 · 回归项 · H8（2026-09-13 verifier；本节纯追加）

**1. 裸 `Write-Output` 行的口径（定稿）**
- 谓词逐字：整行 trim 恰为 `Write-Output` 或 `Write-Output -NoEnumerate`。
- **面 A（任何扩展名，含 .log；排 bin/obj/reversed/.git）＝ 6 ｜ 面 B（.txt/.md/.ps1）＝ 2**，谓词同一。
- 面 A 的 6 **对扫描 universe 不敏感**（6,019 / 6,035 / 3,924 三套 universe 命中逐格相同）⇒ 可直接引用。
- 「台账」不适用：同一谓词在**仅 `.md`** 面命中 **0** ⇒ 该 6 不可能住在台账/文档面里。

**2. 跨行谓词禁作验收**
`) \s* Write-Output \s` **随代漂**（对照代 `e5a77ad` ＝ 3 ｜ 当刻 ＝ 4，四处全是合法换行语句）。
一个每代都可能变的数写进验收＝自埋假红：**可报、不可门**。

**3. 不跨行谓词 ＝ 0 作回归项**
- 谓词：`) [^\S\r\n]* Write-Output [^\S\r\n]`；口径必须是**全文本**（逐行读恒为 0 —— 见下条陷阱）。
- **机制**（这为什么配当回归项）：该形态若真是代码，PS 5.1 **解析都过不去** ——
  `Missing statement block after if ( condition )`，脚本根本加载不了。
- **可失败正控**（作用在内容上，不作用在路径字面量上）：把本门禁**逐字节复制**到 `%TEMP%`，只在副本**内容**里注入：
  注释形 ⇒ 脚本可跑 且 `sameLineCount=`**1**（证明读数不是恒 0 装饰）；真语句形 ⇒ `EXIT=1` / 零 stdout / 上述解析错误。
- 门禁每轮由 `NOTE|H9|noncrossline-writeout|…|sameLineCount=|crossLineCount=` 自报。
- **陷阱**：`Select-String` 逐行读 ⇒ 跨行谓词**恒为 0**（假零）。两个计数一律 `[IO.File]::ReadAllText` + `[regex]::Matches`。

**4. H8 插件目录两副本检查（只 INFO，不进 checks=、不 FAIL）**
- 按**面**搜索：目录叶名匹配 `plugins?`（大小写不敏感）、深度 ≤3、排除 bin/obj/reversed/.git。
- 无命中 ⇒ 照实印 `result=absent`（这是**读数**，不是失败）；命中 ⇒ 绝对路径 + 各自 `sha256_12`，缺一份写 `absent`。
- **不点名任何未经测量的路径**；若指仓库外目录，必须显式声明「仓库外 + 只读 INFO」并给**两条绝对路径**。

**5. 计数面已知限制（只登记，本卡不改）**
`$h5Docs` 由 `$scanRoots` 累加且**无去重** ⇒ 同一根、或**嵌套/重叠**的根传入两次，文档就进两次：
`bareRows`/`scannedRows` 翻倍、advisory 打印两遍（探针实测：2 行 → `bareRows=4`）。
调用面重叠（如 `-ScanRoot kernel -ExtraScanPath <prod>`）时须自行注意。

## 12.5.6 t301 门禁两处收紧：H7 基线跟随存量 + `$h5Docs` 去重（2026-09-13 verifier；本节纯追加）

**1. H7 两条基线降到当刻存量：`EmojiAstralBaseline` 53 → 44、`EmojiFe0fBaseline` 51 → 49（`baselineAt` → `c99c45a`）**
- 理由：棘轮必须**跟随存量下降**。改前 `baselineFe0f=51` 而 committed `stockFe0f=49` ⇒ `new = max(0, stock − baseline) = 0`：
  真实新增 **2 个 U+FE0F** 时仍打印 PASS（astral 余量 9）⇒「有红也报绿」。
- **反向臂**（证明收紧有牙，且**不注入源码**）：`-EmojiFe0fBaseline 48`（= 存量 − 1）⇒ `newFe0f=1` ⇒ `CHECK|H7` **FAIL**、`SUMMARY|verdict=FAIL`（`EXIT=1`）。
  用"压基线"而非"真注入一个 U+FE0F"的原因：门禁 note 逐字规定该旋钮就是反控形态，且它走**同一条** `new = max(0, stock − baseline)` 路径、不必改 `shell/App`（本卡 outOfScope）、跑完天然复原。
- 默认臂不受影响：`PASS|checks=10|pass=10|fail=0|inconclusive=0`。

**2. `$h5Docs` 按规范化全路径去重（`$scanRoots` 逐条累加且原先无去重）**
- 症状：同一个根（或**嵌套/重叠**根）传给 `-ScanRoot` / `-ExtraScanPath` ⇒ 同一份文档进面两次 ⇒
  `bareRows` / `scannedRows` **翻倍**、每条 advisory 打印两遍。同面同参数实测：**改前代 `bareRows=6` → 改后 `3`**。
- 去重键 = `[IO.Path]::GetFullPath($_.FullName).ToLowerInvariant()`；**只变"文档身份"，不变任何 check / 计数器定义 / 扫描面 / allowlist**。
- 三组对照：同根两次 3=3 ｜ 嵌套根 3=3 ｜ 合法两根 3+1=4（**不被误合并**）。

**3. 引用纪律（与本门禁相关）**
- 引 H7 读数必须写明**列**：`emojiAstral/fe0f` 是 **worktree（盘面）** 列，`stockAstral/stockFe0f` 是 **committed(HEAD)** 列（棘轮比这一列）；再带**采样时刻**。
- 引门禁身份必须同轮现采（bytes / sha256_12 / RAL（方法=RAL｜split=v+1）/ blob）；行号只作定位辅助，**不进入判据**。

## 12.5.7 t312：H7「HEAD 侧存量读取」的静默恒 PASS 通路已修（2026-09-13 verifier；本节纯追加）

**对象**：`shell/tools/evidence-hygiene-check.ps1`。改前 = 提交 `684742d` 那版（工作树 93,166 B / `54CD84496A63` / blob `88b93df5eb31`）；改后 = 98,054 B / `873AAF3E9575` / RAL 1,301 / CR=LF=1,301 / nonASCII 0 / Parser 0。

**缺陷（改前代码）**：`:1130` 用 `cmd /c "git cat-file blob HEAD:<rel> > $emTmp 2>nul"` 取 blob，`:1132` 用 `try { ReadAllText } catch { skip }` 读。当 `git cat-file` **失败**时：
- `>` 仍建出 **0 字节**文件（最小复现：`cmd /c "git cat-file blob HEAD:<不存在路径> > tmp 2>nul"` ⇒ 退出码 128、文件存在且 0 字节）；
- `ReadAllText` 对空文件**不抛异常**、返回 `''` ⇒ **走成功分支**，catch 永不触发 ⇒ 该成员被当成「没有 emoji」⇒ 存量**欠计**；
- `:1144` 只要 `$emHeadMembers -gt 0` 就宣布 `stockSource=committed(HEAD)`、**不回退** ⇒ `newAstral = Max(0, 欠计 − 基线) = 0` ⇒ 判绿；
- `2>nul` 连 git 的报错文本都丢掉 ⇒「命令没跑成」与「读到空内容」**不可分**（`PROBE_B_PROTOCOL.md §12-5` 的同族问题回流进门禁自身）。

**改法（11 处替换，逐点可复核）**：① 增 `$emHeadReadFail` / `$emHeadReadOk` 计数；② `cmd` 之后取 `$LASTEXITCODE`，非 0 ⇒ 计数 + 记 `$emSkipped` + `continue`；③ 读成功后再校验完整性：临时件缺失、或长度为 0 而 `git cat-file -s HEAD:<rel>` 非 0 ⇒ 同样计数（**正常路径不增加任何 git 调用**）；④ 成员循环包进 `try { … } finally { Remove-Item … }`；⑤ 仅当 `$emHeadReadFail -eq 0` 才接受 `committed(HEAD)`，否则 `stockSource='worktree(reason=head-read-incomplete:N/M)'`；⑥ 读失败 > 0 ⇒ H7 判 **INCONCLUSIVE**（**禁止**在欠计存量上出 PASS）；⑦ `measured=` 增 `headReadOk=` / `headReadFail=`，并加 `NOTE|H7|head-read-incomplete` 与 `head-read-accounting`（自证 `headReadOk + headReadFail == headMembers`）；⑧ 起跑清扫：只删「同模式 ∧ 文件 >10 分钟 ∧ 其内嵌 pid 已不存在」的陈旧件。

**五臂读数（全部现采，窗口/HEAD/pid 并列）**

| 臂 | 命令 | 窗口 | HEAD | 结果 |
|---|---|---|---|---|
| 默认面 | `-File shell/tools/evidence-hygiene-check.ps1` | 02:20:08.338→02:22:03.587（pid 2156） | `bd44994` | `PASS 10/10/0/0`；`headMembers=77&#124;headReadOk=77&#124;headReadFail=0`；`stockSource=committed(HEAD)`；`stockAstral/Fe0f=44/49`；EXIT=0 |
| 默认面+清扫 | 同上 | 02:22:28.584→02:24:25.225（pid 4060） | `bd44994` | `PASS 10/10/0/0` + `NOTE&#124;H7&#124;stale-temp-swept&#124;count=1`（清掉 09-12 23:40 的 pid-已死残留 12,893 B）；EXIT=0 |
| 反向臂 | `-EmojiAstralBaseline 43` | 02:28:58.432→02:30:52.910（pid 8756） | `0029bab` | `FAIL 10/9/1/0`；`newAstral=1`；`headReadFail=0`；EXIT=1 |
| 破坏读·**改后** | 同源副本把 `git cat-file blob` 换成不存在子命令 | 02:24:32.781→02:26:27.731（pid 13308） | `0029bab` | `INCONCLUSIVE 10/9/0/1`；`skipped=77&#124;headReadOk=0&#124;headReadFail=77`；`stockSource=worktree(reason=head-read-incomplete:77/77)`；`NOTE&#124;H7&#124;head-read-incomplete&#124;count=77`；EXIT=0 |
| 破坏读·**改前** | 从 `HEAD:…` 取回改前 blob 后做**同样**破坏 | 02:26:44.066→02:28:42.293（pid 3672） | `0029bab` | **`PASS 10/10/0/0`**；`skipped=0`；`stockAstral=0&#124;stockFe0f=0`；`newAstral=0`；EXIT=0 ⇒ **静默欠计 + 假绿**（存量被读成 0 时任何基线都不可能判红） |
| `finally` 正控 | 同源副本在第 40 个成员后 `throw` | 02:31:02.785 起（pid 13108） | `7191a88` | 脚本以 rc=1 终止；运行前 `%TEMP%` 残留 0、运行后 **0** ⇒ finally 生效 |

**边界（必须同引）**：① **硬杀/断电不执行 finally** ⇒ 靠下一轮起跑清扫兜底（上表第 2 行即其实证）；② H7 判 INCONCLUSIVE 时**整轮 exit code 仍为 0**（门禁既有约定）⇒ 引用必须读 `verdict=`，不得只看退出码；③ 清扫只动「可证明已死」的件，绝不碰活并发跑的临时件。

**顺带纠正一条判据（当刻实测）**：原「无并发」判据（命令行含 `evidence-hygiene-check` 的进程命中 = 0）**会自匹配** —— 在无任何门禁运行时按该判据采样仍命中 2 个，全部是**采样命令自身的包装进程**（node runner + powershell `-Command`，且都在采样者的祖先链上）。可用形态：锚定真实调用形（`-File <仓库绝对路径>…evidence-hygiene-check.ps1`）**且排除采样者 pid 及其祖先链**；更稳的是以窗口输出中的唯一 `runId` 与 `H7-TEMP` 为**必要条件**（由门禁自身产生，不受采样者命令行污染）。

## 12.5.8 转交材料入册：harness 六条 + 单次读库内 blob 判据（来源 kernel2 / reviewer / ui3；2026-09-13 verifier 复现）

**来源与边界**：内容归属 `kernel2`（六条实测）与 `reviewer`+`ui3`（单次读判据）；由 `reviewer` 转交；`verifier` 复现其中可复现项。**未独立复现**的项一律标注，不当作本团队共识。

### A. harness 使用纪律（kernel2 实测，reviewer 转交）
1. 宿主 exe 不得改名（WinUI 资源按模块名解析 ⇒ 改名后静默秒退、连日志目录都不建）。— **未独立复现**（需起窗，属 kernel2 面）。
2. 沙箱环境变量必须显式给子进程（`ProcessStartInfo.EnvironmentVariables[…]`；纯 `$env:X` + `Start-Process` 在本宿主不继承）。— **未独立复现**。
3. `KERNEL-FORK` / `KERNEL-SELF` 身份行须两行合取，且按正确字段匹配（按 `sha12=` / `--title` 各匹配一次会各白等 120 s）。— **半证实**：`KERNEL-SELF … sha12=` 库内 **3 条**成立（例 `KERNEL-SELF path=…\AIPlayer.MpvHost.dll sha12=CF0BFC9145C5 pid=18976`）；但以 `KERNEL-FORK` 开头的行库内 **0 条**（48 个文件只是散文式提及）⇒ 该行形状需一次 harness 起窗才有原始行。
4. `Select-Object -First N` 会杀掉上游子进程 ⇒ harness 末尾写证据的动作跑不到。— **已复现**：同一条 `cmd`（打印 3 行 + 末尾写文件）截断臂 ⇒ 证据文件**未生成**；`| Out-Null` 臂 ⇒ 生成。
5. 读文本必须钉编码：`(Get-Content <utf8 件>).Count` 在本机（ANSI=GBK）会吃掉双字节后的 `\n`、静默少报。— **已复现且更强**：拿本件自身量 —— 默认编码 **919 行** vs `-Encoding UTF8` **1,502 行** vs `ReadAllLines(UTF8)` **1,502 行** ⇒ 少报 **583 行（38.8%）**；且本机 `chcp` = **65001** 仍不救 ⇒「控制台是 UTF-8」是**假信心**。
6. PS 变量名大小写不敏感。— **已复现**：`$l=5; $L=7` ⇒ `$l`=7；`$K = UTF8Encoding` 之后 `$k = 3` ⇒ `$K.GetType().Name` = **Int32**（编码对象被静默换成整数）。

### B. 「单次读 + 进程内算库内 blob」（reviewer + ui3 提案，verifier 复现）
一次 `ReadAllBytes` 得字节数组 ⇒ 用它算 sha256_12、行尾统计、末字节，并用**同一份字节**算库内对象 sha1（`SHA1("blob <len>\0" + bytes)`）与 `git rev-parse HEAD:<path>` 比对，三态：`raw`（盘上字节即库内对象，`-text` 族）/ `cr-stripped`（`text=auto eol=lf` 族，库内 = 盘上减全部 CR）/ `MISMATCH`（在途态）。
- 复现分母：`kernel/src` 下 `*.cs` = **121 件**（提案记 120），**全部 CR>0 且全部 `cr-stripped`（121/121）**，dirty = 0。
- `raw` 的活实例在 `-text` 证据面（两件都在 `kernel/evidence/` 下；此处按 H2b 书写规则写裸名，以免被计成引用）：`t53-k8-sub-bogus-pre.txt`（CR 308）与 `t69-tracklist-post-sub-bogus.txt`（CR 422）**raw == HEAD，2/2**。
- 提案里「LF-only 件为 raw」在 `kernel/src` **无实例**（clean ∧ CR=0 = 0 件）；「在途件 `PlayerViewModel.cs` = MISMATCH」在当刻已被提交覆盖（porcelain 空 / CR 8,011 / `cr-stripped`）。
- **补充的判别力反控**：同一分类器未改动 ⇒ `cr-stripped`；**内存里翻 1 个字节 ⇒ `MISMATCH`** ⇒ 分类器可失败、不退化。
- 结构性好处（采纳理由）：**一次读** ⇒「worktree blob == HEAD blob」不再依赖两次采样时刻对齐。

### C. 棘轮型判据的反控臂纪律
反控臂必须写成「**当刻实测提交存量 − 1**」并**每次重采**：判据是 `new = Max(0, stock − baseline)`，**基线 ≥ 存量时恒为 0**（假安全）。实例（2026-09-13）：02:12 判别对（相隔 101 ms 真并发）`-EmojiAstralBaseline 45 ⇒ PASS(newAstral=0)`、`43 ⇒ FAIL(newAstral=1)`；而 21:35 那刻存量 46/50，故同一个 `45` 那时有效、现在失效。

## 12.5.9 并发快照谓词：硬化版 + 正控（captain 2026-09-13 逐字采纳；verifier 实测坐实假阴性与假阳性）

**为什么单列**：所有「该窗口无并发门禁」的结论都建立在这个谓词上，而它是**阴性结论仪器** ⇒ 必须与其它判据过同一套（枚举面 + 分母 + 采样时刻 + **正控**）。

**标准片段（逐字）**
```powershell
$gates = @(Get-CimInstance Win32_Process -Filter "Name='powershell.exe' OR Name='pwsh.exe'" |
  Where-Object { $_.ProcessId -ne $PID -and
                 $_.CommandLine -match '-ExecutionPolicy\s+Bypass\s+-File\s+"?[^"]*evidence-hygiene-check\.ps1' -and
                 $_.CommandLine -notmatch '\s-Command\s' })
"real_gates=$($gates.Count)"; $gates | ForEach-Object { "pid=$($_.ProcessId) start=$($_.CreationDate)" }
```

**两个坑（2026-09-13 verifier 实测；方向相反）**

| 坑 | 形态 | 实测 |
|---|---|---|
| **假阴性（更危险）** | `-File\s+\S*evidence-hygiene-check\.ps1` 的 `\S*` **不跨含空格的引号路径** | 字符串级：相对路径形 `True`、`"E:\AI Player\…"` 形 **`False`**；**活体级**：让真门禁以 `-File "E:\AI Player\shell\tools\evidence-hygiene-check.ps1"` 跑（pid 13736，`GATE-START` 02:44:40.837，runId `13736-1789238677758`）⇒ 同一刻 naive 谓词 **real_gates=0** ⇒ 会给出「无并发」的**假绿** |
| **假阳性** | 观察者自身的 `-Command "…"` 包装命令行也含脚本名（甚至含匹配用的正则字面本身） | 与 `ui3` 的实测互证（naive=5 / robust=2）；我另一轮实测命中 2 个，**全部**是采样命令自身的包装进程（node runner + `powershell -Command`），且都在采样者的祖先链上 |

**三重谓词同刻对照（同一刻、同一枚举面 `Name in (powershell.exe, pwsh.exe)`）**

| 谓词 | 同刻 `real_gates` | 说明 |
|---|---|---|
| V1 = `-File\s+\S*evidence-hygiene-check\.ps1`（+ `pid≠$PID`） | **0** | 假阴性：真门禁在跑却读 0 |
| V2 = 上面标准片段（逐字，含 `-notmatch '\s-Command\s'`） | **1** | 命中真门禁 pid 13736（parent 18552） |
| V3 = V2 + 排除采样者自身 pid 及其**祖先链** | **1** | 本轮与 V2 同值 |

**负控**：另起一个**只提及脚本名**的观察进程（`powershell -NoProfile -Command 'Start-Sleep -Seconds 25; $null = "evidence-hygiene-check.ps1"'`）⇒ V1/V2/V3 **都不计数**它 ✓（`-File` 要求 + `-notmatch '\s-Command\s'` 共同起作用）。
**正控**：真门禁（引号 + 绝对路径形）被 V2/V3 **命中** ✓。

**为什么仍建议加"祖先链排除"（即 V3）**：V2 只在观察者自己的命令行**不含该字面模式**时才安全；若观察者是通过外层包装（harness / runner / `.cmd` 转手）启动门禁的，那层包装的命令行同样含 `-ExecutionPolicy Bypass -File "…"` ⇒ V2 会把包装算成一个「门禁」，V3 不会。**V2 与 V3 本轮同值只是本轮字面形态使然，不是普遍结论。**

**引用规则（与本谓词配套，缺一不可）**：窗口「无并发」⇔ ①以 V3（或 V2 + 说明）在**窗口起止各采一次**均得 `real_gates=0`；**且** ②窗口输出含本 run 的唯一 `runId` 与 `H7-TEMP`。②由门禁自身产生、不受采样者命令行污染，应作**必要条件**；①只作辅助（快照法盲区 = 中段起停的短命门禁，必要时用门禁自写心跳锁覆盖）。
