using Android.Runtime;
using Avalonia;
using Calculator.UI;

namespace Calculator.Android;

/// <summary>Starts the Avalonia app when Android starts the process; gives it the Android share sheet.</summary>
[global::Android.App.Application]
public sealed class AndroidApp : Avalonia.Android.AvaloniaAndroidApplication<App>
{
    public AndroidApp(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).AfterSetup(setup => ((App)setup.Instance!).Share = new AndroidShareService());
}
