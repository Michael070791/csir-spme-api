using Csir.Spme.Application.Common.Interfaces;
using SkiaSharp;

namespace Csir.Spme.Infrastructure.Storage;

public sealed class ProfileImageProcessor : IProfileImageProcessor
{
    private const int MaximumDimension = 8_000;
    private const long MaximumPixels = 25_000_000;
    private const int OutputDimension = 512;
    private const int MaximumOutputBytes = 256 * 1024;

    public Task<ProfileImageProcessingResult> ProcessAsync(Stream content, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        using var managedStream = new SKManagedStream(content, false);
        using var codec = SKCodec.Create(managedStream)
            ?? throw new InvalidDataException("The uploaded file is not a supported image.");
        var sourceContentType = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Webp => "image/webp",
            SKEncodedImageFormat.Gif => "image/gif",
            _ => throw new InvalidDataException("Profile image must be a JPEG, PNG, WebP, or GIF file.")
        };

        var sourceInfo = codec.Info;
        if (sourceInfo.Width <= 0 || sourceInfo.Height <= 0 ||
            sourceInfo.Width > MaximumDimension || sourceInfo.Height > MaximumDimension ||
            (long)sourceInfo.Width * sourceInfo.Height > MaximumPixels)
        {
            throw new InvalidDataException("Profile image dimensions exceed the supported limit.");
        }

        using var decoded = new SKBitmap(new SKImageInfo(
            sourceInfo.Width,
            sourceInfo.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul));
        var decodeResult = codec.GetPixels(decoded.Info, decoded.GetPixels());
        if (decodeResult is not SKCodecResult.Success and not SKCodecResult.IncompleteInput)
            throw new InvalidDataException("The uploaded image could not be decoded safely.");

        using var oriented = ApplyOrientation(decoded, codec.EncodedOrigin);
        var scale = Math.Min(1d, (double)OutputDimension / Math.Max(oriented.Width, oriented.Height));
        var outputWidth = Math.Max(1, (int)Math.Round(oriented.Width * scale));
        var outputHeight = Math.Max(1, (int)Math.Round(oriented.Height * scale));
        using var resized = new SKBitmap(new SKImageInfo(outputWidth, outputHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(resized))
        using (var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true })
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(oriented, new SKRect(0, 0, outputWidth, outputHeight), paint);
            canvas.Flush();
        }

        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, 80)
            ?? throw new InvalidDataException("The profile image could not be encoded.");
        if (encoded.Size > MaximumOutputBytes)
            throw new InvalidDataException("The normalized profile image exceeds 256 KiB.");

        var output = new MemoryStream(encoded.ToArray(), writable: false);
        return Task.FromResult(new ProfileImageProcessingResult(output, "image/webp", output.Length, sourceContentType));
    }

    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        var swapsDimensions = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or
            SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var output = new SKBitmap(
            swapsDimensions ? source.Height : source.Width,
            swapsDimensions ? source.Width : source.Height,
            source.ColorType,
            source.AlphaType);

        using var canvas = new SKCanvas(output);
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(source.Width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(source.Width, source.Height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, source.Height);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(source.Height, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(source.Height, source.Width);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, source.Width);
                canvas.RotateDegrees(270);
                break;
        }

        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();
        return output;
    }
}
