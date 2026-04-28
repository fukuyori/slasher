using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Slasher.Api;

namespace Slasher.Files;

public sealed partial class FileSystemAutomationService
{
    public object CreateShortcut(ShortcutRequest request)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Shortcut creation is only supported on Windows.");
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell is not available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(request.ShortcutPath);
        shortcut.TargetPath = request.TargetPath;
        shortcut.Arguments = request.Arguments ?? string.Empty;
        shortcut.WorkingDirectory = request.WorkingDirectory ?? Path.GetDirectoryName(request.TargetPath) ?? string.Empty;
        shortcut.Save();
        return new { created = true, path = Path.GetFullPath(request.ShortcutPath) };
    }

    public object CreateSymbolicLink(SymbolicLinkRequest request)
    {
        if (request.IsDirectory)
        {
            Directory.CreateSymbolicLink(request.LinkPath, request.TargetPath);
        }
        else
        {
            File.CreateSymbolicLink(request.LinkPath, request.TargetPath);
        }

        return new { created = true, path = Path.GetFullPath(request.LinkPath) };
    }
}

