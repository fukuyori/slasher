using System.Globalization;

namespace Slasher;

public static class WindowHandle
{
    public static IntPtr Parse(string value)
    {
        var text = value.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return new IntPtr(long.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        return new IntPtr(long.Parse(text, CultureInfo.InvariantCulture));
    }

    public static string Format(IntPtr handle)
    {
        return $"0x{handle.ToInt64():X}";
    }
}
