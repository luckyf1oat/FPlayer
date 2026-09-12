// t31（U-E 收藏 + 聚合视界）的**卡片视图模型**（两个屏共用）。
//
// 规格依据：`HILLSLITE_UI_ANALYSIS.md` 行 3 —— 「每张海报下有一条**进度条**（紫/蓝，观看进度）」
//            + 「`server`/`ServerB` 两组显示**占位图**（灰底 + 胶片图标）」。
// `UI_SPEC_SHELL.md` P4 —— 「跨服分组 + 进度条；**空源显示占位图不崩**」。
//
// [!] 两条实测得来的纪律（不是猜的）：
//   ① **封面必须异步加载**：`ImageUrl` 带 `api_key`，直接让 `Image.Source` 拿绝对 URL 会走 WinRT 自己的
//      网络栈（错误不可控）。这里用 `HttpClient` 取字节 → `BitmapImage.SetSourceAsync` ⇒ **失败可兜**，
//      且 `ImageFailed` 不再需要（异步路径先失败、界面已有占位）。
//   ② **占位是显式状态**：`HasImage=false` ⇒ XAML 画 `#333333` 底 + 胶片图标。
//      **绝不**用"留空"当占位（参照物里空源就是灰底+图标，不是空框）。

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AIPlayer.Shell.Services.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace AIPlayer.Shell.Features.Aggregate;

/// <summary>一张海报卡（收藏屏与聚合视界共用）。</summary>
public sealed class AggregatePoster : INotifyPropertyChanged
{
    private string _imageUrl;
    private BitmapImage _image;
    private bool _imageRequested;
    private bool _favorite;

    public AggregatePoster(EmbyItem item, ServerConfig server, string imageUrl, string badgeText, bool showProgress, double progressFraction)
    {
        Item = item;
        Server = server;
        _imageUrl = imageUrl;
        BadgeText = badgeText ?? string.Empty;
        ProgressFraction = Math.Clamp(progressFraction, 0, 1);
        ShowProgress = showProgress;
        _favorite = item?.UserData?.IsFavorite ?? false;
    }

    public EmbyItem Item { get; }

    /// <summary>归属服务器（聚合视界按它分组；收藏屏用它做角标来源说明）。</summary>
    public ServerConfig Server { get; }

    public string Title => string.IsNullOrWhiteSpace(Item?.Name) ? "(无名)" : Item.Name;

    public string Subtitle => MediaAggregator.SubtitleOf(Item);

    /// <summary>角标文本（`UserData.UnplayedItemCount` / `✓`）；空串 ⇒ 不画。</summary>
    public string BadgeText { get; }

    /// <summary>
/// [!] **未看集数角标**（`HILLSLITE_UI_ANALYSIS.md` 行 2 实测 +「单帧放大判读」）：
    /// 海报**右上角紫色圆形徽章** —— 数字 = **未看集数**（例 4/4/4/3/21/12），**`✓` = 已看完**。
    /// 数据源 = Emby `UserData.UnplayedItemCount` / `UserData.Played`（`EmbyModels.cs:15/:20`）。
    /// 拿不到数据（两者都为默认）⇒ **不画**，不猜数字。
    /// </summary>
    public static string UnplayedBadgeOf(EmbyItem item)
    {
        if (item?.UserData == null)
        {
            return string.Empty;
        }

        var unplayed = item.UserData.UnplayedItemCount ?? 0;
        if (unplayed > 0)
        {
            return unplayed.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return item.UserData.Played ? "\u2713" : string.Empty;   // ✓
    }

    public Visibility BadgeVisibility => string.IsNullOrEmpty(BadgeText) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>0..1；`ShowProgress=false` 时不画（规格：只有"继续播放"族的卡才需要进度条）。</summary>
    public double ProgressFraction { get; }

    /// <summary>海报内宽（= 166，实测值）；进度条填充宽按它换算，避免引入值转换器。</summary>
    public const double PosterInnerWidth = 166;

    /// <summary>进度条填充像素宽（0..166，再留 8px 内边距）。</summary>
    public double ProgressWidth => Math.Max(0, Math.Round((PosterInnerWidth - 8) * ProgressFraction));

    public bool ShowProgress { get; }

    public Visibility ProgressVisibility => ShowProgress ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>来源服务器名（跨服收藏卡右上角的来源角标）。</summary>
    public string SourceName => Server == null
        ? string.Empty
        : (string.IsNullOrWhiteSpace(Server.Name) ? Server.Id : Server.Name);

    public Visibility SourceBadgeVisibility
        => string.IsNullOrEmpty(SourceName) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>封面 URL（**含凭据，只进内存不出日志**）。</summary>
    public string ImageUrl => _imageUrl;

    /// <summary>封面是否已就绪；`false` ⇒ XAML 画占位（灰底 + 胶片图标）。</summary>
    public bool HasImage => _image != null;

    public Visibility PlaceholderVisibility => _image == null ? Visibility.Visible : Visibility.Collapsed;

    public BitmapImage Image
    {
        get => _image;
        private set
        {
            _image = value;
            Raise();
            Raise(nameof(HasImage));
            Raise(nameof(PlaceholderVisibility));
        }
    }

    /// <summary>收藏态（该屏的"加入/移出收藏"按钮用它，并会回写）。</summary>
    public bool IsFavorite
    {
        get => _favorite;
        set
        {
            if (_favorite == value) return;
            _favorite = value;
            Raise();
            Raise(nameof(FavoriteGlyph));
            Raise(nameof(FavoriteTooltip));
        }
    }

    /// <summary>收藏按钮字形（`♡` 未收藏 / `♥` 已收藏）。</summary>
    public string FavoriteGlyph => _favorite ? "\u2665" : "\u2661";

    public string FavoriteTooltip => _favorite ? "取消收藏" : "加入收藏";

    /// <summary>t103：卡片上的**显式播放入口**字形（`▶` U+25B6，非 emoji 面，H7 不受影响）。</summary>
    public string PlayGlyph => "\u25B6";

    /// <summary>t103：播放按钮的 tooltip —— 明确"点卡片进详情页、起播走这里"两种意图。</summary>
    public string PlayTooltip => "播放（点卡片是进详情页；选集与换源在详情页里）";

    /// <summary>异步取封面；幂等（每张卡只请求一次）。**永不抛异常**。</summary>
    public async Task EnsureImageAsync(string fallbackUrl = null)
    {
        if (_imageRequested)
        {
            return;
        }

        _imageRequested = true;
        var url = string.IsNullOrEmpty(_imageUrl) ? fallbackUrl : _imageUrl;
        if (string.IsNullOrEmpty(url))
        {
            AggregateDiagnostics.WriteNoImageUrl(Title);
            return;   // 无封面 ⇒ 保持占位
        }

        if (!TryTakeImageSlot())
        {
            AggregateDiagnostics.Write("poster-image-skip-budget title=" + Title
                + " used=" + _imageLoadsUsed + " window=" + _imageWindow);
            return;   // 取证预算用尽（生产不设该变量 ⇒ 不会走到这里）
        }

        try
        {
            // 取字节走**服务层单点** `ImageCacheManager.GetOrFetchAsync(url)`（t155）：
            // ① 命中 ⇒ **不联网**（本地磁盘缓存；键 = URL 经 `NormalizeKey`，同图不同尺寸/不同 tag 不互相折叠）；
            // ② 未命中才真发请求 —— 由它内部那条 `ShellHttpClient` 取回（统一 UA / 代理 / 重试）并**落盘**，
            //    超时用**图片专用** deadline（比通用 20 s 短），失败返回 `null` + 一行可见 `IMG-CACHE …`（不抛）；
            // ③ 于是"海报"与首页/详情/人物头像走**同一份缓存与同一套 UA/代理口径**，不再各处 `new ShellHttpClient()` 自取。
            var bytes = await AIPlayer.Shell.Services.Infra.ImageCacheManager.Default
                .GetOrFetchAsync(url)
                .ConfigureAwait(true);
            if (bytes == null || bytes.Length == 0)
            {
                return;
            }

            var bitmap = await Shared.ImageSourceLoader.LoadAsync(bytes).ConfigureAwait(true);
            if (bitmap != null)
            {
                Image = bitmap;
            }
            else
            {
                AggregateDiagnostics.Write("poster-image-decode-null title=" + Title + " bytes=" + bytes.Length);
            }
        }
        catch (Exception ex)
        {
            // 封面失败**不影响列表**（占位照常显示）；只记类型，不记 URL；同类失败超过 3 条自动折叠
            AggregateDiagnostics.WriteImageFailure(
                "poster-image-fail server=" + (Server?.Name ?? "?") + " " + ex.GetType().Name);
        }
    }

    /// <summary>
    /// 封面加载上限（`SHELL_AGG_IMAGE_LIMIT`，默认 0 = 不限）。
    ///
    /// [!] **为什么需要它**（实测，不是理论）：封面是**逐张串行**取的（每张一次 HTTP + 一次位图解码）。
    /// 一台服务器 60 条 × 若干台 ⇒ 单屏 100+ 张 ⇒ 自检窗口被拖到 150 s 以上仍跑不完，
    /// 而"封面能加载"这件事的**信息量在头 N 张之后就不再增长**。
    /// ⇒ 取证时设小值（如 12）即可证明「占位 → 封面」的转换真的发生；
    ///   生产**不设**该变量 ⇒ 行为与设计完全一致（全量加载）。
    /// </summary>
    public static int ImageBudget
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable("SHELL_AGG_IMAGE_LIMIT");
            return int.TryParse(raw, out var limit) && limit > 0 ? limit : 0;
        }
    }

    private static int _imageLoadsUsed;

    /// <summary>已加载封面的张数（跨本进程内所有卡）。</summary>
    public static int ImageLoadsUsed => _imageLoadsUsed;

    /// <summary>
    /// 再放开 <paramref name="count"/> 个加载名额（自检专用）。
    /// 每次渲染都会重建卡片对象，但**全局预算**是跨全进程的 ⇒ 需要"再给一个独立窗口"才能
    /// 在某一屏上测到真实加载率（窗口内**不受**全局预算限制；窗口用尽后回到预算逻辑）。
    /// </summary>
    public static void OpenImageWindow(int count)
    {
        if (count > 0)
        {
            _imageWindow = count;
        }
    }

    private static int _imageWindow;

    /// <summary>本窗口是否还有名额（自检读数用）。</summary>
    public static int ImageWindowRemaining => _imageWindow;

    private static bool TryTakeImageSlot()
    {
        // 自检窗口优先（每次渲染都能再给一次额度）
        if (_imageWindow > 0)
        {
            _imageWindow--;
            _imageLoadsUsed++;
            return true;
        }

        var budget = ImageBudget;
        if (budget <= 0)
        {
            _imageLoadsUsed++;
            return true;
        }

        if (_imageLoadsUsed >= budget)
        {
            return false;
        }

        _imageLoadsUsed++;
        return true;
    }

    /// <summary>已有 URL 时不需要再算（自检用）。</summary>
    public bool HasImageUrl => !string.IsNullOrEmpty(_imageUrl);

    public event PropertyChangedEventHandler PropertyChanged;

    private void Raise([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
