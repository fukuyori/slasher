namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static ScriptCommandResult ExecuteValueAssert(IReadOnlyList<string> args)
    {
        RequireArgs(args, 3, "assert value syntax is: assert value <left> <operator> <right>.");
        var left = args[0];
        var op = args[1];
        var right = string.Join(' ', args.Skip(2));
        if (!CompareValues(left, op, right))
        {
            throw new ScriptCommandException(
                "assertion_failed",
                $"Value assertion failed. Expected '{left}' {op} '{right}'.",
                Expected: new { value = right, op },
                Actual: new { value = left });
        }

        return new ScriptCommandResult(new { asserted = true, left, op, right });
    }

    private static ScriptCommandResult ExecuteVariableAssert(
        IReadOnlyList<string> args,
        Dictionary<string, object?> variables)
    {
        RequireArgs(args, 2, "assert variable syntax is: assert variable [not] exists <name>.");
        var negate = args[0].Equals("not", StringComparison.OrdinalIgnoreCase);
        var existsIndex = negate ? 1 : 0;
        if (args.Count <= existsIndex || !args[existsIndex].Equals("exists", StringComparison.OrdinalIgnoreCase))
        {
            throw new ScriptCommandException("unsupported_assertion", "assert variable supports exists and not exists.");
        }

        RequireArgs(args.Skip(existsIndex + 1).ToArray(), 1, "assert variable exists requires a variable name.");
        var path = args[existsIndex + 1];
        var exists = VariableExists(variables, path);
        if (negate && exists)
        {
            throw new ScriptCommandException(
                "assertion_failed",
                $"Expected variable '{path}' not to exist.",
                Expected: new { exists = false, path },
                Actual: new { exists = true });
        }

        if (!negate && !exists)
        {
            throw new ScriptCommandException(
                "assertion_failed",
                $"Expected variable '{path}' to exist.",
                Expected: new { exists = true, path },
                Actual: new { exists = false });
        }

        return new ScriptCommandResult(new { asserted = true, exists = !negate, path });
    }
}
