// t65：User-Agent 的**唯一构造点**（用户要求「UA 默认为 Hills 支持自定义」；Hills = 原版外壳的品牌名）。
//
// 为什么必须只有一处：UA 有两个消费面 —— ① `ShellHttpClient` 的请求头；② `EmbyService.HostHttpHeaders`
// （经内核 `--http-header=` 影响 mpv 取流）。此前两边各写一份默认串（"AIPlayer/1.0 (rebuilt shell)" 与
// "AI Player/1.0.0"）⇒ **同一事实两个构造点**，改一处另一处会静默留旧值。现在两边都只读 `Current`。
//
// 默认值依据 = **实测**（不是印象）：原版内核二进制 `data\player\AIPlayer.MpvHost.exe` 对 loopback 的实发
// 请求头就是 `User-Agent: ai-player`（原始输出与命令见 `shell/Tests/evidence/t65-ua.txt`）。
// 与内核源码 `kernel/src/WinUISample.Models.Constants/ClientIdentity.cs:7`（默认 "ai-player"）一致。
//
// t88（captain 2026-09-12 终裁 (a)）：**热路径惰性读设置** —— 形态与纪律照抄 `ProxyPolicy`（t86 同款）。
// 🔴 为什么：`Current` 原先**只**由 `Apply()`（`SettingsService.Load/Save` 推）写入，而 App 的默认路径
// （`HomePage` 只**读属性** `SettingsService.Instance.Settings.LastServerId`）**从不调用 `Load()`**
// ⇒ 该进程里 `Current` 恒为常量默认 `ai-player` ⇒ **设置里配好的自定义 UA 静默失效**（t65 出口①/②/③ 全发 `ai-player`）。
//
// 优先级 = 本类**唯一权威定义**（自检 `S11⑦` 与证据 `t88-ua-lazy-load.txt` 的 A/B 读数按此判）：
//   ① `SetCurrent` —— **显式覆盖**（测试/多实例/取证），压过一切，直到 `FollowSettings()` 解锁；
//   ② `Apply`（`SettingsService.Load/Save` 推入）—— 覆盖当前值，**包括刚 `ResetToDefault()` 之后**
//      （否则"设置一改下一次请求即生效"这条契约会被一次重置永久掐死 —— `S11③/⑥` 就是这条的反控，实测抓到过）；
//   ③ `ResetToDefault` —— 当前值 = 常量 `Default`，**不粘住**（语义 = `Apply(空设置)`），不写盘、不改设置；
//   ④ ①②③ 都没定过值（或刚调过 `FollowSettings()`）⇒ **惰性**读 `SettingsService.Instance.Settings`：
//      未加载则自己 `Load()` 一次（`Load()` 幂等；它回调 `Apply` 只是把同一份设置推回来 ⇒ **不递归**）；
//   ⑤ 读设置失败 / 盘上值为空或非法 ⇒ 回落常量 `Default`（**绝不发空 UA**、启动路径不抛）。

using System;
using AIPlayer.Shell.Services.Logging;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Settings;

namespace AIPlayer.Shell.Services.Http;

/// <summary>User-Agent 策略：默认值、校验、以及全局唯一生效值。</summary>
public static class UserAgentPolicy
{
    /// <summary>
    /// 默认 UA（原版实测值）。改这个常量 = 改全局默认；**不要在别处再写一份默认串**。
    /// </summary>
    public const string Default = "ai-player";

    /// <summary>长度上限（防把超长串塞进头里；HTTP 头整体也有服务端侧上限）。</summary>
    public const int MaxLength = 256;

    private static volatile string _explicit;
    private static volatile bool _hasExplicit;
    private static volatile string _applied = Default;
    private static volatile bool _hasApplied;

    /// <summary>惰性加载设置的并发门（与 <see cref="ProxyPolicy"/> 同款：`Load()` 幂等，但仍加锁避免并发重入读文件）。</summary>
    private static readonly object LazyLoadGate = new object();

    /// <summary>
    /// 当前生效值（**唯一构造点**：所有出站 UA 都从这里取）。
    /// 优先级见类头注释：显式写入 → `Apply` 推入 → 惰性读设置 → 常量 <see cref="Default"/>。
    /// </summary>
    public static string Current
    {
        get
        {
            if (_hasExplicit) return Resolve(_explicit);
            if (_hasApplied) return Resolve(_applied);
            return FromSettings();
        }
    }

    /// <summary>是否已"定值"（①/② 发生过）；<c>false</c> ⇒ <see cref="Current"/> 会去读设置（自检/取证用）。</summary>
    public static bool IsPinned => _hasExplicit || _hasApplied;

    /// <summary>是否处于**显式**态（`SetCurrent` / `ResetToDefault`；自检/取证用）。</summary>
    public static bool HasExplicit => _hasExplicit;

    /// <summary>
    /// 规范化 + 校验：
    /// · <c>null</c>/纯空白 ⇒ <paramref name="normalized"/> = <see cref="Default"/>（**绝不发空 UA**），返回 true；
    /// · 含控制字符（含 <c>\r</c>/<c>\n</c>/<c>\t</c>）⇒ 返回 false + 可读 <paramref name="error"/>（可见拒绝）；
    /// · 超长 ⇒ 返回 false。
    /// </summary>
    public static bool TryNormalize(string raw, out string normalized, out string error)
    {
        normalized = Default;
        error = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            // 清空/纯空白 = 回落默认（用户要求；也保证任何路径都不会发出空 UA）
            return true;
        }

        if (raw.Length > MaxLength)
        {
            error = $"User-Agent 过长（{raw.Length} > {MaxLength}）";
            return false;
        }

        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c < 0x20 || c == 0x7F)
            {
                // 换行/回车会被 HttpClient 直接抛异常或造成**头注入**（同一请求里塞入额外头）⇒ 在入口拒绝，不留到发送时
                error = $"User-Agent 含非法头字符（控制字符 0x{(int)c:X2}，位置 {i}）；不允许换行/制表符等";
                return false;
            }
        }

        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            return true; // 全空白（含全角空格等）⇒ 同样回落默认
        }

        normalized = trimmed;
        return true;
    }

    /// <summary>宽松版：非法值 ⇒ 回落默认（用于"已在盘上的脏值"，不让历史数据把请求打挂）。</summary>
    public static string Resolve(string raw)
        => TryNormalize(raw, out var normalized, out _) ? normalized : Default;

    /// <summary>由设置喂入（<see cref="AIPlayer.Shell.Services.Settings.SettingsService"/> 在 Load/Save 时调用）。</summary>
    public static void Apply(AppSettings settings)
    {
        // 盘上脏值一律走 Resolve（回落默认），不抛：启动路径不能被一个坏设置打挂
        _applied = Resolve(settings?.UserAgent);
        _hasApplied = true;
    }

    /// <summary>测试/多实例用：直接设定生效值（值必须已通过 <see cref="TryNormalize"/>）。</summary>
    public static void SetCurrent(string normalized)
    {
        _explicit = string.IsNullOrEmpty(normalized) ? Default : normalized;
        _hasExplicit = true;
    }

    /// <summary>
    /// 回到默认（自检/反控用）：当前值 = 常量 <see cref="Default"/>，**不粘住** ——
    /// 之后任何一次 <see cref="Apply"/>（设置 Load/Save 推入）都能改回它。
    /// </summary>
    public static void ResetToDefault()
    {
        _hasExplicit = false;
        _explicit = null;
        _applied = Default;
        _hasApplied = true;
    }

    /// <summary>
    /// 回到"跟随设置"（**唯一解锁入口**）：清掉显式/推入态 ⇒ 下一次读 <see cref="Current"/> 重新读设置。
    /// 与 <see cref="ProxyPolicy.FollowSettings"/> 同款；显式 `SetCurrent`/`ResetToDefault` 之后若想再跟随设置，必须显式调用本方法。
    /// </summary>
    public static void FollowSettings()
    {
        // 日志取"解锁前"的值：若在这里读 `Current`，惰性读会顺手把 `Apply` 推回来（又变回"已定值"），
        // 反而让"解锁成功"这一步读不出来（自检 S11⑦ 要读 `IsPinned`）。
        var previous = _hasExplicit ? Resolve(_explicit) : _hasApplied ? Resolve(_applied) : Default;
        _hasExplicit = false;
        _explicit = null;
        _hasApplied = false;
        DebugLog.Info($"UA-POLICY follow-settings previous={previous} pinned={IsPinned}（下一次读 Current 重新读设置）");
    }

    /// <summary>
    /// 读**单例**设置里的 UA（③ 惰性路径）。
    /// 只在没有任何显式/推入值（③）时才会走到这里；`Load()` 是幂等的，且它回调的 `Apply` 不读设置 ⇒ 不递归。
    /// 这条路径**不写** `_applied`：取值靠当刻的 `SettingsService.Instance.Settings`，
    /// 而 `Load()` 自己已经把同一份设置推给了 `Apply`。
    /// </summary>
    private static string FromSettings()
    {
        try
        {
            var service = SettingsService.Instance;
            if (!service.IsLoaded)
            {
                lock (LazyLoadGate)
                {
                    if (!service.IsLoaded)
                    {
                        service.Load();
                        DebugLog.Info($"UA-POLICY lazy-load-settings file=settings.json loaded=true effective={Resolve(service.Settings?.UserAgent)}");
                    }
                }
            }
            return Resolve(service.Settings?.UserAgent);
        }
        catch (Exception ex)
        {
            // 取设置失败不得让请求面整体不可用：回落默认，但**留可见痕迹**（不静默）
            DebugLog.Warn($"UA-POLICY settings-read-failed {ex.GetType().Name} ⇒ 本次按默认 {Default} 处理");
            return Default;
        }
    }
}
