using System.Collections.ObjectModel;
using System.Globalization;
using Calculator.UI.Localization;
using Calculator.UI.Platform;
using Calculator.Core.Engine;
using Calculator.Core.Formatting;
using Calculator.Core.Numerics;
using Calculator.Core.Storage;

namespace Calculator.UI.ViewModels;

/// <summary>
/// One calculator mode (standard or scientific): forwards key presses to the engine and exposes display text,
/// history and memory for the page. Memory is shared between modes, history belongs to the mode.
/// </summary>
public sealed class CalculatorViewModel : ObservableObject, IDisposable
{
    private readonly CalculatorEngine _engine;
    private readonly CalculationHistory _history = new();
    private readonly CalculatorMemory _memory;
    private readonly ISystemServices _system;
    // Math notation (sin, √, mod) is the same in every language.
    private readonly ExpressionFormatter _expressionFormatter = new(new EnglishExpressionVocabulary());

    private string _displayText = "0";
    private string _expressionText = string.Empty;
    private bool _isError;
    private bool _showClearEntry;
    private string _parenthesisCountText = string.Empty;
    private bool _isInverse;
    private bool _isHyperbolic;
    private bool _isScientificNotation;

    public CalculatorViewModel(string title, CalculatorOptions options, CalculatorMemory memory, ISystemServices system)
    {
        Title = title;
        _engine = new CalculatorEngine(options);
        _memory = memory;
        _system = system;

        _engine.Changed += (_, _) => UpdateDisplay();
        _engine.CalculationCompleted += (_, entry) => _history.Add(entry);
        _history.Changed += (_, _) => UpdateHistory();
        _memory.Changed += OnMemoryChanged;

        DigitCommand = new DelegateCommand<string>(digit => _engine.EnterDigit(int.Parse(digit, CultureInfo.InvariantCulture)));
        DecimalPointCommand = new DelegateCommand(_engine.EnterDecimalPoint);
        OperatorCommand = new DelegateCommand<BinaryOperator>(_engine.ApplyBinaryOperator);
        FunctionCommand = new DelegateCommand<UnaryFunction>(_engine.ApplyUnaryFunction);
        ConstantCommand = new DelegateCommand<MathConstant>(_engine.InsertConstant);
        EvaluateCommand = new DelegateCommand(_engine.Evaluate);
        ClearCommand = new DelegateCommand(_engine.Clear);
        ClearEntryCommand = new DelegateCommand(_engine.ClearEntry);
        BackspaceCommand = new DelegateCommand(_engine.Backspace);
        NegateCommand = new DelegateCommand(_engine.Negate);
        PercentCommand = new DelegateCommand(_engine.ApplyPercent);
        OpenParenthesisCommand = new DelegateCommand(_engine.OpenParenthesis);
        CloseParenthesisCommand = new DelegateCommand(_engine.CloseParenthesis);
        ExponentCommand = new DelegateCommand(_engine.BeginExponent);
        RandomCommand = new DelegateCommand(_engine.InsertRandom);
        CycleAngleUnitCommand = new DelegateCommand(CycleAngleUnit);
        ToggleInverseCommand = new DelegateCommand(() => IsInverse = !IsInverse);
        ToggleHyperbolicCommand = new DelegateCommand(() => IsHyperbolic = !IsHyperbolic);
        ToggleScientificNotationCommand = new DelegateCommand(() => IsScientificNotation = !IsScientificNotation);

        MemoryStoreCommand = new DelegateCommand(() => _memory.Store(_engine.DisplayValue), () => !IsError);
        MemoryRecallCommand = new DelegateCommand(RecallLatestMemory, () => !_memory.IsEmpty);
        MemoryAddCommand = new DelegateCommand(() => _memory.AddToLatest(_engine.DisplayValue), () => !IsError);
        MemorySubtractCommand = new DelegateCommand(() => _memory.SubtractFromLatest(_engine.DisplayValue), () => !IsError);
        MemoryClearCommand = new DelegateCommand(_memory.Clear, () => !_memory.IsEmpty);
        RecallMemoryItemCommand = new DelegateCommand<MemoryItemViewModel>(item => _engine.SetValue(item.Value));

        LoadHistoryItemCommand = new DelegateCommand<HistoryItemViewModel>(item => _engine.LoadHistoryEntry(item.Entry));
        DeleteHistoryItemCommand = new DelegateCommand<HistoryItemViewModel>(item => _history.RemoveAt(HistoryItems.IndexOf(item)));
        ClearHistoryCommand = new DelegateCommand(_history.Clear);

        CopyCommand = new AsyncDelegateCommand(CopyAsync);
        PasteCommand = new AsyncDelegateCommand(PasteAsync);

        UpdateDisplay();
        UpdateMemory();
    }

    public string Title { get; }

    public bool UsesPrecedence => _engine.Options.UsePrecedence;

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

    /// <summary>While an error message is shown, operator keys are disabled.</summary>
    public bool IsError
    {
        get => _isError;
        private set
        {
            if (SetProperty(ref _isError, value))
            {
                OnPropertyChanged(nameof(IsInputEnabled));
                MemoryStoreCommand.RaiseCanExecuteChanged();
                MemoryAddCommand.RaiseCanExecuteChanged();
                MemorySubtractCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsInputEnabled => !IsError;

    /// <summary>The scientific keypad shows "CE" while a number is typed and "C" otherwise, like Windows Calculator.</summary>
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

    public string DecimalSeparator => CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

    /// <summary>Number of open parentheses shown on the "(" key; empty when none are open.</summary>
    public string ParenthesisCountText
    {
        get => _parenthesisCountText;
        private set => SetProperty(ref _parenthesisCountText, value);
    }

    public string AngleUnitText => _engine.AngleUnit switch
    {
        AngleUnit.Degrees => "DEG",
        AngleUnit.Radians => "RAD",
        _ => "GRAD",
    };

    /// <summary>"2nd": shows the alternative functions (x³ instead of x², sin⁻¹ instead of sin, ...).</summary>
    public bool IsInverse
    {
        get => _isInverse;
        set
        {
            if (SetProperty(ref _isInverse, value))
            {
                OnPropertyChanged(nameof(IsNotInverse));
                OnTrigonometryVariantChanged();
            }
        }
    }

    public bool IsNotInverse => !IsInverse;

    public bool IsHyperbolic
    {
        get => _isHyperbolic;
        set
        {
            if (SetProperty(ref _isHyperbolic, value))
            {
                OnTrigonometryVariantChanged();
            }
        }
    }

    // Exactly one of the four trigonometry variants is visible in the trigonometry flyout.
    public bool ShowTrigonometric => !IsInverse && !IsHyperbolic;

    public bool ShowInverseTrigonometric => IsInverse && !IsHyperbolic;

    public bool ShowHyperbolic => !IsInverse && IsHyperbolic;

    public bool ShowInverseHyperbolic => IsInverse && IsHyperbolic;

    /// <summary>"F-E": always show the display in scientific notation.</summary>
    public bool IsScientificNotation
    {
        get => _isScientificNotation;
        set
        {
            if (SetProperty(ref _isScientificNotation, value))
            {
                UpdateDisplay();
                UpdateHistory();
                UpdateMemory();
            }
        }
    }

    public ObservableCollection<HistoryItemViewModel> HistoryItems { get; } = [];

    public bool IsHistoryEmpty => HistoryItems.Count == 0;

    public ObservableCollection<MemoryItemViewModel> MemoryItems { get; } = [];

    public bool IsMemoryEmpty => _memory.IsEmpty;

    public DelegateCommand<string> DigitCommand { get; }

    public DelegateCommand DecimalPointCommand { get; }

    public DelegateCommand<BinaryOperator> OperatorCommand { get; }

    public DelegateCommand<UnaryFunction> FunctionCommand { get; }

    public DelegateCommand<MathConstant> ConstantCommand { get; }

    public DelegateCommand EvaluateCommand { get; }

    public DelegateCommand ClearCommand { get; }

    public DelegateCommand ClearEntryCommand { get; }

    public DelegateCommand BackspaceCommand { get; }

    public DelegateCommand NegateCommand { get; }

    public DelegateCommand PercentCommand { get; }

    public DelegateCommand OpenParenthesisCommand { get; }

    public DelegateCommand CloseParenthesisCommand { get; }

    public DelegateCommand ExponentCommand { get; }

    public DelegateCommand RandomCommand { get; }

    public DelegateCommand CycleAngleUnitCommand { get; }

    public DelegateCommand ToggleInverseCommand { get; }

    public DelegateCommand ToggleHyperbolicCommand { get; }

    public DelegateCommand ToggleScientificNotationCommand { get; }

    public DelegateCommand MemoryStoreCommand { get; }

    public DelegateCommand MemoryRecallCommand { get; }

    public DelegateCommand MemoryAddCommand { get; }

    public DelegateCommand MemorySubtractCommand { get; }

    public DelegateCommand MemoryClearCommand { get; }

    public DelegateCommand<MemoryItemViewModel> RecallMemoryItemCommand { get; }

    public DelegateCommand<HistoryItemViewModel> LoadHistoryItemCommand { get; }

    public DelegateCommand<HistoryItemViewModel> DeleteHistoryItemCommand { get; }

    public DelegateCommand ClearHistoryCommand { get; }

    public AsyncDelegateCommand CopyCommand { get; }

    public AsyncDelegateCommand PasteCommand { get; }

    /// <summary>Keyboard shortcuts, following Windows Calculator.</summary>
    public void HandleKeyboardInput(KeyboardInput input)
    {
        if (input.Control)
        {
            HandleControlShortcut(input.Character);
            return;
        }

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
                _engine.Negate();
                return;
        }

        if (input.Character is not char character)
        {
            return;
        }

        if (character is >= '0' and <= '9')
        {
            _engine.EnterDigit(character - '0');
            return;
        }

        string decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        if (character == '.' || decimalSeparator.Contains(character, StringComparison.Ordinal))
        {
            _engine.EnterDecimalPoint();
            return;
        }

        HandleCharacterShortcut(character);
    }

    private void HandleCharacterShortcut(char character)
    {
        switch (character)
        {
            case '+':
                _engine.ApplyBinaryOperator(BinaryOperator.Add);
                break;
            case '-':
                _engine.ApplyBinaryOperator(BinaryOperator.Subtract);
                break;
            case '*':
                _engine.ApplyBinaryOperator(BinaryOperator.Multiply);
                break;
            case '/':
                _engine.ApplyBinaryOperator(BinaryOperator.Divide);
                break;
            case '=':
                _engine.Evaluate();
                break;
            case '%':
                _engine.ApplyPercent();
                break;
            case '@':
                _engine.ApplyUnaryFunction(UnaryFunction.SquareRoot);
                break;
            case 'r':
                _engine.ApplyUnaryFunction(UnaryFunction.Reciprocal);
                break;
            case 'q':
                _engine.ApplyUnaryFunction(UnaryFunction.Square);
                break;
        }

        if (!UsesPrecedence)
        {
            return;
        }

        // Scientific mode only.
        switch (character)
        {
            case '(':
                _engine.OpenParenthesis();
                break;
            case ')':
                _engine.CloseParenthesis();
                break;
            case '^':
            case 'y':
                _engine.ApplyBinaryOperator(BinaryOperator.Power);
                break;
            case '!':
                _engine.ApplyUnaryFunction(UnaryFunction.Factorial);
                break;
            case 'p':
                _engine.InsertConstant(MathConstant.Pi);
                break;
            case 'x':
                _engine.BeginExponent();
                break;
            case 's':
                _engine.ApplyUnaryFunction(UnaryFunction.Sin);
                break;
            case 'o':
                _engine.ApplyUnaryFunction(UnaryFunction.Cos);
                break;
            case 't':
                _engine.ApplyUnaryFunction(UnaryFunction.Tan);
                break;
            case 'l':
                _engine.ApplyUnaryFunction(UnaryFunction.Log10);
                break;
            case 'n':
                _engine.ApplyUnaryFunction(UnaryFunction.Ln);
                break;
        }
    }

    private void HandleControlShortcut(char? letter)
    {
        switch (letter)
        {
            case 'm':
                ExecuteIfEnabled(MemoryStoreCommand);
                break;
            case 'r':
                ExecuteIfEnabled(MemoryRecallCommand);
                break;
            case 'p':
                ExecuteIfEnabled(MemoryAddCommand);
                break;
            case 'q':
                ExecuteIfEnabled(MemorySubtractCommand);
                break;
            case 'l':
                ExecuteIfEnabled(MemoryClearCommand);
                break;
            case 'c':
                CopyCommand.Execute(null);
                break;
            case 'v':
                PasteCommand.Execute(null);
                break;
        }
    }

    private static void ExecuteIfEnabled(DelegateCommand command)
    {
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private NumberFormatOptions FormatOptions =>
        NumberFormatOptions.FromCulture(CultureInfo.CurrentCulture, _engine.Options.MaxInputDigits, IsScientificNotation);

    private void UpdateDisplay()
    {
        NumberFormatOptions options = FormatOptions;

        IsError = _engine.Error is not null;
        ShowClearEntry = _engine.IsTyping;
        DisplayText = _engine.Error switch
        {
            CalculationError error => ErrorMessage(error),
            null when _engine.IsTyping => NumberFormatter.FormatInput(_engine.Input, options),
            _ => NumberFormatter.Format(_engine.DisplayValue, options),
        };
        ExpressionText = _expressionFormatter.Format(_engine.Expression, options);
        ParenthesisCountText = _engine.OpenParenthesisCount > 0
            ? _engine.OpenParenthesisCount.ToString(CultureInfo.CurrentCulture)
            : string.Empty;
    }

    private static string ErrorMessage(CalculationError error) => error switch
    {
        CalculationError.DivideByZero => AppStrings.Get("Error.DivideByZero"),
        CalculationError.Undefined => AppStrings.Get("Error.Undefined"),
        CalculationError.Overflow => AppStrings.Get("Error.Overflow"),
        _ => AppStrings.Get("Error.InvalidInput"),
    };

    private void UpdateHistory()
    {
        NumberFormatOptions options = FormatOptions;
        HistoryItems.Clear();
        foreach (HistoryEntry entry in _history.Entries)
        {
            HistoryItems.Add(new HistoryItemViewModel(
                entry,
                _expressionFormatter.Format(entry.Expression, options),
                NumberFormatter.Format(entry.Result, options)));
        }

        OnPropertyChanged(nameof(IsHistoryEmpty));
    }

    /// <summary>The memory is shared by all modes and outlives this view model: stop listening to it.</summary>
    public void Dispose() => _memory.Changed -= OnMemoryChanged;

    private void OnMemoryChanged(object? sender, EventArgs e) => UpdateMemory();

    private void UpdateMemory()
    {
        NumberFormatOptions options = FormatOptions;
        MemoryItems.Clear();
        for (int i = 0; i < _memory.Values.Count; i++)
        {
            int index = i;
            MemoryItems.Add(new MemoryItemViewModel(
                _memory.Values[i],
                NumberFormatter.Format(_memory.Values[i], options),
                add: () => _memory.AddAt(index, _engine.DisplayValue),
                subtract: () => _memory.SubtractAt(index, _engine.DisplayValue),
                clear: () => _memory.RemoveAt(index)));
        }

        OnPropertyChanged(nameof(IsMemoryEmpty));
        MemoryRecallCommand.RaiseCanExecuteChanged();
        MemoryClearCommand.RaiseCanExecuteChanged();
    }

    private static bool IsClipboardUnavailable(Exception exception) =>
        exception is System.Runtime.InteropServices.ExternalException or UnauthorizedAccessException;

    private void RecallLatestMemory()
    {
        if (_memory.Recall() is BigDecimal value)
        {
            _engine.SetValue(value);
        }
    }

    private void CycleAngleUnit()
    {
        _engine.AngleUnit = _engine.AngleUnit switch
        {
            AngleUnit.Degrees => AngleUnit.Radians,
            AngleUnit.Radians => AngleUnit.Gradians,
            _ => AngleUnit.Degrees,
        };
        OnPropertyChanged(nameof(AngleUnitText));
    }

    private void OnTrigonometryVariantChanged()
    {
        OnPropertyChanged(nameof(ShowTrigonometric));
        OnPropertyChanged(nameof(ShowInverseTrigonometric));
        OnPropertyChanged(nameof(ShowHyperbolic));
        OnPropertyChanged(nameof(ShowInverseHyperbolic));
    }

    private async Task CopyAsync()
    {
        if (IsError)
        {
            return;
        }

        // The copied text has no digit grouping so it can be pasted into other programs.
        NumberFormatOptions options = FormatOptions with { UseDigitGrouping = false };
        try
        {
            await _system.SetClipboardTextAsync(NumberFormatter.Format(_engine.DisplayValue, options));
        }
        catch (Exception exception) when (IsClipboardUnavailable(exception))
        {
            // Another application holds the clipboard; copying is skipped like in Windows Calculator.
        }
    }

    private async Task PasteAsync()
    {
        string? text;
        try
        {
            text = await _system.GetClipboardTextAsync();
        }
        catch (Exception exception) when (IsClipboardUnavailable(exception))
        {
            return;
        }

        if (text is null)
        {
            return;
        }

        NumberFormatInfo format = CultureInfo.CurrentCulture.NumberFormat;
        string normalized = text.Trim()
            .Replace(format.NumberGroupSeparator, string.Empty, StringComparison.Ordinal)
            .Replace(format.NumberDecimalSeparator, ".", StringComparison.Ordinal);

        if (BigDecimal.TryParse(normalized, out BigDecimal value))
        {
            _engine.SetValue(value);
        }
    }
}
