using TwZipCodeImporter;

namespace TwZipCodeImporter.Tests;

public class AddressParserTests
{
    [Theory]
    [InlineData("台北市中山區中山北路二段100號", "台北市", "中山區", "中山北路二段", 100)]
    [InlineData("新北市板橋區文化路一段26號",   "新北市", "板橋區", "文化路一段",   26)]
    [InlineData("高雄市鳳山區青年路二段123號",  "高雄市", "鳳山區", "青年路二段",   123)]
    [InlineData("宜蘭縣礁溪鄉中山路二段58號",   "宜蘭縣", "礁溪鄉", "中山路二段",   58)]
    [InlineData("彰化縣彰化市中正路一段9號",    "彰化縣", "彰化市", "中正路一段",   9)]
    public void Parse_StandardAddress(string raw, string city, string area, string road, int no)
    {
        var p = AddressParser.Parse(raw);
        Assert.Equal(city, p.CityName);
        Assert.Equal(area, p.AreaName);
        Assert.Equal(road, p.RoadName);
        Assert.Equal(no,   p.HouseNo);
    }

    [Fact]
    public void Parse_FullWidthDigits()
    {
        var p = AddressParser.Parse("台北市中山區中山北路二段１００號");
        Assert.Equal("台北市", p.CityName);
        Assert.Equal("中山區", p.AreaName);
        Assert.Equal(100, p.HouseNo);
    }

    [Fact]
    public void Parse_WithoutHouseNumber_StillParsesRoad()
    {
        var p = AddressParser.Parse("台北市中山區中山北路二段");
        Assert.Equal("台北市", p.CityName);
        Assert.Equal("中山區", p.AreaName);
        Assert.Equal("中山北路二段", p.RoadName);
        Assert.Null(p.HouseNo);
    }

    [Fact]
    public void Parse_Empty_ReturnsEmpty()
    {
        var p = AddressParser.Parse("");
        Assert.Equal("", p.CityName);
        Assert.Null(p.HouseNo);
    }
}
