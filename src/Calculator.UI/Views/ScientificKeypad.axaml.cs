using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Calculator.UI.Views;

/// <summary>Code-behind only opens and closes the Trigonometry and Function panels (pure view state).</summary>
public partial class ScientificKeypad : UserControl
{
    public ScientificKeypad()
    {
        InitializeComponent();
    }

    private void OnTrigonometryClicked(object? sender, RoutedEventArgs e)
    {
        Motion.Hide(FunctionPanel);
        Motion.SetVisible(TrigonometryPanel, !TrigonometryPanel.IsVisible);
    }

    private void OnFunctionClicked(object? sender, RoutedEventArgs e)
    {
        Motion.Hide(TrigonometryPanel);
        Motion.SetVisible(FunctionPanel, !FunctionPanel.IsVisible);
    }

    private void OnPanelFunctionClicked(object? sender, RoutedEventArgs e)
    {
        Motion.Hide(TrigonometryPanel);
        Motion.Hide(FunctionPanel);
    }
}
