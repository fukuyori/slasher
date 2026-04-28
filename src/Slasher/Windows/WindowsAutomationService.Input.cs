using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Slasher.Api;

namespace Slasher.Windows;

public sealed partial class WindowsAutomationService
{
    public bool SendKeys(KeyInputRequest request, out ErrorResponse? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(request.Keys))
        {
            error = new ErrorResponse("invalid_keys", "Keys is required.");
            return false;
        }

        foreach (var token in request.Keys.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryVirtualKey(token, out var key))
            {
                error = new ErrorResponse("unknown_key", $"Unknown key '{token}'.");
                return false;
            }

            SendVirtualKey(key, keyUp: false);
            if (request.DelayMs > 0)
            {
                Thread.Sleep(request.DelayMs);
            }
        }

        foreach (var token in request.Keys.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Reverse())
        {
            TryVirtualKey(token, out var key);
            SendVirtualKey(key, keyUp: true);
            if (request.DelayMs > 0)
            {
                Thread.Sleep(request.DelayMs);
            }
        }

        return true;
    }

    public bool SendText(TextInputRequest request, out ErrorResponse? error)
    {
        error = null;
        foreach (var ch in request.Text)
        {
            SendUnicodeChar(ch, keyUp: false);
            SendUnicodeChar(ch, keyUp: true);
            if (request.DelayMs > 0)
            {
                Thread.Sleep(request.DelayMs);
            }
        }

        return true;
    }

    public bool SendMouse(MouseInputRequest request, out ErrorResponse? error)
    {
        error = null;
        if (request.X is not null && request.Y is not null)
        {
            NativeMethods.SetCursorPos(request.X.Value, request.Y.Value);
        }

        var action = request.Action.ToLowerInvariant();
        var button = request.Button.ToLowerInvariant();

        return action switch
        {
            "move" => true,
            "click" => SendMouseButton(button, down: true, out error) && SendMouseButton(button, down: false, out error),
            "doubleclick" => SendMouseButton(button, down: true, out error) && SendMouseButton(button, down: false, out error)
                && Pause(80)
                && SendMouseButton(button, down: true, out error) && SendMouseButton(button, down: false, out error),
            "down" => SendMouseButton(button, down: true, out error),
            "up" => SendMouseButton(button, down: false, out error),
            "wheel" => SendMouseInput(NativeMethods.MouseEventFWheel, (uint)request.WheelDelta, out error),
            _ => Fail("invalid_mouse_action", "Action must be one of move, click, doubleclick, down, up, wheel.", out error)
        };
    }

    public bool DragMouse(MouseDragRequest request, out ErrorResponse? error)
    {
        error = null;
        if (request.Steps <= 0)
        {
            error = new ErrorResponse("invalid_drag_steps", "Steps must be positive.");
            return false;
        }

        if (request.DurationMs < 0)
        {
            error = new ErrorResponse("invalid_drag_duration", "DurationMs must be zero or positive.");
            return false;
        }

        var button = request.Button.ToLowerInvariant();
        NativeMethods.SetCursorPos(request.FromX, request.FromY);
        Thread.Sleep(40);
        if (!SendMouseButton(button, down: true, out error))
        {
            return false;
        }

        var delay = request.Steps == 0 ? 0 : request.DurationMs / request.Steps;
        for (var step = 1; step <= request.Steps; step++)
        {
            var t = (double)step / request.Steps;
            var x = (int)Math.Round(request.FromX + (request.ToX - request.FromX) * t);
            var y = (int)Math.Round(request.FromY + (request.ToY - request.FromY) * t);
            NativeMethods.SetCursorPos(x, y);
            if (delay > 0)
            {
                Thread.Sleep(delay);
            }
        }

        Thread.Sleep(40);
        return SendMouseButton(button, down: false, out error);
    }

    public bool GetContextMenu(ContextMenuRequest request, out ContextMenuResponse? response, out ErrorResponse? error)
    {
        response = null;
        error = null;

        if (!SendMouse(new MouseInputRequest("click", request.X, request.Y, "right"), out error))
        {
            return false;
        }

        Thread.Sleep(Math.Max(0, request.DelayMs));
        TryGetForegroundWindow(out var foreground);
        if (!TakeScreenshot(new ScreenshotRequest(), out var screenshot, out error) || screenshot is null)
        {
            return false;
        }

        response = new ContextMenuResponse(
            request.X,
            request.Y,
            foreground,
            screenshot,
            "Opened the context menu with a secondary click and captured the full desktop for visual inspection. Menu item text extraction is not implemented yet.");
        return true;
    }
}

