using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private string ResolveIncludePath(string path, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ScriptCommandException("invalid_include_path", "Include path is required.");
        }

        var fullPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDirectory, path));
        var root = Path.GetFullPath(_workspaceRoot);
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ScriptCommandException("include_path_outside_workspace", "Included script files must be inside the Slasher workspace.");
        }

        if (!File.Exists(fullPath))
        {
            throw new ScriptCommandException("include_file_not_found", $"Included script file '{path}' was not found.");
        }

        return fullPath;
    }

    private string ResolveScriptPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ScriptCommandException("invalid_script_path", "Script path is required.");
        }

        var fullPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(_workspaceRoot, path));
        var root = Path.GetFullPath(_workspaceRoot);
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ScriptCommandException("script_path_outside_workspace", "Script files must be inside the Slasher workspace.");
        }

        if (!File.Exists(fullPath))
        {
            throw new ScriptCommandException("script_file_not_found", $"Script file '{path}' was not found.");
        }

        return fullPath;
    }

    private string ResolveRuntimeFilePath(string path, ScriptLine line, string kind)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ScriptCommandException($"invalid_{kind}_path", $"{kind} path is required.");
        }

        var baseDirectory = line.SourceFile.Equals("inline-script", StringComparison.OrdinalIgnoreCase)
            ? _workspaceRoot
            : Path.GetDirectoryName(Path.Combine(_workspaceRoot, line.SourceFile)) ?? _workspaceRoot;
        var fullPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDirectory, path));
        if (!File.Exists(fullPath))
        {
            throw new ScriptCommandException($"{kind}_file_not_found", $"{kind} file '{path}' was not found.");
        }

        return fullPath;
    }
}

