using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;

using Microsoft.Extensions.DependencyInjection;

using SocialPublisher.Services;
using SocialPublisher.ViewModels;
using SocialPublisher.Views;

namespace SocialPublisher;

public partial class App : Application {
    public IServiceProvider? Services { get; private set; }

    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        ServiceCollection collection = new();
        collection.AddSingleton<ISettingService, SettingService>();
        collection.AddSingleton<IClipboardService, AvaloniaClipboardService>();
        collection.AddSingleton<IUrlAnalysisImagesService, UrlAnalysisImagesService>();
        collection.AddSingleton<IIOPickerService, IOPickerService>();
        collection.AddTransient<PublisherViewModel>();
        this.Services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            desktop.MainWindow = new MainWindow {
                DataContext = this.Services.GetRequiredService<PublisherViewModel>()
            };
        } else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform) {
            singleViewPlatform.MainView = new MainView {
                DataContext = this.Services.GetRequiredService<PublisherViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

public static class AppExtensions {
    public static T? GetService<T>(this Application app) where T : notnull {
        IServiceProvider? services = (app as App)?.Services;
        if (services is not null) {
            return services.GetService<T>();
        }
        return default;
    }
}