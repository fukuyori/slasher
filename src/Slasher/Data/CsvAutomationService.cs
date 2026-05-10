using System.Globalization;
using System.Text;
using Microsoft.Extensions.Hosting;

namespace Slasher.Data;

public sealed class CsvAutomationService
{
    private readonly string? _contentRoot;

    public CsvAutomationService(IHostEnvironment? environment = null)
    {
        _contentRoot = environment?.ContentRootPath;
    }

    public CsvReadResponse Read(CsvReadRequest request)
    {
        var delimiter = GetDelimiter(request.Delimiter);
        var path = ResolvePath(request.Path);
        var text = File.ReadAllText(path, Encoding.UTF8);
        var rows = Parse(text, delimiter);
        var hasHeader = request.HasHeader ?? true;

        string[] headers;
        IReadOnlyList<string[]> dataRows;
        if (hasHeader && rows.Count > 0)
        {
            headers = rows[0];
            dataRows = rows.Skip(1).ToArray();
        }
        else
        {
            var width = rows.Count == 0 ? 0 : rows.Max(row => row.Length);
            headers = Enumerable.Range(1, width).Select(index => $"column{index.ToString(CultureInfo.InvariantCulture)}").ToArray();
            dataRows = rows;
        }

        var objects = dataRows.Select(row => ToObject(headers, row)).ToArray();
        return new CsvReadResponse(
            Path.GetFullPath(path),
            delimiter.ToString(),
            hasHeader,
            headers,
            dataRows,
            objects);
    }

    public CsvConvertResponse ToJson(CsvReadRequest request)
    {
        var response = Read(request);
        return new CsvConvertResponse(response.Path, response.Rows.Count, response.Objects);
    }

    private static Dictionary<string, string?> ToObject(IReadOnlyList<string> headers, IReadOnlyList<string> row)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            var header = string.IsNullOrWhiteSpace(headers[index])
                ? $"column{(index + 1).ToString(CultureInfo.InvariantCulture)}"
                : headers[index];
            result[header] = index < row.Count ? row[index] : null;
        }

        return result;
    }

    private static char GetDelimiter(string? delimiter)
    {
        if (string.IsNullOrEmpty(delimiter))
        {
            return ',';
        }

        if (delimiter.Length != 1)
        {
            throw new ArgumentException("CSV delimiter must be a single character.", nameof(delimiter));
        }

        return delimiter[0];
    }

    private string ResolvePath(string path)
    {
        return Path.IsPathRooted(path) || string.IsNullOrWhiteSpace(_contentRoot)
            ? path
            : Path.Combine(_contentRoot, path);
    }

    private static List<string[]> Parse(string text, char delimiter)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < text.Length; index++)
        {
            var c = text[index];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(field.ToString());
                field.Clear();
                rows.Add(row.ToArray());
                row.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        if (inQuotes)
        {
            throw new FormatException("CSV input ended inside a quoted field.");
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }
}
