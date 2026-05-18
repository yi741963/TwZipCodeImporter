using Microsoft.Data.SqlClient;
using TwZipCodeImporter.Models;

namespace TwZipCodeImporter.Storage;

public class SqlServerRepository : IZipCodeRepository
{
    private readonly string _connectionString;

    public SqlServerRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public DatabaseProvider Provider => DatabaseProvider.SqlServer;

    public async Task EnsureSchemaAsync()
    {
        const string sql = @"
IF SCHEMA_ID(N'tms') IS NULL EXEC(N'CREATE SCHEMA [tms]');

IF OBJECT_ID(N'[tms].[Zone3Plus3]', 'U') IS NULL
BEGIN
    CREATE TABLE [tms].[Zone3Plus3] (
        Id               int            IDENTITY(1,1) NOT NULL,
        CityName         nvarchar(10)   NOT NULL,
        AreaName         nvarchar(20)   NOT NULL,
        Code6            int            NOT NULL,
        RoadName         nvarchar(100)  NOT NULL,
        DeliveryRangeRaw nvarchar(100)  NOT NULL,
        OddEven          char(1)        NOT NULL,
        MinNo            int            NULL,
        MaxNo            int            NULL,
        PostOffice       nvarchar(100)  NULL,
        BulkNote         nvarchar(200)  NULL,
        IsEnable         bit            NOT NULL CONSTRAINT DF_Zone3Plus3_IsEnable DEFAULT (1),
        UpdatedAt        datetime2(0)   NOT NULL CONSTRAINT DF_Zone3Plus3_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_Zone3Plus3 PRIMARY KEY CLUSTERED (Id)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UQ_Zone3Plus3_Natural
        ON [tms].[Zone3Plus3] (CityName, AreaName, RoadName, Code6, DeliveryRangeRaw);

    CREATE NONCLUSTERED INDEX IX_Zone3Plus3_Code6
        ON [tms].[Zone3Plus3] (Code6);

    CREATE NONCLUSTERED INDEX IX_Zone3Plus3_Address
        ON [tms].[Zone3Plus3] (CityName, AreaName, RoadName);
END";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = 120;
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<(int Inserted, int Updated)> UpsertAsync(
        IReadOnlyList<Zone3Plus3Row> rows,
        IProgress<string>? log = null,
        CancellationToken ct = default)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using var tran = (SqlTransaction)await conn.BeginTransactionAsync(ct);
        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandText = @"
CREATE TABLE #Stage (
    CityName         nvarchar(10)  COLLATE DATABASE_DEFAULT NOT NULL,
    AreaName         nvarchar(20)  COLLATE DATABASE_DEFAULT NOT NULL,
    Code6            int                                    NOT NULL,
    RoadName         nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL,
    DeliveryRangeRaw nvarchar(100) COLLATE DATABASE_DEFAULT NOT NULL,
    OddEven          char(1)       COLLATE DATABASE_DEFAULT NOT NULL,
    MinNo            int                                    NULL,
    MaxNo            int                                    NULL,
    PostOffice       nvarchar(100) COLLATE DATABASE_DEFAULT NULL,
    BulkNote         nvarchar(200) COLLATE DATABASE_DEFAULT NULL
);";
                await cmd.ExecuteNonQueryAsync(ct);
            }

            using (var bulk = new SqlBulkCopy(conn, SqlBulkCopyOptions.Default, tran))
            {
                bulk.DestinationTableName = "#Stage";
                bulk.BatchSize = 5000;
                bulk.BulkCopyTimeout = 300;

                bulk.ColumnMappings.Add(nameof(Zone3Plus3Row.CityName), "CityName");
                bulk.ColumnMappings.Add(nameof(Zone3Plus3Row.AreaName), "AreaName");
                bulk.ColumnMappings.Add(nameof(Zone3Plus3Row.Code6), "Code6");
                bulk.ColumnMappings.Add(nameof(Zone3Plus3Row.RoadName), "RoadName");
                bulk.ColumnMappings.Add(nameof(Zone3Plus3Row.DeliveryRangeRaw), "DeliveryRangeRaw");
                bulk.ColumnMappings.Add(nameof(Zone3Plus3Row.OddEven), "OddEven");
                bulk.ColumnMappings.Add(nameof(Zone3Plus3Row.MinNo), "MinNo");
                bulk.ColumnMappings.Add(nameof(Zone3Plus3Row.MaxNo), "MaxNo");
                bulk.ColumnMappings.Add(nameof(Zone3Plus3Row.PostOffice), "PostOffice");
                bulk.ColumnMappings.Add(nameof(Zone3Plus3Row.BulkNote), "BulkNote");

                var reader = new ZoneRowDataReader(rows);
                await bulk.WriteToServerAsync(reader, ct);
            }

            log?.Report($"寫入 Staging: {rows.Count} 筆");

            int inserted = 0, updated = 0;
            await using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tran;
                cmd.CommandTimeout = 300;
                cmd.CommandText = @"
DECLARE @actions TABLE (act nvarchar(10));

MERGE [tms].[Zone3Plus3] WITH (HOLDLOCK) AS T
USING #Stage AS S
  ON  T.CityName         = S.CityName
  AND T.AreaName          = S.AreaName
  AND T.RoadName          = S.RoadName
  AND T.Code6             = S.Code6
  AND T.DeliveryRangeRaw  = S.DeliveryRangeRaw
WHEN MATCHED THEN
    UPDATE SET
        T.OddEven     = S.OddEven,
        T.MinNo       = S.MinNo,
        T.MaxNo       = S.MaxNo,
        T.PostOffice  = S.PostOffice,
        T.BulkNote    = S.BulkNote,
        T.UpdatedAt   = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (CityName, AreaName, Code6, RoadName, DeliveryRangeRaw,
            OddEven, MinNo, MaxNo, PostOffice, BulkNote, IsEnable, UpdatedAt)
    VALUES (S.CityName, S.AreaName, S.Code6, S.RoadName, S.DeliveryRangeRaw,
            S.OddEven, S.MinNo, S.MaxNo, S.PostOffice, S.BulkNote, 1,
            SYSUTCDATETIME())
OUTPUT $action INTO @actions;

SELECT act, COUNT(*) FROM @actions GROUP BY act;";

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var act = reader.GetString(0);
                    var cnt = reader.GetInt32(1);
                    if (act == "INSERT") inserted = cnt;
                    else if (act == "UPDATE") updated = cnt;
                }
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
        var sql = @"
SELECT CityName, AreaName, RoadName, Code6, DeliveryRangeRaw,
       OddEven, MinNo, MaxNo, PostOffice, BulkNote
FROM [tms].[Zone3Plus3]
WHERE IsEnable = 1
  AND (@city IS NULL OR CityName = @city)
  AND (@area IS NULL OR AreaName = @area)
  AND (@road IS NULL OR RoadName = @road)";

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@city", (object?)cityName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@area", (object?)areaName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@road", (object?)roadName ?? DBNull.Value);

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
