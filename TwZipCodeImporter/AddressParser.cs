using System.Text.RegularExpressions;

namespace TwZipCodeImporter;

public class ParsedAddress
{
    public string CityName { get; set; } = "";
    public string AreaName { get; set; } = "";
    public string RoadName { get; set; } = "";
    public int? HouseNo { get; set; }
    public string Remainder { get; set; } = "";
}

public static class AddressParser
{
    // 縣市:??市 / ??縣;區域:??區 / ??鄉 / ??鎮 / ??市
    private static readonly Regex CityArea = new(
        @"^(?<city>.+?[市縣])(?<area>.+?(?:區|鄉|鎮|市))",
        RegexOptions.Compiled);

    // 第一個門牌數字+號 (允許「之X」「-X」附號,先以主號為主)
    private static readonly Regex FirstNo = new(
        @"(?<no>\d+)\s*號",
        RegexOptions.Compiled);

    public static ParsedAddress Parse(string raw)
    {
        var result = new ParsedAddress();
        if (string.IsNullOrWhiteSpace(raw)) return result;

        var s = Normalize(raw);

        var rest = s;
        var m = CityArea.Match(s);
        if (m.Success)
        {
            result.CityName = m.Groups["city"].Value;
            result.AreaName = m.Groups["area"].Value;
            rest = s.Substring(m.Length);
        }

        var noMatch = FirstNo.Match(rest);
        if (noMatch.Success)
        {
            result.RoadName = rest.Substring(0, noMatch.Index).Trim();
            result.HouseNo = int.Parse(noMatch.Groups["no"].Value);
            var after = noMatch.Index + noMatch.Length;
            result.Remainder = after < rest.Length ? rest.Substring(after).Trim() : "";
        }
        else
        {
            result.RoadName = rest.Trim();
        }

        return result;
    }

    private static string Normalize(string raw)
    {
        var s = raw.Trim();
        // 全形數字 → 半形
        s = Regex.Replace(s, @"[０-９]", c => ((char)(c.Value[0] - '０' + '0')).ToString());
        // 移除常見空白
        s = Regex.Replace(s, @"\s+", "");
        return s;
    }

    // 將地址中常見的「臺/台」與羅馬數字段 (一二三四) 視為等價
    public static IEnumerable<string> RoadNameCandidates(string roadName)
    {
        if (string.IsNullOrWhiteSpace(roadName)) yield break;
        yield return roadName;
        // 不做更多自動展開,留給 UI 顯示提示即可
    }
}
