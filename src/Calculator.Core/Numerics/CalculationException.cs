namespace Calculator.Core.Numerics;

public enum CalculationError
{
    /// <summary>x ÷ 0 with x ≠ 0.</summary>
    DivideByZero,

    /// <summary>0 ÷ 0 and similar indeterminate forms.</summary>
    Undefined,

    /// <summary>The argument is outside of the function domain, e.g. √(−1) or log(0).</summary>
    InvalidInput,

    /// <summary>The result is too large to be represented.</summary>
    Overflow,
}

/// <summary>Raised by <see cref="BigMath"/> when an operation has no representable result.</summary>
public sealed class CalculationException : Exception
{
    public CalculationException(CalculationError error)
        : base($"Calculation failed: {error}.")
    {
        Error = error;
    }

    public CalculationError Error { get; }
}
