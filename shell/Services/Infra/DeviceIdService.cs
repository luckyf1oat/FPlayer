// S5 体验设施（t33）：设备 ID —— 稳定生成 + 落盘 + 备份/恢复后保持。
// 语义（本文件即口径）：
//   ① 首次调用 ⇒ 生成 GUID("N") 并落盘；此后**任何新实例/新进程**读到同一个值（稳定）；
//   ② 文件被**显式删除且没有备份** ⇒ 生成新值（这是"设备重装"的语义，不是缺陷）；
//   ③ 备份/恢复 ⇒ 恢复后与原值**逐字符相同**（`BackupTo` → 删文件 → `RestoreFrom` ⇒ 值不变）。
using System;
using System.IO;

namespace AIPlayer.Shell.Services.Infra;

public sealed class DeviceIdService
{
    public const string FileName = "device-id.txt";

    /// <summary>
    /// 合法设备 ID 的**精确长度**（<c>Guid.ToString("N")</c> = 32 位十六进制）。
    /// t148：此前只判 `Length &gt;= 8` ⇒ **写入被截断的半截 ID 会被当成合法值接受**（前半段仍是合法 hex），
    /// 而设备 ID 一旦变化，服务端会把本机当**新设备**（t33 S5④ 的语义正是"同一数据根恒定同值"）。
    /// </summary>
    public const int IdLength = 32;

    /// <summary>历史常量（保留以不破坏既有调用面）；**判定不再用它**，见 <see cref="IsValidId"/>。</summary>
    public const int MinLength = 8;

    private readonly AppDataDir _data;
    private readonly Action<string> _log;

    public DeviceIdService(AppDataDir data, Action<string> log = null)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _log = log;
    }

    public string FilePath => _data.File(FileName);

    /// <summary>
    /// 合法设备 ID 判据：**长度 == 32 且全为十六进制**（t148）。
    /// 为什么不能只判长度：`File.WriteAllText` 被中断会留下"前 N 个字符"，若 N ≥ 8，旧的 `Length &gt;= 8`
    /// 会把它当合法值 ⇒ 安静地换掉设备身份。⇒ 这里要求**完整形态**，截断/多余字符/非 hex 一律不接受。
    /// </summary>
    public static bool IsValidId(string value)
    {
        if (value == null || value.Length != IdLength) return false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            var hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!hex) return false;
        }

        return true;
    }

    /// <summary>读；没有或损坏 ⇒ 生成并落盘。**同一数据根上恒定返回同值**。</summary>
    public string GetOrCreate()
    {
        var existing = TryRead();
        if (existing != null) return existing;

        var fresh = Guid.NewGuid().ToString("N");
        AtomicFile.WriteAllText(FilePath, fresh);   // t148：原子写（不再裸 File.WriteAllText）
        _log?.Invoke($"DEVICE-ID 生成 {fresh}");
        return fresh;
    }

    /// <summary>只读（不生成）：没有合法值 ⇒ <c>null</c>（t148：拒绝截断/非 hex 形态，并留一行可见诊断）。</summary>
    public string TryRead()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var text = File.ReadAllText(FilePath).Trim();
            if (IsValidId(text)) return text;

            // 可见失败：这不是"没有文件"，而是"盘上有东西但不合法"——必须留痕，否则截断会被静默吞掉。
            _log?.Invoke($"DEVICE-ID 拒绝非法/截断值（len={text.Length}，期望 {IdLength} hex）⇒ 将重新生成");
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <summary>备份到指定文件（父目录自动建）。返回备份文件路径。</summary>
    public string BackupTo(string backupPath)
    {
        var id = GetOrCreate();
        AtomicFile.WriteAllText(backupPath, id);   // t148：原子写
        _log?.Invoke($"DEVICE-ID 已备份 → {backupPath}");
        return backupPath;
    }

    /// <summary>从备份恢复（覆盖当前值）。返回恢复后的值；备份缺失/非法 ⇒ <c>null</c>（当前值不动）。</summary>
    public string RestoreFrom(string backupPath)
    {
        if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath)) return null;
        var text = File.ReadAllText(backupPath).Trim();
        if (!IsValidId(text))
        {
            _log?.Invoke($"DEVICE-ID 备份非法/截断（len={text.Length}）⇒ 拒绝恢复，当前值不动");
            return null;
        }

        AtomicFile.WriteAllText(FilePath, text);   // t148：原子写
        _log?.Invoke($"DEVICE-ID 已从备份恢复 {text}");
        return text;
    }
}
