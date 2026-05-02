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
            [CapabilityKey("slasher_input", "Keys")] = new(
                "slasher_input",
                "Keys",
                "User-input",
                "interactive",
                "Sends a key chord to the active or selected application."),
            [CapabilityKey("slasher_input", "Mouse")] = new(
                "slasher_input",
                "Mouse",
                "User-input",
                "interactive",
                "Sends a mouse action to the active desktop target."),
            [CapabilityKey("slasher_input", "Wheel")] = new(
                "slasher_input",
                "Wheel",
                "User-input",
                "interactive",
                "Sends a mouse wheel action to the active desktop target."),
            [CapabilityKey("slasher_input", "Drag")] = new(
                "slasher_input",
                "Drag",
                "User-input",
                "interactive",
                "Sends a mouse drag action to the active desktop target."),
            [CapabilityKey("slasher_input", "ContextMenu")] = new(
                "slasher_input",
                "ContextMenu",
                "User-input",
                "interactive",
                "Opens a context menu at a desktop coordinate and records visual observation metadata."),
            [CapabilityKey("slasher_screen", "Capture")] = new(
                "slasher_screen",
                "Capture",
                "Observe",
                "observe",
                "Captures the full desktop or current foreground target as screenshot evidence."),
            [CapabilityKey("slasher_element", "Find")] = new(
                "slasher_element",
                "Find",
                "Observe",
                "observe",
                "Finds native window elements within the foreground or selected target."),
            [CapabilityKey("slasher_element", "Exists")] = new(
                "slasher_element",
                "Exists",
                "Observe",
                "observe",
                "Checks whether a native window element exists."),
            [CapabilityKey("slasher_element", "ReadText")] = new(
                "slasher_element",
                "ReadText",
                "Observe",
                "observe",
                "Reads text from a matching native window element."),
            [CapabilityKey("slasher_element", "Tree")] = new(
                "slasher_element",
                "Tree",
                "Observe",
                "observe",
                "Captures a bounded native element tree for a target window."),
            [CapabilityKey("slasher_browser", "Current")] = new(
                "slasher_browser",
                "Current",
                "Observe",
                "observe",
                "Reads metadata for the current WebDriver browser session."),
            [CapabilityKey("slasher_browser", "Title")] = new(
                "slasher_browser",
                "Title",
                "Observe",
                "observe",
                "Reads the current browser page title."),
            [CapabilityKey("slasher_browser", "Url")] = new(
                "slasher_browser",
                "Url",
                "Observe",
                "observe",
                "Reads the current browser page URL."),
            [CapabilityKey("slasher_browser", "Locate")] = new(
                "slasher_browser",
                "Locate",
                "Observe",
                "observe",
                "Finds a browser DOM element without changing page state."),
            [CapabilityKey("slasher_browser", "DomText")] = new(
                "slasher_browser",
                "DomText",
                "Observe",
                "observe",
                "Reads text from a browser DOM element."),
            [CapabilityKey("slasher_browser", "Attribute")] = new(
                "slasher_browser",
                "Attribute",
                "Observe",
                "observe",
                "Reads an attribute from a browser DOM element."),
            [CapabilityKey("slasher_browser", "Screenshot")] = new(
                "slasher_browser",
                "Screenshot",
                "Observe",
                "observe",
                "Captures the current browser viewport as screenshot evidence."),
            [CapabilityKey("slasher_browser", "Links")] = new(
                "slasher_browser",
                "Links",
                "Observe",
                "observe",
                "Reads links from the current browser page."),
            [CapabilityKey("slasher_browser", "Windows")] = new(
                "slasher_browser",
                "Windows",
                "Observe",
                "observe",
                "Reads browser window and tab handles."),
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
