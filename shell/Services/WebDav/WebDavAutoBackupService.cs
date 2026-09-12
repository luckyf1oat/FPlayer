// 等价移植：rebuild/ai_player/lib/core/services/webdav_backup_service.dart（Dart `WebDavBackupService`）。
// 端点依据：reversed/FlutterApp/SERVICE_API.md §4（WebDAV）——
//   `/backup_<日期>` 命名、`PROPFIND`（列举）/ `MKCOL`（建目录）/ `PUT`（上传）/ `DELETE`（滚动清理）。
// 打包内容面（同节推断「设置 / 服务器配置 / 播放进度 / 图标库」的 JSON 打包）：
//   settings.json + servers.json(**剥离密码/令牌**) + skip_segments.json(跳过片段) + icons.json(图标库)。
// 本地路径：AppDataDir.Instance（等价 Dart AppPaths.instance）；文件读写走 JsonStorage / System.IO。
// 说明：Dart 侧同文件内的 `schedule` / `cancelSchedule` / `isScheduled`（webdav_backup_service.dart:183-204）
// 已按 DESIGN §4.1 #145 独立成 WebDavAutoBackupScheduler.cs（调度语义不变），本文件不再重复实现定时器。
// 未实证处：备份包字段缺失、JSON 非法、远端返回空体等一律容错（返回 null / 空列表 / 默认值）。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Infra;
using AIPlayer.Shell.Services.Models;
using AIPlayer.Shell.Services.Storage;
using AIPlayer.Shell.Services.Util;

namespace AIPlayer.Shell.Services.WebDav;

/// <summary>WebDAV 自动备份（对应 Dart <c>WebDavBackupService</c>）。</summary>
public sealed class WebDavAutoBackupService
{
    /// <summary>备份文件前缀（Dart <c>backupPrefix</c>）。</summary>
    public const string BackupPrefix = "backup_";

    /// <summary>缩进写盘（等价 Dart <c>JsonEncoder.withIndent('  ')</c>；.NET 默认同为 2 空格）。</summary>
    private static readonly JsonSerializerOptions Indented = new JsonSerializerOptions { WriteIndented = true };

    public WebDavAutoBackupService(Action<string> onLog = null)
    {
        OnLog = onLog;
    }

    public Action<string> OnLog { get; }

    private void Log(string message)
    {
        OnLog?.Invoke(message);
        AppDataDir.Instance.Log("[webdav-backup] " + message);
    }

    // ── 打包 ────────────────────────────────────────────────────────────────

    /// <summary>组装备份 JSON（**不含任何密码 / 令牌**；恢复后需重新登录，避免凭据外泄）。</summary>
    public async Task<string> BuildBundleJsonAsync()
    {
        var paths = AppDataDir.Instance;

        var settings = (await new JsonStorage(paths.SettingsFile).ReadMapAsync().ConfigureAwait(false)) ?? new JsonObject();
        var serversRaw = (await new JsonStorage(paths.ServersFile).ReadMapAsync().ConfigureAwait(false)) ?? new JsonObject();
        var skipCache = (await new JsonStorage(paths.SkipCacheFile).ReadMapAsync().ConfigureAwait(false)) ?? new JsonObject();
        var icons = (await new JsonStorage(paths.IconsFile).ReadMapAsync().ConfigureAwait(false)) ?? new JsonObject();

        var bundle = new BackupBundle
        {
            Schema = 1,
            CreatedAt = DateTime.Now,
            Settings = CloneMap(settings),
            Servers = SanitizeServers(serversRaw),
            SkipCache = CloneMap(skipCache),
            Icons = CloneMap(icons),
        };
        return bundle.ToJson().ToJsonString(Indented);
    }

    /// <summary>剔除服务器配置中的凭据（等价 Dart：<c>accessToken = ''</c>，并移除 <c>extra.password</c>/<c>extra.token</c>）。</summary>
    private static List<JsonNode> SanitizeServers(JsonObject serversRaw)
    {
        var sanitized = new List<JsonNode>();
        if (serversRaw == null) return sanitized;

        var list = serversRaw["servers"] as JsonArray;
        if (list == null) return sanitized;

        foreach (var item in list)
        {
            // 等价 Dart `whereType<Map>()`：丢弃非对象元素
            if (item is not JsonObject server) continue;

            var copy = (JsonObject)server.DeepClone();
            copy["accessToken"] = string.Empty;
            if (copy["extra"] is JsonObject extra)
            {
                extra.Remove("password");
                extra.Remove("token");
            }
            sanitized.Add(copy);
        }
        return sanitized;
    }

    private static Dictionary<string, JsonNode> CloneMap(JsonObject source)
    {
        var map = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        if (source == null) return map;
        foreach (var kv in source)
        {
            map[kv.Key] = kv.Value == null ? null : kv.Value.DeepClone();
        }
        return map;
    }

    // ── 备份 / 清理 ─────────────────────────────────────────────────────────

    /// <summary>执行一次备份（<c>MKCOL</c> + <c>PUT</c>），成功返回远端路径，失败返回 <c>null</c>。</summary>
    public async Task<string> BackupAsync(
        WebDavService dav,
        string directory = "/backup",
        CancellationToken cancellationToken = default)
    {
        if (dav == null) return null;
        try
        {
            await dav.EnsureDirectoryAsync(directory, cancellationToken).ConfigureAwait(false);
            // 生成名不含非法字符，SanitizeFileName 仅作防御性过滤（Dart 侧无此步，行为等价）
            var name = TextUtils.SanitizeFileName(BackupPrefix + Timestamp(DateTime.Now)) + ".json";
            var path = $"{directory}/{name}";
            var content = await BuildBundleJsonAsync().ConfigureAwait(false);
            if (!await dav.WriteStringAsync(path, content, cancellationToken).ConfigureAwait(false)) return null;
            Log($"备份完成：{path}");
            return path;
        }
        catch (Exception ex)
        {
            Log($"备份失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>列出已有备份（按文件名**降序**，即最新在前；等价 Dart <c>listBackups</c>）。</summary>
    public async Task<List<WebDavEntry>> ListBackupsAsync(
        WebDavService dav,
        string directory = "/backup",
        CancellationToken cancellationToken = default)
    {
        var backups = new List<WebDavEntry>();
        if (dav == null) return backups;

        var entries = await dav.ListAsync(directory, cancellationToken).ConfigureAwait(false);
        foreach (var entry in entries)
        {
            if (entry.IsDirectory) continue;
            if (!entry.Name.StartsWith(BackupPrefix, StringComparison.Ordinal)) continue;
            backups.Add(entry);
        }
        // Dart `b.name.compareTo(a.name)` ⇒ UTF-16 序比较、降序
        backups.Sort((a, b) => string.CompareOrdinal(b.Name, a.Name));
        return backups;
    }

    /// <summary>滚动清理：仅保留最新 <paramref name="keepCount"/> 份，返回删除数量。</summary>
    public async Task<int> PruneAsync(
        WebDavService dav,
        string directory = "/backup",
        int keepCount = 7,
        CancellationToken cancellationToken = default)
    {
        if (dav == null) return 0;
        if (keepCount < 0) keepCount = 0; // Dart `skip(负数)` 会抛 RangeError；此处按 0 容错

        var backups = await ListBackupsAsync(dav, directory, cancellationToken).ConfigureAwait(false);
        if (backups.Count <= keepCount) return 0;

        var removed = 0;
        for (var i = keepCount; i < backups.Count; i++)
        {
            if (await dav.DeleteAsync(backups[i].Path, cancellationToken).ConfigureAwait(false)) removed++;
        }
        if (removed > 0) Log($"清理旧备份 {removed} 份");
        return removed;
    }

    // ── 恢复 ────────────────────────────────────────────────────────────────

    /// <summary>下载并解析指定备份；空体或非法 JSON 返回 <c>null</c>。</summary>
    public async Task<BackupBundle> DownloadAsync(
        WebDavService dav,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        if (dav == null) return null;
        var text = await dav.ReadStringAsync(remotePath, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text)) return null;

        var json = JsonRead.FromNode(text);
        if (json == null)
        {
            Log("备份解析失败：非法 JSON");
            return null;
        }

        try
        {
            return BackupBundle.FromJson(json.Value);
        }
        catch (Exception ex)
        {
            Log($"备份解析失败：{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 把备份内容写回本地存储（settings / servers / 跳过片段 / 图标库）。
    /// t148：<paramref name="cancellationToken"/> 现在**真的转到底**（每条 <c>WriteAsync</c> 都收它）——
    /// 已取消时第一条写就会抛 <c>OperationCanceledException</c>（目标文件不动），不再"收下 token 但一路无视"。
    /// </summary>
    public async Task<bool> RestoreLocallyAsync(BackupBundle bundle, CancellationToken cancellationToken = default)
    {
        if (bundle == null) return false;
        try
        {
            var paths = AppDataDir.Instance;

            cancellationToken.ThrowIfCancellationRequested();

            await new JsonStorage(paths.SettingsFile)
                .WriteAsync(BackupBundle.ToJsonObject(bundle.Settings), cancellationToken).ConfigureAwait(false);

            var serversRoot = new JsonObject { ["servers"] = BackupBundle.ToJsonArray(bundle.Servers) };
            await new JsonStorage(paths.ServersFile).WriteAsync(serversRoot, cancellationToken).ConfigureAwait(false);

            // 与 Dart 一致：空表不覆盖本地已有文件
            if (bundle.SkipCache.Count > 0)
            {
                await new JsonStorage(paths.SkipCacheFile)
                    .WriteAsync(BackupBundle.ToJsonObject(bundle.SkipCache), cancellationToken).ConfigureAwait(false);
            }
            if (bundle.Icons.Count > 0)
            {
                await new JsonStorage(paths.IconsFile)
                    .WriteAsync(BackupBundle.ToJsonObject(bundle.Icons), cancellationToken).ConfigureAwait(false);
            }

            Log($"已恢复备份（{WebDavTime.ToIso(bundle.CreatedAt)}）");
            return true;
        }
        catch (OperationCanceledException)
        {
            // t148：取消**不是失败**，必须原样冒泡（否则调用方无法区分"用户取消"与"恢复失败"）。
            Log("恢复已取消（取消令牌触发）");
            throw;
        }
        catch (Exception ex)
        {
            Log($"恢复失败：{ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 一步恢复（新增便捷入口，Dart 需调用方自行 <c>listBackups</c> + <c>download</c> + <c>restoreLocally</c>）：
    /// <paramref name="remotePath"/> 为空时取最新一份（名单按名称降序 ⇒ 首项最新）。
    /// </summary>
    public async Task<bool> RestoreAsync(
        WebDavService dav,
        string remotePath = null,
        string directory = "/backup",
        CancellationToken cancellationToken = default)
    {
        if (dav == null) return false;

        var target = remotePath;
        if (string.IsNullOrEmpty(target))
        {
            var backups = await ListBackupsAsync(dav, directory, cancellationToken).ConfigureAwait(false);
            if (backups.Count == 0)
            {
                Log($"未找到可恢复的备份（{directory}）");
                return false;
            }
            target = backups[0].Path;
        }

        var bundle = await DownloadAsync(dav, target, cancellationToken).ConfigureAwait(false);
        if (bundle == null) return false;
        return await RestoreLocallyAsync(bundle, cancellationToken).ConfigureAwait(false);
    }

    // ── 本地快照 / 命名 ─────────────────────────────────────────────────────

    /// <summary>时间戳（等价 Dart <c>timestamp</c>：<c>yyyyMMdd_HHmmss</c>，本地时间）。</summary>
    public static string Timestamp(DateTime time)
        => time.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);

    /// <summary>本地快照（<c>backup/backup_*.json</c>），便于用户手动保管；失败返回 <c>null</c>。</summary>
    public async Task<string> WriteLocalSnapshotAsync()
    {
        try
        {
            var directory = AppDataDir.Instance.BackupDir;
            var name = TextUtils.SanitizeFileName(BackupPrefix + Timestamp(DateTime.Now)) + ".json";
            var file = Path.Combine(directory, name);
            var content = await BuildBundleJsonAsync().ConfigureAwait(false);
            File.WriteAllText(file, content, new UTF8Encoding(false));
            return file;
        }
        catch (Exception ex)
        {
            Log($"本地快照失败：{ex.Message}");
            return null;
        }
    }
}
