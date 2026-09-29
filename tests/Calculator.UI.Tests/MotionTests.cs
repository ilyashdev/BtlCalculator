using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Calculator.UI.Views;

namespace Calculator.UI.Tests;

public sealed class MotionTests
{
    private static readonly TimeSpan AfterTransition = TimeSpan.FromMilliseconds(400);

    [AvaloniaFact]
    public async Task HideCollapsesAShownPanel()
    {
        var panel = new Border { Width = 100, Height = 100, IsVisible = false };
        var window = new Window { Content = panel };
        window.Show();

        Motion.Show(panel);
        await TestApp.RunAsync(AfterTransition);
        Assert.True(panel.IsVisible);

        Motion.Hide(panel);
        await TestApp.RunAsync(AfterTransition);
        Assert.False(panel.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task SlideFromStartClosesThePane()
    {
        var pane = new Border { Width = 320, IsVisible = false };
        var scrim = new Border { IsVisible = false };
        var window = new Window { Content = new Panel { Children = { scrim, pane } } };
        window.Show();

        Motion.SlideFromStart(pane, scrim, open: true);
        await TestApp.RunAsync(AfterTransition);
        Assert.True(pane.IsVisible);

        Motion.SlideFromStart(pane, scrim, open: false);
        await TestApp.RunAsync(AfterTransition);
        Assert.False(scrim.IsVisible);
        Assert.False(pane.IsVisible);
        window.Close();
    }
}
