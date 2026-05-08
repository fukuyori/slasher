using System.Text.Json;
using System.Text.Json.Nodes;

namespace Slasher.Data;

public sealed class JsonAutomationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public JsonReadResponse Read(JsonReadRequest request)
    {
        var text = File.ReadAllText(request.Path);
        var node = JsonNode.Parse(text) ?? throw new FormatException("JSON input is empty.");
        return new JsonReadResponse(Path.GetFullPath(request.Path), GetKind(node), node);
    }

    public JsonQueryResponse Query(JsonQueryRequest request)
    {
        var read = Read(new JsonReadRequest(request.Path));
        var selected = Select(read.Value, request.Pointer);
        return new JsonQueryResponse(read.Path, request.Pointer, selected is null ? null : GetKind(selected), selected);
    }

    public JsonWriteResponse Write(JsonWriteRequest request)
    {
        var fullPath = Path.GetFullPath(request.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
        File.WriteAllText(fullPath, request.Value.ToJsonString(SerializerOptions));
        return new JsonWriteResponse(fullPath, true);
    }

    private static JsonNode? Select(JsonNode node, string pointer)
    {
        if (string.IsNullOrEmpty(pointer) || pointer == "/")
        {
            return node;
        }

        if (!pointer.StartsWith('/'))
        {
            throw new ArgumentException("JSON pointer must be empty or start with '/'.", nameof(pointer));
        }

        JsonNode? current = node;
        foreach (var rawSegment in pointer.Split('/').Skip(1))
        {
            if (current is null)
            {
                return null;
            }

            var segment = rawSegment.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            current = current switch
            {
                JsonObject obj => obj[segment],
                JsonArray array when int.TryParse(segment, out var index) && index >= 0 && index < array.Count => array[index],
                _ => null
            };
        }

        return current;
    }

    private static string GetKind(JsonNode node)
    {
        return node switch
        {
            JsonObject => "object",
            JsonArray => "array",
            JsonValue => "value",
            _ => "unknown"
        };
    }
}
