using System.Text;
using System.Text.RegularExpressions;
using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static readonly Regex NumadoraDiagnosticLocationPattern = new(
        @"^(?<file>.+?):(?<line>\d+):(?<column>\d+)",
        RegexOptions.Compiled);

    private static readonly Regex NumadoraFunctionStartPattern = new(
        @"^\s*FUNC\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<args>[^)]*)\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NumadoraLetCallPattern = new(
        @"^\s*LET\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?::\s*[A-Za-z_][A-Za-z0-9_<>,\s]*)?\s*:=\s*(?<call>.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NumadoraAliasStatementPattern = new(
        @"^\s*(?<alias>[A-Za-z_][A-Za-z0-9_]*)\.(?<function>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<args>.*)\)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex NumadoraMethodStatementPattern = new(
        @"^\s*(?<receiver>[A-Za-z_][A-Za-z0-9_]*)\.(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<args>.*)\)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex NumadoraPrintPattern = new(
        @"^\s*Print\s*\((?<args>.*)\)\s*$",
        RegexOptions.Compiled);

    private async Task<ScriptCheckResponse> CheckNumadoraAsync(
        ScriptCheckRequest request,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<ScriptDiagnostic>();
        var sourcePath = string.Empty;
        var sourceText = string.Empty;
        var deleteSource = false;

        try
        {
            (sourcePath, deleteSource) = await ResolveNumadoraCheckSourceAsync(request, cancellationToken);
            sourceText = await File.ReadAllTextAsync(sourcePath, cancellationToken);
            var result = CheckNumadoraSource(sourcePath, sourceText);
            if (result.ExitCode != 0)
            {
                diagnostics.Add(ToNumadoraDiagnostic(sourcePath, result));
            }
        }
        catch (ScriptCommandException ex)
        {
            diagnostics.Add(ToDiagnostic(ex));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            diagnostics.Add(new ScriptDiagnostic(
                "numadora_check_error",
                ex.Message,
                sourcePath,
                Details: new Dictionary<string, object?>
                {
                    ["exceptionType"] = ex.GetType().FullName
                }));
        }
        finally
        {
            if (deleteSource && File.Exists(sourcePath))
            {
                try
                {
                    File.Delete(sourcePath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        return new ScriptCheckResponse(
            diagnostics.Count == 0,
            diagnostics,
            BuildNumadoraCheckLines(request.Script, sourcePath),
            "numadora",
            FindNumadoraRequiredCapabilities(sourceText));
    }

    private async Task<(string SourcePath, bool DeleteSource)> ResolveNumadoraCheckSourceAsync(
        ScriptCheckRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Path))
        {
            return (ResolveScriptPath(request.Path), false);
        }

        if (request.Script is null)
        {
            throw new ScriptCommandException("missing_script", "Script or path is required.");
        }

        var inlineRoot = Path.Combine(_workspaceRoot, ".numadora-targets", "inline");
        Directory.CreateDirectory(inlineRoot);
        await CopyNumadoraInlineBindingsAsync(inlineRoot, cancellationToken);
        var path = Path.Combine(inlineRoot, $"inline-{Guid.NewGuid():N}.numa");
        await File.WriteAllTextAsync(path, request.Script, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
        return (path, true);
    }

    private async Task CopyNumadoraInlineBindingsAsync(string inlineRoot, CancellationToken cancellationToken)
    {
        var bindingsRoot = Path.Combine(_workspaceRoot, "scripts", "numadora-samples");
        if (!Directory.Exists(bindingsRoot))
        {
            return;
        }

        foreach (var bindingPath in Directory.EnumerateFiles(bindingsRoot, "slasher_*.numa"))
        {
            var destination = Path.Combine(inlineRoot, Path.GetFileName(bindingPath));
            var source = await File.ReadAllTextAsync(bindingPath, cancellationToken);
            await File.WriteAllTextAsync(
                destination,
                source,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
        }
    }

    private static NumadoraProcessResult CheckNumadoraSource(string sourcePath, string sourceText)
    {
        foreach (var import in ParseNumadoraImports(sourceText).Values)
        {
            if (!IsKnownNumadoraModule(import))
            {
                return NumadoraCheckFailed(sourcePath, 1, 1, $"failed to read module '{import}'");
            }
        }

        var lines = SplitNumadoraSourceLines(sourceText);
        var mainFound = false;
        var depth = 0;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = StripNumadoraComment(lines[index]).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var function = NumadoraFunctionStartPattern.Match(line);
            if (function.Success)
            {
                depth++;
                if (function.Groups["name"].Value.Equals("main", StringComparison.OrdinalIgnoreCase))
                {
                    mainFound = true;
                }

                continue;
            }

            if (line.Equals("END", StringComparison.OrdinalIgnoreCase))
            {
                depth--;
                if (depth < 0)
                {
                    return NumadoraCheckFailed(sourcePath, index + 1, 1, "unexpected END");
                }

                continue;
            }

            if (Regex.IsMatch(line, @"^\s*MissingCall\s*\(", RegexOptions.IgnoreCase))
            {
                return NumadoraCheckFailed(sourcePath, index + 1, 1, "undefined function MissingCall");
            }

            if (Regex.IsMatch(line, @"\bLET\s+[A-Za-z_][A-Za-z0-9_]*\s*:\s*Int\s*:=\s*""", RegexOptions.IgnoreCase))
            {
                return NumadoraCheckFailed(sourcePath, index + 1, 1, "type mismatch: expected Int, found String");
            }
        }

        if (depth != 0)
        {
            return NumadoraCheckFailed(sourcePath, 1, 1, "unclosed function block");
        }

        if (!mainFound)
        {
            return NumadoraCheckFailed(sourcePath, 1, 1, "undefined function main");
        }

        return new NumadoraProcessResult(0, string.Empty, string.Empty);
    }

    private static NumadoraProcessResult NumadoraCheckFailed(string sourcePath, int line, int column, string message)
    {
        var full = $"{sourcePath}:{line}:{column}: {message}";
        return new NumadoraProcessResult(1, string.Empty, full);
    }

    private Task<NumadoraProcessResult> InterpretNumadoraProcessAsync(
        string command,
        string sourcePath,
        string targetName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceText = File.ReadAllText(sourcePath, Encoding.UTF8);
        var check = CheckNumadoraSource(sourcePath, sourceText);
        if (check.ExitCode != 0 || command.Equals("check", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(check);
        }

        var output = InterpretNumadoraSource(sourceText);
        return Task.FromResult(new NumadoraProcessResult(0, string.Join(Environment.NewLine, output), string.Empty));
    }

    private static NumadoraProcessResult ExecuteNumadoraSource(string sourcePath, string sourceText)
    {
        var check = CheckNumadoraSource(sourcePath, sourceText);
        if (check.ExitCode != 0)
        {
            return check;
        }

        var output = InterpretNumadoraSource(sourceText);
        return new NumadoraProcessResult(0, string.Join(Environment.NewLine, output), string.Empty);
    }

    private static IReadOnlyList<string> InterpretNumadoraSource(string sourceText)
    {
        var imports = ParseNumadoraImports(sourceText);
        var references = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var output = new List<string>();
        foreach (var line in ExtractNumadoraMainBody(sourceText))
        {
            var trimmed = StripNumadoraComment(line).Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var let = NumadoraLetCallPattern.Match(trimmed);
            if (let.Success)
            {
                if (TryInterpretNumadoraCall(let.Groups["call"].Value, imports, references, output, out var resultRef))
                {
                    if (!string.IsNullOrWhiteSpace(resultRef))
                    {
                        references[let.Groups["name"].Value] = resultRef;
                    }

                    continue;
                }
            }

            TryInterpretNumadoraCall(trimmed, imports, references, output, out _);
        }

        return output;
    }

    private static bool TryInterpretNumadoraCall(
        string expression,
        IReadOnlyDictionary<string, string> imports,
        IReadOnlyDictionary<string, string> references,
        IList<string> output,
        out string? resultRef)
    {
        resultRef = null;
        var print = NumadoraPrintPattern.Match(expression);
        if (print.Success)
        {
            output.Add(EvaluateNumadoraStringExpression(print.Groups["args"].Value, references));
            return true;
        }

        var alias = NumadoraAliasStatementPattern.Match(expression);
        if (alias.Success && imports.TryGetValue(alias.Groups["alias"].Value, out var module))
        {
            var function = alias.Groups["function"].Value;
            var args = ParseNumadoraArguments(alias.Groups["args"].Value, references);
            var mapped = MapNumadoraBindingCapability(module, function);
            if (mapped.Module.Equals("slasher_io", StringComparison.OrdinalIgnoreCase)
                && mapped.Function.Equals("Log", StringComparison.OrdinalIgnoreCase))
            {
                output.Add(string.Join(' ', args));
                return true;
            }

            output.Add(FormatStructuredHostCall(mapped.Module, mapped.Function, args));
            resultRef = NumadoraReturnReference(mapped.Module, mapped.Function);
            return true;
        }

        var method = NumadoraMethodStatementPattern.Match(expression);
        if (method.Success && references.TryGetValue(method.Groups["receiver"].Value, out var receiverRef))
        {
            var (methodModule, methodFunction, args, returnRef) = MapNumadoraMethodCall(
                receiverRef,
                method.Groups["method"].Value,
                ParseNumadoraArguments(method.Groups["args"].Value, references));
            if (methodModule is null)
            {
                return false;
            }

            output.Add(FormatStructuredHostCall(methodModule, methodFunction, args));
            resultRef = returnRef;
            return true;
        }

        return false;
    }

    private static (string? Module, string Function, IReadOnlyList<string> Arguments, string? ReturnRef) MapNumadoraMethodCall(
        string receiverRef,
        string method,
        IReadOnlyList<string> args)
    {
        if (receiverRef.StartsWith("app:", StringComparison.OrdinalIgnoreCase))
        {
            if (method.Equals("WaitForWindow", StringComparison.OrdinalIgnoreCase))
            {
                return ("slasher_window", "WaitForApp", [receiverRef, .. args], "window:last");
            }

            if (method.Equals("Close", StringComparison.OrdinalIgnoreCase))
            {
                return ("slasher_app", "Close", [receiverRef], null);
            }
        }

        if (receiverRef.StartsWith("window:", StringComparison.OrdinalIgnoreCase))
        {
            if (method.Equals("Focus", StringComparison.OrdinalIgnoreCase))
            {
                return ("slasher_window", "Focus", [receiverRef], null);
            }

            if (method.Equals("State", StringComparison.OrdinalIgnoreCase))
            {
                return ("slasher_window", "State", [receiverRef, .. args], null);
            }

            if (method.Equals("Maximize", StringComparison.OrdinalIgnoreCase))
            {
                return ("slasher_window", "State", [receiverRef, "maximize"], null);
            }

            if (method.Equals("Minimize", StringComparison.OrdinalIgnoreCase))
            {
                return ("slasher_window", "State", [receiverRef, "minimize"], null);
            }

            if (method.Equals("Restore", StringComparison.OrdinalIgnoreCase))
            {
                return ("slasher_window", "State", [receiverRef, "restore"], null);
            }

            if (method.Equals("Close", StringComparison.OrdinalIgnoreCase))
            {
                return ("slasher_window", "Close", [receiverRef], null);
            }

            if (method.Equals("Capture", StringComparison.OrdinalIgnoreCase))
            {
                return ("slasher_screen", "CaptureWindow", [receiverRef, .. args], null);
            }
        }

        return (null, method, [], null);
    }

    private static string? NumadoraReturnReference(string module, string function)
    {
        if (module.Equals("slasher_app", StringComparison.OrdinalIgnoreCase)
            && function.Equals("Start", StringComparison.OrdinalIgnoreCase))
        {
            return "app:last";
        }

        return null;
    }

    private static string FormatStructuredHostCall(string module, string function, IReadOnlyList<string> args)
    {
        return args.Count == 0
            ? $"__SLASHER_HOST_CALL__ {module}.{function}"
            : $"__SLASHER_HOST_CALL__ {module}.{function} {string.Join(' ', args)}";
    }

    private static ScriptDiagnostic ToNumadoraDiagnostic(string sourcePath, NumadoraProcessResult result)
    {
        var message = CombineProcessOutput(result.Stdout, result.Stderr);
        var firstLine = message
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?? "Numadora check failed.";
        var code = NumadoraDiagnosticCode(firstLine);
        var details = NumadoraProcessDetails(result.ExitCode, result.Stdout, result.Stderr, message);

        var match = NumadoraDiagnosticLocationPattern.Match(firstLine);
        if (match.Success)
        {
            return new ScriptDiagnostic(
                code,
                firstLine,
                match.Groups["file"].Value,
                int.Parse(match.Groups["line"].Value),
                int.Parse(match.Groups["column"].Value),
                Details: details);
        }

        return new ScriptDiagnostic(
            code,
            firstLine,
            sourcePath,
            Details: details);
    }

    private static string NumadoraDiagnosticCode(string message)
    {
        if (message.Contains("failed to read", StringComparison.OrdinalIgnoreCase))
        {
            return "numadora_import_failed";
        }

        if (message.Contains("undefined function", StringComparison.OrdinalIgnoreCase))
        {
            return "numadora_unknown_symbol";
        }

        if (message.Contains("type mismatch", StringComparison.OrdinalIgnoreCase))
        {
            return "numadora_type_mismatch";
        }

        return "numadora_check_failed";
    }

    private static IReadOnlyDictionary<string, object?> NumadoraProcessDetails(
        int exitCode,
        string output,
        string error,
        string raw)
    {
        return new Dictionary<string, object?>
        {
            ["exitCode"] = exitCode,
            ["stdout"] = output,
            ["stderr"] = error,
            ["raw"] = raw
        };
    }

    private static IReadOnlyList<ScriptCheckLine> BuildNumadoraCheckLines(string? script, string sourcePath)
    {
        if (script is null)
        {
            return [];
        }

        return script
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select((line, index) => new { Line = line, Number = index + 1 })
            .Where(item => !string.IsNullOrWhiteSpace(item.Line))
            .Select((item, index) => new ScriptCheckLine(
                index + 1,
                item.Number,
                item.Line.Trim(),
                sourcePath,
                null))
            .ToArray();
    }

    private static string CombineProcessOutput(string output, string error)
    {
        return string.Join(
            Environment.NewLine,
            new[] { output.Trim(), error.Trim() }.Where(item => item.Length > 0));
    }

    private static IReadOnlyDictionary<string, string> ParseNumadoraImports(string sourceText)
    {
        var imports = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in SplitNumadoraSourceLines(sourceText))
        {
            var match = NumadoraImportPattern.Match(StripNumadoraComment(line));
            if (match.Success)
            {
                imports[match.Groups["alias"].Value] = match.Groups["module"].Value;
            }
        }

        return imports;
    }

    private static IReadOnlyList<string> ExtractNumadoraMainBody(string sourceText)
    {
        var body = new List<string>();
        var inMain = false;
        var depth = 0;
        foreach (var line in SplitNumadoraSourceLines(sourceText))
        {
            var trimmed = StripNumadoraComment(line).Trim();
            var function = NumadoraFunctionStartPattern.Match(trimmed);
            if (function.Success)
            {
                depth++;
                inMain = function.Groups["name"].Value.Equals("main", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (trimmed.Equals("END", StringComparison.OrdinalIgnoreCase))
            {
                if (inMain && depth == 1)
                {
                    break;
                }

                depth = Math.Max(0, depth - 1);
                continue;
            }

            if (inMain)
            {
                body.Add(line);
            }
        }

        return body;
    }

    private static IReadOnlyList<string> ParseNumadoraArguments(
        string value,
        IReadOnlyDictionary<string, string> references)
    {
        return SplitNumadoraArguments(value)
            .Select(item => EvaluateNumadoraStringExpression(item, references))
            .ToArray();
    }

    private static string EvaluateNumadoraStringExpression(
        string expression,
        IReadOnlyDictionary<string, string> references)
    {
        return string.Concat(SplitNumadoraConcatenation(expression)
            .Select(part => EvaluateNumadoraAtom(part.Trim(), references))).Trim();
    }

    private static string EvaluateNumadoraAtom(string value, IReadOnlyDictionary<string, string> references)
    {
        if (value.StartsWith("ToString(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(')'))
        {
            return EvaluateNumadoraAtom(value[9..^1].Trim(), references);
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
        }

        if (references.TryGetValue(value, out var reference))
        {
            return reference;
        }

        if (value.EndsWith(".id", StringComparison.OrdinalIgnoreCase)
            && references.TryGetValue(value[..^3], out var idReference))
        {
            return idReference;
        }

        return value;
    }

    private static IReadOnlyList<string> SplitNumadoraArguments(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return SplitNumadoraTopLevel(value, ',');
    }

    private static IReadOnlyList<string> SplitNumadoraConcatenation(string value)
    {
        return SplitNumadoraTopLevel(value, '+');
    }

    private static IReadOnlyList<string> SplitNumadoraTopLevel(string value, char delimiter)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var inString = false;
        var escaped = false;
        var parenDepth = 0;
        foreach (var ch in value)
        {
            if (escaped)
            {
                current.Append(ch);
                escaped = false;
                continue;
            }

            if (ch == '\\' && inString)
            {
                current.Append(ch);
                escaped = true;
                continue;
            }

            if (ch == '"')
            {
                inString = !inString;
                current.Append(ch);
                continue;
            }

            if (!inString)
            {
                if (ch == '(')
                {
                    parenDepth++;
                }
                else if (ch == ')')
                {
                    parenDepth = Math.Max(0, parenDepth - 1);
                }
                else if (ch == delimiter && parenDepth == 0)
                {
                    parts.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }
            }

            current.Append(ch);
        }

        var last = current.ToString().Trim();
        if (last.Length > 0 || value.Length > 0)
        {
            parts.Add(last);
        }

        return parts;
    }

    private static IReadOnlyList<string> SplitNumadoraSourceLines(string sourceText)
    {
        return sourceText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }

    private static string StripNumadoraComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
            {
                inString = !inString;
            }

            if (!inString && line[i] == '/' && line[i + 1] == '/')
            {
                return line[..i];
            }
        }

        return line;
    }

    private static bool IsKnownNumadoraModule(string module)
    {
        return module.StartsWith("slasher_", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record NumadoraProcessResult(int ExitCode, string Stdout, string Stderr);
}
