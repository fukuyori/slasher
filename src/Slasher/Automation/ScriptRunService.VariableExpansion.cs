using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static string ExpandVariables(string input, IReadOnlyDictionary<string, object?> variables)
    {
        var output = input;
        var start = output.IndexOf("${", StringComparison.Ordinal);
        while (start >= 0)
        {
            var end = output.IndexOf('}', start + 2);
            if (end < 0)
            {
                throw new ScriptCommandException("unclosed_variable", "Variable expression is missing a closing brace.");
            }

            var path = output[(start + 2)..end];
            var value = FormatVariableValue(GetVariableValue(variables, path));
            output = output[..start] + value + output[(end + 1)..];
            start = output.IndexOf("${", start + value.Length, StringComparison.Ordinal);
        }

        return output;
    }

    private static object? GetVariableValue(IReadOnlyDictionary<string, object?> variables, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ScriptCommandException("invalid_variable", "Variable expression is empty.");
        }

        var parts = path.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !variables.TryGetValue(parts[0], out var value))
        {
            throw new ScriptCommandException("variable_not_found", $"Variable '{parts.FirstOrDefault() ?? path}' was not found.");
        }

        foreach (var part in parts.Skip(1))
        {
            value = ReadMember(value, part, path);
        }

        return value;
    }

    private static object? ReadMember(object? value, string member, string fullPath)
    {
        if (value is null)
        {
            throw new ScriptCommandException("variable_member_not_found", $"Variable '{fullPath}' is null.");
        }

        if (value is IReadOnlyDictionary<string, object?> dictionary)
        {
            return dictionary.TryGetValue(member, out var dictionaryValue)
                ? dictionaryValue
                : throw new ScriptCommandException("variable_member_not_found", $"Member '{member}' was not found in variable '{fullPath}'.");
        }

        if (value is IReadOnlyList<object?> list)
        {
            if (member.Equals("length", StringComparison.OrdinalIgnoreCase)
                || member.Equals("count", StringComparison.OrdinalIgnoreCase))
            {
                return list.Count;
            }

            if (int.TryParse(member, out var index) && index >= 0 && index < list.Count)
            {
                return list[index];
            }

            throw new ScriptCommandException("variable_member_not_found", $"Member '{member}' was not found in variable '{fullPath}'.");
        }

        var property = value.GetType()
            .GetProperties()
            .FirstOrDefault(item => item.Name.Equals(member, StringComparison.OrdinalIgnoreCase));
        if (property is null)
        {
            throw new ScriptCommandException("variable_member_not_found", $"Member '{member}' was not found in variable '{fullPath}'.");
        }

        return property.GetValue(value);
    }

    private static string FormatVariableValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            string text => text,
            DateTimeOffset date => date.ToString("O"),
            _ => value.ToString() ?? string.Empty
        };
    }
}

