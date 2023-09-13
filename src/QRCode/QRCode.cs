using QRCode.Abstracts;
using SkiaSharp;

namespace QRCode
{
    public class QRCode : AbstractQRCode
    {
        public QRCode(QRCodeData data) : base(data)
        {
        }

        public SKBitmap GetGraphic(int pixelsPerModule)
        {
            return GetGraphic(pixelsPerModule, SKColors.Black, SKColors.White, true);
        }

        private SKBitmap GetGraphic(int pixelsPerModule, SKColor darkColor, SKColor lightColor,
            bool drawQuietZones = true)
        {
            var size = (QrCodeData.ModuleMatrix.Count - (drawQuietZones ? 0 : 8)) * pixelsPerModule;
            var offset = drawQuietZones ? 0 : 4 * pixelsPerModule;

            var bmp = new SKBitmap(size, size);
            using var canvas = new SKCanvas(bmp);

            var lightPaint = new SKPaint { Color = lightColor };
            var darkPaint = new SKPaint { Color = darkColor };

            for (var x = 0; x < size + offset; x = x + pixelsPerModule)
            {
                for (var y = 0; y < size + offset; y = y + pixelsPerModule)
                {
                    var module =
                        QrCodeData.ModuleMatrix[(y + pixelsPerModule) / pixelsPerModule - 1][
                            (x + pixelsPerModule) / pixelsPerModule - 1];

                    canvas.DrawRect(
                        new SKRect(x - offset, y - offset, x + pixelsPerModule - offset,
                            y + pixelsPerModule - offset), module ? darkPaint : lightPaint);
                }
            }

            return bmp;
        }
    }
}