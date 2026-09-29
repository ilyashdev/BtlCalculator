namespace Calculator.Core.Graphing;

public enum GraphEquationKind
{
    /// <summary>y = f(x)</summary>
    FunctionOfX,

    /// <summary>x = g(y)</summary>
    FunctionOfY,

    /// <summary>F(x, y) = 0</summary>
    Implicit,

    /// <summary>y &lt;rel&gt; f(x)</summary>
    InequalityY,

    /// <summary>x &lt;rel&gt; g(y)</summary>
    InequalityX,

    /// <summary>G(x, y) &lt;rel&gt; 0</summary>
    InequalityImplicit,
}

/// <summary>
/// A graphable equation. <see cref="Expression"/> is f(x), g(y), F(x, y) or G(x, y) depending on <see cref="Kind"/>;
/// for inequalities <see cref="Relation"/> compares y (or x, or G) with it.
/// </summary>
public sealed record GraphEquation(GraphEquationKind Kind, GraphNode Expression, GraphRelation Relation)
{
    public bool IsInequality => Kind is GraphEquationKind.InequalityX or GraphEquationKind.InequalityY or GraphEquationKind.InequalityImplicit;

    /// <summary>Parameter names such as "a" in y = ax².</summary>
    public IReadOnlySet<string> Parameters
    {
        get
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            Expression.CollectParameters(names);
            return names;
        }
    }

    /// <summary>Parses and classifies one input line (like the legacy GetRequest + graph engine classification).</summary>
    public static GraphEquation Parse(string input, char decimalSeparator = '.')
    {
        ParsedGraphInput parsed = GraphParser.Parse(input, decimalSeparator);
        return Classify(parsed);
    }

    private static GraphEquation Classify(ParsedGraphInput parsed)
    {
        if (parsed.Right is null)
        {
            // A plain expression: y = expr, or x = expr when it only uses y.
            GraphNode expression = parsed.Left;
            if (expression.UsesX && expression.UsesY)
            {
                throw new GraphInputException(GraphInputError.InvalidEquationSyntax);
            }

            GraphEquationKind kind = expression.UsesY ? GraphEquationKind.FunctionOfY : GraphEquationKind.FunctionOfX;
            return new GraphEquation(kind, expression, GraphRelation.Equal);
        }

        GraphNode left = parsed.Left;
        GraphNode right = parsed.Right;
        GraphRelation relation = parsed.Relation;
        if (!left.UsesX && !left.UsesY && !right.UsesX && !right.UsesY)
        {
            throw new GraphInputException(GraphInputError.EqualWithoutGraphVariable);
        }

        bool equality = relation == GraphRelation.Equal;

        // y = f(x), f(x) = y, y > f(x), …
        if (left is YNode && !right.UsesY)
        {
            return new GraphEquation(equality ? GraphEquationKind.FunctionOfX : GraphEquationKind.InequalityY, right, relation);
        }

        if (right is YNode && !left.UsesY)
        {
            return new GraphEquation(equality ? GraphEquationKind.FunctionOfX : GraphEquationKind.InequalityY, left, Flip(relation));
        }

        // x = g(y), g(y) = x, …
        if (left is XNode && !right.UsesX)
        {
            return new GraphEquation(equality ? GraphEquationKind.FunctionOfY : GraphEquationKind.InequalityX, right, relation);
        }

        if (right is XNode && !left.UsesX)
        {
            return new GraphEquation(equality ? GraphEquationKind.FunctionOfY : GraphEquationKind.InequalityX, left, Flip(relation));
        }

        // General case: left − right = 0 (or <rel> 0)
        GraphNode difference = new BinaryNode(GraphOperator.Subtract, left, right);
        return new GraphEquation(equality ? GraphEquationKind.Implicit : GraphEquationKind.InequalityImplicit, difference, relation);
    }

    private static GraphRelation Flip(GraphRelation relation) => relation switch
    {
        GraphRelation.Less => GraphRelation.Greater,
        GraphRelation.LessOrEqual => GraphRelation.GreaterOrEqual,
        GraphRelation.Greater => GraphRelation.Less,
        GraphRelation.GreaterOrEqual => GraphRelation.LessOrEqual,
        _ => relation,
    };
}
