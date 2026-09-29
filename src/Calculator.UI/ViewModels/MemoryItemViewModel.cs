using Calculator.Core.Numerics;

namespace Calculator.UI.ViewModels;

/// <summary>A memory list item with its own MC, M+ and M− buttons.</summary>
public sealed class MemoryItemViewModel
{
    public MemoryItemViewModel(BigDecimal value, string valueText, Action add, Action subtract, Action clear)
    {
        Value = value;
        ValueText = valueText;
        AddCommand = new DelegateCommand(add);
        SubtractCommand = new DelegateCommand(subtract);
        ClearCommand = new DelegateCommand(clear);
    }

    public BigDecimal Value { get; }

    public string ValueText { get; }

    public DelegateCommand AddCommand { get; }

    public DelegateCommand SubtractCommand { get; }

    public DelegateCommand ClearCommand { get; }
}
