using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using SocialPublisher.Models;
using SocialPublisher.Services;

namespace SocialPublisher.ViewModels;

public partial class PublisherViewModel : ViewModelBase {
    private readonly IClipboardService _clipboardService;

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

    public PublisherViewModel(IClipboardService clipboardService) {
        this._clipboardService = clipboardService;
    }

    [RelayCommand]
    public async Task Paste() {
        var newImages = await this._clipboardService.GetImagesFromClipboardAsync();
        if (newImages.Count > 0) {
            foreach (var image in newImages) {
                this.Images.Add(image);
            }
            this.StatusMessage = $"Pasted {newImages.Count} image(s).";
        } else {
            this.StatusMessage = "No images found in clipboard.";
        }
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
