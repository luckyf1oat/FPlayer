# t99【外壳日志 base64 认证头未脱敏】完成报告

卡：`t99`（`t89` 承接），attempt 1，`attempt_id=7e507d30-8f17-454e-b458-d80956b8c24f`。
原始读数全部在 **`shell/Tests/evidence/log-authheader-single-sink.txt`**（两轮：sink 落地前 / 落地后；本文件只做归属、结论与验收映射）。

## 1. 结论（三态）

| 态 | 内容 |
|---|---|
| **逆向到了** | 无（长期卫生缺陷：`aiplayer.log` 实测 **48 行裸 base64**，两条写路径各 24 行 —— `PLAY-KERNEL-ARGS-RAW <args>` 与 `KERNEL-LAUNCH exe=… args=…`） |
| **重建实现了** | 打码**唯一实现**下沉到服务层 sink（`services`：`Services/Logging/SecretMasking.cs` + `PlayerLogService.Add` 入口调用）；App 侧退化为转发（我）；两个真实写路径 + 内核日志面识别式全部接上 |
| **运行验证过了** | **是**：两轮真起窗（沙箱数据根）配对读数；第二轮四条反控全绿（含"绕过门面"那条），并含**起窗存活 + 反控落点**读数 |

## 2. 最终结构（单点 + 覆盖两条写路径 + 副本合并）

| 位置 | 角色 |
|---|---|
| `shell/Services/Logging/SecretMasking.cs` | **唯一实现**：5 类规则（`--http-header=` / `api_key=`（含 `%3D`）/ 认证头 / URL 查询串凭据键 / `password=`）+ 幂等守卫；值 → `<masked len=N sha4=XXXX>`；不抛，失败交回原因由 sink 留 `LOG-MASK-FAIL` 诊断行 |
| `shell/Services/Logging/DebugLog.cs:97` | **唯一 sink**：`PlayerLogService.Add` 入口先 `SecretMasking.Mask(message, out …)` ⇒ **环形缓冲与落盘都吃到** ⇒ 任何新调用点自动受益 |
| `shell/App/Infrastructure/SecretMasker.cs`（我） | **转发**到上面那份（`Mask`/`Describe`/`Sha4`/`MaskToken`）+ App 侧取证钩子（`SHELL_SELFTEST_LOGMASK=1`） |
| `shell/App/Program.cs:52`（`ui`，提交 `9b32126`） | `MaskSecrets` 转发 ⇒ 15+ 个既有调用点零改动受益 |
| `shell/App/KernelHost/KernelLauncher.cs:85`（我） | `KERNEL-LAUNCH … args=` 落日志前打码（该路径实测贡献 24 行） |
| `shell/App/Features/Aggregate/AggregatePlayback.cs:126`（我） | `AGG PLAY-KERNEL-ARGS-RAW` 同上 |
| `shell/App/KernelHost/KernelLogMasker.cs:37`（我） | 内核日志面识别式扩到 `--http-header=` + 认证头，替换统一委托唯一实现 |
| 窄正则副本 | 原 4 处：`Program.MaskSecrets`（已转发）、`Features/Search/{SearchLog,SearchRunner}`（我发请求给 `ui3`）、`shell/Spike/Probe.cs`（我发请求给 `ui`） |

## 3. 四条反控（逐条可 False；两轮对照见证据文件 §3 与 §6.4）

| 反控 | 判据 | 第二轮读数 |
|---|---|---|
| ① 门面路径 | `--http-header=` 值必须变 `<masked …>` | ✅ `<masked len=76 sha4=F073>` |
| ② 百分号编码 | `api_key%3D<值>` → `api_key%3D***` | ✅ |
| ③ **不该打码**（防打过头） | 条目 id / 服务器 id / 端口 / `--subtitle-track=3` 逐字保留 | ✅ 全在 |
| ④ **故意绕过门面**（直接 `DebugLog.Info`） | sink 必须兜住 | ✅（**第一轮为 ❌ 裸 base64**；下沉到 sink 后转绿 —— 这正是"逐点补漏不住"的证据） |

**计数**（第二轮）：含裸 base64 的行 = **0**；含未打码 `manual-api_key=` 的行 = **0**。

## 4. 构建 / 运行 / 卫生

- 构建：`-t:Rebuild` 隔离输出 `E:\ui2-bin\`，**2026-09-12 07:54:40** / HEAD `dfc9c5b` / **EXIT=0 / 0 错误 / 1216 警告**。
- 起窗预检：`list-instances -Samples 3` ⇒ `FREE-FIELD|verdict=FREE-OVER-WINDOW|allFree=True|spanSec=2.17`。
- 两次落点（新纪律：改 UI 面须有起窗存活读数 + 一个反控落点）：`favorites` pid=21712 @07:55:27 **alive9s=True / MainWindow ready=True**；`aggregate` pid=4104 @07:55:38 **alive9s=True / MainWindow ready=True**；`LEFT-MINE=none`。
- 沙箱：`%TEMP%\t99-sandbox`（LOCALAPPDATA/APPDATA 重定向；真实根 `settings.json` sha12 `E6220105E7CC` / mtime 06:29:23 未变）。
- 历史 48 行**不重写**（卡面 nonGoal）；`servers` 页当刻会崩 `0xC000027B`，本轮落点刻意不含它。

## 5. 证据

| 文件 | 身份 |
|---|---|
| `shell/Tests/evidence/log-authheader-single-sink.txt` | **12,929 B / 155 行 / sha12 `0E6BC38DA549`**（**三轮**原始行 + 计数 + 代际四件套）｜前 8,870 B 逐字节不变（前缀 sha12 仍 `299D31E4AC4F`）｜第三轮 = `t132` 落地后复测，追加见该件 §5 |
| 本文件 | `EVIDENCE_T99.md` |
