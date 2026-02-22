using SkiaSharp;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SocialPublisher.Utils;

public static class ImageHelper {

    public static async Task<Byte[]> RotateImageAsync(Byte[] inputBytes, Single degrees = 90, CancellationToken token = default) {
        return await Task.Run(() => RotateImage(inputBytes, degrees), token);
    }

    public static Byte[] RotateImage(Byte[] inputBytes, Single degrees = 90) {
        using MemoryStream inputStream = new(inputBytes);
        //using SKBitmap originBitmap = SKBitmap.Decode(inputStream);
        using SKImage? originImage = SKImage.FromEncodedData(inputStream);
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

    public static async Task<Byte[]> ProcessAndCompressImageAsync(Byte[] inputBytes, Int32 maxDimensionSum = 10000, Int64 maxFileSizeBytes = 2 * 1024 * 1024 /* 2MiB */, CancellationToken token = default) {
        return await Task.Run(() => ProcessAndCompressImage(inputBytes, maxDimensionSum, maxFileSizeBytes, token), token);
    }

    public static Byte[] ProcessAndCompressImage(Byte[] inputBytes, Int32 maxDimensionSum = 10000, Int64 maxFileSizeBytes = 2 * 1024 * 1024 /* 2MiB */, CancellationToken token = default) {
        using SKBitmap? originalBitmap = SKBitmap.Decode(inputBytes);
        if (originalBitmap is null) {
            return inputBytes;
        }

        Int32 width = originalBitmap.Width;
        Int32 height = originalBitmap.Height;
        SKBitmap bitmapToProcess = originalBitmap;
        Boolean isResized = false;
        try {
            if (width + height > maxDimensionSum) {
                token.ThrowIfCancellationRequested();

                Double scale = (Double)maxDimensionSum / (width + height);
                Int32 newWidth = Math.Max(1, (Int32)(width * scale));
                Int32 newHeight = Math.Max(1, (Int32)(height * scale));
                bitmapToProcess = new SKBitmap(newWidth, newHeight);
                originalBitmap.ScalePixels(bitmapToProcess, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
                isResized = true;
            }
            using SKImage image = SKImage.FromBitmap(bitmapToProcess);
            SKData initialData = image.Encode(SKEncodedImageFormat.Jpeg, 100);
            if (initialData.Size <= maxFileSizeBytes) {
                return initialData.ToArray();
            }
            initialData.Dispose();

            // Binary search for the best quality that meets the file size requirement
            Int32 minQ = 1;
            Int32 maxQ = 99;
            SKData? bestData = null;

            while (minQ <= maxQ) {
                token.ThrowIfCancellationRequested();

                //token.ThrowIfCancellationRequested();

                Int32 midQ = minQ + (maxQ - minQ) / 2;
                var data = image.Encode(SKEncodedImageFormat.Jpeg, midQ);

                if (data.Size <= maxFileSizeBytes) {
                    bestData?.Dispose();
                    bestData = data;
                    minQ = midQ + 1;
                } else {
                    data.Dispose();
                    maxQ = midQ - 1;
                }
            }
            // If we couldn't find any quality that meets the requirement, use the lowest quality
            bestData ??= image.Encode(SKEncodedImageFormat.Jpeg, 1);
            return bestData.ToArray();
        } finally {
            // Dispose the resized bitmap if it was created
            if (isResized) {
                bitmapToProcess.Dispose();
            }
        }
    }

}
