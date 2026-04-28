using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<IReadOnlyList<ScriptLine>> ParseScriptFileAsync(string fullPath, CancellationToken cancellationToken)
    {
        return ParseScript(
            await File.ReadAllTextAsync(fullPath, cancellationToken),
            Path.GetRelativePath(_workspaceRoot, fullPath),
            Path.GetDirectoryName(fullPath) ?? _workspaceRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            [],
            inheritedFunction: null,
            depth: 0);
    }

    private IReadOnlyList<ScriptLine> ParseScript(
        string script,
        string sourceFile,
        string baseDirectory,
        HashSet<string> includeStack,
        IReadOnlyList<AutomationSourceFrame> sourceStack,
        string? inheritedFunction,
        int depth)
    {
        if (depth > MaxIncludeDepth)
        {
            throw new ScriptCommandException("include_depth_exceeded", $"Include depth exceeded {MaxIncludeDepth}.");
        }

        var lines = new List<ScriptLine>();
        var currentFunction = inheritedFunction;
        var functionStack = new Stack<string>();
        foreach (var item in script
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select((text, index) => new { Text = text.Trim(), Line = index + 1 }))
        {
            if (string.IsNullOrWhiteSpace(item.Text) || item.Text.StartsWith('#'))
            {
                continue;
            }

            var tokens = ParseCommandLine(item.Text);
            var word = tokens.FirstOrDefault()?.ToLowerInvariant();
            if (word is "include" or "import")
            {
                if (tokens.Count != 2)
                {
                    throw new ScriptCommandException("invalid_include", "include syntax is: include <path>");
                }

                var includePath = ResolveIncludePath(tokens[1], baseDirectory);
                var normalized = Path.GetFullPath(includePath);
                if (!includeStack.Add(normalized))
                {
                    throw new ScriptCommandException("include_cycle", $"Include cycle detected for '{tokens[1]}'.");
                }

                var includedSourceFile = Path.GetRelativePath(_workspaceRoot, includePath);
                var nextStack = sourceStack
                    .Concat([
                        new AutomationSourceFrame(sourceFile, item.Line, 1, currentFunction, item.Text)
                    ])
                    .ToArray();
                lines.AddRange(ParseScript(
                    File.ReadAllText(includePath),
                    includedSourceFile,
                    Path.GetDirectoryName(includePath) ?? _workspaceRoot,
                    includeStack,
                    nextStack,
                    currentFunction,
                    depth + 1));
                includeStack.Remove(normalized);
                continue;
            }

            if (word == "function")
            {
                if (tokens.Count < 2 || !IsValidVariableName(tokens[1]))
                {
                    throw new ScriptCommandException("invalid_function", "function syntax is: function <name> [params...]");
                }

                functionStack.Push(currentFunction ?? string.Empty);
                currentFunction = tokens[1];
                lines.Add(new ScriptLine(lines.Count + 1, item.Line, item.Text, sourceFile, currentFunction, sourceStack));
                continue;
            }

            var lineFunction = TryGetStepName(tokens) ?? currentFunction;
            lines.Add(new ScriptLine(lines.Count + 1, item.Line, item.Text, sourceFile, lineFunction, sourceStack));
            if (word == "endfunction")
            {
                currentFunction = functionStack.Count == 0
                    ? inheritedFunction
                    : EmptyToNull(functionStack.Pop());
            }
            else
            {
                currentFunction = lineFunction;
            }
        }

        return lines;
    }
}

