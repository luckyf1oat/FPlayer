using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Richasy.WinUIKernel.Share;
using Richasy.WinUIKernel.Share.Toolkits;
using RichasyKernel;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using WinUISample.Extensions;
using WinUISample.Toolkits;
using WinUISample.ViewModels;

namespace WinUISample;

internal static class GlobalDependencies
{
	public static readonly LoggingLevelSwitch PlayerLogLevel = new LoggingLevelSwitch();

	public static Kernel Kernel { get; private set; }

	public static void Initialize()
	{
		if (Kernel == null)
		{
			Kernel = RichasyKernel.Kernel.CreateBuilder().AddSerilog().AddDispatcherQueue()
				.AddShareToolkits()
				.AddXamlRootProvider()
				.AddXamlRootProvider()
				.AddSingleton<AppViewModel>()
				.AddSingleton<LocalVideoPageViewModel>()
				.AddTransient<PlayerViewModel>()
				.Build();
			LogEventLevel minimumLevel = Kernel.GetRequiredService<ISettingsToolkit>().ReadLocalSetting("LogLevel", "information") switch
			{
				"debug" => LogEventLevel.Debug,
				"warning" => LogEventLevel.Warning,
				"error" => LogEventLevel.Error,
				_ => LogEventLevel.Information,
			};
			PlayerLogLevel.MinimumLevel = minimumLevel;
		}
	}

	public static IKernelBuilder AddDispatcherQueue(this IKernelBuilder builder)
	{
		DispatcherQueue forCurrentThread = DispatcherQueue.GetForCurrentThread();
		builder.Services.AddSingleton(forCurrentThread);
		return builder;
	}

	public static IKernelBuilder AddShareToolkits(this IKernelBuilder builder)
	{
		builder.Services.AddSingleton<IAppToolkit, SharedAppToolkit>().AddSingleton<ISettingsToolkit, DesktopSettingsToolkit>().AddSingleton<IFileToolkit, SharedFileToolkit>()
			.AddSingleton<IFontToolkit, SharedFontToolkit>();
		return builder;
	}

	public static IKernelBuilder AddXamlRootProvider(this IKernelBuilder builder)
	{
		builder.Services.AddSingleton<IXamlRootProvider, XamlRootProvider>();
		return builder;
	}

	public static IKernelBuilder AddSingleton<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IKernelBuilder kernelBuilder) where T : class
	{
		kernelBuilder.Services.AddSingleton<T>();
		return kernelBuilder;
	}

	public static IKernelBuilder AddTransient<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(this IKernelBuilder kernelBuilder) where T : class
	{
		kernelBuilder.Services.AddTransient<T>();
		return kernelBuilder;
	}

	/// <summary>日志行模板（唯一一份；与 t222 之前逐字相同）。</summary>
	private const string LogOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

	/// <summary>
	/// **sink 注册的唯一漏斗**（t222 / K-07「打码按 sink 而非按落盘」）：formatter 只能从
	/// `SecretMaskingTextFormatter.CreateForSink` 取（该类构造函数已私有 ⇒ 拿得到的必是**已打码**的那个），
	/// 而 sink 注册调用在整个 `kernel/src` 里只有本方法这一处 ⇒ **新增 sink 一律经本方法，默认就是打码的**
	/// （构造保证，而不是每个写点自己记得）。
	/// <para>⚠️ 这个"唯一"是**可机械核**的：`git grep -n -E 'WriteTo[.]File' -- kernel/src` 命中 = 1
	/// （注释里一律写成带方括号的形态，不自匹配）；谁绕开漏斗直接注册 sink，读数就从 1 变 2
	/// ⇒ 违规是**可见**的，不是隐形的。</para>
	/// </summary>
	private static LoggerConfiguration AddMaskedFileSink(this LoggerConfiguration configuration, string path)
	{
		return configuration.WriteTo.File(WinUISample.Services.SecretMaskingTextFormatter.CreateForSink(LogOutputTemplate), path, LogEventLevel.Verbose, 1073741824L, null, buffered: false, shared: true, null, RollingInterval.Infinite, rollOnFileSizeLimit: false, 31);
	}

	public static IKernelBuilder AddSerilog(this IKernelBuilder builder)
	{
		string text = Path.Combine(App.AppDataRoot, "logs");
		if (!Directory.Exists(text))
		{
			Directory.CreateDirectory(text);
		}
		Log.Logger = new LoggerConfiguration().MinimumLevel.ControlledBy(PlayerLogLevel)
			.AddMaskedFileSink(Path.Combine(text, $"log-player-{DateTimeOffset.Now:yyyy-MM-dd}.txt"))
			.CreateLogger();
		builder.Services.AddLogging(delegate (ILoggingBuilder b)
		{
			b.AddSerilog(null, dispose: true);
		});
		return builder;
	}

	public static T Get<T>(this object ele) where T : class
	{
		return Kernel.GetRequiredService<T>();
	}
}
