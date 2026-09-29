using Calculator.Core.Engine;
using Calculator.Core.Numerics;

namespace Calculator.Core.Formatting;

/// <summary>The expression names of Windows Calculator in English.</summary>
public sealed class EnglishExpressionVocabulary : IExpressionVocabulary
{
    public string OperatorSymbol(BinaryOperator op) => op switch
    {
        BinaryOperator.Add => "+",
        BinaryOperator.Subtract => "-",
        BinaryOperator.Multiply => "×",
        BinaryOperator.Divide => "÷",
        BinaryOperator.Modulo => "Mod",
        BinaryOperator.Power => "^",
        BinaryOperator.Root => "yroot",
        BinaryOperator.LogBase => "log base",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, null),
    };

    public string ConstantSymbol(MathConstant constant) => constant switch
    {
        MathConstant.Pi => "π",
        MathConstant.E => "e",
        _ => throw new ArgumentOutOfRangeException(nameof(constant), constant, null),
    };

    public string FunctionName(UnaryFunction function, AngleUnit? unit)
    {
        string suffix = unit switch
        {
            AngleUnit.Degrees => "₀",
            AngleUnit.Radians => "ᵣ",
            AngleUnit.Gradians => "₉",
            _ => string.Empty,
        };

        return function switch
        {
            UnaryFunction.Negate => "negate",
            UnaryFunction.Square => "sqr",
            UnaryFunction.Cube => "cube",
            UnaryFunction.SquareRoot => "√",
            UnaryFunction.CubeRoot => "cuberoot",
            UnaryFunction.Reciprocal => "1/",
            UnaryFunction.Abs => "abs",
            UnaryFunction.Floor => "floor",
            UnaryFunction.Ceiling => "ceil",
            UnaryFunction.Factorial => "fact",
            UnaryFunction.PowerOfTen => "10^",
            UnaryFunction.PowerOfTwo => "2^",
            UnaryFunction.PowerOfE => "e^",
            UnaryFunction.Log10 => "log",
            UnaryFunction.Ln => "ln",
            UnaryFunction.ToDegreesMinutesSeconds => "dms",
            UnaryFunction.FromDegreesMinutesSeconds => "degrees",
            UnaryFunction.Sin => "sin" + suffix,
            UnaryFunction.Cos => "cos" + suffix,
            UnaryFunction.Tan => "tan" + suffix,
            UnaryFunction.Sec => "sec" + suffix,
            UnaryFunction.Csc => "csc" + suffix,
            UnaryFunction.Cot => "cot" + suffix,
            UnaryFunction.Asin => "sin" + suffix + "⁻¹",
            UnaryFunction.Acos => "cos" + suffix + "⁻¹",
            UnaryFunction.Atan => "tan" + suffix + "⁻¹",
            UnaryFunction.Asec => "sec" + suffix + "⁻¹",
            UnaryFunction.Acsc => "csc" + suffix + "⁻¹",
            UnaryFunction.Acot => "cot" + suffix + "⁻¹",
            UnaryFunction.Sinh => "sinh",
            UnaryFunction.Cosh => "cosh",
            UnaryFunction.Tanh => "tanh",
            UnaryFunction.Sech => "sech",
            UnaryFunction.Csch => "csch",
            UnaryFunction.Coth => "coth",
            UnaryFunction.Asinh => "sinh⁻¹",
            UnaryFunction.Acosh => "cosh⁻¹",
            UnaryFunction.Atanh => "tanh⁻¹",
            UnaryFunction.Asech => "sech⁻¹",
            UnaryFunction.Acsch => "csch⁻¹",
            UnaryFunction.Acoth => "coth⁻¹",
            _ => throw new ArgumentOutOfRangeException(nameof(function), function, null),
        };
    }
}
