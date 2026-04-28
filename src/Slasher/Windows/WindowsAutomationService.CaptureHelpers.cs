using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Slasher.Api;

namespace Slasher.Windows;

public sealed partial class WindowsAutomationService
{
    private static (int Width, int Height) FitSize(int width, int height, int? maxWidth, int? maxHeight)
    {
        var targetWidth = maxWidth.GetValueOrDefault(width);
        var targetHeight = maxHeight.GetValueOrDefault(height);
        if (targetWidth <= 0 || targetHeight <= 0 || (width <= targetWidth && height <= targetHeight))
        {
            return (width, height);
        }

        var scale = Math.Min((double)targetWidth / width, (double)targetHeight / height);
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static bool TryCaptureBmp(
        int x,
        int y,
        int sourceWidth,
        int sourceHeight,
        int outputWidth,
        int outputHeight,
        out byte[] bytes,
        out ErrorResponse? error)
    {
        bytes = [];
        error = null;

        var screenDc = NativeMethods.GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero)
        {
            error = new ErrorResponse("screen_dc_failed", "GetDC failed.");
            return false;
        }

        var memoryDc = IntPtr.Zero;
        var bitmap = IntPtr.Zero;
        var oldObject = IntPtr.Zero;

        try
        {
            memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
            bitmap = NativeMethods.CreateCompatibleBitmap(screenDc, outputWidth, outputHeight);
            if (memoryDc == IntPtr.Zero || bitmap == IntPtr.Zero)
            {
                error = new ErrorResponse("bitmap_create_failed", "Failed to create a compatible bitmap.");
                return false;
            }

            oldObject = NativeMethods.SelectObject(memoryDc, bitmap);
            var copied = sourceWidth == outputWidth && sourceHeight == outputHeight
                ? NativeMethods.BitBlt(memoryDc, 0, 0, outputWidth, outputHeight, screenDc, x, y, NativeMethods.Srccopy)
                : TryStretchBlt(screenDc, memoryDc, x, y, sourceWidth, sourceHeight, outputWidth, outputHeight);
            if (!copied)
            {
                error = new ErrorResponse("bitblt_failed", "BitBlt failed.");
                return false;
            }

            var stride = ((outputWidth * 32 + 31) / 32) * 4;
            var imageSize = stride * outputHeight;
            var bitmapInfo = new BitmapInfo
            {
                Header = new BitmapInfoHeader
                {
                    Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                    Width = outputWidth,
                    Height = -outputHeight,
                    Planes = 1,
                    BitCount = 32,
                    Compression = 0,
                    SizeImage = (uint)imageSize
                }
            };

            var pixels = new byte[imageSize];
            if (NativeMethods.GetDIBits(memoryDc, bitmap, 0, (uint)outputHeight, pixels, ref bitmapInfo, NativeMethods.DibRgbColors) == 0)
            {
                error = new ErrorResponse("getdibits_failed", "GetDIBits failed.");
                return false;
            }

            bytes = BuildBmpFile(outputWidth, outputHeight, pixels);
            return true;
        }
        finally
        {
            if (oldObject != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memoryDc, oldObject);
            }

            if (bitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(bitmap);
            }

            if (memoryDc != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(memoryDc);
            }

            NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static bool TryStretchBlt(
        IntPtr sourceDc,
        IntPtr destinationDc,
        int x,
        int y,
        int sourceWidth,
        int sourceHeight,
        int outputWidth,
        int outputHeight)
    {
        NativeMethods.SetStretchBltMode(destinationDc, NativeMethods.StretchHalftone);
        return NativeMethods.StretchBlt(
            destinationDc,
            0,
            0,
            outputWidth,
            outputHeight,
            sourceDc,
            x,
            y,
            sourceWidth,
            sourceHeight,
            NativeMethods.Srccopy);
    }

    private static byte[] BuildBmpFile(int width, int height, byte[] bgraPixels)
    {
        const int fileHeaderSize = 14;
        var dibHeaderSize = Marshal.SizeOf<BitmapInfoHeader>();
        var pixelOffset = fileHeaderSize + dibHeaderSize;
        var fileSize = pixelOffset + bgraPixels.Length;

        using var stream = new MemoryStream(fileSize);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write((byte)'B');
        writer.Write((byte)'M');
        writer.Write(fileSize);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(pixelOffset);

        writer.Write(dibHeaderSize);
        writer.Write(width);
        writer.Write(-height);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(0);
        writer.Write(bgraPixels.Length);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0);
        writer.Write(bgraPixels);

        return stream.ToArray();
    }
}

