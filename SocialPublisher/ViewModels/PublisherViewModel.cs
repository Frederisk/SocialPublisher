using Avalonia.Controls;
using Avalonia.Media.Imaging;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Mastonet;
using Mastonet.Entities;

using Microsoft.Extensions.DependencyInjection;

using SocialPublisher.Services;
using SocialPublisher.Utils;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace SocialPublisher.ViewModels;

public partial class PublisherViewModel : ViewModelBase {
    private readonly ISettingService _settingService;
    private readonly IClipboardService _clipboardService;
    private readonly IUrlAnalysisImagesService _urlAnalysisImagesService;

    public AppSettings AppSettings => _settingService.Settings;

    [ObservableProperty]
    private Boolean _isLightboxOpen = false;

    [ObservableProperty]
    private Bitmap? _lightboxImage;

    [ObservableProperty]
    private Boolean _isSettingsOpen = false;

    [ObservableProperty]
    private Boolean _isAlter = false;

    [ObservableProperty]
    private Boolean _isLowQuality = false;

    [ObservableProperty]
    private String _caption = String.Empty;

    [ObservableProperty]
    private String _statusMessage = "Ready to Paste.";

    [ObservableProperty]
    private Boolean _isBusy = false;

    private TelegramBotClient? _telegramClient;

    private MastodonClient? _mastodonClient;
    private MastodonClient? _alterMastodonClient;

    public TopLevel? TopLevelContext { get; set; }

    public ObservableCollection<PostImageViewModel> Images { get; } = [];

    private Progress<String> ProgressReporter => new(message => this.StatusMessage = message);

    private CancellationTokenSource? _cancellationTokenSource;

    [ActivatorUtilitiesConstructor]
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
                    this.Images.Add(new PostImageViewModel(image, RemoveAction, OpenLightbox));
                } catch {
                    // ignore: invalid image data
                }
            }
            this.StatusMessage = $"Pasted {newImages.Count} image(s).";
        } else {
            this.StatusMessage = "No images found in clipboard.";
        }
    }

    [RelayCommand]
    public void CloseLightbox() {
        this.IsLightboxOpen = false;
        var imgToDispose = this.LightboxImage;
        this.LightboxImage = null;
        imgToDispose?.Dispose();
    }

    [RelayCommand]
    public async Task Analysis() {
        String url = this.Caption.Trim();
        if (String.IsNullOrEmpty(url)) {
            return;
        }
        this.StatusMessage = "Analyzing images from URL...";
        this.IsBusy = true;
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;
        try {
            await foreach (var image in this._urlAnalysisImagesService.AnalysisImagesAsync(this.Caption, this.AppSettings.ImagesStoragePath, this.ProgressReporter, token)) {
                this.Images.Add(new PostImageViewModel(image, RemoveAction, OpenLightbox));
            }
            this.StatusMessage = $"Analysis completed.";
        } catch (OperationCanceledException) {
            this.StatusMessage = "Analysis cancelled.";
        } catch {
            this.StatusMessage = "Failed to load an image from URL.";
        } finally {
            this.IsBusy = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
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

    private async void /* void for Action<PostImageViewModel> */ OpenLightbox(PostImageViewModel item) {
        this.IsLightboxOpen = true;

        var oldImage = this.LightboxImage;
        oldImage?.Dispose();
        this.LightboxImage = await Task.Run(() => {
            using MemoryStream stream = new(item.ImageBytes);
            return new Bitmap(stream);
        });
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
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;
        //TelegramBotClient client = new(this.AppSettings.TelegramToken);
        try {
            _telegramClient ??= new TelegramBotClient(this.AppSettings.TelegramToken);
            //await _client.GetMe();
            String chatId = this.AppSettings.TelegramChatId;
            var telegramChunks = this.Images.Chunk(10);
            using SemaphoreSlim throttler = new(4, 4);
            foreach (var chunk in telegramChunks) {
                token.ThrowIfCancellationRequested();

                List<InputMediaPhoto> album = [];
                List<Stream> streamsToDispose = [];
                //Boolean isFirst = true;
                try {
                    var compressionTasks = chunk.Select(async (image) => {
                        await throttler.WaitAsync(token);
                        try {
                            return await ImageHelper.ProcessAndCompressImageAsync(image.ImageBytes, token: token);
                        } finally {
                            throttler.Release();
                        }
                    });

                    Byte[][] compressedImages = await Task.WhenAll(compressionTasks);

                    for (Int32 i = 0; i < chunk.Length; i++) {
                        MemoryStream stream = new MemoryStream(compressedImages[i]);
                        streamsToDispose.Add(stream);
                        InputMediaPhoto photo = new InputMediaPhoto(InputFile.FromStream(stream));
                        if (i is 0) {
                            photo.Caption = this.Caption;
                        }
                        album.Add(photo);
                    }

                    await _telegramClient.SendMediaGroup(chatId, album, cancellationToken: token);
                } /* catch (Exception ex) {
                this.StatusMessage = "Failed to send images to Telegram: " + ex.Message;
                this.IsBusy = false;
                return;
                } */ finally {
                    foreach (var stream in streamsToDispose) {
                        stream.Dispose();
                    }
                }
            }
            this.StatusMessage = "Sent!";
        } catch (OperationCanceledException) {
            this.StatusMessage = "Sending cancelled.";
        } catch (Exception ex) {
            this.StatusMessage = "Failed to send images to Telegram: " + ex.Message;
        } finally {
            this.IsBusy = false;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    public async Task SendMastodon() {
        if (this.Images.Count is 0) {
            return;
        }

        this.IsBusy = true;
        this.StatusMessage = "Sending...";
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;
        try {
            MastodonClient client = this.IsAlter switch {
                true => _alterMastodonClient ??= new(this.AppSettings.AlterMastodonInstanceUrl, this.AppSettings.AlterMastodonAccessToken),
                false => _mastodonClient ??= new(this.AppSettings.MastodonInstanceUrl, this.AppSettings.MastodonAccessToken)
            };
            var mastodonChunks = this.Images.Chunk(4).ToList();
            String? replyStatusId = null;

            for (Int32 i = 0; i < mastodonChunks.Count; i++) {
                token.ThrowIfCancellationRequested();

                var chunk = mastodonChunks[i];
                var uploadTasks = chunk.Select(async (image) => {
                    token.ThrowIfCancellationRequested();
                    Byte[] compressedImage = await ImageHelper.ProcessAndCompressImageAsync(image.ImageBytes, maxDimensionSum: Int32.MaxValue /*unlimited*/, maxFileSizeBytes: 15 * 1024 * 1024 /* 15MiB */, token: token);
                    using MemoryStream stream = new(image.ImageBytes);
                    return await client.UploadMedia(stream);
                });

                Attachment[] attachments = await Task.WhenAll(uploadTasks);
                var mediaIds = attachments.Select(a => a.Id);

                String counterText = $"({i + 1}/{mastodonChunks.Count})";
                //String statusText = (i is 0) ? this.Caption + " " + counterText : counterText;
                String statusText = $"{this.Caption} {counterText}"; //this.Caption + " " + counterText;
                Visibility visibility = (i is 0) ? Visibility.Public : Visibility.Unlisted;
                Status status = await client.PublishStatus(
                    statusText,
                    visibility: visibility,
                    replyStatusId: replyStatusId,
                    mediaIds: mediaIds);
                replyStatusId = status.Id;

            }
            this.StatusMessage = "Sent!";
        } catch (OperationCanceledException) {
            this.StatusMessage = "Cancelled.";
        } catch (Exception ex) {
            this.StatusMessage = "Failed to send to Mastodon: " + ex.Message;
        } finally {
            this.IsBusy = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    public void CancelOperation() {
        if (_cancellationTokenSource is not null && !_cancellationTokenSource.IsCancellationRequested) {
            this.StatusMessage = "Cancelling...";
            _cancellationTokenSource.Cancel();
        }
    }
}
