using System;
using System.IO;
using System.Security.Cryptography;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;

namespace AIPlayer.Shell.KernelHost;

/// <summary>
/// t12 步骤 ③ 运行时闸门 + 内核 XBF 回填工具（两个环境变量各自独立、默认关闭＝零影响）。
///
/// <para>
/// 背景（2026-09-11 实测）：M1 把 reversed/MpvHost 的反编译**源码**编进本工程，而该内核
/// **0 个 .xaml 源文件**。但内核生成代码里有 9 处硬依赖（grep `ms-appx:///` 全量命中），
/// 例如 <c>WinUISample.Controls\PlayerOverlay.cs:3493</c>：
/// <code>
/// Uri resourceLocator = new Uri("ms-appx:///Controls/PlayerOverlay.xaml");
/// Application.LoadComponent(this, resourceLocator, ComponentResourceLocation.Application);
/// </code>
/// 这些 XBF **不在**本应用资源里（运行时实测 TARGET-FAIL = XamlParseException；
/// MRT 交叉验证 Files/Controls/PlayerOverlay.xbf = null，而阳性对照 Files/MainWindow.xbf = FOUND）。
/// 所幸原装 PRI <c>data\player\AIPlayer.MpvHost.pri</c> 里内嵌着这些 XBF 载荷
/// （实测 Files/Controls/PlayerOverlay.xbf = 21,885 字节），故可回填。
/// </para>
///
/// <para>
/// ⚠️ 回填的**同名陷阱**：内核请求的 9 个里有 2 个与外壳自己的 XAML 同名
/// （<c>App.xaml</c>、<c>MainWindow.xaml</c>）。盲目覆盖会让内核的
/// <c>WinUISample.App</c>/<c>WinUISample.MainWindow</c> 加载到**外壳**的那份 XAML
/// （x:Class 根类型不匹配）。故本工具把这两个标为 <c>HOLD</c>，只回填其余 7 个。
/// </para>
/// </summary>
public static class KernelLoadGate
{
    /// <summary>置任意非空值即运行闸门（不设＝零影响）。</summary>
    public const string EnvVar = "SHELL_SELFTEST_LOADCOMPONENT";

    /// <summary>置一个目标目录即把内核 XBF 回填到该目录（不设＝零影响）。</summary>
    public const string ExtractEnvVar = "SHELL_XBF_EXTRACT";

    private const string TargetUri = "ms-appx:///Controls/PlayerOverlay.xaml";
    private const string ControlUri = "ms-appx:///MainWindow.xaml";

    private const string KernelPriInBin = "AIPlayer.MpvHost.pri";
    private const string KernelPriInData = @"E:\AI Player\data\player\AIPlayer.MpvHost.pri";

    /// <summary>与外壳自带 XAML 同名 ⇒ 不得直接回填（会让内核类型加载到外壳的 XAML）。</summary>
    private static readonly string[] HeldBack = { "App.xaml", "MainWindow.xaml" };

    /// <summary>内核生成代码请求的全部 XAML 资源（grep `ms-appx:///` 全量命中，9 处）。</summary>
    private static readonly string[] KernelXamlUris =
    {
        "Controls/PlayerOverlay.xaml",
        "Controls/RootLayout.xaml",
        "Controls/DanmakuSearchDialog.xaml",
        "Controls/DanmakuSettingsDialog.xaml",
        "Controls/SubtitleSearchDialog.xaml",
        "Controls/SubtitleSyncDialog.xaml",
        "Pages/LocalVideoPage.xaml",
        "App.xaml",
        "MainWindow.xaml",
    };

    private sealed class ProbeComponent : IComponentConnector
    {
        public int ConnectCount;

        public void Connect(int connectionId, object target)
        {
            ConnectCount++;
        }

        /// <summary>WinUI 3 的 IComponentConnector 还要求这一句（生成代码里常驻，返回 null）。缺它 ⇒ CS0535。</summary>
        public IComponentConnector GetBindingConnector(int connectionId, object target)
        {
            return null;
        }
    }

    public static void RunIfRequested()
    {
        RunGateIfRequested();
        RunExtractIfRequested();
    }

    // ============================ 模式 A：运行时闸门 ============================

    private static void RunGateIfRequested()
    {
        var flag = Environment.GetEnvironmentVariable(EnvVar);
        if (string.IsNullOrEmpty(flag))
        {
            return;
        }

        Program.Log("SELFTEST-LOADCOMPONENT begin " + EnvVar + "=" + flag);

        try
        {
            // ⚠️ 不写死程序集名：M1 下内核类型在本程序集（`AIPlayer.Shell`），
            //    M2 下在 `AIPlayer.MpvHost`（`data\player` 的原版成品）⇒ 用无限定名搜索，
            //    再打印**实际装载路径 + sha256_12**（captain 事实表要求：来源表必须配运行时可观察量，
            //    不能只看"文件在盘上"；`t16` 的反控就靠这一行翻转）。
            var t = Type.GetType("WinUISample.Controls.PlayerOverlay", throwOnError: false);
            if (t == null)
            {
                Program.Log("SELFTEST-LOADCOMPONENT kernel-type WinUISample.Controls.PlayerOverlay = NOT-FOUND");
            }
            else
            {
                var asm = t.Assembly;
                var loc = asm.Location;
                var hash = "<no-location>";
                var size = -1;
                try
                {
                    if (!string.IsNullOrEmpty(loc) && System.IO.File.Exists(loc))
                    {
                        var fi = new System.IO.FileInfo(loc);
                        size = (int)fi.Length;
                        using (var sha = System.Security.Cryptography.SHA256.Create())
                        using (var fs = System.IO.File.OpenRead(loc))
                        {
                            hash = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", string.Empty).Substring(0, 12);
                        }
                    }
                }
                catch (Exception ex2)
                {
                    hash = "<" + ex2.GetType().Name + ">";
                }

                // SELFTEST-LOADCOMPONENT KERNEL-ORIGIN 是本闸门给 t16 的"逐程序集来源表"证据行
                Program.Log("SELFTEST-LOADCOMPONENT kernel-type = FOUND assembly=" + asm.GetName().Name
                    + " version=" + asm.GetName().Version);
                Program.Log("SELFTEST-LOADCOMPONENT KERNEL-ORIGIN path=" + (string.IsNullOrEmpty(loc) ? "<none>" : loc)
                    + " bytes=" + size + " sha256_12=" + hash);
            }
        }
        catch (Exception ex)
        {
            Program.Log("SELFTEST-LOADCOMPONENT kernel-type THREW " + ex.GetType().FullName + ": " + ex.Message);
        }

        // 阳性对照（必须成功，否则本闸门结论无效）
        TryLoad("CONTROL", ControlUri);
        // 目标
        TryLoad("TARGET", TargetUri);
        // 独立交叉验证（不经过 LoadComponent）
        TryResourceMap();
        // 内核原装 PRI 是否含载荷
        TryKernelPri();
        // 元数据提供器串接判定：内核 XBF 解析需要内核的 XamlTypeInfo 被 Application 串接
        TryMetadataProviders();
        // 真类型实例化：组件即 XBF 根类型 ⇒ 这正是内核生产路径（ctor 内调 InitializeComponent）
        TryRealControlInstantiation();

        Program.Log("SELFTEST-LOADCOMPONENT end");
    }

    private static void TryLoad(string label, string uri)
    {
        var probe = new ProbeComponent();
        try
        {
            Application.LoadComponent(probe, new Uri(uri), ComponentResourceLocation.Application);
            Program.Log("SELFTEST-LOADCOMPONENT " + label + "-OK uri=" + uri + " ConnectCount=" + probe.ConnectCount);
        }
        catch (Exception ex)
        {
            // 只记 Message 会丢掉真实原因（XamlParseException 的外层 Message 恒为 "XAML parsing failed."）
            Program.Log("SELFTEST-LOADCOMPONENT " + label + "-FAIL uri=" + uri + " "
                + ex.GetType().FullName + ": " + ex.Message);
            var depth = 0;
            for (var inner = ex.InnerException; inner != null && depth < 5; inner = inner.InnerException, depth++)
            {
                Program.Log("SELFTEST-LOADCOMPONENT " + label + "-INNER[" + depth + "] "
                    + inner.GetType().FullName + ": " + inner.Message);
            }

            Program.Log("SELFTEST-LOADCOMPONENT " + label + "-HRESULT 0x"
                + ex.HResult.ToString("X8") + " StackTop=" + FirstLine(ex.StackTrace));

            // ⚠️ 不打印 XamlParseException.LineNumber/LinePosition：WinAppSDK 1.8 的
            //    Microsoft.UI.Xaml.Markup.XamlParseException **没有**这两个属性（实测 CS1061）。
            Program.Log("SELFTEST-LOADCOMPONENT " + label + "-TOSTRING " + FirstLine(ex.ToString()));
        }
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "<none>";
        }

        var idx = text.IndexOf('\n');
        return idx < 0 ? text.Trim() : text.Substring(0, idx).Trim();
    }

    /// <summary>
    /// 内核 XBF 里的类型要靠运行中 Application 的 IXamlMetadataProvider 解析。
    /// 外壳的生成代码只会串接**其它程序集**的 XamlMetaDataProvider
    /// （obj\…\XamlTypeInfo.g.cs L3440-3457：Richasy.MpvKernel.WinUI / WinUIEx / Sizers 等 5 个），
    /// 而内核的 WinUISample.AIPlayer_MpvHost_XamlTypeInfo 现在被编进**同一个**程序集
    /// ⇒ 若它没被串接，内核 XBF 就会解析失败。本段用两条判据证实/证伪。
    /// </summary>
    private static void TryMetadataProviders()
    {
        const string probeType = "WinUISample.Controls.PlayerOverlayBase";

        try
        {
            var appProvider = Application.Current as IXamlMetadataProvider;
            var viaApp = appProvider?.GetXamlType(probeType);
            Program.Log("SELFTEST-LOADCOMPONENT PROVIDER app IXamlMetadataProvider="
                + (appProvider == null ? "NOT-IMPLEMENTED" : "ok")
                + " GetXamlType(" + probeType + ")=" + (viaApp == null ? "null" : viaApp.ToString()));
        }
        catch (Exception ex)
        {
            Program.Log("SELFTEST-LOADCOMPONENT PROVIDER app THREW " + ex.GetType().FullName + ": " + ex.Message);
        }

        try
        {
            var kp = Type.GetType("WinUISample.AIPlayer_MpvHost_XamlTypeInfo.XamlTypeInfoProvider, AIPlayer.Shell", throwOnError: false);
            Program.Log("SELFTEST-LOADCOMPONENT PROVIDER kernel-type=" + (kp != null ? "FOUND " + kp.FullName : "NOT-FOUND"));
            if (kp == null)
            {
                return;
            }

            var inst = Activator.CreateInstance(kp);
            var method = kp.GetMethod("GetXamlTypeByName") ?? kp.GetMethod("GetXamlType");
            Program.Log("SELFTEST-LOADCOMPONENT PROVIDER kernel-method=" + (method == null ? "NOT-FOUND" : method.Name));
            if (method == null)
            {
                return;
            }

            var result = method.Invoke(inst, new object[] { probeType });
            Program.Log("SELFTEST-LOADCOMPONENT PROVIDER kernel " + method.Name + "(" + probeType + ")="
                + (result == null ? "null" : result.ToString()));
        }
        catch (Exception ex)
        {
            Program.Log("SELFTEST-LOADCOMPONENT PROVIDER kernel THREW " + ex.GetType().FullName + ": " + ex.Message);
        }
    }

    /// <summary>
    /// 真类型实例化：若内核控件有无参构造，就直接实例化它。
    /// 其 ctor 内部会走 <c>InitializeComponent()</c>（= 内核生产路径，例如
    /// PlayerViewModel.cs:3665 的 <c>Window.SetUIElement(new PlayerOverlay(this))</c>），
    /// 此时 **组件就是 XBF 的根类型** —— 正好二分「上面 TARGET-FAIL 是因为我传的探针组件类型不对」
    /// 这一嫌疑。PlayerOverlay 需要 PlayerViewModel，故只用无参构造的其它内核控件做代表。
    /// </summary>
    private static void TryRealControlInstantiation()
    {
        foreach (var name in new[]
                 {
                     "WinUISample.Controls.RootLayout",
                     "WinUISample.Controls.PlayerOverlay",
                     "WinUISample.Controls.SubtitleSyncDialog",
                     "WinUISample.Controls.DanmakuSearchDialog",
                 })
        {
            try
            {
                var t = Type.GetType(name + ", AIPlayer.Shell", throwOnError: false);
                if (t == null)
                {
                    Program.Log("SELFTEST-LOADCOMPONENT REALITY " + name + " type=NOT-FOUND");
                    continue;
                }

                var ctor = t.GetConstructor(Type.EmptyTypes);
                Program.Log("SELFTEST-LOADCOMPONENT REALITY " + name + " parameterless-ctor=" + (ctor != null));
                if (ctor == null)
                {
                    continue;
                }

                var inst = ctor.Invoke(null);
                Program.Log("SELFTEST-LOADCOMPONENT REALITY " + name + " INSTANTIATED-OK "
                    + inst.GetType().FullName);
            }
            catch (Exception ex)
            {
                // TargetInvocationException 会包一层，取内层才是真因
                var inner = ex.InnerException ?? ex;
                Program.Log("SELFTEST-LOADCOMPONENT REALITY " + name + " FAIL "
                    + inner.GetType().FullName + ": " + inner.Message
                    + " | hresult=0x" + inner.HResult.ToString("X8"));
            }
        }
    }

    private static void TryResourceMap()
    {
        try
        {
            var rm = new Microsoft.Windows.ApplicationModel.Resources.ResourceManager();
            var map = rm.MainResourceMap;
            foreach (var name in new[]
                     {
                         "Controls/PlayerOverlay.xaml",
                         "Files/Controls/PlayerOverlay.xbf",
                         "PlayerOverlay.xaml",
                         "MainWindow.xaml",
                         "Files/MainWindow.xbf",
                     })
            {
                try
                {
                    var cand = map.TryGetValue(name);
                    Program.Log("SELFTEST-LOADCOMPONENT MRT name=" + name + " -> "
                        + (cand == null ? "null" : "FOUND valueAsString=" + Safe(cand)));
                }
                catch (Exception ex)
                {
                    Program.Log("SELFTEST-LOADCOMPONENT MRT name=" + name + " THREW "
                        + ex.GetType().FullName + ": " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Program.Log("SELFTEST-LOADCOMPONENT MRT-ROOT THREW " + ex.GetType().FullName + ": " + ex.Message);
        }
    }

    private static void TryKernelPri()
    {
        foreach (var pri in new[] { Path.Combine(AppContext.BaseDirectory, KernelPriInBin), KernelPriInData })
        {
            Program.Log("SELFTEST-LOADCOMPONENT KERNELPRI path=" + pri + " exists=" + File.Exists(pri));
            if (!File.Exists(pri))
            {
                continue;
            }

            try
            {
                var map = new Microsoft.Windows.ApplicationModel.Resources.ResourceManager(pri).MainResourceMap;
                foreach (var name in new[]
                         {
                             "Files/Controls/PlayerOverlay.xbf",
                             "Controls/PlayerOverlay.xbf",
                             "Files/PlayerOverlay.xbf",
                             "PlayerOverlay.xbf",
                         })
                {
                    try
                    {
                        var cand = map.TryGetValue(name);
                        if (cand == null)
                        {
                            Program.Log("SELFTEST-LOADCOMPONENT KERNELPRI name=" + name + " -> null");
                            continue;
                        }

                        var bytes = cand.ValueAsBytes;
                        Program.Log("SELFTEST-LOADCOMPONENT KERNELPRI name=" + name
                            + " -> FOUND bytes=" + (bytes == null ? -1 : bytes.Length));
                    }
                    catch (Exception ex)
                    {
                        Program.Log("SELFTEST-LOADCOMPONENT KERNELPRI name=" + name + " THREW "
                            + ex.GetType().FullName + ": " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Log("SELFTEST-LOADCOMPONENT KERNELPRI OPEN-FAIL " + ex.GetType().FullName + ": " + ex.Message);
            }
        }
    }

    private static string Safe(Microsoft.Windows.ApplicationModel.Resources.ResourceCandidate cand)
    {
        try
        {
            return cand.ValueAsString ?? "<null>";
        }
        catch (Exception ex)
        {
            return "<" + ex.GetType().Name + ">";
        }
    }

    // ============================ 模式 B：XBF 回填 ============================

    private static void RunExtractIfRequested()
    {
        var dest = Environment.GetEnvironmentVariable(ExtractEnvVar);
        if (string.IsNullOrEmpty(dest))
        {
            return;
        }

        Program.Log("XBF-EXTRACT begin dest=" + dest);

        Microsoft.Windows.ApplicationModel.Resources.ResourceMap map;
        try
        {
            var pri = File.Exists(Path.Combine(AppContext.BaseDirectory, KernelPriInBin))
                ? Path.Combine(AppContext.BaseDirectory, KernelPriInBin)
                : KernelPriInData;
            Program.Log("XBF-EXTRACT source-pri=" + pri + " exists=" + File.Exists(pri));
            if (!File.Exists(pri))
            {
                Program.Log("XBF-EXTRACT FAIL source pri missing");
                return;
            }

            map = new Microsoft.Windows.ApplicationModel.Resources.ResourceManager(pri).MainResourceMap;
        }
        catch (Exception ex)
        {
            Program.Log("XBF-EXTRACT FAIL open pri " + ex.GetType().FullName + ": " + ex.Message);
            return;
        }

        var shipped = 0;
        var held = 0;
        var missing = 0;

        foreach (var uri in KernelXamlUris)
        {
            var relative = uri.Substring(0, uri.Length - ".xaml".Length) + ".xbf";
            var resourceName = "Files/" + relative;
            var decision = Array.IndexOf(HeldBack, uri) >= 0 ? "HOLD" : "SHIP";

            try
            {
                var cand = map.TryGetValue(resourceName);
                var bytes = cand?.ValueAsBytes;
                if (bytes == null || bytes.Length == 0)
                {
                    missing++;
                    Program.Log("XBF-EXTRACT " + decision + " " + uri + " -> MISSING (" + resourceName + ")");
                    continue;
                }

                var target = Path.Combine(dest, relative.Replace('/', '\\'));
                Directory.CreateDirectory(Path.GetDirectoryName(target));

                // HOLD 的仍然抽出来（便于人工比对），但落到 dest 下的 _hold\ 子目录，绝不覆盖外壳自己的同名文件。
                if (decision == "HOLD")
                {
                    target = Path.Combine(dest, "_hold", relative.Replace('/', '\\'));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    held++;
                }
                else
                {
                    shipped++;
                }

                File.WriteAllBytes(target, bytes);

                string hash;
                using (var sha = SHA256.Create())
                {
                    hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).Substring(0, 16);
                }

                Program.Log("XBF-EXTRACT " + decision + " " + uri + " -> " + target
                    + " bytes=" + bytes.Length + " sha256_16=" + hash);
            }
            catch (Exception ex)
            {
                missing++;
                Program.Log("XBF-EXTRACT " + decision + " " + uri + " THREW "
                    + ex.GetType().FullName + ": " + ex.Message);
            }
        }

        Program.Log("XBF-EXTRACT end shipped=" + shipped + " held=" + held + " missing=" + missing);
    }
}
