using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Slasher.Api;

namespace Slasher.Windows;

public sealed partial class WindowsAutomationService
{
    public StartAppResponse StartApp(StartAppRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ArgumentException("FileName is required.", nameof(request));
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            Arguments = request.Arguments ?? string.Empty,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty,
            UseShellExecute = request.UseShellExecute
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start '{request.FileName}'.");

        try
        {
            process.WaitForInputIdle(3000);
        }
        catch (InvalidOperationException)
        {
        }

        try
        {
            process.Refresh();
        }
        catch (InvalidOperationException)
        {
        }

        var processName = SafeRead(() => process.ProcessName, request.FileName);
        var mainWindowHandle = SafeRead(() => process.MainWindowHandle, IntPtr.Zero);
        var mainWindowTitle = SafeRead(() => process.MainWindowTitle, string.Empty);

        return new StartAppResponse(
            process.Id,
            processName,
            mainWindowHandle == IntPtr.Zero ? null : WindowHandle.Format(mainWindowHandle),
            string.IsNullOrWhiteSpace(mainWindowTitle) ? null : mainWindowTitle);
    }
}

