// S5 体验设施（t33）：系统代理 —— 读 Windows 的 WinINET 代理设置（HKCU\...\Internet Settings），
// 产出内核 `--http-proxy=` 的值；未启用/未配置 ⇒ 返回 null（= 不加该参数，走内核默认直连）。
// 实现选择：直接用 advapi32 的注册表 API（**不引第三方包**，也不改 csproj 的引用面）。
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AIPlayer.Shell.Services.Infra;

/// <summary>WinINET 代理设置的一份快照。</summary>
public sealed class ProxySetting
{
    public bool Enabled { get; set; }

    public string Server { get; set; }

    public string Bypass { get; set; }

    /// <summary>该设置是否**实际可用**（启用且服务器非空）。</summary>
    public bool IsUsable => Enabled && !string.IsNullOrWhiteSpace(Server);

    public override string ToString()
        => $"ProxyEnable={Enabled} Server={(string.IsNullOrEmpty(Server) ? "-" : Server)} Bypass={(string.IsNullOrEmpty(Bypass) ? "-" : Bypass)}";
}

public static class WindowsProxy
{
    private const string SubKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private const uint HkeyCurrentUser = 0x80000001;
    private const uint KeyRead = 0x20019;
    private const uint RegSz = 1;
    private const uint RegDword = 4;

    /// <summary>读当前用户的代理设置；任何失败都退化成一个"未启用"的对象（**绝不抛**）。</summary>
    public static ProxySetting Read()
    {
        var setting = new ProxySetting();
        IntPtr key = IntPtr.Zero;
        try
        {
            if (RegOpenKeyExW(HkeyCurrentUser, SubKey, 0, KeyRead, out key) != 0) return setting;

            var dword = QueryDword(key, "ProxyEnable");
            setting.Enabled = dword.HasValue && dword.Value != 0;
            setting.Server = QueryString(key, "ProxyServer");
            setting.Bypass = QueryString(key, "ProxyOverride");
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException) { /* 有意忽略：注册表不可读（受限环境）=> 当作没有配代理 */ }
        finally
        {
            if (key != IntPtr.Zero) RegCloseKey(key);
        }
        return setting;
    }

    /// <summary>
    /// 内核参数：可用 ⇒ <c>--http-proxy=&lt;server&gt;</c>；否则 <c>null</c>（调用方**不加**该参数）。
    /// </summary>
    public static string ToKernelArgument(ProxySetting setting)
        => setting == null || !setting.IsUsable ? null : "--http-proxy=" + setting.Server;

    /// <summary>一步到位：读系统设置并产出内核参数（或 null）。</summary>
    public static string ResolveKernelArgument() => ToKernelArgument(Read());

    private static string QueryString(IntPtr key, string name)
    {
        var size = 0u;
        if (RegQueryValueExW(key, name, IntPtr.Zero, out var type, null, ref size) != 0 || size == 0) return null;
        if (type != RegSz && type != 2 /* REG_EXPAND_SZ */) { /* 仍尝试按字符串读 */ }
        var buffer = new byte[size];
        if (RegQueryValueExW(key, name, IntPtr.Zero, out type, buffer, ref size) != 0) return null;
        var text = Encoding.Unicode.GetString(buffer, 0, (int)size).TrimEnd('\0');
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static uint? QueryDword(IntPtr key, string name)
    {
        var size = 4u;
        var buffer = new byte[4];
        if (RegQueryValueExW(key, name, IntPtr.Zero, out _, buffer, ref size) != 0) return null;
        return BitConverter.ToUInt32(buffer, 0);
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegOpenKeyExW(uint hKey, string subKey, uint options, uint sam, out IntPtr result);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegQueryValueExW(IntPtr hKey, string valueName, IntPtr reserved, out uint type, byte[] data, ref uint dataSize);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern int RegCloseKey(IntPtr hKey);
}
