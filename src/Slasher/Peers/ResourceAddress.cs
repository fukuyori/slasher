namespace Slasher.Peers;

public sealed record ResourceAddress(string Path, IReadOnlyList<string> Segments)
{
    public static ResourceAddress Parse(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return new ResourceAddress("/", []);
        }

        if (!path.StartsWith('/'))
        {
            throw new ArgumentException("Resource path must start with '/'.", nameof(path));
        }

        if (path.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("Resource path must use '/' separators.", nameof(path));
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or ".." || segment.Length == 0))
        {
            throw new ArgumentException("Resource path contains an invalid segment.", nameof(path));
        }

        return new ResourceAddress("/" + string.Join('/', segments), segments);
    }
}
