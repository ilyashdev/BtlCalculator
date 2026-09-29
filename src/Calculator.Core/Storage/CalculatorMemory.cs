using Calculator.Core.Numerics;

namespace Calculator.Core.Storage;

/// <summary>
/// The memory list (MS, MR, M+, M−, MC). The newest value is first; M+/M− change the newest value
/// or create one when the memory is empty.
/// </summary>
public sealed class CalculatorMemory
{
    public const int MaxItems = 100;

    private readonly List<BigDecimal> _values = [];

    public event EventHandler? Changed;

    public IReadOnlyList<BigDecimal> Values => _values;

    public bool IsEmpty => _values.Count == 0;

    public BigDecimal? Recall() => IsEmpty ? null : _values[0];

    public void Store(BigDecimal value)
    {
        _values.Insert(0, value);
        if (_values.Count > MaxItems)
        {
            _values.RemoveAt(_values.Count - 1);
        }

        OnChanged();
    }

    public void AddToLatest(BigDecimal value)
    {
        if (IsEmpty)
        {
            Store(value);
            return;
        }

        AddAt(0, value);
    }

    public void SubtractFromLatest(BigDecimal value)
    {
        if (IsEmpty)
        {
            Store(value.Negate());
            return;
        }

        AddAt(0, value.Negate());
    }

    public void AddAt(int index, BigDecimal value)
    {
        _values[index] = BigMath.EnsureInRange(_values[index] + value);
        OnChanged();
    }

    public void SubtractAt(int index, BigDecimal value) => AddAt(index, value.Negate());

    public void RemoveAt(int index)
    {
        _values.RemoveAt(index);
        OnChanged();
    }

    public void Clear()
    {
        _values.Clear();
        OnChanged();
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
