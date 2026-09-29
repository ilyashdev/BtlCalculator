using Calculator.Core.Numerics;

namespace Calculator.Core.Engine;

/// <summary>
/// A structural piece of the expression line ("2 + sqr(3) ="). The engine produces tokens;
/// the UI turns them into localized text with <see cref="Formatting.ExpressionFormatter"/>.
/// </summary>
public abstract record ExpressionToken;

public sealed record NumberToken(BigDecimal Value) : ExpressionToken;

public sealed record ConstantToken(MathConstant Constant) : ExpressionToken;

public sealed record OperatorToken(BinaryOperator Operator) : ExpressionToken;

/// <summary>A function applied to an argument, e.g. sqr(5). <see cref="Unit"/> is set for trigonometric functions.</summary>
public sealed record FunctionToken(UnaryFunction Function, AngleUnit? Unit, IReadOnlyList<ExpressionToken> Argument) : ExpressionToken;

public sealed record OpenParenthesisToken : ExpressionToken;

public sealed record CloseParenthesisToken : ExpressionToken;

public sealed record EqualsToken : ExpressionToken;

public enum MathConstant
{
    Pi,
    E,
}
