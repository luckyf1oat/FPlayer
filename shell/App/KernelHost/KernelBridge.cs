using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.WinUIKernel.Share;
using Richasy.WinUIKernel.Share.Toolkits;
using WinUISample.ViewModels;

namespace AIPlayer.Shell.KernelHost;

/// <summary>
/// 复刻内核 DI 容器（内核 `GlobalDependencies.Initialize()` 是 internal，外壳不能调用）。
/// 依据 `reversed/MpvHost/WinUISample/GlobalDependencies.cs:25-66`：
///   CreateBuilder().AddSerilog().AddDispatcherQueue().AddShareToolkits().AddXamlRootProvider()
///   .AddSingleton&lt;AppViewModel&gt;().AddTransient&lt;PlayerViewModel&gt;()
/// 其中两个 toolkit 内核侧是 internal，改由 `ShellToolkits.cs` 提供等价实现。
///
/// 来源：自 `shell/Spike/KernelHost.cs`（108 行）搬迁（captain 裁决 ⑤，事实 91/92：
/// 进程内桥在 M1/M2 两条路线下字节级同构 —— `HostLaunchOptions` 两条路线都是 `public`，
/// `GlobalDependencies` 两条路线都靠反射注入）。
/// 与 Spike 版的**唯一差异**：① 命名空间 `AIPlayer.Spike` → `AIPlayer.Shell.KernelHost`；
/// ② 类名 `KernelHost` → `KernelBridge`（与其所在命名空间同名会造成解析歧义）；
/// ③ `Probe.Add(...)` → `Program.Log(...)`（外壳有统一的打码咽喉，不再有 Spike 的 Probe 列表）。
/// </summary>
public static class KernelBridge
{
    public static string AppDataRoot { get; private set; }

    public static RichasyKernel.Kernel Current { get; private set; }

    /// <summary>外壳的 <c>ISettingsToolkit</c> 实例（`<AppDataRoot>\player\settings.json` 的唯一读写点）。</summary>
    public static ShellSettingsToolkit SettingsToolkit { get; private set; }

    public static void Initialize()
    {
        // t245：根推导改走 `AppDataDir.Instance.Root`（与外壳其余落点同源）。
        // 改前 = `Path.Combine(<本地已知文件夹 API>, "AIPlayer")` —— 该 API 走 `SHGetKnownFolderPath`，
        // **不看 `AIPLAYER_APPDATA_ROOT`** ⇒ 沙箱覆盖对这里无效：
        // 设了覆盖的实例仍会读/写**真实根**的 `player\settings.json`（与 `LocalVideoPathSemantic` 同一破口族）。
        // 同源推导的唯一实现见 `shell/Services/Infra/AppDataDir.cs`（环境变量 → 便携 `data\` → 默认根）。
        AppDataRoot = AIPlayer.Shell.Services.Infra.AppDataDir.Instance.Root;
        Directory.CreateDirectory(Path.Combine(AppDataRoot, "player"));

        RichasyKernel.IKernelBuilder builder = RichasyKernel.Kernel.CreateBuilder();

        SettingsToolkit = new ShellSettingsToolkit();
        builder.Services.AddSingleton(DispatcherQueue.GetForCurrentThread());
        builder.Services.AddSingleton<ISettingsToolkit>(SettingsToolkit);
        builder.Services.AddSingleton<IAppToolkit>(sp =>
            new SharedAppToolkit(sp.GetRequiredService<ISettingsToolkit>()));
        builder.Services.AddSingleton<IFileToolkit, SharedFileToolkit>();
        builder.Services.AddSingleton<IFontToolkit, SharedFontToolkit>();
        builder.Services.AddSingleton<IXamlRootProvider, ShellXamlRootProvider>();
        builder.Services.AddLogging();
        builder.Services.AddSingleton<AppViewModel>();
        builder.Services.AddTransient<PlayerViewModel>();
        // 事实 107 F9：DI 清单**不能漏** LocalVideoPageViewModel（t13 本地视频屏要解析它）。
        // Spike 原版没有它 —— 这是搬迁时按事实补齐的一处（captain 2026-09-11 明确列为红线）。
        builder.Services.AddSingleton<LocalVideoPageViewModel>();

        // 内核用 `builder.Build()`（RichasyKernel.Core 里的扩展方法）。此处改用 Kernel 的公开构造函数
        // `Kernel(IServiceProvider)` —— 反射实测存在（RichasyKernel.Abstractions / Kernel），
        // 避免依赖未定位到的扩展方法所在命名空间。
        Current = new RichasyKernel.Kernel(builder.Services.BuildServiceProvider());

        // 反控制开关（沿用 Spike 口径，无需重建即可跑正/反两组对照）：
        //   SHELL_KERNEL_INJECT=0 → 跳过 KERNEL-INJECT，OpenVideoAsync 应复现**无栈 NRE**、栈首 GlobalDependencies.Get[T]
        //   未设 / 其他值        → 正常注入（默认行为）
        var inject = Environment.GetEnvironmentVariable("SHELL_KERNEL_INJECT");
        if (inject == "0")
        {
            Program.Log("KERNEL-INJECT SKIPPED (SHELL_KERNEL_INJECT=0) —— 反控制档，预期播放阶段抛无栈 NRE");
        }
        else
        {
            InjectIntoKernelStatics(Current);
        }
    }

    /// <summary>
    /// **进程内复用的关键一步。**
    /// 内核代码内部大量使用 `this.Get&lt;T&gt;()`（`GlobalDependencies.cs:95-98` 的扩展方法），
    /// 它读的是 `WinUISample.GlobalDependencies.Kernel` 这个 **internal 类的静态属性**。
    /// 外壳自建容器后若不把它指过去，内核任何走 `Get&lt;T&gt;()` 的路径都会 NRE —— 实测栈：
    ///   at WinUISample.GlobalDependencies.Get[T](Object ele)
    ///   at WinUISample.ViewModels.AppViewModel.OpenVideoAsync(...)
    /// 该类是 internal，外壳无法直接赋值，故用反射注入。
    /// </summary>
    private static void InjectIntoKernelStatics(RichasyKernel.Kernel kernel)
    {
        try
        {
            var t = typeof(AppViewModel).Assembly.GetType("WinUISample.GlobalDependencies");
            if (t == null)
            {
                Program.Log("KERNEL-INJECT: type not found");
                return;
            }

            const System.Reflection.BindingFlags sf =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Static;

            var prop = t.GetProperty("Kernel", sf);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(null, kernel);
                Program.Log("KERNEL-INJECT via property OK");
                return;
            }

            var field = t.GetField("<Kernel>k__BackingField", sf);
            if (field != null)
            {
                field.SetValue(null, kernel);
                Program.Log("KERNEL-INJECT via backing field OK");
                return;
            }

            Program.Log("KERNEL-INJECT: no settable member found");
        }
        catch (Exception ex)
        {
            Program.Log("KERNEL-INJECT-FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    public static AppViewModel GetAppViewModel() => Current.Services.GetRequiredService<AppViewModel>();
}
