using System.Text.Json.Nodes;

namespace Slasher.Data;

public sealed record JsonReadRequest(string Path);

public sealed record JsonQueryRequest(string Path, string Pointer);

public sealed record JsonWriteRequest(string Path, JsonNode Value);

public sealed record JsonReadResponse(string Path, string Kind, JsonNode Value);

public sealed record JsonQueryResponse(string Path, string Pointer, string? Kind, JsonNode? Value);

public sealed record JsonWriteResponse(string Path, bool Written);
