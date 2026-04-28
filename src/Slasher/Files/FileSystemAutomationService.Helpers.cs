using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Slasher.Api;

namespace Slasher.Files;

public sealed partial class FileSystemAutomationService
{
    private static void RequireDestination(FileOperationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Destination))
        {
            throw new ArgumentException("Destination is required.");
        }
    }

    private static IReadOnlyList<string> ResolveFiles(string path, bool useRegex)
    {
        if (useRegex)
        {
            var directory = Path.GetDirectoryName(path);
            var pattern = Path.GetFileName(path);
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            return Directory.EnumerateFiles(string.IsNullOrWhiteSpace(directory) ? "." : directory)
                .Where(file => regex.IsMatch(Path.GetFileName(file)))
                .ToArray();
        }

        if (path.IndexOfAny(['*', '?']) >= 0)
        {
            var directory = Path.GetDirectoryName(path);
            var pattern = Path.GetFileName(path);
            return Directory.GetFiles(string.IsNullOrWhiteSpace(directory) ? "." : directory, pattern);
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File was not found.", path);
        }

        return [path];
    }

    private static void CopyDirectory(string source, string destination, bool overwrite)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)), overwrite);
        }
    }
}

