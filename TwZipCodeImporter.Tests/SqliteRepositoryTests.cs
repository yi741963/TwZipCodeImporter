using TwZipCodeImporter.Models;
using TwZipCodeImporter.Storage;

namespace TwZipCodeImporter.Tests;

public class SqliteRepositoryTests : IDisposable
{
    private readonly string _dbFile;
    private readonly string _connStr;

    public SqliteRepositoryTests()
    {
        _dbFile = Path.Combine(Path.GetTempPath(), $"zipcode_test_{Guid.NewGuid():N}.db");
        _connStr = $"Data Source={_dbFile}";
    }

    public void Dispose()
    {
        try { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); } catch { }
        if (File.Exists(_dbFile)) File.Delete(_dbFile);
    }

    private static List<Zone3Plus3Row> Sample()
    {
        return new List<Zone3Plus3Row>
        {
            new()
            {
                CityName = "台北市", AreaName = "中山區", Code6 = 104091,
                RoadName = "中山北路二段", DeliveryRangeRaw = "單1至99號",
                OddEven = 'S', MinNo = 1, MaxNo = 99, PostOffice = "中山郵局"
            },
            new()
            {
                CityName = "台北市", AreaName = "中山區", Code6 = 104092,
                RoadName = "中山北路二段", DeliveryRangeRaw = "雙2至100號",
                OddEven = 'D', MinNo = 2, MaxNo = 100
            },
            new()
            {
                CityName = "台北市", AreaName = "中山區", Code6 = 104101,
                RoadName = "民權東路一段", DeliveryRangeRaw = "全",
                OddEven = 'A',
            },
        };
    }

    [Fact]
    public async Task UpsertAndLookup()
    {
        var repo = new SqliteRepository(_connStr);
        await repo.EnsureSchemaAsync();

        var (ins1, upd1) = await repo.UpsertAsync(Sample());
        Assert.Equal(3, ins1);
        Assert.Equal(0, upd1);

        // 重複匯入應全部變 update
        var (ins2, upd2) = await repo.UpsertAsync(Sample());
        Assert.Equal(0, ins2);
        Assert.Equal(3, upd2);

        // 完整查詢
        var svc = new AddressLookupService(repo);

        var r1 = await svc.LookupAsync("台北市中山區中山北路二段25號");
        Assert.Single(r1.Matches);
        Assert.Equal(104091, r1.Matches[0].Code6); // 單號 25

        var r2 = await svc.LookupAsync("台北市中山區中山北路二段50號");
        Assert.Single(r2.Matches);
        Assert.Equal(104092, r2.Matches[0].Code6); // 雙號 50

        var r3 = await svc.LookupAsync("台北市中山區民權東路一段500號");
        Assert.Single(r3.Matches);
        Assert.Equal(104101, r3.Matches[0].Code6); // 全

        var r4 = await svc.LookupAsync("台北市中山區中山北路二段500號");
        Assert.Empty(r4.Matches);
        Assert.NotEmpty(r4.Candidates); // 同路段但門牌不在範圍
    }
}
