using Slasher.Automation;
using Slasher.Files;
using Slasher.Windows;

namespace Slasher.Api;

public static partial class SlasherEndpointExtensions
{
    private static void MapFileSystemEndpoints(WebApplication app)
    {
        app.MapPost("/files/copy", (FileOperationRequest request, FileSystemAutomationService files) => Results.Ok(files.CopyFile(request)));
        app.MapPost("/files/delete", (FileOperationRequest request, FileSystemAutomationService files) => Results.Ok(files.DeleteFile(request)));
        app.MapPost("/files/rename", (FileOperationRequest request, FileSystemAutomationService files) => Results.Ok(files.RenameFile(request)));
        app.MapPost("/files/open", (FileOperationRequest request, FileSystemAutomationService files) => Results.Ok(files.OpenPath(request.Path)));
        app.MapPost("/files/print", (FileOperationRequest request, FileSystemAutomationService files) => Results.Ok(files.OpenPath(request.Path, "print")));
        app.MapGet("/files/info", (string path, FileSystemAutomationService files) => Results.Ok(files.GetInfo(path)));

        app.MapPost("/folders/create", (FileOperationRequest request, FileSystemAutomationService files) => Results.Ok(files.CreateFolder(request.Path)));
        app.MapPost("/folders/copy", (FileOperationRequest request, FileSystemAutomationService files) => Results.Ok(files.CopyFolder(request)));
        app.MapPost("/folders/delete", (FileOperationRequest request, FileSystemAutomationService files) => Results.Ok(files.DeleteFolder(request)));
        app.MapPost("/folders/rename", (FileOperationRequest request, FileSystemAutomationService files) => Results.Ok(files.RenameFolder(request)));
        app.MapPost("/folders/open", (FileOperationRequest request, FileSystemAutomationService files) => Results.Ok(files.OpenPath(request.Path)));
        app.MapPost("/folders/zip", (FileOperationRequest request, FileSystemAutomationService files) => Results.Ok(files.ZipFolder(request)));
        app.MapPost("/folders/unzip", (FileOperationRequest request, FileSystemAutomationService files) => Results.Ok(files.Unzip(request)));

        app.MapPost("/shortcuts", (ShortcutRequest request, FileSystemAutomationService files) => Results.Ok(files.CreateShortcut(request)));
        app.MapPost("/symlinks", (SymbolicLinkRequest request, FileSystemAutomationService files) => Results.Ok(files.CreateSymbolicLink(request)));
    }
}

