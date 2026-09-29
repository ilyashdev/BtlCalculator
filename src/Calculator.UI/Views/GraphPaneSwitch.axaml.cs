using Avalonia.Controls;
using Avalonia.Interactivity;
using Calculator.UI.Localization;

namespace Calculator.UI.Views;

public partial class GraphPaneSwitch : UserControl
{
    private bool _isGraphSelected;

    public GraphPaneSwitch()
    {
        InitializeComponent();
        ApplySelection(animate: false);
    }

    /// <summary>Raised with true when the graph is chosen, false for the equations.</summary>
    public event EventHandler<bool>? PaneSelected;

    public void SetGraphSelected(bool graph)
    {
        if (_isGraphSelected == graph)
        {
            return;
        }

        _isGraphSelected = graph;
        ApplySelection(animate: true);
    }

    private void OnTrackClicked(object? sender, RoutedEventArgs e) => PaneSelected?.Invoke(this, !_isGraphSelected);

    private void ApplySelection(bool animate)
    {
        // The knob covers the left half for the graph and the right half for the equations.
        Grid.SetColumn(Knob, _isGraphSelected ? 0 : 1);
        if (animate)
        {
            // It glides from the half it left.
            Motion.GlideFrom(Knob, _isGraphSelected ? Knob.Bounds.Width : -Knob.Bounds.Width);
        }

        GraphIcon.Classes.Set("onKnob", _isGraphSelected);
        EquationsIcon.Classes.Set("onKnob", !_isGraphSelected);
        ToolTip.SetTip(Track, AppStrings.Get(_isGraphSelected ? "GraphSwitchToEquationMode" : "GraphSwitchToGraphMode"));
    }
}
