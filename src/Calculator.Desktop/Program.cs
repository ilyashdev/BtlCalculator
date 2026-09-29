using Avalonia;
using Calculator.UI;
using Calculator.UI.Platform;

namespace Calculator.Desktop;

/// <summary>Entry point of the desktop app (Windows, Linux, macOS).</summary>
public static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    // Also used by the XAML previewer of IDEs.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .AfterSetup(builder => ((App)builder.Instance!).Share = CreateShareService());

    private static IShareService CreateShareService()
    {
#if WINDOWS
        return new WindowsShareService();
#else
        return new NoShareService();
#endif
    }
}
