// 等价移植：rebuild/ai_player/lib/core/storage/json_storage.dart。
// 安全要点保持一致：原子写（*.tmp → 替换）+ 容错读（损坏文件隔离为 *.corrupt 并返回 null，绝不因坏文件启动失败）。

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIPlayer.Shell.Services.Infra;

namespace AIPlayer.Shell.Services.Storage;

/// <summary>结构化本地文件存储（对应 Dart <c>JsonStorage</c>）。</summary>
public sealed class JsonStorage
{
    private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
    };

    public JsonStorage(string filePath)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public string FilePath { get; }

    /// <summary>读取为 JSON 节点；不存在或损坏时返回 <c>null</c>。</summary>
    public JsonNode Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var text = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(text)) return null;
            return JsonNode.Parse(text);
        }
        catch (Exception ex)
        {
            Quarantine(ex);
            return null;
        }
    }

    /// <summary>读取为 JSON 对象；非对象时返回 <c>null</c>。</summary>
    public JsonObject ReadMap() => Read() as JsonObject;

    /// <summary>异步读取（保持 Dart API 形状；实现为线程池读，避免 UI 线程阻塞）。</summary>
    public System.Threading.Tasks.Task<JsonNode> ReadAsync()
        => System.Threading.Tasks.Task.Run(Read);

    public System.Threading.Tasks.Task<JsonObject> ReadMapAsync()
        => System.Threading.Tasks.Task.Run(ReadMap);

    /// <summary>写入 JSON（接受 POCO / 字典 / JsonNode）。原子替换。</summary>
    public void Write(object value)
    {
        var node = value as JsonNode ?? JsonSerializer.SerializeToNode(value, WriteOptions);
        // t148：写盘收敛到唯一实现 `Infra.AtomicFile`（tmp + **覆盖式** Move）。
        // 旧实现是"先 Delete 再 Move" ⇒ Delete 成功、Move 之前崩溃时目标文件会**直接消失**；现在没有这个窗口。
        AtomicFile.WriteAllText(FilePath, node == null ? "null" : node.ToJsonString(WriteOptions));
    }

    public System.Threading.Tasks.Task WriteAsync(object value)
        => System.Threading.Tasks.Task.Run(() => Write(value));

    /// <summary>
    /// t148：**可取消**的异步写 —— 取消令牌真的走到文件 IO 上（不再是"收下 token 但从不使用"）。
    /// 已取消 ⇒ 抛 <c>OperationCanceledException</c> 且**目标文件不动**（连 tmp 都不会建）。
    /// </summary>
    public System.Threading.Tasks.Task WriteAsync(object value, System.Threading.CancellationToken cancellationToken)
    {
        var node = value as JsonNode ?? JsonSerializer.SerializeToNode(value, WriteOptions);
        return AtomicFile.WriteAllTextAsync(FilePath, node == null ? "null" : node.ToJsonString(WriteOptions), cancellationToken);
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { /* 有意忽略：删除是尽力而为（被占用/无权限）=> 下次写入会再试 */ }
    }

    private void Quarantine(Exception error)
    {
        try
        {
            if (!File.Exists(FilePath)) return;
            var target = FilePath + ".corrupt";
            if (File.Exists(target))
            {
                try { File.Delete(target); }
                catch (IOException)   // 有意忽略：旧隔离文件删不掉 => 由紧随其后的 File.Move(overwrite) 暴露真实失败
                {
                }
            }
            File.Move(FilePath, target);
            AppDataDir.Instance.Log($"存储文件损坏，已隔离：{FilePath}（{error.Message}）");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { /* 有意忽略：隔离移动失败（被占用/无权限）=> 仍按损坏处理，不阻断调用方 */ }
    }
}
