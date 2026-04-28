using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    public async Task<ScriptCheckResponse> CheckAsync(ScriptCheckRequest request, CancellationToken cancellationToken)
    {
        var diagnostics = new List<ScriptDiagnostic>();
        IReadOnlyList<ScriptLine> lines = [];

        try
        {
            lines = await ParseCheckLinesAsync(request, cancellationToken);
            diagnostics.AddRange(ValidateScriptStructure(lines));
        }
        catch (ScriptCommandException ex)
        {
            diagnostics.Add(ToDiagnostic(ex));
        }

        return new ScriptCheckResponse(
            diagnostics.Count == 0,
            diagnostics,
            lines.Select(ToCheckLine).ToArray());
    }

    private async Task<IReadOnlyList<ScriptLine>> ParseCheckLinesAsync(
        ScriptCheckRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Path))
        {
            var scriptPath = ResolveScriptPath(request.Path);
            return await ParseScriptFileAsync(scriptPath, cancellationToken);
        }

        if (request.Script is null)
        {
            throw new ScriptCommandException("missing_script", "Script or path is required.");
        }

        return ParseScript(
            request.Script,
            "inline-script",
            _workspaceRoot,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            [],
            inheritedFunction: null,
            depth: 0);
    }

    private static IReadOnlyList<ScriptDiagnostic> ValidateScriptStructure(IReadOnlyList<ScriptLine> lines)
    {
        var diagnostics = new List<ScriptDiagnostic>();
        var stack = new Stack<ScriptLine>();

        foreach (var line in lines)
        {
            var word = FirstWord(line.Command);
            if (IsStructureOpener(word))
            {
                stack.Push(line);
                continue;
            }

            if (word is "else")
            {
                ValidateMiddle(line, stack, "if", diagnostics);
                continue;
            }

            if (word is "catch" or "finally")
            {
                ValidateMiddle(line, stack, "try", diagnostics);
                continue;
            }

            if (!TryGetStructureCloser(word, out var opener))
            {
                continue;
            }

            if (stack.Count == 0)
            {
                diagnostics.Add(ToDiagnostic(
                    "unexpected_block_close",
                    $"{word} does not have a matching opening command.",
                    line));
                continue;
            }

            var openLine = stack.Pop();
            var openWord = FirstWord(openLine.Command);
            if (!openWord.Equals(opener, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(ToDiagnostic(
                    "mismatched_block_close",
                    $"{word} closes {opener}, but the current open block is {openWord}.",
                    line));
            }
        }

        while (stack.Count > 0)
        {
            var line = stack.Pop();
            var word = FirstWord(line.Command);
            diagnostics.Add(ToDiagnostic(
                "block_not_closed",
                $"{word} block is missing its closing command.",
                line));
        }

        return diagnostics;
    }

    private static void ValidateMiddle(
        ScriptLine line,
        Stack<ScriptLine> stack,
        string requiredOpener,
        ICollection<ScriptDiagnostic> diagnostics)
    {
        if (stack.Count == 0)
        {
            diagnostics.Add(ToDiagnostic(
                "unexpected_block_middle",
                $"{FirstWord(line.Command)} does not have a matching {requiredOpener} block.",
                line));
            return;
        }

        var openWord = FirstWord(stack.Peek().Command);
        if (!openWord.Equals(requiredOpener, StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(ToDiagnostic(
                "mismatched_block_middle",
                $"{FirstWord(line.Command)} belongs to {requiredOpener}, but the current open block is {openWord}.",
                line));
        }
    }

    private static bool IsStructureOpener(string word)
    {
        return word is "function" or "if" or "repeat" or "foreach" or "while" or "try";
    }

    private static bool TryGetStructureCloser(string word, out string opener)
    {
        opener = word switch
        {
            "endfunction" => "function",
            "endif" => "if",
            "endrepeat" => "repeat",
            "endforeach" => "foreach",
            "endwhile" => "while",
            "endtry" => "try",
            _ => string.Empty
        };

        return opener.Length > 0;
    }

    private static ScriptCheckLine ToCheckLine(ScriptLine line)
    {
        return new ScriptCheckLine(
            line.Sequence,
            line.Line,
            line.Command,
            line.SourceFile,
            line.Function);
    }

    private static ScriptDiagnostic ToDiagnostic(ScriptCommandException exception)
    {
        return new ScriptDiagnostic(exception.Code, exception.Message);
    }

    private static ScriptDiagnostic ToDiagnostic(string code, string message, ScriptLine line)
    {
        return new ScriptDiagnostic(
            code,
            message,
            line.SourceFile,
            line.Line,
            1,
            line.Command,
            line.Function,
            line.Stack.Count == 0 ? null : line.Stack);
    }
}
