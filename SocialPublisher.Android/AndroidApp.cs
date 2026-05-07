using System;

using Android.App;
using Android.Runtime;

using Avalonia;
using Avalonia.Android;

namespace SocialPublisher.Android;

[Application]
public class AndroidApp : AvaloniaAndroidApplication<App> {
    public AndroidApp(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership) {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) {
        return base.CustomizeAppBuilder(builder)
        .WithInterFont();
    }
}

