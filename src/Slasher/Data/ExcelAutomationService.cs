using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Slasher.Data;

public sealed class ExcelAutomationService
{
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

    public ExcelWorkbookResponse GetWorkbook(ExcelReadRequest request)
    {
        using var archive = ZipFile.OpenRead(request.Path);
        var workbook = LoadXml(archive, "xl/workbook.xml");
        var sheets = workbook.Root?
            .Element(Spreadsheet + "sheets")?
            .Elements(Spreadsheet + "sheet")
            .Select(sheet => sheet.Attribute("name")?.Value)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToArray() ?? [];

        return new ExcelWorkbookResponse(Path.GetFullPath(request.Path), sheets);
    }

    public ExcelReadResponse Read(ExcelReadRequest request)
    {
        using var archive = ZipFile.OpenRead(request.Path);
        var workbook = LoadXml(archive, "xl/workbook.xml");
        var workbookRels = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        var sharedStrings = LoadSharedStrings(archive);
        var sheetInfo = ResolveSheet(workbook, workbookRels, request.Sheet);
        var worksheet = LoadXml(archive, sheetInfo.Path);
        var rows = ReadRows(worksheet, sharedStrings);
        var hasHeader = request.HasHeader ?? true;

        string[] headers;
        IReadOnlyList<IReadOnlyList<string?>> dataRows;
        if (hasHeader && rows.Count > 0)
        {
            headers = rows[0].Select((value, index) => string.IsNullOrWhiteSpace(value) ? DefaultHeader(index) : value!).ToArray();
            dataRows = rows.Skip(1).ToArray();
        }
        else
        {
            var width = rows.Count == 0 ? 0 : rows.Max(row => row.Count);
            headers = Enumerable.Range(0, width).Select(DefaultHeader).ToArray();
            dataRows = rows;
        }

        var objects = dataRows.Select(row => ToObject(headers, row)).ToArray();
        return new ExcelReadResponse(
            Path.GetFullPath(request.Path),
            sheetInfo.Name,
            hasHeader,
            headers,
            dataRows,
            objects);
    }

    private static (string Name, string Path) ResolveSheet(XDocument workbook, XDocument rels, string? requestedSheet)
    {
        var sheets = workbook.Root?.Element(Spreadsheet + "sheets")?.Elements(Spreadsheet + "sheet").ToArray() ?? [];
        var selected = string.IsNullOrWhiteSpace(requestedSheet)
            ? sheets.FirstOrDefault()
            : sheets.FirstOrDefault(sheet => string.Equals(sheet.Attribute("name")?.Value, requestedSheet, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            throw new InvalidOperationException($"Worksheet '{requestedSheet ?? "<first>"}' was not found.");
        }

        var relationshipId = selected.Attribute(Relationships + "id")?.Value
            ?? throw new InvalidOperationException("Worksheet relationship id was not found.");
        var relationship = rels.Root?
            .Elements(PackageRelationships + "Relationship")
            .FirstOrDefault(rel => string.Equals(rel.Attribute("Id")?.Value, relationshipId, StringComparison.Ordinal));
        var target = relationship?.Attribute("Target")?.Value
            ?? throw new InvalidOperationException("Worksheet relationship target was not found.");
        var normalized = target.Replace('\\', '/');
        var path = normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : $"xl/{normalized.TrimStart('/')}";
        return (selected.Attribute("name")?.Value ?? "Sheet1", path);
    }

    private static string[] LoadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Root?
            .Elements(Spreadsheet + "si")
            .Select(ReadSharedString)
            .ToArray() ?? [];
    }

    private static string ReadSharedString(XElement element)
    {
        var text = element.Element(Spreadsheet + "t")?.Value;
        if (text is not null)
        {
            return text;
        }

        return string.Concat(element.Descendants(Spreadsheet + "t").Select(part => part.Value));
    }

    private static List<IReadOnlyList<string?>> ReadRows(XDocument worksheet, IReadOnlyList<string> sharedStrings)
    {
        var result = new List<IReadOnlyList<string?>>();
        var sheetData = worksheet.Root?.Element(Spreadsheet + "sheetData");
        if (sheetData is null)
        {
            return result;
        }

        foreach (var row in sheetData.Elements(Spreadsheet + "row"))
        {
            var values = new List<string?>();
            foreach (var cell in row.Elements(Spreadsheet + "c"))
            {
                var reference = cell.Attribute("r")?.Value;
                var index = reference is null ? values.Count : Math.Max(0, ColumnIndex(reference));
                while (values.Count < index)
                {
                    values.Add(null);
                }

                values.Add(ReadCell(cell, sharedStrings));
            }

            result.Add(values);
        }

        return result;
    }

    private static string? ReadCell(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = cell.Attribute("t")?.Value;
        if (type == "inlineStr")
        {
            return cell.Element(Spreadsheet + "is")?.Element(Spreadsheet + "t")?.Value;
        }

        var value = cell.Element(Spreadsheet + "v")?.Value;
        if (value is null)
        {
            return null;
        }

        if (type == "s" && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedIndex))
        {
            return sharedIndex >= 0 && sharedIndex < sharedStrings.Count ? sharedStrings[sharedIndex] : null;
        }

        return value;
    }

    private static int ColumnIndex(string reference)
    {
        var index = 0;
        foreach (var c in reference)
        {
            if (!char.IsLetter(c))
            {
                break;
            }

            index = (index * 26) + (char.ToUpperInvariant(c) - 'A' + 1);
        }

        return index - 1;
    }

    private static Dictionary<string, string?> ToObject(IReadOnlyList<string> headers, IReadOnlyList<string?> row)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            result[headers[index]] = index < row.Count ? row[index] : null;
        }

        return result;
    }

    private static string DefaultHeader(int index)
    {
        return $"column{(index + 1).ToString(CultureInfo.InvariantCulture)}";
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new FileNotFoundException($"Workbook entry '{path}' was not found.");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }
}
