using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static ScopedVariableArgs ParseScopedVariableArgs(IReadOnlyList<string> args)
    {
        RequireArgs(args, 1, "A variable name is required.");
        var scope = TryParseVariableScope(args[0]);
        var remaining = scope is null ? args : args.Skip(1).ToArray();
        RequireArgs(remaining, 1, "A variable name is required.");
        return new ScopedVariableArgs(scope, remaining[0], remaining);
    }

    private static ScriptVariableScope? TryParseVariableScope(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "global" => ScriptVariableScope.Global,
            "file" or "filelocal" or "file-local" => ScriptVariableScope.File,
            "local" => ScriptVariableScope.Local,
            _ => null
        };
    }

    private static string ScopeName(ScriptVariableScope? scope)
    {
        return scope switch
        {
            ScriptVariableScope.Global => "global",
            ScriptVariableScope.File => "file",
            ScriptVariableScope.Local => "local",
            _ => "global"
        };
    }

    private static object AssignVariable(IDictionary<string, object?> variables, string name, object? value)
    {
        if (!IsValidVariableName(name))
        {
            throw new ScriptCommandException("invalid_variable_name", "Variable names must start with a letter or underscore and contain only letters, digits, and underscores.");
        }

        variables[name] = value;
        return value ?? string.Empty;
    }

    private static List<object?> GetOrCreateArray(IDictionary<string, object?> variables, string name)
    {
        if (!variables.TryGetValue(name, out var value))
        {
            var created = new List<object?>();
            AssignVariable(variables, name, created);
            return created;
        }

        return ToMutableArray(name, value);
    }

    private static List<object?> RequireArray(IDictionary<string, object?> variables, string name)
    {
        if (!variables.TryGetValue(name, out var value))
        {
            throw new ScriptCommandException("variable_not_found", $"Variable '{name}' was not found.");
        }

        return ToMutableArray(name, value);
    }

    private static List<object?> ToMutableArray(string name, object? value)
    {
        return value switch
        {
            List<object?> mutable => mutable,
            object?[] array => array.ToList(),
            IReadOnlyList<object?> list => list.ToList(),
            _ => throw new ScriptCommandException("variable_not_array", $"Variable '{name}' is not an array.")
        };
    }

    private static bool IsValidVariableName(string name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && (char.IsLetter(name[0]) || name[0] == '_')
            && name.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
    }

    private static IReadOnlyDictionary<string, object?> SnapshotVariables(IDictionary<string, object?> variables)
    {
        return variables.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
    }
}

