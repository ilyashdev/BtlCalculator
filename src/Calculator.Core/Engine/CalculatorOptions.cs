namespace Calculator.Core.Engine;

/// <param name="UsePrecedence">Scientific mode: × before +. Standard mode evaluates every operator immediately.</param>
/// <param name="MaxInputDigits">Digits that can be typed and that are shown in the display.</param>
/// <param name="Precision">Significant digits used for every calculation (more than displayed, so that 1/3×3 = 1).</param>
public sealed record CalculatorOptions(bool UsePrecedence, int MaxInputDigits, int Precision)
{
    public static CalculatorOptions Standard { get; } = new(UsePrecedence: false, MaxInputDigits: 16, Precision: 40);

    public static CalculatorOptions Scientific { get; } = new(UsePrecedence: true, MaxInputDigits: 32, Precision: 40);
}
