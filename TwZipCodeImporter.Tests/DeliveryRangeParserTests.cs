using Xunit;
using TwZipCodeImporter;

namespace TwZipCodeImporter.Tests;

public class DeliveryRangeParserTests
{
    [Theory]
    // OddEven, MinNo, MaxNo, input
    [InlineData('A', null, null, "全")]
    [InlineData('S', null, null, "單號")]
    [InlineData('D', null, null, "雙號")]
    [InlineData('S', null, 19,   "單19號以下")]
    [InlineData('S', 21,   39,   "單21至39號")]
    [InlineData('S', 41,   null, "單41號以上")]
    [InlineData('D', null, 18,   "雙18號以下")]
    [InlineData('D', 20,   48,   "雙20至48號")]
    [InlineData('D', 50,   null, "雙50號以上")]
    [InlineData('A', 23,   23,   "23號")]
    [InlineData('A', null, null, "")]          // 空字串視同「全」
    public void Parse_KnownPatterns(char expectedOe, int? expectedMin, int? expectedMax, string input)
    {
        var (oe, min, max) = DeliveryRangeParser.Parse(input);
        Assert.Equal(expectedOe, oe);
        Assert.Equal(expectedMin, min);
        Assert.Equal(expectedMax, max);
    }

    [Theory]
    [InlineData("單 21 至 39 號")]    // 有空格
    [InlineData("單21~39號")]          // 波浪號
    [InlineData("單21-39號")]          // 半形橫線
    [InlineData("雙２０至４８號")]      // 全形數字
    public void Parse_NormalizedVariants(string input)
    {
        var (oe, min, max) = DeliveryRangeParser.Parse(input);
        Assert.NotEqual('\0', oe);
        Assert.True(min.HasValue || max.HasValue || oe == 'A');
    }

    [Fact]
    public void Parse_UnknownPattern_ReturnsAllA()
    {
        // 附號等特殊格式應回 ('A', null, null) 不 throw
        var (oe, min, max) = DeliveryRangeParser.Parse("附號全");
        Assert.Equal('A', oe);
        Assert.Null(min);
        Assert.Null(max);
    }

    [Theory]
    [InlineData("全",    true)]
    [InlineData("單號",  true)]
    [InlineData("雙號",  true)]
    [InlineData("23號",  true)]
    [InlineData("附號全", false)]
    [InlineData("巷全戶", false)]
    public void IsKnownPattern_Correct(string input, bool expected)
    {
        Assert.Equal(expected, DeliveryRangeParser.IsKnownPattern(input));
    }
}
