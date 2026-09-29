namespace Calculator.Core.Graphing;

public enum GraphFunction
{
    Sin, Cos, Tan, Sec, Csc, Cot,
    Asin, Acos, Atan, Asec, Acsc, Acot,
    Sinh, Cosh, Tanh, Sech, Csch, Coth,
    Asinh, Acosh, Atanh, Asech, Acsch, Acoth,
    Abs, Floor, Ceiling, Round, Sign,
    Sqrt, Cbrt, Root, Log, LogBase, Ln, Exp,
    Min, Max, Mod,
}

public enum GraphRelation
{
    None,
    Equal,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
}

public enum TrigonometricUnit
{
    Radians,
    Degrees,
    Gradians,
}

/// <summary>Values of the variables while evaluating: x, y and the parameters (a, b, …).</summary>
public sealed class GraphEvaluationContext
{
    public double X { get; set; }

    public double Y { get; set; }

    public TrigonometricUnit TrigonometricUnit { get; set; } = TrigonometricUnit.Radians;

    public IReadOnlyDictionary<string, double> Parameters { get; set; } = new Dictionary<string, double>();
}

/// <summary>An expression tree evaluated in double precision (enough for drawing graphs).</summary>
public abstract record GraphNode
{
    public abstract double Evaluate(GraphEvaluationContext context);

    /// <summary>True when the expression refers to x (or y, see <see cref="UsesY"/>).</summary>
    public abstract bool UsesX { get; }

    public abstract bool UsesY { get; }

    /// <summary>Adds the parameter names used by the expression.</summary>
    public abstract void CollectParameters(ISet<string> names);
}

public sealed record NumberNode(double Value) : GraphNode
{
    public override double Evaluate(GraphEvaluationContext context) => Value;

    public override bool UsesX => false;

    public override bool UsesY => false;

    public override void CollectParameters(ISet<string> names)
    {
    }
}

public sealed record XNode : GraphNode
{
    public override double Evaluate(GraphEvaluationContext context) => context.X;

    public override bool UsesX => true;

    public override bool UsesY => false;

    public override void CollectParameters(ISet<string> names)
    {
    }
}

public sealed record YNode : GraphNode
{
    public override double Evaluate(GraphEvaluationContext context) => context.Y;

    public override bool UsesX => false;

    public override bool UsesY => true;

    public override void CollectParameters(ISet<string> names)
    {
    }
}

public sealed record ParameterNode(string Name) : GraphNode
{
    public override double Evaluate(GraphEvaluationContext context) =>
        context.Parameters.TryGetValue(Name, out double value) ? value : double.NaN;

    public override bool UsesX => false;

    public override bool UsesY => false;

    public override void CollectParameters(ISet<string> names) => names.Add(Name);
}

public enum GraphOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Power,
}

public sealed record BinaryNode(GraphOperator Operator, GraphNode Left, GraphNode Right) : GraphNode
{
    public override double Evaluate(GraphEvaluationContext context)
    {
        double left = Left.Evaluate(context);
        double right = Right.Evaluate(context);
        return Operator switch
        {
            GraphOperator.Add => left + right,
            GraphOperator.Subtract => left - right,
            GraphOperator.Multiply => left * right,
            GraphOperator.Divide => right == 0 ? double.NaN : left / right,
            GraphOperator.Power => GraphMath.RealPower(left, right),
            _ => double.NaN,
        };
    }

    public override bool UsesX => Left.UsesX || Right.UsesX;

    public override bool UsesY => Left.UsesY || Right.UsesY;

    public override void CollectParameters(ISet<string> names)
    {
        Left.CollectParameters(names);
        Right.CollectParameters(names);
    }
}

public sealed record NegateNode(GraphNode Operand) : GraphNode
{
    public override double Evaluate(GraphEvaluationContext context) => -Operand.Evaluate(context);

    public override bool UsesX => Operand.UsesX;

    public override bool UsesY => Operand.UsesY;

    public override void CollectParameters(ISet<string> names) => Operand.CollectParameters(names);
}

public sealed record FactorialNode(GraphNode Operand) : GraphNode
{
    public override double Evaluate(GraphEvaluationContext context) => GraphMath.Factorial(Operand.Evaluate(context));

    public override bool UsesX => Operand.UsesX;

    public override bool UsesY => Operand.UsesY;

    public override void CollectParameters(ISet<string> names) => Operand.CollectParameters(names);
}

public sealed record FunctionNode(GraphFunction Function, IReadOnlyList<GraphNode> Arguments) : GraphNode
{
    public override double Evaluate(GraphEvaluationContext context)
    {
        var values = new double[Arguments.Count];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = Arguments[i].Evaluate(context);
        }

        return GraphMath.Apply(Function, values, context.TrigonometricUnit);
    }

    public override bool UsesX => Arguments.Any(argument => argument.UsesX);

    public override bool UsesY => Arguments.Any(argument => argument.UsesY);

    public override void CollectParameters(ISet<string> names)
    {
        foreach (GraphNode argument in Arguments)
        {
            argument.CollectParameters(names);
        }
    }
}
