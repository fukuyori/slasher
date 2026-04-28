using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Slasher.Api;

namespace Slasher.Windows;

public sealed partial class WindowsAutomationService
{
    public IReadOnlyList<WindowInfo> ListWindows(string? titleFilter, int? processId)
    {
        var windows = new List<WindowInfo>();

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (TryReadWindow(hwnd, out var info)
                && info is not null
                && (string.IsNullOrWhiteSpace(titleFilter) || info.Title.Contains(titleFilter, StringComparison.OrdinalIgnoreCase))
                && (processId is null || info.ProcessId == processId.Value))
            {
                windows.Add(info);
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    public WindowInfo? FindWindow(WindowQueryRequest request)
    {
        return ListWindows(request.Title, request.ProcessId)
            .Where(window => string.IsNullOrWhiteSpace(request.ProcessName)
                || string.Equals(window.ProcessName, request.ProcessName, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(window => string.IsNullOrWhiteSpace(request.Title)
                || (request.Match.Equals("exact", StringComparison.OrdinalIgnoreCase)
                    ? string.Equals(window.Title, request.Title, StringComparison.OrdinalIgnoreCase)
                    : window.Title.Contains(request.Title, StringComparison.OrdinalIgnoreCase)));
    }

    public WindowInfo? SelectApp(AppSelectRequest request, out ErrorResponse? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            error = new ErrorResponse("invalid_app_name", "Name is required.");
            return null;
        }

        var name = NormalizeAppName(request.Name);
        var exact = request.Match.Equals("exact", StringComparison.OrdinalIgnoreCase);
        if (!exact && !request.Match.Equals("contains", StringComparison.OrdinalIgnoreCase))
        {
            error = new ErrorResponse("invalid_match", "Match must be exact or contains.");
            return null;
        }

        var selected = ListWindows(null, null)
            .Select(window => new
            {
                Window = window,
                Score = ScoreAppWindow(window, name, exact)
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Window.Bounds.Width * item.Window.Bounds.Height)
            .Select(item => item.Window)
            .FirstOrDefault();

        if (selected is null)
        {
            error = new ErrorResponse("app_window_not_found", $"No window was found for app '{request.Name}'.");
            return null;
        }

        if (request.Focus && !FocusWindow(selected.Handle, out error))
        {
            return null;
        }

        return TryGetWindow(selected.Handle, out var refreshed) && refreshed is not null
            ? refreshed
            : selected;
    }

    public async Task<WindowInfo?> WaitForWindowAsync(WindowQueryRequest request, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromMilliseconds(Math.Max(1, request.TimeoutMs));
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var window = FindWindow(request);
            if (window is not null)
            {
                return window;
            }

            await Task.Delay(200, cancellationToken);
        }

        return null;
    }

    public string? GetActiveWindowTitle()
    {
        return TryGetForegroundWindow(out var window) ? window?.Title : null;
    }

    public int CloseAllWindows(CloseAllWindowsRequest request)
    {
        var windows = ListWindows(request.Title, null)
            .Where(window => string.IsNullOrWhiteSpace(request.ProcessName)
                || string.Equals(window.ProcessName, request.ProcessName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var closed = 0;
        foreach (var window in windows)
        {
            if (CloseWindow(window.Handle, out _))
            {
                closed++;
            }
        }

        return closed;
    }

    public int CloseProgram(CloseProgramRequest request)
    {
        IEnumerable<Process> processes;
        if (request.ProcessId is not null)
        {
            processes = [Process.GetProcessById(request.ProcessId.Value)];
        }
        else if (!string.IsNullOrWhiteSpace(request.ProcessName))
        {
            processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(request.ProcessName));
        }
        else
        {
            throw new ArgumentException("ProcessName or ProcessId is required.");
        }

        var closed = 0;
        foreach (var process in processes)
        {
            if (request.Force)
            {
                process.Kill(entireProcessTree: true);
                closed++;
            }
            else if (process.CloseMainWindow())
            {
                closed++;
            }
        }

        return closed;
    }

    public bool TryGetWindow(string handleText, out WindowInfo? window)
    {
        window = null;
        if (!TryParseHandle(handleText, out var handle, out _))
        {
            return false;
        }

        return TryReadWindow(handle, out window);
    }

    public bool TryGetForegroundWindow(out WindowInfo? window)
    {
        var handle = NativeMethods.GetForegroundWindow();
        return TryReadWindow(handle, out window);
    }
}

