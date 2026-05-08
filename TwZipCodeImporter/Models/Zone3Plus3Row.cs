namespace TwZipCodeImporter.Models;

public class Zone3Plus3Row
{
    public string CityName { get; set; } = "";
    public string AreaName { get; set; } = "";
    public int Code6 { get; set; }
    public string RoadName { get; set; } = "";
    public string DeliveryRangeRaw { get; set; } = "";
    public char OddEven { get; set; } = 'A';
    public int? MinNo { get; set; }
    public int? MaxNo { get; set; }
    public string? PostOffice { get; set; }
    public string? BulkNote { get; set; }
}
