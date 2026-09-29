using Calculator.Core.Engine;

namespace Calculator.Core.Storage;

/// <summary>The history list; the newest entry is first.</summary>
public sealed class CalculationHistory
{
    public const int MaxItems = 20;

    private readonly List<HistoryEntry> _entries = [];

    public event EventHandler? Changed;

    public IReadOnlyList<HistoryEntry> Entries => _entries;

    public void Add(HistoryEntry entry)
    {
        _entries.Insert(0, entry);
        if (_entries.Count > MaxItems)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }

        OnChanged();
    }

    public void RemoveAt(int index)
    {
        _entries.RemoveAt(index);
        OnChanged();
    }

    public void Clear()
    {
        _entries.Clear();
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
