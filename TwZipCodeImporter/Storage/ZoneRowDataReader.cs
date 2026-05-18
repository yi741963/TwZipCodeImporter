using TwZipCodeImporter.Models;

namespace TwZipCodeImporter.Storage;

internal sealed class ZoneRowDataReader : System.Data.Common.DbDataReader
{
    private readonly IReadOnlyList<Zone3Plus3Row> _rows;
    private int _index = -1;

    public ZoneRowDataReader(IReadOnlyList<Zone3Plus3Row> rows) => _rows = rows;

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

    public override int GetOrdinal(string name) =>
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
