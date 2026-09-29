using System.Globalization;

namespace Calculator.Core.Graphing;

/// <summary>Input errors; the names match the legacy resource keys of the error messages.</summary>
public enum GraphInputError
{
    EmptyExpression,
    UnexpectedEndOfExpression,
    UnexpectedToken,
    InvalidToken,
    ParenthesisMismatch,
    UnmatchedParenthesis,
    TooManyDecimalPoints,
    TooManyEquals,
    EqualWithoutGraphVariable,
    InvalidEquationSyntax,
    IncorrectNumParameter,
    ExpectParenthesisAfterFunctionName,
}

public sealed class GraphInputException(GraphInputError error) : Exception($"Invalid graph input: {error}.")
{
    public GraphInputError Error { get; } = error;
}

/// <summary>A parsed input line: an expression or a relation between two expressions.</summary>
public sealed record ParsedGraphInput(GraphNode Left, GraphRelation Relation, GraphNode? Right);

/// <summary>
/// Parses linear math input such as "y = 2x^2 - sin 3x", "x^2 + y^2 = 25" or "y > |x|".
/// Letters are split into function names, constants (pi, e) and single-letter variables; adjacent factors multiply.
/// </summary>
public static class GraphParser
{
    private static readonly (string Word, GraphFunction Function, bool NeedsParenthesis)[] FunctionWords =
    [
        ("sin", GraphFunction.Sin, false), ("cos", GraphFunction.Cos, false), ("tan", GraphFunction.Tan, false),
        ("sec", GraphFunction.Sec, false), ("csc", GraphFunction.Csc, false), ("cot", GraphFunction.Cot, false),
        ("arcsin", GraphFunction.Asin, false), ("arccos", GraphFunction.Acos, false), ("arctan", GraphFunction.Atan, false),
        ("arcsec", GraphFunction.Asec, false), ("arccsc", GraphFunction.Acsc, false), ("arccot", GraphFunction.Acot, false),
        ("asin", GraphFunction.Asin, true), ("acos", GraphFunction.Acos, true), ("atan", GraphFunction.Atan, true),
        ("sinh", GraphFunction.Sinh, false), ("cosh", GraphFunction.Cosh, false), ("tanh", GraphFunction.Tanh, false),
        ("sech", GraphFunction.Sech, false), ("csch", GraphFunction.Csch, false), ("coth", GraphFunction.Coth, false),
        ("arcsinh", GraphFunction.Asinh, false), ("arccosh", GraphFunction.Acosh, false), ("arctanh", GraphFunction.Atanh, false),
        ("arcsech", GraphFunction.Asech, false), ("arccsch", GraphFunction.Acsch, false), ("arccoth", GraphFunction.Acoth, false),
        ("abs", GraphFunction.Abs, true), ("floor", GraphFunction.Floor, true), ("ceil", GraphFunction.Ceiling, true),
        ("ceiling", GraphFunction.Ceiling, true), ("round", GraphFunction.Round, true), ("sign", GraphFunction.Sign, true),
        ("sgn", GraphFunction.Sign, true), ("sqrt", GraphFunction.Sqrt, false), ("cbrt", GraphFunction.Cbrt, true),
        ("root", GraphFunction.Root, true), ("log", GraphFunction.Log, false), ("ln", GraphFunction.Ln, false),
        ("exp", GraphFunction.Exp, true), ("min", GraphFunction.Min, true), ("max", GraphFunction.Max, true),
        ("mod", GraphFunction.Mod, true),
    ];

    /// <param name="decimalSeparator">'.' or ','. With ',' arguments are separated by ';'.</param>
    public static ParsedGraphInput Parse(string input, char decimalSeparator = '.')
    {
        List<Token> tokens = Tokenize(input, decimalSeparator);
        var parser = new Parser(tokens);
        return parser.ParseInput();
    }

    private enum TokenKind
    {
        Number,
        Letter,
        Function,
        Plus,
        Minus,
        Times,
        Divide,
        Caret,
        Underscore,
        Bang,
        Open,
        Close,
        Bar,
        Comma,
        Relation,
        End,
    }

    private sealed record Token(TokenKind Kind, double Number = 0, string Text = "", GraphFunction Function = default, GraphRelation Relation = GraphRelation.None);

    private static List<Token> Tokenize(string input, char decimalSeparator)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < input.Length)
        {
            char c = input[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (char.IsAsciiDigit(c) || c == decimalSeparator)
            {
                int start = i;
                int points = 0;
                while (i < input.Length && (char.IsAsciiDigit(input[i]) || input[i] == decimalSeparator))
                {
                    points += input[i] == decimalSeparator ? 1 : 0;
                    i++;
                }

                string text = input[start..i].Replace(decimalSeparator, '.');
                if (points > 1)
                {
                    throw new GraphInputException(GraphInputError.TooManyDecimalPoints);
                }

                if (text == ".")
                {
                    throw new GraphInputException(GraphInputError.InvalidToken);
                }

                tokens.Add(new Token(TokenKind.Number, double.Parse(text, CultureInfo.InvariantCulture)));
                continue;
            }

            if (char.IsLetter(c) || c == 'π')
            {
                int start = i;
                while (i < input.Length && (char.IsLetter(input[i]) || input[i] == 'π'))
                {
                    i++;
                }

                AddLetterRun(tokens, input[start..i].ToLowerInvariant(), i < input.Length && input[i] is '(' or '[' or '{');
                continue;
            }

            i++;
            string twoCharacters = i < input.Length ? string.Concat(c, input[i]) : string.Empty;
            if (twoCharacters is "<=" or ">=")
            {
                tokens.Add(new Token(TokenKind.Relation, Relation: twoCharacters == "<=" ? GraphRelation.LessOrEqual : GraphRelation.GreaterOrEqual));
                i++;
                continue;
            }

            tokens.Add(c switch
            {
                '+' => new Token(TokenKind.Plus),
                // Hyphen, minus sign and the en and em dashes that editors and web pages put in formulas instead.
                '-' or '−' or '–' or '—' => new Token(TokenKind.Minus),
                '*' or '×' or '·' or '⋅' => new Token(TokenKind.Times),
                '/' or '÷' => new Token(TokenKind.Divide),
                '^' => new Token(TokenKind.Caret),
                '_' => new Token(TokenKind.Underscore),
                '!' => new Token(TokenKind.Bang),
                '(' or '[' or '{' => new Token(TokenKind.Open, Text: c.ToString()),
                ')' or ']' or '}' => new Token(TokenKind.Close, Text: c.ToString()),
                '|' => new Token(TokenKind.Bar),
                ';' => new Token(TokenKind.Comma),
                ',' when decimalSeparator != ',' => new Token(TokenKind.Comma),
                '=' => new Token(TokenKind.Relation, Relation: GraphRelation.Equal),
                '<' => new Token(TokenKind.Relation, Relation: GraphRelation.Less),
                '>' => new Token(TokenKind.Relation, Relation: GraphRelation.Greater),
                '≤' => new Token(TokenKind.Relation, Relation: GraphRelation.LessOrEqual),
                '≥' => new Token(TokenKind.Relation, Relation: GraphRelation.GreaterOrEqual),
                '√' => new Token(TokenKind.Function, Function: GraphFunction.Sqrt),
                '∞' => new Token(TokenKind.Number, double.PositiveInfinity),
                _ => throw new GraphInputException(GraphInputError.InvalidToken),
            });
        }

        tokens.Add(new Token(TokenKind.End));
        return tokens;
    }

    /// <summary>Splits "asinx" into function names, constants and single-letter variables, longest function name first.</summary>
    private static void AddLetterRun(List<Token> tokens, string run, bool followedByParenthesis)
    {
        int position = 0;
        while (position < run.Length)
        {
            (string Word, GraphFunction Function, bool NeedsParenthesis)? best = null;
            foreach (var entry in FunctionWords)
            {
                bool matches = string.CompareOrdinal(run, position, entry.Word, 0, entry.Word.Length) == 0;
                bool allowed = !entry.NeedsParenthesis || (position + entry.Word.Length == run.Length && followedByParenthesis);
                if (matches && allowed && (best is null || entry.Word.Length > best.Value.Word.Length))
                {
                    best = entry;
                }
            }

            if (best is { } function)
            {
                tokens.Add(new Token(TokenKind.Function, Function: function.Function));
                position += function.Word.Length;
            }
            else if (string.CompareOrdinal(run, position, "pi", 0, 2) == 0)
            {
                tokens.Add(new Token(TokenKind.Number, Math.PI));
                position += 2;
            }
            else
            {
                tokens.Add(new Token(TokenKind.Letter, Text: run[position].ToString()));
                position++;
            }
        }
    }

    private sealed class Parser(List<Token> tokens)
    {
        private int _position;
        private int _absoluteValueDepth;

        public ParsedGraphInput ParseInput()
        {
            if (Peek.Kind == TokenKind.End)
            {
                throw new GraphInputException(GraphInputError.EmptyExpression);
            }

            GraphNode left = ParseExpression();
            GraphRelation relation = GraphRelation.None;
            GraphNode? right = null;
            if (Peek.Kind == TokenKind.Relation)
            {
                relation = Next().Relation;
                right = ParseExpression();
                if (Peek.Kind == TokenKind.Relation)
                {
                    throw new GraphInputException(GraphInputError.TooManyEquals);
                }
            }

            if (Peek.Kind != TokenKind.End)
            {
                throw new GraphInputException(Peek.Kind == TokenKind.Close ? GraphInputError.ParenthesisMismatch : GraphInputError.UnexpectedToken);
            }

            return new ParsedGraphInput(left, relation, right);
        }

        private Token Peek => tokens[_position];

        private Token Next()
        {
            Token token = tokens[_position];
            if (token.Kind != TokenKind.End)
            {
                _position++;
            }

            return token;
        }

        private bool StartsOperand(Token token) => token.Kind switch
        {
            TokenKind.Number or TokenKind.Letter or TokenKind.Function or TokenKind.Open => true,
            TokenKind.Bar => _absoluteValueDepth == 0,
            _ => false,
        };

        private GraphNode ParseExpression()
        {
            GraphNode left = ParseTerm();
            while (Peek.Kind is TokenKind.Plus or TokenKind.Minus)
            {
                GraphOperator op = Next().Kind == TokenKind.Plus ? GraphOperator.Add : GraphOperator.Subtract;
                left = new BinaryNode(op, left, ParseTerm());
            }

            return left;
        }

        private GraphNode ParseTerm()
        {
            GraphNode left = ParseUnary();
            while (true)
            {
                if (Peek.Kind is TokenKind.Times or TokenKind.Divide)
                {
                    GraphOperator op = Next().Kind == TokenKind.Times ? GraphOperator.Multiply : GraphOperator.Divide;
                    left = new BinaryNode(op, left, ParseUnary());
                }
                else if (StartsOperand(Peek))
                {
                    left = new BinaryNode(GraphOperator.Multiply, left, ParsePower());
                }
                else
                {
                    return left;
                }
            }
        }

        private GraphNode ParseUnary()
        {
            if (Peek.Kind == TokenKind.Minus)
            {
                Next();
                return new NegateNode(ParseUnary());
            }

            if (Peek.Kind == TokenKind.Plus)
            {
                Next();
                return ParseUnary();
            }

            return ParsePower();
        }

        private GraphNode ParsePower()
        {
            GraphNode baseNode = ParsePostfix();
            if (Peek.Kind == TokenKind.Caret)
            {
                Next();
                return new BinaryNode(GraphOperator.Power, baseNode, ParseUnary());
            }

            return baseNode;
        }

        private GraphNode ParsePostfix()
        {
            GraphNode operand = ParsePrimary();
            while (Peek.Kind == TokenKind.Bang)
            {
                Next();
                operand = new FactorialNode(operand);
            }

            return operand;
        }

        private GraphNode ParsePrimary()
        {
            Token token = Next();
            switch (token.Kind)
            {
                case TokenKind.Number:
                    return new NumberNode(token.Number);
                case TokenKind.Letter:
                    return token.Text switch
                    {
                        "x" => new XNode(),
                        "y" => new YNode(),
                        "e" => new NumberNode(Math.E),
                        _ => new ParameterNode(token.Text),
                    };
                case TokenKind.Function:
                    return ParseFunction(token.Function);
                case TokenKind.Open:
                {
                    int savedDepth = _absoluteValueDepth;
                    _absoluteValueDepth = 0;
                    GraphNode inner = ParseExpression();
                    _absoluteValueDepth = savedDepth;
                    ExpectClosing(token.Text);
                    return inner;
                }

                case TokenKind.Bar:
                {
                    _absoluteValueDepth++;
                    GraphNode inner = ParseExpression();
                    _absoluteValueDepth--;
                    if (Peek.Kind != TokenKind.Bar)
                    {
                        throw new GraphInputException(Peek.Kind == TokenKind.End ? GraphInputError.UnexpectedEndOfExpression : GraphInputError.UnexpectedToken);
                    }

                    Next();
                    return new FunctionNode(GraphFunction.Abs, [inner]);
                }

                case TokenKind.End:
                    throw new GraphInputException(GraphInputError.UnexpectedEndOfExpression);
                case TokenKind.Close:
                    throw new GraphInputException(GraphInputError.ParenthesisMismatch);
                default:
                    throw new GraphInputException(GraphInputError.UnexpectedToken);
            }
        }

        private void ExpectClosing(string open)
        {
            Token token = Peek;
            if (token.Kind == TokenKind.End)
            {
                throw new GraphInputException(GraphInputError.UnmatchedParenthesis);
            }

            string expected = open switch
            {
                "[" => "]",
                "{" => "}",
                _ => ")",
            };

            if (token.Kind != TokenKind.Close || token.Text != expected)
            {
                throw new GraphInputException(token.Kind == TokenKind.Close ? GraphInputError.ParenthesisMismatch : GraphInputError.UnexpectedToken);
            }

            Next();
        }

        private GraphNode ParseFunction(GraphFunction function)
        {
            // log_b x
            GraphNode? logBase = null;
            if (Peek.Kind == TokenKind.Underscore)
            {
                Next();
                logBase = ParsePrimary();
            }

            // sin^2 x, and sin^-1 x as the inverse
            GraphNode? power = null;
            if (Peek.Kind == TokenKind.Caret)
            {
                Next();
                power = ParseUnary();
                if (IsMinusOne(power) && Inverse(function) is GraphFunction inverse)
                {
                    function = inverse;
                    power = null;
                }
            }

            var arguments = new List<GraphNode>();
            if (Peek.Kind == TokenKind.Open)
            {
                string open = Next().Text;
                int savedDepth = _absoluteValueDepth;
                _absoluteValueDepth = 0;
                arguments.Add(ParseExpression());
                while (Peek.Kind == TokenKind.Comma)
                {
                    Next();
                    arguments.Add(ParseExpression());
                }

                _absoluteValueDepth = savedDepth;
                ExpectClosing(open);
            }
            else if (StartsOperand(Peek))
            {
                // "sin 2x" is sin(2x); the implicit argument stops at the next function ("sin x cos x").
                GraphNode argument = ParsePower();
                while (StartsOperand(Peek) && Peek.Kind is not (TokenKind.Function or TokenKind.Open))
                {
                    argument = new BinaryNode(GraphOperator.Multiply, argument, ParsePower());
                }

                arguments.Add(argument);
            }
            else
            {
                throw new GraphInputException(Peek.Kind == TokenKind.End
                    ? GraphInputError.UnexpectedEndOfExpression
                    : GraphInputError.ExpectParenthesisAfterFunctionName);
            }

            if (logBase is not null)
            {
                if (function != GraphFunction.Log || arguments.Count != 1)
                {
                    throw new GraphInputException(GraphInputError.IncorrectNumParameter);
                }

                arguments.Insert(0, logBase);
                function = GraphFunction.LogBase;
            }
            else if (function == GraphFunction.Log && arguments.Count == 2)
            {
                function = GraphFunction.LogBase;
            }

            ValidateArgumentCount(function, arguments.Count);

            GraphNode call = new FunctionNode(function, arguments);
            return power is null ? call : new BinaryNode(GraphOperator.Power, call, power);
        }

        private static void ValidateArgumentCount(GraphFunction function, int count)
        {
            bool valid = function switch
            {
                GraphFunction.LogBase or GraphFunction.Mod => count == 2,
                GraphFunction.Root => count is 1 or 2,
                GraphFunction.Min or GraphFunction.Max => count >= 1,
                _ => count == 1,
            };

            if (!valid)
            {
                throw new GraphInputException(GraphInputError.IncorrectNumParameter);
            }
        }

        private static bool IsMinusOne(GraphNode node) =>
            node is NumberNode { Value: -1 } || node is NegateNode { Operand: NumberNode { Value: 1 } };

        private static GraphFunction? Inverse(GraphFunction function) => function switch
        {
            GraphFunction.Sin => GraphFunction.Asin,
            GraphFunction.Cos => GraphFunction.Acos,
            GraphFunction.Tan => GraphFunction.Atan,
            GraphFunction.Sec => GraphFunction.Asec,
            GraphFunction.Csc => GraphFunction.Acsc,
            GraphFunction.Cot => GraphFunction.Acot,
            GraphFunction.Sinh => GraphFunction.Asinh,
            GraphFunction.Cosh => GraphFunction.Acosh,
            GraphFunction.Tanh => GraphFunction.Atanh,
            GraphFunction.Sech => GraphFunction.Asech,
            GraphFunction.Csch => GraphFunction.Acsch,
            GraphFunction.Coth => GraphFunction.Acoth,
            _ => null,
        };
    }
}
