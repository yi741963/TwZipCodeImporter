using TwZipCodeImporter.Models;
using TwZipCodeImporter.Storage;

namespace TwZipCodeImporter;

// 維持回溯相容,實際委派給 SqlServerRepository
public class ZoneImporter
{
    private readonly SqlServerRepository _repo;

    public ZoneImporter(string connectionString)
    {
        _repo = new SqlServerRepository(connectionString);
    }

    public Task<(int Inserted, int Updated)> ImportAsync(IReadOnlyList<Zone3Plus3Row> rows)
        => _repo.UpsertAsync(rows, new Progress<string>(Console.WriteLine));
}
