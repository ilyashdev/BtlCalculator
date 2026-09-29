using Calculator.Core.Numerics;

namespace Calculator.Core.Engine;

/// <summary>
/// The standard/scientific calculator. It receives key presses and exposes what to display.
///
/// The expression being built is kept as a stack of parenthesis levels. Each level is a list of
/// pending operations "left operand + operator". When an operator is pressed, pending operations
/// with the same or higher precedence are evaluated first (with <see cref="CalculatorOptions.UsePrecedence"/>
/// switched off every operator has the same precedence, which gives the immediate execution of the standard mode).
/// </summary>
public sealed class CalculatorEngine
{
    private const int MaxParenthesisDepth = 25;

    private readonly CalculatorOptions _options;
    private readonly IRandomSource _random;
    private readonly List<List<PendingOperation>> _levels = [[]];

    private EntryState _state = EntryState.Start;
    private NumberInput _input;
    private Operand _current = Operand.FromNumber(BigDecimal.Zero);
    private bool _currentIsParenthesizedGroup;
    private RepeatOperation? _repeat;
    private IReadOnlyList<ExpressionToken>? _completedExpression;
    private CalculationError? _error;

    public CalculatorEngine(CalculatorOptions options, IRandomSource? random = null)
    {
        _options = options;
        _random = random ?? new SystemRandomSource();
        _input = new NumberInput(options.MaxInputDigits);
    }

    /// <summary>Raised after every command that may have changed what is displayed.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when "=" completes a calculation that should be recorded in the history.</summary>
    public event EventHandler<HistoryEntry>? CalculationCompleted;

    public CalculatorOptions Options => _options;

    public AngleUnit AngleUnit { get; set; } = AngleUnit.Degrees;

    /// <summary>True while a number is being typed; the display then shows <see cref="Input"/>.</summary>
    public bool IsTyping => _state == EntryState.Typing;

    public NumberInput Input => _input;

    /// <summary>The value shown in the main display (the typed number while typing).</summary>
    public BigDecimal DisplayValue => IsTyping ? _input.ToValue() : _current.Value;

    /// <summary>Set when the last command failed; the display shows an error message until the next input.</summary>
    public CalculationError? Error => _error;

    public int OpenParenthesisCount => _levels.Count - 1;

    /// <summary>The expression line shown above the display.</summary>
    public IReadOnlyList<ExpressionToken> Expression => _completedExpression ?? BuildPendingExpression();

    public void EnterDigit(int digit)
    {
        PrepareForNewOperand();

        if (_state != EntryState.Typing)
        {
            BeginTyping();
        }

        _input.TryAppendDigit(digit);
        NotifyChanged();
    }

    public void EnterDecimalPoint()
    {
        PrepareForNewOperand();

        if (_state != EntryState.Typing)
        {
            BeginTyping();
        }

        _input.TryAddDecimalPoint();
        NotifyChanged();
    }

    /// <summary>Starts the exponent of the typed number ("Exp" key): 1.5 Exp 3 = 1500.</summary>
    public void BeginExponent()
    {
        if (_error is not null)
        {
            return;
        }

        PrepareForNewOperand();
        if (_state != EntryState.Typing)
        {
            BeginTyping();
        }

        _input.TryBeginExponent();
        NotifyChanged();
    }

    public void ApplyBinaryOperator(BinaryOperator op)
    {
        if (_error is not null)
        {
            return;
        }

        Run(() =>
        {
            if (_state == EntryState.AfterOperator)
            {
                // Pressing another operator replaces the previous one.
                PendingOperation previous = PopPending();
                PushOperation(previous.Left, op);
                return;
            }

            if (_state == EntryState.Result)
            {
                StartNewExpression();
            }

            PushOperation(TakeCurrentOperand(), op);
        });
    }

    public void ApplyUnaryFunction(UnaryFunction function)
    {
        if (_error is not null)
        {
            return;
        }

        Run(() =>
        {
            Operand operand = TakeCurrentOperand();
            if (_state == EntryState.Result)
            {
                StartNewExpression();
            }

            BigDecimal result = function.Apply(operand.Value, AngleUnit, _options.Precision);
            AngleUnit? unit = function.UsesAngleUnit() ? AngleUnit : null;
            SetCurrent(operand.WithFunction(function, unit, result));
        });
    }

    /// <summary>
    /// Percent: after + or − it is a percentage of the left operand (200 + 10% → 200 + 20),
    /// after × or ÷ it is a fraction (200 × 10% → 200 × 0.1), and 0 without a pending operator.
    /// </summary>
    public void ApplyPercent()
    {
        if (_error is not null)
        {
            return;
        }

        Run(() =>
        {
            Operand operand = TakeCurrentOperand();
            if (_state == EntryState.Result)
            {
                StartNewExpression();
            }

            List<PendingOperation> level = _levels[^1];
            BigDecimal result;
            if (level.Count == 0)
            {
                result = BigDecimal.Zero;
            }
            else
            {
                PendingOperation pending = level[^1];
                BigDecimal fraction = operand.Value.ScaleByPowerOfTen(-2);
                result = pending.Operator is BinaryOperator.Add or BinaryOperator.Subtract
                    ? pending.Left.Value * fraction
                    : fraction;
            }

            SetCurrent(Operand.FromNumber(BigMath.EnsureInRange(result.RoundToSignificantDigits(_options.Precision))));
        });
    }

    /// <summary>"+/−": changes the sign of the typed number, or applies negate() to a computed value.</summary>
    public void Negate()
    {
        if (_error is not null)
        {
            return;
        }

        if (_state == EntryState.Typing)
        {
            _input.ToggleSign();
            NotifyChanged();
            return;
        }

        ApplyUnaryFunction(UnaryFunction.Negate);
    }

    public void OpenParenthesis()
    {
        if (_error is not null || !_options.UsePrecedence || OpenParenthesisCount >= MaxParenthesisDepth)
        {
            return;
        }

        Run(() =>
        {
            if (_state == EntryState.Result)
            {
                StartNewExpression();
            }
            else if (_state is EntryState.Typing or EntryState.Operand)
            {
                // "2(" means "2 × (".
                PushOperation(TakeCurrentOperand(), BinaryOperator.Multiply);
            }

            _levels.Add([]);
            SetCurrent(Operand.FromNumber(BigDecimal.Zero));
            _state = EntryState.Start;
        });
    }

    public void CloseParenthesis()
    {
        if (_error is not null || OpenParenthesisCount == 0)
        {
            return;
        }

        Run(() =>
        {
            Operand group = ReduceLevel(_levels[^1], TakeCurrentOperand()).Parenthesized();
            _levels.RemoveAt(_levels.Count - 1);
            SetCurrent(group);
            _currentIsParenthesizedGroup = true;
        });
    }

    public void Evaluate()
    {
        if (_error is not null)
        {
            return;
        }

        Run(() =>
        {
            if (_state == EntryState.Result)
            {
                RepeatLastOperation();
                return;
            }

            Operand operand = TakeCurrentOperand();
            bool hasOperation = _levels.Any(level => level.Count > 0) || operand.IsComposite || OpenParenthesisCount > 0;

            _repeat = FindLastPending() is PendingOperation last ? new RepeatOperation(last.Operator, operand.Value) : null;

            while (OpenParenthesisCount > 0)
            {
                operand = ReduceLevel(_levels[^1], operand).Parenthesized();
                _levels.RemoveAt(_levels.Count - 1);
            }

            Operand result = ReduceLevel(_levels[0], operand);
            _levels[0].Clear();
            CompleteCalculation(result.Tokens, result.Value, hasOperation);
        });
    }

    /// <summary>"C": clears everything except the angle unit.</summary>
    public void Clear()
    {
        ResetExpression();
        _error = null;
        SetCurrent(Operand.FromNumber(BigDecimal.Zero));
        _state = EntryState.Start;
        NotifyChanged();
    }

    /// <summary>"CE": clears the current entry; after "=" or an error it clears everything.</summary>
    public void ClearEntry()
    {
        if (_error is not null || _state == EntryState.Result)
        {
            Clear();
            return;
        }

        SetCurrent(Operand.FromNumber(BigDecimal.Zero));
        _state = EntryState.Start;
        NotifyChanged();
    }

    public void Backspace()
    {
        if (_error is not null)
        {
            Clear();
            return;
        }

        if (_state == EntryState.Typing)
        {
            _input.Backspace();
        }
        else if (_state == EntryState.Result)
        {
            // After "=" backspace only clears the expression line.
            _completedExpression = [];
        }

        NotifyChanged();
    }

    public void InsertConstant(MathConstant constant)
    {
        BigDecimal value = constant switch
        {
            MathConstant.Pi => BigMath.Pi(_options.Precision),
            MathConstant.E => BigMath.E(_options.Precision),
            _ => throw new ArgumentOutOfRangeException(nameof(constant), constant, null),
        };

        SetOperand(Operand.FromToken(value, new ConstantToken(constant)));
    }

    /// <summary>"rand": a uniformly distributed number in [0, 1).</summary>
    public void InsertRandom() => SetOperand(Operand.FromNumber(_random.Next(_options.MaxInputDigits)));

    /// <summary>Puts a value (memory recall, pasted number) into the display as the current operand.</summary>
    public void SetValue(BigDecimal value) => SetOperand(Operand.FromNumber(value.RoundToSignificantDigits(_options.Precision)));

    /// <summary>Shows a history entry as if it had just been calculated.</summary>
    public void LoadHistoryEntry(HistoryEntry entry)
    {
        ResetExpression();
        _error = null;
        SetCurrent(Operand.FromNumber(entry.Result));
        _completedExpression = entry.Expression;
        _state = EntryState.Result;
        NotifyChanged();
    }

    private void SetOperand(Operand operand)
    {
        if (_error is not null)
        {
            Clear();
        }

        if (_state == EntryState.Result)
        {
            StartNewExpression();
        }

        SetCurrent(operand);
        NotifyChanged();
    }

    private void RepeatLastOperation()
    {
        if (_repeat is not RepeatOperation repeat)
        {
            return;
        }

        Operand left = Operand.FromNumber(_current.Value);
        Operand right = Operand.FromNumber(repeat.Right);
        BigDecimal value = repeat.Operator.Apply(left.Value, right.Value, _options.Precision);
        Operand result = Operand.Combine(left, repeat.Operator, right, value);
        CompleteCalculation(result.Tokens, value, hasOperation: true);
    }

    private void CompleteCalculation(IReadOnlyList<ExpressionToken> tokens, BigDecimal value, bool hasOperation)
    {
        var expression = new List<ExpressionToken>(tokens) { new EqualsToken() };
        _completedExpression = expression;
        SetCurrent(Operand.FromNumber(value));
        _state = EntryState.Result;

        if (hasOperation)
        {
            CalculationCompleted?.Invoke(this, new HistoryEntry(expression, value));
        }
    }

    /// <summary>Called before a new number starts: clears errors and finished calculations.</summary>
    private void PrepareForNewOperand()
    {
        if (_error is not null)
        {
            Clear();
        }

        if (_state == EntryState.Result)
        {
            StartNewExpression();
        }
        else if (_state == EntryState.Operand && _currentIsParenthesizedGroup)
        {
            // "(2 + 3)4" means "(2 + 3) × 4".
            Run(() => PushOperation(TakeCurrentOperand(), BinaryOperator.Multiply));
        }
    }

    private void BeginTyping()
    {
        _input = new NumberInput(_options.MaxInputDigits);
        _currentIsParenthesizedGroup = false;
        _state = EntryState.Typing;
    }

    /// <summary>
    /// The operand that the next operation works on. After an operator ("5 + √") it is the left operand,
    /// as in most calculators.
    /// </summary>
    private Operand TakeCurrentOperand() => _state switch
    {
        EntryState.Typing => Operand.FromNumber(_input.ToValue()),
        EntryState.AfterOperator => Operand.FromNumber(_levels[^1][^1].Left.Value),
        _ => _current,
    };

    private void PushOperation(Operand left, BinaryOperator op)
    {
        List<PendingOperation> level = _levels[^1];
        Operand operand = left;
        bool reduced = false;

        while (level.Count > 0 && ShouldEvaluateBefore(level[^1].Operator, op))
        {
            PendingOperation pending = PopPending();
            BigDecimal value = pending.Operator.Apply(pending.Left.Value, operand.Value, _options.Precision);
            operand = Operand.Combine(pending.Left, pending.Operator, operand, value);
            reduced = true;
        }

        if (!_options.UsePrecedence && reduced)
        {
            // Standard mode shows the running result instead of the whole chain: "2 + 3 ×" becomes "5 ×".
            operand = Operand.FromNumber(operand.Value);
        }
        else if (operand.LowestPrecedence is int lowest && lowest < op.Precedence())
        {
            // Only happens when an operator is replaced by a tighter one: "2 × 3 +" changed to "^" reads "(2 × 3) ^".
            operand = operand.Parenthesized();
        }

        level.Add(new PendingOperation(operand, op));
        SetCurrent(operand);
        _state = EntryState.AfterOperator;
    }

    private bool ShouldEvaluateBefore(BinaryOperator pending, BinaryOperator incoming) =>
        !_options.UsePrecedence || pending.Precedence() >= incoming.Precedence();

    private Operand ReduceLevel(List<PendingOperation> level, Operand right)
    {
        Operand operand = right;
        for (int i = level.Count - 1; i >= 0; i--)
        {
            PendingOperation pending = level[i];
            BigDecimal value = pending.Operator.Apply(pending.Left.Value, operand.Value, _options.Precision);
            operand = Operand.Combine(pending.Left, pending.Operator, operand, value);
        }

        return operand;
    }

    private PendingOperation PopPending()
    {
        List<PendingOperation> level = _levels[^1];
        PendingOperation last = level[^1];
        level.RemoveAt(level.Count - 1);
        return last;
    }

    private PendingOperation? FindLastPending()
    {
        for (int i = _levels.Count - 1; i >= 0; i--)
        {
            if (_levels[i].Count > 0)
            {
                return _levels[i][^1];
            }
        }

        return null;
    }

    private void SetCurrent(Operand operand)
    {
        _current = operand;
        _currentIsParenthesizedGroup = false;
        _state = EntryState.Operand;
    }

    private void StartNewExpression()
    {
        ResetExpression();
        _state = EntryState.Operand;
    }

    private void ResetExpression()
    {
        _levels.Clear();
        _levels.Add([]);
        _repeat = null;
        _completedExpression = null;
        _currentIsParenthesizedGroup = false;
    }

    private List<ExpressionToken> BuildPendingExpression()
    {
        var tokens = new List<ExpressionToken>();
        for (int i = 0; i < _levels.Count; i++)
        {
            if (i > 0)
            {
                tokens.Add(new OpenParenthesisToken());
            }

            foreach (PendingOperation pending in _levels[i])
            {
                tokens.AddRange(pending.Left.Tokens);
                tokens.Add(new OperatorToken(pending.Operator));
            }
        }

        // A plain number is shown only in the display; functions and groups also appear in the expression line.
        if (_state == EntryState.Operand && _current.IsComposite)
        {
            tokens.AddRange(_current.Tokens);
        }

        return tokens;
    }

    /// <summary>Runs a command and turns calculation errors into the error state.</summary>
    private void Run(Action command)
    {
        try
        {
            _completedExpression = null;
            command();
        }
        catch (CalculationException exception)
        {
            _error = exception.Error;
            _state = EntryState.Start;
        }

        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private enum EntryState
    {
        /// <summary>Nothing entered for the current operand; the display shows 0 (or keeps the value after "(").</summary>
        Start,

        /// <summary>A number is being typed.</summary>
        Typing,

        /// <summary>A computed operand is shown: function result, constant, recalled value, closed group.</summary>
        Operand,

        /// <summary>The last key was a binary operator.</summary>
        AfterOperator,

        /// <summary>The last key was "=".</summary>
        Result,
    }

    private sealed record PendingOperation(Operand Left, BinaryOperator Operator);

    private sealed record RepeatOperation(BinaryOperator Operator, BigDecimal Right);
}
