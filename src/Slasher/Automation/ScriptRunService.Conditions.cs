using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static bool EvaluateCondition(string condition, IReadOnlyDictionary<string, object?> variables)
    {
        var tokens = ParseCommandLine(ExpandVariables(condition, variables)).ToList();
        if (tokens.Count == 0)
        {
            return false;
        }

        var negate = tokens[0].Equals("not", StringComparison.OrdinalIgnoreCase);
        if (negate)
        {
            tokens.RemoveAt(0);
        }

        bool result;
        if (tokens.Count == 0)
        {
            result = false;
        }
        else if (tokens[0].Equals("exists", StringComparison.OrdinalIgnoreCase))
        {
            result = VariableExists(variables, tokens.ElementAtOrDefault(1) ?? string.Empty);
        }
        else if (tokens[0].Equals("empty", StringComparison.OrdinalIgnoreCase))
        {
            result = string.IsNullOrEmpty(string.Join(' ', tokens.Skip(1)));
        }
        else if (tokens.Count == 1)
        {
            result = Truthy(tokens[0]);
        }
        else
        {
            result = CompareValues(tokens[0], tokens[1], string.Join(' ', tokens.Skip(2)));
        }

        return negate ? !result : result;
    }

    private static bool VariableExists(IReadOnlyDictionary<string, object?> variables, string path)
    {
        try
        {
            _ = GetVariableValue(variables, path.Trim().TrimStart('$', '{').TrimEnd('}'));
            return true;
        }
        catch (ScriptCommandException)
        {
            return false;
        }
    }

    private static string CatchVariableName(string command)
    {
        var tokens = ParseCommandLine(command);
        if (tokens.Count <= 1)
        {
            return "error";
        }

        if (tokens.Count == 2 && IsValidVariableName(tokens[1]))
        {
            return tokens[1];
        }

        throw new ScriptCommandException("invalid_catch", "catch syntax is: catch [errorName]");
    }

    private static bool Truthy(string value)
    {
        var text = value.Trim().ToLowerInvariant();
        return text is not ("" or "0" or "false" or "null");
    }

    private static bool CompareValues(string left, string op, string right)
    {
        var leftNumber = double.TryParse(left, out var parsedLeft);
        var rightNumber = double.TryParse(right, out var parsedRight);
        if (leftNumber && rightNumber)
        {
            return op.ToLowerInvariant() switch
            {
                "==" or "=" or "eq" => parsedLeft == parsedRight,
                "!=" or "<>" or "ne" => parsedLeft != parsedRight,
                ">" => parsedLeft > parsedRight,
                ">=" => parsedLeft >= parsedRight,
                "<" => parsedLeft < parsedRight,
                "<=" => parsedLeft <= parsedRight,
                _ => throw new ScriptCommandException("unknown_condition_operator", $"Unknown condition operator '{op}'.")
            };
        }

        return op.ToLowerInvariant() switch
        {
            "==" or "=" or "eq" => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            "!=" or "<>" or "ne" => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            "contains" => left.Contains(right, StringComparison.OrdinalIgnoreCase),
            "startswith" or "starts-with" => left.StartsWith(right, StringComparison.OrdinalIgnoreCase),
            "endswith" or "ends-with" => left.EndsWith(right, StringComparison.OrdinalIgnoreCase),
            _ => throw new ScriptCommandException("unknown_condition_operator", $"Unknown condition operator '{op}'.")
        };
    }
}

