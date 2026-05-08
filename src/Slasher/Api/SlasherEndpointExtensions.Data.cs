using Slasher.Data;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapDataEndpoints(WebApplication app)
    {
        app.MapPost("/data/csv/read", (CsvReadRequest request, CsvAutomationService csv) =>
            Results.Ok(csv.Read(request)));

        app.MapPost("/data/csv/to-json", (CsvReadRequest request, CsvAutomationService csv) =>
            Results.Ok(csv.ToJson(request)));

        app.MapPost("/data/json/read", (JsonReadRequest request, JsonAutomationService json) =>
            Results.Ok(json.Read(request)));

        app.MapPost("/data/json/query", (JsonQueryRequest request, JsonAutomationService json) =>
            Results.Ok(json.Query(request)));

        app.MapPost("/data/json/write", (JsonWriteRequest request, JsonAutomationService json) =>
            Results.Ok(json.Write(request)));
    }
}
