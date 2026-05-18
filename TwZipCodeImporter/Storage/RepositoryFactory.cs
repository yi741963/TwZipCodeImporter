namespace TwZipCodeImporter.Storage;

public static class RepositoryFactory
{
    public static IZipCodeRepository Create(DatabaseProvider provider, string connectionString)
    {
        return provider switch
        {
            DatabaseProvider.SqlServer => new SqlServerRepository(connectionString),
            DatabaseProvider.Sqlite => new SqliteRepository(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
        };
    }

    public static bool TryParse(string? value, out DatabaseProvider provider)
    {
        provider = DatabaseProvider.SqlServer;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (string.Equals(value, "SqlServer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "MsSql",    StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "MSSQL",    StringComparison.OrdinalIgnoreCase))
        {
            provider = DatabaseProvider.SqlServer;
            return true;
        }
        if (string.Equals(value, "Sqlite",  StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "SQLite", StringComparison.OrdinalIgnoreCase))
        {
            provider = DatabaseProvider.Sqlite;
            return true;
        }
        return false;
    }
}
