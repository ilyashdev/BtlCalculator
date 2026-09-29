using Avalonia.Controls;
using Calculator.UI.Navigation;
using Calculator.UI.Platform;
using Calculator.UI.ViewModels;

namespace Calculator.UI.Views;

/// <summary>
/// Shows the loading status until rates are available, then replaces itself with the converter
/// (<see cref="ConverterView"/> with <see cref="CurrencyStatusView"/> under the fields).
/// </summary>
public partial class CurrencyView : UserControl, IModeView
{
    private readonly CurrencyViewModel _viewModel;

    public CurrencyView(CurrencyViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.ConverterCreated += (_, _) => ShowConverter();
        viewModel.InitializeCommand.Execute(null);
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

    public void HandleKeyboardInput(KeyboardInput input) => _viewModel.Converter?.HandleKeyboardInput(input);

    private void ShowConverter()
    {
        if (_viewModel.Converter is ConverterViewModel converter)
        {
            Content = new ConverterView(converter, new CurrencyStatusView { DataContext = _viewModel });
        }
    }
}
