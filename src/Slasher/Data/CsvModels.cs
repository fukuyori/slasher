namespace Slasher.Data;

public sealed record CsvReadRequest(string Path, bool? HasHeader = true, string? Delimiter = null);

public sealed record CsvReadResponse(
    string Path,
    string Delimiter,
    bool HasHeader,
    IReadOnlyList<string> Headers,
    IReadOnlyList<string[]> Rows,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Objects);

public sealed record CsvConvertResponse(
    string Path,
    int RowCount,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Objects);
