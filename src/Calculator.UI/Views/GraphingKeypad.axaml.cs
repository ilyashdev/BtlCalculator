using Avalonia.Controls;
using Avalonia.Interactivity;
using Calculator.UI.ViewModels;

namespace Calculator.UI.Views;

/// <summary>Code-behind only opens and closes the Trigonometry, Inequalities and Function panels.</summary>
public partial class GraphingKeypad : UserControl
{
    public GraphingKeypad(GraphingKeypadViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <returns>True when a panel was open.</returns>
    public bool ClosePanels()
    {
        bool wasOpen = TrigonometryPanel.IsVisible || InequalitiesPanel.IsVisible || FunctionPanel.IsVisible;
        Motion.Hide(TrigonometryPanel);
        Motion.Hide(InequalitiesPanel);
        Motion.Hide(FunctionPanel);
        return wasOpen;
    }

    private void TogglePanel(Border panel)
    {
        bool show = !panel.IsVisible;
        ClosePanels();
        if (show)
        {
            Motion.Show(panel);
        }
    }

    private void OnTrigonometryClicked(object? sender, RoutedEventArgs e) => TogglePanel(TrigonometryPanel);

    private void OnInequalitiesClicked(object? sender, RoutedEventArgs e) => TogglePanel(InequalitiesPanel);

    private void OnFunctionClicked(object? sender, RoutedEventArgs e) => TogglePanel(FunctionPanel);

    private void OnPanelKeyClicked(object? sender, RoutedEventArgs e) => ClosePanels();
}
