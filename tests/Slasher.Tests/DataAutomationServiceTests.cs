using Slasher.Data;
using System.IO.Compression;
using Xunit;

namespace Slasher.Tests;

public sealed class DataAutomationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "slasher-data-tests", Guid.NewGuid().ToString("N"));

    public DataAutomationServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void CsvRead_ParsesHeadersAndQuotedFields()
    {
        var path = Path.Combine(_root, "items.csv");
        File.WriteAllText(path, "name,notes\r\nalpha,\"a,b\"\r\nbeta,\"quoted \"\"text\"\"\"\r\n");

        var response = new CsvAutomationService().Read(new CsvReadRequest(path));

        Assert.Equal(["name", "notes"], response.Headers);
        Assert.Equal(2, response.Rows.Count);
        Assert.Equal("a,b", response.Objects[0]["notes"]);
        Assert.Equal("quoted \"text\"", response.Objects[1]["notes"]);
    }

    [Fact]
    public void JsonQuery_SelectsPointer()
    {
        var path = Path.Combine(_root, "items.json");
        File.WriteAllText(path, """{"items":[{"name":"alpha"},{"name":"beta"}]}""");

        var response = new JsonAutomationService().Query(new JsonQueryRequest(path, "/items/1/name"));

        Assert.Equal("value", response.Kind);
        Assert.Equal("beta", response.Value?.GetValue<string>());
    }

    [Fact]
    public void ExcelRead_ReadsBasicXlsxWorksheet()
    {
        var path = Path.Combine(_root, "items.xlsx");
        CreateWorkbook(path);

        var response = new ExcelAutomationService().Read(new ExcelReadRequest(path));

        Assert.Equal("Sheet1", response.Sheet);
        Assert.Equal(["name", "count"], response.Headers);
        Assert.Equal(2, response.Rows.Count);
        Assert.Equal("alpha", response.Objects[0]["name"]);
        Assert.Equal("2", response.Objects[1]["count"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static void CreateWorkbook(string path)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "xl/workbook.xml",
            """
            <?xml version="1.0" encoding="utf-8"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>
                <sheet name="Sheet1" sheetId="1" r:id="rId1" />
              </sheets>
            </workbook>
            """);
        WriteEntry(archive, "xl/_rels/workbook.xml.rels",
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
            </Relationships>
            """);
        WriteEntry(archive, "xl/sharedStrings.xml",
            """
            <?xml version="1.0" encoding="utf-8"?>
            <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <si><t>name</t></si>
              <si><t>count</t></si>
              <si><t>alpha</t></si>
              <si><t>beta</t></si>
            </sst>
            """);
        WriteEntry(archive, "xl/worksheets/sheet1.xml",
            """
            <?xml version="1.0" encoding="utf-8"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetData>
                <row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c></row>
                <row r="2"><c r="A2" t="s"><v>2</v></c><c r="B2"><v>1</v></c></row>
                <row r="3"><c r="A3" t="s"><v>3</v></c><c r="B3"><v>2</v></c></row>
              </sheetData>
            </worksheet>
            """);
    }

    private static void WriteEntry(ZipArchive archive, string path, string text)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(text);
    }
}
