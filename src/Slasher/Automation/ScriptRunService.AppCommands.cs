using Slasher.Api;
using Slasher.Windows;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private ScriptCommandResult ExecuteAppCommand(string verb, IReadOnlyList<string> args)
    {
        switch (verb)
        {
            case "start":
            {
                RequireArgs(args, 1, "start requires a file name.");
                var result = _automation.StartApp(new StartAppRequest(args[0], JoinArgs(args, 1)));
                return new ScriptCommandResult(result, result.MainWindowHandle, AssignmentValue: result);
            }
            case "app":
            case "application":
            {
                if (args.Count >= 2 && args[0].Equals("select", StringComparison.OrdinalIgnoreCase))
                {
                    var selected = SelectApp(args.Skip(1).ToArray());
                    return new ScriptCommandResult(selected, selected.Handle, selected, AssignmentValue: selected);
                }

                throw new ScriptCommandException("unsupported_command", "app supports 'app select <name>' in server-side script runs.");
            }
            case "select":
                return ExecuteSelectCommand(args);
            case "foreground":
            {
                if (!_automation.TryGetForegroundWindow(out var window) || window is null)
                {
                    throw new ScriptCommandException("foreground_window_not_found", "No foreground window was found.");
                }

                return new ScriptCommandResult(window, window.Handle, window, AssignmentValue: window);
            }
            default:
                throw new ScriptCommandException("unsupported_command", $"Unsupported app command: {verb}");
        }
    }

    private ScriptCommandResult ExecuteSelectCommand(IReadOnlyList<string> args)
    {
        if (args.Count >= 2 && args[0].Equals("app", StringComparison.OrdinalIgnoreCase))
        {
            var selected = SelectApp(args.Skip(1).ToArray());
            return new ScriptCommandResult(selected, selected.Handle, selected, AssignmentValue: selected);
        }

        RequireArgs(args, 1, "select requires a window handle or 'select app <name>'.");
        if (!_automation.TryGetWindow(args[0], out var window) || window is null)
        {
            throw new ScriptCommandException("window_not_found", $"Window '{args[0]}' was not found.");
        }

        return new ScriptCommandResult(window, window.Handle, window, AssignmentValue: window);
    }

    private WindowInfo SelectApp(IReadOnlyList<string> args)
    {
        RequireArgs(args, 1, "app select requires an app/process name or title.");
        var match = args[^1].Equals("exact", StringComparison.OrdinalIgnoreCase) ? "exact" : "contains";
        var nameArgs = match == "exact" ? args.Take(args.Count - 1) : args;
        var name = string.Join(' ', nameArgs);
        var window = _automation.SelectApp(new AppSelectRequest(name, match, Focus: true), out var error);
        if (window is null)
        {
            throw FromError(error, "app_window_not_found");
        }

        return window;
    }
}
