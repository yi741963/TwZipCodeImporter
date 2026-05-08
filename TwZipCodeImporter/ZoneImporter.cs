using Microsoft.Data.SqlClient;
using TwZipCodeImporter.Models;

namespace TwZipCodeImporter;

public class ZoneImporter
{
    private readonly string _connectionString;

    public ZoneImporter(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<(int Inserted, int Updated)> ImportAsync(IReadOnlyList<Zone3Plus3Row> rows)
    {
        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var tran = conn.BeginTransaction();
        try
        {
            // 建立暫存表
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
                await cmd.ExecuteNonQueryAsync();
            }

            // SqlBulkCopy → #Stage
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

                var reader = new RowDataReader(rows);
                await bulk.WriteToServerAsync(reader);
            }

            Console.WriteLine($"寫入 Staging: {rows.Count} 筆");

            // MERGE
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

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var act = reader.GetString(0);
                    var cnt = reader.GetInt32(1);
                    if (act == "INSERT") inserted = cnt;
                    else if (act == "UPDATE") updated = cnt;
                }
            }

            tran.Commit();
            return (inserted, updated);
        }
        catch
        {
            tran.Rollback();
            throw;
        }
    }

    // IDataReader 包裝，供 SqlBulkCopy 使用
    private sealed class RowDataReader : System.Data.Common.DbDataReader
    {
        private readonly IReadOnlyList<Zone3Plus3Row> _rows;
        private int _index = -1;

        public RowDataReader(IReadOnlyList<Zone3Plus3Row> rows) => _rows = rows;

        public override bool Read() => ++_index < _rows.Count;
        public override int FieldCount => 10;

        public override object GetValue(int ordinal)
        {
            var r = _rows[_index];
            return ordinal switch
            {
                0 => r.CityName,
                1 => r.AreaName,
                2 => r.Code6,
                3 => r.RoadName,
                4 => r.DeliveryRangeRaw,
                5 => r.OddEven.ToString(),
                6 => (object?)r.MinNo ?? DBNull.Value,
                7 => (object?)r.MaxNo ?? DBNull.Value,
                8 => (object?)r.PostOffice ?? DBNull.Value,
                9 => (object?)r.BulkNote ?? DBNull.Value,
                _ => throw new IndexOutOfRangeException()
            };
        }

        public override string GetName(int ordinal) => ordinal switch
        {
            0 => "CityName", 1 => "AreaName", 2 => "Code6", 3 => "RoadName",
            4 => "DeliveryRangeRaw", 5 => "OddEven", 6 => "MinNo", 7 => "MaxNo",
            8 => "PostOffice", 9 => "BulkNote", _ => throw new IndexOutOfRangeException()
        };

        public override bool IsDBNull(int ordinal) => GetValue(ordinal) is DBNull;

        // --- 其餘必要實作 ---
        public override int GetOrdinal(string name) => GetName(0) == name ? 0 :
            Enumerable.Range(0, FieldCount).First(i => GetName(i) == name);
        public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);
        public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
        public override char GetChar(int ordinal) => (char)GetValue(ordinal);
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
        public override string GetDataTypeName(int ordinal) => "nvarchar";
        public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);
        public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);
        public override double GetDouble(int ordinal) => (double)GetValue(ordinal);
        public override System.Type GetFieldType(int ordinal) => GetValue(ordinal).GetType();
        public override float GetFloat(int ordinal) => (float)GetValue(ordinal);
        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
        public override short GetInt16(int ordinal) => (short)GetValue(ordinal);
        public override int GetInt32(int ordinal) => (int)GetValue(ordinal);
        public override long GetInt64(int ordinal) => (long)GetValue(ordinal);
        public override string GetString(int ordinal) => (string)GetValue(ordinal);
        public override int GetValues(object[] values) { for (int i = 0; i < FieldCount; i++) values[i] = GetValue(i); return FieldCount; }
        public override bool HasRows => _rows.Count > 0;
        public override bool IsClosed => false;
        public override int RecordsAffected => -1;
        public override int Depth => 0;
        public override System.Collections.IEnumerator GetEnumerator() => throw new NotSupportedException();
        public override bool NextResult() => false;
        public override object this[int ordinal] => GetValue(ordinal);
        public override object this[string name] => GetValue(GetOrdinal(name));
    }
}
