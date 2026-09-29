using System.Globalization;
using System.Resources;

namespace Calculator.UI.Localization;

/// <summary>
/// Localized strings of the current UI language (see Services/LanguageService) from Resources/Strings/Strings*.resx:
/// English (neutral), Russian, Chinese (Simplified), Hindi, Spanish, Arabic and French.
/// Missing keys fall back to English, then to the key itself so the gap is visible.
/// </summary>
public static class AppStrings
{
    private static readonly ResourceManager Strings =
        new("Calculator.UI.Resources.Strings.Strings", typeof(AppStrings).Assembly);

    public static string Get(string key) =>
        Strings.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    /// <summary>Formats strings that use the "%1", "%2" placeholders.</summary>
    public static string Format(string key, params string[] arguments)
    {
        string text = Get(key);
        for (int i = 0; i < arguments.Length; i++)
        {
            text = text.Replace("%" + (i + 1).ToString(CultureInfo.InvariantCulture), arguments[i], StringComparison.Ordinal);
        }

        return text;
    }

    /// <summary>
    /// The word for a count in the right plural form: key "Date_Day" reads "Date_Day_one", "Date_Day_few", ... as
    /// chosen by <see cref="PluralRules"/>, and "Date_Day_other" when the language has no separate form.
    /// </summary>
    public static string Plural(string key, long count)
    {
        PluralCategory category = PluralRules.Of(count, CultureInfo.CurrentUICulture);
        string formKey = $"{key}_{category.ToString().ToLowerInvariant()}";
        return Strings.GetString(formKey, CultureInfo.CurrentUICulture) ?? Get($"{key}_other");
    }
}
