using System.Text;
using Calculator.Core.Numerics;

namespace Calculator.Core.Engine;

/// <summary>
/// The number currently being typed. Keeps the text exactly as entered ("12.50", "1.5e-3")
/// so the display can show trailing zeros and an unfinished exponent.
/// </summary>
public sealed class NumberInput
{
    private const int MaxExponentDigits = 4;

    private readonly int _maxDigits;
    private string _integerDigits = string.Empty;
    private string _fractionDigits = string.Empty;
    private string _exponentDigits = string.Empty;

    public NumberInput(int maxDigits)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDigits, 1);
        _maxDigits = maxDigits;
    }

    public bool IsNegative { get; private set; }

    /// <summary>Integer part without leading zeros; "0" when nothing was typed.</summary>
    public string IntegerDigits => _integerDigits.Length == 0 ? "0" : _integerDigits;

    public bool HasDecimalPoint { get; private set; }

    public string FractionDigits => _fractionDigits;

    public bool HasExponent { get; private set; }

    public bool IsExponentNegative { get; private set; }

    /// <summary>Exponent digits; "0" when the exponent was started but no digits were typed yet.</summary>
    public string ExponentDigits => _exponentDigits.Length == 0 ? "0" : _exponentDigits;

    private int SignificantDigitCount => _integerDigits.Length + _fractionDigits.Length;

    /// <summary>Appends a digit. Returns false when the maximum number of digits is reached.</summary>
    public bool TryAppendDigit(int digit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(digit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digit, 9);

        if (HasExponent)
        {
            if (_exponentDigits.Length >= MaxExponentDigits)
            {
                return false;
            }

            // An exponent never keeps a leading zero.
            _exponentDigits = _exponentDigits == "0" ? digit.ToString() : _exponentDigits + digit;
            return true;
        }

        if (SignificantDigitCount >= _maxDigits)
        {
            return false;
        }

        if (HasDecimalPoint)
        {
            _fractionDigits += digit;
        }
        else if (_integerDigits.Length > 0 || digit != 0)
        {
            _integerDigits += digit;
        }

        return true;
    }

    public bool TryAddDecimalPoint()
    {
        if (HasDecimalPoint || HasExponent)
        {
            return false;
        }

        HasDecimalPoint = true;
        return true;
    }

    public bool TryBeginExponent()
    {
        if (HasExponent)
        {
            return false;
        }

        HasExponent = true;
        return true;
    }

    /// <summary>Toggles the sign of the exponent while it is being typed, otherwise the sign of the number.</summary>
    public void ToggleSign()
    {
        if (HasExponent)
        {
            IsExponentNegative = !IsExponentNegative;
        }
        else
        {
            IsNegative = !IsNegative;
        }
    }

    /// <summary>Removes the last typed character.</summary>
    public void Backspace()
    {
        if (HasExponent)
        {
            if (_exponentDigits.Length > 0)
            {
                _exponentDigits = _exponentDigits[..^1];
            }
            else
            {
                HasExponent = false;
                IsExponentNegative = false;
            }

            return;
        }

        if (_fractionDigits.Length > 0)
        {
            _fractionDigits = _fractionDigits[..^1];
        }
        else if (HasDecimalPoint)
        {
            HasDecimalPoint = false;
        }
        else if (_integerDigits.Length > 0)
        {
            _integerDigits = _integerDigits[..^1];
        }

        if (_integerDigits.Length == 0 && !HasDecimalPoint)
        {
            IsNegative = false;
        }
    }

    public BigDecimal ToValue()
    {
        var text = new StringBuilder();
        if (IsNegative)
        {
            text.Append('-');
        }

        text.Append(IntegerDigits);
        if (_fractionDigits.Length > 0)
        {
            text.Append('.').Append(_fractionDigits);
        }

        if (_exponentDigits.Length > 0)
        {
            text.Append('e').Append(IsExponentNegative ? '-' : '+').Append(_exponentDigits);
        }

        return BigDecimal.Parse(text.ToString());
    }
}
