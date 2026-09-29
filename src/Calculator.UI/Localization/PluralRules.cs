using System.Globalization;

namespace Calculator.UI.Localization;

/// <summary>The plural categories of Unicode CLDR; a language uses only some of them.</summary>
public enum PluralCategory
{
    Zero,
    One,
    Two,
    Few,
    Many,
    Other,
}

/// <summary>
/// Which plural form a whole number takes in the languages of the app (Unicode CLDR plural rules for integers):
/// "1 day, 2 days" in English, "1 день, 2 дня, 5 дней" in Russian, six forms in Arabic, one in Chinese.
/// </summary>
public static class PluralRules
{
    public static PluralCategory Of(long count, CultureInfo culture)
    {
        long n = Math.Abs(count);
        return culture.TwoLetterISOLanguageName switch
        {
            "ru" => Russian(n),
            "ar" => Arabic(n),
            "fr" or "hi" => n is 0 or 1 ? PluralCategory.One : PluralCategory.Other,
            "zh" => PluralCategory.Other,
            _ => n == 1 ? PluralCategory.One : PluralCategory.Other, // English, Spanish
        };
    }

    private static PluralCategory Russian(long n)
    {
        long lastDigit = n % 10;
        long lastTwoDigits = n % 100;
        if (lastDigit == 1 && lastTwoDigits != 11)
        {
            return PluralCategory.One;
        }

        return lastDigit is >= 2 and <= 4 && lastTwoDigits is < 12 or > 14 ? PluralCategory.Few : PluralCategory.Many;
    }

    private static PluralCategory Arabic(long n)
    {
        long lastTwoDigits = n % 100;
        return n switch
        {
            0 => PluralCategory.Zero,
            1 => PluralCategory.One,
            2 => PluralCategory.Two,
            _ when lastTwoDigits is >= 3 and <= 10 => PluralCategory.Few,
            _ when lastTwoDigits is >= 11 and <= 99 => PluralCategory.Many,
            _ => PluralCategory.Other,
        };
    }
}
