using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Calculator.UI.Navigation;
using Calculator.UI.Platform;
using Calculator.UI.ViewModels;

namespace Calculator.UI.Views;

/// <summary>
/// Standard and scientific modes. Code-behind handles layout only: adaptive key and display font sizes and the
/// placement of the History | Memory pane.
/// </summary>
public partial class CalculatorView : UserControl, IModeView, IDisposable
{
    // Layout thresholds of the legacy app (Calculator.xaml / CalculatorStandardOperators.xaml visual states).
    private const double DockedPaneMinWidth = 640;
    private const double DockedPaneWidth = 320;
    private const double MediumMinWidth = 468;
    private const double MediumMinHeight = 500;
    private const double LargeMinWidth = 780;
    private const double LargeMinHeight = 814;

    private readonly CalculatorViewModel _viewModel;
    private bool? _paneDockedState; // null until the first layout pass

    public CalculatorView(CalculatorViewModel viewModel, Control keypad)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
        KeypadHost.Content = keypad;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DisplayLabel.SizeChanged += (_, _) => FitDisplayText();
        SizeChanged += (_, e) => OnSizeChanged(e.NewSize);
        DockedPanel.SelectTab(HistoryMemoryTab.History);
        OverlayPanel.ItemInvoked += (_, _) => Motion.Hide(OverlayPane);
    }

    public event EventHandler? ShowsHistoryButtonChanged;

    public bool ShowsHistoryButton => _paneDockedState != true;

    public bool SupportsKeepOnTop => !_viewModel.UsesPrecedence;

    public Control? TopBarAccessory => null;

    public void Dispose() => _viewModel.Dispose();

    public void ToggleHistory() => ToggleOverlay(HistoryMemoryTab.History);

    public void SetCompactMode(bool compact)
    {
        MemoryRow.IsVisible = !compact;
        ExpressionLabel.IsVisible = !compact;
        OverlayPane.IsVisible = false;
        RootGrid.ColumnDefinitions[1].Width = new GridLength(compact || _paneDockedState != true ? 0 : DockedPaneWidth);
        DockedPanel.IsVisible = !compact && _paneDockedState == true;
    }

    public bool CloseOverlay()
    {
        if (!OverlayPane.IsVisible)
        {
            return false;
        }

        Motion.Hide(OverlayPane);
        return true;
    }

    public void HandleKeyboardInput(KeyboardInput input)
    {
        if (input.Key == KeyboardKey.Escape && CloseOverlay())
        {
            return;
        }

        if (input.Control && input.Character == 'h')
        {
            ToggleHistory();
            return;
        }

        _viewModel.HandleKeyboardInput(input);
    }

    private void OnSizeChanged(Avalonia.Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        ApplyKeyFontSizes(size.Width, size.Height);
        SetPaneDocked(size.Width >= DockedPaneMinWidth);
        FitDisplayText();
    }

    private void ApplyKeyFontSizes(double width, double height)
    {
        (double number, double symbol, double text) = (width, height) switch
        {
            _ when width >= LargeMinWidth && height >= LargeMinHeight => (38.0, 34.0, 20.0),
            _ when width >= MediumMinWidth && height >= MediumMinHeight => (24.0, 20.0, 16.0),
            _ => (18.0, 16.0, 14.0),
        };

        Resources["NumberKeyFontSize"] = number;
        Resources["SymbolKeyFontSize"] = symbol;
        Resources["OperatorKeyFontSize"] = text;
    }

    private void SetPaneDocked(bool docked)
    {
        if (_paneDockedState == docked)
        {
            return;
        }

        _paneDockedState = docked;
        RootGrid.ColumnDefinitions[1].Width = new GridLength(docked ? DockedPaneWidth : 0);
        DockedPanel.IsVisible = docked;
        OverlayPane.IsVisible = false;
        ShowsHistoryButtonChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shrinks the display font so that long numbers fit, like the legacy CalculationResult control.</summary>
    private void FitDisplayText()
    {
        double available = DisplayLabel.Bounds.Width;
        if (available <= 0)
        {
            return;
        }

        // The largest size grows with the height (26 … 72 in the legacy app).
        double maxSize = Math.Clamp(Bounds.Height * 0.1, 26, 72);

        // Average glyph width of the display font is about 0.55 em.
        int length = Math.Max(1, _viewModel.DisplayText.Length);
        double fitting = available / (length * 0.55);
        DisplayLabel.FontSize = Math.Clamp(fitting, 12, maxSize);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CalculatorViewModel.DisplayText))
        {
            FitDisplayText();
        }
    }

    private void OnDisplayDoubleTapped(object? sender, TappedEventArgs e) => _viewModel.CopyCommand.Execute(null);

    private void OnMemoryPaneButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (_paneDockedState == true)
        {
            DockedPanel.SelectTab(HistoryMemoryTab.Memory);
            return;
        }

        ToggleOverlay(HistoryMemoryTab.Memory);
    }

    private void ToggleOverlay(HistoryMemoryTab tab)
    {
        OverlayPanel.SelectTab(tab);
        Motion.SetVisible(OverlayPane, !OverlayPane.IsVisible);
    }
}
