namespace Calculator.Core.Graphing;

public readonly record struct GraphPoint(float X, float Y);

/// <summary>The visible range of the graph and the size of the drawing surface (device independent pixels).</summary>
public sealed record GraphViewport(double XMin, double XMax, double YMin, double YMax, double Width, double Height)
{
    private const double MinSpan = 1e-10;
    private const double MaxSpan = 1e14;

    public static GraphViewport Default(double width, double height) => new(-10, 10, -10, 10, width, height);

    public bool IsValid => Width > 0 && Height > 0 && XMax > XMin && YMax > YMin;

    public float ScreenX(double x) => (float)((x - XMin) / (XMax - XMin) * Width);

    public float ScreenY(double y) => (float)((YMax - y) / (YMax - YMin) * Height);

    public double GraphX(double screenX) => XMin + (screenX / Width * (XMax - XMin));

    public double GraphY(double screenY) => YMax - (screenY / Height * (YMax - YMin));

    public GraphViewport WithSize(double width, double height) => this with { Width = width, Height = height };

    /// <summary>Equal units per pixel on both axes, keeping the center (legacy "force proportional axes").</summary>
    public GraphViewport MakeProportional()
    {
        if (Width <= 0 || Height <= 0)
        {
            return this;
        }

        double unitsPerPixel = Math.Max((XMax - XMin) / Width, (YMax - YMin) / Height);
        double centerX = (XMin + XMax) / 2;
        double centerY = (YMin + YMax) / 2;
        return this with
        {
            XMin = centerX - (unitsPerPixel * Width / 2),
            XMax = centerX + (unitsPerPixel * Width / 2),
            YMin = centerY - (unitsPerPixel * Height / 2),
            YMax = centerY + (unitsPerPixel * Height / 2),
        };
    }

    /// <summary>Keeps the scale and the center when the drawing surface is resized.</summary>
    public GraphViewport Resize(double width, double height)
    {
        if (Width <= 0 || Height <= 0)
        {
            return WithSize(width, height);
        }

        double unitsPerPixelX = (XMax - XMin) / Width;
        double unitsPerPixelY = (YMax - YMin) / Height;
        double centerX = (XMin + XMax) / 2;
        double centerY = (YMin + YMax) / 2;
        return new GraphViewport(
            centerX - (unitsPerPixelX * width / 2),
            centerX + (unitsPerPixelX * width / 2),
            centerY - (unitsPerPixelY * height / 2),
            centerY + (unitsPerPixelY * height / 2),
            width,
            height);
    }

    /// <summary>Zooms around a screen point; factor &lt; 1 zooms in. Returns this viewport when the range would become unusable.</summary>
    public GraphViewport Zoom(double screenX, double screenY, double factor)
    {
        double centerX = GraphX(screenX);
        double centerY = GraphY(screenY);
        var zoomed = this with
        {
            XMin = centerX + ((XMin - centerX) * factor),
            XMax = centerX + ((XMax - centerX) * factor),
            YMin = centerY + ((YMin - centerY) * factor),
            YMax = centerY + ((YMax - centerY) * factor),
        };

        return zoomed.IsUsable ? zoomed : this;
    }

    /// <summary>Moves the view by a screen distance, so the graph follows the pointer. Stops where it would become unusable.</summary>
    public GraphViewport Pan(double deltaScreenX, double deltaScreenY)
    {
        double dx = deltaScreenX / Width * (XMax - XMin);
        double dy = deltaScreenY / Height * (YMax - YMin);
        var moved = this with { XMin = XMin - dx, XMax = XMax - dx, YMin = YMin + dy, YMax = YMax + dy };
        return moved.IsUsable ? moved : this;
    }

    /// <summary>
    /// Whether the ranges can be drawn: spans between 1e-10 and 1e14, and far enough from the limits of double
    /// precision that neighbouring pixels still have different coordinates (1e15 + 1e-10 would equal 1e15).
    /// Views typed into the graph options are checked with it too.
    /// </summary>
    public bool IsUsable => IsValid && HasUsableSpan(XMin, XMax) && HasUsableSpan(YMin, YMax);

    private static bool HasUsableSpan(double min, double max)
    {
        double span = max - min;
        double magnitude = Math.Max(Math.Abs(min), Math.Abs(max));
        return span is > MinSpan and < MaxSpan && span > magnitude * 1e-9;
    }
}
