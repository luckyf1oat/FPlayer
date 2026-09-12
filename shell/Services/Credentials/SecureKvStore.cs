// 等价移植：rebuild/ai_player/lib/core/storage/secure_kv_store.dart（Dart `SecureKvStore`）。
// 机制：落盘 credentials.bin = DPAPI(UTF-8 JSON)（用户级 CryptProtectData，等价 flutter_secure_storage_windows）。
// DPAPI 不可用时自动降级为「可逆混淆」并在 IsEncrypted=false / DegradedReason 标记（不静默降级安全性）。
// Dart 侧用 dart:ffi 直调 Crypt32；C# 侧用 System.Security.Cryptography.ProtectedData（同一 Win32 API）。

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIPlayer.Shell.Services.Infra;

namespace AIPlayer.Shell.Services.Credentials;

/// <summary>加密键值存储（对应 Dart <c>SecureKvStore</c>）。</summary>
public sealed class SecureKvStore
{
    private const string ObfuscationMagic = "AIP-KV1:";
    private const int DefaultSalt = 0x5AF3;

    private static SecureKvStore _instance;
    private static readonly object Gate = new object();

    private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);
    private bool _loaded;
    private readonly object _ioGate = new object();

    public SecureKvStore(string filePath)
    {
        FilePath = filePath;
    }

    public static SecureKvStore Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Gate)
            {
                return _instance ??= new SecureKvStore(AppDataDir.Instance.CredentialsFile);
            }
        }
    }

    /// <summary>便于测试/多实例（对应 Dart 的 <c>SecureKvStore.at</c>）。</summary>
    public static SecureKvStore At(string filePath) => new SecureKvStore(filePath);

    public string FilePath { get; }

    /// <summary>是否真正加密（DPAPI 生效）。</summary>
    public bool IsEncrypted { get; private set; }

    /// <summary>降级原因（供 UI 提示）。</summary>
    public string DegradedReason { get; private set; }

    public void EnsureLoaded()
    {
        lock (_ioGate)
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                var raw = File.ReadAllBytes(FilePath);
                if (raw.Length == 0) return;

                var plain = Unprotect(raw);
                if (plain != null)
                {
                    IsEncrypted = true;
                    Merge(plain);
                    return;
                }

                var decoded = Deobfuscate(raw);
                if (decoded != null)
                {
                    IsEncrypted = false;
                    DegradedReason = "DPAPI 不可用，凭据以混淆方式存储（建议检查系统 Crypt32 可用性）";
                    Merge(decoded);
                }
            }
            catch (Exception ex)
            {
                AppDataDir.Instance.Log($"凭据读取失败: {ex.Message}");
            }
        }
    }

    private void Merge(byte[] plain)
    {
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(plain));
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
        _values.Clear();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            _values[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                ? prop.Value.GetString()
                : prop.Value.GetRawText().Trim('"');
        }
    }

    public string Read(string key)
    {
        EnsureLoaded();
        return _values.TryGetValue(key, out var v) ? v : null;
    }

    public IReadOnlyDictionary<string, string> All
    {
        get
        {
            EnsureLoaded();
            return new Dictionary<string, string>(_values, StringComparer.Ordinal);
        }
    }

    public void Write(string key, string value)
    {
        EnsureLoaded();
        _values[key] = value;
        Flush();
    }

    public void Remove(string key)
    {
        EnsureLoaded();
        _values.Remove(key);
        Flush();
    }

    /// <summary>清除全部凭据（退出登录/重置）。</summary>
    public void Clear()
    {
        EnsureLoaded();
        _values.Clear();
        Flush();
    }

    public void Flush()
    {
        lock (_ioGate)
        {
            var plain = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(_values));

            byte[] payload;
            var protectedBytes = Protect(plain);
            if (protectedBytes != null)
            {
                payload = protectedBytes;
                IsEncrypted = true;
                DegradedReason = null;
            }
            else
            {
                payload = Obfuscate(plain);
                IsEncrypted = false;
                DegradedReason ??= "DPAPI 不可用，凭据以混淆方式存储";
            }

            // t148：写盘收敛到唯一实现 `Infra.AtomicFile`（tmp + 覆盖式 Move；不再"先 Delete 再 Move"）。
            AtomicFile.WriteAllBytes(FilePath, payload);
        }
    }

    // ── DPAPI ──────────────────────────────────────────────────────────────

    /// <summary>DPAPI 是否可用（Windows 上恒为 true；其它平台返回 null）。</summary>
    public static bool DpapiSupported => OperatingSystem.IsWindows();

    /// <summary>用当前用户凭据加密；失败返回 <c>null</c>（对应 Dart <c>Dpapi.protect</c>）。</summary>
    public static byte[] Protect(byte[] plain)
    {
        if (!DpapiSupported) return null;
        try
        {
            return ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        }
        catch (Exception ex)   // 记录 + 降级：DPAPI 加密不可用或失败 => 返回 null，由调用方降级为混淆存储（DegradedReason 已记录）
        {
            AppDataDir.Instance.Log($"CRED-PROTECT-FAIL {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>解密；失败（换用户/换机器/被篡改）返回 <c>null</c>。</summary>
    public static byte[] Unprotect(byte[] cipher)
    {
        if (!DpapiSupported) return null;
        try
        {
            return ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
        }
        catch (Exception ex)   // 记录 + 降级：DPAPI 解密失败（换用户/换机/损坏）=> 返回 null，由调用方按「读不到凭据」处理
        {
            AppDataDir.Instance.Log($"CRED-UNPROTECT-FAIL {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    // ── 降级：可逆混淆（明确非加密强度）────────────────────────────────────
    // 算法与 Dart 侧逐字对齐：seed = "<0x5AF3 十进制>:<USERNAME>"，LCG(state*31+c) → x=(x*1103515245+12345)&0x7fffffff → (x>>16)&0xff。

    private static byte[] KeyStream(int length)
    {
        var seed = $"{DefaultSalt}:{Environment.GetEnvironmentVariable("USERNAME") ?? string.Empty}";
        var outBytes = new byte[length];
        int state = 0;
        foreach (var ch in seed)
        {
            state = unchecked(state * 31 + ch); // int 溢出即 Dart 的 & 0xffffffff 语义
        }
        long x = state;
        for (var i = 0; i < length; i++)
        {
            x = (x * 1103515245 + 12345) & 0x7fffffff;
            outBytes[i] = (byte)((x >> 16) & 0xff);
        }
        return outBytes;
    }

    private static byte[] Obfuscate(byte[] plain)
    {
        var ks = KeyStream(plain.Length);
        var xored = new byte[plain.Length];
        for (var i = 0; i < plain.Length; i++)
        {
            xored[i] = (byte)(plain[i] ^ ks[i]);
        }
        var magic = Encoding.UTF8.GetBytes(ObfuscationMagic);
        var b64 = Encoding.UTF8.GetBytes(Convert.ToBase64String(xored));
        var result = new byte[magic.Length + b64.Length];
        Buffer.BlockCopy(magic, 0, result, 0, magic.Length);
        Buffer.BlockCopy(b64, 0, result, magic.Length, b64.Length);
        return result;
    }

    private static byte[] Deobfuscate(byte[] raw)
    {
        try
        {
            var text = Encoding.UTF8.GetString(raw);
            if (!text.StartsWith(ObfuscationMagic, StringComparison.Ordinal)) return null;
            var body = Convert.FromBase64String(text.Substring(ObfuscationMagic.Length).Trim());
            var ks = KeyStream(body.Length);
            var result = new byte[body.Length];
            for (var i = 0; i < body.Length; i++)
            {
                result[i] = (byte)(body[i] ^ ks[i]);
            }
            return result;
        }
        catch (Exception ex)   // 记录 + 降级：混淆解码失败（长度/内容不符）=> 返回 null，由调用方按「格式不认」处理
        {
            AppDataDir.Instance.Log($"CRED-OBFUSCATE-DECODE-FAIL {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}
