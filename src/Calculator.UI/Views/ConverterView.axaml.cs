using Avalonia.Controls;
using Calculator.UI.Navigation;
using Calculator.UI.Platform;
using Calculator.UI.ViewModels;

namespace Calculator.UI.Views;

public partial class ConverterView : UserControl, IModeView
{
    private readonly ConverterViewModel _viewModel;

    /// <param name="footer">Extra information under the results, e.g. the currency rate status.</param>
    public ConverterView(ConverterViewModel viewModel, Control? footer = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        FooterHost.Content = footer;
    }

    // Converters have no history.
    public event EventHandler? ShowsHistoryButtonChanged
    {
        add { }
        remove { }
    }

    public bool ShowsHistoryButton => false;

    public bool SupportsKeepOnTop => false;

    public Control? TopBarAccessory => null;

    public void ToggleHistory()
    {
        // No history in converters.
    }

    public void SetCompactMode(bool compact)
    {
        // Only the standard mode supports "keep on top".
    }

    public void HandleKeyboardInput(KeyboardInput input) => _viewModel.HandleKeyboardInput(input);
}
