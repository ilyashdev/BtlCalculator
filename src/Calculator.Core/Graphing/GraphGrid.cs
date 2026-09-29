using System.Globalization;

namespace Calculator.Core.Graphing;

public readonly record struct GridLine(double Value, float Position, bool IsMajor);

public readonly record struct AxisLabel(double Value, float Position, string Text);

/// <summary>Grid lines and axis labels for a viewport: spacing of 1, 2 or 5 × 10^k with about 100 pixels between major lines.</summary>
public sealed class GraphGrid
{
    private const double MajorSpacingPixels = 100;
    private const int MaxLines = 4000;
    private const int MaxLabels = 500;

    private GraphGrid(IReadOnlyList<GridLine> vertical, IReadOnlyList<GridLine> horizontal, IReadOnlyList<AxisLabel> xLabels, IReadOnlyList<AxisLabel> yLabels)
    {
        VerticalLines = vertical;
        HorizontalLines = horizontal;
        XLabels = xLabels;
        YLabels = yLabels;
    }

    public IReadOnlyList<GridLine> VerticalLines { get; }

    public IReadOnlyList<GridLine> HorizontalLines { get; }

    public IReadOnlyList<AxisLabel> XLabels { get; }

    public IReadOnlyList<AxisLabel> YLabels { get; }

    /// <param name="sameStepOnBothAxes">With proportional axes both axes use the x spacing.</param>
    public static GraphGrid Compute(GraphViewport viewport, bool sameStepOnBothAxes, string decimalSeparator)
    {
        (double majorX, int divisionsX) = NiceStep(viewport.XMax - viewport.XMin, viewport.Width);
        (double majorY, int divisionsY) = NiceStep(viewport.YMax - viewport.YMin, viewport.Height);
        if (sameStepOnBothAxes)
        {
            (majorY, divisionsY) = (majorX, divisionsX);
        }

        return new GraphGrid(
            Lines(viewport.XMin, viewport.XMax, majorX, divisionsX, viewport.ScreenX),
            Lines(viewport.YMin, viewport.YMax, majorY, divisionsY, viewport.ScreenY),
            Labels(viewport.XMin, viewport.XMax, majorX, viewport.ScreenX, decimalSeparator),
            Labels(viewport.YMin, viewport.YMax, majorY, viewport.ScreenY, decimalSeparator));
    }

    /// <summary>A step of the form {1, 2, 5} × 10^k close to <see cref="MajorSpacingPixels"/>, and its number of minor divisions.</summary>
    public static (double Step, int MinorDivisions) NiceStep(double range, double pixels)
    {
        double raw = range * MajorSpacingPixels / Math.Max(pixels, 1);
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double fraction = raw / magnitude;
        return fraction switch
        {
            < 1.5 => (magnitude, 5),
            < 3.5 => (2 * magnitude, 4),
            < 7.5 => (5 * magnitude, 5),
            _ => (10 * magnitude, 5),
        };
    }

    /// <summary>Label text: integers or the decimals the step needs; scientific for very large or small steps. "−" as minus.</summary>
    public static string FormatLabel(double value, double step, string decimalSeparator)
    {
        if (Math.Abs(value) < step * 1e-9)
        {
            return "0";
        }

        double stepExponent = Math.Floor(Math.Log10(step) + 1e-9);
        string text;
        if (step >= 1e6 || step < 1e-5)
        {
            int exponent = (int)Math.Floor(Math.Log10(Math.Abs(value)) + 1e-9);
            double mantissa = value / Math.Pow(10, exponent);
            int decimals = Math.Clamp(exponent - (int)stepExponent, 0, 6);
            text = mantissa.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + "E" + exponent.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            int decimals = Math.Max(0, -(int)stepExponent);
            text = value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        return text.Replace("-", "−", StringComparison.Ordinal).Replace(".", decimalSeparator, StringComparison.Ordinal);
    }

    private static List<GridLine> Lines(double low, double high, double major, int divisions, Func<double, float> toScreen)
    {
        var lines = new List<GridLine>();
        double minor = major / divisions;
        double first = Math.Ceiling(low / minor);
        double last = Math.Floor(high / minor);

        // The loop counts with an integer: near the limits of double precision k + 1 == k, which never ends.
        if (!IsCountable(first, last, MaxLines))
        {
            return lines;
        }

        int count = (int)(last - first);
        for (int i = 0; i <= count; i++)
        {
            double k = first + i;
            bool isMajor = Math.Abs(k) % divisions == 0;
            double value = k * minor;
            lines.Add(new GridLine(value, toScreen(value), isMajor));
        }

        return lines;
    }

    /// <summary>
    /// Whether the multiples first … last of the step can be listed: a sensible count, and every one of them a
    /// different double (not so from about 1e15 on).
    /// </summary>
    private static bool IsCountable(double first, double last, int maxCount) =>
        double.IsFinite(first) && double.IsFinite(last) && last - first <= maxCount
        && Math.Abs(first) < 1e15 && Math.Abs(last) < 1e15;

    private static List<AxisLabel> Labels(double low, double high, double major, Func<double, float> toScreen, string decimalSeparator)
    {
        var labels = new List<AxisLabel>();
        double first = Math.Ceiling(low / major);
        double last = Math.Floor(high / major);
        if (!IsCountable(first, last, MaxLabels))
        {
            return labels;
        }

        int count = (int)(last - first);
        for (int i = 0; i <= count; i++)
        {
            double k = first + i;
            if (k == 0)
            {
                continue;
            }

            double value = k * major;
            labels.Add(new AxisLabel(value, toScreen(value), FormatLabel(value, major, decimalSeparator)));
        }

        return labels;
    }
}
