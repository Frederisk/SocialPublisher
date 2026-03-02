using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace SocialPublisher.Utils;

public class TopLevelHelper {

    private static TopLevel? _topLevel;

    public static TopLevel? GetTopLevel() {
        if (_topLevel is null) {
            _topLevel = Application.Current?.ApplicationLifetime switch {
                IClassicDesktopStyleApplicationLifetime desktop => TopLevel.GetTopLevel(desktop.MainWindow),
                ISingleViewApplicationLifetime singleView => TopLevel.GetTopLevel(singleView.MainView),
                _ => null
            };
        }

        return _topLevel;
    }
}
