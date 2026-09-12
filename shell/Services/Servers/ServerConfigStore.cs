// 等价移植：rebuild/ai_player/lib/core/services/server_config_store.dart。
// 落盘结构：{ "servers": [ … ] }；**密码与令牌不落在 servers.json**，而在 DPAPI 凭据库（键见 ServerConfig.PasswordKey 等）。
// 映射依据：DESIGN §4.1 #33 → Services/Servers/ServerConfigStore.cs。
// 已实现 P1 零调用项：reorder() / passwordOf()（验收②「编辑服务器时可回填已存密码」）。
//
// **原版 accounts.json 兼容读（事实 70 裁决 2，2026-09-11）**：
//   原版外壳把服务器列表放在 **`%LOCALAPPDATA%\AIPlayer\accounts.json`**（串表实证：`strings_all.txt:23314`
//   `accounts.json`、`:23441` 起 `ai_player_*`），字段面（本机 16 条实测逐字段读出）：
//   `id, provider, name, username, lastKnownUserId, serverName, encryptedToken, rememberPassword,
//    encryptedPassword, lastLoginAt, lines[]{name,url,isMain}, preferredLineUrl, iconUrl, sortOrder,
//    useProxy, cached{Movie,Series,Episode,Music,Book,Artist,Album,Song,Playlist,Favorite}Count,
//    cachedLastPlayedAt, note, visibleLibraryIds, spotlightLibraryIds, libraryOrder`。
//   映射：`provider`→Kind、**`preferredLineUrl`→BaseUrl**（`lines[].url` 是不带 `/emby` 的主机名，仅作兜底）、
//   `name` 空则用 `serverName`、`lastKnownUserId`→UserId、`encryptedToken`→AccessToken（本机实测可直接当
//   Emby `api_key` 用，HTTP 200/17 个库）、`sortOrder`→SortIndex、`iconUrl`→IconUrl；`lines[]`/`cached*Count`/
//   `visibleLibraryIds`/`spotlightLibraryIds`/`libraryOrder`/`useProxy` 等**原样存进 `Extra` 只读保留**。
//   **只读、绝不回写**：`accounts.json` 永不写入，其条目也不会被 `Save()` 落到 `servers.json`
//   （否则"改名 accounts.json 应回到 0 台"的反控制会失效）。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using AIPlayer.Shell.Services.Credentials;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Logging;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.State;
using AIPlayer.Shell.Services.Storage;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.Servers;

/// <summary>服务器配置存储（对应 Dart <c>ServerConfigStore</c>）。</summary>
public sealed class ServerConfigStore : ChangeNotifierBase
{
    private static ServerConfigStore _instance;
    private static readonly object Gate = new object();

    private readonly JsonStorage _storage;
    private readonly SecureKvStore _credentials;
    private readonly List<ServerConfig> _servers = new List<ServerConfig>();
    private readonly HashSet<string> _fromOriginalAccounts = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// t47 **墓碑**：已删除的导入条目 id（落在 `servers.json` 的 `hiddenOriginalIds` 里）。
    /// 作用 = 删除后**重启不复活**（否则下次 `LoadOriginalAccounts` 会把它重新导入）。
    /// 🔴 只写我们自己的 `servers.json`，**绝不动 `accounts.json`**。
    /// </summary>
    private readonly HashSet<string> _hiddenOriginalIds = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>本机原版 `accounts.json` 路径来源（仅 <see cref="Instance"/> 使用；显式路径实例不读它）。</summary>
    private readonly bool _readOriginalAccounts;

    private readonly string _originalAccountsFile;

    /// <summary>旧 Roaming 根 <c>servers.json</c>（**只读合并来源**，永不写入）——事实 70 裁决 1。</summary>
    private readonly string _legacyServersFile;

    private bool _loaded;

    public ServerConfigStore(JsonStorage storage, SecureKvStore credentials, bool readOriginalAccounts = false, string originalAccountsFile = null, string legacyServersFile = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _readOriginalAccounts = readOriginalAccounts;
        _originalAccountsFile = originalAccountsFile;
        _legacyServersFile = legacyServersFile;
    }

    /// <summary>落盘目标路径（**恒定 = 构造时传入的 <c>servers.json</c>**；兼容读来源不影响它）。
    /// 供自检断言「写只写新根、绝不回写旧根/原版 accounts.json」。</summary>
    public string SaveTargetPath => _storage.FilePath;

    public static ServerConfigStore Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Gate)
            {
                return _instance ??= new ServerConfigStore(
                    new JsonStorage(AppDataDir.Instance.ServersFile),
                    SecureKvStore.Instance,
                    readOriginalAccounts: true,
                    // 传 null ⇒ 每次 Load 都按当前机器重新解析 accounts.json（文件被移走/放回都能反映）
                    originalAccountsFile: null,
                    legacyServersFile: AppDataDir.Instance.LegacyServersFile);
            }
        }
    }

    /// <summary>测试/多实例（独立文件与凭据库；默认不读原版 <c>accounts.json</c>、不合并旧根）。</summary>
    public static ServerConfigStore At(string serversFilePath, SecureKvStore credentials)
        => new ServerConfigStore(new JsonStorage(serversFilePath), credentials ?? SecureKvStore.At(serversFilePath + ".creds"));

    /// <summary>测试/多实例 + 显式指定原版 <c>accounts.json</c>（用于兼容读的双向反控制）。</summary>
    public static ServerConfigStore At(string serversFilePath, SecureKvStore credentials, string originalAccountsFile)
        => new ServerConfigStore(
            new JsonStorage(serversFilePath),
            credentials ?? SecureKvStore.At(serversFilePath + ".creds"),
            readOriginalAccounts: !string.IsNullOrEmpty(originalAccountsFile),
            originalAccountsFile: originalAccountsFile);

    /// <summary>测试/多实例 + 显式旧根 <c>servers.json</c>（用于旧根合并的双向反控制）。</summary>
    public static ServerConfigStore At(string serversFilePath, SecureKvStore credentials, string originalAccountsFile, string legacyServersFile)
        => new ServerConfigStore(
            new JsonStorage(serversFilePath),
            credentials ?? SecureKvStore.At(serversFilePath + ".creds"),
            readOriginalAccounts: !string.IsNullOrEmpty(originalAccountsFile),
            originalAccountsFile: originalAccountsFile,
            legacyServersFile: legacyServersFile);

    /// <summary>原版 <c>accounts.json</c> 最近一次读取的显式错误（无错误为 <c>null</c>）。
    /// 供调用方/自检断言「坏数据必须报错」，而不是解析 0 条就当通过。</summary>
    public string OriginalAccountsError { get; private set; }

    /// <summary>原版 <c>accounts.json</c> 实际读取到的条目数（0 表示没有该文件或读取失败）。</summary>
    public int OriginalAccountsLoaded { get; private set; }

    /// <summary>从旧 Roaming 根合并进来的条目数（只读来源；写入仍只写当前根）。</summary>
    public int LegacyServersLoaded { get; private set; }

    /// <summary>该 id 是否来自原版 <c>accounts.json</c>（**只读条目**：改动不会回写原文件，也不会存进 servers.json）。</summary>
    public bool IsReadOnlyServer(string id) => !string.IsNullOrEmpty(id) && _fromOriginalAccounts.Contains(id);

    /// <summary>当前是否**可写**（自己添加的 / 已接管的）—— 与 <see cref="IsReadOnlyServer"/> 互补。</summary>
    public bool CanWriteServer(string id) => !string.IsNullOrEmpty(id) && !_fromOriginalAccounts.Contains(id);

    /// <summary>已删除（墓碑）的导入条目 id 清单（取证/测试用）。</summary>
    public IReadOnlyCollection<string> HiddenOriginalIds => _hiddenOriginalIds.ToList();

    /// <summary>该方法条目是否已进墓碑。</summary>
    public bool IsHidden(string id) => !string.IsNullOrEmpty(id) && _hiddenOriginalIds.Contains(id);

    /// <summary>
    /// t47 **adopt-on-write（接管）**：把只读（导入）条目**显式接管**为外壳自有条目。
    /// <para>做法：把它从"只读集合"移出 ⇒ 下一次 <see cref="Save"/> 就会写进我们自己的 `servers.json`
    /// （<see cref="Save"/> 只跳过仍在只读集合里的条目）；条目**沿用原来的 id** ⇒ 下次 `Load` 时
    /// `LoadOriginalAccounts` 因"同 id 已存在"而跳过原版那条 ⇒ **不会出第二份**。</para>
    /// <para>🔴 **血缘保留**：`Extra["accountsOrigin"]=true`（导入时写入）原样保留，另加 `shellAdopted=true` + `adoptedAt` 供取证；
    /// **`accounts.json` 全程一字节都不写**（本方法只改内存 + 写我们自己的 servers.json）。</para>
    /// </summary>
    /// <returns>是否真的发生了接管（不是只读条目 / 找不到该 id ⇒ <c>false</c>）。</returns>
    public bool Adopt(string id)
    {
        if (string.IsNullOrEmpty(id) || !_fromOriginalAccounts.Contains(id)) return false;
        var index = _servers.FindIndex(s => s.Id == id);
        if (index < 0) return false;

        var server = _servers[index];
        var extra = new Dictionary<string, System.Text.Json.Nodes.JsonNode>(server.Extra, StringComparer.Ordinal)
        {
            ["accountsOrigin"] = System.Text.Json.Nodes.JsonValue.Create(true),                        // 血缘保留
            ["shellAdopted"] = System.Text.Json.Nodes.JsonValue.Create(true),                          // 已接管标记
            ["adoptedAt"] = System.Text.Json.Nodes.JsonValue.Create(DateTimeOffset.UtcNow.ToString("o")),
        };
        _servers[index] = server.With(extra: extra);
        _fromOriginalAccounts.Remove(id);                                                              // ⇒ 之后按普通可写条目对待
        Save();                                                                                        // 落进我们自己的 servers.json
        DebugLog.Info($"[servers] ADOPT id={id} ⇒ 已接管为外壳自有条目（血缘 accountsOrigin 保留；accounts.json 未写）");
        return true;
    }

    /// <summary>「第一次写操作」的编辑入口：接管 + 更新（未接管的只读行**不会**被静默更新）。</summary>
    public void AdoptAndUpdate(ServerConfig server)
    {
        Adopt(server.Id);
        Update(server);
    }

    /// <summary>「第一次写操作」的删除入口：接管 + 删除（删除只记墓碑，**不触碰 `accounts.json`**）。</summary>
    public void AdoptAndRemove(string id)
    {
        Adopt(id);
        Remove(id);
    }

    /// <summary>该条目是否已被接管（`shellAdopted` 标记；只读导入条目 ⇒ false）。</summary>
    public static bool IsAdoptedEntry(ServerConfig server) => server != null && server.ExtraBool("shellAdopted");

    /// <summary>按 sortIndex 排序后的副本。</summary>
    public List<ServerConfig> Servers
    {
        get
        {
            var list = new List<ServerConfig>(_servers);
            list.Sort((a, b) => a.SortIndex.CompareTo(b.SortIndex));
            return list;
        }
    }

    public List<ServerConfig> EnabledServers => Servers.Where(s => s.Enabled).ToList();

    public bool IsLoaded => _loaded;

    public ServerConfig ById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var s in _servers)
        {
            if (s.Id == id) return s;
        }
        return null;
    }

    public ServerConfig FirstOf(ServerKind kind)
    {
        foreach (var s in _servers)
        {
            if (s.Kind == kind && s.Enabled) return s;
        }
        return null;
    }

    public void Load()
    {
        if (_loaded) return;
        _loaded = true;
        _servers.Clear();

        // t57：用 Read()（返回 JsonNode）而不是 ReadMap()（= as JsonObject）—— 后者对裸数组根返回 null，
        // 会让兜底分支永远进不去（首轮反控实测：裸数组仍读成 0 条）。
        var json = _storage.Read();
        var rootElem = JsonRead.From(json);
        var list = JsonRead.Items(rootElem, "servers");

        // t57：**裸数组根兜底**。裸数组是合法 JSON（不是损坏），只是外层没有 `{"servers":…}` 包装 ——
        // 旧实现只吃对象根 ⇒ 读到 0 条且**不报错、不隔离**（= 静默清空用户服务器列表，最贵的一类缺陷）。
        // 这里显式接受第二种形态，并留一条**可见日志**：否则只是把「静默丢弃」换成「静默接受」，同一族缺陷换方向。
        if (list.Count == 0 && rootElem.HasValue && rootElem.Value.ValueKind == JsonValueKind.Array)
        {
            var bareCount = 0;
            foreach (var element in JsonRead.Objects(rootElem.Value.EnumerateArray()))
            {
                var server = ServerConfig.FromJson(element);
                if (server.Id.Length > 0) { _servers.Add(server); bareCount++; }
            }
            if (bareCount > 0)
            {
                DebugLog.Warn($"SERVERS-SHAPE bare-array-fallback count={bareCount} path={_storage.FilePath}");
            }
        }
        foreach (var element in JsonRead.Objects(list))
        {
            var server = ServerConfig.FromJson(element);
            if (server.Id.Length > 0) _servers.Add(server);
        }

        // t47：读墓碑（删除过的导入条目 ⇒ 本次不再从 accounts.json 复活）
        foreach (var hiddenId in JsonRead.StrList(JsonRead.From(json), "hiddenOriginalIds"))
        {
            if (!string.IsNullOrEmpty(hiddenId)) _hiddenOriginalIds.Add(hiddenId);
        }

        MergeLegacyServers();
        LoadOriginalAccounts();
        AttachCredentials();
        NotifyListeners();
    }

    /// <summary>
    /// 旧 Roaming 根 <c>servers.json</c> 的**只读合并**（事实 70 裁决 1）：按 id 去重、**当前根优先**；
    /// 合并进来的条目可正常管理（下次 <see cref="Save"/> 落到当前根 ⇒ 相当于按需迁移）。
    /// **旧文件本身永不写入、永不改名** —— 因此这里刻意不用 <see cref="JsonStorage"/>（它"损坏即隔离"会 rename 旧根文件）。
    /// </summary>
    private void MergeLegacyServers()
    {
        LegacyServersLoaded = 0;
        var path = _legacyServersFile;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
        if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(_storage.FilePath), StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            var text = System.IO.File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return;
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
            if (!doc.RootElement.TryGetProperty("servers", out var arr) || arr.ValueKind != JsonValueKind.Array) return;

            var merged = 0;
            foreach (var element in arr.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) continue;
                var server = ServerConfig.FromJson(element);
                if (server.Id.Length == 0 || ById(server.Id) != null) continue;
                _servers.Add(server);
                merged++;
            }
            LegacyServersLoaded = merged;
            if (merged > 0)
            {
                DebugLog.Info($"[servers] 旧根 servers.json 合并 {merged} 条（只读来源 {path}；写入目标恒为 {_storage.FilePath}）");
            }
        }
        catch (Exception ex)
        {
            // 旧根坏文件：只记录、不动它（不隔离、不改名）
            DebugLog.Warn($"[servers] 旧根 servers.json 读取失败（已忽略，旧文件未改动）：{ex.GetType().Name}: {ex.Message} path={path}");
        }
    }

    /// <summary>
    /// 兼容读原版 <c>accounts.json</c>（事实 70 裁决 2）：**只读**，条目既不写回原文件，也不进 <c>servers.json</c>。
    /// 坏数据必须留下**显式错误**（<see cref="OriginalAccountsError"/>），不得静默当成"0 条即通过"。
    /// </summary>
    private void LoadOriginalAccounts()
    {
        OriginalAccountsLoaded = 0;
        OriginalAccountsError = null;
        if (!_readOriginalAccounts) return;

        var path = _originalAccountsFile;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
        {
            // 实例模式（未显式指定路径）每次 Load 都重新解析：文件被移走 → 0 台；再放回 → 又能读到。
            // （ui 的反控制「改名 accounts.json 应回到 0 台」依赖这条；显式路径实例不走重解析。）
            if (!_readOriginalAccounts || !string.IsNullOrEmpty(_originalAccountsFile)) return;
            path = AppDataDir.Instance.ExistingOriginalAccountsFile();
            if (string.IsNullOrEmpty(path)) return;
        }

        try
        {
            var text = System.IO.File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text))
            {
                OriginalAccountsError = "原版 accounts.json 为空文件";
                DebugLog.Warn($"[accounts] {OriginalAccountsError} path={path}");
                return;
            }

            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                OriginalAccountsError = $"原版 accounts.json 根节点不是数组（{doc.RootElement.ValueKind}）";
                DebugLog.Warn($"[accounts] {OriginalAccountsError} path={path}");
                return;
            }

            var total = doc.RootElement.GetArrayLength();
            var mapped = 0;
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) continue;
                var cfg = FromOriginalAccount(element);
                if (cfg == null) continue;
                if (ById(cfg.Id) != null) continue; // 已在 servers.json 里 ⇒ 以我们自己的配置为准（**已接管的条目走这条**）
                if (_hiddenOriginalIds.Contains(cfg.Id)) continue; // t47 墓碑 ⇒ 已删除的条目不得复活
                _servers.Add(cfg);
                _fromOriginalAccounts.Add(cfg.Id);
                mapped++;
            }
            OriginalAccountsLoaded = mapped;

            if (mapped == 0 && total > 0)
            {
                OriginalAccountsError = $"原版 accounts.json 有 {total} 个元素但均无法映射（缺 id 或类型不符）";
                DebugLog.Warn($"[accounts] {OriginalAccountsError} path={path}");
            }
            else if (total > mapped)
            {
                DebugLog.Warn($"[accounts] 原版 accounts.json 共 {total} 条，成功映射 {mapped} 条（其余缺 id）path={path}");
            }
        }
        catch (Exception ex)
        {
            OriginalAccountsError = $"{ex.GetType().Name}: {ex.Message}";
            DebugLog.Warn($"[accounts] 原版 accounts.json 读取失败：{OriginalAccountsError} path={path}");
        }
    }

    /// <summary>原版 <c>accounts.json</c> 单条 → <see cref="ServerConfig"/>（字段映射见文件头注释）。<c>id</c> 为空返回 <c>null</c>。</summary>
    private static ServerConfig FromOriginalAccount(JsonElement json)
    {
        var id = JsonRead.Str(json, "id");
        if (id.Length == 0) return null;

        var name = JsonRead.Str(json, "name");
        if (name.Length == 0) name = JsonRead.Str(json, "serverName");

        var baseUrl = JsonRead.Str(json, "preferredLineUrl");
        var lines = JsonRead.Prop(json, "lines");
        if (baseUrl.Length == 0 && lines.HasValue && lines.Value.ValueKind == JsonValueKind.Array)
        {
            string fallback = null;
            foreach (var line in lines.Value.EnumerateArray())
            {
                if (line.ValueKind != JsonValueKind.Object) continue;
                var url = JsonRead.Str(line, "url");
                if (url.Length == 0) continue;
                if (JsonRead.Bool(line, "isMain")) { fallback = url; break; }
                fallback ??= url;
            }
            baseUrl = fallback ?? string.Empty;
        }

        var cfg = new ServerConfig
        {
            Id = id,
            Kind = ServerKindExtensions.ParseKind(JsonRead.Str(json, "provider")),
            Name = name,
            BaseUrl = baseUrl,
            UserName = JsonRead.Str(json, "username"),
            UserId = JsonRead.Str(json, "lastKnownUserId"),
            AccessToken = JsonRead.Str(json, "encryptedToken"),
            IconUrl = JsonRead.Str(json, "iconUrl"),
            Enabled = true,
            SortIndex = JsonRead.IntOrNull(json, "sortOrder") ?? 0,
        };

        // 原版其余字段原样保留在 Extra（只读，供 UI/后续 t14 使用）；**秘密字段不入 Extra**。
        foreach (var p in json.EnumerateObject())
        {
            if (p.Name == "encryptedToken" || p.Name == "encryptedPassword") continue;
            try { cfg.Extra[p.Name] = System.Text.Json.Nodes.JsonNode.Parse(p.Value.GetRawText()); }
            catch (System.Text.Json.JsonException) { /* 有意忽略：个别原版字段不是合法 JSON => 跳过该字段，不影响整体映射 */ }
        }
        cfg.Extra["accountsOrigin"] = System.Text.Json.Nodes.JsonValue.Create(true);
        return cfg;
    }

    public System.Threading.Tasks.Task LoadAsync() => System.Threading.Tasks.Task.Run(Load);

    /// <summary>把加密存储里的密码/令牌贴回配置对象（内存态使用）。</summary>
    private void AttachCredentials()
    {
        _credentials.EnsureLoaded();
        for (var i = 0; i < _servers.Count; i++)
        {
            var server = _servers[i];
            var password = _credentials.Read(server.PasswordKey);
            var token = _credentials.Read(server.TokenKey) ?? server.AccessToken;
            if (password == null && token == server.AccessToken) continue;

            var withToken = server.With(accessToken: token ?? string.Empty);
            if (password != null) withToken.SetExtra("password", password);
            _servers[i] = withToken;
        }
    }

    /// <summary>新增（自动分配 sortIndex；凭据单独加密保存）。</summary>
    public ServerConfig Add(ServerConfig server)
    {
        var sortIndex = _servers.Count == 0 ? 0 : _servers.Max(s => s.SortIndex) + 1;
        var withOrder = server.With(sortIndex: sortIndex);
        _servers.Add(withOrder);
        PersistCredentials(withOrder);
        Save();
        return withOrder;
    }

    /// <summary>
    /// 更新（密码/token 变更时同步加密存储）。
    /// 🔴 t47：**未接管的只读导入条目一律拒绝**（此前是"静默替换内存副本、Save 又跳过" ⇒ 静默无效果）。
    /// 反控依赖这条：接管必须是**显式**发生的，不是把只读判定整个删掉。
    /// </summary>
    public void Update(ServerConfig server)
    {
        if (server != null && _fromOriginalAccounts.Contains(server.Id))
        {
            throw new InvalidOperationException(
                $"拒绝写入：id={server.Id} 是只读导入条目且尚未接管（先 Adopt(id) 或用 AdoptAndUpdate(server)）");
        }
        var index = _servers.FindIndex(s => s.Id == server.Id);
        if (index < 0)
        {
            Add(server);
            return;
        }
        _servers[index] = server;
        PersistCredentials(server);
        Save();
    }

    /// <summary>
    /// 删除（同时清理凭据）＋ **墓碑**：若该条目来自原版导入，记入 `hiddenOriginalIds` ⇒ 重启不复活。
    /// 🔴 t47：**未接管的只读导入条目一律拒绝**（同样为反控服务）；🔴 全程不写 `accounts.json`。
    /// </summary>
    public void Remove(string id)
    {
        if (!string.IsNullOrEmpty(id) && _fromOriginalAccounts.Contains(id))
        {
            throw new InvalidOperationException(
                $"拒绝删除：id={id} 是只读导入条目且尚未接管（先 Adopt(id) 或用 AdoptAndRemove(id)）");
        }
        var server = ById(id);
        if (server != null && server.IsOriginalAccountEntry && _hiddenOriginalIds.Add(id))
        {
            DebugLog.Info($"[servers] TOMBSTONE id={id}（记入 servers.json 的 hiddenOriginalIds；accounts.json 未写）");
        }
        _servers.RemoveAll(s => s.Id == id);
        if (server != null)
        {
            _credentials.Remove(server.PasswordKey);
            _credentials.Remove(server.TokenKey);
        }
        Save();
    }

    /// <summary>P1 零调用项 <c>reorder()</c>：按给定 id 顺序重排 sortIndex。</summary>
    public void Reorder(IReadOnlyList<string> idsInOrder)
    {
        if (idsInOrder == null) return;
        for (var i = 0; i < idsInOrder.Count; i++)
        {
            var server = ById(idsInOrder[i]);
            if (server == null) continue;
            var index = _servers.FindIndex(s => s.Id == server.Id);
            if (index >= 0) _servers[index] = server.With(sortIndex: i);
        }
        Save();
    }

    /// <summary>从磁盘重新读取（备份恢复后使用）。</summary>
    public void Reload()
    {
        _loaded = false;
        Load();
    }

    /// <summary>落盘（剔除凭据：只存在于加密存储；**剔除原版 accounts.json 的只读条目**）。</summary>
    public void Save()
    {
        var serialized = _servers
            .Where(s => !_fromOriginalAccounts.Contains(s.Id))
            .Select(s => s.With(accessToken: string.Empty, extra: StripSecrets(s.Extra)).ToJson())
            .ToList();

        var root = new System.Text.Json.Nodes.JsonObject();
        var arr = new System.Text.Json.Nodes.JsonArray();
        foreach (var item in serialized) arr.Add(item);
        root["servers"] = arr;
        // t47 墓碑（删除过的导入条目）——**只写我们自己的文件**，原版 accounts.json 永不触碰
        var hidden = new System.Text.Json.Nodes.JsonArray();
        foreach (var id in _hiddenOriginalIds) hidden.Add(id);
        root["hiddenOriginalIds"] = hidden;

        _storage.Write(root);
        NotifyListeners();
    }

    public System.Threading.Tasks.Task SaveAsync() => System.Threading.Tasks.Task.Run(Save);

    private static Dictionary<string, System.Text.Json.Nodes.JsonNode> StripSecrets(Dictionary<string, System.Text.Json.Nodes.JsonNode> extra)
    {
        var copy = new Dictionary<string, System.Text.Json.Nodes.JsonNode>(extra ?? new Dictionary<string, System.Text.Json.Nodes.JsonNode>(), StringComparer.Ordinal);
        copy.Remove("password");
        copy.Remove("token");
        return copy;
    }

    private void PersistCredentials(ServerConfig server)
    {
        _credentials.EnsureLoaded();
        var password = server.ExtraString("password");
        if (!string.IsNullOrEmpty(password))
        {
            _credentials.Write(server.PasswordKey, password);
        }
        if (!string.IsNullOrEmpty(server.AccessToken))
        {
            _credentials.Write(server.TokenKey, server.AccessToken);
        }
    }

    /// <summary>读取密码（新建连接/重新登录/编辑回填时用）—— P1 零调用项 <c>passwordOf()</c>。</summary>
    public string PasswordOf(string serverId)
    {
        var server = ById(serverId);
        if (server == null) return null;
        _credentials.EnsureLoaded();
        return _credentials.Read(server.PasswordKey) ?? server.Password;
    }

    /// <summary>生成唯一 id（时间戳 + 计数）。</summary>
    public string NewId()
    {
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var candidate = $"srv_{stamp}";
        var counter = 0;
        while (ById(candidate) != null)
        {
            candidate = $"srv_{stamp}_{counter++}";
        }
        return candidate;
    }
}
