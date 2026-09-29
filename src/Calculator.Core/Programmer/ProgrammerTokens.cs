using Calculator.Core.Engine;

namespace Calculator.Core.Programmer;

// Expression line tokens of the programmer mode. Parentheses and "=" reuse the tokens of the standard engine.

public sealed record WordToken(ulong Raw) : ExpressionToken;

public sealed record ProgrammerOperatorToken(ProgrammerOperator Operator) : ExpressionToken;

public sealed record ProgrammerFunctionToken(ProgrammerFunction Function, IReadOnlyList<ExpressionToken> Argument) : ExpressionToken;

/// <summary>A value with its written form; see <c>Engine.Operand</c>.</summary>
internal sealed record ProgrammerOperand(ulong Raw, IReadOnlyList<ExpressionToken> Tokens, int? LowestPrecedence)
{
    public static ProgrammerOperand FromRaw(ulong raw) => new(raw, [new WordToken(raw)], null);

    public bool IsComposite => Tokens.Count != 1 || Tokens[0] is not WordToken;

    public ProgrammerOperand WithFunction(ProgrammerFunction function, ulong result) =>
        new(result, [new ProgrammerFunctionToken(function, Tokens)], null);

    public ProgrammerOperand Parenthesized()
    {
        var tokens = new List<ExpressionToken>(Tokens.Count + 2) { new OpenParenthesisToken() };
        tokens.AddRange(Tokens);
        tokens.Add(new CloseParenthesisToken());
        return new ProgrammerOperand(Raw, tokens, null);
    }

    public static ProgrammerOperand Combine(ProgrammerOperand left, ProgrammerOperator op, ProgrammerOperand right, ulong result)
    {
        var tokens = new List<ExpressionToken>(left.Tokens.Count + right.Tokens.Count + 1);
        tokens.AddRange(left.Tokens);
        tokens.Add(new ProgrammerOperatorToken(op));
        tokens.AddRange(right.Tokens);

        int lowest = op.Precedence();
        if (left.LowestPrecedence is int leftLowest)
        {
            lowest = Math.Min(lowest, leftLowest);
        }

        if (right.LowestPrecedence is int rightLowest)
        {
            lowest = Math.Min(lowest, rightLowest);
        }

        return new ProgrammerOperand(result, tokens, lowest);
    }
}
