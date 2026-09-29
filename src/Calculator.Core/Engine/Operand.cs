using Calculator.Core.Numerics;

namespace Calculator.Core.Engine;

/// <summary>
/// A value together with how it is written in the expression line.
/// <see cref="LowestPrecedence"/> is the precedence of the loosest operator inside the written form
/// (null for atoms such as numbers, functions and parenthesized groups); it decides when parentheses are needed.
/// </summary>
internal sealed record Operand(BigDecimal Value, IReadOnlyList<ExpressionToken> Tokens, int? LowestPrecedence)
{
    public static Operand FromNumber(BigDecimal value) => new(value, [new NumberToken(value)], null);

    public static Operand FromToken(BigDecimal value, ExpressionToken token) => new(value, [token], null);

    /// <summary>True when the written form is more than a plain number, e.g. "sqr(5)" or "(2 + 3)".</summary>
    public bool IsComposite => Tokens.Count != 1 || Tokens[0] is not NumberToken;

    public Operand WithFunction(UnaryFunction function, AngleUnit? unit, BigDecimal result) =>
        new(result, [new FunctionToken(function, unit, Tokens)], null);

    public Operand Parenthesized()
    {
        var tokens = new List<ExpressionToken>(Tokens.Count + 2) { new OpenParenthesisToken() };
        tokens.AddRange(Tokens);
        tokens.Add(new CloseParenthesisToken());
        return new Operand(Value, tokens, null);
    }

    public static Operand Combine(Operand left, BinaryOperator op, Operand right, BigDecimal result)
    {
        var tokens = new List<ExpressionToken>(left.Tokens.Count + right.Tokens.Count + 1);
        tokens.AddRange(left.Tokens);
        tokens.Add(new OperatorToken(op));
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

        return new Operand(result, tokens, lowest);
    }
}
