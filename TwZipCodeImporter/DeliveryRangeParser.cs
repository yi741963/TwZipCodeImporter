using System.Text.RegularExpressions;

namespace TwZipCodeImporter;

public static class DeliveryRangeParser
{
    // 比對「單/雙 N 號以下」「單/雙 N 至 M 號」「單/雙 N 號以上」「單號」「雙號」「全」「N 號」
    private static readonly Regex RangePattern = new(
        @"^(?<oe>[單雙])?(?:\s*(?<min>\d+)\s*(?:號以上|至[-~～到\s]*(?<max>\d+)\s*號|號以下(?<max_b>\d+)?|號))?(?:\s*號)?$",
        RegexOptions.Compiled);

    // 以下/以上/至 的完整解析
    private static readonly Regex PatternAll = new(@"^全$", RegexOptions.Compiled);
    private static readonly Regex PatternOddOnly = new(@"^單號$", RegexOptions.Compiled);
    private static readonly Regex PatternEvenOnly = new(@"^雙號$", RegexOptions.Compiled);
    private static readonly Regex PatternBelow = new(@"^([單雙])\s*(\d+)\s*號以下$", RegexOptions.Compiled);
    private static readonly Regex PatternRange = new(@"^([單雙])\s*(\d+)\s*[至\-~～]\s*(\d+)\s*號$", RegexOptions.Compiled);
    private static readonly Regex PatternAbove = new(@"^([單雙])\s*(\d+)\s*號以上$", RegexOptions.Compiled);
    private static readonly Regex PatternSingle = new(@"^(\d+)\s*號$", RegexOptions.Compiled);

    public static (char OddEven, int? MinNo, int? MaxNo) Parse(string raw)
    {
        var text = Normalize(raw);

        if (string.IsNullOrWhiteSpace(text) || PatternAll.IsMatch(text))
            return ('A', null, null);

        if (PatternOddOnly.IsMatch(text))
            return ('S', null, null);

        if (PatternEvenOnly.IsMatch(text))
            return ('D', null, null);

        var m = PatternBelow.Match(text);
        if (m.Success)
        {
            char oe = m.Groups[1].Value == "單" ? 'S' : 'D';
            int max = int.Parse(m.Groups[2].Value);
            return (oe, null, max);
        }

        m = PatternRange.Match(text);
        if (m.Success)
        {
            char oe = m.Groups[1].Value == "單" ? 'S' : 'D';
            int min = int.Parse(m.Groups[2].Value);
            int max = int.Parse(m.Groups[3].Value);
            return (oe, min, max);
        }

        m = PatternAbove.Match(text);
        if (m.Success)
        {
            char oe = m.Groups[1].Value == "單" ? 'S' : 'D';
            int min = int.Parse(m.Groups[2].Value);
            return (oe, min, null);
        }

        m = PatternSingle.Match(text);
        if (m.Success)
        {
            int no = int.Parse(m.Groups[1].Value);
            return ('A', no, no);
        }

        // 無法解析 — 回傳 fallback，呼叫端負責記 warning
        return ('A', null, null);
    }

    public static bool IsKnownPattern(string raw)
    {
        var text = Normalize(raw);
        if (string.IsNullOrWhiteSpace(text)) return true;
        return PatternAll.IsMatch(text)
            || PatternOddOnly.IsMatch(text)
            || PatternEvenOnly.IsMatch(text)
            || PatternBelow.IsMatch(text)
            || PatternRange.IsMatch(text)
            || PatternAbove.IsMatch(text)
            || PatternSingle.IsMatch(text);
    }

    private static string Normalize(string raw)
    {
        // 全形數字 → 半形
        var s = raw.Trim();
        s = Regex.Replace(s, @"[０-９]", c => ((char)(c.Value[0] - '０' + '0')).ToString());
        // 統一「至」的各種寫法，含「N號至M號」→「N至M號」
        s = Regex.Replace(s, @"[－\-~～]", "至");
        // 移除多餘空白（先移再處理號至，避免空白干擾）
        s = Regex.Replace(s, @"\s+", "");
        // 「21號至39號」→「21至39號」（Excel 實際格式）
        s = Regex.Replace(s, @"(\d+)號至", "$1至");
        return s;
    }
}
