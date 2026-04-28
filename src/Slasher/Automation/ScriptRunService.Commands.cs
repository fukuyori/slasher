using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<ScriptCommandResult> ExecuteCommandAsync(
        string command,
        string? selectedHandle,
        ScriptExecutionState state,
        ScriptLine line,
        CancellationToken cancellationToken)
    {
        var tokens = ParseCommandLine(command);
        if (tokens.Count == 0)
        {
            return new ScriptCommandResult(null);
        }

        var supportsAssignment = tokens.Count < 2
            || !tokens[0].Equals("test", StringComparison.OrdinalIgnoreCase)
            || !tokens[1].Equals("attach", StringComparison.OrdinalIgnoreCase);
        var assignment = supportsAssignment
            ? SplitAssignmentSuffix(tokens)
            : new ScriptAssignment(tokens, null);
        tokens = assignment.Tokens;
        var verb = tokens[0].ToLowerInvariant();
        var args = tokens.Skip(1).ToArray();
        var variables = state.ResolveVariables(line);

        var result = await DispatchCommandAsync(verb, args, selectedHandle, state, line, variables, cancellationToken);
        return assignment.VariableName is null
            ? result
            : result with { AssignmentName = assignment.VariableName };
    }

    private async Task<ScriptCommandResult> DispatchCommandAsync(
        string verb,
        IReadOnlyList<string> args,
        string? selectedHandle,
        ScriptExecutionState state,
        ScriptLine line,
        Dictionary<string, object?> variables,
        CancellationToken cancellationToken)
    {
        if (IsWindowsCommand(verb))
        {
            return await ExecuteWindowsCommandAsync(verb, args, selectedHandle, line, cancellationToken);
        }

        if (IsFileSystemCommand(verb))
        {
            return ExecuteFileSystemCommand(verb, args);
        }

        if (verb == "clipboard")
        {
            return ExecuteClipboardCommand(args, selectedHandle);
        }

        if (verb == "browser")
        {
            return ExecuteBrowserCommand(args, selectedHandle);
        }

        if (verb == "assert")
        {
            return await ExecuteAssertAsync(args, selectedHandle, variables, line, cancellationToken);
        }

        if (IsScriptCommand(verb))
        {
            return ExecuteScriptCommand(verb, args, state, line);
        }

        if (IsVariableCommand(verb))
        {
            return ExecuteVariableCommand(verb, args, state, line);
        }

        throw new ScriptCommandException("unsupported_command", $"Unsupported server-side script command: {verb}");
    }

    private static bool IsWindowsCommand(string verb)
    {
        return verb is
            "start" or
            "wait" or
            "app" or
            "application" or
            "select" or
            "foreground" or
            "focus" or
            "restore" or
            "maximize" or
            "minimize" or
            "hide" or
            "show" or
            "move" or
            "text" or
            "type" or
            "keys" or
            "key" or
            "mouse" or
            "element" or
            "image" or
            "capture" or
            "close";
    }

    private static bool IsScriptCommand(string verb)
    {
        return verb is "fail" or "return" or "log" or "step" or "test" or "agent";
    }

    private static bool IsFileSystemCommand(string verb)
    {
        return verb is "file" or "folder" or "shortcut" or "symlink" or "symboliclink" or "symbolic-link";
    }

    private static bool IsVariableCommand(string verb)
    {
        return verb is
            "set" or
            "let" or
            "unset" or
            "add" or
            "inc" or
            "vars" or
            "variables" or
            "array" or
            "push" or
            "pop" or
            "get" or
            "length" or
            "join";
    }
}
