using System.Data;
using System.Text;
using ExcelDataReader;
using TwZipCodeImporter.Models;

namespace TwZipCodeImporter;

public static class ExcelReader
{
    // 欄序 (0-based): 0=縣市, 1=區域, 2=郵遞區號, 3=街路名稱, 4=投遞範圍, 5=投遞局, 6=大宗戶
    public static IEnumerable<(Zone3Plus3Row Row, int FileRowIndex, string FileName)> ReadAll(
        string directory,
        List<string> warnings)
    {
        // ExcelDataReader 在非 Windows 環境需要此設定，Windows 下也無害
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var files = Directory.GetFiles(directory, "*.xls")
            .Concat(Directory.GetFiles(directory, "*.xlsx"))
            .OrderBy(f => f)
            .ToArray();

        if (files.Length == 0)
            throw new FileNotFoundException($"在 {directory} 找不到任何 .xls / .xlsx 檔案");

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);
            Console.WriteLine($"讀取檔案: {fileName}");

            using var stream = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = file.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                ? ExcelReaderFactory.CreateOpenXmlReader(stream)
                : ExcelReaderFactory.CreateBinaryReader(stream);

            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true   // 第 1 列為標題，自動跳過
                }
            });

            var sheet = dataSet.Tables[0];
            int rowCount = 0;

            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                var row = sheet.Rows[i];
                int excelRowNum = i + 2; // 1-based，+1 標題列，+1 資料列從第2列起

                var cityName  = GetStr(row, 0);
                var areaName  = GetStr(row, 1);
                var code6Str  = GetStr(row, 2);
                var roadName  = GetStr(row, 3);
                var rangeRaw  = GetStr(row, 4);
                var postOffice = GetStr(row, 5);
                var bulkNote  = GetStr(row, 6);

                // 跳過空白列
                if (string.IsNullOrWhiteSpace(cityName) && string.IsNullOrWhiteSpace(roadName))
                    continue;

                if (!int.TryParse(code6Str, out int code6))
                {
                    warnings.Add($"{fileName} 列{excelRowNum}: Code6 無法轉換為數字 (值={code6Str})，已跳過");
                    continue;
                }

                // 空白投遞範圍視同「全」
                if (string.IsNullOrWhiteSpace(rangeRaw))
                    rangeRaw = "全";

                var (oddEven, minNo, maxNo) = DeliveryRangeParser.Parse(rangeRaw);

                if (!DeliveryRangeParser.IsKnownPattern(rangeRaw))
                    warnings.Add($"{fileName} 列{excelRowNum}: 投遞範圍「{rangeRaw}」無法完整解析，已以 OddEven=A 寫入");

                yield return (new Zone3Plus3Row
                {
                    CityName         = cityName,
                    AreaName         = areaName,
                    Code6            = code6,
                    RoadName         = roadName,
                    DeliveryRangeRaw = rangeRaw,
                    OddEven          = oddEven,
                    MinNo            = minNo,
                    MaxNo            = maxNo,
                    PostOffice       = string.IsNullOrWhiteSpace(postOffice) ? null : postOffice,
                    BulkNote         = string.IsNullOrWhiteSpace(bulkNote)   ? null : bulkNote,
                }, excelRowNum, fileName);

                rowCount++;
            }

            Console.WriteLine($"  → {rowCount} 列");
        }
    }

    private static string GetStr(DataRow row, int col)
    {
        if (col >= row.Table.Columns.Count) return "";
        var v = row[col];
        if (v == null || v == DBNull.Value) return "";
        // 郵遞區號欄位有時被讀成 double（如 104091.0），先轉數字再取整數字串
        if (v is double d) return ((long)d).ToString();
        return v.ToString()?.Trim() ?? "";
    }
}
