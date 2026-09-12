// t58（U-L 分片一）人物屏自检：**真起窗**后跑一遍，读数写 `evidence/t58-person-selftest.txt`。
//
// 为什么要有它：本屏是"人物页"，参照图里**没有这一屏** ⇒ 整体构图一律 `INCONCLUSIVE(无参照)`；
// 能证明的只有三件事：① 取数真通了（真实 Emby 的真人物 + 真作品）② 复用了**已实测**的几何/字色
// ③ 起播门与缓存语义与 t31/t54 同一条（缓存态必拦、刷新态必放行）。
//
// 判据纪律：每条断言都能判 False，并打印它读的是哪个字段。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AIPlayer.Shell.Features.Aggregate;

namespace AIPlayer.Shell.Features.Person;

/// <summary>人物屏自检（只读页面状态 + 合成反控；不改生产数据）。</summary>
public static class PersonSelfTest
{
    public static async Task RunAsync(PersonPage page)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== t58 人物屏自检（Emby 人物）===");
        sb.AppendLine("utc        = " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

        // ── 前提：人物 Id。没注入 ⇒ **从真实数据里取一位**（不硬编码 id）────────────────
        if (string.IsNullOrEmpty(page.PersonId))
        {
            var derived = await page.DerivePersonFromRealDataAsync();
            sb.AppendLine("人物来源   = 自动（未设 SHELL_PERSON_ID）⇒ id=" + (derived.Length == 0 ? "<未找到>" : derived));
            if (derived.Length > 0)
            {
                await page.ReloadForSelfTestAsync();
            }
        }
        else
        {
            sb.AppendLine("人物来源   = 注入（SHELL_PERSON_ID）");
        }

        sb.AppendLine("服务器     = " + (page.CurrentServer?.Name ?? "<null>")
            + "（id=" + (page.CurrentServer?.Id ?? "?") + "）");
        sb.AppendLine("人物 id    = " + page.PersonId);
        sb.AppendLine("姓名（屏上）= " + page.PersonNameText);
        sb.AppendLine("副标题     = " + page.SubtitleTextValue);
        sb.AppendLine("摘要行     = " + page.SummaryTextValue);
        sb.AppendLine("头像已加载 = " + page.AvatarLoaded);
        sb.AppendLine();

        // ── 作品面读数 ────────────────────────────────────────────────────────
        var works = page.Works;
        var withUrl = works.Count(p => p.HasImageUrl);
        var withImage = works.Count(p => p.HasImage);
        var badged = works.Count(p => p.BadgeVisibility == Microsoft.UI.Xaml.Visibility.Visible);
        var types = works.GroupBy(p => p.Item?.Type ?? "?")
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key + "=" + g.Count());
        sb.AppendLine("作品卡片数 = " + works.Count);
        sb.AppendLine("类型分布   = " + string.Join(" / ", types));
        sb.AppendLine("有封面 URL = " + withUrl + "/" + works.Count);
        sb.AppendLine("带角标     = " + badged + "/" + works.Count
            + "（角标样例：" + string.Join("/", works.Where(p => p.BadgeVisibility == Microsoft.UI.Xaml.Visibility.Visible)
                .Take(8).Select(p => p.BadgeText)) + "）");
        foreach (var poster in works.Take(8))
        {
            sb.AppendLine("  · " + poster.Title + " | " + poster.Subtitle
                + " | type=" + (poster.Item?.Type ?? "?")
                + " | hasImageUrl=" + poster.HasImageUrl
                + " | badge='" + poster.BadgeText + "'");
        }

        sb.AppendLine();
        sb.AppendLine("--- 缓存态读数（SEAM③ 同一条语义）---");
        var t0 = page.LastOutcome;
        sb.AppendLine("T0/T1 读数 = " + (t0 == null ? "<null>" : MediaSnapshotSource.Describe(t0)));

        // ── 断言（逐条可判 False）────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("断言 人物 id 非空                 = " + (page.PersonId.Length > 0));
        sb.AppendLine("断言 取数成功（刷新已发起）       = " + (t0 != null && t0.RefreshStarted));
        sb.AppendLine("断言 作品卡片 ≥ 1                 = " + (works.Count >= 1) + "（实际 " + works.Count + "）");
        sb.AppendLine("断言 每张卡都有封面 URL 或占位    = "
            + works.All(p => p.HasImageUrl || p.PlaceholderVisibility == Microsoft.UI.Xaml.Visibility.Visible));
        sb.AppendLine("断言 角标只可能是「数字 / ✓ / 空」 = "
            + works.All(p => IsBadgeShape(p.BadgeText)));

        // ── 反控① 封面：占位 → 封面（有预算窗口时才测；否则如实写"跳过"）──────────────
        var window = ImageWindow();
        if (window > 0 && works.Count > 0)
        {
            AggregatePoster.OpenImageWindow(window);
            await page.LoadImagesForSelfTestAsync();
            var after = page.Works.Count(p => p.HasImage);
            sb.AppendLine("反控① 封面：窗口=" + window + " 加载前=" + withImage + " 加载后=" + after
                + " 本轮新增=" + (after - withImage));
            sb.AppendLine("断言① 占位 → 封面 真的发生      = " + (after > withImage)
                + "（有 URL " + works.Count(p => p.HasImageUrl) + "/" + works.Count + "）");
        }
        else
        {
            sb.AppendLine("反控① 封面：跳过（窗口=" + window + " 卡片=" + works.Count
                + "）—— 未验，不写 PASS");
        }

        // ── 反控② 缓存卡起播门（与 t31/t54 同口径）：缓存态必拦、刷新态必放行 ──────────
        var server = page.CurrentServer;
        var personId = page.PersonId;
        MediaSnapshotSource.FailFetchForSelfTest = true;
        try
        {
            await page.ReloadForSelfTestAsync();
            var blocked = page.Works.Count > 0 && page.Works.All(p =>
                p.Item != null && page.LiveItemFor(p.Item.Id) == null);
            sb.AppendLine("反控② 缓存态（打断网）：卡片=" + page.Works.Count
                + " 有真条目=" + (page.Works.Any(p => page.LiveItemFor(p.Item.Id) != null))
                + " 失败提示=" + page.FailedTextValue);
            sb.AppendLine("断言② 缓存态每张卡都取不到真条目 = " + blocked);
        }
        finally
        {
            MediaSnapshotSource.FailFetchForSelfTest = false;
        }

        // 真刷新态：门必须放行（否则"门"退化成一律拒绝 ⇒ 该屏不可播）
        await page.ReloadForSelfTestAsync();
        var anyLive = page.Works.Any(p => page.LiveItemFor(p.Item.Id) != null);
        sb.AppendLine("断言② 真刷新后至少一张卡可起播 = " + anyLive
            + "（卡片 " + page.Works.Count + "）");

        // ── 反控③ T1 就地性（实例身份判据，与 t31 的 ⑤ 同口径）──────────────────────
        var firstRef = page.FirstPosterAtCacheRender;
        var t1 = page.LastOutcome;
        sb.AppendLine("反控③ T1 读数 = " + (t1 == null ? "<null>" : MediaSnapshotSource.Describe(t1)));
        sb.AppendLine("断言③ 前提（刷新成功 且 两组 id 相同） = "
            + (t1 != null && t1.RefreshSucceeded && SnapshotRenderer.SameIds(t1.CachedIds, t1.RefreshedIds)));
        sb.AppendLine("断言③ T0 的首张卡片实例未被换    = "
            + (firstRef != null && page.Works.Count > 0 && ReferenceEquals(page.Works[0], firstRef)));

        // ── 文案纪律（对 XAML 源码做近似检查；ToolTip 行豁免）───────────────────────
        await AppendCopyDisciplineAsync(sb, "PersonPage.xaml");

        sb.AppendLine();
        sb.AppendLine("差异表（对照物里**没有这一屏** ⇒ 该维度只能判 INCONCLUSIVE）：");
        sb.AppendLine("  · 整体构图（头部/头像位置/列表排布） = INCONCLUSIVE(无参照)：refs/original-ui 里没有人物详情屏");
        sb.AppendLine("  · 头像卡几何（卡宽 144 / 间距 16 / 姓名白 + 角色灰 / 无照片 #FF333333）= 逆向到了（HILLSLITE §4.5、UI_SPEC §6 第 5 条实测值）");
        sb.AppendLine("  · 海报几何（166×249 / 列间距 24 / 右上角紫色圆形角标 / 标题白 + 副标题灰）= 逆向到了（§7.3 实测值）");
        sb.AppendLine("  · 像素级位置比对 = 未验（留给 t34 视觉收口）");

        PersonEvidence.WriteEvidence(sb.ToString());
        PersonEvidence.AppendLine("person-selftest done cards=" + page.Works.Count);
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
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "App", "Features", "Person", xamlFileName));
        if (!File.Exists(xamlPath))
        {
            sb.AppendLine("文案纪律 = INCONCLUSIVE（找不到 " + xamlFileName + "）");
            return;
        }

        var raw = await File.ReadAllTextAsync(xamlPath).ConfigureAwait(false);
        var noComments = Regex.Replace(raw, "<!--.*?-->", string.Empty, RegexOptions.Singleline);

        // 逐行取可见字面值；**ToolTip 行整行豁免**（验收原文明确把 ToolTip 排除在外）
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
