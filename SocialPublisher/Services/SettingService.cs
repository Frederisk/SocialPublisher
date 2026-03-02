using Avalonia.Controls;

using CommunityToolkit.Mvvm.ComponentModel;

using SocialPublisher.Utils;

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SocialPublisher.Services;

public partial class AppSettings : ObservableObject {
    [ObservableProperty]
    private String _telegramToken = String.Empty;
    [ObservableProperty]
    private String _telegramChatId = String.Empty;
    [ObservableProperty]
    private String _mastodonInstanceUrl = String.Empty;
    [ObservableProperty]
    private String _mastodonAccessToken = String.Empty;
    [ObservableProperty]
    private String _alterMastodonInstanceUrl = String.Empty;
    [ObservableProperty]
    private String _alterMastodonAccessToken = String.Empty;
    [ObservableProperty]
    private String _pixivRefreshToken = String.Empty;
    [ObservableProperty]
    //[NotifyPropertyChangedFor(nameof(ImageStoragePath))]
    private String _imagesStorageBookmark = String.Empty;
    [JsonIgnore]
    [ObservableProperty]
    private String _imagesStoragePath = String.Empty;

    partial void OnImagesStorageBookmarkChanged(String value) {
        this.UpdateImagesStoragePathAsync(value);
    }

    private async void UpdateImagesStoragePathAsync(String bookmark) {
        if (String.IsNullOrEmpty(bookmark)) {
            this.ImagesStoragePath = String.Empty;
            return;
        }

        TopLevel? topLevel = TopLevelHelper.GetTopLevel();
        //var provider = topLevel?.StorageProvider;
        if (topLevel is null) {
            this.ImagesStoragePath = String.Empty;
            return;
        }
        var folder = await topLevel.StorageProvider.OpenFolderBookmarkAsync(bookmark);
        this.ImagesStoragePath = folder?.Path.LocalPath ?? String.Empty;
    }
}

public interface ISettingService {
    AppSettings Settings { get; }
    public void Save();
}

public class SettingService : ISettingService {
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
    private readonly String _settingsFilePath;

    public AppSettings Settings { get; private set; }

    public SettingService() {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(appData, "SocialPublisher");
        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
        if (File.Exists(_settingsFilePath)) {
            try {
                String json = File.ReadAllText(_settingsFilePath);
                this.Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                return;
            } catch {
                // Ignore errors and use default settings
            }
        }
        this.Settings = new AppSettings();
    }

    public void Save() {
        String json = JsonSerializer.Serialize(this.Settings, _jsonSerializerOptions);
        File.WriteAllText(_settingsFilePath, json);
    }
}
