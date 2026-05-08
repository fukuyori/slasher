using System.IO.Compression;
using Slasher.Api;

namespace Slasher.Files;

public sealed partial class FileSystemAutomationService
{
    public object CreateFolder(string path)
    {
        var directory = Directory.CreateDirectory(path);
        return new { created = true, path = directory.FullName };
    }

    public object CopyFolder(FileOperationRequest request)
    {
        RequireDestination(request);
        if (request.Overwrite)
        {
            RequireDestructiveApproval(request, "folder.copy.overwrite");
        }

        var plan = CreatePlan("folder.copy", request, [request.Path], request.Overwrite, request.Destination);
        if (request.DryRun)
        {
            return plan;
        }

        CopyDirectory(request.Path, request.Destination!, request.Overwrite);
        return new { copied = true, source = Path.GetFullPath(request.Path), destination = Path.GetFullPath(request.Destination!) };
    }

    public object DeleteFolder(FileOperationRequest request)
    {
        RequireDestructiveApproval(request, "folder.delete");
        var plan = CreatePlan("folder.delete", request, [request.Path], destructive: true);
        if (request.DryRun)
        {
            return plan;
        }

        Directory.Delete(request.Path, request.Recursive);
        return new { deleted = true, path = Path.GetFullPath(request.Path), plan };
    }

    public object RenameFolder(FileOperationRequest request)
    {
        RequireDestination(request);
        var plan = CreatePlan("folder.rename", request, [request.Path], destructive: false, request.Destination);
        if (request.DryRun)
        {
            return plan;
        }

        Directory.Move(request.Path, request.Destination!);
        return new { renamed = true, path = Path.GetFullPath(request.Destination!) };
    }

    public object ZipFolder(FileOperationRequest request)
    {
        RequireDestination(request);
        if (File.Exists(request.Destination) && request.Overwrite)
        {
            RequireDestructiveApproval(request, "folder.zip.overwrite");
        }

        var plan = CreatePlan("folder.zip", request, [request.Path], File.Exists(request.Destination) && request.Overwrite, request.Destination);
        if (request.DryRun)
        {
            return plan;
        }

        if (File.Exists(request.Destination) && request.Overwrite)
        {
            File.Delete(request.Destination);
        }

        ZipFile.CreateFromDirectory(request.Path, request.Destination!);
        return new { zipped = true, path = Path.GetFullPath(request.Destination!) };
    }

    public object Unzip(FileOperationRequest request)
    {
        RequireDestination(request);
        if (request.Overwrite)
        {
            RequireDestructiveApproval(request, "folder.unzip.overwrite");
        }

        var plan = CreatePlan("folder.unzip", request, [request.Path], request.Overwrite, request.Destination);
        if (request.DryRun)
        {
            return plan;
        }

        ZipFile.ExtractToDirectory(request.Path, request.Destination!, request.Overwrite);
        return new { unzipped = true, path = Path.GetFullPath(request.Destination!) };
    }
}

