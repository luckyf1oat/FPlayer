// S5 体验设施（t33）：数据迁移 —— 旧 Roaming 根 → 新数据根（Local 或便携根）。
// 语义：**只复制、不删除源**；目标已存在同名文件 ⇒ 跳过（幂等）；单个文件失败 ⇒ 记入 Failed 并继续（不丢数据）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AIPlayer.Shell.Services.Infra;

/// <summary>一次迁移的结果（可打印、可断言）。</summary>
public sealed class DataMigrationReport
{
    public int SourceFiles { get; set; }

    public int TargetFilesBefore { get; set; }

    public int TargetFilesAfter { get; set; }

    public int Copied { get; set; }

    public int Skipped { get; set; }

    public int Failed { get; set; }

    public List<string> Notes { get; } = new List<string>();

    public bool IsIdempotentRun => Copied == 0;

    public override string ToString()
        => $"源 {SourceFiles} 个文件 → 目标 {TargetFilesBefore} ⇒ {TargetFilesAfter} 个（复制 {Copied} / 跳过 {Skipped} / 失败 {Failed}）"
           + (Notes.Count == 0 ? string.Empty : "；" + string.Join("；", Notes));
}

/// <summary>
/// 通用"旧根 → 新根"迁移器（与 <see cref="AppDataDir.MigrateLegacyDataIfNeeded"/> 的分工：
/// 后者是**启动期自动迁移**（按 <see cref="AppDataDir.CanAutoMigrate"/> 闸门、且只在目标缺文件时搬）；
/// 本类是**可显式调用、可重复执行、带报告**的同一套语义，供 UI 的"数据迁移"按钮与自检使用）。
/// </summary>
public sealed class DataMigrationService
{
    private readonly Action<string> _log;

    public DataMigrationService(string sourceRoot, string targetRoot, Action<string> log = null)
    {
        SourceRoot = sourceRoot;
        TargetRoot = targetRoot;
        _log = log;
    }

    public string SourceRoot { get; }

    public string TargetRoot { get; }

    /// <summary>跑一次迁移。<paramref name="overwrite"/> = false（默认）时**目标已存在即跳过** ⇒ 天然幂等。</summary>
    public DataMigrationReport Run(bool overwrite = false)
    {
        var report = new DataMigrationReport();
        if (string.IsNullOrEmpty(SourceRoot) || string.IsNullOrEmpty(TargetRoot))
        {
            report.Notes.Add("源或目标为空，未执行");
            report.Failed++;
            return report;
        }
        if (!Directory.Exists(SourceRoot))
        {
            report.Notes.Add("源目录不存在，未执行（不是错误：全新安装没有旧根）");
            if (Directory.Exists(TargetRoot)) report.TargetFilesAfter = CountFiles(TargetRoot);
            return report;
        }

        System.IO.Directory.CreateDirectory(TargetRoot);
        var sourceFiles = Directory.GetFiles(SourceRoot, "*", SearchOption.AllDirectories);
        report.SourceFiles = sourceFiles.Length;
        report.TargetFilesBefore = CountFiles(TargetRoot);

        foreach (var src in sourceFiles)
        {
            var rel = Path.GetRelativePath(SourceRoot, src);
            var dst = Path.Combine(TargetRoot, rel);
            try
            {
                if (File.Exists(dst) && !overwrite)
                {
                    report.Skipped++;
                    continue;
                }
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(src, dst, overwrite: true);
                report.Copied++;
            }
            catch (Exception ex)
            {
                report.Failed++;
                report.Notes.Add($"{rel}: {ex.GetType().Name}");
                _log?.Invoke($"MIGRATE-FAIL {rel} {ex.GetType().Name}");
            }
        }

        report.TargetFilesAfter = CountFiles(TargetRoot);
        _log?.Invoke($"MIGRATE {report}");
        return report;
    }

    private static int CountFiles(string root)
        => Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length : 0;
}
