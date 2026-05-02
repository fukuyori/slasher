using System.Text.RegularExpressions;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private sealed record NumadoraBindingCapability(
        string Module,
        string Function,
        string CapabilityClass,
        string Profile,
        string Reason);

    private static readonly Regex NumadoraImportPattern = new(
        @"^\s*IMPORT\s+(?<module>[A-Za-z_][A-Za-z0-9_]*)\s+AS\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex NumadoraAliasCallPattern = new(
        @"(?<alias>[A-Za-z_][A-Za-z0-9_]*)\.(?<function>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
        RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, NumadoraBindingCapability> NumadoraBindingCapabilities =
        new Dictionary<string, NumadoraBindingCapability>(StringComparer.OrdinalIgnoreCase)
        {
            [CapabilityKey("slasher_app", "Start")] = new(
                "slasher_app",
                "Start",
                "Process/app",
                "interactive",
                "Starts a local application process and records process/window metadata."),
            [CapabilityKey("slasher_window", "WaitForTitle")] = new(
                "slasher_window",
                "WaitForTitle",
                "Observe",
                "observe",
                "Inspects top-level windows until a matching title is found."),
            [CapabilityKey("slasher_window", "Focus")] = new(
                "slasher_window",
                "Focus",
                "User-input",
                "interactive",
                "Changes foreground focus to a target window."),
            [CapabilityKey("slasher_input", "Text")] = new(
                "slasher_input",
                "Text",
                "User-input",
                "interactive",
                "Types text into the active or selected application."),
            [CapabilityKey("slasher_io", "Step")] = new(
                "slasher_io",
                "Step",
                "Observe",
                "observe",
                "Adds an auditable step marker to the run log."),
            [CapabilityKey("slasher_io", "Log")] = new(
                "slasher_io",
                "Log",
                "Observe",
                "observe",
                "Adds a non-secret log message to the run log."),
            [CapabilityKey("slasher_io", "Wait")] = new(
                "slasher_io",
                "Wait",
                "Observe",
                "observe",
                "Waits without issuing GUI input."),
            [CapabilityKey("slasher_test", "AssertForegroundTitle")] = new(
                "slasher_test",
                "AssertForegroundTitle",
                "Observe",
                "observe",
                "Inspects the foreground window title for an assertion.")
        };

    private static IReadOnlyList<ScriptCapabilityRequirement> FindNumadoraRequiredCapabilities(string source)
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in source.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var match = NumadoraImportPattern.Match(line);
            if (match.Success)
            {
                aliases[match.Groups["alias"].Value] = match.Groups["module"].Value;
            }
        }

        var requirements = new Dictionary<string, ScriptCapabilityRequirement>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in NumadoraAliasCallPattern.Matches(source))
        {
            var alias = match.Groups["alias"].Value;
            if (!aliases.TryGetValue(alias, out var module))
            {
                continue;
            }

            var function = match.Groups["function"].Value;
            if (!NumadoraBindingCapabilities.TryGetValue(CapabilityKey(module, function), out var capability))
            {
                continue;
            }

            requirements[CapabilityKey(module, function)] = new ScriptCapabilityRequirement(
                capability.Module,
                capability.Function,
                capability.CapabilityClass,
                capability.Profile,
                capability.Reason);
        }

        return requirements.Values
            .OrderBy(item => item.Module, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Function, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string CapabilityKey(string module, string function)
    {
        return $"{module}.{function}";
    }
}
