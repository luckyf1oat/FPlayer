// S5 体验设施（t33）：缓存统计与清理的**统一入口**（UI 的"缓存管理"面接这个）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AIPlayer.Shell.Services.Infra;

/// <summary>一个缓存组的统计（组名 + 目录 + 条数 + 字节）。</summary>
public sealed class CacheGroupStats
{
    public string Name { get; set; }

    public string Directory { get; set; }

    public int Count { get; set; }

    public long Bytes { get; set; }

    public override string ToString() => $"{Name}: {Count} 条 / {Bytes} B";
}

/// <summary>
/// 缓存统计与清理：把"图片 / 弹幕 / 歌词 / 内核弹幕"四个组一次性统计、整组清理。
/// <para>清理语义 = **删除缓存文件本身**（不删目录、不碰其它数据）；统计语义 = 现场枚举，不缓存结果。</para>
/// </summary>
public sealed class CacheStatsService
{
    private readonly AppDataDir _data;

    public CacheStatsService(AppDataDir data)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
    }

    /// <summary>已知缓存组（名 → 目录）。内核弹幕缓存目录只统计、不清理（它是内核的落点，见事实 35）。</summary>
    public IReadOnlyList<CacheGroupStats> All()
    {
        var groups = new List<CacheGroupStats>
        {
            Snapshot("images", Path.Combine(_data.CacheDir, ImageCacheManager.SubDirectory)),
            Snapshot("danmaku", _data.DanmakuCacheDir),
            Snapshot("lyrics", _data.LyricsCacheDir),
            Snapshot("kernel-danmaku", _data.KernelDanmakuCacheDir),
        };
        return groups;
    }

    /// <summary>清理**外壳自有的**三个缓存组（图片/弹幕/歌词）⇒ 返回删除文件总数。<b>不含</b>内核弹幕缓存。</summary>
    public int ClearShellCaches()
    {
        var removed = 0;
        removed += ClearDirectory(Path.Combine(_data.CacheDir, ImageCacheManager.SubDirectory));
        removed += ClearDirectory(_data.DanmakuCacheDir);
        removed += ClearDirectory(_data.LyricsCacheDir);
        return removed;
    }

    /// <summary>对所有外壳缓存组按各自上限做 LRU 淘汰，返回删除文件总数。</summary>
    public int PruneShellCaches()
    {
        var removed = 0;
        removed += new DiskCacheStore(Path.Combine(_data.CacheDir, ImageCacheManager.SubDirectory)).Prune();
        removed += new DiskCacheStore(_data.DanmakuCacheDir).Prune();
        removed += new DiskCacheStore(_data.LyricsCacheDir).Prune();
        return removed;
    }

    /// <summary>一行汇总（供日志/自检直接打印）：<c>images: 3 条 / 456 B ｜ danmaku: 0 条 / 0 B ｜ …</c></summary>
    public string Report() => string.Join(" ｜ ", All().Select(g => g.ToString()));

    private static CacheGroupStats Snapshot(string name, string directory)
    {
        var stats = new CacheGroupStats { Name = name, Directory = directory };
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return stats;
        var files = new DirectoryInfo(directory).EnumerateFiles("*", SearchOption.AllDirectories)
            .Where(f => !f.Name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
            .ToList();
        stats.Count = files.Count;
        stats.Bytes = files.Sum(f => f.Length);
        return stats;
    }

    private static int ClearDirectory(string directory)
    {
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return 0;
        var removed = 0;
        foreach (var f in new DirectoryInfo(directory).EnumerateFiles("*", SearchOption.AllDirectories))
        {
            try { f.Delete(); removed++; }
            catch (IOException) { /* 有意忽略：缓存文件被占用/权限不足 => 删不掉不阻断整轮统计清理（下次 Prune/Clear 会再试） */ }
        }
        return removed;
    }
}
