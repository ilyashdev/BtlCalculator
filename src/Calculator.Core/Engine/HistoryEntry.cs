using Calculator.Core.Numerics;

namespace Calculator.Core.Engine;

/// <summary>A finished calculation: the expression line (ending with "=") and its result.</summary>
public sealed record HistoryEntry(IReadOnlyList<ExpressionToken> Expression, BigDecimal Result);
