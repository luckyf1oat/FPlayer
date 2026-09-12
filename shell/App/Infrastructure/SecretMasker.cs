// t99 / t132：外壳侧的凭据打码**转发口**（唯一实现已下沉到服务层 sink 入口）。
//
// 历史与归属（两卡收口后的最终形态）：
//   · `t99`（我）先把规则落在本文件（App 侧），覆盖门面路径（`Program.Log`）与 App 各调用点；
//   · `t132`（services）把**同一套规则**落到 `shell/Services/Logging/SecretMasking.cs`，并在**唯一 sink**
//     `PlayerLogService.Add` 入口调用 ⇒ **任何绕过门面的新调用点也自动被打码**（这才是"结构上不可能漏"）。
//   · 按两卡约定：**服务层那份是唯一实现**，本文件退化为转发 + App 侧取证钩子（规则不再有两份会漂的副本）。
//
// 🔴 保留本文件而不是删掉的理由：App 侧仍有 15+ 个调用点写 `Program.MaskSecrets(...)` / `SecretMasker.Mask(...)`，
//    它们经此转发即可，不需要逐个改；测试与证据行形态（`<masked len=N sha4=XXXX>`、`api_key=***`）逐字不变。

using System;
using System.Text;

namespace AIPlayer.Shell.Infrastructure;

/// <summary>App 侧的凭据打码入口（**转发**到服务层唯一实现 <c>Services.Logging.SecretMasking</c>）。</summary>
public static class SecretMasker
{
    /// <summary>打码占位串（与服务层同源）。</summary>
    public const string MaskToken = AIPlayer.Shell.Services.Logging.SecretMasking.MaskToken;

    /// <summary>打码一行（幂等、不抛）；实现见服务层 <c>SecretMasking.Mask</c>。</summary>
    public static string Mask(string s) => AIPlayer.Shell.Services.Logging.SecretMasking.Mask(s);

    /// <summary>值的可读替代 `&lt;masked len=N sha4=XXXX&gt;`（转发）。</summary>
    public static string Describe(string value) => AIPlayer.Shell.Services.Logging.SecretMasking.Describe(value);

    /// <summary>值的前 4 位十六进制指纹（转发）。</summary>
    public static string Sha4(string value) => AIPlayer.Shell.Services.Logging.SecretMasking.Sha4(value);

    /// <summary>`SHELL_SELFTEST_LOGMASK=1` ⇒ 往日志写**合成**凭据行，用于证明 sink 打码（见 <see cref="RunLogMaskSelfTestIfRequested"/>）。</summary>
    public const string SelfTestEnvVar = "SHELL_SELFTEST_LOGMASK";

    private static bool _selfTestRan;

    /// <summary>
    /// **反控自检（默认零影响）**：`SHELL_SELFTEST_LOGMASK=1` 时写三类**合成**行，用来证明打码真的落在落盘面上：
    /// <list type="bullet">
    /// <item><b>A 忘了打码的调用点</b>：**绕过** App 门面（直接 `DebugLog.Info`）⇒ 靠**服务层 sink** 兜住
    ///       （t132 之前这条是裸的，正是它证明了"逐点补漏不住"）。</item>
    /// <item><b>B 不该打码的行</b>：条目 id / 服务器 id / 端口 / `--subtitle-track=3` ⇒ 必须**逐字保留**（防打过头）。</item>
    /// <item><b>C/E 门面路径</b>：`Program.Log` 里带合成 `--http-header=` 与百分号编码 `api_key%3D` ⇒ 必须被打码。</item>
    /// </list>
    /// 全部值都是**合成占位**（`SYNTH-T99…`），不含任何真实凭据。
    /// </summary>
    public static void RunLogMaskSelfTestIfRequested()
    {
        if (_selfTestRan
            || !string.Equals(Environment.GetEnvironmentVariable(SelfTestEnvVar), "1", StringComparison.Ordinal))
        {
            return;
        }

        _selfTestRan = true;

        var fakePayload = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"name\":\"X-Emby-Token\",\"value\":\"SYNTH-T99-PLACEHOLDER\"}"));
        var fakeArgs = "--open=https://example.invalid/x --http-header=" + fakePayload
            + " --user-agent=SYNTH-T99 --subtitle-track=3";
        var keepLine = "SYNTH-KEEP|itemId=0123456789abcdef0123456789abcdef|serverId=vU9cHAh8ZJ5Xv7El|port=49153|--subtitle-track=3";

        try
        {
            // A：**故意绕过门面**（模拟"新增日志调用点忘了打码"）⇒ 只能靠 sink 兜住
            AIPlayer.Shell.Services.Logging.DebugLog.Info("SYNTH-UNMASKED|" + fakeArgs + "|manual-api_key=SYNTHT99TOKEN0123456789ABCDEF");

            // B：不该被打码的行（防打过头）
            AIPlayer.Shell.Services.Logging.DebugLog.Info(keepLine);

            // C：门面路径
            Program.Log("SYNTH-FACADE " + fakeArgs);

            // E：百分号编码形态（来自 `Features/Search/SearchLog.cs` 的既有规则，合并时不许丢）
            Program.Log("SYNTH-PERCENT|url=https://example.invalid/Images/Primary?maxHeight=300&api_key%3DSYNTHT99PERCENT0123456789ABCDEF&x=1");
        }
        catch (Exception ex)
        {
            Program.Log("SYNTH-LOGMASK FAIL " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
