using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SocialPublisher.Services;

public interface IClipboardService {
    public Task<List<Byte[]>> GetImagesFromClipboardAsync();
}

public class AvaloniaClipboardService : IClipboardService {
    public async Task<List<Byte[]>> GetImagesFromClipboardAsync() {
        List<Byte[]> images = [];
        TopLevel? topLevel = GetTopLevel();

        if (topLevel?.Clipboard is not { } clipboard) {
            return images;
        }

        try {
            var files = await clipboard.TryGetFilesAsync();
            if (files is not null && files.Length > 0) {
                foreach (var file in files) {
                    String path = file.Path.LocalPath;
                    if (IsImageFile(path)) {
                        var bytes = await File.ReadAllBytesAsync(path);
                        images.Add(bytes);
                    }
                }
                return images;
            }

            var format = await clipboard.GetDataFormatsAsync();
            if (format.Any(t => t.Identifier is "PNG" or "Bitmap")) {
                var data = await clipboard.TryGetDataAsync();
                foreach (var item in data!.Items.Where(i => i.Formats.Any(f => f.Identifier is "PNG" or "Bitmap"))) {
                    var a = await item.TryGetRawAsync(DataFormat.Bitmap);
                    if (a is Bitmap bitmap) {
                        using MemoryStream stream = new();
                        bitmap.Save(stream);
                        images.Add(stream.ToArray());
                    } else if (a is Byte[] bytes) {
                        images.Add(bytes);
                    }
                }
                return images;
            }

        } catch {
            // ignore
        }
        return images;
    }

    private static TopLevel? GetTopLevel() {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            return TopLevel.GetTopLevel(desktop.MainWindow);
        } else if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleView) {
            return TopLevel.GetTopLevel(singleView.MainView);
        }
        return null;
    }

    private static Boolean IsImageFile(String path) {
        String extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif";
    }
}
