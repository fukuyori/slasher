using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Slasher.Api;

namespace Slasher.Windows;

public sealed partial class WindowsAutomationService
{
    public bool TakeScreenshot(ScreenshotRequest request, out ScreenshotResponse? screenshot, out ErrorResponse? error)
    {
        screenshot = null;
        error = null;

        int x;
        int y;
        int width;
        int height;
        if (string.IsNullOrWhiteSpace(request.Handle))
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
}

