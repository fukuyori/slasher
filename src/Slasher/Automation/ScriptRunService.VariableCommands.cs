namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static ScriptCommandResult ExecuteVariableCommand(
        string verb,
        IReadOnlyList<string> args,
        ScriptExecutionState state,
        ScriptLine line)
    {
        return verb switch
        {
            "set" or "let" or "unset" or "add" or "inc" or "vars" or "variables" =>
                ExecuteScalarVariableCommand(verb, args, state, line),
            "array" or "push" or "pop" or "get" or "length" or "join" =>
                ExecuteArrayCommand(verb, args, state, line),
            _ => throw new ScriptCommandException("unsupported_command", $"Unsupported variable command: {verb}")
        };
    }
}
