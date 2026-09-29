using System.Collections.ObjectModel;
using System.Globalization;
using Calculator.UI.Localization;
using Calculator.UI.Platform;
using Calculator.Core.Numerics;
using Calculator.Core.Programmer;
using Calculator.Core.Storage;

namespace Calculator.UI.ViewModels;

/// <summary>One bit of the bit toggling keypad.</summary>
public sealed class BitViewModel(int index, Action<int> toggle) : ObservableObject
{
    private bool _isSet;
    private bool _isEnabled = true;

    public int Index { get; } = index;

    public string IndexText { get; } = index.ToString(CultureInfo.CurrentCulture);

    /// <summary>Bit numbers are written under every fourth bit, like the original.</summary>
    public string IndexLabel => Index % 4 == 0 ? IndexText : string.Empty;

    public string Text => IsSet ? "1" : "0";

    public bool IsSet
    {
        get => _isSet;
        set
        {
            if (SetProperty(ref _isSet, value))
            {
                OnPropertyChanged(nameof(Text));
            }
        }
    }

    /// <summary>Bits above the word size are disabled.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public DelegateCommand ToggleCommand { get; } = new(() => toggle(index));
}

/// <summary>The programmer mode: forwards keys to <see cref="ProgrammerEngine"/> and shows the value in every radix.</summary>
public sealed class ProgrammerViewModel : ObservableObject, IDisposable
{
    private readonly ProgrammerEngine _engine = new();
    private readonly CalculatorMemory _memory;
    // Operator names (AND, Lsh, Mod) are the same in every language.
    private readonly ProgrammerExpressionFormatter _expressionFormatter = new(new EnglishProgrammerVocabulary());

    private string _displayText = "0";
    private string _expressionText = string.Empty;
    private bool _isError;
    private bool _showClearEntry;
    private bool _isBitKeypadVisible;
    private string _parenthesisCountText = string.Empty;

    public ProgrammerViewModel(CalculatorMemory memory)
    {
        _memory = memory;

        for (int i = 63; i >= 0; i--)
        {
            Bits.Add(new BitViewModel(i, _engine.ToggleBit));
        }

        _engine.Changed += (_, _) => UpdateDisplay();
        _memory.Changed += OnMemoryChanged;

        DigitCommand = new DelegateCommand<string>(digit => _engine.EnterDigit(int.Parse(digit, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
        OperatorCommand = new DelegateCommand<ProgrammerOperator>(_engine.ApplyOperator);
        FunctionCommand = new DelegateCommand<ProgrammerFunction>(_engine.ApplyFunction);
        LeftShiftCommand = new DelegateCommand(ShiftLeft);
        RightShiftCommand = new DelegateCommand(ShiftRight);
        EvaluateCommand = new DelegateCommand(_engine.Evaluate);
        ClearCommand = new DelegateCommand(_engine.Clear);
        ClearEntryCommand = new DelegateCommand(_engine.ClearEntry);
        BackspaceCommand = new DelegateCommand(_engine.Backspace);
        OpenParenthesisCommand = new DelegateCommand(_engine.OpenParenthesis);
        CloseParenthesisCommand = new DelegateCommand(_engine.CloseParenthesis);
        SelectRadixCommand = new DelegateCommand<Radix>(radix => _engine.Radix = radix);
        CycleWordSizeCommand = new DelegateCommand(CycleWordSize);
        SelectShiftModeCommand = new DelegateCommand<ShiftMode>(SetShiftMode);
        ShowFullKeypadCommand = new DelegateCommand(() => IsBitKeypadVisible = false);
        ShowBitKeypadCommand = new DelegateCommand(() => IsBitKeypadVisible = true);
        MemoryStoreCommand = new DelegateCommand(StoreInMemory, () => !IsError);
        RecallMemoryItemCommand = new DelegateCommand<MemoryItemViewModel>(item => _engine.SetValue(Word.FromDecimal(item.Value, _engine.WordSize)));
        MemoryClearCommand = new DelegateCommand(_memory.Clear, () => !_memory.IsEmpty);

        UpdateDisplay();
        UpdateMemory();
    }

    public string DisplayText
    {
        get => _displayText;
        private set => SetProperty(ref _displayText, value);
    }

    public string ExpressionText
    {
        get => _expressionText;
        private set => SetProperty(ref _expressionText, value);
    }

    public bool IsError
    {
        get => _isError;
        private set
        {
            if (SetProperty(ref _isError, value))
            {
                OnPropertyChanged(nameof(IsInputEnabled));
                MemoryStoreCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsInputEnabled => !IsError;

    public bool ShowClearEntry
    {
        get => _showClearEntry;
        private set
        {
            if (SetProperty(ref _showClearEntry, value))
            {
                OnPropertyChanged(nameof(ShowClear));
            }
        }
    }

    public bool ShowClear => !ShowClearEntry;

    public string ParenthesisCountText
    {
        get => _parenthesisCountText;
        private set => SetProperty(ref _parenthesisCountText, value);
    }

    // The value in every radix (the rows under the display).
    public string HexText => Format(Radix.Hexadecimal);

    public string DecimalText => Format(Radix.Decimal);

    public string OctalText => Format(Radix.Octal);

    public string BinaryText => Format(Radix.Binary);

    public bool IsHexSelected => _engine.Radix == Radix.Hexadecimal;

    public bool IsDecimalSelected => _engine.Radix == Radix.Decimal;

    public bool IsOctalSelected => _engine.Radix == Radix.Octal;

    public bool IsBinarySelected => _engine.Radix == Radix.Binary;

    // Digit keys that exist in the current radix.
    public bool AreHexDigitsEnabled => IsInputEnabled && _engine.Radix == Radix.Hexadecimal;

    public bool AreDigits8To9Enabled => IsInputEnabled && _engine.Radix is Radix.Hexadecimal or Radix.Decimal;

    public bool AreDigits2To7Enabled => IsInputEnabled && _engine.Radix != Radix.Binary;

    public string WordSizeText => _engine.WordSize.ToString().ToUpperInvariant();

    public ShiftMode ShiftMode => _engine.ShiftMode;

    /// <summary>Arithmetic and logical modes show the &lt;&lt; &gt;&gt; operators, rotate modes the one-bit rotations.</summary>
    public bool ShowShiftOperators => _engine.ShiftMode is ShiftMode.Arithmetic or ShiftMode.Logical;

    public bool ShowRotateFunctions => !ShowShiftOperators;

    public bool IsBitKeypadVisible
    {
        get => _isBitKeypadVisible;
        set
        {
            if (SetProperty(ref _isBitKeypadVisible, value))
            {
                OnPropertyChanged(nameof(IsFullKeypadVisible));
            }
        }
    }

    public bool IsFullKeypadVisible => !IsBitKeypadVisible;

    public ObservableCollection<BitViewModel> Bits { get; } = [];

    public ObservableCollection<MemoryItemViewModel> MemoryItems { get; } = [];

    public bool IsMemoryEmpty => _memory.IsEmpty;

    public DelegateCommand<string> DigitCommand { get; }

    public DelegateCommand<ProgrammerOperator> OperatorCommand { get; }

    public DelegateCommand<ProgrammerFunction> FunctionCommand { get; }

    public DelegateCommand LeftShiftCommand { get; }

    public DelegateCommand RightShiftCommand { get; }

    public DelegateCommand EvaluateCommand { get; }

    public DelegateCommand ClearCommand { get; }

    public DelegateCommand ClearEntryCommand { get; }

    public DelegateCommand BackspaceCommand { get; }

    public DelegateCommand OpenParenthesisCommand { get; }

    public DelegateCommand CloseParenthesisCommand { get; }

    public DelegateCommand<Radix> SelectRadixCommand { get; }

    public DelegateCommand CycleWordSizeCommand { get; }

    public DelegateCommand<ShiftMode> SelectShiftModeCommand { get; }

    public DelegateCommand ShowFullKeypadCommand { get; }

    public DelegateCommand ShowBitKeypadCommand { get; }

    public DelegateCommand MemoryStoreCommand { get; }

    public DelegateCommand<MemoryItemViewModel> RecallMemoryItemCommand { get; }

    public DelegateCommand MemoryClearCommand { get; }

    /// <summary>Keyboard shortcuts of the legacy programmer mode.</summary>
    public void HandleKeyboardInput(KeyboardInput input)
    {
        switch (input.Key)
        {
            case KeyboardKey.Enter:
                _engine.Evaluate();
                return;
            case KeyboardKey.Escape:
                _engine.Clear();
                return;
            case KeyboardKey.Delete:
                _engine.ClearEntry();
                return;
            case KeyboardKey.Backspace:
                _engine.Backspace();
                return;
            case KeyboardKey.F9:
                _engine.ApplyFunction(ProgrammerFunction.Negate);
                return;
            case KeyboardKey.F5:
                _engine.Radix = Radix.Hexadecimal;
                return;
            case KeyboardKey.F6:
                _engine.Radix = Radix.Decimal;
                return;
            case KeyboardKey.F7:
                _engine.Radix = Radix.Octal;
                return;
            case KeyboardKey.F8:
                _engine.Radix = Radix.Binary;
                return;
            case KeyboardKey.F12:
                _engine.WordSize = WordSize.QWord;
                return;
            case KeyboardKey.F2:
                _engine.WordSize = WordSize.DWord;
                return;
            case KeyboardKey.F3:
                _engine.WordSize = WordSize.Word;
                return;
            case KeyboardKey.F4:
                _engine.WordSize = WordSize.Byte;
                return;
        }

        if (input.Control || input.Character is not char character)
        {
            return;
        }

        if (Uri.IsHexDigit(character))
        {
            _engine.EnterDigit(Convert.ToInt32(character.ToString(), 16));
            return;
        }

        switch (character)
        {
            case '+':
                _engine.ApplyOperator(ProgrammerOperator.Add);
                break;
            case '-':
                _engine.ApplyOperator(ProgrammerOperator.Subtract);
                break;
            case '*':
                _engine.ApplyOperator(ProgrammerOperator.Multiply);
                break;
            case '/':
                _engine.ApplyOperator(ProgrammerOperator.Divide);
                break;
            case '%':
                _engine.ApplyOperator(ProgrammerOperator.Modulo);
                break;
            case '&':
                _engine.ApplyOperator(ProgrammerOperator.And);
                break;
            case '|':
                _engine.ApplyOperator(ProgrammerOperator.Or);
                break;
            case '^':
                _engine.ApplyOperator(ProgrammerOperator.Xor);
                break;
            case '~':
                _engine.ApplyFunction(ProgrammerFunction.Not);
                break;
            case '<':
                ShiftLeft();
                break;
            case '>':
                ShiftRight();
                break;
            case '(':
                _engine.OpenParenthesis();
                break;
            case ')':
                _engine.CloseParenthesis();
                break;
            case '=':
                _engine.Evaluate();
                break;
        }
    }

    private void ShiftLeft()
    {
        switch (_engine.ShiftMode)
        {
            case ShiftMode.Rotate:
                _engine.ApplyFunction(ProgrammerFunction.RotateLeft);
                break;
            case ShiftMode.RotateThroughCarry:
                _engine.ApplyFunction(ProgrammerFunction.RotateLeftThroughCarry);
                break;
            default:
                _engine.ApplyOperator(ProgrammerOperator.LeftShift);
                break;
        }
    }

    private void ShiftRight()
    {
        switch (_engine.ShiftMode)
        {
            case ShiftMode.Rotate:
                _engine.ApplyFunction(ProgrammerFunction.RotateRight);
                break;
            case ShiftMode.RotateThroughCarry:
                _engine.ApplyFunction(ProgrammerFunction.RotateRightThroughCarry);
                break;
            default:
                _engine.ApplyOperator(ProgrammerOperator.RightShift);
                break;
        }
    }

    private void CycleWordSize()
    {
        _engine.WordSize = _engine.WordSize switch
        {
            WordSize.QWord => WordSize.DWord,
            WordSize.DWord => WordSize.Word,
            WordSize.Word => WordSize.Byte,
            _ => WordSize.QWord,
        };
    }

    private void SetShiftMode(ShiftMode mode)
    {
        _engine.ShiftMode = mode;
        OnPropertyChanged(nameof(ShiftMode));
        OnPropertyChanged(nameof(ShowShiftOperators));
        OnPropertyChanged(nameof(ShowRotateFunctions));
    }

    private BigDecimal CurrentSignedValue => Word.ToSigned(_engine.DisplayValue, _engine.WordSize);

    private void StoreInMemory() => _memory.Store(CurrentSignedValue);

    private string GroupSeparator => CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;

    private string Format(Radix radix) => ProgrammerFormatter.Format(_engine.DisplayValue, radix, _engine.WordSize, GroupSeparator);

    private void UpdateDisplay()
    {
        IsError = _engine.Error is not null;
        ShowClearEntry = _engine.IsTyping;

        DisplayText = _engine.Error switch
        {
            CalculationError error => ErrorMessage(error),
            null when _engine.IsTyping => ProgrammerFormatter.FormatInput(_engine.Input, GroupSeparator),
            _ => Format(_engine.Radix),
        };
        ExpressionText = _expressionFormatter.Format(_engine.Expression, _engine.Radix, _engine.WordSize);
        ParenthesisCountText = _engine.OpenParenthesisCount > 0
            ? _engine.OpenParenthesisCount.ToString(CultureInfo.CurrentCulture)
            : string.Empty;

        ulong value = _engine.DisplayValue;
        int bits = Word.Bits(_engine.WordSize);
        foreach (BitViewModel bit in Bits)
        {
            bit.IsSet = Word.GetBit(value, bit.Index);
            bit.IsEnabled = bit.Index < bits;
        }

        OnPropertyChanged(nameof(HexText));
        OnPropertyChanged(nameof(DecimalText));
        OnPropertyChanged(nameof(OctalText));
        OnPropertyChanged(nameof(BinaryText));
        OnPropertyChanged(nameof(IsHexSelected));
        OnPropertyChanged(nameof(IsDecimalSelected));
        OnPropertyChanged(nameof(IsOctalSelected));
        OnPropertyChanged(nameof(IsBinarySelected));
        OnPropertyChanged(nameof(AreHexDigitsEnabled));
        OnPropertyChanged(nameof(AreDigits8To9Enabled));
        OnPropertyChanged(nameof(AreDigits2To7Enabled));
        OnPropertyChanged(nameof(WordSizeText));

        // Memory values are shown in the current radix and word size.
        UpdateMemory();
    }

    /// <summary>The memory is shared by all modes and outlives this view model: stop listening to it.</summary>
    public void Dispose() => _memory.Changed -= OnMemoryChanged;

    private void OnMemoryChanged(object? sender, EventArgs e) => UpdateMemory();

    private void UpdateMemory()
    {
        MemoryItems.Clear();
        for (int i = 0; i < _memory.Values.Count; i++)
        {
            int index = i;
            BigDecimal value = _memory.Values[i];
            ulong raw = Word.FromDecimal(value, _engine.WordSize);
            MemoryItems.Add(new MemoryItemViewModel(
                value,
                ProgrammerFormatter.Format(raw, _engine.Radix, _engine.WordSize, GroupSeparator),
                add: () => _memory.AddAt(index, CurrentSignedValue),
                subtract: () => _memory.SubtractAt(index, CurrentSignedValue),
                clear: () => _memory.RemoveAt(index)));
        }

        OnPropertyChanged(nameof(IsMemoryEmpty));
        MemoryClearCommand.RaiseCanExecuteChanged();
    }

    private static string ErrorMessage(CalculationError error) => error switch
    {
        CalculationError.DivideByZero => AppStrings.Get("Error.DivideByZero"),
        CalculationError.Undefined => AppStrings.Get("Error.Undefined"),
        CalculationError.Overflow => AppStrings.Get("Error.Overflow"),
        _ => AppStrings.Get("Error.InvalidInput"),
    };
}
