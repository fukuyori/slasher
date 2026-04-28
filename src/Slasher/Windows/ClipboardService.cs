using System.Runtime.InteropServices;
using System.Text;

namespace Slasher.Windows;

public sealed class ClipboardService
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;
    private const int OpenClipboardAttempts = 10;
    private const int OpenClipboardRetryDelayMs = 40;

    public void SetText(string text)
    {
        OpenClipboardWithRetry();

        try
        {
            NativeMethods.EmptyClipboard();
            var bytes = Encoding.Unicode.GetBytes(text + '\0');
            var handle = NativeMethods.GlobalAlloc(GmemMoveable, (UIntPtr)bytes.Length);
            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("GlobalAlloc failed.");
            }

            var pointer = NativeMethods.GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                NativeMethods.GlobalFree(handle);
                throw new InvalidOperationException("GlobalLock failed.");
            }

            try
            {
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }

            if (NativeMethods.SetClipboardData(CfUnicodeText, handle) == IntPtr.Zero)
            {
                NativeMethods.GlobalFree(handle);
                throw new InvalidOperationException("SetClipboardData failed.");
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    public string GetText()
    {
        OpenClipboardWithRetry();

        try
        {
            var handle = NativeMethods.GetClipboardData(CfUnicodeText);
            if (handle == IntPtr.Zero)
            {
                return string.Empty;
            }

            var pointer = NativeMethods.GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                return Marshal.PtrToStringUni(pointer) ?? string.Empty;
            }
            finally
            {
                NativeMethods.GlobalUnlock(handle);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    public void Clear()
    {
        OpenClipboardWithRetry();

        try
        {
            NativeMethods.EmptyClipboard();
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    private static void OpenClipboardWithRetry()
    {
        for (var attempt = 1; attempt <= OpenClipboardAttempts; attempt++)
        {
            if (NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                return;
            }

            if (attempt < OpenClipboardAttempts)
            {
                Thread.Sleep(OpenClipboardRetryDelayMs);
            }
        }

        throw new InvalidOperationException("OpenClipboard failed.");
    }
}
