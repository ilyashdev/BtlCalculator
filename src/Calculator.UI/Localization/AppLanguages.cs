using System.Globalization;

namespace Calculator.UI.Localization;

/// <summary>A choice of the language setting; <see cref="CultureName"/> is empty for "use system setting".</summary>
public sealed record LanguageOption(string CultureName, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// The languages the app is translated into: the cultures of Resources/Strings/Strings.*.resx. They are neutral
/// cultures, so every regional variant finds them ("ru-BY" and "ru-RU" use "ru", "es-MX" uses "es").
/// </summary>
public static class AppLanguages
{
    /// <summary>The neutral resources (Strings.resx) are English.</summary>
    public const string NeutralCulture = "en";

    /// <summary>Keep in sync with Resources/Strings (one file per culture).</summary>
    public static IReadOnlyList<string> CultureNames { get; } = [NeutralCulture, "ru", "zh-Hans", "hi", "es", "ar", "fr"];

    /// <summary>
    /// "Use system setting" first, then every language by its own name ("English (United States)", "русский (Россия)"),
    /// so that anyone can find their language whatever language the app currently shows.
    /// </summary>
    public static IReadOnlyList<LanguageOption> CreateOptions(string systemSettingText)
    {
        var languages = CultureNames
            .Select(name => new LanguageOption(name, NativeName(CultureInfo.GetCultureInfo(name))))
            .OrderBy(option => option.DisplayName, StringComparer.InvariantCultureIgnoreCase);
        return [new LanguageOption(string.Empty, systemSettingText), .. languages];
    }

    private static string NativeName(CultureInfo culture)
    {
        string name = culture.NativeName;
        return name.Length == 0 ? culture.Name : culture.TextInfo.ToUpper(name[0]) + name[1..];
    }
}
