using Avalonia.Controls;
using Avalonia.Interactivity;
using Calculator.Core.Programmer;
using Calculator.UI.Navigation;
using Calculator.UI.Platform;
using Calculator.UI.ViewModels;

namespace Calculator.UI.Views;

/// <summary>Code-behind opens and closes the Bitwise, Bit shift and memory panels (pure view state).</summary>
public partial class ProgrammerView : UserControl, IModeView, IDisposable
{
    private readonly ProgrammerViewModel _viewModel;

    public ProgrammerView(ProgrammerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    // The programmer mode has no history.
    public event EventHandler? ShowsHistoryButtonChanged
    {
        add { }
        remove { }
    }

    public bool ShowsHistoryButton => false;

    public bool SupportsKeepOnTop => false;

    public Control? TopBarAccessory => null;

    public void Dispose() => _viewModel.Dispose();

    public void ToggleHistory()
    {
        // No history in this mode.
    }

    public void SetCompactMode(bool compact)
    {
        // Only the standard mode supports "keep on top".
    }

    public bool CloseOverlay() => ClosePanels();

    public void HandleKeyboardInput(KeyboardInput input)
    {
        if (input.Key == KeyboardKey.Escape && ClosePanels())
        {
            return;
        }

        _viewModel.HandleKeyboardInput(input);
    }

    private bool ClosePanels()
    {
        bool wasOpen = BitwisePanel.IsVisible || BitShiftPanel.IsVisible || MemoryPanel.IsVisible;
        BitwisePanel.IsVisible = false;
        BitShiftPanel.IsVisible = false;
        MemoryPanel.IsVisible = false;
        return wasOpen;
    }

    private void OnBitwiseClicked(object? sender, RoutedEventArgs e)
    {
        bool open = !BitwisePanel.IsVisible;
        ClosePanels();
        Motion.SetVisible(BitwisePanel, open);
    }

    private void OnBitShiftClicked(object? sender, RoutedEventArgs e)
    {
        bool open = !BitShiftPanel.IsVisible;
        ClosePanels();
        Motion.SetVisible(BitShiftPanel, open);
    }

    private void OnMemoryClicked(object? sender, RoutedEventArgs e)
    {
        bool open = !MemoryPanel.IsVisible;
        ClosePanels();
        Motion.SetVisible(MemoryPanel, open);
    }

    private void OnPanelKeyClicked(object? sender, RoutedEventArgs e) => ClosePanels();

    // After a value was recalled or the memory cleared, the list is done.
    private void OnMemoryItemClicked(object? sender, RoutedEventArgs e) => Motion.Hide(MemoryPanel);

    private void OnMemoryClearClicked(object? sender, RoutedEventArgs e) => Motion.Hide(MemoryPanel);

    private void OnShiftModeChecked(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true } button)
        {
            return;
        }

        ShiftMode mode = button == LogicalShift ? ShiftMode.Logical
            : button == RotateShift ? ShiftMode.Rotate
            : button == RotateCarryShift ? ShiftMode.RotateThroughCarry
            : ShiftMode.Arithmetic;
        _viewModel.SelectShiftModeCommand.Execute(mode);
        BitShiftPanel.IsVisible = false;
    }
}
