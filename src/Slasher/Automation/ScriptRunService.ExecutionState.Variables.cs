using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private sealed partial class ScriptExecutionState
    {
        public Dictionary<string, object?> ResolveVariables(ScriptLine line)
        {
            var resolved = new Dictionary<string, object?>(Variables, StringComparer.OrdinalIgnoreCase);
            foreach (var item in GetFileVariables(line, create: false))
            {
                resolved[item.Key] = item.Value;
            }

            foreach (var item in GetLocalVariables(line, create: false))
            {
                resolved[item.Key] = item.Value;
            }

            return resolved;
        }

        public IReadOnlyDictionary<string, object?> SnapshotVariables(ScriptLine line)
        {
            return new Dictionary<string, object?>
            {
                ["global"] = Variables.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase),
                ["file"] = GetFileVariables(line, create: false).ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase),
                ["local"] = GetLocalVariables(line, create: false).ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase),
                ["resolved"] = ResolveVariables(line)
            };
        }

        public object AssignVariable(ScriptLine line, string name, object? value, ScriptVariableScope? scope)
        {
            ScriptRunService.AssignVariable(GetWriteScope(line, name, scope), name, value);
            return value ?? string.Empty;
        }

        public bool RemoveVariable(ScriptLine line, string name, ScriptVariableScope? scope)
        {
            if (!IsValidVariableName(name))
            {
                throw new ScriptCommandException("invalid_variable_name", "Variable names must start with a letter or underscore and contain only letters, digits, and underscores.");
            }

            if (scope is not null)
            {
                return GetScopedVariables(line, scope.Value, create: false).Remove(name);
            }

            return GetLocalVariables(line, create: false).Remove(name)
                || GetFileVariables(line, create: false).Remove(name)
                || Variables.Remove(name);
        }

        public bool TryGetVariable(ScriptLine line, string name, ScriptVariableScope? scope, out object? value)
        {
            if (scope is not null)
            {
                return GetScopedVariables(line, scope.Value, create: false).TryGetValue(name, out value);
            }

            if (GetLocalVariables(line, create: false).TryGetValue(name, out value))
            {
                return true;
            }

            if (GetFileVariables(line, create: false).TryGetValue(name, out value))
            {
                return true;
            }

            return Variables.TryGetValue(name, out value);
        }

        public List<object?> GetOrCreateArray(ScriptLine line, string name, ScriptVariableScope? scope)
        {
            var variables = GetWriteScope(line, name, scope);
            if (!variables.TryGetValue(name, out var value))
            {
                var created = new List<object?>();
                ScriptRunService.AssignVariable(variables, name, created);
                return created;
            }

            return ToMutableArray(name, value);
        }

        public List<object?> RequireArray(ScriptLine line, string name, ScriptVariableScope? scope)
        {
            if (!TryGetVariable(line, name, scope, out var value))
            {
                throw new ScriptCommandException("variable_not_found", $"Variable '{name}' was not found.");
            }

            return ToMutableArray(name, value);
        }

        private Dictionary<string, object?> GetWriteScope(ScriptLine line, string name, ScriptVariableScope? scope)
        {
            if (scope is not null)
            {
                return GetScopedVariables(line, scope.Value, create: true);
            }

            if (GetLocalVariables(line, create: false).ContainsKey(name))
            {
                return GetLocalVariables(line, create: true);
            }

            if (GetFileVariables(line, create: false).ContainsKey(name))
            {
                return GetFileVariables(line, create: true);
            }

            return Variables;
        }

        private Dictionary<string, object?> GetScopedVariables(ScriptLine line, ScriptVariableScope scope, bool create)
        {
            return scope switch
            {
                ScriptVariableScope.Global => Variables,
                ScriptVariableScope.File => GetFileVariables(line, create),
                ScriptVariableScope.Local => GetLocalVariables(line, create),
                _ => Variables
            };
        }

        private Dictionary<string, object?> GetFileVariables(ScriptLine line, bool create)
        {
            var key = string.IsNullOrWhiteSpace(line.SourceFile) ? "inline-script" : line.SourceFile;
            if (!FileVariables.TryGetValue(key, out var variables) && create)
            {
                variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                FileVariables[key] = variables;
            }

            return variables ?? [];
        }

        private Dictionary<string, object?> GetLocalVariables(ScriptLine line, bool create)
        {
            var key = CurrentLocalKey(line);
            if (!LocalVariables.TryGetValue(key, out var variables) && create)
            {
                variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                LocalVariables[key] = variables;
            }

            return variables ?? [];
        }

        private string CurrentLocalKey(ScriptLine line)
        {
            if (CallStack.Count > 0)
            {
                return CallStack[^1].LocalKey;
            }

            return $"{(string.IsNullOrWhiteSpace(line.SourceFile) ? "inline-script" : line.SourceFile)}::{(string.IsNullOrWhiteSpace(line.Function) ? "__entry" : line.Function)}";
        }
    }
}

