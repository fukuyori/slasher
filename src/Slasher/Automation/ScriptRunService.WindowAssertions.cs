using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private async Task<ScriptCommandResult> ExecuteWindowAssertAsync(
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        RequireArgs(args, 2, "assert window requires 'exists <title>' or 'not exists <title>'.");
        var negate = args[0].Equals("not", StringComparison.OrdinalIgnoreCase);
        var existsIndex = negate ? 1 : 0;
        if (!args[existsIndex].Equals("exists", StringComparison.OrdinalIgnoreCase))
        {
            throw new ScriptCommandException("unsupported_assertion", "assert window supports exists and not exists.");
        }

        var titleArgs = args.Skip(existsIndex + 1).ToArray();
        RequireArgs(titleArgs, 1, "assert window exists requires a title.");
        var timeoutMs = int.TryParse(titleArgs[^1], out var parsedTimeout) ? parsedTimeout : 0;
        var titleEnd = int.TryParse(titleArgs[^1], out _) ? titleArgs.Length - 1 : titleArgs.Length;
        var title = string.Join(' ', titleArgs[..titleEnd]);

        WindowInfo? window = null;
        if (negate)
        {
            window = _automation.FindWindow(new WindowQueryRequest(title));
        }
        else if (timeoutMs > 0)
        {
            window = await _automation.WaitForWindowAsync(new WindowQueryRequest(title, TimeoutMs: timeoutMs), cancellationToken);
        }
        else
        {
            window = _automation.FindWindow(new WindowQueryRequest(title));
        }

        if (negate && window is not null)
        {
            throw new ScriptCommandException(
                "assertion_failed",
                $"Expected no window containing '{title}', but found '{window.Title}'.",
                Expected: new { exists = false, title },
                Actual: new { exists = true, window.Title, window.Handle });
        }

        if (!negate && window is null)
        {
            throw new ScriptCommandException(
                "assertion_failed",
                $"Expected a window containing '{title}', but none was found.",
                Expected: new { exists = true, title },
                Actual: new { exists = false });
        }

        return new ScriptCommandResult(new { asserted = true, exists = !negate, title }, window?.Handle, window);
    }

    private ScriptCommandResult ExecuteWindowTitleAssert(
        string subject,
        IReadOnlyList<string> args,
        string? selectedHandle)
    {
        var titleArgs = subject switch
        {
            "selected" => args.Skip(1).ToArray(),
            "foreground" or "active" => args.Skip(1).ToArray(),
            "title" => args,
            _ => throw new ScriptCommandException("unsupported_assertion", "Unsupported title assertion target.")
        };

        WindowInfo? window;
        if (subject == "selected")
        {
            var handle = RequireSelected(selectedHandle);
            if (!_automation.TryGetWindow(handle, out window) || window is null)
            {
                throw new ScriptCommandException("window_not_found", $"Selected window '{handle}' was not found.");
            }
        }
        else
        {
            if (!_automation.TryGetForegroundWindow(out window) || window is null)
            {
                throw new ScriptCommandException("foreground_window_not_found", "No foreground window was found.");
            }
        }

        AssertTitle(window, titleArgs);
        return new ScriptCommandResult(new { asserted = true, title = window.Title }, window.Handle, window);
    }

    private static void AssertTitle(WindowInfo window, IReadOnlyList<string> args)
    {
        RequireArgs(args, 3, "title assertion syntax is: assert [selected|foreground] title <operator> <expected>.");
        if (!args[0].Equals("title", StringComparison.OrdinalIgnoreCase))
        {
            throw new ScriptCommandException("unsupported_assertion", "Only title assertions are supported for selected and foreground windows.");
        }

        var op = args[1].ToLowerInvariant();
        var expected = string.Join(' ', args.Skip(2));
        var actual = window.Title ?? string.Empty;
        var passed = op switch
        {
            "contains" => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            "notcontains" or "not-contains" => !actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            "==" or "=" or "equals" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "!=" or "notequals" or "not-equals" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "startswith" or "starts-with" => actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
            "endswith" or "ends-with" => actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase),
            _ => throw new ScriptCommandException("unsupported_assertion_operator", $"Unsupported title assertion operator '{op}'.")
        };

        if (!passed)
        {
            throw new ScriptCommandException(
                "assertion_failed",
                $"Title assertion failed. Expected title {op} '{expected}', actual '{actual}'.",
                Expected: new { title = expected, op },
                Actual: new { title = actual, window.Handle });
        }
    }
}
