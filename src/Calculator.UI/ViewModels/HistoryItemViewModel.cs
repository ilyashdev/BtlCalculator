using Calculator.Core.Engine;

namespace Calculator.UI.ViewModels;

public sealed class HistoryItemViewModel(HistoryEntry entry, string expressionText, string resultText)
{
    public HistoryEntry Entry { get; } = entry;

    public string ExpressionText { get; } = expressionText;

    public string ResultText { get; } = resultText;
}
