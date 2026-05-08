using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using TwZipCodeImporter;

Console.OutputEncoding = Encoding.UTF8;

Console.WriteLine("========================================");
Console.WriteLine("  3+3 郵遞區號 Excel 匯入工具");
Console.WriteLine("========================================");

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var connStr = config.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("找不到 ConnectionStrings:DefaultConnection");

var excelDir = config["ExcelDirectory"]
    ?? throw new InvalidOperationException("找不到 ExcelDirectory 設定");

// 允許以命令列參數 --file <path> 指定單一檔案目錄
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--file")
    {
        excelDir = Path.GetDirectoryName(args[i + 1])!;
        Console.WriteLine($"指定單檔模式: {args[i + 1]}");
        break;
    }
}

var sw = Stopwatch.StartNew();
var warnings = new List<string>();

Console.WriteLine($"Excel 目錄: {excelDir}");
Console.WriteLine();

// 讀取 Excel
var allRows = ExcelReader.ReadAll(excelDir, warnings)
    .Select(x => x.Row)
    .ToList();

Console.WriteLine();
Console.WriteLine($"總計讀取: {allRows.Count} 筆");

if (warnings.Count > 0)
{
    Console.WriteLine($"解析警告: {warnings.Count} 筆 (詳見 import_warnings.log)");
    await File.WriteAllLinesAsync("import_warnings.log", warnings, Encoding.UTF8);
}

// 匯入
Console.WriteLine();
Console.WriteLine("開始寫入資料庫...");

var importer = new ZoneImporter(connStr);
var (inserted, updated) = await importer.ImportAsync(allRows);

sw.Stop();

Console.WriteLine();
Console.WriteLine("========================================");
Console.WriteLine($"  MERGE 完成: Insert {inserted} / Update {updated}");
Console.WriteLine($"  警告筆數:   {warnings.Count}");
Console.WriteLine($"  總耗時:     {sw.Elapsed.TotalSeconds:F1} 秒");
Console.WriteLine("========================================");
