using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using TwZipCodeImporter;
using TwZipCodeImporter.Storage;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("========================================");
Console.WriteLine("  3+3 郵遞區號 Excel 匯入工具");
Console.WriteLine("========================================");

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .Build();

// ── 解析參數 ────────────────────────────────────────────
// --provider SqlServer|Sqlite
// --conn "<connection string>"
// --file <path-to-excel> (可重複)
// --dir <directory>
// --lookup "<address>"
var provider = DatabaseProvider.SqlServer;
if (RepositoryFactory.TryParse(config["DatabaseProvider"], out var p)) provider = p;

string? connStr = null;
var fileList = new List<string>();
string? excelDir = config["ExcelDirectory"];
string? lookupAddress = null;

for (int i = 0; i < args.Length; i++)
{
    var a = args[i];
    if (a == "--provider" && i + 1 < args.Length)
    {
        if (RepositoryFactory.TryParse(args[++i], out var prov)) provider = prov;
    }
    else if (a == "--conn" && i + 1 < args.Length) connStr = args[++i];
    else if (a == "--file" && i + 1 < args.Length) fileList.Add(args[++i]);
    else if (a == "--dir"  && i + 1 < args.Length) excelDir = args[++i];
    else if (a == "--lookup" && i + 1 < args.Length) lookupAddress = args[++i];
}

connStr ??= provider == DatabaseProvider.SqlServer
    ? config.GetConnectionString("DefaultConnection")
    : (config.GetConnectionString("Sqlite") ?? "Data Source=zipcode.db");

if (string.IsNullOrWhiteSpace(connStr))
    throw new InvalidOperationException("找不到連線字串 (--conn 或設定檔 ConnectionStrings)");

Console.WriteLine($"DB 提供者: {provider}");
Console.WriteLine($"連線字串: {Mask(connStr)}");

var repo = RepositoryFactory.Create(provider, connStr);
await repo.EnsureSchemaAsync();

// ── 地址查詢模式 ────────────────────────────────────────
if (!string.IsNullOrWhiteSpace(lookupAddress))
{
    var svc = new AddressLookupService(repo);
    var result = await svc.LookupAsync(lookupAddress);
    Console.WriteLine();
    Console.WriteLine($"輸入: {lookupAddress}");
    Console.WriteLine($"解析: 縣市={result.Parsed.CityName} 區={result.Parsed.AreaName} 路={result.Parsed.RoadName} 號={result.Parsed.HouseNo}");
    Console.WriteLine();
    Console.WriteLine($"符合筆數: {result.Matches.Count}");
    foreach (var m in result.Matches.Take(20))
    {
        Console.WriteLine($"  {m.Code6}  {m.CityName}{m.AreaName}{m.RoadName} ({m.DeliveryRangeRaw})");
    }
    return;
}

// ── 匯入模式 ────────────────────────────────────────────
var sw = Stopwatch.StartNew();
var warnings = new List<string>();

IEnumerable<(Models.Zone3Plus3Row Row, int FileRowIndex, string FileName)> source;
if (fileList.Count > 0)
{
    Console.WriteLine($"來源檔案: {fileList.Count} 個");
    source = ExcelReader.ReadFiles(fileList, warnings);
}
else
{
    if (string.IsNullOrWhiteSpace(excelDir))
        throw new InvalidOperationException("請以 --file 指定檔案或設定 ExcelDirectory");
    Console.WriteLine($"Excel 目錄: {excelDir}");
    source = ExcelReader.ReadAll(excelDir, warnings);
}

var allRows = source.Select(x => x.Row).ToList();
Console.WriteLine($"總計讀取: {allRows.Count} 筆");

if (warnings.Count > 0)
{
    Console.WriteLine($"解析警告: {warnings.Count} 筆 (詳見 import_warnings.log)");
    await File.WriteAllLinesAsync("import_warnings.log", warnings, Encoding.UTF8);
}

Console.WriteLine();
Console.WriteLine("開始寫入資料庫...");

var progress = new Progress<string>(Console.WriteLine);
var (inserted, updated) = await repo.UpsertAsync(allRows, progress);

sw.Stop();

Console.WriteLine();
Console.WriteLine("========================================");
Console.WriteLine($"  完成: Insert {inserted} / Update {updated}");
Console.WriteLine($"  警告筆數:   {warnings.Count}");
Console.WriteLine($"  總耗時:     {sw.Elapsed.TotalSeconds:F1} 秒");
Console.WriteLine("========================================");

static string Mask(string s)
{
    // 簡單遮蔽密碼欄位
    return System.Text.RegularExpressions.Regex.Replace(
        s, @"(Password|Pwd)\s*=\s*[^;]+",
        "$1=***",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
