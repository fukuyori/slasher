using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private static readonly Regex NumadoraDiagnosticLocationPattern = new(
        @"^(?<file>.+?):(?<line>\d+):(?<column>\d+)",
        RegexOptions.Compiled);

    private static readonly Regex NumadoraProcessExitLinePattern = new(
        @"^error:\s+process didn't exit successfully:",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
            var numadoraHome = ResolveNumadoraHome();
            if (numadoraHome is null)
            {
                diagnostics.Add(new ScriptDiagnostic(
                    "numadora_not_found",
                    "Numadora home was not found. Set NUMADORA_HOME or place Numadora at D:\\home\\source\\rust\\Numadora.",
                    sourcePath));
            }
            else
            {
                var result = await RunNumadoraProcessAsync(numadoraHome, "check", sourcePath, "check", cancellationToken);
                if (result.ExitCode != 0)
                {
                    diagnostics.Add(ToNumadoraDiagnostic(sourcePath, result));
                }
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Win32Exception)
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

    private static string? ResolveNumadoraHome()
    {
        var configured = Environment.GetEnvironmentVariable("NUMADORA_HOME");
        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return configured;
        }

        const string defaultPath = @"D:\home\source\rust\Numadora";
        return Directory.Exists(defaultPath) ? defaultPath : null;
    }

    private async Task<NumadoraProcessResult> RunNumadoraProcessAsync(
        string numadoraHome,
        string command,
        string sourcePath,
        string targetName,
        CancellationToken cancellationToken)
    {
        var targetDir = Path.Combine(_workspaceRoot, ".numadora-targets", targetName);
        Directory.CreateDirectory(targetDir);

        var startInfo = new ProcessStartInfo
        {
            FileName = "cargo",
            WorkingDirectory = numadoraHome,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--quiet");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add(command);
        startInfo.ArgumentList.Add(sourcePath);
        startInfo.Environment["CARGO_TARGET_DIR"] = targetDir;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start Numadora check process.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            TryKillProcess(process);
            throw;
        }

        var output = await outputTask;
        var error = await errorTask;
        return new NumadoraProcessResult(process.ExitCode, output, error);
    }

    private static ScriptDiagnostic ToNumadoraDiagnostic(string sourcePath, NumadoraProcessResult result)
    {
        var message = CombineProcessOutput(result.Stdout, result.Stderr);
        var firstLine = message
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => !NumadoraProcessExitLinePattern.IsMatch(line.Trim()))
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

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    private sealed record NumadoraProcessResult(int ExitCode, string Stdout, string Stderr);
}
