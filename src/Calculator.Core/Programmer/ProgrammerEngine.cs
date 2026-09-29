using Calculator.Core.Engine;
using Calculator.Core.Numerics;

namespace Calculator.Core.Programmer;

/// <summary>
/// The programmer calculator: integers of a fixed word size in two's complement, entered and shown in any radix,
/// with bitwise operators and shifts. It follows the same structure as <see cref="CalculatorEngine"/>:
/// parenthesis levels of pending "left operand + operator" pairs, evaluated by precedence.
/// </summary>
public sealed class ProgrammerEngine
{
    private const int MaxParenthesisDepth = 25;

    private readonly List<List<PendingOperation>> _levels = [[]];

    private EntryState _state = EntryState.Start;
    private ProgrammerInput _input;
    private ProgrammerOperand _current = ProgrammerOperand.FromRaw(0);
    private bool _currentIsParenthesizedGroup;
    private IReadOnlyList<ExpressionToken>? _completedExpression;
    private CalculationError? _error;
    private Radix _radix = Radix.Decimal;
    private WordSize _wordSize = WordSize.QWord;
    private bool _carry;

    public ProgrammerEngine()
    {
        _input = new ProgrammerInput(_radix, _wordSize);
    }

    public event EventHandler? Changed;

    public Radix Radix
    {
        get => _radix;
        set
        {
            if (_radix == value)
            {
                return;
            }

            // A number being typed keeps its value; new digits start a new number in the new radix.
            CommitTyping();
            _radix = value;
            NotifyChanged();
        }
    }

    /// <summary>Changing the word size truncates the shown value, like the original.</summary>
    public WordSize WordSize
    {
        get => _wordSize;
        set
        {
            if (_wordSize == value)
            {
                return;
            }

            CommitTyping();
            _wordSize = value;
            ResetExpression();
            SetCurrent(ProgrammerOperand.FromRaw(Word.Truncate(_current.Raw, value)));
            NotifyChanged();
        }
    }

    public ShiftMode ShiftMode { get; set; } = ShiftMode.Arithmetic;

    /// <summary>The carry bit used by the rotate-through-carry functions.</summary>
    public bool Carry => _carry;

    public bool IsTyping => _state == EntryState.Typing;

    public ProgrammerInput Input => _input;

    /// <summary>Raw bits of the value in the display.</summary>
    public ulong DisplayValue => IsTyping ? _input.ToRaw() : _current.Raw;

    public CalculationError? Error => _error;

    public int OpenParenthesisCount => _levels.Count - 1;

    public IReadOnlyList<ExpressionToken> Expression => _completedExpression ?? BuildPendingExpression();

    public void EnterDigit(int digit)
    {
        if (digit >= (int)_radix)
        {
            return;
        }

        PrepareForNewOperand();
        if (_state != EntryState.Typing)
        {
            _input = new ProgrammerInput(_radix, _wordSize);
            _currentIsParenthesizedGroup = false;
            _state = EntryState.Typing;
        }

        _input.TryAppendDigit(digit);
        NotifyChanged();
    }

    public void ApplyOperator(ProgrammerOperator op)
    {
        if (_error is not null)
        {
            return;
        }

        Run(() =>
        {
            if (_state == EntryState.AfterOperator)
            {
                PendingOperation previous = PopPending();
                PushOperation(previous.Left, op);
                return;
            }

            if (_state == EntryState.Result)
            {
                ResetExpression();
            }

            PushOperation(TakeCurrentOperand(), op);
        });
    }

    public void ApplyFunction(ProgrammerFunction function)
    {
        if (_error is not null)
        {
            return;
        }

        if (function == ProgrammerFunction.Negate && _state == EntryState.Typing && _radix == Radix.Decimal)
        {
            _input.ToggleSign();
            NotifyChanged();
            return;
        }

        Run(() =>
        {
            ProgrammerOperand operand = TakeCurrentOperand();
            if (_state == EntryState.Result)
            {
                ResetExpression();
            }

            ulong result = Word.Apply(function, operand.Raw, _wordSize, ref _carry);
            SetCurrent(operand.WithFunction(function, result));
        });
    }

    public void OpenParenthesis()
    {
        if (_error is not null || OpenParenthesisCount >= MaxParenthesisDepth)
        {
            return;
        }

        Run(() =>
        {
            if (_state == EntryState.Result)
            {
                ResetExpression();
            }
            else if (_state is EntryState.Typing or EntryState.Operand)
            {
                PushOperation(TakeCurrentOperand(), ProgrammerOperator.Multiply);
            }

            _levels.Add([]);
            SetCurrent(ProgrammerOperand.FromRaw(0));
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
            ProgrammerOperand group = ReduceLevel(_levels[^1], TakeCurrentOperand()).Parenthesized();
            _levels.RemoveAt(_levels.Count - 1);
            SetCurrent(group);
            _currentIsParenthesizedGroup = true;
        });
    }

    public void Evaluate()
    {
        if (_error is not null || _state == EntryState.Result)
        {
            return;
        }

        Run(() =>
        {
            ProgrammerOperand operand = TakeCurrentOperand();
            while (OpenParenthesisCount > 0)
            {
                operand = ReduceLevel(_levels[^1], operand).Parenthesized();
                _levels.RemoveAt(_levels.Count - 1);
            }

            ProgrammerOperand result = ReduceLevel(_levels[0], operand);
            _levels[0].Clear();

            var expression = new List<ExpressionToken>(result.Tokens) { new EqualsToken() };
            SetCurrent(ProgrammerOperand.FromRaw(result.Raw));
            _completedExpression = expression;
            _state = EntryState.Result;
        });
    }

    public void Clear()
    {
        ResetExpression();
        _error = null;
        SetCurrent(ProgrammerOperand.FromRaw(0));
        _state = EntryState.Start;
        NotifyChanged();
    }

    public void ClearEntry()
    {
        if (_error is not null || _state == EntryState.Result)
        {
            Clear();
            return;
        }

        SetCurrent(ProgrammerOperand.FromRaw(0));
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
            _completedExpression = [];
        }

        NotifyChanged();
    }

    /// <summary>Bit toggling keypad: flips one bit of the displayed value.</summary>
    public void ToggleBit(int index)
    {
        if (_error is not null || index >= Word.Bits(_wordSize))
        {
            return;
        }

        ulong raw = Word.ToggleBit(DisplayValue, index, _wordSize);
        if (_state == EntryState.Result)
        {
            ResetExpression();
        }

        SetCurrent(ProgrammerOperand.FromRaw(raw));
        NotifyChanged();
    }

    /// <summary>Puts a value (memory recall, paste) into the display, truncated to the word size.</summary>
    public void SetValue(ulong raw)
    {
        if (_error is not null)
        {
            Clear();
        }

        if (_state == EntryState.Result)
        {
            ResetExpression();
        }

        SetCurrent(ProgrammerOperand.FromRaw(Word.Truncate(raw, _wordSize)));
        NotifyChanged();
    }

    private void PrepareForNewOperand()
    {
        if (_error is not null)
        {
            Clear();
        }

        if (_state == EntryState.Result)
        {
            ResetExpression();
            _state = EntryState.Start;
        }
        else if (_state == EntryState.Operand && _currentIsParenthesizedGroup)
        {
            Run(() => PushOperation(TakeCurrentOperand(), ProgrammerOperator.Multiply));
        }
    }

    private void CommitTyping()
    {
        if (_state == EntryState.Typing)
        {
            SetCurrent(ProgrammerOperand.FromRaw(_input.ToRaw()));
        }
    }

    private ProgrammerOperand TakeCurrentOperand() => _state switch
    {
        EntryState.Typing => ProgrammerOperand.FromRaw(_input.ToRaw()),
        EntryState.AfterOperator => ProgrammerOperand.FromRaw(_levels[^1][^1].Left.Raw),
        _ => _current,
    };

    private void PushOperation(ProgrammerOperand left, ProgrammerOperator op)
    {
        List<PendingOperation> level = _levels[^1];
        ProgrammerOperand operand = left;

        while (level.Count > 0 && level[^1].Operator.Precedence() >= op.Precedence())
        {
            PendingOperation pending = PopPending();
            ulong value = Word.Apply(pending.Operator, pending.Left.Raw, operand.Raw, _wordSize, ShiftMode);
            operand = ProgrammerOperand.Combine(pending.Left, pending.Operator, operand, value);
        }

        if (operand.LowestPrecedence is int lowest && lowest < op.Precedence())
        {
            operand = operand.Parenthesized();
        }

        level.Add(new PendingOperation(operand, op));
        SetCurrent(operand);
        _state = EntryState.AfterOperator;
    }

    private ProgrammerOperand ReduceLevel(List<PendingOperation> level, ProgrammerOperand right)
    {
        ProgrammerOperand operand = right;
        for (int i = level.Count - 1; i >= 0; i--)
        {
            PendingOperation pending = level[i];
            ulong value = Word.Apply(pending.Operator, pending.Left.Raw, operand.Raw, _wordSize, ShiftMode);
            operand = ProgrammerOperand.Combine(pending.Left, pending.Operator, operand, value);
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

    private void SetCurrent(ProgrammerOperand operand)
    {
        _current = operand;
        _currentIsParenthesizedGroup = false;
        _state = EntryState.Operand;
    }

    private void ResetExpression()
    {
        _levels.Clear();
        _levels.Add([]);
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
                tokens.Add(new ProgrammerOperatorToken(pending.Operator));
            }
        }

        if (_state == EntryState.Operand && _current.IsComposite)
        {
            tokens.AddRange(_current.Tokens);
        }

        return tokens;
    }

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
        Start,
        Typing,
        Operand,
        AfterOperator,
        Result,
    }

    private sealed record PendingOperation(ProgrammerOperand Left, ProgrammerOperator Operator);
}
