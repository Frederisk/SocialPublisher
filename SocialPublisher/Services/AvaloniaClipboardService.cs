using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

using SocialPublisher.Utils;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SocialPublisher.Services;

public interface IClipboardService {
    public IAsyncEnumerable<Byte[]> GetImagesFromClipboardAsync();

    public Task<String?> GetTextFromClipboardAsync();
}

public class AvaloniaClipboardService : IClipboardService {
    public async IAsyncEnumerable<Byte[]> GetImagesFromClipboardAsync() {
        TopLevel? topLevel = TopLevelHelper.GetTopLevel();

        if (topLevel?.Clipboard is not { } clipboard) {
            //return images;
            yield break;
        }

        var files = await clipboard.TryGetFilesAsync();
        if (files is not null && files.Length > 0) {
            foreach (var file in files) {
                String path = file.Path.LocalPath;
                if (IsImageFile(path)) {
                    yield return await File.ReadAllBytesAsync(path);
                }
            }
            yield break;
        }

        var format = await clipboard.GetDataFormatsAsync();
        if (format.Any(t => t.Identifier is "PNG" or "Bitmap")) {
            var data = await clipboard.TryGetDataAsync();
            foreach (var item in data!.Items.Where(i => i.Formats.Any(f => f.Identifier is "PNG" or "Bitmap"))) {
                var a = await item.TryGetRawAsync(DataFormat.Bitmap);
                if (a is Bitmap bitmap) {
                    using MemoryStream stream = new();
                    bitmap.Save(stream);
                    yield return stream.ToArray();
                } else if (a is Byte[] bytes) {
                    yield return bytes;
                }
            }
            yield break;
        }
        yield break;
    }

    private static Boolean IsImageFile(String path) {
        String extension = Path.GetExtension(path).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".webp" or ".jxl";
    }

    public async Task<String?> GetTextFromClipboardAsync() {
        var topLevel = TopLevelHelper.GetTopLevel();
        if (topLevel?.Clipboard is not { } clipboard) {
            return null;
        }

        var text = await clipboard.TryGetTextAsync();
        return text;
    }
}
