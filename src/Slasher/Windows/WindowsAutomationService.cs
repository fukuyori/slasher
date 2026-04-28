using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Slasher.Api;

namespace Slasher.Windows;

public sealed partial class WindowsAutomationService
{
    private static T SafeRead<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (InvalidOperationException)
        {
            return fallback;
        }
    }
}

