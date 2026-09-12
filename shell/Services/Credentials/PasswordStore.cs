// 新写（对应原版 `src/core/services/password_store.dart`，DESIGN §4.1 #30 → Services/Credentials/PasswordStore.cs）。
// 依据：SERVICE_API.md §8 —— 原版凭据用 `flutter_secure_storage(+_windows)`；DESIGN §2 D5 指定 C# 侧用 **DPAPI**。
// 验收②：认证令牌与密码受保护存储，且**编辑服务器时必须能回填已存密码**（P1 零调用项 `passwordOf`）。
// 安全语义：不落明文；DPAPI 不可用时降级为混淆存储并**显式暴露** IsEncrypted/DegradedReason（不静默降级）。

using System;
using System.Collections.Generic;
using System.Linq;

namespace AIPlayer.Shell.Services.Credentials;

/// <summary>受保护凭据存储（DPAPI，当前用户作用域）。</summary>
public sealed class PasswordStore
{
    private static PasswordStore _instance;
    private static readonly object Gate = new object();

    private readonly SecureKvStore _vault;

    public PasswordStore(SecureKvStore vault)
    {
        _vault = vault ?? throw new ArgumentNullException(nameof(vault));
    }

    public static PasswordStore Instance
    {
        get
        {
            if (_instance != null) return _instance;
            lock (Gate)
            {
                return _instance ??= new PasswordStore(SecureKvStore.Instance);
            }
        }
    }

    /// <summary>便于测试：独立文件 + 独立实例。</summary>
    public static PasswordStore At(string credentialsFilePath) => new PasswordStore(SecureKvStore.At(credentialsFilePath));

    public SecureKvStore Vault => _vault;

    /// <summary>是否真正加密（DPAPI 生效）。</summary>
    public bool IsEncrypted
    {
        get
        {
            _vault.EnsureLoaded();
            return _vault.IsEncrypted;
        }
    }

    /// <summary>降级原因（供 UI 提示，不静默）。</summary>
    public string DegradedReason
    {
        get
        {
            _vault.EnsureLoaded();
            return _vault.DegradedReason;
        }
    }

    // ── 服务器密码 / 令牌（键与 ServerConfig.passwordKey 一致）──────────────

    public static string ServerPasswordKey(string serverId) => $"server.{serverId}.password";

    public static string ServerTokenKey(string serverId) => $"server.{serverId}.token";

    public static string ServerRefreshTokenKey(string serverId) => $"server.{serverId}.refreshToken";

    /// <summary>编辑服务器时回填已存密码（P1 零调用项 <c>passwordOf</c> 的凭据侧实现）。</summary>
    public string GetServerPassword(string serverId)
    {
        if (string.IsNullOrEmpty(serverId)) return null;
        return _vault.Read(ServerPasswordKey(serverId));
    }

    public void SetServerPassword(string serverId, string password)
    {
        if (string.IsNullOrEmpty(serverId)) return;
        if (string.IsNullOrEmpty(password))
        {
            _vault.Remove(ServerPasswordKey(serverId));
            return;
        }
        _vault.Write(ServerPasswordKey(serverId), password);
    }

    public void RemoveServerPassword(string serverId)
    {
        if (string.IsNullOrEmpty(serverId)) return;
        _vault.Remove(ServerPasswordKey(serverId));
    }

    public string GetServerToken(string serverId) => string.IsNullOrEmpty(serverId) ? null : _vault.Read(ServerTokenKey(serverId));

    public void SetServerToken(string serverId, string token)
    {
        if (string.IsNullOrEmpty(serverId)) return;
        if (string.IsNullOrEmpty(token))
        {
            _vault.Remove(ServerTokenKey(serverId));
            return;
        }
        _vault.Write(ServerTokenKey(serverId), token);
    }

    public string GetServerRefreshToken(string serverId) => string.IsNullOrEmpty(serverId) ? null : _vault.Read(ServerRefreshTokenKey(serverId));

    public void SetServerRefreshToken(string serverId, string token)
    {
        if (string.IsNullOrEmpty(serverId)) return;
        if (string.IsNullOrEmpty(token))
        {
            _vault.Remove(ServerRefreshTokenKey(serverId));
            return;
        }
        _vault.Write(ServerRefreshTokenKey(serverId), token);
    }

    /// <summary>删除某服务器的全部凭据（删服务器时调用）。</summary>
    public void RemoveServer(string serverId)
    {
        RemoveServerPassword(serverId);
        if (string.IsNullOrEmpty(serverId)) return;
        _vault.Remove(ServerTokenKey(serverId));
        _vault.Remove(ServerRefreshTokenKey(serverId));
    }

    // ── 通用密钥 ───────────────────────────────────────────────────────────

    public string GetSecret(string key) => _vault.Read(key);

    public void SetSecret(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (value == null)
        {
            _vault.Remove(key);
            return;
        }
        _vault.Write(key, value);
    }

    public void RemoveSecret(string key) => _vault.Remove(key);

    public IReadOnlyDictionary<string, string> AllSecrets() => _vault.All;

    public IReadOnlyList<string> Keys() => _vault.All.Keys.ToList();

    /// <summary>清除全部凭据（退出登录/重置）。</summary>
    public void ClearAll() => _vault.Clear();

    public void Save() => _vault.Flush();
}
