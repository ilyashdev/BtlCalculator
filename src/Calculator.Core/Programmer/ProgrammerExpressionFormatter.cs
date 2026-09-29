using System.Text;
using Calculator.Core.Engine;

namespace Calculator.Core.Programmer;

/// <summary>Names used in the programmer expression line; the app provides localized names.</summary>
public interface IProgrammerVocabulary
{
    string OperatorSymbol(ProgrammerOperator op);

    string FunctionName(ProgrammerFunction function);
}

public sealed class EnglishProgrammerVocabulary : IProgrammerVocabulary
{
    public string OperatorSymbol(ProgrammerOperator op) => op switch
    {
        ProgrammerOperator.Add => "+",
        ProgrammerOperator.Subtract => "-",
        ProgrammerOperator.Multiply => "×",
        ProgrammerOperator.Divide => "÷",
        ProgrammerOperator.Modulo => "%",
        ProgrammerOperator.And => "AND",
        ProgrammerOperator.Or => "OR",
        ProgrammerOperator.Xor => "XOR",
        ProgrammerOperator.Nand => "NAND",
        ProgrammerOperator.Nor => "NOR",
        ProgrammerOperator.LeftShift => "Lsh",
        ProgrammerOperator.RightShift => "Rsh",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    public string FunctionName(ProgrammerFunction function) => function switch
    {
        ProgrammerFunction.Negate => "negate",
        ProgrammerFunction.Not => "NOT",
        ProgrammerFunction.RotateLeft => "RoL",
        ProgrammerFunction.RotateRight => "RoR",
        ProgrammerFunction.RotateLeftThroughCarry => "RcL",
        ProgrammerFunction.RotateRightThroughCarry => "RcR",
        _ => throw new ArgumentOutOfRangeException(nameof(function), function, null),
    };
}

/// <summary>Turns programmer expression tokens into text, numbers in the current radix: "1F AND 3 =".</summary>
public sealed class ProgrammerExpressionFormatter(IProgrammerVocabulary vocabulary)
{
    public string Format(IReadOnlyList<ExpressionToken> tokens, Radix radix, WordSize size)
    {
        var text = new StringBuilder();
        for (int i = 0; i < tokens.Count; i++)
        {
            bool afterOpen = i > 0 && tokens[i - 1] is OpenParenthesisToken;
            bool beforeClose = tokens[i] is CloseParenthesisToken;
            if (i > 0 && !afterOpen && !beforeClose)
            {
                text.Append(' ');
            }

            text.Append(FormatToken(tokens[i], radix, size));
        }

        return text.ToString();
    }

    private string FormatToken(ExpressionToken token, Radix radix, WordSize size) => token switch
    {
        WordToken word => ProgrammerFormatter.Format(word.Raw, radix, size),
        ProgrammerOperatorToken op => vocabulary.OperatorSymbol(op.Operator),
        ProgrammerFunctionToken function => vocabulary.FunctionName(function.Function) + "(" + Format(function.Argument, radix, size) + ")",
        OpenParenthesisToken => "(",
        CloseParenthesisToken => ")",
        EqualsToken => "=",
        _ => throw new ArgumentOutOfRangeException(nameof(token), token, null),
    };
}
