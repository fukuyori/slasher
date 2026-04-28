namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<ScriptCommandResult> ExecuteWindowsCommandAsync(
        string verb,
        IReadOnlyList<string> args,
        string? selectedHandle,
        ScriptLine line,
        CancellationToken cancellationToken)
    {
        switch (verb)
        {
            case "start":
            case "app":
            case "application":
            case "select":
            case "foreground":
                return ExecuteAppCommand(verb, args);
            case "wait":
                return await ExecuteWaitCommandAsync(args, selectedHandle, cancellationToken);
            case "focus":
            case "restore":
            case "maximize":
            case "minimize":
            case "hide":
            case "show":
            case "move":
            case "close":
                return ExecuteWindowCommand(verb, args, selectedHandle);
            case "text":
            case "type":
            case "keys":
            case "key":
                return ExecuteInputCommand(verb, args, selectedHandle);
            case "mouse":
                return ExecuteMouseCommand(args);
            case "element":
                return ExecuteElementCommand(args, selectedHandle);
            case "image":
                return ExecuteImageCommand(args, selectedHandle, line);
            case "capture":
                return ExecuteCaptureCommand(args, selectedHandle);
            default:
                throw new ScriptCommandException("unsupported_command", $"Unsupported Windows command: {verb}");
        }
    }
}
