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
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace SocialPublisher.ViewModels;

public partial class PublisherViewModel : ViewModelBase {
    private readonly ISettingService _settingService;
    private readonly IClipboardService _clipboardService;
    private readonly IUrlAnalysisImagesService _urlAnalysisImagesService;
    private readonly IIOPickerService _iOPickerService;

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
    private String _batchUri = String.Empty;

    [ObservableProperty]
    private String _statusMessage = "Ready to Paste.";

    [ObservableProperty]
    private Boolean _isBusy = false;

    private TelegramBotClient? _telegramClient;

    private MastodonClient? _mastodonClient;
    private MastodonClient? _alterMastodonClient;

    public TopLevel? TopLevelContext { get; set; }

    public ObservableCollection<PostImageViewModel> Images { get; } = [];

    private readonly Progress<String> _progressReporter;

    private CancellationTokenSource? _cancellationTokenSource;

#pragma warning disable CS8618
    public PublisherViewModel() {
        // Parameterless constructor for design-time data context
    }
#pragma warning restore CS8618

    [ActivatorUtilitiesConstructor]
    public PublisherViewModel(
        IClipboardService clipboardService,
        IUrlAnalysisImagesService urlAnalysisImagesService,
        ISettingService settingService,
        IIOPickerService iOPickerService) {
        _clipboardService = clipboardService;
        _urlAnalysisImagesService = urlAnalysisImagesService;
        _settingService = settingService;
        _iOPickerService = iOPickerService;
        _progressReporter = new Progress<String>(message => StatusMessage = message);
    }

    [RelayCommand]
    public void ToggleSettings() {
        this.IsSettingsOpen = !this.IsSettingsOpen;
    }

    [RelayCommand]
    public async Task PickImagesStorageFolder() {
        this.AppSettings.ImagesStorageBookmark = await _iOPickerService.PickFolderAsync(this.AppSettings.ImagesStorageBookmark);
    }

    [RelayCommand]
    public void ClearImagesStorageFolder() {
        this.AppSettings.ImagesStorageBookmark = String.Empty;
    }

    [RelayCommand]
    public void SaveSettings() {
        _settingService.Save();

        _mastodonClient = null;
        _alterMastodonClient = null;
        _telegramClient = null;

        this.IsSettingsOpen = false;
        this.StatusMessage = "Settings saved.";
    }

    [RelayCommand]
    public async Task Paste() {
        Int32 insertImageCount = 0;
        await foreach (var image in this._clipboardService.GetImagesFromClipboardAsync()) {
            this.Images.Add(new PostImageViewModel(image, RemoveAction, OpenLightbox, this.AppSettings));
            insertImageCount++;
        }
        if (insertImageCount is not 0) {
            this.StatusMessage = $"Successfully pasted {insertImageCount} image(s) from clipboard.";
        } else {
            var text = await this._clipboardService.GetTextFromClipboardAsync();
            if (!String.IsNullOrEmpty(text)) {
                this.Caption = text;
                this.StatusMessage = "Text caption loaded from clipboard.";
            } else {
                this.StatusMessage = "No compatible images or text found in clipboard.";
            }
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
        if (this.AppSettings.EnableBatchMode) {
            await this.StartBatchAsync(SocialPlatform.None);
            return;
        }

        String uri = this.Caption.Trim();
        if (String.IsNullOrEmpty(uri)) {
            return;
        }
        this.IsBusy = true;
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;
        try {
            await this.AnalysisUriToImagesAsync(uri, token);
        } catch (OperationCanceledException) {
            this.StatusMessage = "Analysis cancelled.";
        } catch (Exception ex) {
            this.StatusMessage = "Failed to load an image from URL: " + ex.Message;
        } finally {
            this.IsBusy = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private async Task AnalysisUriToImagesAsync(String uri, CancellationToken token) {
        this.StatusMessage = "Analyzing images from URL...";
        await foreach (var image in this._urlAnalysisImagesService.AnalysisImagesAsync(uri, this.AppSettings.ImagesStorageBookmark, this._progressReporter, token)) {
            this.Images.Add(new PostImageViewModel(image, RemoveAction, OpenLightbox, this.AppSettings));
        }
        this.StatusMessage = $"Analysis completed. Number of images: {this.Images.Count}";
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
        try {
            this.IsLightboxOpen = true;

            var oldImage = this.LightboxImage;
            this.LightboxImage = await Task.Run(() => {
                using MemoryStream stream = new(item.ImageBytes);
                return new Bitmap(stream);
            });
            oldImage?.Dispose();
        } catch (Exception ex) {
            this.StatusMessage = $"Failed to open image: {ex.Message}";
            this.IsLightboxOpen = false;
        }
    }

    [RelayCommand]
    public void Clear() {
        foreach (var image in this.Images) {
            image.Dispose();
        }
        this.Images.Clear();
        this.Caption = String.Empty;
        //this.BatchUri = String.Empty;
        this.StatusMessage = "All images and caption have been cleared.";
    }

    [RelayCommand]
    public async Task SendToTelegram() => await this.SendToSocialAsync(SocialPlatform.Telegram);

    [RelayCommand]
    public async Task SendToMastodon() => await this.SendToSocialAsync(SocialPlatform.Mastodon);

    public async Task SendToSocialAsync(SocialPlatform platform) {
        if (this.AppSettings.EnableBatchMode) {
            await this.StartBatchAsync(platform);
            return;
        }

        this.IsBusy = true;
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;
        try {
            this.StatusMessage = $"Sending to {platform.ToString()}...";
            if (this.Images.Count is 0) {
                throw new InvalidOperationException("No image found.");
            }
            if (platform.HasFlag(SocialPlatform.Mastodon)) {
                await this.SendImagesToMastodonAsync(token);
            }
            if (platform.HasFlag(SocialPlatform.Telegram)) {
                await this.SendImagesToTelegramAsync(token);
            }
            this.StatusMessage = $"Sent to {platform.ToString()}!";
        } catch (OperationCanceledException) {
            this.StatusMessage = "Sending cancelled.";
        } catch (Exception ex) {
            this.StatusMessage = $"Failed to send images to {platform.ToString()}: " + ex.Message;
        } finally {
            this.IsBusy = false;
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private async Task SendImagesToMastodonAsync(CancellationToken token) {
        MastodonClient client = this.IsAlter switch {
            true => _alterMastodonClient ??= new(this.AppSettings.AlterMastodonInstanceUrl, this.AppSettings.AlterMastodonAccessToken),
            false => _mastodonClient ??= new(this.AppSettings.MastodonInstanceUrl, this.AppSettings.MastodonAccessToken)
        };
        var mastodonChunks = this.Images.Chunk(4).ToList();
        String? replyStatusId = null;

        for (Int32 i = 0; i < mastodonChunks.Count; i++) {
            token.ThrowIfCancellationRequested();

            this.StatusMessage = $"Processing Mastodon upload: Chunk {i + 1} of {mastodonChunks.Count}...";

            var chunk = mastodonChunks[i];
            var uploadTasks = chunk.Select(async (image) => {
                token.ThrowIfCancellationRequested();
                //this.StatusMessage = $"Compressing image {Array.IndexOf(chunk, image) + 1}/{chunk.Length} for Mastodon upload...";
                Byte[] compressedImage = await ImageHelper.ProcessAndCompressImageAsync(image.ImageBytes, maxDimensionSum: Int32.MaxValue /* unlimited */, maxFileSizeBytes: 16 * 1024 * 1024 /* 16MiB */, token: token);
                using MemoryStream stream = new(compressedImage);
                //this.StatusMessage = $"Uploading image {Array.IndexOf(chunk, image) + 1} of {chunk.Length} in chunk {i + 1} of {mastodonChunks.Count}...";
                //return await client.UploadMedia(stream);
                return await this.ExecuteApiWithRetryAsync(
                    action: () => client.UploadMedia(stream),
                    resetStreamAction: () => stream.Position = 0,
                    token: token,
                    handleRateLimit: true
                    );
            });

            Attachment[] attachments = await Task.WhenAll(uploadTasks);
            var mediaIds = attachments.Select(a => a.Id);

            String counterText = $"({i + 1}/{mastodonChunks.Count})";
            //String statusText = (i is 0) ? this.Caption + " " + counterText : counterText;
            String statusText = $"{this.Caption} {counterText}"; //this.Caption + " " + counterText;
            Visibility visibility = (i is 0) ? Visibility.Public : Visibility.Unlisted;

            this.StatusMessage = $"Publishing status to Mastodon ({i + 1}/{mastodonChunks.Count})...";

            Status status = await this.ExecuteApiWithRetryAsync(
                action: () => client.PublishStatus(
                statusText,
                visibility: visibility,
                replyStatusId: replyStatusId,
                mediaIds: mediaIds),
                resetStreamAction: null,
                token: token,
                handleRateLimit: false
                );
            replyStatusId = status.Id;
        }
    }

    private async Task SendImagesToTelegramAsync(CancellationToken token) {
        _telegramClient ??= new TelegramBotClient(this.AppSettings.TelegramToken);
        String chatId = this.AppSettings.TelegramChatId;
        var telegramChunks = this.Images.Chunk(10).ToArray();
        using SemaphoreSlim throttler = new(4, 4);
        //foreach (var chunk in telegramChunks) {
        for (Int32 i = 0; i < telegramChunks.Length; i++) {
            token.ThrowIfCancellationRequested();
            var chunk = telegramChunks[i];

            this.StatusMessage = $"Compressing images for Telegram: Chunk {i + 1} of {telegramChunks.Length}...";

            List<InputMediaPhoto> album = [];
            List<Stream> streamsToDispose = [];
            try {
                //this.StatusMessage = $"Compressing {chunk.Length} image(s) for Telegram upload...";
                var compressionTasks = chunk.Select(async (image) => {
                    await throttler.WaitAsync(token);
                    try {
                        return await ImageHelper.ProcessAndCompressImageAsync(image.ImageBytes, maxMatrixLimit: Int64.MaxValue /* unlimited */, token: token);
                    } finally {
                        throttler.Release();
                    }
                });

                Byte[][] compressedImages = await Task.WhenAll(compressionTasks);

                for (Int32 ii = 0; ii < chunk.Length; ii++) {
                    MemoryStream stream = new MemoryStream(compressedImages[i]);
                    streamsToDispose.Add(stream);
                    InputMediaPhoto photo = new InputMediaPhoto(InputFile.FromStream(stream));
                    if (ii is 0) {
                        photo.Caption = this.Caption;
                    }
                    album.Add(photo);
                }
                this.StatusMessage = $"Uploading chunk {i + 1} to Telegram ({chunk.Length} images)...";
                await this.ExecuteApiWithRetryAsync(
                    action: () => _telegramClient.SendMediaGroup(chatId, album, cancellationToken: token),
                    resetStreamAction: () => {
                        foreach (var stream in streamsToDispose) {
                            stream.Position = 0;
                        }
                    },
                    token: token,
                    handleRateLimit: false
                    );
            } finally {
                foreach (var stream in streamsToDispose) {
                    stream.Dispose();
                }
            }
        }
    }

    [RelayCommand]
    public void CancelOperation() {
        var source = _cancellationTokenSource;
        if (source is { IsCancellationRequested: false }) {
            this.StatusMessage = "Cancelling...";
            try {
                source.Cancel();
            } catch (ObjectDisposedException) { }
        }
    }

    [Flags]
    public enum SocialPlatform : UInt32 {
        All = UInt32.MaxValue,
        None = UInt32.MinValue,
        Mastodon = 0B0001,
        Telegram = 0B0010,
    }

    public async Task StartBatchAsync(SocialPlatform platform) {
        String[] uriList = this.BatchUri.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;
        this.IsBusy = true;
        Boolean isAllDone = true;
        this.Clear();
        //foreach (String uri in uriList) {
        for (Int32 i = 0; i < uriList.Length; i++) {
            var uri = uriList[i];
            try {
                token.ThrowIfCancellationRequested();
                this.Caption = uri.Trim();
                this.StatusMessage = $"Batch processing ({i + 1}/{uriList.Length}): Analyzing URL...";
                await this.AnalysisUriToImagesAsync(this.Caption, token);
                this.StatusMessage = $"Batch processing ({i + 1}/{uriList.Length}): Sending to {platform.ToString()}...";
                if (this.Images.Count is 0) {
                    throw new InvalidOperationException("No images extracted from the URL.");
                }
                if (platform.HasFlag(SocialPlatform.Mastodon)) {
                    await this.SendImagesToMastodonAsync(token);
                }
                if (platform.HasFlag(SocialPlatform.Telegram)) {
                    await this.SendImagesToTelegramAsync(token);
                }
                this.StatusMessage = $"Batch processing ({i + 1}/{uriList.Length}): Successfully sent to {platform.ToString()}!";
            } catch (OperationCanceledException) {
                this.StatusMessage = "Batch processing was cancelled by the user.";
                isAllDone = false;
                break;
            } catch (Exception ex) {
                isAllDone = false;
                this.StatusMessage = $"Batch error on URL {i + 1}: {ex.Message}";
                break;
            }
            // Instead of placing `Clear` in a finally block,
            // we try to preserve the state when exception occurs.
            this.Clear();
        }
        if (isAllDone) {
            this.StatusMessage = "All batch operations completed successfully.";
        }
        this.IsBusy = false;
        _cancellationTokenSource.Dispose();
        _cancellationTokenSource = null;
    }

    private async Task<T> ExecuteApiWithRetryAsync<T>(Func<Task<T>> action, Action? resetStreamAction, CancellationToken token, Boolean handleRateLimit = false) {
        Int32 maxRetries = 3;
        Int32 attempts = 0;

        while (true) {
            token.ThrowIfCancellationRequested();

            try {
                resetStreamAction?.Invoke();
                return await action();
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) when (handleRateLimit && IsTooManyRequests(ex)) {
                this.StatusMessage = $"Too many requests. Waiting to 5 minutes...";
                await Task.Delay(TimeSpan.FromMinutes(5), token);
            } catch (Exception ex) {
                attempts++;
                if (attempts >= maxRetries) {
                    throw;
                }
                this.StatusMessage = $"Network failure, {ex.Message} Retrying...";
                await Task.Delay(TimeSpan.FromSeconds(3), token);
            }
        }
    }

    private static Boolean IsTooManyRequests(Exception ex) {
        //if (ex is ServerErrorException serverEx && serverEx.HResult is 0x) {
        //    return true;
        //}
        if (ex is HttpRequestException httpEx && httpEx.StatusCode is HttpStatusCode.TooManyRequests) {
            return true;
        }
        if (ex.InnerException is HttpRequestException innerHttpEx && innerHttpEx.StatusCode is HttpStatusCode.TooManyRequests) {
            return true;
        }
        if (ex.Message.Contains("429") || ex.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }
        return false;
    }

}
