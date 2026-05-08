using Slasher.Data;
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

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
