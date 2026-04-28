using System.Runtime.InteropServices;

namespace Slasher.Windows;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct NativeRect
{
    public readonly int Left;
    public readonly int Top;
    public readonly int Right;
    public readonly int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BitmapInfo
{
    public BitmapInfoHeader Header;
    public uint Colors;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BitmapInfoHeader
{
    public uint Size;
    public int Width;
    public int Height;
    public ushort Planes;
    public ushort BitCount;
    public uint Compression;
    public uint SizeImage;
    public int XPelsPerMeter;
    public int YPelsPerMeter;
    public uint ClrUsed;
    public uint ClrImportant;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Input
{
    public uint Type;
    public InputUnion U;
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)]
    public MouseInput Mouse;

    [FieldOffset(0)]
    public KeyboardInput Keyboard;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MouseInput
{
    public int Dx;
    public int Dy;
    public uint MouseData;
    public uint DwFlags;
    public uint Time;
    public IntPtr DwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KeyboardInput
{
    public ushort Vk;
    public ushort Scan;
    public uint DwFlags;
    public uint Time;
    public IntPtr DwExtraInfo;
}

