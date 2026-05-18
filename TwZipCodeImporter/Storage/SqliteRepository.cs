using Microsoft.Data.Sqlite;
using TwZipCodeImporter.Models;

namespace TwZipCodeImporter.Storage;

public class SqliteRepository : IZipCodeRepository
{
    private readonly string _connectionString;

    public SqliteRepository(string connectionString)
    {
        _connectionString = NormalizeConnectionString(connectionString);
    }

    public DatabaseProvider Provider => DatabaseProvider.Sqlite;

    // 接受純檔案路徑或標準 connection string
    private static string NormalizeConnectionString(string input)
    {
        var s = (input ?? "").Trim();
        if (string.IsNullOrEmpty(s)) return "Data Source=zipcode.db";
        if (s.Contains('=')) return s;
        return new SqliteConnectionStringBuilder { DataSource = s }.ToString();
    }

    public async Task EnsureSchemaAsync()
    {
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        var dir = Path.GetDirectoryName(builder.DataSource);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Zone3Plus3 (
    Id               INTEGER PRIMARY KEY AUTOINCREMENT,
    CityName         TEXT    NOT NULL,
    AreaName         TEXT    NOT NULL,
    Code6            INTEGER NOT NULL,
    RoadName         TEXT    NOT NULL,
    DeliveryRangeRaw TEXT    NOT NULL,
    OddEven          TEXT    NOT NULL,
    MinNo            INTEGER NULL,
    MaxNo            INTEGER NULL,
    PostOffice       TEXT    NULL,
    BulkNote         TEXT    NULL,
    IsEnable         INTEGER NOT NULL DEFAULT 1,
    UpdatedAt        TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);
CREATE UNIQUE INDEX IF NOT EXISTS UQ_Zone3Plus3_Natural
    ON Zone3Plus3 (CityName, AreaName, RoadName, Code6, DeliveryRangeRaw);
CREATE INDEX IF NOT EXISTS IX_Zone3Plus3_Code6
    ON Zone3Plus3 (Code6);
CREATE INDEX IF NOT EXISTS IX_Zone3Plus3_Address
    ON Zone3Plus3 (CityName, AreaName, RoadName);
";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<(int Inserted, int Updated)> UpsertAsync(
        IReadOnlyList<Zone3Plus3Row> rows,
        IProgress<string>? log = null,
        CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA temp_store=MEMORY;";
            await pragma.ExecuteNonQueryAsync(ct);
        }

        await using var tran = (SqliteTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            // 暫存表
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandText = @"
CREATE TEMP TABLE Stage (
    CityName         TEXT    NOT NULL,
    AreaName         TEXT    NOT NULL,
    Code6            INTEGER NOT NULL,
    RoadName         TEXT    NOT NULL,
    DeliveryRangeRaw TEXT    NOT NULL,
    OddEven          TEXT    NOT NULL,
    MinNo            INTEGER NULL,
    MaxNo            INTEGER NULL,
    PostOffice       TEXT    NULL,
    BulkNote         TEXT    NULL
);";
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // 批次寫入 Stage (prepared statement,單交易)
            await using (var insert = conn.CreateCommand())
            {
                insert.Transaction = tran;
                insert.CommandText = @"
INSERT INTO Stage (CityName, AreaName, Code6, RoadName, DeliveryRangeRaw,
                   OddEven, MinNo, MaxNo, PostOffice, BulkNote)
VALUES ($CityName, $AreaName, $Code6, $RoadName, $DeliveryRangeRaw,
        $OddEven, $MinNo, $MaxNo, $PostOffice, $BulkNote);";
                var pCity   = insert.Parameters.Add("$CityName", SqliteType.Text);
                var pArea   = insert.Parameters.Add("$AreaName", SqliteType.Text);
                var pCode6  = insert.Parameters.Add("$Code6", SqliteType.Integer);
                var pRoad   = insert.Parameters.Add("$RoadName", SqliteType.Text);
                var pRange  = insert.Parameters.Add("$DeliveryRangeRaw", SqliteType.Text);
                var pOE     = insert.Parameters.Add("$OddEven", SqliteType.Text);
                var pMin    = insert.Parameters.Add("$MinNo", SqliteType.Integer);
                var pMax    = insert.Parameters.Add("$MaxNo", SqliteType.Integer);
                var pPO     = insert.Parameters.Add("$PostOffice", SqliteType.Text);
                var pBulk   = insert.Parameters.Add("$BulkNote", SqliteType.Text);
                insert.Prepare();

                int n = 0;
                foreach (var r in rows)
                {
                    ct.ThrowIfCancellationRequested();
                    pCity.Value  = r.CityName;
                    pArea.Value  = r.AreaName;
                    pCode6.Value = r.Code6;
                    pRoad.Value  = r.RoadName;
                    pRange.Value = r.DeliveryRangeRaw;
                    pOE.Value    = r.OddEven.ToString();
                    pMin.Value   = (object?)r.MinNo ?? DBNull.Value;
                    pMax.Value   = (object?)r.MaxNo ?? DBNull.Value;
                    pPO.Value    = (object?)r.PostOffice ?? DBNull.Value;
                    pBulk.Value  = (object?)r.BulkNote ?? DBNull.Value;
                    await insert.ExecuteNonQueryAsync(ct);
                    n++;
                    if (n % 5000 == 0) log?.Report($"  Stage: {n:N0} / {rows.Count:N0}");
                }
            }

            log?.Report($"寫入 Staging: {rows.Count} 筆");

            // 計算 insert / update 筆數
            int updated;
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandText = @"
SELECT COUNT(*) FROM Stage s
WHERE EXISTS (
    SELECT 1 FROM Zone3Plus3 t
    WHERE t.CityName = s.CityName
      AND t.AreaName = s.AreaName
      AND t.RoadName = s.RoadName
      AND t.Code6 = s.Code6
      AND t.DeliveryRangeRaw = s.DeliveryRangeRaw
);";
                var r = await cmd.ExecuteScalarAsync(ct);
                updated = Convert.ToInt32(r ?? 0);
            }
            int inserted = rows.Count - updated;

            // UPSERT
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandText = @"
INSERT INTO Zone3Plus3 (CityName, AreaName, Code6, RoadName, DeliveryRangeRaw,
                        OddEven, MinNo, MaxNo, PostOffice, BulkNote, IsEnable, UpdatedAt)
SELECT CityName, AreaName, Code6, RoadName, DeliveryRangeRaw,
       OddEven, MinNo, MaxNo, PostOffice, BulkNote, 1,
       strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
FROM Stage
WHERE true
ON CONFLICT (CityName, AreaName, RoadName, Code6, DeliveryRangeRaw) DO UPDATE SET
    OddEven    = excluded.OddEven,
    MinNo      = excluded.MinNo,
    MaxNo      = excluded.MaxNo,
    PostOffice = excluded.PostOffice,
    BulkNote   = excluded.BulkNote,
    UpdatedAt  = strftime('%Y-%m-%dT%H:%M:%fZ', 'now');";
                await cmd.ExecuteNonQueryAsync(ct);
            }

            await using (var drop = conn.CreateCommand())
            {
                drop.Transaction = tran;
                drop.CommandText = "DROP TABLE Stage;";
                await drop.ExecuteNonQueryAsync(ct);
            }

            await tran.CommitAsync(ct);
            return (inserted, updated);
        }
        catch
        {
            await tran.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<IReadOnlyList<ZoneQueryResult>> LookupAsync(
        string? cityName,
        string? areaName,
        string? roadName,
        int? houseNo,
        CancellationToken ct = default)
    {
        const string sql = @"
SELECT CityName, AreaName, RoadName, Code6, DeliveryRangeRaw,
       OddEven, MinNo, MaxNo, PostOffice, BulkNote
FROM Zone3Plus3
WHERE IsEnable = 1
  AND ($city IS NULL OR CityName = $city)
  AND ($area IS NULL OR AreaName = $area)
  AND ($road IS NULL OR RoadName = $road);";

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$city", (object?)cityName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$area", (object?)areaName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$road", (object?)roadName ?? DBNull.Value);

        var list = new List<ZoneQueryResult>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new ZoneQueryResult
            {
                CityName = reader.GetString(0),
                AreaName = reader.GetString(1),
                RoadName = reader.GetString(2),
                Code6 = reader.GetInt32(3),
                DeliveryRangeRaw = reader.GetString(4),
                OddEven = reader.GetString(5)[0],
                MinNo = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                MaxNo = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                PostOffice = reader.IsDBNull(8) ? null : reader.GetString(8),
                BulkNote = reader.IsDBNull(9) ? null : reader.GetString(9),
            });
        }
        return list;
    }
}
