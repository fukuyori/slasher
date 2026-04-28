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
        CopyDirectory(request.Path, request.Destination!, request.Overwrite);
        return new { copied = true, source = Path.GetFullPath(request.Path), destination = Path.GetFullPath(request.Destination!) };
    }

    public object DeleteFolder(FileOperationRequest request)
    {
        Directory.Delete(request.Path, request.Recursive);
        return new { deleted = true, path = Path.GetFullPath(request.Path) };
    }

    public object RenameFolder(FileOperationRequest request)
    {
        RequireDestination(request);
        Directory.Move(request.Path, request.Destination!);
        return new { renamed = true, path = Path.GetFullPath(request.Destination!) };
    }

    public object ZipFolder(FileOperationRequest request)
    {
        RequireDestination(request);
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
        ZipFile.ExtractToDirectory(request.Path, request.Destination!, request.Overwrite);
        return new { unzipped = true, path = Path.GetFullPath(request.Destination!) };
    }
}

