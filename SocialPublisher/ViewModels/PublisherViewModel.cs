using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using SocialPublisher.Models;

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SocialPublisher.ViewModels;

public partial class PublisherViewModel : ViewModelBase {
    public ObservableCollection<PostImage> Images { get; } = [];

    [ObservableProperty]
    private String _caption = String.Empty;

    [ObservableProperty]
    private String _statusMessage = "Ready to Paste.";

    [ObservableProperty]
    private Boolean _isBusy = false;

    private const String Telegram_Token = "token";
    private const Int64 Chat_ID = -1;
    private const String Mastodon_Instance = "https://instance.url";
    private const String Mastodon_Token = "token";

    public TopLevel? TopLevelContext { get; set; }

    [RelayCommand]
    public async Task Paste() {
        if (this.TopLevelContext?.Clipboard is not { } clipboard) {
            this.StatusMessage = "Clipboard not available.";
            return;
        }

        try {
            var files = await clipboard.TryGetFilesAsync();
            if (files is not null && files.Length > 0) {
                foreach (var file in files) {
                    String path = file.Path.LocalPath;
                    if (IsImageFile(path)) {
                        var bytes = await File.ReadAllBytesAsync(path);
                        this.Images.Add(new PostImage(bytes));
                    }
                }
                this.StatusMessage = $"Pasted {this.Images.Count} image(s) from clipboard.";
                return;
            }

            var format = await clipboard.GetDataFormatsAsync();
            if (format.Any(t => t.Identifier is "PNG" or "Bitmap")) {
                var data = await clipboard.TryGetDataAsync();
                foreach (var item in data!.Items.Where(i => i.Formats.Any(f => f.Identifier is "PNG" or "Bitmap"))) {
                    var a = await item.TryGetRawAsync(DataFormat.Bitmap);
                    if (a is Bitmap bitmap) {
                        this.Images.Add(new PostImage(bitmap));
                    } else if (a is Byte[] bytes) {
                        this.Images.Add(new PostImage(bytes));
                    }
                }
                this.StatusMessage = $"Pasted {this.Images.Count} image(s) from clipboard.";
                return;
            }

            this.StatusMessage = "No image data found in clipboard.";
        } catch (Exception ex) {
            this.StatusMessage = $"Error pasting from clipboard: {ex.Message}";
        }
    }

    private static Boolean IsImageFile(String path) {
        String extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif";
    }

    [RelayCommand]
    public void Clear() {
        foreach (var image in this.Images) {
            image.Dispose();
        }
        this.Images.Clear();
        this.Caption = String.Empty;
        this.StatusMessage = "Cleared.";
    }

    [RelayCommand]
    public async Task Send() {
        if (this.Images.Count is 0) {
            return;
        }

        this.IsBusy = true;
        this.StatusMessage = "Sending...";

        // ...

        this.IsBusy = false;
        this.StatusMessage = "Sent!";
    }
}
