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
        if (request.Overwrite)
        {
            RequireDestructiveApproval(request, "file.copy.overwrite");
        }

        var plan = CreatePlan("file.copy", request, sources, request.Overwrite, request.Destination);
        if (request.DryRun)
        {
            return plan;
        }

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
        RequireDestructiveApproval(request, "file.delete");
        var plan = CreatePlan("file.delete", request, files, destructive: true);
        if (request.DryRun)
        {
            return plan;
        }

        foreach (var file in files)
        {
            File.Delete(file);
        }

        return new { deleted = files.Count, plan };
    }

    public object RenameFile(FileOperationRequest request)
    {
        RequireDestination(request);
        if (request.Overwrite)
        {
            RequireDestructiveApproval(request, "file.rename.overwrite");
        }

        var plan = CreatePlan("file.rename", request, [request.Path], request.Overwrite, request.Destination);
        if (request.DryRun)
        {
            return plan;
        }

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

