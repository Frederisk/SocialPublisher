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
        collection.AddSingleton<IClipboardService, AvaloniaClipboardService>();
        collection.AddTransient<PublisherViewModel>();
        this.Services = collection.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
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

    private void DisableAvaloniaDataAnnotationValidation() {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove) {
            BindingPlugins.DataValidators.Remove(plugin);
        }
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