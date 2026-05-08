using System;
using System.IO;
using System.Text.Json;

using Avalonia.Controls;

using CommunityToolkit.Mvvm.ComponentModel;

using SocialPublisher.Utils;

namespace SocialPublisher.Services;

public partial class AppSettings : ObservableObject {
    [ObservableProperty]
    public partial String TelegramToken { get; set; } = String.Empty;
    [ObservableProperty]
    public partial String TelegramChatId { get; set; } = String.Empty;
    [ObservableProperty]
    public partial String MastodonInstanceUrl { get; set; } = String.Empty;
    [ObservableProperty]
    public partial String MastodonAccessToken { get; set; } = String.Empty;
    [ObservableProperty]
    public partial String AlterMastodonInstanceUrl { get; set; } = String.Empty;
    [ObservableProperty]
    public partial String AlterMastodonAccessToken { get; set; } = String.Empty;

    [ObservableProperty]
    public partial String PixivRefreshToken { get; set; } = String.Empty;

    [ObservableProperty]
    public partial String ImagesStorageBookmark { get; set; } = String.Empty;

    //[property: JsonIgnore] No longer ignore the ImagesStoragePath. We need this stored value so that when the program starts and `TopLevel` is not available, the UI can obtain a value to display.
    [ObservableProperty]
    public partial String ImagesStoragePath { get; set; } = String.Empty;

    //[ObservableProperty]
    //private Boolean _enableBatchMode = false;
    [ObservableProperty]
    public partial Boolean IsNsfwTime { get; set; } = false;

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
    void Save();
}

public class SettingService : ISettingService {
    private static readonly JsonSerializerOptions json_serializer_options = new() { WriteIndented = true };
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
        String json = JsonSerializer.Serialize(this.Settings, json_serializer_options);
        File.WriteAllText(_settingsFilePath, json);
    }
}
