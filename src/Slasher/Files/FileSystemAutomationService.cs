using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using Slasher.Api;

namespace Slasher.Files;

public sealed partial class FileSystemAutomationService
{
    public object CopyFile(FileOperationRequest request)
    {
        RequireDestination(request);
        var sources = ResolveFiles(request.Path, request.UseRegex);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.Destination!)) ?? ".");

        if (sources.Count == 1 && !Directory.Exists(request.Destination))
        {
            File.Copy(sources[0], request.Destination!, request.Overwrite);
            return new { copied = 1, destination = Path.GetFullPath(request.Destination!) };
        }

        Directory.CreateDirectory(request.Destination!);
        foreach (var source in sources)
        {
            File.Copy(source, Path.Combine(request.Destination!, Path.GetFileName(source)), request.Overwrite);
        }

        return new { copied = sources.Count, destination = Path.GetFullPath(request.Destination!) };
    }

    public object DeleteFile(FileOperationRequest request)
    {
        var files = ResolveFiles(request.Path, request.UseRegex);
        foreach (var file in files)
        {
            File.Delete(file);
        }

        return new { deleted = files.Count };
    }

    public object RenameFile(FileOperationRequest request)
    {
        RequireDestination(request);
        File.Move(request.Path, request.Destination!, request.Overwrite);
        return new { renamed = true, path = Path.GetFullPath(request.Destination!) };
    }

    public object OpenPath(string path, string verb = "open")
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true,
            Verb = verb
        };
        var process = Process.Start(startInfo);
        return new { started = process is not null, processId = process?.Id };
    }

    public FileInfoResponse GetInfo(string path)
    {
        if (Directory.Exists(path))
        {
            var directory = new DirectoryInfo(path);
            return new FileInfoResponse(directory.Name, directory.FullName, true, true, null, directory.CreationTime, directory.LastWriteTime, directory.Attributes);
        }

        if (File.Exists(path))
        {
            var file = new FileInfo(path);
            return new FileInfoResponse(file.Name, file.FullName, true, false, file.Length, file.CreationTime, file.LastWriteTime, file.Attributes);
        }

        return new FileInfoResponse(Path.GetFileName(path), Path.GetFullPath(path), false, false, null, null, null, null);
    }
}

