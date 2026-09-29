using System.Globalization;
using Calculator.Core.Engine;
using Calculator.Core.Formatting;

namespace Calculator.Core.Tests;

/// <summary>Drives the engine with a string of keys and reads the display like a user would.</summary>
internal sealed class CalculatorDriver
{
    private readonly ExpressionFormatter _expressionFormatter = new(new EnglishExpressionVocabulary());

    public CalculatorDriver(CalculatorOptions options)
    {
        Engine = new CalculatorEngine(options);
        Engine.CalculationCompleted += (_, entry) => History.Add(entry);
    }

    public CalculatorEngine Engine { get; }

    public List<HistoryEntry> History { get; } = [];

    private NumberFormatOptions FormatOptions =>
        NumberFormatOptions.FromCulture(CultureInfo.InvariantCulture, Engine.Options.MaxInputDigits) with { UseDigitGrouping = false };

    public string Display => Engine.Error is { } error
        ? error.ToString()
        : Engine.IsTyping
            ? NumberFormatter.FormatInput(Engine.Input, FormatOptions)
            : NumberFormatter.Format(Engine.DisplayValue, FormatOptions);

    public string Expression => _expressionFormatter.Format(Engine.Expression, FormatOptions);

    /// <summary>
    /// Keys: digits, '.', + - * / = ( ), 'n' negate, '%' percent, 'c' clear, 'e' clear entry, 'b' backspace, 'x' exponent.
    /// </summary>
    public CalculatorDriver Press(string keys)
    {
        foreach (char key in keys)
        {
            switch (key)
            {
                case >= '0' and <= '9':
                    Engine.EnterDigit(key - '0');
                    break;
                case '.':
                    Engine.EnterDecimalPoint();
                    break;
                case '+':
                    Engine.ApplyBinaryOperator(BinaryOperator.Add);
                    break;
                case '-':
                    Engine.ApplyBinaryOperator(BinaryOperator.Subtract);
                    break;
                case '*':
                    Engine.ApplyBinaryOperator(BinaryOperator.Multiply);
                    break;
                case '/':
                    Engine.ApplyBinaryOperator(BinaryOperator.Divide);
                    break;
                case '^':
                    Engine.ApplyBinaryOperator(BinaryOperator.Power);
                    break;
                case '=':
                    Engine.Evaluate();
                    break;
                case '(':
                    Engine.OpenParenthesis();
                    break;
                case ')':
                    Engine.CloseParenthesis();
                    break;
                case 'n':
                    Engine.Negate();
                    break;
                case '%':
                    Engine.ApplyPercent();
                    break;
                case 'c':
                    Engine.Clear();
                    break;
                case 'e':
                    Engine.ClearEntry();
                    break;
                case 'b':
                    Engine.Backspace();
                    break;
                case 'x':
                    Engine.BeginExponent();
                    break;
                case ' ':
                    break;
                default:
                    throw new ArgumentException($"Unknown key '{key}'.", nameof(keys));
            }
        }

        return this;
    }

    public CalculatorDriver Apply(UnaryFunction function)
    {
        Engine.ApplyUnaryFunction(function);
        return this;
    }
}
