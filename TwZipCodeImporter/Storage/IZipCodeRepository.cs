using TwZipCodeImporter.Models;

namespace TwZipCodeImporter.Storage;

public interface IZipCodeRepository
{
    DatabaseProvider Provider { get; }

    Task EnsureSchemaAsync();

    Task<(int Inserted, int Updated)> UpsertAsync(
        IReadOnlyList<Zone3Plus3Row> rows,
        IProgress<string>? log = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ZoneQueryResult>> LookupAsync(
        string? cityName,
        string? areaName,
        string? roadName,
        int? houseNo,
        CancellationToken ct = default);
}
