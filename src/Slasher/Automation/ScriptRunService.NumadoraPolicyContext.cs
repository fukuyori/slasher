using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<IReadOnlyDictionary<string, object?>> BuildNumadoraLineageAsync(
        ScriptCheckRequest checkRequest,
        string entryPoint,
        string purpose,
        CancellationToken cancellationToken)
    {
        var source = checkRequest.Script;
        if (source is null && !string.IsNullOrWhiteSpace(checkRequest.Path))
        {
            source = await File.ReadAllTextAsync(ResolveScriptPath(checkRequest.Path), cancellationToken);
        }

        return new Dictionary<string, object?>
        {
            ["purpose"] = purpose,
            ["actor"] = new Dictionary<string, object?>
            {
                ["kind"] = "local-user",
                ["surface"] = entryPoint.Equals("POST /scripts/run", StringComparison.OrdinalIgnoreCase) ? "http" : "script-file"
            },
            ["script"] = new Dictionary<string, object?>
            {
                ["language"] = "numadora",
                ["entryPoint"] = entryPoint,
                ["sha256"] = source is null ? null : Sha256Hex(source)
            },
            ["data"] = new Dictionary<string, object?>
            {
                ["classification"] = "local",
                ["redaction"] = "default"
            }
        };
    }

    private static NumadoraPolicyInput BuildNumadoraPolicyInput(
        AutomationRunReport report,
        NumadoraHostCall hostCall,
        ScriptCheckResponse check,
        string normalizedPurpose,
        AutomationTarget? target = null)
    {
        return new NumadoraPolicyInput(
            "numadora",
            report.RunId,
            normalizedPurpose,
            NumadoraSurfaceFromReport(report),
            FindNumadoraCapability(hostCall.Module, hostCall.Function, check),
            new NumadoraPolicyHostCall(hostCall.Module, hostCall.Function, hostCall.Arguments),
            NumadoraLineageFromReport(report),
            target,
            NumadoraApprovalsFromReport(report));
    }

    private AutomationTarget? GetNumadoraPolicyTarget(NumadoraHostCall hostCall)
    {
        if (hostCall.Module.Equals("slasher_window", StringComparison.OrdinalIgnoreCase)
            && hostCall.Function.Equals("Focus", StringComparison.OrdinalIgnoreCase))
        {
            var handle = string.Join(' ', hostCall.Arguments).Trim();
            return string.IsNullOrWhiteSpace(handle)
                ? null
                : new AutomationTarget("window", Handle: handle);
        }

        return _automation.TryGetForegroundWindow(out var foreground) && foreground is not null
            ? ToTarget(foreground)
            : null;
    }

    private static ScriptCapabilityRequirement? FindNumadoraCapability(
        string module,
        string function,
        ScriptCheckResponse check)
    {
        var existing = (check.RequiredCapabilities ?? [])
            .FirstOrDefault(item =>
                item.Module.Equals(module, StringComparison.OrdinalIgnoreCase)
                && item.Function.Equals(function, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        if (!NumadoraBindingCapabilities.TryGetValue(CapabilityKey(module, function), out var capability))
        {
            return null;
        }

        return new ScriptCapabilityRequirement(
            capability.Module,
            capability.Function,
            capability.CapabilityClass,
            capability.Profile,
            capability.Reason);
    }

    private static string NormalizeNumadoraPurpose(string? purpose)
    {
        return string.IsNullOrWhiteSpace(purpose) ? "local-test" : purpose.Trim();
    }

    private static string NumadoraPurposeFromReport(AutomationRunReport report)
    {
        return report.Metadata is not null
            && report.Metadata.TryGetValue("purpose", out var purpose)
            && purpose is string value
            && !string.IsNullOrWhiteSpace(value)
            ? value
            : "local-test";
    }

    private static string NumadoraSurfaceFromReport(AutomationRunReport report)
    {
        return report.EntryPoint?.Equals("POST /scripts/run", StringComparison.OrdinalIgnoreCase) == true
            ? "http"
            : "script-file";
    }

    private static IReadOnlyDictionary<string, object?> NumadoraLineageFromReport(AutomationRunReport report)
    {
        if (report.Metadata is not null
            && report.Metadata.TryGetValue("lineage", out var lineage)
            && lineage is IReadOnlyDictionary<string, object?> typed)
        {
            return typed;
        }

        return new Dictionary<string, object?>
        {
            ["purpose"] = NumadoraPurposeFromReport(report),
            ["runId"] = report.RunId
        };
    }

    private static IReadOnlyDictionary<string, object?> NumadoraApprovalsFromReport(AutomationRunReport report)
    {
        if (report.Metadata is not null
            && report.Metadata.TryGetValue("approvals", out var approvals)
            && approvals is IReadOnlyDictionary<string, object?> typed)
        {
            return typed;
        }

        return new Dictionary<string, object?>
        {
            ["interactiveInput"] = false
        };
    }

    private static string Sha256Hex(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
