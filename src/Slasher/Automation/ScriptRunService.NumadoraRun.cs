using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<ScriptRunResponse> RunNumadoraPreflightAsync(
        ScriptCheckRequest checkRequest,
        string name,
        string entryPoint,
        bool stopOnError,
        CapturePolicy? capturePolicy,
        string? purpose,
        bool allowInteractiveInput,
        CancellationToken cancellationToken)
    {
        var normalizedPurpose = NormalizeNumadoraPurpose(purpose);
        var lineage = await BuildNumadoraLineageAsync(checkRequest, entryPoint, normalizedPurpose, cancellationToken);
        var report = _artifacts.StartRun(
            name,
            AutomationRunMode.Script,
            entryPoint,
            capturePolicy,
            new Dictionary<string, object?>
            {
                ["purpose"] = normalizedPurpose,
                ["lineage"] = lineage,
                ["approvals"] = new Dictionary<string, object?>
                {
                    ["interactiveInput"] = allowInteractiveInput
                }
            });
        var state = new ScriptExecutionState(report);
        var check = await CheckNumadoraAsync(checkRequest, cancellationToken);
        var sourceFile = checkRequest.Path ?? "inline-script";
        var line = new ScriptLine(1, 1, "numadora run preflight", sourceFile, null, []);

        if (!check.Ok)
        {
            var checkException = new ScriptCommandException(
                "numadora_check_failed",
                "Numadora check failed before run.",
                NumadoraRunDetails(check),
                Recoverable: true);
            await RecordScriptErrorAsync(line, state, checkException, stopOnError, cancellationToken);
            state.Report = _artifacts.CompleteRun(state.Report, AutomationRunStatus.Failed, state.FinalError, null);
            return new ScriptRunResponse(false, state.Report, state.Events, state.FinalError);
        }

        if (CanRunNumadoraLocally(check))
        {
            return await RunCheckedNumadoraLocallyAsync(
                checkRequest,
                check,
                state,
                cancellationToken);
        }

        var trace = await TraceNumadoraHostCallsAsync(checkRequest, check, cancellationToken);
        var exception = new ScriptCommandException(
            "numadora_run_not_implemented",
            "Numadora host-call run integration is not implemented yet for this capability set. Pure Numadora and policy-allowed local Slasher bindings can run.",
            NumadoraRunDetails(check, trace, state.Report, normalizedPurpose),
            Recoverable: false);
        await RecordScriptErrorAsync(line, state, exception, stopOnError, cancellationToken);
        state.Report = _artifacts.CompleteRun(state.Report, AutomationRunStatus.Failed, state.FinalError, null);
        return new ScriptRunResponse(false, state.Report, state.Events, state.FinalError);
    }

    private async Task<NumadoraHostCallTrace> TraceNumadoraHostCallsAsync(
        ScriptCheckRequest checkRequest,
        ScriptCheckResponse check,
        CancellationToken cancellationToken)
    {
        var (sourcePath, deleteSource) = await ResolveNumadoraCheckSourceAsync(checkRequest, cancellationToken);
        try
        {
            var numadoraHome = ResolveNumadoraHome();
            if (numadoraHome is null)
            {
                return new NumadoraHostCallTrace(
                    [],
                    null,
                    null,
                    "Numadora home was not found. Set NUMADORA_HOME or place Numadora at D:\\home\\source\\rust\\Numadora.",
                    null);
            }

            var result = await RunNumadoraProcessAsync(numadoraHome, "run", sourcePath, "host-call-trace", cancellationToken);
            var raw = CombineProcessOutput(result.Stdout, result.Stderr);
            var diagnostic = result.ExitCode == 0 ? null : ToNumadoraDiagnostic(sourcePath, result);
            return new NumadoraHostCallTrace(
                ParseNumadoraHostCalls(result.Stdout),
                result.ExitCode,
                raw,
                null,
                diagnostic);
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
    }

    private async Task<ScriptRunResponse> RunCheckedNumadoraLocallyAsync(
        ScriptCheckRequest checkRequest,
        ScriptCheckResponse check,
        ScriptExecutionState state,
        CancellationToken cancellationToken)
    {
        var (sourcePath, deleteSource) = await ResolveNumadoraCheckSourceAsync(checkRequest, cancellationToken);
        try
        {
            var numadoraHome = ResolveNumadoraHome();
            if (numadoraHome is null)
            {
                throw new ScriptCommandException(
                    "numadora_not_found",
                    "Numadora home was not found. Set NUMADORA_HOME or place Numadora at D:\\home\\source\\rust\\Numadora.",
                    NumadoraRunDetails(check),
                    Recoverable: false);
            }

            var result = await RunNumadoraProcessAsync(numadoraHome, "run", sourcePath, "run", cancellationToken);
            await AppendNumadoraProcessEventAsync(
                state,
                sourcePath,
                result,
                check,
                normalizedPurpose: NumadoraPurposeFromReport(state.Report),
                cancellationToken);
            var status = result.ExitCode == 0 && state.FinalError is null
                ? AutomationRunStatus.Passed
                : AutomationRunStatus.Failed;
            state.Report = _artifacts.CompleteRun(state.Report, status, state.FinalError, null);
            return new ScriptRunResponse(status == AutomationRunStatus.Passed, state.Report, state.Events, state.FinalError);
        }
        catch (ScriptCommandException ex)
        {
            await RecordScriptErrorAsync(
                new ScriptLine(1, 1, "numadora run", checkRequest.Path ?? "inline-script", null, []),
                state,
                ex,
                stopOnError: true,
                cancellationToken);
            state.Report = _artifacts.CompleteRun(state.Report, AutomationRunStatus.Failed, state.FinalError, null);
            return new ScriptRunResponse(false, state.Report, state.Events, state.FinalError);
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
    }

    private async Task AppendNumadoraProcessEventAsync(
        ScriptExecutionState state,
        string sourcePath,
        NumadoraProcessResult result,
        ScriptCheckResponse check,
        string normalizedPurpose,
        CancellationToken cancellationToken)
    {
        var sequence = state.NextSequence++;
        var startedAt = DateTimeOffset.UtcNow;
        var endedAt = DateTimeOffset.UtcNow;
        var raw = CombineProcessOutput(result.Stdout, result.Stderr);
        var hostCalls = ParseNumadoraHostCalls(result.Stdout);
        var logs = BuildNumadoraLogs(result);
        AutomationError? error = null;
        if (result.ExitCode != 0)
        {
            var diagnostic = ToNumadoraDiagnostic(sourcePath, result);
            error = new AutomationError(
                diagnostic.Code,
                diagnostic.Message,
                "numadora.run",
                new AutomationSource(sourcePath, diagnostic.Line, diagnostic.Column, "numadora run"),
                Recoverable: false,
                Details: new Dictionary<string, object?>
                {
                    ["diagnostics"] = new[] { diagnostic },
                    ["requiredCapabilities"] = check.RequiredCapabilities ?? [],
                    ["hostCalls"] = hostCalls,
                    ["stdout"] = result.Stdout,
                    ["stderr"] = result.Stderr,
                    ["raw"] = raw,
                    ["exitCode"] = result.ExitCode
                });
        }

        var automationEvent = _artifacts.CreateEvent(
            state.Report,
            sequence,
            "numadora.run",
            startedAt,
            endedAt,
            result.ExitCode == 0,
            "numadora run",
            new AutomationSource(sourcePath, 1, 1, "numadora run"),
            parameters: new Dictionary<string, object?>
            {
                ["language"] = "numadora",
                ["requiredCapabilities"] = check.RequiredCapabilities ?? [],
                ["hostCalls"] = hostCalls
            },
            result: new
            {
                result.ExitCode,
                OutputLineCount = logs.Count,
                HostCallCount = hostCalls.Count
            },
            logs: logs,
            error: error);

        state.Report = _artifacts.AppendEvent(state.Report, automationEvent);
        state.Events.Add(automationEvent);
        state.FinalError = error;

        if (result.ExitCode == 0)
        {
            await AppendNumadoraHostCallEventsAsync(state, sourcePath, hostCalls, check, normalizedPurpose, cancellationToken);
        }
    }

    private async Task AppendNumadoraHostCallEventsAsync(
        ScriptExecutionState state,
        string sourcePath,
        IReadOnlyList<NumadoraHostCall> hostCalls,
        ScriptCheckResponse check,
        string normalizedPurpose,
        CancellationToken cancellationToken)
    {
        foreach (var hostCall in hostCalls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sequence = state.NextSequence++;
            var startedAt = DateTimeOffset.UtcNow;
            var formatted = FormatNumadoraHostCall(hostCall);
            var policyTarget = GetNumadoraPolicyTarget(hostCall);
            var policyInput = BuildNumadoraPolicyInput(state.Report, hostCall, check, normalizedPurpose, policyTarget);
            var policyDecision = _numadoraPolicy.Evaluate(policyInput);
            var execution = await ExecuteNumadoraLocalHostCallAsync(hostCall, policyInput, policyDecision, cancellationToken);
            var endedAt = DateTimeOffset.UtcNow;
            var evidence = new List<AutomationEvidence>();
            if (execution.Screenshot is not null)
            {
                SaveScreenshotEvidence(state.Report, sequence, "numadora-host-call", execution.Screenshot, evidence);
            }

            var automationEvent = _artifacts.CreateEvent(
                state.Report,
                sequence,
                "numadora.hostCall",
                startedAt,
                endedAt,
                ok: execution.Ok,
                step: formatted,
                source: new AutomationSource(sourcePath, 1, 1, formatted),
                target: execution.Target,
                parameters: new Dictionary<string, object?>
                {
                    ["language"] = "numadora",
                    ["module"] = hostCall.Module,
                    ["function"] = hostCall.Function,
                    ["arguments"] = hostCall.Arguments,
                    ["raw"] = hostCall.Raw,
                    ["policyInput"] = policyInput,
                    ["policyDecision"] = policyDecision,
                    ["executedBy"] = execution.ExecutedBy
                },
                result: execution.Result,
                logs:
                [
                    new AutomationLogEntry(
                        DateTimeOffset.UtcNow,
                        "info",
                        "numadora.hostCall",
                        formatted)
                ],
                evidence: evidence,
                error: execution.Error);

            state.Report = _artifacts.AppendEvent(state.Report, automationEvent);
            state.Events.Add(automationEvent);
            if (execution.Error is not null)
            {
                state.FinalError = execution.Error;
                break;
            }
        }
    }

    private static IReadOnlyList<AutomationLogEntry> BuildNumadoraLogs(NumadoraProcessResult result)
    {
        var logs = new List<AutomationLogEntry>();
        foreach (var line in SplitProcessLines(result.Stdout))
        {
            if (TryParseStructuredHostCall(line, out var hostCall))
            {
                logs.Add(new AutomationLogEntry(DateTimeOffset.UtcNow, "info", "numadora.hostCall", FormatNumadoraHostCall(hostCall)));
                continue;
            }

            logs.Add(new AutomationLogEntry(DateTimeOffset.UtcNow, "info", "numadora", line));
        }

        foreach (var line in SplitProcessLines(result.Stderr))
        {
            logs.Add(new AutomationLogEntry(DateTimeOffset.UtcNow, "error", "numadora", line));
        }

        return logs;
    }

    private static string FormatNumadoraHostCall(NumadoraHostCall call)
    {
        var name = $"{call.Module}.{call.Function}";
        return call.Arguments.Count == 0 ? name : $"{name} {string.Join(" ", call.Arguments)}";
    }

    private static IReadOnlyList<string> SplitProcessLines(string value)
    {
        return value
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
    }

    private static bool CanRunNumadoraLocally(ScriptCheckResponse check)
    {
        return (check.RequiredCapabilities ?? [])
            .All(CanRunNumadoraCapabilityLocally);
    }

    private IReadOnlyDictionary<string, object?> NumadoraRunDetails(
        ScriptCheckResponse check,
        NumadoraHostCallTrace? trace = null,
        AutomationRunReport? report = null,
        string normalizedPurpose = "local-test")
    {
        var requiredCapabilities = check.RequiredCapabilities ?? [];
        var blockedCapabilities = requiredCapabilities
            .Where(item => !CanRunNumadoraCapabilityLocally(item))
            .ToArray();
        var diagnostics = trace?.Diagnostic is null
            ? check.Diagnostics
            : check.Diagnostics.Concat([trace.Diagnostic]).ToArray();

        return new Dictionary<string, object?>
        {
            ["language"] = check.Language,
            ["diagnostics"] = diagnostics,
            ["requiredCapabilities"] = requiredCapabilities,
            ["blockedCapabilities"] = blockedCapabilities,
            ["allowedLocalModules"] = new[] { "slasher_app", "slasher_window", "slasher_input", "slasher_screen", "slasher_element", "slasher_browser", "slasher_io", "slasher_test" },
            ["allowedLocalHostCalls"] = new[]
            {
                "slasher_app.Start",
                "slasher_window.Focus",
                "slasher_input.Text",
                "slasher_input.Keys",
                "slasher_input.Mouse",
                "slasher_input.Wheel",
                "slasher_input.Drag",
                "slasher_input.ContextMenu",
                "slasher_screen.Capture",
                "slasher_element.Find",
                "slasher_element.Exists",
                "slasher_element.ReadText",
                "slasher_element.Tree",
                "slasher_browser.Current",
                "slasher_browser.Title",
                "slasher_browser.Url",
                "slasher_browser.Locate",
                "slasher_browser.DomText",
                "slasher_browser.Attribute",
                "slasher_browser.Screenshot",
                "slasher_browser.Links",
                "slasher_browser.Windows",
                "slasher_io.*",
                "slasher_window.WaitForTitle",
                "slasher_test.AssertForegroundTitle"
            },
            ["runMode"] = blockedCapabilities.Length == 0 ? "local-numadora-cli" : "blocked-host-call",
            ["hostCalls"] = trace?.HostCalls ?? [],
            ["policyInputs"] = trace?.HostCalls is null || report is null
                ? []
                : trace.HostCalls.Select(call => BuildNumadoraPolicyInput(report, call, check, normalizedPurpose, GetNumadoraPolicyTarget(call))).ToArray(),
            ["policyDecisions"] = trace?.HostCalls is null || report is null
                ? []
                : trace.HostCalls
                    .Select(call => _numadoraPolicy.Evaluate(BuildNumadoraPolicyInput(report, call, check, normalizedPurpose, GetNumadoraPolicyTarget(call))))
                    .ToArray(),
            ["hostCallTraceExitCode"] = trace?.ExitCode,
            ["hostCallTraceRaw"] = trace?.Raw,
            ["hostCallTraceError"] = trace?.Error,
            ["lineCount"] = check.Lines.Count
        };
    }

    private static bool CanRunNumadoraCapabilityLocally(ScriptCapabilityRequirement item)
    {
        return item.Module.Equals("slasher_io", StringComparison.OrdinalIgnoreCase)
            || (item.Module.Equals("slasher_app", StringComparison.OrdinalIgnoreCase)
                && item.Function.Equals("Start", StringComparison.OrdinalIgnoreCase))
            || (item.Module.Equals("slasher_window", StringComparison.OrdinalIgnoreCase)
                && item.Function.Equals("Focus", StringComparison.OrdinalIgnoreCase))
            || (item.Module.Equals("slasher_input", StringComparison.OrdinalIgnoreCase)
                && (item.Function.Equals("Text", StringComparison.OrdinalIgnoreCase)
                    || item.Function.Equals("Keys", StringComparison.OrdinalIgnoreCase)
                    || item.Function.Equals("Mouse", StringComparison.OrdinalIgnoreCase)
                    || item.Function.Equals("Wheel", StringComparison.OrdinalIgnoreCase)
                    || item.Function.Equals("Drag", StringComparison.OrdinalIgnoreCase)
                    || item.Function.Equals("ContextMenu", StringComparison.OrdinalIgnoreCase)))
            || (item.CapabilityClass.Equals("Observe", StringComparison.OrdinalIgnoreCase)
                && item.Profile.Equals("observe", StringComparison.OrdinalIgnoreCase));
    }

    private static (string Title, int TimeoutMs) ParseNumadoraTitleAndOptionalTimeout(
        IReadOnlyList<string> args,
        int defaultTimeoutMs)
    {
        var value = string.Join(' ', args).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return (string.Empty, defaultTimeoutMs);
        }

        var lastSpace = value.LastIndexOf(' ');
        if (lastSpace > 0 && int.TryParse(value[(lastSpace + 1)..], out var timeoutMs))
        {
            return (value[..lastSpace].Trim(), Math.Max(1, timeoutMs));
        }

        return (value, defaultTimeoutMs);
    }

    private static (string Operator, string Expected) ParseNumadoraOperatorAndExpected(IReadOnlyList<string> args)
    {
        var value = string.Join(' ', args).Trim();
        var firstSpace = value.IndexOf(' ');
        return firstSpace <= 0
            ? (value, string.Empty)
            : (value[..firstSpace].Trim(), value[(firstSpace + 1)..].Trim());
    }

    private static bool MatchesNumadoraTitleAssertion(string actual, string op, string expected)
    {
        return op.ToLowerInvariant() switch
        {
            "contains" => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            "notcontains" or "not-contains" => !actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            "==" or "=" or "equals" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "!=" or "notequals" or "not-equals" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "startswith" or "starts-with" => actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
            "endswith" or "ends-with" => actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static IReadOnlyList<NumadoraHostCall> ParseNumadoraHostCalls(string output)
    {
        var calls = new List<NumadoraHostCall>();
        foreach (var rawLine in SplitProcessLines(output))
        {
            if (TryParseStructuredHostCall(rawLine, out var structured))
            {
                calls.Add(structured);
                continue;
            }

            if (TryParseLegacyHostCall(rawLine, out var legacy))
            {
                calls.Add(legacy);
            }
        }

        return calls;
    }

    private static bool TryParseStructuredHostCall(string line, out NumadoraHostCall call)
    {
        const string prefix = "__SLASHER_HOST_CALL__ ";
        call = default!;
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = line[prefix.Length..].Trim();
        var firstSpace = rest.IndexOf(' ', StringComparison.Ordinal);
        var name = firstSpace < 0 ? rest : rest[..firstSpace];
        var args = firstSpace < 0 ? string.Empty : rest[(firstSpace + 1)..].Trim();
        var dot = name.LastIndexOf('.');
        if (dot <= 0 || dot == name.Length - 1)
        {
            return false;
        }

        call = new NumadoraHostCall(
            name[..dot],
            name[(dot + 1)..],
            string.IsNullOrEmpty(args) ? [] : [args],
            line);
        return true;
    }

    private static bool TryParseLegacyHostCall(string line, out NumadoraHostCall call)
    {
        call = default!;
        var firstSpace = line.IndexOf(' ', StringComparison.Ordinal);
        var name = firstSpace < 0 ? line : line[..firstSpace];
        var args = firstSpace < 0 ? string.Empty : line[(firstSpace + 1)..].Trim();
        var mapped = name switch
        {
            "app.Start" => ("slasher_app", "Start"),
            "window.WaitForTitle" => ("slasher_window", "WaitForTitle"),
            "window.Focus" => ("slasher_window", "Focus"),
            "input.Text" => ("slasher_input", "Text"),
            "input.Keys" => ("slasher_input", "Keys"),
            "input.Mouse" => ("slasher_input", "Mouse"),
            "input.Wheel" => ("slasher_input", "Wheel"),
            "input.Drag" => ("slasher_input", "Drag"),
            "input.ContextMenu" => ("slasher_input", "ContextMenu"),
            "screen.Capture" => ("slasher_screen", "Capture"),
            "element.Find" => ("slasher_element", "Find"),
            "element.Exists" => ("slasher_element", "Exists"),
            "element.ReadText" => ("slasher_element", "ReadText"),
            "element.Tree" => ("slasher_element", "Tree"),
            "browser.Current" => ("slasher_browser", "Current"),
            "browser.Title" => ("slasher_browser", "Title"),
            "browser.Url" => ("slasher_browser", "Url"),
            "browser.Locate" => ("slasher_browser", "Locate"),
            "browser.DomText" => ("slasher_browser", "DomText"),
            "browser.Attribute" => ("slasher_browser", "Attribute"),
            "browser.Screenshot" => ("slasher_browser", "Screenshot"),
            "browser.Links" => ("slasher_browser", "Links"),
            "browser.Windows" => ("slasher_browser", "Windows"),
            "test.AssertForegroundTitle" => ("slasher_test", "AssertForegroundTitle"),
            "step" => ("slasher_io", "Step"),
            "wait" => ("slasher_io", "Wait"),
            _ => default
        };

        if (mapped == default)
        {
            return false;
        }

        call = new NumadoraHostCall(
            mapped.Item1,
            mapped.Item2,
            string.IsNullOrEmpty(args) ? [] : [args],
            line);
        return true;
    }

    private sealed record NumadoraHostCallTrace(
        IReadOnlyList<NumadoraHostCall> HostCalls,
        int? ExitCode,
        string? Raw,
        string? Error,
        ScriptDiagnostic? Diagnostic);

    private sealed record NumadoraHostCall(
        string Module,
        string Function,
        IReadOnlyList<string> Arguments,
        string Raw);

}
