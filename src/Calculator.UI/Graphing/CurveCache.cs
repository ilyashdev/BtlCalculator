using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using Calculator.Core.Graphing;
using Calculator.UI.ViewModels;

namespace Calculator.UI.Graphing;

/// <summary>What is drawn for one equation: the computed geometry and the shapes made from it once.</summary>
public sealed record EquationFrame(
    GraphEquationViewModel Item,
    GraphEquation Equation,
    EquationGeometry Geometry,
    StreamGeometry Curves,
    StreamGeometry? RegionPolygons,
    StreamGeometry? RegionCells);

/// <summary>The curves of all equations, computed for one viewport and one <see cref="GraphingViewModel.GeometryVersion"/>.</summary>
public sealed record GraphFrame(GraphViewport Viewport, int Version, IReadOnlyList<EquationFrame> Equations);

/// <summary>
/// Computes the curves off the UI thread, so panning and zooming stay smooth even for hard functions (tan(100x) needs
/// tens of milliseconds). Only one computation runs at a time; requests made meanwhile collapse into the latest one.
/// <para>
/// Frames cover more than the screen: half a screen beyond every edge, at the same density. Panning within that
/// margin only moves the frame, with no computation at all; a new frame (again centered on the view) is computed in
/// the background once less than <see cref="RefreshMargin"/> of it is left. Until new curves are ready — also after
/// zooming — the drawable shows the previous frame moved and scaled to the view.
/// </para>
/// <para>
/// The worker computes points only; the shapes Avalonia draws are made from them on the UI thread, when a frame is
/// published (once per frame, not per drawing).
/// </para>
/// </summary>
public sealed class CurveCache
{
    // Screens computed beyond each edge of the view, and the part of a screen left before a new frame is needed.
    private const double Overscan = 0.5;
    private const double RefreshMargin = 0.2;

    private BuildRequest? _running;
    private BuildRequest? _pending;

    /// <summary>Raised on the UI thread when a new frame is ready to be drawn.</summary>
    public event EventHandler? FrameReady;

    /// <summary>The latest computed frame, or null before the first one.</summary>
    public GraphFrame? Current { get; private set; }

    /// <summary>
    /// Asks for the curves around this view; nothing happens when the current frame, or the one being computed,
    /// covers it with enough margin.
    /// </summary>
    /// <param name="equations">A snapshot taken on the UI thread: the worker never reads the view models.</param>
    public void Request(GraphViewport visible, int version, IReadOnlyList<(GraphEquationViewModel Item, GraphEquation Equation)> equations, GraphEvaluationContext context)
    {
        if (Current is GraphFrame frame && frame.Version == version && Covers(frame.Viewport, visible))
        {
            return;
        }

        if (_running is BuildRequest running && running.Version == version && Covers(running.Viewport, visible))
        {
            _pending = null; // back within the frame being computed: a request waiting after it is obsolete
            return;
        }

        var request = new BuildRequest(visible, Extend(visible), version, equations, context);
        if (_running is null)
        {
            Start(request);
        }
        else
        {
            // The newest request replaces an older waiting one.
            _pending = request;
        }
    }

    /// <summary>The view with half a screen more on every side, at the same number of units per pixel.</summary>
    private static GraphViewport Extend(GraphViewport visible)
    {
        double marginX = Overscan * (visible.XMax - visible.XMin);
        double marginY = Overscan * (visible.YMax - visible.YMin);
        return new GraphViewport(
            visible.XMin - marginX,
            visible.XMax + marginX,
            visible.YMin - marginY,
            visible.YMax + marginY,
            visible.Width * (1 + (2 * Overscan)),
            visible.Height * (1 + (2 * Overscan)));
    }

    /// <summary>
    /// Whether a frame can be shown for the view by moving it only: the same scale, and the view inside the frame
    /// with at least <see cref="RefreshMargin"/> of a screen to spare on every side.
    /// </summary>
    private static bool Covers(GraphViewport frame, GraphViewport visible)
    {
        double unitsPerPixelX = (visible.XMax - visible.XMin) / visible.Width;
        double unitsPerPixelY = (visible.YMax - visible.YMin) / visible.Height;
        bool sameScale =
            Math.Abs(((frame.XMax - frame.XMin) / frame.Width) - unitsPerPixelX) <= 1e-9 * unitsPerPixelX
            && Math.Abs(((frame.YMax - frame.YMin) / frame.Height) - unitsPerPixelY) <= 1e-9 * unitsPerPixelY;
        if (!sameScale)
        {
            return false;
        }

        double marginX = RefreshMargin * (visible.XMax - visible.XMin);
        double marginY = RefreshMargin * (visible.YMax - visible.YMin);
        return frame.XMin <= visible.XMin - marginX && frame.XMax >= visible.XMax + marginX
            && frame.YMin <= visible.YMin - marginY && frame.YMax >= visible.YMax + marginY;
    }

    private void Start(BuildRequest request)
    {
        _running = request;

        // The continuation runs on the UI thread and reads the result, so an exception while building surfaces there.
        Task.Run(() => Build(request)).ContinueWith(built => Dispatcher.UIThread.Post(() => OnBuilt(built)), TaskScheduler.Default);
    }

    private void OnBuilt(Task<BuiltFrame> build)
    {
        BuiltFrame built = build.Result;
        Current = new GraphFrame(
            built.Viewport,
            built.Version,
            [.. built.Equations.Select(equation => new EquationFrame(
                equation.Item,
                equation.Equation,
                equation.Geometry,
                CurvesShape(equation.Geometry.Curves),
                PolygonsShape(equation.Geometry.Region),
                CellsShape(equation.Geometry.Region)))]);
        _running = null;
        FrameReady?.Invoke(this, EventArgs.Empty);

        if (_pending is BuildRequest next)
        {
            _pending = null;
            if (Current.Version != next.Version || !Covers(Current.Viewport, next.Visible))
            {
                Start(next);
            }
        }
    }

    /// <summary>Runs on a worker thread: only immutable equations and a private evaluation context are used.</summary>
    private static BuiltFrame Build(BuildRequest request)
    {
        var equations = new List<BuiltEquation>(request.Equations.Count);
        foreach ((GraphEquationViewModel item, GraphEquation equation) in request.Equations)
        {
            equations.Add(new BuiltEquation(item, equation, CurveBuilder.Build(equation, request.Viewport, request.Context)));
        }

        return new BuiltFrame(request.Viewport, request.Version, equations);
    }

    /// <summary>All curves of an equation as one shape: one drawing call however many pieces (tan(100x) has thousands).</summary>
    private static StreamGeometry CurvesShape(IReadOnlyList<IReadOnlyList<GraphPoint>> curves)
    {
        var shape = new StreamGeometry();
        using StreamGeometryContext context = shape.Open();
        foreach (IReadOnlyList<GraphPoint> curve in curves)
        {
            context.BeginFigure(new Point(curve[0].X, curve[0].Y), isFilled: false);
            for (int i = 1; i < curve.Count; i++)
            {
                context.LineTo(new Point(curve[i].X, curve[i].Y));
            }

            context.EndFigure(isClosed: false);
        }

        return shape;
    }

    private static StreamGeometry? PolygonsShape(GraphRegion? region)
    {
        if (region is null || region.Polygons.Count == 0)
        {
            return null;
        }

        var shape = new StreamGeometry();
        using StreamGeometryContext context = shape.Open();
        foreach (IReadOnlyList<GraphPoint> polygon in region.Polygons)
        {
            context.BeginFigure(new Point(polygon[0].X, polygon[0].Y), isFilled: true);
            for (int i = 1; i < polygon.Count; i++)
            {
                context.LineTo(new Point(polygon[i].X, polygon[i].Y));
            }

            context.EndFigure(isClosed: true);
        }

        return shape;
    }

    private static StreamGeometry? CellsShape(GraphRegion? region)
    {
        if (region is null || region.Cells.Count == 0)
        {
            return null;
        }

        var shape = new StreamGeometry();
        using StreamGeometryContext context = shape.Open();
        foreach ((GraphPoint topLeft, GraphPoint bottomRight) in region.Cells)
        {
            context.BeginFigure(new Point(topLeft.X, topLeft.Y), isFilled: true);
            context.LineTo(new Point(bottomRight.X, topLeft.Y));
            context.LineTo(new Point(bottomRight.X, bottomRight.Y));
            context.LineTo(new Point(topLeft.X, bottomRight.Y));
            context.EndFigure(isClosed: true);
        }

        return shape;
    }

    /// <param name="Visible">The view that asked for the frame.</param>
    /// <param name="Viewport">The area computed: the view with the overscan around it.</param>
    private sealed record BuildRequest(
        GraphViewport Visible,
        GraphViewport Viewport,
        int Version,
        IReadOnlyList<(GraphEquationViewModel Item, GraphEquation Equation)> Equations,
        GraphEvaluationContext Context);

    private sealed record BuiltEquation(GraphEquationViewModel Item, GraphEquation Equation, EquationGeometry Geometry);

    private sealed record BuiltFrame(GraphViewport Viewport, int Version, IReadOnlyList<BuiltEquation> Equations);
}
