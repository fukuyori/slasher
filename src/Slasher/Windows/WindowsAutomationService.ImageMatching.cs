using Slasher.Api;

namespace Slasher.Windows;

public sealed partial class WindowsAutomationService
{
    private const long MaxImageMatchComparisons = 250_000_000;

    public bool MatchImage(ImageMatchRequest request, out ImageMatchResponse? response, out ErrorResponse? error)
    {
        response = null;
        error = null;

        if (string.IsNullOrWhiteSpace(request.TemplatePath))
        {
            error = new ErrorResponse("invalid_template_path", "TemplatePath is required.");
            return false;
        }

        if (!File.Exists(request.TemplatePath))
        {
            error = new ErrorResponse("template_not_found", $"Template image '{request.TemplatePath}' was not found.");
            return false;
        }

        var threshold = Math.Clamp(request.Threshold, 0.0, 1.0);
        var step = Math.Clamp(request.Step, 1, 64);
        if (!TryReadBmp(File.ReadAllBytes(request.TemplatePath), out var template, out error))
        {
            return false;
        }

        if (!TakeScreenshot(new ScreenshotRequest(request.Handle, MaxWidth: request.MaxWidth, MaxHeight: request.MaxHeight), out var screenshot, out error)
            || screenshot is null)
        {
            return false;
        }

        if (!TryReadBmp(Convert.FromBase64String(screenshot.Base64Image), out var screen, out error))
        {
            return false;
        }

        if (template.Width > screen.Width || template.Height > screen.Height)
        {
            response = new ImageMatchResponse(
                false,
                0,
                null,
                screen.Width,
                screen.Height,
                template.Width,
                template.Height,
                threshold);
            return true;
        }

        var positionsX = ((screen.Width - template.Width) / step) + 1;
        var positionsY = ((screen.Height - template.Height) / step) + 1;
        var comparisons = (long)positionsX * positionsY * template.Width * template.Height;
        if (comparisons > MaxImageMatchComparisons)
        {
            error = new ErrorResponse(
                "image_match_too_large",
                "Image match search is too large. Use a smaller screenshot, smaller template, or larger step.");
            return false;
        }

        var bestScore = -1.0;
        Rect? bestBounds = null;
        var found = false;
        for (var y = 0; y <= screen.Height - template.Height; y += step)
        {
            for (var x = 0; x <= screen.Width - template.Width; x += step)
            {
                var score = CompareAt(screen, template, x, y, threshold);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestBounds = new Rect(x, y, template.Width, template.Height);
                }

                if (score >= threshold)
                {
                    response = new ImageMatchResponse(
                        true,
                        score,
                        new Rect(x, y, template.Width, template.Height),
                        screen.Width,
                        screen.Height,
                        template.Width,
                        template.Height,
                        threshold);
                    return true;
                }
            }
        }

        response = new ImageMatchResponse(
            found,
            Math.Max(0, bestScore),
            bestBounds,
            screen.Width,
            screen.Height,
            template.Width,
            template.Height,
            threshold);
        return true;
    }

    private static double CompareAt(BmpImage screen, BmpImage template, int offsetX, int offsetY, double threshold)
    {
        var sum = 0L;
        var maxAllowed = (long)Math.Floor((1.0 - threshold) * template.Width * template.Height * 3 * 255);
        for (var y = 0; y < template.Height; y++)
        {
            var screenRow = ((offsetY + y) * screen.Width + offsetX) * 4;
            var templateRow = y * template.Width * 4;
            for (var x = 0; x < template.Width; x++)
            {
                var screenIndex = screenRow + x * 4;
                var templateIndex = templateRow + x * 4;
                sum += Math.Abs(screen.Pixels[screenIndex] - template.Pixels[templateIndex]);
                sum += Math.Abs(screen.Pixels[screenIndex + 1] - template.Pixels[templateIndex + 1]);
                sum += Math.Abs(screen.Pixels[screenIndex + 2] - template.Pixels[templateIndex + 2]);
                if (sum > maxAllowed)
                {
                    return 1.0 - (double)sum / (template.Width * template.Height * 3 * 255);
                }
            }
        }

        return 1.0 - (double)sum / (template.Width * template.Height * 3 * 255);
    }

    private static bool TryReadBmp(byte[] bytes, out BmpImage image, out ErrorResponse? error)
    {
        image = default;
        error = null;
        if (bytes.Length < 54 || bytes[0] != 'B' || bytes[1] != 'M')
        {
            error = new ErrorResponse("unsupported_image_format", "Only uncompressed BMP images are supported for image matching.");
            return false;
        }

        using var reader = new BinaryReader(new MemoryStream(bytes));
        reader.BaseStream.Position = 10;
        var pixelOffset = reader.ReadInt32();
        var dibSize = reader.ReadInt32();
        if (dibSize < 40)
        {
            error = new ErrorResponse("unsupported_bmp_header", "Only BITMAPINFOHEADER BMP images are supported.");
            return false;
        }

        var width = reader.ReadInt32();
        var rawHeight = reader.ReadInt32();
        _ = reader.ReadUInt16();
        var bitCount = reader.ReadUInt16();
        var compression = reader.ReadInt32();
        if (width <= 0 || rawHeight == 0 || compression != 0 || bitCount is not (24 or 32))
        {
            error = new ErrorResponse("unsupported_bmp_format", "Only uncompressed 24-bit or 32-bit BMP images are supported.");
            return false;
        }

        var topDown = rawHeight < 0;
        var height = Math.Abs(rawHeight);
        var sourceStride = ((width * bitCount + 31) / 32) * 4;
        if (pixelOffset < 0 || pixelOffset + sourceStride * height > bytes.Length)
        {
            error = new ErrorResponse("invalid_bmp_data", "BMP pixel data is incomplete.");
            return false;
        }

        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var sourceY = topDown ? y : height - 1 - y;
            var sourceRow = pixelOffset + sourceY * sourceStride;
            var destinationRow = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                var sourceIndex = sourceRow + x * (bitCount / 8);
                var destinationIndex = destinationRow + x * 4;
                pixels[destinationIndex] = bytes[sourceIndex];
                pixels[destinationIndex + 1] = bytes[sourceIndex + 1];
                pixels[destinationIndex + 2] = bytes[sourceIndex + 2];
                pixels[destinationIndex + 3] = bitCount == 32 ? bytes[sourceIndex + 3] : (byte)255;
            }
        }

        image = new BmpImage(width, height, pixels);
        return true;
    }

    private readonly record struct BmpImage(int Width, int Height, byte[] Pixels);
}
