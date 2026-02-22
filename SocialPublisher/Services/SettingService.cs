using CommunityToolkit.Mvvm.ComponentModel;

using System;
using System.IO;
using System.Text.Json;

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
    private String _pixivRefreshToken = String.Empty;
}

public interface ISettingService {
    AppSettings Settings { get; }
    public void Save();
}

public class SettingService : ISettingService {
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
        String json = JsonSerializer.Serialize(this.Settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsFilePath, json);
    }
}
