// t27-F-A 第 ③ 项的**分帧用例**（按 `shell/docs/VERIFY_PLAN_T35_T41.md` 第 8 行重做）。
//
// 判据原文（verifier 入库，与 t41 一致）：
//   「三偏移用例：**Content-Length=8400**、**3 字节 CJK 起于 8190 / 8191 / 8192** ⇒ 解码正确；
//     **ASCII 对照**必须同过。**ASCII 与 CJK 都过 ⇒ 用例没打在分帧上 ⇒ 判"用例无效"，重设计**。」
//
// 🔴 这条判据把我**第一版用例**否掉了（那一版把 `\r\n\r\n` 顶到 8190/8191/8192 —— 打的是"头边界"，
//    而读块 = 8192 ⇒ 头边界贴近读边界时**没有任何多字节字符被拆开**，所以 ASCII/CJK 都会"通过"，
//    正是判据要防的"用例无效"形态）。本文件按判据重做为**真正打在分帧上**的版本：
//
//   · 头结束于字节 8192 * 2 之前 → 头本身强制落在**前两个读块**里（headerLen ≈ 16354 > 2×8192 ⇒ 第 2 次补读）；
//   · 正文起于字节 8192 的整数倍处，**3 字节 CJK 的前若干字节刚好填满第一个读块**：
//       offset=8190 → CJK 占 [8190,8193)，块边界 8192 落在**字符中间**
//       offset=8191 → CJK 占 [8191,8194)，块边界落在**第 2 字节后**
//       offset=8192 → CJK 占 [8192,8195)，字符**整体在下一块**（同一场景的边界值）
//   · **必须用 TcpClient 手写原始字节**：`HttpClient` 会把请求体**变成 string 再编码**发出去
//     （也就是"发送侧"就把流改了）⇒ 那样一来自检测的是 HttpClient，不是我的读取器。
//   · **ASCII 对照**：同样三偏移，把 CJK 换成 3 个 ASCII 字节 ⇒ 若 ASCII 与 CJK 结果完全一样，
//     说明用例仍然没打在分帧上（判"用例无效"）；本用例给出**可判别的差异**：CJK 版正文含 U+FFFD 或
//     字节数不符即失败，ASCII 版本来就该全过。

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace AIPlayer.Shell.KernelHost;

/// <summary>分帧用例的原始 HTTP 客户端（不做任何字符串转换，字节进字节出）。</summary>
public static class FramingProbeClient
{
    /// <summary>判据指定的样本身份。</summary>
    public const string SampleValue = "t27fA";

    /// <summary>
    /// 判据指定的 `Content-Length` 值（`VERIFY_PLAN_T35_T41.md` 第 8 行原文：**Content-Length=8400**）。
    /// 三个偏移用例**都**用这个长度 —— 不然"三偏移"就变成了"三个不同大小"，无法互相比较。
    /// </summary>
    public const int ContentLengthPerCriterion = 8400;

    public sealed class ProbeResult
    {
        public int TargetOffset { get; set; }

        public bool IsAscii { get; set; }

        public int HeaderLen { get; set; }

        public int BodyByteLen { get; set; }

        public string BodyText { get; set; } = string.Empty;

        public string StatusLine { get; set; } = string.Empty;

        public string ResponseBody { get; set; } = string.Empty;

        public string Error { get; set; }

        /// <summary>正文里出现 U+FFFD（替换字符）⇒ 说明"字节被按字符读"破坏过。</summary>
        public bool HasReplacementChar => (BodyText ?? string.Empty).IndexOf('\uFFFD') >= 0;
    }

    /// <summary>
    /// 构造判据要求的原始请求体（UTF-8 字节）：**总长恒为 <see cref="ContentLengthPerCriterion"/>**，
    /// 3 字节标记**起于 <paramref name="markerByteOffset"/>**（= 判据的 8190/8191/8192）。
    /// <paramref name="withCjk"/>=false 时标记换成 3 个 ASCII 字节（判据要求的 ASCII 对照）。
    /// </summary>
    public static byte[] BuildBody(int markerByteOffset, bool withCjk, out int bodyLen)
    {
        bodyLen = ContentLengthPerCriterion;

        var preamble = Encoding.ASCII.GetBytes("{\"event\":\"stopped\",\"note\":\"");
        if (markerByteOffset < preamble.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(markerByteOffset),
                "偏移必须 >= 前缀长度 " + preamble.Length);
        }

        var headPad = markerByteOffset - preamble.Length;
        var marker = withCjk ? Encoding.UTF8.GetBytes("\u5B57") : Encoding.ASCII.GetBytes("zzz");
        var tailLen = ContentLengthPerCriterion - markerByteOffset - marker.Length;
        if (tailLen < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(markerByteOffset), "偏移过大，尾部放不下");
        }

        // 尾部：先闭合 note，再补 probe 字段，最后用空格补足到 Content-Length（JSON 里空格合法）
        var tailSeed = Encoding.ASCII.GetBytes("\",\"probe\":\"" + SampleValue + "\"");
        if (tailSeed.Length > tailLen)
        {
            throw new InvalidOperationException("尾部种子比可容纳空间还长（" + tailSeed.Length + " > " + tailLen + "）");
        }

        var body = new byte[ContentLengthPerCriterion];
        var at = 0;

        // ① 前缀（ASCII）
        for (var i = 0; i < preamble.Length; i++) body[at++] = preamble[i];
        // ② 填充到标记偏移（ASCII 'x'）
        for (var i = 0; i < headPad; i++) body[at++] = (byte)'x';
        // ③ 标记（3 字节；CJK 版即"跨块"的那一下）
        for (var i = 0; i < marker.Length; i++) body[at++] = marker[i];
        // ④ 尾部种子 + 空格补齐
        for (var i = 0; i < tailSeed.Length; i++) body[at++] = tailSeed[i];
        while (at < body.Length) body[at++] = (byte)' ';

        return body;
    }

    /// <summary>
    /// 发一个原始请求：<c>Content-Length</c> 精确等于正文字节数（判据要求 8400，由调用方选偏移达成），
    /// 头用填充顶到指定长度以控制"正文起于 8192 的哪个倍数"。
    /// </summary>
    public static ProbeResult Send(string host, int port, string basePath, int targetOffset, bool withCjk,
        int headerLen, int timeoutSeconds = 20)
    {
        var result = new ProbeResult { TargetOffset = targetOffset, IsAscii = !withCjk, HeaderLen = headerLen };
        try
        {
            var body = BuildBody(targetOffset, withCjk, out var bodyLen);
            result.BodyByteLen = bodyLen;
            result.BodyText = Encoding.UTF8.GetString(body);

            // 头：`POST <path>?<pad> HTTP/1.1` + 定长头行；总长精确等于 headerLen
            var suffix = " HTTP/1.1\r\n" +
                         "Host: 127.0.0.1\r\n" +
                         "Content-Type: application/json\r\n" +
                         "Content-Length: " + bodyLen + "\r\n" +
                         "Connection: close\r\n\r\n";
            var prefix = "POST " + basePath + "?";
            var padLen = headerLen - Encoding.ASCII.GetByteCount(prefix) - Encoding.ASCII.GetByteCount(suffix);
            if (padLen < 0)
            {
                result.Error = "headerLen 太小（需要 >= " + (Encoding.ASCII.GetByteCount(prefix)
                    + Encoding.ASCII.GetByteCount(suffix)) + "，给定 " + headerLen + "）";
                return result;
            }

            var head = Encoding.ASCII.GetBytes(prefix + new string(' ', padLen) + suffix);

            using var client = new TcpClient();
            client.Connect(host, port);
            using var stream = client.GetStream();
            stream.ReadTimeout = timeoutSeconds * 1000;
            stream.WriteTimeout = timeoutSeconds * 1000;

            // 头 = 3 × 8192 - 2 = 24574？由调用方给；这里只保证"字节原样写"
            stream.Write(head, 0, head.Length);
            // 正文**分两段写**：第一段故意只写 1 字节 ⇒ 强制服务端做补读（更接近真实分帧）
            stream.WriteByte(body[0]);
            stream.Write(body, 1, body.Length - 1);
            stream.Flush();

            var raw = ReadAll(stream, timeoutSeconds);
            var text = Encoding.ASCII.GetString(raw);
            var lineEnd = text.IndexOf("\r\n", StringComparison.Ordinal);
            result.StatusLine = lineEnd > 0 ? text.Substring(0, lineEnd) : text;

            var split = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            result.ResponseBody = split >= 0 && split + 4 <= text.Length ? text.Substring(split + 4) : string.Empty;
        }
        catch (Exception ex)
        {
            result.Error = ex.GetType().Name + ": " + ex.Message;
        }

        return result;
    }

    private static byte[] ReadAll(NetworkStream stream, int timeoutSeconds)
    {
        var buffer = new byte[4096];
        var all = new List<byte>();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            int n;
            try
            {
                n = stream.Read(buffer, 0, buffer.Length);
            }
            catch (IOException)
            {
                break;   // 对端关连接（本端点每个请求都 Connection: close）
            }

            if (n <= 0)
            {
                break;
            }

            for (var i = 0; i < n; i++) all.Add(buffer[i]);

            // 已有完整响应头就够判（204 无正文）
            var head = Encoding.ASCII.GetString(all.ToArray());
            if (head.Contains("\r\n\r\n"))
            {
                // 有正文的话再读一轮就够（本用例回应体为空）
                if (head.StartsWith("HTTP/1.1 204", StringComparison.Ordinal))
                {
                    break;
                }
            }
        }

        return all.ToArray();
    }
}
