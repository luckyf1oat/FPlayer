using System;
using System.Threading;
using System.Threading.Tasks;

namespace AIPlayer.Shell.Shell;

/// <summary>
/// 外壳**唯一**的图片取字节预算（t215：把预算与闸门从 `HomePage` 内部**提升为共享单点**）。
///
/// <para>为什么必须有全局预算（t152 ④ 的实测结论，不是审美）：① 真实代理下 N 路并发互相排队，单图反而变慢；
/// ② 更硬的：同一张图在多行/多屏重复出现时，两个 writer 同时写同一个缓存文件 ⇒ 实测
/// `IOException: The process cannot access the file '…\cache\images\&lt;sha1&gt;.cache'`，那张图当场丢失。</para>
///
/// <para>默认 **8**；`SHELL_HOME_IMG_BUDGET` 可覆盖（取证用；0/非法 ⇒ 回落 8）—— **环境变量名与默认值逐字沿用**
/// `HomePage.xaml.cs` 旧私有实现，避免出现"第二个预算常量"。</para>
///
/// <para>[!] 临时期限（t215 卡面要求写明）：本卡**不碰** `Features/Home/`（那一面被 `t196` 的 inScope 占着）
/// ⇒ 期间全仓存在**两个**闸门：`HomePage` 的私有 `ImageGate` 与本共享单点，**总并发上限暂为 8+8=16**。
/// `t196` 结单后由 captain 另开一张卡让 `HomePage` 改用本单点并删除其私有闸门。</para>
/// </summary>
public static class ImageFillBudget
{
    /// <summary>覆盖用的环境变量名（与 `HomePage.xaml.cs` 旧实现逐字一致）。</summary>
    public const string OverrideEnvVar = "SHELL_HOME_IMG_BUDGET";

    /// <summary>默认预算（与旧实现逐字一致）。</summary>
    public const int DefaultBudget = 8;

    /// <summary>本次进程生效的预算（进程内不变；日志里必须打这个值）。</summary>
    public static int Budget { get; } = ResolveBudget();

    private static readonly SemaphoreSlim Gate = new SemaphoreSlim(Budget);

    private static int _inFlight;
    private static int _inFlightMax;
    private static int _gateWaits;
    private static int _completed;

    /// <summary>当前在飞张数（供日志与断言）。</summary>
    public static int InFlight => Volatile.Read(ref _inFlight);

    /// <summary>本次进程观测到的在飞峰值（"并发真的发生"的判据）。</summary>
    public static int InFlightMax => Volatile.Read(ref _inFlightMax);

    /// <summary>进入闸门排队的累计次数（与旧实现的 `gateWaits` 同义）。</summary>
    public static int GateWaits => Volatile.Read(ref _gateWaits);

    /// <summary>走完闸门的累计张数（用于"改后真的跑过"的自证）。</summary>
    public static int Completed => Volatile.Read(ref _completed);

    private static int ResolveBudget()
    {
        var raw = Environment.GetEnvironmentVariable(OverrideEnvVar);
        return int.TryParse(raw, out var v) && v > 0 ? v : DefaultBudget;
    }

    /// <summary>
    /// 占一个预算位。**必须 Dispose 归还**（`using` 即可），失败路径也要归还 —— 否则预算会被漏掉的异常永久吃掉，
    /// 表现为"越用越慢"这种最难查的形态。
    /// </summary>
    public static async Task<IDisposable> AcquireAsync(CancellationToken ct = default)
    {
        Interlocked.Increment(ref _gateWaits);
        await Gate.WaitAsync(ct).ConfigureAwait(true);

        var inFlight = Interlocked.Increment(ref _inFlight);
        int seen;
        while (inFlight > (seen = Volatile.Read(ref _inFlightMax)))
        {
            if (Interlocked.CompareExchange(ref _inFlightMax, inFlight, seen) == seen) { break; }
        }

        return new Slot();
    }

    private sealed class Slot : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0) { return; }   // 幂等：重复 Dispose 不重复归还
            Interlocked.Increment(ref _completed);
            Interlocked.Decrement(ref _inFlight);
            Gate.Release();
        }
    }
}
