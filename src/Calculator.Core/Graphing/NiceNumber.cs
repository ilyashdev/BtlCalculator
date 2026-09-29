using System.Globalization;

namespace Calculator.Core.Graphing;

/// <summary>Formats analysis results: integers, simple fractions (1/3) and multiples of π (π/2) are recognized.</summary>
public static class NiceNumber
{
    public static string Format(double value, CultureInfo culture, double tolerance = 1e-9)
    {
        if (double.IsPositiveInfinity(value))
        {
            return "∞";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "−∞";
        }

        if (Math.Abs(value) < 1e-12)
        {
            return "0";
        }

        string sign = value < 0 ? "−" : string.Empty;
        double magnitude = Math.Abs(value);
        double scale = Math.Max(1, magnitude);

        if (Math.Abs(magnitude - Math.Round(magnitude)) < tolerance * scale)
        {
            return sign + Math.Round(magnitude).ToString("0", culture);
        }

        for (int denominator = 2; denominator <= 12; denominator++)
        {
            double numerator = magnitude * denominator;
            if (Math.Abs(numerator - Math.Round(numerator)) < tolerance * denominator * scale)
            {
                return sign + Math.Round(numerator).ToString("0", culture) + "/" + denominator.ToString(culture);
            }
        }

        double multipleOfPi = magnitude / Math.PI;
        for (int denominator = 1; denominator <= 12; denominator++)
        {
            double numerator = multipleOfPi * denominator;
            double rounded = Math.Round(numerator);
            if (rounded >= 1 && Math.Abs(numerator - rounded) < tolerance * denominator * scale)
            {
                string piText = rounded == 1 ? "π" : rounded.ToString("0", culture) + "π";
                return sign + (denominator == 1 ? piText : piText + "/" + denominator.ToString(culture));
            }
        }

        string decimals = magnitude.ToString("0.####", culture);
        return sign + (decimals == "0" ? magnitude.ToString("G4", culture) : decimals);
    }

    /// <summary>Rounds to a nearby integer, simple fraction or multiple of π when within the tolerance.</summary>
    public static double Snap(double value, double tolerance)
    {
        double scale = Math.Max(1, Math.Abs(value));
        double rounded = Math.Round(value);
        if (Math.Abs(value - rounded) < tolerance * scale)
        {
            return rounded;
        }

        for (int denominator = 2; denominator <= 12; denominator++)
        {
            double numerator = Math.Round(value * denominator);
            if (Math.Abs((value * denominator) - numerator) < tolerance * denominator * scale)
            {
                return numerator / denominator;
            }
        }

        for (int denominator = 1; denominator <= 12; denominator++)
        {
            double numerator = Math.Round(value / Math.PI * denominator);
            if (numerator != 0 && Math.Abs((value / Math.PI * denominator) - numerator) < tolerance * denominator * scale)
            {
                return numerator * Math.PI / denominator;
            }
        }

        return value;
    }
}
