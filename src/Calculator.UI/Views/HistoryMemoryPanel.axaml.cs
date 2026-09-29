using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Calculator.UI.Views;

public enum HistoryMemoryTab
{
    History,
    Memory,
}

/// <summary>Code-behind switches between the two tabs (pure view state).</summary>
public partial class HistoryMemoryPanel : UserControl
{
    public HistoryMemoryPanel()
    {
        InitializeComponent();
    }

    /// <summary>Raised after a history or memory entry was chosen (the entry is already loaded by its command).</summary>
    public event EventHandler? ItemInvoked;

    public void SelectTab(HistoryMemoryTab tab)
    {
        bool history = tab == HistoryMemoryTab.History;
        HistoryContent.IsVisible = history;
        HistoryTabIndicator.IsVisible = history;
        MemoryContent.IsVisible = !history;
        MemoryTabIndicator.IsVisible = !history;
    }

    private void OnItemClicked(object? sender, RoutedEventArgs e) => ItemInvoked?.Invoke(this, EventArgs.Empty);

    private void OnHistoryTabClicked(object? sender, RoutedEventArgs e) => SelectTab(HistoryMemoryTab.History);

    private void OnMemoryTabClicked(object? sender, RoutedEventArgs e) => SelectTab(HistoryMemoryTab.Memory);
}
