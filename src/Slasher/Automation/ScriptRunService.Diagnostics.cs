using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private AutomationError EnrichErrorDiagnostics(ScriptExecutionState state, AutomationError error)
    {
        var details = error.Details is null
            ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, object?>(error.Details, StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<Dictionary<string, object?>>();

        if (state.SelectedWindow is null)
        {
            diagnostics.Add(new Dictionary<string, object?>
            {
                ["code"] = "no_selected_window",
                ["message"] = "No Slasher target window is selected. Use app select, select, or foreground before target-specific input and captures.",
                ["severity"] = "warning"
            });
        }
        else
        {
            details["selectedWindow"] = ToDiagnosticWindow(state.SelectedWindow);
        }

        if (_automation.TryGetForegroundWindow(out var foreground) && foreground is not null)
        {
            details["foregroundWindow"] = ToDiagnosticWindow(foreground);
            if (state.SelectedWindow is not null
                && !string.Equals(state.SelectedWindow.Handle, foreground.Handle, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new Dictionary<string, object?>
                {
                    ["code"] = "foreground_mismatch",
                    ["message"] = "The selected window is not the current foreground window. Input may have gone to a different app.",
                    ["severity"] = "warning",
                    ["selectedHandle"] = state.SelectedWindow.Handle,
                    ["foregroundHandle"] = foreground.Handle
                });
            }

            if (LooksLikeControlSurface(foreground))
            {
                diagnostics.Add(new Dictionary<string, object?>
                {
                    ["code"] = "possible_control_surface_capture",
                    ["message"] = "The foreground window looks like Slasher, Codex, or a browser. Confirm that the app under test is selected before relying on this capture.",
                    ["severity"] = "warning",
                    ["foregroundTitle"] = foreground.Title,
                    ["foregroundProcess"] = foreground.ProcessName
                });
            }
        }

        if (diagnostics.Count > 0)
        {
            details["diagnostics"] = diagnostics;
        }

        return error with { Details = details.Count == 0 ? null : details };
    }

    private static Dictionary<string, object?> ToDiagnosticWindow(WindowInfo window)
    {
        return new Dictionary<string, object?>
        {
            ["handle"] = window.Handle,
            ["title"] = window.Title,
            ["processId"] = window.ProcessId,
            ["processName"] = window.ProcessName,
            ["className"] = window.ClassName,
            ["bounds"] = new Dictionary<string, object?>
            {
                ["x"] = window.Bounds.X,
                ["y"] = window.Bounds.Y,
                ["width"] = window.Bounds.Width,
                ["height"] = window.Bounds.Height
            },
            ["isVisible"] = window.IsVisible,
            ["isEnabled"] = window.IsEnabled,
            ["isMinimized"] = window.IsMinimized
        };
    }

    private static bool LooksLikeControlSurface(WindowInfo window)
    {
        var process = (window.ProcessName ?? string.Empty).ToLowerInvariant();
        var title = (window.Title ?? string.Empty).ToLowerInvariant();
        return process is "slasher" or "codex" or "code" or "msedge" or "chrome" or "firefox"
            || title.Contains("slasher", StringComparison.OrdinalIgnoreCase)
            || title.Contains("codex", StringComparison.OrdinalIgnoreCase);
    }
}
