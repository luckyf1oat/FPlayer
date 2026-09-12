# t47 · 导入服务器「接管即写」（adopt-on-write）+ 墓碑 —— 服务层交付说明（给 `ui3` 一行接线）

> 背景（问题）：本机 16 台服务器**全部**来自原版 `accounts.json` 兼容读 ⇒ 按旧设计右键菜单的「修改密码 / 编辑 / 删除」**对每一台永久置灰** ⇒ 用户要求"功能全面、无漏洞"时这就是漏洞。
> 本件：让只读导入条目**第一次写操作时被显式接管**成外壳自有条目；删除只记**墓碑**。

## 1. 新 API（`shell/Services/Servers/ServerConfigStore.cs`）

| 成员 | 语义 |
|---|---|
| `bool IsReadOnlyServer(id)` | 仍是**只读导入**（未接管）⇒ **仅用于判断"是否先弹接管确认"**；🔴 **UI 三项不据此置灰**（最终口径见 §3） |
| `bool CanWriteServer(id)` | 已可写（自己添加的 / **已接管**）⇒ **只驱动 tooltip 文案 + 是否先弹确认**（不驱动 IsEnabled） |
| `bool Adopt(id)` | **接管**：把只读条目移出只读集合并写进我们自己的 `servers.json`（沿用原 `id`，**血缘 `accountsOrigin` 保留**，另加 `shellAdopted=true` + `adoptedAt`）。返回是否真的接管了 |
| `void AdoptAndUpdate(ServerConfig)` | **第一次写操作（编辑/改密码）**：接管 + 更新，一步到位 |
| `void AdoptAndRemove(id)` | **第一次写操作（删除）**：接管 + 删除（删除 = 记墓碑） |
| `static bool IsAdoptedEntry(ServerConfig)` | 该条目是否已接管（`Extra["shellAdopted"]`） |
| `IReadOnlyCollection<string> HiddenOriginalIds` / `bool IsHidden(id)` | 墓碑清单（`servers.json` 的 `hiddenOriginalIds` 键） |

## 2. 🔴 行为变更（UI 必须知道）

`Update(server)` 与 `Remove(id)` 对**未接管的只读条目**由"静默无效果"改为**抛 `InvalidOperationException`**：

```
拒绝写入：id=imp-1 是只读导入条目且尚未接管（先 Adopt(id) 或用 AdoptAndUpdate(server)）
拒绝删除：id=imp-3 是只读导入条目且尚未接管（先 Adopt(id) 或用 AdoptAndRemove(id)）
```
⇒ 这是**反控(d)** 要求的"接管必须显式发生"，不是把只读判定删掉。**但 UI 不得据此置灰**（见下）——请对 `InvalidOperationException` 做兜底提示。

## 3. 一行接线（给 `ui3` 的服务器页右键菜单）

```
SEAM⑤（最终口径，2026-09-12 Captain 裁定）：
  三项 IsEnabled 恒为 true（未接管的只读行也必须可点 —— 菜单内没有第二个"接管"入口，
  置灰会让用户无路可走：本机 16 台导入服务器会重新变成"全都改不了删不掉"）；
  CanWriteServer(id) 只用于两件事：
    ① tooltip 文案（未接管 / 已接管 两版）；
    ② 决定是否先弹"接管确认"。
  点击：未接管 ⇒ 弹接管确认 ⇒ Adopt(id)（+ Save，内部已落盘）⇒ 再执行该动作；
        已接管 ⇒ 直接执行。
  编辑/改密码 ⇒ _store.AdoptAndUpdate(edited)；删除 ⇒ _store.AdoptAndRemove(row.Id)；
  两者都 try/catch(InvalidOperationException) 兜底提示（未走确认路径时会被拦）。
  删除 = 接管 + 外壳侧墓碑（servers.json 的 hiddenOriginalIds）；accounts.json 一个字节不动。
```

🆕 **一般规则（Captain 2026-09-12 入台账）**：**凡"置灰"某个入口，必须同时存在另一条使它能用的路径；否则置灰 = 功能缺失。**

## 4. 红线与实测（自检 S9①–④，全绿）

🔴 **`accounts.json` 全程一个字节都不许写**：四条步都在改前/改后用 `SHA256` 比对 **同一路径**，全部 `不变=True`；`servers.json` 是本组件唯一写入目标。

| 步 | 判据 | 实测读数 |
|---|---|---|
| S9① | **反控(d)**：未接管的只读行 写入/删除 必须被拦 | `Update 被拦=True Remove 被拦=True｜名字未被改=True｜imp-3 仍在=True｜servers.json 未含 imp-1/imp-3=True｜accounts.json sha 不变=True` |
| S9② | (a) 编辑 ⇒ `servers.json` 出现该条目（含血缘） | `Adopt 生效=True｜只读=False｜落盘 name=改过的名字｜血缘 IsOriginalAccountEntry=True 已接管标记=True｜accounts.json sha 不变=True` |
| S9③ | (b) 接管后写操作**重启仍在** | `重启后名字=改过的名字｜同 id 条数=1（不重复）｜仍只读=False` |
| S9④ | (c) 删除 ⇒ 可见集合消失 + **重启不复活** | `删除后可见=False｜墓碑（内存/重载后）=True/True｜servers.json 含 hiddenOriginalIds=True｜重载后可见=2 台｜accounts.json sha 不变=True｜原件仍 3 条=True` |

自检步数 **57 → 61**（新增 S9①–④）；`ServiceSelfCheckTests` 通过；`dotnet build shell/Services/Services.csproj -c Debug` 0 警告 0 错误。

## 5. 实现要点（供复核）

- **接管沿用原 `id`** ⇒ 下次 `Load` 时 `LoadOriginalAccounts()` 因"同 id 已在 servers.json"而跳过原版那条 ⇒ **不会出第二份**（S9③ 实测同 id 条数 = 1）。
- **墓碑**落在 `servers.json` 的 `hiddenOriginalIds` ⇒ `LoadOriginalAccounts()` 跳过墓碑 id ⇒ **删除后重启不复活**（S9④）。
- `Save()` 仍排除"仍在只读集合里的条目"，因此**未接管的导入条目永不落盘**（S9① 实测 servers.json 不含 imp-1/imp-3）。
- ⚠️ 断言坑（已记账）：`servers.json` 由 `JsonStorage` 写出时**非 ASCII 会被转义成 `\uXXXX`** ⇒ 验证"落盘内容"必须**解析 JSON 后比字段**，不能拿原文 `Contains("中文")` 判（否则得到假 FAIL）。
