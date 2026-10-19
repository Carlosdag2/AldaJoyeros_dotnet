using SkiaSharp;

namespace AldaJoyeros.Services;

public static class PublicImageEncoder
{
    public static byte[]? Encode(byte[] data)
    {
        if (data.Length == 0 || data.Length > 20 * 1024 * 1024) return null;
        using var stream = new SKMemoryStream(data);
        using var codec = SKCodec.Create(stream);
        if (codec == null || codec.Info.Width <= 0 || codec.Info.Height <= 0 || (long)codec.Info.Width * codec.Info.Height > 25000000) return null;
        using var decoded = SKBitmap.Decode(codec);
        if (decoded == null) return null;
        var rotated = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        using var oriented = new SKBitmap(rotated ? decoded.Height : decoded.Width, rotated ? decoded.Width : decoded.Height);
        using (var canvas = new SKCanvas(oriented))
        {
            var w = decoded.Width; var h = decoded.Height;
            var matrix = codec.EncodedOrigin switch
            {
                SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
                SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
                SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
                SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
                SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
                SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
                _ => SKMatrix.Identity
            };
            canvas.SetMatrix(matrix);
            canvas.DrawBitmap(decoded, 0, 0);
        }
        var scale = Math.Min(1d, 800d / Math.Max(oriented.Width, oriented.Height));
        using var resized = oriented.Resize(new SKImageInfo(Math.Max(1, (int)(oriented.Width * scale)), Math.Max(1, (int)(oriented.Height * scale))), new SKSamplingOptions(SKFilterMode.Linear));
        if (resized == null) return null;
        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, 82);
        return encoded?.ToArray();
    }
}
