using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Slasher.Api;

namespace Slasher.Windows;

public sealed partial class WindowsAutomationService
{
    private static void SendVirtualKey(ushort key, bool keyUp)
    {
        var input = new Input
        {
            Type = NativeMethods.InputKeyboard,
            U = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    Vk = key,
                    DwFlags = keyUp ? NativeMethods.KeyEventFKeyUp : 0
                }
            }
        };

        NativeMethods.SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private static void SendUnicodeChar(char ch, bool keyUp)
    {
        var input = new Input
        {
            Type = NativeMethods.InputKeyboard,
            U = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    Scan = ch,
                    DwFlags = NativeMethods.KeyEventFUnicode | (keyUp ? NativeMethods.KeyEventFKeyUp : 0)
                }
            }
        };

        NativeMethods.SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private static bool SendMouseButton(string button, bool down, out ErrorResponse? error)
    {
        var flags = (button, down) switch
        {
            ("left", true) => NativeMethods.MouseEventFLeftDown,
            ("left", false) => NativeMethods.MouseEventFLeftUp,
            ("right", true) => NativeMethods.MouseEventFRightDown,
            ("right", false) => NativeMethods.MouseEventFRightUp,
            ("middle", true) => NativeMethods.MouseEventFMiddleDown,
            ("middle", false) => NativeMethods.MouseEventFMiddleUp,
            _ => 0u
        };

        return flags == 0
            ? Fail("invalid_mouse_button", "Button must be one of left, right, middle.", out error)
            : SendMouseInput(flags, 0, out error);
    }

    private static bool Pause(int milliseconds)
    {
        Thread.Sleep(milliseconds);
        return true;
    }

    private static bool SendMouseInput(uint flags, uint mouseData, out ErrorResponse? error)
    {
        var input = new Input
        {
            Type = NativeMethods.InputMouse,
            U = new InputUnion
            {
                Mouse = new MouseInput
                {
                    DwFlags = flags,
                    MouseData = mouseData
                }
            }
        };

        if (NativeMethods.SendInput(1, [input], Marshal.SizeOf<Input>()) != 1)
        {
            error = new ErrorResponse("send_input_failed", "SendInput failed.");
            return false;
        }

        error = null;
        return true;
    }

    private static bool Fail(string code, string message, out ErrorResponse? error)
    {
        error = new ErrorResponse(code, message);
        return false;
    }

    private static bool TryVirtualKey(string token, out ushort key)
    {
        key = token.ToUpperInvariant() switch
        {
            "BACKSPACE" or "BACK" => 0x08,
            "TAB" => 0x09,
            "ENTER" or "RETURN" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "SPACE" => 0x20,
            "PAGEUP" => 0x21,
            "PAGEDOWN" => 0x22,
            "END" => 0x23,
            "HOME" => 0x24,
            "LEFT" => 0x25,
            "UP" => 0x26,
            "RIGHT" => 0x27,
            "DOWN" => 0x28,
            "INSERT" => 0x2D,
            "DELETE" => 0x2E,
            "CTRL" or "CONTROL" => 0x11,
            "SHIFT" => 0x10,
            "ALT" or "MENU" => 0x12,
            "WIN" or "LWIN" => 0x5B,
            _ when token.Length == 1 && char.IsLetterOrDigit(token[0]) => (ushort)char.ToUpperInvariant(token[0]),
            _ when token.StartsWith('F') && ushort.TryParse(token[1..], out var n) && n is >= 1 and <= 24 => (ushort)(0x70 + n - 1),
            _ => 0
        };

        return key != 0;
    }
}

