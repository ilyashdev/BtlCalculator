using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using Calculator.UI;

namespace Calculator.Android;

/// <summary>The one screen of the app; Avalonia shows the main view in it.</summary>
[Activity(
    Label = "BTL Calculator",
    Theme = "@style/CalculatorTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.ScreenLayout)]
public sealed class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // The back button and the back gesture go to the app first; unhandled, Android leaves the app.
        BackRequested += (_, e) => e.Handled = Avalonia.Application.Current is App app && app.HandleBack();
    }
}
