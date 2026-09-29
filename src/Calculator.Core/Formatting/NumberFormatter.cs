using System.Globalization;
using System.Numerics;
using System.Text;
using Calculator.Core.Engine;
using Calculator.Core.Numerics;

namespace Calculator.Core.Formatting;

/// <param name="MaxDigits">Significant digits shown; longer values are rounded.</param>
/// <param name="UseScientificNotation">"F-E" key: always show 1.5e+3 style.</param>
public sealed record NumberFormatOptions(
    int MaxDigits,
    string DecimalSeparator,
    string GroupSeparator,
    bool UseDigitGrouping,
    bool UseScientificNotation)
{
    public static NumberFormatOptions FromCulture(CultureInfo culture, int maxDigits, bool useScientificNotation = false) =>
        new(
            maxDigits,
            culture.NumberFormat.NumberDecimalSeparator,
            culture.NumberFormat.NumberGroupSeparator,
            UseDigitGrouping: true,
            useScientificNotation);
}

public static class NumberFormatter
{
    /// <summary>
    /// Formats a value for display. Fixed notation is used when the value fits into
    /// <see cref="NumberFormatOptions.MaxDigits"/> digits, otherwise scientific notation ("1.5e+20").
    /// </summary>
    public static string Format(BigDecimal value, NumberFormatOptions options)
    {
        BigDecimal rounded = value.RoundToSignificantDigits(options.MaxDigits);
        if (rounded.IsZero)
        {
            return "0";
        }

        string sign = rounded.IsNegative ? "-" : string.Empty;
        string digits = BigInteger.Abs(rounded.Coefficient).ToString(CultureInfo.InvariantCulture);
        int magnitude = rounded.Magnitude;
        int decimalPlaces = Math.Max(0, -rounded.Exponent);

        bool fits = magnitude < options.MaxDigits && decimalPlaces <= options.MaxDigits;
        if (options.UseScientificNotation || !fits)
        {
            return sign + FormatScientific(digits, magnitude, options);
        }

        string integerPart;
        string fractionPart;
        if (rounded.Exponent >= 0)
        {
            integerPart = digits + new string('0', rounded.Exponent);
            fractionPart = string.Empty;
        }
        else if (digits.Length > decimalPlaces)
        {
            integerPart = digits[..^decimalPlaces];
            fractionPart = digits[^decimalPlaces..];
        }
        else
        {
            integerPart = "0";
            fractionPart = new string('0', decimalPlaces - digits.Length) + digits;
        }

        string result = sign + GroupDigits(integerPart, options);
        return fractionPart.Length == 0 ? result : result + options.DecimalSeparator + fractionPart;
    }

    /// <summary>Formats the number being typed, keeping a trailing decimal point, trailing zeros and the exponent.</summary>
    public static string FormatInput(NumberInput input, NumberFormatOptions options)
    {
        var text = new StringBuilder();
        if (input.IsNegative)
        {
            text.Append('-');
        }

        text.Append(GroupDigits(input.IntegerDigits, options));
        if (input.HasDecimalPoint)
        {
            text.Append(options.DecimalSeparator).Append(input.FractionDigits);
        }

        if (input.HasExponent)
        {
            text.Append('e').Append(input.IsExponentNegative ? '-' : '+').Append(input.ExponentDigits);
        }

        return text.ToString();
    }

    private static string FormatScientific(string digits, int magnitude, NumberFormatOptions options)
    {
        var text = new StringBuilder();
        text.Append(digits[0]);
        if (digits.Length > 1)
        {
            text.Append(options.DecimalSeparator).Append(digits, 1, digits.Length - 1);
        }

        text.Append('e').Append(magnitude < 0 ? '-' : '+').Append(Math.Abs(magnitude).ToString(CultureInfo.InvariantCulture));
        return text.ToString();
    }

    private static string GroupDigits(string integerDigits, NumberFormatOptions options)
    {
        if (!options.UseDigitGrouping || integerDigits.Length <= 3)
        {
            return integerDigits;
        }

        var text = new StringBuilder();
        int firstGroup = integerDigits.Length % 3;
        if (firstGroup > 0)
        {
            text.Append(integerDigits, 0, firstGroup);
        }

        for (int i = firstGroup; i < integerDigits.Length; i += 3)
        {
            if (text.Length > 0)
            {
                text.Append(options.GroupSeparator);
            }

            text.Append(integerDigits, i, 3);
        }

        return text.ToString();
    }
}
