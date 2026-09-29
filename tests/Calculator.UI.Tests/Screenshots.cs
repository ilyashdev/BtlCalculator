using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Calculator.UI.Navigation;
using Calculator.UI.ViewModels;
using Calculator.UI.Views;

namespace Calculator.UI.Tests;

/// <summary>
/// Pictures of every mode, rendered in memory (no window on screen). Runs only when BTL_SCREENSHOTS names a folder;
/// used to check the look without running the app, and for the screenshots of the project page.
/// </summary>
public sealed class Screenshots
{
    [AvaloniaFact]
    public async Task RenderEveryMode()
    {
        string? folder = Environment.GetEnvironmentVariable("BTL_SCREENSHOTS");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        foreach (ThemeVariant theme in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            Application.Current!.RequestedThemeVariant = theme;
            foreach ((double width, double height) in new[] { (322.0, 540.0), (1000.0, 700.0) })
            {
                foreach (AppMode mode in new[] { AppMode.Standard, AppMode.Scientific, AppMode.Graphing, AppMode.Programmer, AppMode.DateCalculation, AppMode.Length, AppMode.Currency, AppMode.Settings })
                {
                    (Window window, MainView view) = TestApp.ShowMainView(width, height);
                    ((MainViewModel)view.DataContext!).SelectedMode = mode;
                    await TestApp.RunAsync(TimeSpan.FromMilliseconds(400));

                    if (mode == AppMode.Graphing && view.FindControl<ContentControl>("ModeHost")!.Content is GraphingView { DataContext: GraphingViewModel graphing })
                    {
                        graphing.Equations[0].Text = "y=tan(10x)";
                        await TestApp.RunAsync(TimeSpan.FromMilliseconds(800));
                    }

                    using WriteableBitmap? frame = window.CaptureRenderedFrame();
                    frame?.Save(Path.Combine(folder, $"{mode}-{(int)width}x{(int)height}-{theme}.png"), new PngBitmapEncoderOptions());
                    window.Close();
                }
            }
        }

        Application.Current!.RequestedThemeVariant = ThemeVariant.Default;
    }
}
