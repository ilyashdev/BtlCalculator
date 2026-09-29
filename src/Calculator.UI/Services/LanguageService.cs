using System.Globalization;
using Calculator.UI.Localization;
using Calculator.UI.Platform;

namespace Calculator.UI.Services;

/// <summary>
/// The language of the app: the system language (default) or one chosen in Settings, saved between runs.
/// Only the UI language changes; number and date formats keep following the system region, like in Windows apps.
/// </summary>
public sealed class LanguageService(ISettingsStore settings)
{
    private const string PreferenceKey = "Language";

    // The language of the system, remembered before the saved language replaces it.
    private readonly CultureInfo _systemLanguage = CultureInfo.CurrentUICulture;

    /// <summary>Raised after the language changed; the app then rebuilds its pages with the new strings.</summary>
    public event EventHandler? LanguageChanged;

    /// <summary>The chosen culture name, or empty for the system language.</summary>
    public string CultureName => settings.GetString(PreferenceKey, string.Empty);

    /// <summary>True when the current language is written right to left (Arabic, Hebrew, Persian).</summary>
    public static bool IsRightToLeft => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

    /// <summary>Applies the saved language; called once at startup, before any text is read.</summary>
    public void ApplySaved() => Apply(CultureName);

    public void SetLanguage(string cultureName)
    {
        if (cultureName == CultureName)
        {
            return;
        }

        settings.Set(PreferenceKey, cultureName);
        Apply(cultureName);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Apply(string cultureName)
    {
        CultureInfo language = cultureName.Length > 0 && AppLanguages.CultureNames.Contains(cultureName)
            ? CultureInfo.GetCultureInfo(cultureName)
            : _systemLanguage;

        // Both the current thread (the UI thread) and threads started later use the language.
        CultureInfo.CurrentUICulture = language;
        CultureInfo.DefaultThreadCurrentUICulture = language;
    }
}
