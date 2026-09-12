using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AIPlayer.Shell.KernelHost;

/// <summary>
/// **只读诊断**：把 `Microsoft.UI.Xaml.Application` 的真实 API 面枚举出来
/// （`Exiting` / `Exit` / `OnExit` 到底存不存在、什么形态）。
///
/// <para>为什么需要它：t27-F-A ⑤ 的退出挂点我**错过两次** ——
/// 第一次凭印象说"WinUI 3 没有退出事件"，第二次照 captain 转述写了 `Exiting += …` 得到 **CS0103**。
/// ⇒ 不再靠印象/转述：把宿主真实加载的那个 `Microsoft.UI.Xaml.dll` 反射一遍，按 **实测** 决定挂点。</para>
///
/// <para>开关：`SHELL_SELFTEST_PROBE_EXIT_API=1`（默认关闭 ⇒ 生产零影响）。结果写
/// <c>shell/Tests/evidence/t27-exitapi-probe.txt</c>。</para>
/// </summary>
public static class ExitApiProbe
{
    public const string EnvVar = "SHELL_SELFTEST_PROBE_EXIT_API";

    public static void RunIfRequested()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnvVar), "1", StringComparison.Ordinal))
        {
            return;
        }

        var lines = new System.Text.StringBuilder();
        lines.AppendLine("=== t27-F-A ⑤ 退出挂点：Application API 面实测枚举 ===");
        lines.AppendLine("utc = " + DateTime.UtcNow.ToString("O"));
        lines.AppendLine();

        try
        {
            var appType = typeof(Microsoft.UI.Xaml.Application);
            lines.AppendLine("typeof(Application).Assembly = " + appType.Assembly.FullName);
            lines.AppendLine("Assembly.Location = " + Safe(() => appType.Assembly.Location));
            lines.AppendLine();

            lines.AppendLine("--- events（全部）---");
            foreach (var e in appType.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                         .OrderBy(x => x.Name))
            {
                lines.AppendLine("  " + e.Name + " : " + e.EventHandlerType?.Name);
            }

            lines.AppendLine();
            lines.AppendLine("--- methods 名字里含 Exit ---");
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            foreach (var m in appType.GetMethods(flags).Where(x => x.Name.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0)
                         .OrderBy(x => x.Name))
            {
                var vis = m.IsPublic ? "public" : "non-public";
                var ps = string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name));
                lines.AppendLine("  " + vis + " " + m.Name + "(" + ps + ") virtual=" + m.IsVirtual + " static=" + m.IsStatic);
            }

            lines.AppendLine();
            lines.AppendLine("--- properties（名字含 Exit）---");
            foreach (var p in appType.GetProperties(flags).Where(x => x.Name.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                lines.AppendLine("  " + p.Name + " : " + p.PropertyType.Name);
            }

            lines.AppendLine();
            lines.AppendLine("--- 诊断结论 ---");
            var hasExiting = appType.GetEvent("Exiting") != null;
            var hasExitMethod = appType.GetMethods(flags).Any(m => m.Name == "Exit" && m.GetParameters().Length == 0);
            var hasOnExit = appType.GetMethods(flags).Any(m => m.Name == "OnExit");
            lines.AppendLine("  Application.Exiting 事件存在 = " + hasExiting);
            lines.AppendLine("  Application.Exit() 无参方法存在 = " + hasExitMethod);
            lines.AppendLine("  Application.OnExit 成员存在 = " + hasOnExit);
        }
        catch (Exception ex)
        {
            lines.AppendLine("PROBE FAIL " + ex.GetType().FullName + ": " + ex.Message);
        }

        Write(lines.ToString());
    }

    private static string Safe(Func<string> get)
    {
        try
        {
            return get() ?? "<null>";
        }
        catch (Exception ex)
        {
            return "<" + ex.GetType().Name + ">";
        }
    }

    private static void Write(string content)
    {
        try
        {
            // ⚠️ 证据扩展名必须 .txt（`.gitignore:65` 有 *.log）
            var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Tests", "evidence"));
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "t27-exitapi-probe.txt");
            File.WriteAllText(file, content, new System.Text.UTF8Encoding(false));
            Program.Log("EXITAPI-PROBE written " + file);
        }
        catch (Exception ex)
        {
            Program.Log("EXITAPI-PROBE write FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
