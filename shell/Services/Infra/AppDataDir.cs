// 等价移植：rebuild/ai_player/lib/core/storage/app_paths.dart（Dart `AppPaths`）。
// 映射依据：DESIGN §4.1 #3 `app_data_dir.dart` → `Services/Infra/AppDataDir.cs`。
//
// **数据根（事实 70 / 事实 35 扩展裁决，2026-09-11）**：
//   - 便携模式：exe 同级存在 `data/` ⇒ 用它（绿色版，与原版一致）
//   - 否则：**`%LOCALAPPDATA%\AIPlayer`** —— 与**原版 Flutter 外壳**（`reversed/FlutterApp/strings_all.txt:23441`
//     起的 `ai_player_*` 键与 `:23314 accounts.json` 所在根）以及**内核**（`reversed/MpvHost/WinUISample/App.cs:37`
//     `AppDataRoot = LocalApplicationData\AIPlayer`）一致。
//   - 旧根 `%APPDATA%\AIPlayer`（Roaming）保留为 **`LegacyRoot`，仅作兼容读**：我们早期版本写过的
//     `servers.json` / `credentials.bin` 等不能变孤儿（`MigrateLegacyDataIfNeeded()` 做 copy-if-absent 迁移，
//     与内核自己的 `MigrateDataIfNeeded()`（`App.cs:942-976`）同机制：只复制、不改动源）。
//
// 诚实边界：`accounts.json` 是**原版**数据文件，本类只给出路径；读取与解释在 `ServerConfigStore`，
// 且**只读、绝不回写**。
//
// 注意：本类有成员方法 `File(string)`，故本文件内一律写 `System.IO.File.*`（裸 `File` 会解析到该方法）。

using System;
using System.Collections.Generic;
using System.IO;

namespace AIPlayer.Shell.Services.Infra;

/// <summary>应用数据目录解析（对应 Dart <c>AppPaths</c>）。</summary>
public sealed class AppDataDir
{
    /// <summary>服务器清单文件名（**唯一归属处**：生产代码一律走 <see cref="ServersFile"/> / <see cref="File"/>，不要散落字面量）。</summary>
    private const string ServersFileName = "servers.json";

    /// <summary>应用数据目录名。</summary>
    public const string AppFolderName = "AIPlayer";

    /// <summary>迁移完成标记（与内核 `.migrated_player` 同机制：幂等、只复制不移动）。</summary>
    public const string MigratedMarker = ".migrated_shell";

    /// <summary>原版服务器列表文件名（`reversed/FlutterApp/strings_all.txt:23314`）。</summary>
    public const string OriginalAccountsFileName = "accounts.json";

    /// <summary>
    /// 数据根**显式覆盖**的环境变量名 —— **与内核侧 t106（`kernel/src/WinUISample/App.cs:37`
    /// `AppDataRootOverrideEnvVar`）同名同义**，两侧必须逐字一致（卡 t109/A6：联合反控的前提）。
    /// </summary>
    public const string RootOverrideEnvVar = "AIPLAYER_APPDATA_ROOT";

    /// <summary><see cref="RootSource"/>：显式覆盖生效。</summary>
    public const string RootSourceOverride = "override";

    /// <summary><see cref="RootSource"/>：exe 同级 `data\` 便携根生效。</summary>
    public const string RootSourcePortable = "portable";

    /// <summary><see cref="RootSource"/>：默认根（`%LOCALAPPDATA%\AIPlayer`）生效。</summary>
    public const string RootSourceDefault = "default";

    /// <summary><see cref="RootSource"/>：非法覆盖值被拒（前缀后接 reason，与内核 <c>AppDataRootSource</c> 同形）。</summary>
    public const string RootSourceRejectedPrefix = "rejected:";

    private static AppDataDir _instance;
    private static readonly object Gate = new object();

    /// <summary>**本次**根解析中发生拒绝时的判据行（`[ROOT-OVERRIDE-REJECTED] …`）；未发生（含未设变量）为 <c>null</c>。</summary>
    public static string LastRootOverrideRejection { get; private set; }

    /// <summary>被拒原因（词表与内核 t106 同源：`not-absolute` / 异常类型名），供 <see cref="RootSource"/> 拼 `rejected:&lt;reason&gt;`。</summary>
    private static string _lastRejectReason;

    /// <summary>覆盖生效时的本机数据根（见 <see cref="LocalRoot"/>）；为 <c>null</c> = 按环境现算。</summary>
    private readonly string _localRoot;

    public AppDataDir(string root, bool isPortable, string legacyRoot = null, bool canAutoMigrate = false,
                      string rootSource = null, string localRoot = null)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        IsPortable = isPortable;
        LegacyRoot = legacyRoot ?? DefaultLegacyRoot();
        CanAutoMigrate = canAutoMigrate;
        RootSource = rootSource ?? (isPortable ? RootSourcePortable : "explicit");
        _localRoot = localRoot;
    }

    public static AppDataDir Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Gate)
            {
                return _instance ??= Resolve();
            }
        }
    }

    /// <summary>测试/多实例：显式替换根目录。</summary>
    public static void Override(AppDataDir dir)
    {
        lock (Gate)
        {
            _instance = dir;
        }
    }

    /// <summary>测试辅助：恢复为按环境重新解析。</summary>
    public static void Reset()
    {
        lock (Gate)
        {
            _instance = null;
        }
    }

    /// <summary>当前数据根（生产环境 = <c>%LOCALAPPDATA%\AIPlayer</c>；便携模式 = <c>exe\data</c>）。</summary>
    public string Root { get; }

    /// <summary>与内核/原版一致的本机数据根（<c>%LOCALAPPDATA%\AIPlayer</c>，与便携模式无关）。
    /// 内核把 `danmaku-cache`、`player\settings.json`、`logs`、`mpv` 都写在这里。
    /// **显式覆盖生效时跟随覆盖值** —— 内核是外壳的子进程、继承同一环境变量 ⇒ 两侧必须指向同一棵
    /// `danmaku-cache`/`player` 树，否则本属性会指到真实根而内核写在沙箱里（卡 t109/A6）。</summary>
    public string LocalRoot => _localRoot ?? Path.Combine(LocalApplicationDataRoot(), AppFolderName);

    /// <summary>根解析来源：<c>override</c> / <c>portable</c> / <c>default</c> / <c>rejected:&lt;reason&gt;</c> /
    /// <c>explicit</c>（显式构造的实例）。对应内核 <c>AppDataRootSource</c>，是 A1/A2 反控的判据字段。</summary>
    public string RootSource { get; }

    /// <summary>旧 Roaming 数据根（<c>%APPDATA%\AIPlayer</c>）——**仅兼容读**。</summary>
    public string LegacyRoot { get; }

    public bool IsPortable { get; }

    /// <summary>是否允许自动执行旧根迁移（仅由 <see cref="Resolve"/> 置真；显式构造/测试为假）。</summary>
    public bool CanAutoMigrate { get; }

    private static string LocalApplicationDataRoot()
    {
        var local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (string.IsNullOrEmpty(local))
        {
            try { local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); }
            catch (System.Security.SecurityException) { /* 有意忽略：受限环境读不到已知文件夹 => 下面用 %TEMP% 兜底 */ }
        }
        if (string.IsNullOrEmpty(local)) local = Path.GetTempPath();
        return local;
    }

    private static string DefaultLegacyRoot()
    {
        var roaming = Environment.GetEnvironmentVariable("APPDATA");
        if (string.IsNullOrEmpty(roaming))
        {
            try { roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); }
            catch (System.Security.SecurityException) { /* 有意忽略：受限环境读不到已知文件夹 => 返回 null（视为无旧根） */ }
        }
        return string.IsNullOrEmpty(roaming) ? null : Path.Combine(roaming, AppFolderName);
    }

    private static AppDataDir Resolve()
    {
        // ① 显式覆盖（与内核 t106 同名同优先级）：设了就采纳；非法 ⇒ 显式拒绝后回落默认根。
        var overrideRoot = TryResolveOverrideRoot();
        if (overrideRoot != null)
        {
            // 覆盖根**自洽**：不把用户真实 Roaming 旧数据搬进来、也不在里面生成"原版备份"噪声（沙箱必须可断言自己的全部内容）
            // ⇒ 迁移与备份闸门关（`canAutoMigrate: false`）。
            var over = new AppDataDir(overrideRoot, isPortable: false, canAutoMigrate: false,
                                      rootSource: RootSourceOverride, localRoot: overrideRoot);
            over.LogRootResolution();
            return over;
        }

        // ② 便携根：exe 同级存在 `data\` ⇒ 用它（绿色版，与原版一致）
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                var exeDir = Path.GetDirectoryName(exePath);
                if (!string.IsNullOrEmpty(exeDir))
                {
                    var portable = Path.Combine(exeDir, "data");
                    if (Directory.Exists(portable))
                    {
                        var portableDir = new AppDataDir(portable, true, canAutoMigrate: true, rootSource: RootSourcePortable);
                        portableDir.LogRootResolution();
                        return portableDir;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException) { /* 有意忽略：portable 配置解析失败 => 退回 LOCALAPPDATA */ }

        // ③ 默认根：`%LOCALAPPDATA%\AIPlayer`（顺序见 LocalApplicationDataRoot：环境变量 → SpecialFolder → %TEMP%）
        var root = Path.Combine(LocalApplicationDataRoot(), AppFolderName);
        if (!Directory.Exists(root))
        {
            try { Directory.CreateDirectory(root); }
            catch (Exception)   // 有意忽略：建根失败不在此抛 => 由后续读写暴露真实错误（不改变启动流程）
            {
            }
        }
        var resolved = new AppDataDir(root, false, canAutoMigrate: true,
                                      rootSource: _lastRejectReason == null
                                          ? RootSourceDefault
                                          : RootSourceRejectedPrefix + _lastRejectReason);
        resolved.LogRootResolution();
        resolved.MigrateLegacyDataIfNeeded();
        resolved.BackupPreExistingForeignFiles();
        return resolved;
    }

    /// <summary>
    /// 解析 <see cref="RootOverrideEnvVar"/>。返回 <c>null</c> = 没有可用的覆盖（未设/空白，或非法已被拒）。
    /// 判定与内核 t106 逐条对齐：**非空 → 绝对 → 建得出且可写**（`.write-probe-&lt;pid&gt;` 探针自证后删除）才采纳，
    /// 否则记 reason 并落一行显式 <c>[ROOT-OVERRIDE-REJECTED]</c>。
    /// 比内核多一条严格性：`C:foo` 这类**盘相对**路径 <c>IsPathRooted</c> 为真、却按当前工作目录解析 ⇒ 以
    /// <c>not-fully-qualified</c> 拒（该口径差异已在 t109 证据里点名，待两侧对齐）。
    /// 处置选**回落默认根**而非拒启动：拼错的环境变量不该让播放器起不来（与内核 t106 同一裁定）；且不静默——
    /// stderr + 进程内静态 + 解析出的根日志三处留痕（见 <see cref="RejectOverride"/> / <see cref="LogRootResolution"/>）。
    /// </summary>
    private static string TryResolveOverrideRoot()
    {
        _lastRejectReason = null;
        LastRootOverrideRejection = null;   // 清陈旧：本字段语义 = "本次解析是否发生拒绝"（否则上一格的拒绝行会被后代格重复落日志）
        var raw = Environment.GetEnvironmentVariable(RootOverrideEnvVar);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = raw.Trim();
        string reason = null;
        if (!Path.IsPathRooted(value))
        {
            reason = "not-absolute";
        }
        else if (!Path.IsPathFullyQualified(value))
        {
            reason = "not-fully-qualified";
        }
        else
        {
            try
            {
                Directory.CreateDirectory(value);
                var probe = Path.Combine(value, $".write-probe-{Environment.ProcessId}");
                System.IO.File.WriteAllText(probe, "ok");
                System.IO.File.Delete(probe);
            }
            catch (Exception ex)   // 有意不吞：任何异常类型名都作为 reason 落行（与内核 t106 同词表），随后回落默认根
            {
                reason = ex.GetType().Name;
            }
        }
        if (reason == null) return value;
        RejectOverride(value, reason);
        return null;
    }

    /// <summary>拒绝非法覆盖值：进程内静态（同进程可断言）+ stderr（日志就绪前唯一可见通道，与内核 t106 **同一行格式**）。</summary>
    private static void RejectOverride(string value, string reason)
    {
        _lastRejectReason = reason;
        var line = $"[ROOT-OVERRIDE-REJECTED] env={RootOverrideEnvVar} value={value} reason={reason} action=fallback-to-default";
        LastRootOverrideRejection = line;
        try { Console.Error.WriteLine(line); }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException) { /* 有意忽略：GUI 进程无 stderr（未重定向）时不得影响启动 */ }
    }

    /// <summary>把本次解析结果落进**解析出的根自己**的日志（<c>&lt;root&gt;\logs\aiplayer.log</c>）：一行 <c>APPDATA-ROOT …</c>
    /// 判据行；发生拒绝时再落一行拒绝原文（"不静默"的持久通道）。</summary>
    private void LogRootResolution()
    {
        var legacy = string.IsNullOrEmpty(LegacyRoot) ? "none" : LegacyRoot;
        Log($"APPDATA-ROOT root={Root} source={RootSource} portable={IsPortable} legacy={legacy}");
        if (LastRootOverrideRejection != null) Log(LastRootOverrideRejection);
    }

    /// <summary>
    /// 首次运行前，把**可能被我们覆盖的原版同名文件**做一次性备份（`&lt;name&gt;.original-&lt;时间戳&gt;.bak`）。
    /// 幂等（标记 <c>.&lt;name&gt;.pre-original</c>）；只复制、不改动原文件；返回备份数。
    /// 动机：根对齐到 Local 后，我们的 `settings.json` 与原版 Flutter 外壳的 `settings.json` **同路径同文件名**
    /// （本机实测原版为 2,117 B / 58 键），不备份就会在第一次写设置时静默覆盖用户原版数据。
    /// </summary>
    public int BackupPreExistingForeignFiles()
    {
        if (!CanAutoMigrate) return 0;
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backed = 0;
        if (!Directory.Exists(Root)) return 0;

        foreach (var name in ForeignNameRiskFiles)
        {
            var src = Path.Combine(Root, name);
            if (!System.IO.File.Exists(src)) continue;
            var marker = Path.Combine(Root, "." + name + ".pre-original");
            if (System.IO.File.Exists(marker)) continue;
            try
            {
                System.IO.File.Copy(src, Path.Combine(Root, name + ".original-" + stamp + ".bak"));
                System.IO.File.WriteAllText(marker, DateTimeOffset.Now.ToString("O"));
                backed++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* 有意忽略：备份失败不得阻断启动（不写标记 => 下次仍尝试） */ }
        }
        return backed;
    }

    // ── 旧根兼容（事实 70 裁决 1）────────────────────────────────────────────

    /// <summary>会被迁移的旧根文件（copy-if-absent，绝不移动/删除源文件）。</summary>
    private static readonly string[] LegacyFiles =
    {
        "settings.json", ServersFileName, "credentials.bin", "icons.json",
        "skip_segments.json", "mpv_host_skip_segments.json", "player_settings.json",
        "search_history.json", "audiobook_skip.json",
    };

    /// <summary>会被迁移的旧根目录（递归 copy-if-absent）。</summary>
    private static readonly string[] LegacyDirs = { "logs", "cache" };

    /// <summary>
    /// **可能与原版同名冲突**的文件（事实 70 副作用防护）：这些名字我们也会写，而原版 Flutter 外壳
    /// 在同一个 Local 根下同样使用它们（本机实测原版 `settings.json` 为 2,117 B / 58 键）。
    /// ⇒ 首次写入前先做一次性备份（`<name>.original-<时间戳>.bak` + `.&lt;name&gt;.pre-original` 标记），
    /// 避免把用户原版数据直接覆盖掉（原文件本体不动）。
    /// </summary>
    private static readonly string[] ForeignNameRiskFiles =
    {
        "settings.json", "search_history.json", "icons.json",
        "skip_segments.json", "mpv_host_skip_segments.json", "player_settings.json",
    };

    /// <summary>
    /// 把旧 Roaming 根下的数据**复制**到当前根（仅当目标不存在；幂等，标记 <c>.migrated_shell</c>）。
    /// 返回复制条目数；<b>不改动、不删除</b>旧根任何文件。
    /// <para>
    /// **t97（t84 的 F1）**：本方法以前**全程零日志**，调用方只拿到一个 <c>int</c> ⇒
    /// "复制失败（<c>failed</c>）"与"没什么可复制"在读数上**不可区分**，且"空迁移"会被读成"迁移过"。
    /// 现在**每次调用恰留一行** `MIGRATE-SHELL`，`reason` 六值把六条静默早退逐条点名，
    /// `copied/failed` 把两个静默失败面变成可观测量（本方法仍是**尽力而为、不阻断启动**）。
    /// </para>
    /// </summary>
    public int MigrateLegacyDataIfNeeded()
    {
        var reason = "done";
        var copied = 0;
        var failed = 0;
        var legacy = LegacyRoot;
        var marker = Path.Combine(Root, MigratedMarker);

        try
        {
            if (!CanAutoMigrate) { reason = "not-auto-migratable"; return 0; }
            if (string.IsNullOrEmpty(legacy)) { reason = "legacy-root-empty"; return 0; }
            if (string.Equals(Path.GetFullPath(legacy).TrimEnd('\\'), Path.GetFullPath(Root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) { reason = "same-root"; return 0; }
            if (System.IO.File.Exists(marker)) { reason = "marker-present"; return 0; }
            if (!Directory.Exists(legacy)) { reason = "legacy-missing"; return 0; }

            if (!Directory.Exists(Root)) Directory.CreateDirectory(Root);
            foreach (var name in LegacyFiles)
            {
                var src = Path.Combine(legacy, name);
                var dst = Path.Combine(Root, name);
                if (System.IO.File.Exists(src) && !System.IO.File.Exists(dst))
                {
                    try { System.IO.File.Copy(src, dst); copied++; }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        failed++;   // 有意忽略（不阻断其余文件）—— 但**必须计入 failed**（t97：静默失败改为可见读数）
                    }
                }
            }
            foreach (var dirName in LegacyDirs)
            {
                var srcDir = Path.Combine(legacy, dirName);
                if (!Directory.Exists(srcDir)) continue;
                foreach (var srcFile in Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories))
                {
                    var rel = srcFile.Substring(srcDir.Length).TrimStart('\\', '/');
                    var dstFile = Path.Combine(Root, dirName, rel);
                    if (System.IO.File.Exists(dstFile)) continue;
                    try
                    {
                        var parent = Path.GetDirectoryName(dstFile);
                        if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent)) Directory.CreateDirectory(parent);
                        System.IO.File.Copy(srcFile, dstFile);
                        copied++;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        failed++;   // 同上：单个文件复制失败 => 计入 failed，不阻断其余迁移
                    }
                }
            }
            try { System.IO.File.WriteAllText(marker, DateTimeOffset.Now.ToString("O")); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed++;   // 标记写失败 => 下次启动重试迁移；计入 failed（否则"迁移没落地"会被读成成功）
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            failed++;   // 迁移整体失败不得阻断启动（下次启动重试）；t97：不再静默
        }
        finally
        {
            // **每次调用恰一行**（含全部早退分支）：reason 六值 + 计数 + 两个根 + 标记态。
            // 放在复制循环之后 ⇒ 本行自身创建的 `<root>/logs/aiplayer.log` 不会扰动 copied（LegacyDirs 含 "logs"）。
            Log($"MIGRATE-SHELL reason={reason} copied={copied} failed={failed} legacy={legacy ?? "(空)"} root={Root} marker={(System.IO.File.Exists(marker) ? "present" : "absent")}");
        }
        return copied;
    }

    /// <summary>原版 <c>accounts.json</c> 的候选路径 —— **只读**。
    /// **当前数据根永远第一**；仅当本实例是按环境解析出来的（<see cref="CanAutoMigrate"/> 真，即生产路径）
    /// 才继续回落到内核同根与旧 Roaming 根。显式构造/测试实例（临时根、便携自包含）**只看自己的根**，
    /// 以免读到本机真实用户数据（跨上下文串数据）。</summary>
    public IReadOnlyList<string> OriginalAccountsFileCandidates()
    {
        var list = new List<string>();
        void Add(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return;
            var p = Path.Combine(dir, OriginalAccountsFileName);
            if (!list.Exists(x => string.Equals(x, p, StringComparison.OrdinalIgnoreCase))) list.Add(p);
        }
        Add(Root);
        if (CanAutoMigrate)
        {
            Add(LocalRoot);
            Add(LegacyRoot);
        }
        return list;
    }

    /// <summary>第一个存在的原版 <c>accounts.json</c>；都不存在返回 <c>null</c>（**只读，绝不写入**）。</summary>
    public string ExistingOriginalAccountsFile()
    {
        foreach (var candidate in OriginalAccountsFileCandidates())
        {
            if (System.IO.File.Exists(candidate)) return candidate;
        }
        return null;
    }

    // ── 文件 ────────────────────────────────────────────────────────────────

    public string File(string name) => Path.Combine(Root, name);

    public string SettingsFile => File("settings.json");

    public string ServersFile => File(ServersFileName);

    /// <summary>DPAPI 加密的凭据库。</summary>
    public string CredentialsFile => File("credentials.bin");

    /// <summary>自定义图标库。</summary>
    public string IconsFile => File("icons.json");

    public string SkipCacheFile => File("skip_segments.json");

    /// <summary>旧 Roaming 根下的 <c>servers.json</c>（**只读合并来源**；不存在或与当前根相同时为 <c>null</c>）——
    /// 事实 70 裁决 1 的兼容读面：我们早期版本写过的服务器条目不能变孤儿。</summary>
    public string LegacyServersFile
    {
        get
        {
            if (string.IsNullOrEmpty(LegacyRoot)) return null;
            var p = Path.Combine(LegacyRoot, ServersFileName);
            return string.Equals(p, ServersFile, StringComparison.OrdinalIgnoreCase) ? null : p;
        }
    }

    public string LogFile => Path.Combine(Root, "logs", "aiplayer.log");

    // ── 目录 ────────────────────────────────────────────────────────────────

    private string Dir(string name)
    {
        var d = Path.Combine(Root, name);
        if (!Directory.Exists(d))
        {
            try { Directory.CreateDirectory(d); }
            catch (Exception)   // 有意忽略：按需建目录失败 => 由调用方随后的写入暴露错误
            {
            }
        }
        return d;
    }

    /// <summary>播放内核所在目录（<c>AIPlayer.MpvHost.exe</c> 与 <c>libmpv-2.dll</c> 同目录）。</summary>
    public string PlayerDir => Dir("player");

    public string HostExe => Path.Combine(PlayerDir, "AIPlayer.MpvHost.exe");

    public string LibMpvDll => Path.Combine(PlayerDir, "libmpv-2.dll");

    /// <summary>mpv 配置目录（传给内核 <c>--mpv-config-dir=</c>）。</summary>
    public string MpvConfigDir => Dir("mpv");

    /// <summary>磁盘缓存（弹幕、歌词、图片、章节）。</summary>
    public string CacheDir => Dir("cache");

    public string LyricsCacheDir => Dir(Path.Combine("cache", "lyrics"));

    /// <summary>外壳侧弹幕缓存（原 <c>cache/danmaku</c> 语义，**与内核 Local `danmaku-cache` 不同**）。
    /// 事实 35 裁决 3（修正版）：此成员**保真不动**（原版 Dart 外壳自己也有这一份，见
    /// `rebuild/ai_player/lib/core/storage/app_paths.dart:70-71`）；内核布局用 <see cref="KernelDanmakuCacheDir"/>。</summary>
    public string DanmakuCacheDir => Dir(Path.Combine("cache", "danmaku"));

    /// <summary>与内核 <c>WinUISample.Services.DanmakuDiskCache</c> 对齐的弹幕缓存目录
    /// （<c>&lt;LocalRoot&gt;\danmaku-cache</c>，内核 `DanmakuDiskCache.cs:19/21/23`）。t14 的
    /// <c>DanmakuDiskCacheStore</c> 用它，且需与内核磁盘布局逐项对照。</summary>
    public string KernelDanmakuCacheDir => Path.Combine(LocalRoot, "danmaku-cache");

    /// <summary>备份输出目录（WebDAV 自动备份的本地暂存）。</summary>
    public string BackupDir => Dir("backup");

    /// <summary>应用运行日志（对应 Dart <c>AppPaths.log</c>）。</summary>
    public void Log(string message)
    {
        try
        {
            var f = LogFile;
            var parent = Path.GetDirectoryName(f);
            if (!string.IsNullOrEmpty(parent) && !Directory.Exists(parent)) Directory.CreateDirectory(parent);
            System.IO.File.AppendAllText(f, $"{DateTime.Now:O}  {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* 有意忽略：日志文件写失败不得影响主流程 */ }
    }
}
