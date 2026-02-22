using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using SocialPublisher.Services;

using Telegram.Bot;

namespace SocialPublisher.ViewModels;

public partial class PublisherViewModel : ViewModelBase {
    private readonly ISettingService _settingService;
    private readonly IClipboardService _clipboardService;
    private readonly IUrlAnalysisImagesService _urlAnalysisImagesService;

    public AppSettings AppSettings => _settingService.Settings;

    [ObservableProperty]
    private Boolean _isSettingsOpen = false;


    [ObservableProperty]
    private String _caption = String.Empty;

    [ObservableProperty]
    private String _statusMessage = "Ready to Paste.";

    [ObservableProperty]
    private Boolean _isBusy = false;

    public TopLevel? TopLevelContext { get; set; }

    public ObservableCollection<PostImageViewModel> Images { get; } = [];

    private Progress<String> ProgressReporter => new(message => this.StatusMessage = message);

    public PublisherViewModel(
        IClipboardService clipboardService,
        IUrlAnalysisImagesService urlAnalysisImagesService,
        ISettingService settingService) {
        _clipboardService = clipboardService;
        _urlAnalysisImagesService = urlAnalysisImagesService;
        _settingService = settingService;
    }

    [RelayCommand]
    public void ToggleSettings() {
        this.IsSettingsOpen = !this.IsSettingsOpen;
    }

    [RelayCommand]
    public void SaveSettings() {
        _settingService.Save();
        this.IsSettingsOpen = false;
        this.StatusMessage = "Settings saved.";
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
        String url = this.Caption.Trim();
        if (String.IsNullOrEmpty(url)) {
            return;
        }
        this.StatusMessage = "Analyzing images from URL...";
        this.IsBusy = true;

        try {
            await foreach (var image in this._urlAnalysisImagesService.AnalysisImagesAsync(this.Caption, this.ProgressReporter)) {
                this.Images.Add(new PostImageViewModel(image, RemoveAction));
            }
        } catch {
            this.StatusMessage = "Failed to load an image from URL.";
        }
        this.StatusMessage = $"Analysis completed.";
        this.IsBusy = false;
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
    public async Task SendTelegram() {
        if (this.Images.Count is 0) {
            return;
        }

        this.IsBusy = true;
        this.StatusMessage = "Sending...";

        var telegramChunks = this.Images.Chunk(10).ToList();
        

        this.IsBusy = false;
        this.StatusMessage = "Sent!";
    }

    [RelayCommand]
    public async Task SendMastodon() {
        if (this.Images.Count is 0) {
            return;
        }

        this.IsBusy = true;
        this.StatusMessage = "Sending...";



        this.IsBusy = false;
        this.StatusMessage = "Sent!";
    }

}
