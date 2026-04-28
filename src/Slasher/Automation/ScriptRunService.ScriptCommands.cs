using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private ScriptCommandResult ExecuteScriptCommand(
        string verb,
        IReadOnlyList<string> args,
        ScriptExecutionState state,
        ScriptLine line)
    {
        switch (verb)
        {
            case "fail":
            {
                var message = JoinArgs(args, 0) ?? "Script failed explicitly.";
                throw new ScriptCommandException("explicit_failure", message, Recoverable: false);
            }
            case "return":
            {
                if (!state.IsInsideFunction)
                {
                    throw new ScriptCommandException("return_outside_function", "return can only be used inside a function.");
                }

                var value = JoinArgs(args, 0) ?? string.Empty;
                state.SetReturn(value);
                return new ScriptCommandResult(new { returned = true, value }, AssignmentValue: value);
            }
            case "log":
            {
                var message = JoinArgs(args, 0) ?? string.Empty;
                var entry = new AutomationLogEntry(DateTimeOffset.UtcNow, "info", "script", message);
                return new ScriptCommandResult(new { logged = true, message }, Logs: [entry]);
            }
            case "step":
            {
                var stepName = JoinArgs(args, 0) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(stepName))
                {
                    throw new ScriptCommandException("invalid_step", "step requires a name.");
                }

                var entry = new AutomationLogEntry(DateTimeOffset.UtcNow, "info", "script", $"Step: {stepName}");
                return new ScriptCommandResult(new { step = stepName }, Logs: [entry], StepName: stepName);
            }
            case "test":
            {
                if (args.Count >= 2 && args[0].Equals("step", StringComparison.OrdinalIgnoreCase))
                {
                    var stepName = JoinArgs(args, 1) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(stepName))
                    {
                        throw new ScriptCommandException("invalid_step", "test step requires a name.");
                    }

                    var entry = new AutomationLogEntry(DateTimeOffset.UtcNow, "info", "script", $"Step: {stepName}");
                    return new ScriptCommandResult(new { step = stepName }, Logs: [entry], StepName: stepName);
                }

                if (args.Count >= 2 && args[0].Equals("attach", StringComparison.OrdinalIgnoreCase))
                {
                    return ExecuteTestAttachCommand(args, state, line);
                }

                throw new ScriptCommandException("unsupported_test_command", "test supports: test step <name>, test attach <path> [as <role>].");
            }
            case "agent":
            {
                if (args.Count >= 2 && args[0].Equals("note", StringComparison.OrdinalIgnoreCase))
                {
                    var message = JoinArgs(args, 1) ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        throw new ScriptCommandException("invalid_agent_note", "agent note requires a message.");
                    }

                    var entry = new AutomationLogEntry(DateTimeOffset.UtcNow, "note", "agent", message);
                    return new ScriptCommandResult(
                        new { note = message },
                        Logs: [entry]);
                }

                throw new ScriptCommandException("unsupported_agent_command", "agent supports: agent note <message>.");
            }
            default:
                throw new ScriptCommandException("unsupported_command", $"Unsupported script command: {verb}");
        }
    }

    private ScriptCommandResult ExecuteTestAttachCommand(
        IReadOnlyList<string> args,
        ScriptExecutionState state,
        ScriptLine line)
    {
        var path = args[1];
        var role = ParseAttachmentRole(args);
        var fullPath = ResolveAttachmentPath(path, line);
        var evidence = _artifacts.SaveAttachment(
            state.Report,
            fullPath,
            role,
            new Dictionary<string, object?>
            {
                ["sourceFile"] = line.SourceFile,
                ["sourceLine"] = line.Line
            });
        var entry = new AutomationLogEntry(
            DateTimeOffset.UtcNow,
            "info",
            "script",
            $"Attached {Path.GetFileName(fullPath)} as {evidence.Role}",
            new Dictionary<string, object?>
            {
                ["path"] = evidence.Path,
                ["role"] = evidence.Role
            });

        return new ScriptCommandResult(
            new { attached = true, evidence.Path, evidence.Role },
            Logs: [entry],
            Evidence: [evidence]);
    }

    private static string ParseAttachmentRole(IReadOnlyList<string> args)
    {
        if (args.Count <= 2)
        {
            return "attachment";
        }

        if (args.Count >= 4
            && (args[2].Equals("as", StringComparison.OrdinalIgnoreCase)
                || args[2].Equals("role", StringComparison.OrdinalIgnoreCase)))
        {
            return JoinArgs(args, 3) ?? "attachment";
        }

        return JoinArgs(args, 2) ?? "attachment";
    }

    private string ResolveAttachmentPath(string path, ScriptLine line)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ScriptCommandException("invalid_attachment_path", "Attachment path is required.");
        }

        var baseDirectory = line.SourceFile.Equals("inline-script", StringComparison.OrdinalIgnoreCase)
            ? _workspaceRoot
            : Path.GetDirectoryName(Path.Combine(_workspaceRoot, line.SourceFile)) ?? _workspaceRoot;
        var fullPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDirectory, path));

        if (!File.Exists(fullPath))
        {
            throw new ScriptCommandException(
                "attachment_file_not_found",
                $"Attachment file '{path}' was not found.",
                details: new Dictionary<string, object?> { ["resolvedPath"] = fullPath });
        }

        return fullPath;
    }
}
