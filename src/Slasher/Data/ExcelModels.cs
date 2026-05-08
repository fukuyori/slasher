namespace Slasher.Data;

public sealed record ExcelReadRequest(string Path, string? Sheet = null, bool? HasHeader = true);

public sealed record ExcelWorkbookResponse(
    string Path,
    IReadOnlyList<string> Sheets);

public sealed record ExcelReadResponse(
    string Path,
    string Sheet,
    bool HasHeader,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string?>> Rows,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Objects);
