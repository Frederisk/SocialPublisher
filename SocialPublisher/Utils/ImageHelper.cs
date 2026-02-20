using SkiaSharp;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SocialPublisher.Utils;

public static class ImageHelper {
    public static Byte[] RotateImage(Byte[] inputBytes, Single degrees = 90) {
        using MemoryStream inputStream = new(inputBytes);
        //using SKBitmap originBitmap = SKBitmap.Decode(inputStream);
        using SKImage originImage = SKImage.FromEncodedData(inputStream);
        if (originImage is null) {
            return inputBytes;
        }
        using SKBitmap rotatedBitmap = new(originImage.Height, originImage.Width, originImage.ColorType, originImage.AlphaType, originImage.ColorSpace);
        using SKCanvas canvas = new(rotatedBitmap);
        canvas.Translate(rotatedBitmap.Width, 0);
        canvas.RotateDegrees(degrees);
        using SKPaint paint = new SKPaint { };
        // SKImage.FromBitmap(originBitmap)
        canvas.DrawImage(originImage, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), paint);
        using SKImage rotatedImage = SKImage.FromBitmap(rotatedBitmap);
        using var data = rotatedImage.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

}
