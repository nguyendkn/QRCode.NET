using SkiaSharp;

namespace QRCode.Extensions;

public static class SkiaSharpExtensions
{
    public static void SaveToImage(this SKBitmap bitmap, string filePath)
    {
        using var stream = new SKFileWStream(filePath);
        var pixmap = new SKPixmap(new SKImageInfo(bitmap.Width, bitmap.Height), bitmap.GetPixels());
        pixmap.Encode(stream, SKEncodedImageFormat.Png, 100); // 100 means lossless for PNG
    }

    public static SKBitmap LoadImage(this string path)
    {
        return SKBitmap.Decode(path);
    }
}