// t58（U-L 分片二）合集屏自检：真起窗跑一遍，读数写 `evidence/t58-collection-selftest.txt`。
//
// 判据纪律（逐条可判 False，并说明读的是哪个字段）：
//   · 合集内容为 0 时，必须同时给出**不加类型过滤**的对照读数 ⇒ 才能把"合集本身是空的"与
//     "我的类型过滤太窄"分开（少了对照，0 条读数无法归因，就不是证据）；
//   · 构图维度**无参照图** ⇒ 一律 `INCONCLUSIVE(无参照)`，不因"看着没问题"记 PASS。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AIPlayer.Shell.Features.Aggregate;

namespace AIPlayer.Shell.Features.Collection;

/// <summary>合集屏自检（只读页面状态 + 合成反控；不改生产数据）。</summary>
public static class CollectionSelfTest
{
    public static async Task RunAsync(CollectionPage page)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== t58 合集屏自检（Emby 合集 / BoxSet）===");
        sb.AppendLine("utc        = " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

        if (string.IsNullOrEmpty(page.BoxSetId))
        {
            var (derived, name, attempts) = await page.DeriveCollectionDetailedAsync();
            sb.AppendLine("合集来源   = 自动（未设 SHELL_COLLECTION_ID）⇒ id=" + (derived.Length == 0 ? "<未找到>" : derived)
                + (name.Length == 0 ? string.Empty : "（" + name + "）"));
            sb.AppendLine("探测矩阵   = " + string.Join(" | ", attempts));
            if (derived.Length > 0)
            {
                await page.ReloadForSelfTestAsync();
            }
        }
        else
        {
            sb.AppendLine("合集来源   = 注入（SHELL_COLLECTION_ID）");
        }

        sb.AppendLine("服务器     = " + (page.CurrentServer?.Name ?? "<null>")
            + "（id=" + (page.CurrentServer?.Id ?? "?") + "）");
        sb.AppendLine("合集 id    = " + page.BoxSetId);
        sb.AppendLine("屏上标题   = " + page.NameTextValue);
        sb.AppendLine("副标题     = " + page.SubtitleTextValue);
        sb.AppendLine("摘要行     = " + page.SummaryTextValue);
        sb.AppendLine();

        var items = page.Items;
        var withUrl = items.Count(p => p.HasImageUrl);
        var withImage = items.Count(p => p.HasImage);
        var badged = items.Count(p => p.BadgeVisibility == Microsoft.UI.Xaml.Visibility.Visible);
        var types = items.GroupBy(p => p.Item?.Type ?? "?")
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key + "=" + g.Count());
        sb.AppendLine("条目卡片数 = " + items.Count);
        sb.AppendLine("类型分布   = " + string.Join(" / ", types));
        sb.AppendLine("有封面 URL = " + withUrl + "/" + items.Count);
        sb.AppendLine("带角标     = " + badged + "/" + items.Count
            + "（角标样例：" + string.Join("/", items.Where(p => p.BadgeVisibility == Microsoft.UI.Xaml.Visibility.Visible)
                .Take(8).Select(p => p.BadgeText)) + "）");
        foreach (var poster in items.Take(8))
        {
            sb.AppendLine("  · " + poster.Title + " | " + poster.Subtitle
                + " | type=" + (poster.Item?.Type ?? "?")
                + " | hasImageUrl=" + poster.HasImageUrl
                + " | badge='" + poster.BadgeText + "'");
        }

        // ── 反控：0 条的归因对照（不加类型过滤）──────────────────────────────────
        if (!string.IsNullOrEmpty(page.BoxSetId))
        {
            var (filtered, unfiltered) = await page.ProbeChildrenCountsAsync();
            sb.AppendLine();
            sb.AppendLine("反控（归因对照）同一合集：加类型过滤=" + filtered + " 条 / 不加过滤=" + unfiltered + " 条");
            sb.AppendLine("断言 0 条可归因（有内容时无需归因；0 条时必须两读数为 0） = "
                + (items.Count > 0 || (filtered == 0 && unfiltered == 0)));
        }

        sb.AppendLine();
        sb.AppendLine("--- 缓存态读数（SEAM③ 同一条语义）---");
        var t0 = page.LastOutcome;
        sb.AppendLine("T0/T1 读数 = " + (t0 == null ? "<null>" : MediaSnapshotSource.Describe(t0)));
        sb.AppendLine();
        sb.AppendLine("断言 合集 id 非空                 = " + (page.BoxSetId.Length > 0));
        sb.AppendLine("断言 取数成功（刷新已发起）       = " + (t0 != null && t0.RefreshStarted));
        sb.AppendLine("断言 条目卡片 ≥ 1                 = " + (items.Count >= 1) + "（实际 " + items.Count + "）");
        sb.AppendLine("断言 每张卡都有封面 URL 或占位    = "
            + items.All(p => p.HasImageUrl || p.PlaceholderVisibility == Microsoft.UI.Xaml.Visibility.Visible));
        sb.AppendLine("断言 角标只可能是「数字 / ✓ / 空」 = " + items.All(p => IsBadgeShape(p.BadgeText)));

        // ── 反控① 封面：占位 → 封面（有预算窗口才测）────────────────────────────
        var window = ImageWindow();
        if (window > 0 && items.Count > 0)
        {
            AggregatePoster.OpenImageWindow(window);
            await page.LoadImagesForSelfTestAsync();
            var after = page.Items.Count(p => p.HasImage);
            sb.AppendLine("反控① 封面：窗口=" + window + " 加载前=" + withImage + " 加载后=" + after
                + " 本轮新增=" + (after - withImage));
            sb.AppendLine("断言① 占位 → 封面 真的发生      = " + (after > withImage));
        }
        else
        {
            sb.AppendLine("反控① 封面：跳过（窗口=" + window + " 卡片=" + items.Count + "）—— 未验，不写 PASS");
        }

        // ── 反控② 缓存卡起播门：缓存态必拦、刷新态必放行 ─────────────────────────
        MediaSnapshotSource.FailFetchForSelfTest = true;
        try
        {
            await page.ReloadForSelfTestAsync();
            var blocked = page.Items.Count > 0 && page.Items.All(p =>
                p.Item != null && page.LiveItemFor(p.Item.Id) == null);
            sb.AppendLine("反控② 缓存态（打断网）：卡片=" + page.Items.Count
                + " 失败提示=" + page.FailedTextValue);
            sb.AppendLine("断言② 缓存态每张卡都取不到真条目 = " + blocked);
        }
        finally
        {
            MediaSnapshotSource.FailFetchForSelfTest = false;
        }

        await page.ReloadForSelfTestAsync();
        sb.AppendLine("断言② 真刷新后至少一张卡可起播 = "
            + page.Items.Any(p => page.LiveItemFor(p.Item.Id) != null) + "（卡片 " + page.Items.Count + "）");

        // ── 反控③ T1 就地性 ─────────────────────────────────────────────────
        var firstRef = page.FirstPosterAtCacheRender;
        var t1 = page.LastOutcome;
        sb.AppendLine("反控③ T1 读数 = " + (t1 == null ? "<null>" : MediaSnapshotSource.Describe(t1)));
        sb.AppendLine("断言③ 前提（刷新成功 且 两组 id 相同） = "
            + (t1 != null && t1.RefreshSucceeded && SnapshotRenderer.SameIds(t1.CachedIds, t1.RefreshedIds)));
        sb.AppendLine("断言③ T0 的首张卡片实例未被换    = "
            + (firstRef != null && page.Items.Count > 0 && ReferenceEquals(page.Items[0], firstRef)));

        await AppendCopyDisciplineAsync(sb, "CollectionPage.xaml");

        sb.AppendLine();
        sb.AppendLine("差异表（对照物里**没有这一屏** ⇒ 该维度只能判 INCONCLUSIVE）：");
        sb.AppendLine("  · 整体构图（头部/标题位置/列表排布） = INCONCLUSIVE(无参照)：refs/original-ui 里没有合集详情屏");
        sb.AppendLine("  · 海报几何（166×249 / 列间距 24 / 右上角紫色圆形角标 / 标题白 + 副标题灰）= 逆向到了（§7.3、§4.5 实测值）");
        sb.AppendLine("  · 像素级位置比对 = 未验（留给 t34 视觉收口）");

        CollectionEvidence.WriteEvidence(sb.ToString());
        CollectionEvidence.AppendLine("collection-selftest done cards=" + page.Items.Count);
    }

    private static bool IsBadgeShape(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        if (text == "\u2713")
        {
            return true;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0;
    }

    private static int ImageWindow()
    {
        var raw = Environment.GetEnvironmentVariable("SHELL_AGG_IMAGE_WINDOW");
        return int.TryParse(raw, out var n) && n > 0 ? n : 0;
    }

    /// <summary>
    /// 可见文案纪律（验收第 4 条）：`\b[A-Z][A-Za-z]+Service\b` / `[A-Z]:\\` / `§\d` / `camelCase=` 一条都不许命中。
    /// ⚠ 口径（必须说清，否则判据会假红）：检查对象 = **可见文案**，即
    /// ① **先剥掉 XML 注释**（注释不是"可见文案"；本屏注释里大量引用  这类规格编号，属证据性说明）；
    ///   ② 再取 `Text=` / `Content=` / `Header=` / `PlaceholderText=` 的**字面值**；
    ///   ③ `ToolTipService.ToolTip` 按验收原文**豁免**。
    ///    这是对 XAML 源码的近似提取，**不是**渲染面逐字符取词 —— 证据里如实标注。
    /// </summary>
    private static async Task AppendCopyDisciplineAsync(StringBuilder sb, string xamlFileName)
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "App", "Features", "Collection", xamlFileName));
        if (!File.Exists(xamlPath))
        {
            sb.AppendLine("文案纪律 = INCONCLUSIVE（找不到 " + xamlFileName + "）");
            return;
        }

        var raw = await File.ReadAllTextAsync(xamlPath).ConfigureAwait(false);
        var noComments = Regex.Replace(raw, "<!--.*?-->", string.Empty, RegexOptions.Singleline);

        var visibleLiterals = new List<string>();
        foreach (var line in noComments.Split('\n'))
        {
            if (line.IndexOf("ToolTip", StringComparison.Ordinal) >= 0)
            {
                continue;
            }

            foreach (Match m in Regex.Matches(line, "(Text|Content|Header|PlaceholderText)=\"([^\"]*)\""))
            {
                visibleLiterals.Add(m.Groups[2].Value);
            }
        }

        var joined = string.Join("\n", visibleLiterals);
        sb.AppendLine("文案纪律 取词条数 = " + visibleLiterals.Count + "（已剥 XML 注释；ToolTip 行豁免）");
        var patterns = new[]
        {
            @"\b[A-Z][A-Za-z]+Service\b",
            @"[A-Z]:\\",
            @"§\d",
            @"camelCase=",
        };

        var total = 0;
        foreach (var pattern in patterns)
        {
            var hits = Regex.Matches(joined, pattern).Count;
            total += hits;
            sb.AppendLine("文案纪律 命中 " + pattern + " = " + hits);
        }

        sb.AppendLine("断言 文案纪律 命中总数 = 0        = " + (total == 0)
            + "（口径：XAML 可见文案近似提取 = 剥注释 + 取 Text/Content/Header 字面值，ToolTip 豁免）");
    }
}
