using TwZipCodeImporter.Storage;

namespace TwZipCodeImporter;

public class AddressLookupResult
{
    public ParsedAddress Parsed { get; set; } = new();
    public IReadOnlyList<ZoneQueryResult> Matches { get; set; } = Array.Empty<ZoneQueryResult>();
    public IReadOnlyList<ZoneQueryResult> Candidates { get; set; } = Array.Empty<ZoneQueryResult>();
}

public class AddressLookupService
{
    private readonly IZipCodeRepository _repo;

    public AddressLookupService(IZipCodeRepository repo)
    {
        _repo = repo;
    }

    public async Task<AddressLookupResult> LookupAsync(string rawAddress, CancellationToken ct = default)
    {
        var parsed = AddressParser.Parse(rawAddress);
        var result = new AddressLookupResult { Parsed = parsed };

        if (string.IsNullOrEmpty(parsed.RoadName) && string.IsNullOrEmpty(parsed.CityName))
            return result;

        // 先取出該路段所有規則
        var city = string.IsNullOrEmpty(parsed.CityName) ? null : parsed.CityName;
        var area = string.IsNullOrEmpty(parsed.AreaName) ? null : parsed.AreaName;
        var road = string.IsNullOrEmpty(parsed.RoadName) ? null : parsed.RoadName;
        var candidates = await _repo.LookupAsync(city, area, road, parsed.HouseNo, ct);
        result.Candidates = candidates;

        // 比對門牌號:OddEven + Min/Max
        if (parsed.HouseNo.HasValue)
        {
            int no = parsed.HouseNo.Value;
            bool isOdd = no % 2 == 1;
            var matches = candidates.Where(c =>
            {
                if (c.OddEven == 'S' && !isOdd) return false;
                if (c.OddEven == 'D' && isOdd) return false;
                if (c.MinNo.HasValue && no < c.MinNo.Value) return false;
                if (c.MaxNo.HasValue && no > c.MaxNo.Value) return false;
                return true;
            }).ToList();
            result.Matches = matches;
        }
        else
        {
            // 無門牌號時,給出此路段所有 3+3 碼供使用者參考
            result.Matches = candidates;
        }

        return result;
    }
}
