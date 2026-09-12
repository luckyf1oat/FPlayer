// 等价移植：rebuild/ai_player/lib/core/services/trakt_scrobbler.dart（133 行 Dart → C#）。
// 端点依据：SERVICE_API.md §5 —— 打卡只有 `POST /scrobble/start`、`/scrobble/pause`、`/scrobble/stop` 三个动作
//   （本文件只做状态机与阈值/去重判定，不直接发 HTTP；发送函数由 TraktService.ScrobbleAsync 注入）。
// 为什么需要状态机（原版注释，逐条保留）：
//   1. 内核每 5–10 秒回一次 progress，逐条转发会造成请求风暴并被 Trakt 限流；
//   2. Trakt 的语义是**动作变化**（开始/暂停/停止），不是位置采样；
//   3. progress < 1% 的采样应忽略（Trakt 官方建议，避免污染观看历史）。
// 规则：progress < MinPercent 且尚未开始 ⇒ 不发请求；仅在动作与上次不同时发送（去抖）；
//   stopped 或进度 ≥ WatchedPercent ⇒ stop（Trakt 侧 80% 即计入历史）；换片/换集（媒体身份变化）⇒ 重置状态。
// 说明：Dart 的「防抖」= 动作变化去重（无时间节流），本实现保持一致；未引入 Dart 没有的定时器/节流。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace AIPlayer.Shell.Services.Trakt;

/// <summary>
/// 打卡发送函数（对应 Dart <c>TraktScrobbleSender</c>，trakt_scrobbler.dart:18-22）。
/// 生产实现：<see cref="TraktService.AsScrobbleSender"/>（即 <see cref="TraktService.ScrobbleAsync"/>）；
/// 测试实现：记录调用并返回成功。
/// </summary>
public delegate Task<bool> TraktScrobbleSender(string action, TraktScrobbleMedia media, double progressPercent);

/// <summary>
/// Trakt 自动打卡状态机（对应 Dart <c>TraktScrobbler</c>，trakt_scrobbler.dart:24-133）。
/// 外壳接线：内核 <c>progress</c> 回调 → <see cref="OnProgressAsync"/>；<c>stopped</c> 回调 → <see cref="OnStoppedAsync"/>。
/// </summary>
public sealed class TraktScrobbler
{
    /// <summary>低于该百分比且尚未开始时不打卡（Trakt 官方建议 1%，trakt_scrobbler.dart:34）。</summary>
    public const double MinPercent = 1;

    /// <summary>达到该百分比视为「已看」（Trakt 侧 80% 即计入观看历史，trakt_scrobbler.dart:37）。</summary>
    public const double WatchedPercent = 80;

    private readonly TraktScrobbleSender _send;
    private readonly Action<string> _onLog;

    private string _key;
    private string _lastAction;
    private bool _started;
    private int _sent;

    public TraktScrobbler(TraktScrobbleSender send, Action<string> onLog = null)
    {
        _send = send ?? throw new ArgumentNullException(nameof(send));
        _onLog = onLog;
    }

    /// <summary>新增便利：直接用 <see cref="TraktService"/> 构造（等价 Dart 生产环境传 <c>TraktService.scrobble</c>）。</summary>
    public static TraktScrobbler ForService(TraktService service, Action<string> onLog = null)
    {
        if (service == null) throw new ArgumentNullException(nameof(service));
        return new TraktScrobbler(service.AsScrobbleSender(), onLog);
    }

    /// <summary>已成功发出的打卡次数（诊断用，trakt_scrobbler.dart:62）。</summary>
    public int SentCount => _sent;

    /// <summary>最近一次动作（<c>start</c>/<c>pause</c>/<c>stop</c>），未发送过为 <c>null</c>。</summary>
    public string LastAction => _lastAction;

    /// <summary>是否已发出过 <c>start</c>（trakt_scrobbler.dart:67）。</summary>
    public bool HasStarted => _started;

    /// <summary>当前媒体身份键（新增：诊断/换片检测用；Dart 无公开 getter）。</summary>
    public string CurrentKey => _key;

    /// <summary>
    /// 稳定身份键（对应 Dart <c>keyOf</c>，trakt_scrobbler.dart:40-48）：外部 id 优先，退化为标题 + 季集号。
    /// 逐段拼接顺序与分隔符（<c>|</c>）与 Dart 完全一致。
    /// </summary>
    public static string KeyOf(TraktScrobbleMedia media)
    {
        if (media == null) return string.Empty;

        var parts = new List<string> { media.Type ?? string.Empty };
        if (!string.IsNullOrEmpty(media.Imdb)) parts.Add("imdb:" + media.Imdb);
        if (!string.IsNullOrEmpty(media.Tmdb)) parts.Add("tmdb:" + media.Tmdb);
        if (!string.IsNullOrEmpty(media.Tvdb)) parts.Add("tvdb:" + media.Tvdb);
        parts.Add(media.Title ?? string.Empty);
        if (media.Season.HasValue) parts.Add("S" + media.Season.Value.ToString(CultureInfo.InvariantCulture));
        if (media.Episode.HasValue) parts.Add("E" + media.Episode.Value.ToString(CultureInfo.InvariantCulture));
        return string.Join("|", parts);
    }

    /// <summary>
    /// 进度百分比（对应 Dart <c>percentOf</c>，trakt_scrobbler.dart:51-54）：时长未知/非正 ⇒ 0（此时不参与阈值判定）。
    /// 容错：NaN/Infinity 归 0（Dart 的 clamp 会原样返回 NaN，但 NaN 无法序列化进 HTTP 体）。
    /// </summary>
    public static double PercentOf(double positionSeconds, double? durationSeconds)
    {
        if (durationSeconds == null || durationSeconds.Value <= 0) return 0;
        var percent = positionSeconds / durationSeconds.Value * 100;
        if (double.IsNaN(percent) || double.IsInfinity(percent)) return 0;
        return Math.Clamp(percent, 0, 100);
    }

    /// <summary>重置（对应 Dart <c>reset()</c>，trakt_scrobbler.dart:72-76）：切换媒体或停播后调用。</summary>
    public void Reset()
    {
        _key = null;
        _lastAction = null;
        _started = false;
    }

    /// <summary>
    /// <c>progress</c> 回调入口（对应 Dart <c>onProgress()</c>，trakt_scrobbler.dart:79-107）：按「开始 / 暂停」派发。
    /// <paramref name="isPaused"/> 在 Dart 侧为 required 具名参数，此处保持必填以防误用。
    /// </summary>
    public async Task OnProgressAsync(
        TraktScrobbleMedia media,
        double positionSeconds,
        bool isPaused,
        double? durationSeconds = null,
        double? minimumPercent = null)
    {
        if (media == null || !media.IsUsable) return;

        var key = KeyOf(media);
        if (!string.Equals(key, _key, StringComparison.Ordinal))
        {
            Reset();
            _key = key;
        }

        var percent = PercentOf(positionSeconds, durationSeconds);

        // 起步阶段（尚未达标）不打卡
        if (!_started && percent < (minimumPercent ?? MinPercent)) return;

        var action = isPaused ? "pause" : "start";

        // 未开始过就「暂停」⇒ 无意义，忽略
        if (string.Equals(action, "pause", StringComparison.Ordinal) && !_started) return;

        // 动作未变化 ⇒ 去抖
        if (string.Equals(action, _lastAction, StringComparison.Ordinal)) return;

        var ok = await _send(action, media, percent).ConfigureAwait(false);
        if (!ok) return;

        _lastAction = action;
        if (string.Equals(action, "start", StringComparison.Ordinal)) _started = true;
        _sent++;
        Log($"Trakt 打卡 {action}（{FormatPercent(percent)}%）");
    }

    /// <summary>
    /// <c>stopped</c> 回调入口（对应 Dart <c>onStopped()</c>，trakt_scrobbler.dart:110-132）：
    /// 收尾打卡（Trakt 据此更新进度/计入历史）。
    /// 注意与 <see cref="OnProgressAsync"/> 的差别（Dart 实证）：换片时只重置 <c>_started</c> 与身份键，
    /// **保留** <c>_lastAction</c>。
    /// </summary>
    public async Task OnStoppedAsync(
        TraktScrobbleMedia media,
        double positionSeconds,
        double? durationSeconds = null)
    {
        if (media == null || !media.IsUsable) return;

        var key = KeyOf(media);
        if (!string.Equals(key, _key, StringComparison.Ordinal))
        {
            _key = key;
            _started = false;
        }

        var percent = PercentOf(positionSeconds, durationSeconds);

        // 刚点开就退出（未达 1%）⇒ 忽略，避免产生「看了几秒」的记录
        if (!_started && percent < MinPercent) return;

        var ok = await _send("stop", media, percent).ConfigureAwait(false);
        if (!ok) return;

        _sent++;
        _lastAction = "stop";
        _started = false;
        var watched = percent >= WatchedPercent ? " · 已看" : string.Empty;
        Log($"Trakt 打卡 stop（{FormatPercent(percent)}%）{watched}");
    }

    private void Log(string message) => _onLog?.Invoke(message);

    /// <summary>等价 Dart <c>toStringAsFixed(1)</c>（定点一位小数，InvariantCulture）。</summary>
    private static string FormatPercent(double percent) => percent.ToString("F1", CultureInfo.InvariantCulture);
}
