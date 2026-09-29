using Avalonia;
using Avalonia.Styling;
using Calculator.UI.Platform;

namespace Calculator.UI.Services;

/// <summary>The app theme chosen in Settings (light, dark or the system setting), saved between runs.</summary>
public sealed class AppThemeService(ISettingsStore settings)
{
    private const string PreferenceKey = "AppTheme";

    public ThemeChoice Choice => (ThemeChoice)settings.GetInt(PreferenceKey, (int)ThemeChoice.System);

    /// <summary>The theme the app shows now: the choice, or the system theme for "use system setting".</summary>
    public static AppTheme Current => Application.Current?.ActualThemeVariant == ThemeVariant.Dark ? AppTheme.Dark : AppTheme.Light;

    /// <summary>Applies the saved theme; called once at startup.</summary>
    public void ApplySaved() => Apply(Choice);

    public void SetChoice(ThemeChoice choice)
    {
        settings.Set(PreferenceKey, (int)choice);
        Apply(choice);
    }

    private static void Apply(ThemeChoice choice)
    {
        if (Application.Current is Application application)
        {
            application.RequestedThemeVariant = choice switch
            {
                ThemeChoice.Light => ThemeVariant.Light,
                ThemeChoice.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }
    }
}
