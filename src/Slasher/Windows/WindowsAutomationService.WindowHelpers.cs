using System.Diagnostics;
using System.Text;
using Slasher.Api;

namespace Slasher.Windows;

public sealed partial class WindowsAutomationService
{
    private static bool TryReadWindow(IntPtr hwnd, out WindowInfo? info)
    {
        info = null;
        if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
        {
            return false;
        }

        var title = ReadText(hwnd, NativeMethods.GetWindowText);
        if (string.IsNullOrWhiteSpace(title) && !NativeMethods.IsWindowVisible(hwnd))
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return false;
        }

        string? processName = null;
        try
        {
            processName = Process.GetProcessById((int)pid).ProcessName;
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        info = new WindowInfo(
            WindowHandle.Format(hwnd),
            title,
            ReadText(hwnd, NativeMethods.GetClassName),
            (int)pid,
            processName,
            new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
            NativeMethods.IsWindowVisible(hwnd),
            NativeMethods.IsWindowEnabled(hwnd),
            NativeMethods.IsIconic(hwnd));

        return true;
    }

    private static int ScoreAppWindow(WindowInfo window, string name, bool exact)
    {
        var processName = NormalizeAppName(window.ProcessName ?? string.Empty);
        var title = (window.Title ?? string.Empty).Trim().ToLowerInvariant();
        var className = (window.ClassName ?? string.Empty).Trim().ToLowerInvariant();
        var query = name.ToLowerInvariant();

        var score = 0;
        if (exact)
        {
            if (processName == query)
            {
                score += 120;
            }

            if (title == query)
            {
                score += 80;
            }
        }
        else
        {
            if (processName == query)
            {
                score += 120;
            }
            else if (processName.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 90;
            }

            if (title == query)
            {
                score += 80;
            }
            else if (title.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                score += 50;
            }
        }

        if (score == 0)
        {
            return 0;
        }

        if (IsUtilityWindowTitle(title) || IsUtilityWindowClass(className))
        {
            score -= 200;
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            score -= 40;
        }

        if (className == query || className.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            score += 40;
        }

        if (window.Bounds.Width >= 300 && window.Bounds.Height >= 200)
        {
            score += 50;
        }

        if (window.IsVisible)
        {
            score += 80;
        }
        else
        {
            score -= 80;
        }

        if (window.IsEnabled)
        {
            score += 15;
        }

        if (!window.IsMinimized)
        {
            score += 20;
        }

        return score;
    }

    private static bool IsUtilityWindowTitle(string title)
    {
        return title is "default ime"
            || title is "msctime ui"
            || title is "msctfime ui"
            || title.StartsWith("gdi+ window", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUtilityWindowClass(string className)
    {
        return className is "ime"
            || className is "msctfime ui"
            || className is "gdi+ hook window class";
    }

    private static string NormalizeAppName(string value)
    {
        var leaf = Path.GetFileNameWithoutExtension(value.Trim());
        return string.IsNullOrWhiteSpace(leaf)
            ? value.Trim().ToLowerInvariant()
            : leaf.ToLowerInvariant();
    }

    private static string ReadText(IntPtr hwnd, Func<IntPtr, StringBuilder, int, int> reader)
    {
        var builder = new StringBuilder(512);
        _ = reader(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static bool TryResolveWindow(string handleText, out IntPtr handle, out ErrorResponse? error)
    {
        if (!TryParseHandle(handleText, out handle, out error))
        {
            return false;
        }

        if (!NativeMethods.IsWindow(handle))
        {
            error = new ErrorResponse("window_not_found", $"Window '{handleText}' was not found.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryParseHandle(string handleText, out IntPtr handle, out ErrorResponse? error)
    {
        try
        {
            handle = WindowHandle.Parse(handleText);
            error = null;
            return true;
        }
        catch (FormatException)
        {
            handle = IntPtr.Zero;
            error = new ErrorResponse("invalid_handle", "Handle must be a decimal number or 0x-prefixed hexadecimal value.");
            return false;
        }
        catch (OverflowException)
        {
            handle = IntPtr.Zero;
            error = new ErrorResponse("invalid_handle", "Handle is outside the supported pointer range.");
            return false;
        }
    }
}

