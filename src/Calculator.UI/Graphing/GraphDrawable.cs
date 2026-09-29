using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Calculator.Core.Graphing;
using Calculator.UI.ViewModels;

namespace Calculator.UI.Graphing;

/// <summary>
/// Draws the graph (grid, inequality regions, axes, curves, axis labels and the traced point) from the view model.
/// The curves come from <see cref="CurveCache"/>, computed in the background; while a new view is being computed
/// the previous curves are shown moved and scaled to it, so panning and zooming follow the pointer at once.
/// </summary>
public sealed class GraphDrawable(GraphingViewModel viewModel, CurveCache curves)
{
    private const double LabelFontSize = 12;
    private const double LabelHeight = 16;
    private const double LabelHalfWidth = 45;
    private const double ArrowLength = 8;
    private const double ArrowHalfWidth = 4;
    private const int MaxCachedLabels = 2000;

    private static readonly FontFamily LabelFont = new("Segoe UI Variable Text, Segoe UI, $Default");
    private static readonly Typeface RegularFace = new(LabelFont);
    private static readonly Typeface ItalicFace = new(LabelFont, FontStyle.Italic);

    // Axis numbers already laid out, with their color (all drawn with the same font and size).
    private readonly Dictionary<string, FormattedText> _labels = new(StringComparer.Ordinal);
    private Color _labelColor;

    /// <summary>The graph surface; painted here so it also appears in saved pictures.</summary>
    public Color BackgroundColor { get; set; } = Colors.White;

    public Color GridColor { get; set; } = Colors.LightGray;

    public Color AxisColor { get; set; } = Colors.Black;

    public Color TextColor { get; set; } = Colors.Black;

    public Color TooltipBackground { get; set; } = Colors.White;

    /// <summary>The keyboard tracing cursor (screen coordinates), drawn while active tracing is on.</summary>
    public Point? TraceCursor { get; set; }

    public void Draw(DrawingContext context)
    {
        GraphViewport viewport = viewModel.Viewport;
        if (!viewport.IsValid)
        {
            return;
        }

        // Asks for the curves of this view (nothing happens if they are ready or being computed).
        List<(GraphEquationViewModel, GraphEquation)> equations = viewModel.DrawableEquations
            .Select(item => (item, item.Equation!))
            .ToList();
        curves.Request(viewport, viewModel.GeometryVersion, equations, viewModel.CreateContext());

        var bounds = new Rect(0, 0, viewport.Width, viewport.Height);
        using (context.PushClip(bounds))
        {
            context.FillRectangle(new ImmutableSolidColorBrush(BackgroundColor), bounds);

            string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            GraphGrid grid = GraphGrid.Compute(viewport, sameStepOnBothAxes: true, decimalSeparator);
            DrawGrid(context, viewport, grid);

            GraphFrame? frame = curves.Current;
            if (frame is not null)
            {
                DrawFrame(context, frame, viewport, regions: true);
            }

            DrawAxes(context, viewport);

            if (frame is not null)
            {
                DrawFrame(context, frame, viewport, regions: false);
            }

            DrawLabels(context, viewport, grid);
            DrawTrace(context, viewport);
            DrawTraceCursor(context);
        }
    }

    /// <summary>
    /// The traced point nearest to the pointer. The frame covers more than the view (see <see cref="CurveCache"/>), so
    /// the pointer is taken into the frame's coordinates and the point found is brought back to the view's.
    /// </summary>
    public TracePoint? FindTrace(Point pointer)
    {
        GraphViewport viewport = viewModel.Viewport;
        if (curves.Current is not GraphFrame frame || !viewport.IsValid)
        {
            return null;
        }

        var inFrame = new GraphPoint(
            frame.Viewport.ScreenX(viewport.GraphX((float)pointer.X)),
            frame.Viewport.ScreenY(viewport.GraphY((float)pointer.Y)));
        var equations = frame.Equations.Select(equation => (equation.Equation, equation.Geometry)).ToList();
        TracePoint? trace = GraphTracer.FindNearest(equations, frame.Viewport, viewModel.CreateContext(), inFrame);
        return trace is null ? null : trace with { Screen = new GraphPoint(viewport.ScreenX(trace.X), viewport.ScreenY(trace.Y)) };
    }

    /// <summary>
    /// Draws the regions or the curves of a frame. A frame of another view is mapped onto the current one: screen
    /// coordinates relate linearly (x' = a·x + b), so moving and scaling the drawing is enough.
    /// </summary>
    private void DrawFrame(DrawingContext context, GraphFrame frame, GraphViewport viewport, bool regions)
    {
        GraphViewport from = frame.Viewport;
        double scaleX = (from.XMax - from.XMin) / from.Width / ((viewport.XMax - viewport.XMin) / viewport.Width);
        double scaleY = (from.YMax - from.YMin) / from.Height / ((viewport.YMax - viewport.YMin) / viewport.Height);
        double offsetX = (from.XMin - viewport.XMin) / (viewport.XMax - viewport.XMin) * viewport.Width;
        double offsetY = (viewport.YMax - from.YMax) / (viewport.YMax - viewport.YMin) * viewport.Height;

        using (context.PushTransform(Matrix.CreateScale(scaleX, scaleY) * Matrix.CreateTranslation(offsetX, offsetY)))
        {
            // Line widths are given in pixels of the current view.
            double pixel = 1 / Math.Sqrt(scaleX * scaleY);
            foreach (EquationFrame equation in frame.Equations)
            {
                if (regions)
                {
                    FillRegion(context, equation, equation.Item.Color);
                }
                else
                {
                    DrawCurves(context, equation.Curves, equation.Item.Color, viewModel.LineThickness * pixel, equation.Item.EffectiveLineStyle);
                }
            }
        }
    }

    private void DrawGrid(DrawingContext context, GraphViewport viewport, GraphGrid grid)
    {
        var major = new ImmutablePen(GridColor.ToUInt32(), 1);
        var minor = new ImmutablePen(WithOpacity(GridColor, 0.4).ToUInt32(), 1);
        foreach (GridLine line in grid.VerticalLines)
        {
            context.DrawLine(line.IsMajor ? major : minor, new Point(line.Position, 0), new Point(line.Position, viewport.Height));
        }

        foreach (GridLine line in grid.HorizontalLines)
        {
            context.DrawLine(line.IsMajor ? major : minor, new Point(0, line.Position), new Point(viewport.Width, line.Position));
        }
    }

    /// <summary>Axes with arrow heads at the positive ends and italic "x" / "y" names next to them, like the original.</summary>
    private void DrawAxes(DrawingContext context, GraphViewport viewport)
    {
        double width = viewport.Width;
        double height = viewport.Height;
        var pen = new ImmutablePen(AxisColor.ToUInt32(), 1.5);
        var fill = new ImmutableSolidColorBrush(AxisColor);

        if (viewport.XMin <= 0 && viewport.XMax >= 0)
        {
            double x = viewport.ScreenX(0);
            context.DrawLine(pen, new Point(x, ArrowLength), new Point(x, height));
            FillTriangle(context, fill, new Point(x, 0), new Point(x - ArrowHalfWidth, ArrowLength), new Point(x + ArrowHalfWidth, ArrowLength));
            DrawText(context, Text("y", ItalicFace, LabelFontSize + 2), new Point(x + 8, 0));
        }

        if (viewport.YMin <= 0 && viewport.YMax >= 0)
        {
            double y = viewport.ScreenY(0);
            context.DrawLine(pen, new Point(0, y), new Point(width - ArrowLength, y));
            FillTriangle(context, fill, new Point(width, y), new Point(width - ArrowLength, y - ArrowHalfWidth), new Point(width - ArrowLength, y + ArrowHalfWidth));
            FormattedText name = Text("x", ItalicFace, LabelFontSize + 2);
            DrawText(context, name, new Point(width - 4 - name.Width, y - LabelHeight - 6));
        }
    }

    private static void FillTriangle(DrawingContext context, IBrush fill, Point tip, Point left, Point right)
    {
        var triangle = new StreamGeometry();
        using (StreamGeometryContext geometry = triangle.Open())
        {
            geometry.BeginFigure(tip, isFilled: true);
            geometry.LineTo(left);
            geometry.LineTo(right);
            geometry.EndFigure(isClosed: true);
        }

        context.DrawGeometry(fill, null, triangle);
    }

    /// <summary>Inequality regions: 30 % of the equation color; cells tile the region exactly, so no antialiasing.</summary>
    private static void FillRegion(DrawingContext context, EquationFrame equation, Color color)
    {
        var fill = new ImmutableSolidColorBrush(WithOpacity(color, 0.3));
        if (equation.RegionPolygons is StreamGeometry polygons)
        {
            context.DrawGeometry(fill, null, polygons);
        }

        if (equation.RegionCells is StreamGeometry cells)
        {
            // Without antialiasing the shared edges of the cells do not show.
            using (context.PushRenderOptions(new RenderOptions { EdgeMode = EdgeMode.Aliased }))
            {
                context.DrawGeometry(fill, null, cells);
            }
        }
    }

    /// <summary>All curves of an equation in one call (see <see cref="CurveCache"/>).</summary>
    private static void DrawCurves(DrawingContext context, StreamGeometry shape, Color color, double thickness, GraphLineStyle style)
    {
        ImmutableDashStyle? dashes = style switch
        {
            GraphLineStyle.Dash => new ImmutableDashStyle([4, 2], 0),
            GraphLineStyle.Dot => new ImmutableDashStyle([1, 2], 0),
            _ => null,
        };
        var pen = new ImmutablePen(
            new ImmutableSolidColorBrush(color),
            thickness,
            dashes,
            style == GraphLineStyle.Solid ? PenLineCap.Round : PenLineCap.Flat,
            PenLineJoin.Round);
        context.DrawGeometry(null, pen, shape);
    }

    private void DrawLabels(DrawingContext context, GraphViewport viewport, GraphGrid grid)
    {
        double width = viewport.Width;
        double height = viewport.Height;
        double axisX = viewport.ScreenX(0);
        double axisY = viewport.ScreenY(0);

        // X labels below the axis, pinned to an edge when the axis is off screen.
        double labelTop = Math.Clamp(axisY + 3, 2, height - LabelHeight - 2);
        foreach (AxisLabel label in grid.XLabels)
        {
            if (label.Position < 12 || label.Position > width - 12)
            {
                continue;
            }

            DrawLabel(context, label.Text, label.Position - LabelHalfWidth, labelTop, 2 * LabelHalfWidth, LabelAlignment.Center);
        }

        // Y labels left of the axis, or right of the left edge when the axis is near or beyond it.
        foreach (AxisLabel label in grid.YLabels)
        {
            if (label.Position < LabelHeight || label.Position > height - LabelHeight)
            {
                continue;
            }

            double top = label.Position - (LabelHeight / 2);
            if (axisX < 40)
            {
                double left = Math.Max(axisX, 0) + 5;
                DrawLabel(context, label.Text, left, top, 2 * LabelHalfWidth, LabelAlignment.Left);
            }
            else
            {
                double right = Math.Min(axisX, width) - 5;
                DrawLabel(context, label.Text, right - (2 * LabelHalfWidth), top, 2 * LabelHalfWidth, LabelAlignment.Right);
            }
        }

        if (axisX >= 0 && axisX <= width && axisY >= 0 && axisY <= height)
        {
            FormattedText zero = Text("0", ItalicFace, LabelFontSize);
            DrawText(context, zero, new Point(axisX - 5 - zero.Width, axisY + 3 + ((LabelHeight - zero.Height) / 2)));
        }
    }

    private enum LabelAlignment
    {
        Left,
        Center,
        Right,
    }

    /// <summary>An axis number, aligned in its slot.</summary>
    private void DrawLabel(DrawingContext context, string text, double left, double top, double width, LabelAlignment alignment)
    {
        FormattedText label = Label(text);
        double textLeft = alignment switch
        {
            LabelAlignment.Left => left,
            LabelAlignment.Right => left + width - label.Width,
            _ => left + ((width - label.Width) / 2),
        };

        DrawText(context, label, new Point(textLeft, top + ((LabelHeight - label.Height) / 2)));
    }

    /// <summary>
    /// Laying out text is slow and the same numbers come back frame after frame while panning, so they are kept (until
    /// the color changes with the graph theme).
    /// </summary>
    private FormattedText Label(string text)
    {
        if (_labelColor != TextColor || _labels.Count > MaxCachedLabels)
        {
            _labels.Clear();
            _labelColor = TextColor;
        }

        if (!_labels.TryGetValue(text, out FormattedText? label))
        {
            label = Text(text, RegularFace, LabelFontSize);
            _labels[text] = label;
        }

        return label;
    }

    private FormattedText Text(string text, Typeface face, double size) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, size, new ImmutableSolidColorBrush(TextColor));

    /// <summary>
    /// Text with a thin halo of the graph background around the glyphs, so curves and grid lines passing under it do
    /// not cross the letters (the original does the same).
    /// </summary>
    private void DrawText(DrawingContext context, FormattedText text, Point origin)
    {
        if (text.BuildGeometry(origin) is Geometry outline)
        {
            var halo = new ImmutablePen(new ImmutableSolidColorBrush(BackgroundColor), 3, lineJoin: PenLineJoin.Round);
            context.DrawGeometry(null, halo, outline);
        }

        context.DrawText(text, origin);
    }

    /// <summary>The arrow pointer of keyboard tracing (legacy TracePointer path, 18 px high).</summary>
    private void DrawTraceCursor(DrawingContext context)
    {
        if (!viewModel.IsTracingActive || TraceCursor is not Point cursor)
        {
            return;
        }

        var arrow = new StreamGeometry();
        using (StreamGeometryContext geometry = arrow.Open())
        {
            geometry.BeginFigure(cursor, isFilled: true);
            geometry.LineTo(new Point(cursor.X + 12.9, cursor.Y + 12.9));
            geometry.LineTo(new Point(cursor.X + 5.1, cursor.Y + 12.9));
            geometry.LineTo(new Point(cursor.X, cursor.Y + 18));
            geometry.EndFigure(isClosed: true);
        }

        context.DrawGeometry(Brushes.White, new ImmutablePen(Colors.Black.ToUInt32(), 1), arrow);
    }

    private void DrawTrace(DrawingContext context, GraphViewport viewport)
    {
        // The traced point is in view coordinates already (see FindTrace); its index refers to the current frame.
        if (viewModel.Trace is not TracePoint trace
            || curves.Current is not GraphFrame frame
            || trace.EquationIndex >= frame.Equations.Count)
        {
            return;
        }

        Color color = frame.Equations[trace.EquationIndex].Item.Color;
        double radius = viewModel.LineThickness + 2;
        context.DrawEllipse(new ImmutableSolidColorBrush(color), null, new Point(trace.Screen.X, trace.Screen.Y), radius, radius);

        var culture = CultureInfo.CurrentCulture;
        string separator = culture.NumberFormat.NumberDecimalSeparator == "," ? "; " : ", ";
        FormattedText text = Text($"({trace.X.ToString("G6", culture)}{separator}{trace.Y.ToString("G6", culture)})", RegularFace, LabelFontSize);

        const double TooltipWidth = 150;
        const double TooltipHeight = 24;
        double left = Math.Clamp(trace.Screen.X + 10, 0, viewport.Width - TooltipWidth);
        double top = Math.Clamp(trace.Screen.Y - TooltipHeight - 10, 0, viewport.Height - TooltipHeight);

        var tooltip = new Rect(left, top, TooltipWidth, TooltipHeight);
        context.DrawRectangle(new ImmutableSolidColorBrush(TooltipBackground), new ImmutablePen(GridColor.ToUInt32(), 1), tooltip, 4, 4);
        context.DrawText(text, new Point(left + ((TooltipWidth - text.Width) / 2), top + ((TooltipHeight - text.Height) / 2)));
    }

    private static Color WithOpacity(Color color, double opacity) => Color.FromArgb((byte)(color.A * opacity), color.R, color.G, color.B);
}
