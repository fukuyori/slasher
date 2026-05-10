using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Slasher.Api;

namespace Slasher.Windows;

public sealed partial class WindowsAutomationService
{
    public IReadOnlyList<ScreenInfo> ListScreens()
    {
        var screens = new List<ScreenInfo>();
        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr hdc, ref NativeRect monitorRect, IntPtr data) =>
        {
            var info = new MonitorInfo
            {
                Size = Marshal.SizeOf<MonitorInfo>()
            };

            if (NativeMethods.GetMonitorInfo(monitor, ref info))
            {
                screens.Add(new ScreenInfo(
                    screens.Count,
                    string.IsNullOrWhiteSpace(info.DeviceName) ? $"Screen {screens.Count}" : info.DeviceName,
                    ToRect(info.Monitor),
                    ToRect(info.WorkArea),
                    (info.Flags & 1) == 1));
            }

            return true;
        }, IntPtr.Zero);

        if (screens.Count == 0)
        {
            screens.Add(new ScreenInfo(
                0,
                "VirtualScreen",
                new Rect(
                    NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen),
                    NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen),
                    NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen),
                    NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen)),
                new Rect(
                    NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen),
                    NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen),
                    NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen),
                    NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen)),
                true));
        }

        return screens;
    }

    public bool TakeScreenshot(ScreenshotRequest request, out ScreenshotResponse? screenshot, out ErrorResponse? error)
    {
        screenshot = null;
        error = null;

        int x;
        int y;
        int width;
        int height;
        if (request.Bounds is not null)
        {
            x = request.Bounds.X;
            y = request.Bounds.Y;
            width = request.Bounds.Width;
            height = request.Bounds.Height;
        }
        else if (request.ScreenIndex is not null)
        {
            var screens = ListScreens();
            var screen = screens.FirstOrDefault(item => item.Index == request.ScreenIndex.Value);
            if (screen is null)
            {
                error = new ErrorResponse("screen_not_found", $"Screen index '{request.ScreenIndex.Value}' was not found.");
                return false;
            }

            x = screen.Bounds.X;
            y = screen.Bounds.Y;
            width = screen.Bounds.Width;
            height = screen.Bounds.Height;
        }
        else if (string.IsNullOrWhiteSpace(request.Handle))
        {
            x = NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen);
            y = NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen);
            width = NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen);
            height = NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen);
        }
        else
        {
            if (!TryResolveWindow(request.Handle, out var handle, out error))
            {
                return false;
            }

            if (!NativeMethods.GetWindowRect(handle, out var rect))
            {
                error = new ErrorResponse("window_rect_failed", "GetWindowRect failed.");
                return false;
            }

            x = rect.Left;
            y = rect.Top;
            width = rect.Right - rect.Left;
            height = rect.Bottom - rect.Top;
        }

        if (width <= 0 || height <= 0)
        {
            error = new ErrorResponse("empty_screenshot_bounds", "Screenshot bounds are empty.");
            return false;
        }

        var (outputWidth, outputHeight) = FitSize(width, height, request.MaxWidth, request.MaxHeight);
        if (!TryCaptureBmp(x, y, width, height, outputWidth, outputHeight, out var bytes, out error))
        {
            return false;
        }

        screenshot = new ScreenshotResponse("image/bmp", Convert.ToBase64String(bytes), outputWidth, outputHeight);
        return true;
    }

    private static Rect ToRect(NativeRect rect)
    {
        return new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }
}

