using Slasher.Api;

namespace Slasher.Automation;

public sealed partial class ScriptRunService
{
    private sealed class NumadoraHostReferenceState
    {
        public const string LastAppRef = "app:last";
        public const string LastWindowRef = "window:last";

        private readonly Dictionary<string, NumadoraAppReference> _apps = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AutomationTarget> _windows = new(StringComparer.OrdinalIgnoreCase);

        public NumadoraAppReference RegisterApp(
            StartAppResponse result,
            string fileName,
            string resolvedFileName)
        {
            var stableRef = StableAppRef(result.ProcessId);
            var reference = new NumadoraAppReference(
                stableRef,
                result.ProcessId,
                result.ProcessName,
                fileName,
                resolvedFileName,
                result.MainWindowHandle,
                result.MainWindowTitle);
            _apps[reference.Ref] = reference;
            _apps[LastAppRef] = reference;

            if (!string.IsNullOrWhiteSpace(result.MainWindowHandle))
            {
                RegisterWindow(new AutomationTarget(
                    "window",
                    result.MainWindowHandle,
                    result.MainWindowTitle,
                    ProcessId: result.ProcessId,
                    ProcessName: result.ProcessName));
            }

            return reference;
        }

        public AutomationTarget RegisterWindow(WindowInfo window)
        {
            return RegisterWindow(ToTarget(window)!);
        }

        public AutomationTarget RegisterWindow(AutomationTarget target)
        {
            _windows[LastWindowRef] = target;
            if (!string.IsNullOrWhiteSpace(target.Handle))
            {
                _windows[target.Handle] = target;
                _windows[StableWindowRef(target.Handle)] = target;
            }

            return target;
        }

        public bool TryGetApp(string? reference, out NumadoraAppReference app)
        {
            app = null!;
            return !string.IsNullOrWhiteSpace(reference)
                && _apps.TryGetValue(reference, out app!);
        }

        public bool TryGetWindow(string? reference, out AutomationTarget target)
        {
            target = null!;
            return !string.IsNullOrWhiteSpace(reference)
                && _windows.TryGetValue(reference, out target!);
        }

        public AutomationTarget? ResolveWindowTarget(string? reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return null;
            }

            if (TryGetWindow(reference, out var target))
            {
                return target;
            }

            return LooksLikeWindowReference(reference)
                ? new AutomationTarget("window", Handle: NormalizeWindowHandle(reference))
                : null;
        }

        public static bool LooksLikeWindowHandle(string value)
        {
            return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        }

        public static bool LooksLikeWindowReference(string value)
        {
            return value.StartsWith("window:", StringComparison.OrdinalIgnoreCase)
                || LooksLikeWindowHandle(value);
        }

        private static string StableAppRef(int processId)
        {
            return $"app:{processId}";
        }

        private static string StableWindowRef(string handle)
        {
            return handle.StartsWith("window:", StringComparison.OrdinalIgnoreCase)
                ? handle
                : $"window:{handle}";
        }

        private static string NormalizeWindowHandle(string value)
        {
            return value.StartsWith("window:", StringComparison.OrdinalIgnoreCase)
                ? value["window:".Length..]
                : value;
        }
    }

    private sealed record NumadoraAppReference(
        string Ref,
        int ProcessId,
        string ProcessName,
        string? FileName,
        string? ResolvedFileName,
        string? MainWindowHandle,
        string? MainWindowTitle);
}
