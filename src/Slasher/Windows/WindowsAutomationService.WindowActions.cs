using Slasher.Api;

namespace Slasher.Windows;

public sealed partial class WindowsAutomationService
{
    public bool FocusWindow(string handleText, out ErrorResponse? error)
    {
        if (!TryResolveWindow(handleText, out var handle, out error))
        {
            return false;
        }

        NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
        if (!TrySetForegroundWindow(handle))
        {
            error = new ErrorResponse("focus_failed", "SetForegroundWindow failed. Windows may block focus changes from background processes.");
            return false;
        }

        return true;
    }

    private static bool TrySetForegroundWindow(IntPtr handle)
    {
        if (NativeMethods.SetForegroundWindow(handle))
        {
            return true;
        }

        var currentThread = NativeMethods.GetCurrentThreadId();
        var targetThread = NativeMethods.GetWindowThreadProcessId(handle, out _);
        var foreground = NativeMethods.GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero
            ? 0
            : NativeMethods.GetWindowThreadProcessId(foreground, out _);

        var attachedTarget = false;
        var attachedForeground = false;

        try
        {
            if (targetThread != 0 && targetThread != currentThread)
            {
                attachedTarget = NativeMethods.AttachThreadInput(currentThread, targetThread, true);
            }

            if (foregroundThread != 0 && foregroundThread != currentThread && foregroundThread != targetThread)
            {
                attachedForeground = NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
            }

            NativeMethods.BringWindowToTop(handle);
            NativeMethods.SetActiveWindow(handle);
            return NativeMethods.SetForegroundWindow(handle);
        }
        finally
        {
            if (attachedForeground)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }

            if (attachedTarget)
            {
                NativeMethods.AttachThreadInput(currentThread, targetThread, false);
            }
        }
    }

    public bool CloseWindow(string handleText, out ErrorResponse? error)
    {
        if (!TryResolveWindow(handleText, out var handle, out error))
        {
            return false;
        }

        NativeMethods.PostMessageW(handle, NativeMethods.WmClose, IntPtr.Zero, IntPtr.Zero);
        return true;
    }

    public bool MoveWindow(string handleText, MoveWindowRequest request, out ErrorResponse? error)
    {
        if (!TryResolveWindow(handleText, out var handle, out error))
        {
            return false;
        }

        if (request.Width <= 0 || request.Height <= 0)
        {
            error = new ErrorResponse("invalid_bounds", "Width and height must be positive.");
            return false;
        }

        if (!NativeMethods.MoveWindow(handle, request.X, request.Y, request.Width, request.Height, request.Repaint))
        {
            error = new ErrorResponse("move_failed", "MoveWindow failed.");
            return false;
        }

        return true;
    }

    public bool SetWindowState(string handleText, WindowStateRequest request, out ErrorResponse? error)
    {
        if (!TryResolveWindow(handleText, out var handle, out error))
        {
            return false;
        }

        var command = request.State.ToLowerInvariant() switch
        {
            "hide" => NativeMethods.SwHide,
            "show" => NativeMethods.SwShow,
            "minimize" => NativeMethods.SwMinimize,
            "maximize" => NativeMethods.SwMaximize,
            "restore" => NativeMethods.SwRestore,
            _ => -1
        };

        if (command < 0)
        {
            error = new ErrorResponse("invalid_state", "State must be one of hide, show, minimize, maximize, restore.");
            return false;
        }

        NativeMethods.ShowWindow(handle, command);
        return true;
    }
}

