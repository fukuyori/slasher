namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static ScriptCommandResult ExecuteArrayCommand(
        string verb,
        IReadOnlyList<string> args,
        ScriptExecutionState state,
        ScriptLine line)
    {
        switch (verb)
        {
            case "array":
            {
                RequireArgs(args, 1, "array requires a variable name.");
                var scoped = ParseScopedVariableArgs(args);
                var name = scoped.Name;
                var items = scoped.Args.Skip(1).Cast<object?>().ToList();
                state.AssignVariable(line, name, items, scoped.Scope);
                return new ScriptCommandResult(new { name, length = items.Count, items, scope = ScopeName(scoped.Scope) }, AssignmentValue: items);
            }
            case "push":
            {
                RequireArgs(args, 2, "push requires an array name and one or more values.");
                var scoped = ParseScopedVariableArgs(args);
                var name = scoped.Name;
                var items = state.GetOrCreateArray(line, name, scoped.Scope);
                foreach (var value in scoped.Args.Skip(1))
                {
                    items.Add(value);
                }

                return new ScriptCommandResult(new { name, length = items.Count, items, scope = ScopeName(scoped.Scope) }, AssignmentValue: items);
            }
            case "pop":
            {
                RequireArgs(args, 1, "pop requires an array name.");
                var scoped = ParseScopedVariableArgs(args);
                var items = state.RequireArray(line, scoped.Name, scoped.Scope);
                if (items.Count == 0)
                {
                    throw new ScriptCommandException("array_empty", $"Array '{scoped.Name}' is empty.");
                }

                var value = items[^1];
                items.RemoveAt(items.Count - 1);
                return new ScriptCommandResult(value, AssignmentValue: value);
            }
            case "get":
            {
                RequireArgs(args, 2, "get requires an array name and index.");
                var scoped = ParseScopedVariableArgs(args);
                var items = state.RequireArray(line, scoped.Name, scoped.Scope);
                var index = ParseInt(scoped.Args[1], "index");
                if (index < 0 || index >= items.Count)
                {
                    throw new ScriptCommandException(
                        "array_index_out_of_range",
                        $"Array '{scoped.Name}' does not have index {index}.",
                        Expected: new { min = 0, max = items.Count - 1 },
                        Actual: new { index });
                }

                return new ScriptCommandResult(items[index], AssignmentValue: items[index]);
            }
            case "length":
            {
                RequireArgs(args, 1, "length requires an array name.");
                var scoped = ParseScopedVariableArgs(args);
                var value = state.RequireArray(line, scoped.Name, scoped.Scope).Count;
                return new ScriptCommandResult(value, AssignmentValue: value);
            }
            case "join":
            {
                RequireArgs(args, 1, "join requires an array name.");
                var scoped = ParseScopedVariableArgs(args);
                var separator = scoped.Args.Count >= 2 ? scoped.Args[1] : ",";
                var value = string.Join(separator, state.RequireArray(line, scoped.Name, scoped.Scope).Select(FormatVariableValue));
                return new ScriptCommandResult(value, AssignmentValue: value);
            }
            default:
                throw new ScriptCommandException("unsupported_command", $"Unsupported array command: {verb}");
        }
    }
}
