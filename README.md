# TwZipCodeImporter

台灣 3+3 郵遞區號 (Zone3+3) Excel 匯入與查詢工具。

支援以下功能:

- 匯入單一或多個 Excel 檔 (`.xls` / `.xlsx`),每檔可達 4 萬筆以上
- 兩種儲存後端可在 UI 切換:
  - **SQLite** (本機檔案,零安裝、免費,推薦)
  - **MS-SQL Server** (沿用原本 `[tms].[Zone3Plus3]` 結構)
- 以單一欄位輸入完整地址,自動拆解縣市/區/路段/門牌號,回傳對應 6 碼郵遞區號
- 同時提供 WinForms 圖形介面與 Console 命令列工具

## 專案結構

```
TwZipCodeImporter.slnx
├─ TwZipCodeImporter/                       # Class Library + Console 入口 (核心邏輯)
│  ├─ Storage/                              # 儲存抽象與兩種實作
│  │  ├─ IZipCodeRepository.cs              # 抽象介面 (EnsureSchema / Upsert / Lookup)
│  │  ├─ DatabaseProvider.cs                # enum: SqlServer | Sqlite
│  │  ├─ RepositoryFactory.cs               # 依 Provider 建立 repo
│  │  ├─ SqlServerRepository.cs             # SQL Server (SqlBulkCopy + MERGE)
│  │  ├─ SqliteRepository.cs                # SQLite (Stage + ON CONFLICT UPSERT)
│  │  ├─ ZoneRowDataReader.cs               # SqlBulkCopy 用的 IDataReader
│  │  └─ ZoneQueryResult.cs                 # 查詢結果 DTO
│  ├─ Models/
│  │  └─ Zone3Plus3Row.cs                   # 單筆原始資料
│  ├─ Sql/
│  │  ├─ 001_CreateZone3Plus3.sql           # SQL Server 結構 (參考)
│  │  ├─ 002_MergeFromStaging.sql           # MERGE 語句 (參考)
│  │  └─ sqlite_001_CreateZone3Plus3.sql    # SQLite 結構 (參考,程式會自動建立)
│  ├─ ExcelReader.cs                        # 讀取 Excel (支援檔案清單或目錄)
│  ├─ DeliveryRangeParser.cs                # 解析「單/雙 N 至 M 號」等投遞範圍
│  ├─ AddressParser.cs                      # 解析「縣市/區/路段/門牌」
│  ├─ AddressLookupService.cs               # 整合解析 + DB 查詢
│  ├─ ZoneImporter.cs                       # 向後相容包裝
│  └─ Program.cs                            # Console 入口
│
├─ TwZipCodeImporter.WinForms/              # 桌面 GUI (net8.0-windows)
│  ├─ MainForm.cs                           # 主視窗 (DB 設定列 + TabControl)
│  ├─ DbConfigPanel.cs                      # DB 切換 + 連線字串/檔案瀏覽
│  ├─ ImportTab.cs                          # 匯入分頁 (多檔/目錄 + 進度 + 警告 log)
│  └─ LookupTab.cs                          # 查詢分頁 (地址輸入 + DataGridView)
│
└─ TwZipCodeImporter.Tests/                 # xUnit 單元測試
   ├─ DeliveryRangeParserTests.cs
   ├─ AddressParserTests.cs
   └─ SqliteRepositoryTests.cs              # 完整 upsert + lookup 端對端測試
```

## 資料表結構

兩種後端使用同一邏輯結構 (欄位/索引相同,只差語法):

| 欄位 | 說明 |
| --- | --- |
| `CityName` | 縣市 (如「台北市」) |
| `AreaName` | 區/鄉/鎮 (如「中山區」) |
| `Code6` | 6 碼郵遞區號 (3+3) |
| `RoadName` | 街路名稱 (含段) |
| `DeliveryRangeRaw` | 原始投遞範圍文字 (如「單21至39號」) |
| `OddEven` | `S` 單號 / `D` 雙號 / `A` 全部 |
| `MinNo`, `MaxNo` | 門牌數字範圍 (NULL 表無上下限) |
| `PostOffice` | 投遞局 |
| `BulkNote` | 大宗戶備註 |
| `IsEnable` | 啟用旗標 |
| `UpdatedAt` | 最後更新時間 |

自然鍵 (UPSERT 比對用):
`(CityName, AreaName, RoadName, Code6, DeliveryRangeRaw)`

啟動時 (`EnsureSchemaAsync`) 會自動建表 + 建索引,SQLite 的 `.db` 檔不存在時也會建立。

## 設定檔 `appsettings.json`

```json
{
  "DatabaseProvider": "Sqlite",
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR\\SQLEXPRESS;Database=YOUR_DB;Trusted_Connection=True;TrustServerCertificate=True",
    "Sqlite": "Data Source=zipcode.db"
  },
  "ExcelDirectory": "C:\\path\\to\\excel"
}
```

- `DatabaseProvider`: 預設後端,`SqlServer` 或 `Sqlite` (UI 上仍可即時切換)
- `ConnectionStrings.DefaultConnection`: SQL Server 連線字串
- `ConnectionStrings.Sqlite`: SQLite 連線字串,可省略,預設 `zipcode.db` 放在執行檔旁
- `ExcelDirectory`: GUI 啟動時自動載入該目錄的 Excel 檔

範例請見 `TwZipCodeImporter/appsettings.example.json`。

## 使用方式

### 桌面 GUI

```
cd TwZipCodeImporter.WinForms
dotnet run
```

#### 匯入 Excel

1. 上方「資料庫」列選擇 **SQLite** 或 **MS-SQL Server**
   - SQLite:右側「選檔...」可選擇或新建 `.db` 檔
   - MS-SQL:輸入完整連線字串
2. 切到「匯入 Excel」分頁
3. 「📄 選擇檔案...」可多選 Excel,「📁 加入目錄...」一次加入整個目錄
4. 按「▶ 開始匯入」,進度與結果會顯示在下方 log
5. 若有解析警告,可按「💾 儲存警告 Log」匯出

#### 地址查詢

1. 切到「地址查 3+3」分頁
2. 輸入完整地址 (例:`台北市中山區中山北路二段100號`),按 Enter 或「🔍 查詢」
3. 程式自動拆解 → 縣市 / 區域 / 街路 / 門牌號
4. 結果區顯示:
   - 找到唯一 3+3 → 顯示郵遞區號
   - 多筆符合 → 列出所有可能
   - 同路段但門牌不在範圍 → 顯示該路段所有規則供查驗

### Console 命令列

```bash
# SQLite,匯入單檔
dotnet run --project TwZipCodeImporter -- \
  --provider Sqlite \
  --conn "Data Source=zipcode.db" \
  --file ./Excel/3+3郵遞區號簿_V2602A.xls

# SQLite,匯入目錄下所有 Excel
dotnet run --project TwZipCodeImporter -- \
  --provider Sqlite \
  --dir ./Excel

# SQL Server,使用 appsettings.json 內的連線字串
dotnet run --project TwZipCodeImporter -- --provider SqlServer --dir ./Excel

# 地址查詢
dotnet run --project TwZipCodeImporter -- \
  --provider Sqlite \
  --conn "Data Source=zipcode.db" \
  --lookup "台北市中山區中山北路二段100號"
```

支援參數:

| 參數 | 說明 |
| --- | --- |
| `--provider <SqlServer\|Sqlite>` | 覆寫 `DatabaseProvider` 設定 |
| `--conn <string>` | 覆寫連線字串 |
| `--file <path>` | 指定單一 Excel (可多次) |
| `--dir <path>` | 指定目錄,讀入所有 `.xls/.xlsx` |
| `--lookup "<address>"` | 切換為查詢模式,印出符合的 3+3 |

## 架構說明

### 儲存抽象 (`Storage/`)

所有對 DB 的操作都走 `IZipCodeRepository`:

```csharp
public interface IZipCodeRepository
{
    DatabaseProvider Provider { get; }
    Task EnsureSchemaAsync();
    Task<(int Inserted, int Updated)> UpsertAsync(
        IReadOnlyList<Zone3Plus3Row> rows,
        IProgress<string>? log = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<ZoneQueryResult>> LookupAsync(
        string? cityName, string? areaName, string? roadName,
        int? houseNo, CancellationToken ct = default);
}
```

`RepositoryFactory.Create(provider, connStr)` 依參數返回 `SqlServerRepository` 或 `SqliteRepository`,UI/CLI 都只透過此介面操作。

### SQL Server 後端

`SqlServerRepository`:

1. `EnsureSchemaAsync()` 確保 `[tms]` schema 與 `[tms].[Zone3Plus3]` 存在
2. 匯入流程:
   - 建立 `#Stage` 暫存表
   - `SqlBulkCopy` 批次 (5000/批) 寫入
   - 對 `[tms].[Zone3Plus3]` 執行 `MERGE WITH (HOLDLOCK)`,以自然鍵比對
   - 透過 `$action OUTPUT` 取得實際 Insert/Update 筆數
3. 查詢用 `WHERE` 條件 + 索引 `IX_Zone3Plus3_Address`

### SQLite 後端

`SqliteRepository`:

1. `EnsureSchemaAsync()` 必要時建立 `.db` 目錄與資料表/索引
2. 匯入前設定效能 PRAGMA (`journal_mode=WAL`, `synchronous=NORMAL`, `temp_store=MEMORY`)
3. 在交易內:
   - 建立 `TEMP TABLE Stage`
   - 以 prepared statement 將所有 row 寫入 Stage
   - 計算 update 筆數 (Stage 中已存在於主表的列)
   - 一次性 `INSERT ... SELECT FROM Stage ... ON CONFLICT (natural key) DO UPDATE SET ...`
   - DROP Stage
4. 4 萬筆匯入約幾秒,單檔 SQLite 即可承載

### Excel 解析

`ExcelReader`:

- 欄序 (0-based) 固定:`0 縣市, 1 區域, 2 郵遞區號, 3 街路, 4 投遞範圍, 5 投遞局, 6 大宗戶`
- 第 1 列視為標題自動跳過
- 提供兩種入口:
  - `ReadAll(directory, warnings)`:讀取目錄下所有 `.xls`/`.xlsx`
  - `ReadFiles(filePaths, warnings)`:讀取指定檔案清單 (UI 主要使用)
- 投遞範圍透過 `DeliveryRangeParser` 拆解為 `(OddEven, MinNo, MaxNo)`,無法解析的會記錄在 warnings

### 投遞範圍解析 (`DeliveryRangeParser`)

支援格式:

| 範例輸入 | OddEven | MinNo | MaxNo |
| --- | --- | --- | --- |
| `全` / 空白 | A | null | null |
| `單號` | S | null | null |
| `雙號` | D | null | null |
| `單19號以下` | S | null | 19 |
| `單21至39號` | S | 21 | 39 |
| `單41號以上` | S | 41 | null |
| `23號` | A | 23 | 23 |

支援全形數字、`~`/`-`/`至` 變體與「N號至M號」寫法 (自動正規化)。

### 地址解析 (`AddressParser`)

以 Regex 由前往後切:

1. `^(.+?[市縣])` → CityName
2. `(.+?(?:區|鄉|鎮|市))` → AreaName
3. 剩餘字串中第一個 `(\d+)號` 之前 → RoadName
4. 數字 → HouseNo
5. 之後 (巷弄樓室) → Remainder

如缺欄位則保留為空字串,查詢時自動以可填欄位放寬條件。

### 查詢邏輯 (`AddressLookupService`)

1. 用 `CityName + AreaName + RoadName` 拉出該路段所有規則 (candidates)
2. 對每筆判斷:
   - `OddEven = S` → 門牌須為單數
   - `OddEven = D` → 門牌須為雙數
   - `MinNo <= houseNo <= MaxNo` (有設才檢查)
3. 通過的列為 `Matches`,若 `Matches` 為空但 `Candidates` 非空,UI 顯示「同路段資料但門牌不在範圍」協助使用者判斷資料是否需更新

## 測試

```bash
dotnet test
```

涵蓋:
- 投遞範圍解析 (`DeliveryRangeParserTests`)
- 地址解析 (`AddressParserTests`)
- SQLite 端對端 upsert + lookup (`SqliteRepositoryTests`,使用臨時 `.db` 檔)

SQL Server 端對端測試需實際資料庫,目前未自動化。

## 套件

| 套件 | 用途 |
| --- | --- |
| `ExcelDataReader` / `.DataSet` | 讀取 `.xls`/`.xlsx` |
| `Microsoft.Data.SqlClient` | SQL Server 連線 |
| `Microsoft.Data.Sqlite` | SQLite 連線 (含原生 binary) |
| `Microsoft.Extensions.Configuration.Json` | 讀 `appsettings.json` |
| `xunit` | 單元測試 |

## 加入新後端

實作 `IZipCodeRepository`,在 `RepositoryFactory.Create` 增加分支,並在 `DbConfigPanel` 加入選項即可。其餘 UI 與服務層皆透過介面互動,無需更動。
