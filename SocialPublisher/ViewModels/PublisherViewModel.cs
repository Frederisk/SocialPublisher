using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

using Avalonia.Controls;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using SocialPublisher.Services;

namespace SocialPublisher.ViewModels;

public partial class PublisherViewModel : ViewModelBase {
    private readonly IClipboardService _clipboardService;
    private readonly IUrlAnalysisImagesService _urlAnalysisImagesService;

    public ObservableCollection<PostImageViewModel> Images { get; } = [];

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
    private const String Pixiv_Refresh_Token = "token";

    public TopLevel? TopLevelContext { get; set; }

    public PublisherViewModel(IClipboardService clipboardService, IUrlAnalysisImagesService urlAnalysisImagesService) {
        _clipboardService = clipboardService;
        _urlAnalysisImagesService = urlAnalysisImagesService;
    }

    [RelayCommand]
    public async Task Paste() {
        var newImages = await this._clipboardService.GetImagesFromClipboardAsync();
        if (newImages.Count > 0) {
            foreach (var image in newImages) {
                try {
                    this.Images.Add(new PostImageViewModel(image, RemoveAction));
                } catch {
                    // ignore: invalid image data
                }
            }
            this.StatusMessage = $"Pasted {newImages.Count} image(s).";
        } else {
            this.StatusMessage = "No images found in clipboard.";
        }
    }

    private void RemoveAction(PostImageViewModel image) {
        if (!this.Images.Contains(image)) {
            return;
        }
        image.Dispose();
        this.Images.Remove(image);
        this.StatusMessage = $"Removed an image. {this.Images.Count} image(s) remaining.";
    }

    [RelayCommand]
    public async Task Analysis() {
        var images = await this._urlAnalysisImagesService.AnalysisImagesAsync(this.Caption, Pixiv_Refresh_Token);
        foreach (var image in images) {
            try {
                this.Images.Add(new PostImageViewModel(image, RemoveAction));
            } catch {
                // ignore: invalid image data
            }
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
