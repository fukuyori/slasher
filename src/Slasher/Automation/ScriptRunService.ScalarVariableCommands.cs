namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static ScriptCommandResult ExecuteScalarVariableCommand(
        string verb,
        IReadOnlyList<string> args,
        ScriptExecutionState state,
        ScriptLine line)
    {
        switch (verb)
        {
            case "set":
            case "let":
            {
                RequireArgs(args, 2, "set requires a variable name and value.");
                var scoped = ParseScopedVariableArgs(args);
                var name = scoped.Name;
                var valueArgs = scoped.Args.Count > 1 && scoped.Args[1] == "=" ? scoped.Args.Skip(2).ToArray() : scoped.Args.Skip(1).ToArray();
                if (valueArgs.Length == 0)
                {
                    throw new ScriptCommandException("invalid_arguments", "set requires a variable value.");
                }

                var value = string.Join(' ', valueArgs);
                state.AssignVariable(line, name, value, scoped.Scope);
                return new ScriptCommandResult(new { name, value, scope = ScopeName(scoped.Scope) });
            }
            case "unset":
            {
                RequireArgs(args, 1, "unset requires a variable name.");
                var scoped = ParseScopedVariableArgs(args);
                var removed = state.RemoveVariable(line, scoped.Name, scoped.Scope);
                return new ScriptCommandResult(new { removed = scoped.Name, scope = ScopeName(scoped.Scope), found = removed });
            }
            case "add":
            case "inc":
            {
                RequireArgs(args, 1, "add requires a numeric variable name and optional amount.");
                var scoped = ParseScopedVariableArgs(args);
                var name = scoped.Name;
                var amount = scoped.Args.Count >= 2 ? ParseInt(scoped.Args[1], "amount") : 1;
                var current = state.TryGetVariable(line, name, scoped.Scope, out var value) && value is not null
                    ? ParseInt(FormatVariableValue(value), name)
                    : 0;
                var next = current + amount;
                state.AssignVariable(line, name, next, scoped.Scope);
                return new ScriptCommandResult(new { name, value = next, scope = ScopeName(scoped.Scope) }, AssignmentValue: next);
            }
            case "vars":
            case "variables":
                return new ScriptCommandResult(state.SnapshotVariables(line));
            default:
                throw new ScriptCommandException("unsupported_command", $"Unsupported scalar variable command: {verb}");
        }
    }
}
