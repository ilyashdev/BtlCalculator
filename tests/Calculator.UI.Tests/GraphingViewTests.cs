using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Calculator.UI.Graphing;
using Calculator.UI.Navigation;
using Calculator.UI.ViewModels;
using Calculator.UI.Views;

namespace Calculator.UI.Tests;

public sealed class GraphingViewTests
{
    [AvaloniaFact]
    public async Task GraphOfATangentIsDrawn()
    {
        (Window window, MainView view) = TestApp.ShowMainView(width: 1000, height: 700);
        ((MainViewModel)view.DataContext!).SelectedMode = AppMode.Graphing;
        await TestApp.RunAsync(TimeSpan.FromMilliseconds(300));

        var graphing = (GraphingView)view.FindControl<ContentControl>("ModeHost")!.Content!;
        var viewModel = (GraphingViewModel)graphing.DataContext!;
        viewModel.Equations[0].Text = "y=tan(10x)";
        await TestApp.RunAsync(TimeSpan.FromSeconds(1));

        GraphCanvas canvas = graphing.FindControl<GraphCanvas>("Canvas")!;
        Assert.True(canvas.Bounds.Width > 100 && canvas.Bounds.Height > 100);
        using var picture = new RenderTargetBitmap(new PixelSize((int)canvas.Bounds.Width, (int)canvas.Bounds.Height));
        picture.Render(canvas);

        // The curve color of the first equation is somewhere in the picture.
        Assert.True(ContainsColor(picture, viewModel.Equations[0].Color), "the curve was not drawn");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData(0, 0, 1)]
    [InlineData(60, 40, 1)]
    [InlineData(0, 0, 0.8)]
    [InlineData(0, 0, 0.5, false)]
    [InlineData(400, 250, 1, false)]
    public async Task CurveStaysOnItsCoordinatesWhenTheViewMoves(double panX, double panY, double zoom, bool waitForNewCurves = true)
    {
        (Window window, MainView view) = TestApp.ShowMainView(width: 1000, height: 700);
        ((MainViewModel)view.DataContext!).SelectedMode = AppMode.Graphing;
        await TestApp.RunAsync(TimeSpan.FromMilliseconds(300));
        var graphing = (GraphingView)view.FindControl<ContentControl>("ModeHost")!.Content!;
        var viewModel = (GraphingViewModel)graphing.DataContext!;
        viewModel.Equations[0].Text = "y=x";
        await TestApp.RunAsync(TimeSpan.FromMilliseconds(800));

        viewModel.Pan(panX, panY);
        if (zoom != 1)
        {
            viewModel.Zoom(viewModel.Viewport.Width / 3, viewModel.Viewport.Height / 3, zoom);
        }

        // Without waiting, the previous curves are drawn moved and scaled to the new view (see CurveCache).
        await TestApp.RunAsync(TimeSpan.FromMilliseconds(waitForNewCurves ? 800 : 0));
        GraphCanvas canvas = graphing.FindControl<GraphCanvas>("Canvas")!;
        using var picture = new RenderTargetBitmap(new PixelSize((int)canvas.Bounds.Width, (int)canvas.Bounds.Height));
        picture.Render(canvas);

        // Points of y = x in the current view must be drawn in the curve color.
        var viewport = viewModel.Viewport;
        foreach (double x in new[] { viewport.XMin + ((viewport.XMax - viewport.XMin) * 0.3), viewport.XMin + ((viewport.XMax - viewport.XMin) * 0.6) })
        {
            if (x < viewport.YMin || x > viewport.YMax)
            {
                continue;
            }

            int px = (int)viewport.ScreenX(x);
            int py = (int)viewport.ScreenY(x);
            Assert.True(ColorNear(picture, px, py, viewModel.Equations[0].Color, radius: 3), $"y=x not drawn at ({x:G4}; {x:G4}) = pixel ({px}, {py})");
        }

        window.Close();
    }

    private static bool ColorNear(Bitmap picture, int x, int y, Avalonia.Media.Color color, int radius)
    {
        using var copy = new WriteableBitmap(picture.PixelSize, picture.Dpi, Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using var buffer = copy.Lock();
        picture.CopyPixels(new PixelRect(picture.PixelSize), buffer.Address, buffer.RowBytes * picture.PixelSize.Height, buffer.RowBytes);
        byte[] pixels = new byte[buffer.RowBytes * picture.PixelSize.Height];
        System.Runtime.InteropServices.Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int cx = x + dx;
                int cy = y + dy;
                if (cx < 0 || cy < 0 || cx >= picture.PixelSize.Width || cy >= picture.PixelSize.Height)
                {
                    continue;
                }

                int i = (cy * buffer.RowBytes) + (cx * 4);
                if (Math.Abs(pixels[i] - color.B) < 40 && Math.Abs(pixels[i + 1] - color.G) < 40 && Math.Abs(pixels[i + 2] - color.R) < 40)
                {
                    return true;
                }
            }
        }

        return false;
    }
    [AvaloniaTheory]
    [InlineData("-10", "10", "-6", "6")]
    [InlineData("-0.06", "0.07", "-0.05", "0.045")]
    [InlineData("-0.2", "0.2", "-0.25", "0.45")]
    public async Task TangentPassesThroughTheOrigin(string xMin, string xMax, string yMin, string yMax)
    {
        (Window window, MainView view) = TestApp.ShowMainView(width: 1000, height: 700);
        ((MainViewModel)view.DataContext!).SelectedMode = AppMode.Graphing;
        await TestApp.RunAsync(TimeSpan.FromMilliseconds(300));
        var graphing = (GraphingView)view.FindControl<ContentControl>("ModeHost")!.Content!;
        var viewModel = (GraphingViewModel)graphing.DataContext!;
        viewModel.Equations[0].Text = "y=tan(10x)";
        viewModel.XMinText = xMin.Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        viewModel.XMaxText = xMax.Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        viewModel.YMinText = yMin.Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        viewModel.YMaxText = yMax.Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        await TestApp.RunAsync(TimeSpan.FromMilliseconds(1000));

        GraphCanvas canvas = graphing.FindControl<GraphCanvas>("Canvas")!;
        using var picture = new RenderTargetBitmap(new PixelSize((int)canvas.Bounds.Width, (int)canvas.Bounds.Height));
        picture.Render(canvas);
        var viewport = viewModel.Viewport;
        int px = (int)viewport.ScreenX(0);
        int py = (int)viewport.ScreenY(0);
        picture.Save(Path.Combine(Path.GetTempPath(), $"btl-tan-{xMin}.png"), new PngBitmapEncoderOptions());
        Assert.True(ColorNear(picture, px, py, viewModel.Equations[0].Color, radius: 2),
            $"tan(10x) misses the origin, pixel ({px}, {py}); viewport {viewport}");
        window.Close();
    }
    private static bool ContainsColor(Bitmap picture, Avalonia.Media.Color color)
    {
        using var copy = new WriteableBitmap(picture.PixelSize, picture.Dpi, Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Premul);
        using (var buffer = copy.Lock())
        {
            picture.CopyPixels(new PixelRect(picture.PixelSize), buffer.Address, buffer.RowBytes * picture.PixelSize.Height, buffer.RowBytes);
            byte[] pixels = new byte[buffer.RowBytes * picture.PixelSize.Height];
            System.Runtime.InteropServices.Marshal.Copy(buffer.Address, pixels, 0, pixels.Length);
            for (int i = 0; i + 3 < pixels.Length; i += 4)
            {
                if (Math.Abs(pixels[i] - color.B) < 24 && Math.Abs(pixels[i + 1] - color.G) < 24 && Math.Abs(pixels[i + 2] - color.R) < 24 && pixels[i + 3] > 200)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
