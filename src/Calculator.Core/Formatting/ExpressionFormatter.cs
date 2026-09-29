using System.Text;
using Calculator.Core.Engine;
using Calculator.Core.Numerics;

namespace Calculator.Core.Formatting;

/// <summary>Names used in the expression line. The app provides localized names; <see cref="EnglishExpressionVocabulary"/> is the default.</summary>
public interface IExpressionVocabulary
{
    string OperatorSymbol(BinaryOperator op);

    /// <summary>Name written before the argument, e.g. "sqr", "sin₀", "1/".</summary>
    string FunctionName(UnaryFunction function, AngleUnit? unit);

    string ConstantSymbol(MathConstant constant);
}

/// <summary>Turns expression tokens into the text of the expression line: "2 + sqr(3) =".</summary>
public sealed class ExpressionFormatter(IExpressionVocabulary vocabulary)
{
    public string Format(IReadOnlyList<ExpressionToken> tokens, NumberFormatOptions numberOptions)
    {
        var parts = new List<string>();
        foreach (ExpressionToken token in tokens)
        {
            parts.Add(FormatToken(token, numberOptions));
        }

        // Parentheses hug their content: "(2 + 3)", not "( 2 + 3 )".
        var text = new StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            bool afterOpen = i > 0 && tokens[i - 1] is OpenParenthesisToken;
            bool beforeClose = tokens[i] is CloseParenthesisToken;
            if (i > 0 && !afterOpen && !beforeClose)
            {
                text.Append(' ');
            }

            text.Append(parts[i]);
        }

        return text.ToString();
    }

    private string FormatToken(ExpressionToken token, NumberFormatOptions numberOptions) => token switch
    {
        NumberToken number => NumberFormatter.Format(number.Value, numberOptions),
        ConstantToken constant => vocabulary.ConstantSymbol(constant.Constant),
        OperatorToken op => vocabulary.OperatorSymbol(op.Operator),
        FunctionToken function => vocabulary.FunctionName(function.Function, function.Unit) + "(" + Format(function.Argument, numberOptions) + ")",
        OpenParenthesisToken => "(",
        CloseParenthesisToken => ")",
        EqualsToken => "=",
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, null),
    };
}
