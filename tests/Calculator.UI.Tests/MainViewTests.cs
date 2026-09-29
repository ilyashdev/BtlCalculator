using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Calculator.UI.Navigation;
using Calculator.UI.ViewModels;

namespace Calculator.UI.Tests;

public sealed class MainViewTests
{
    private static readonly TimeSpan AfterTransition = TimeSpan.FromMilliseconds(400);

    [AvaloniaFact]
    public async Task NavigationPaneOpensAndCloses()
    {
        (Window window, Views.MainView view) = TestApp.ShowMainView();
        var viewModel = (MainViewModel)view.DataContext!;
        Border pane = view.FindControl<Border>("NavigationPane")!;
        Border scrim = view.FindControl<Border>("NavigationScrim")!;

        viewModel.ToggleNavigationCommand.Execute(null);
        await TestApp.RunAsync(AfterTransition);
        Assert.True(pane.IsVisible);
        Assert.True(scrim.IsVisible);

        viewModel.ToggleNavigationCommand.Execute(null);
        await TestApp.RunAsync(AfterTransition);
        Assert.False(scrim.IsVisible);
        Assert.False(pane.IsVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task ChoosingAModeClosesTheNavigationPane()
    {
        (Window window, Views.MainView view) = TestApp.ShowMainView();
        var viewModel = (MainViewModel)view.DataContext!;
        Border pane = view.FindControl<Border>("NavigationPane")!;

        viewModel.IsNavigationOpen = true;
        await TestApp.RunAsync(AfterTransition);
        viewModel.NavigateCommand.Execute(viewModel.Items.First(item => item.Mode?.Mode == AppMode.Scientific));
        await TestApp.RunAsync(AfterTransition);

        Assert.False(pane.IsVisible);
        Assert.Equal(AppMode.Scientific, viewModel.SelectedMode);
        window.Close();
    }

    [AvaloniaFact]
    public async Task BackClosesThePaneThenReturnsToStandardThenLeaves()
    {
        (Window window, Views.MainView view) = TestApp.ShowMainView();
        var viewModel = (MainViewModel)view.DataContext!;
        viewModel.SelectedMode = AppMode.Scientific;
        viewModel.IsNavigationOpen = true;
        await TestApp.RunAsync(AfterTransition);

        Assert.True(view.HandleBack());
        Assert.False(viewModel.IsNavigationOpen);
        Assert.Equal(AppMode.Scientific, viewModel.SelectedMode);

        Assert.True(view.HandleBack());
        Assert.Equal(AppMode.Standard, viewModel.SelectedMode);

        Assert.False(view.HandleBack());
        window.Close();
    }

    [AvaloniaTheory]
    [MemberData(nameof(AllModes))]
    public async Task EveryModeShowsWithoutBindingErrors(AppMode mode)
    {
        BindingErrors.TakeAll();
        (Window window, Views.MainView view) = TestApp.ShowMainView(width: 1000, height: 800);
        var viewModel = (MainViewModel)view.DataContext!;

        viewModel.SelectedMode = mode;
        await TestApp.RunAsync(AfterTransition);

        IReadOnlyList<string> errors = BindingErrors.TakeAll();
        Assert.True(errors.Count == 0, string.Join(Environment.NewLine, errors));
        window.Close();
    }

    public static TheoryData<AppMode> AllModes() => [.. Enum.GetValues<AppMode>()];
}

public sealed class CalculatorViewTests
{
    [AvaloniaTheory]
    [InlineData(322, 540)]
    [InlineData(1500, 520)]
    public async Task DisplayUsesALargeFontForShortNumbers(double width, double height)
    {
        (Window window, Views.MainView view) = TestApp.ShowMainView(width, height);
        var calculator = (Views.CalculatorView)view.FindControl<ContentControl>("ModeHost")!.Content!;
        var viewModel = (CalculatorViewModel)calculator.DataContext!;
        viewModel.DigitCommand.Execute("1");
        viewModel.DigitCommand.Execute("2");
        await TestApp.RunAsync(TimeSpan.FromMilliseconds(200));

        TextBlock display = calculator.FindControl<TextBlock>("DisplayLabel")!;
        Assert.True(display.FontSize >= 26, $"display font {display.FontSize}");
        window.Close();
    }
}
