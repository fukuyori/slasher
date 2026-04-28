using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private ScriptCommandResult ExecuteFileSystemCommand(string verb, IReadOnlyList<string> args)
    {
        return verb switch
        {
            "file" => ExecuteFileCommand(args),
            "folder" => ExecuteFolderCommand(args),
            "shortcut" => ExecuteShortcutCommand(args),
            "symlink" or "symboliclink" or "symbolic-link" => ExecuteSymbolicLinkCommand(args),
            _ => throw new ScriptCommandException("unsupported_file_command", $"Unsupported file system command: {verb}")
        };
    }

    private ScriptCommandResult ExecuteFileCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 2, "file syntax is: file <copy|delete|rename|open|print|info> <path> [...]");
        var action = args[0].ToLowerInvariant();
        var result = action switch
        {
            "copy" => _files.CopyFile(CreateFileOperation(args, requireDestination: true)),
            "delete" => _files.DeleteFile(CreateFileOperation(args, requireDestination: false)),
            "rename" => _files.RenameFile(CreateFileOperation(args, requireDestination: true)),
            "open" => _files.OpenPath(args[1]),
            "print" => _files.OpenPath(args[1], "print"),
            "info" => _files.GetInfo(args[1]),
            _ => throw new ScriptCommandException("unsupported_file_command", "file supports copy, delete, rename, open, print, and info.")
        };

        return new ScriptCommandResult(result, AssignmentValue: result);
    }

    private ScriptCommandResult ExecuteFolderCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 2, "folder syntax is: folder <create|copy|delete|rename|open|zip|unzip> <path> [...]");
        var action = args[0].ToLowerInvariant();
        var result = action switch
        {
            "create" => _files.CreateFolder(args[1]),
            "copy" => _files.CopyFolder(CreateFileOperation(args, requireDestination: true)),
            "delete" => _files.DeleteFolder(CreateFileOperation(args, requireDestination: false, defaultRecursive: true)),
            "rename" => _files.RenameFolder(CreateFileOperation(args, requireDestination: true)),
            "open" => _files.OpenPath(args[1]),
            "zip" => _files.ZipFolder(CreateFileOperation(args, requireDestination: true)),
            "unzip" => _files.Unzip(CreateFileOperation(args, requireDestination: true)),
            _ => throw new ScriptCommandException("unsupported_folder_command", "folder supports create, copy, delete, rename, open, zip, and unzip.")
        };

        return new ScriptCommandResult(result, AssignmentValue: result);
    }

    private ScriptCommandResult ExecuteShortcutCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 2, "shortcut syntax is: shortcut <targetPath> <shortcutPath> [arguments] [workingDirectory].");
        var request = new ShortcutRequest(
            args[0],
            args[1],
            args.Count >= 3 ? args[2] : null,
            args.Count >= 4 ? args[3] : null);
        var result = _files.CreateShortcut(request);
        return new ScriptCommandResult(result, AssignmentValue: result);
    }

    private ScriptCommandResult ExecuteSymbolicLinkCommand(IReadOnlyList<string> args)
    {
        RequireArgs(args, 2, "symlink syntax is: symlink <linkPath> <targetPath> [file|directory].");
        var isDirectory = args.Count >= 3 && args[2].Equals("directory", StringComparison.OrdinalIgnoreCase);
        var result = _files.CreateSymbolicLink(new SymbolicLinkRequest(args[0], args[1], isDirectory));
        return new ScriptCommandResult(result, AssignmentValue: result);
    }

    private static FileOperationRequest CreateFileOperation(
        IReadOnlyList<string> args,
        bool requireDestination,
        bool defaultRecursive = false)
    {
        RequireArgs(args, requireDestination ? 3 : 2, requireDestination
            ? $"{args[0]} requires a path and destination."
            : $"{args[0]} requires a path.");

        var optionStart = requireDestination ? 3 : 2;
        var options = args.Skip(optionStart).ToArray();
        return new FileOperationRequest(
            args[1],
            requireDestination ? args[2] : null,
            Overwrite: HasFlag(options, "--overwrite", "overwrite"),
            Recursive: defaultRecursive || HasFlag(options, "--recursive", "recursive"),
            UseRegex: HasFlag(options, "--regex", "regex"));
    }

    private static bool HasFlag(IReadOnlyList<string> values, params string[] names)
    {
        return values.Any(value => names.Any(name => value.Equals(name, StringComparison.OrdinalIgnoreCase)));
    }
}
