using System.Globalization;

namespace Calculator.UI.ViewModels;

public enum GraphKeypadAction
{
    Insert,
    Backspace,
    Clear,
    Submit,
}

/// <summary>A key of the graphing keypad: text to insert at the caret, or an editing action.</summary>
public sealed record GraphKeypadKey(GraphKeypadAction Action, string Text = "");

/// <summary>
/// State of the graphing keypad (legacy GraphingNumPad): the "2nd" key, the 2nd / hyp keys of the Trigonometry panel.
/// Every key is reported through <see cref="KeyInvoked"/>; the view applies it to the equation being edited.
/// </summary>
public sealed class GraphingKeypadViewModel : ObservableObject
{
    private bool _isShifted;
    private bool _isTrigShifted;
    private bool _isHyperbolic;

    public GraphingKeypadViewModel()
    {
        InsertCommand = new DelegateCommand<string>(text => Invoke(new GraphKeypadKey(GraphKeypadAction.Insert, text)));
        BackspaceCommand = new DelegateCommand(() => Invoke(new GraphKeypadKey(GraphKeypadAction.Backspace)));
        ClearCommand = new DelegateCommand(() => Invoke(new GraphKeypadKey(GraphKeypadAction.Clear)));
        SubmitCommand = new DelegateCommand(() => Invoke(new GraphKeypadKey(GraphKeypadAction.Submit)));
        ToggleShiftCommand = new DelegateCommand(() => IsShifted = !IsShifted);
        ToggleTrigShiftCommand = new DelegateCommand(() => IsTrigShifted = !IsTrigShifted);
        ToggleHyperbolicCommand = new DelegateCommand(() => IsHyperbolic = !IsHyperbolic);
    }

    public event EventHandler<GraphKeypadKey>? KeyInvoked;

    public string DecimalSeparator => CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

    public bool IsShifted
    {
        get => _isShifted;
        set
        {
            if (SetProperty(ref _isShifted, value))
            {
                OnPropertyChanged(nameof(IsNotShifted));
            }
        }
    }

    public bool IsNotShifted => !IsShifted;

    public bool IsTrigShifted
    {
        get => _isTrigShifted;
        set
        {
            if (SetProperty(ref _isTrigShifted, value))
            {
                OnTrigSetChanged();
            }
        }
    }

    public bool IsHyperbolic
    {
        get => _isHyperbolic;
        set
        {
            if (SetProperty(ref _isHyperbolic, value))
            {
                OnTrigSetChanged();
            }
        }
    }

    // Exactly one of the four function sets of the Trigonometry panel is visible.
    public bool ShowTrigonometric => !IsTrigShifted && !IsHyperbolic;

    public bool ShowInverseTrigonometric => IsTrigShifted && !IsHyperbolic;

    public bool ShowHyperbolic => !IsTrigShifted && IsHyperbolic;

    public bool ShowInverseHyperbolic => IsTrigShifted && IsHyperbolic;

    public DelegateCommand<string> InsertCommand { get; }

    public DelegateCommand BackspaceCommand { get; }

    public DelegateCommand ClearCommand { get; }

    public DelegateCommand SubmitCommand { get; }

    public DelegateCommand ToggleShiftCommand { get; }

    public DelegateCommand ToggleTrigShiftCommand { get; }

    public DelegateCommand ToggleHyperbolicCommand { get; }

    private void Invoke(GraphKeypadKey key) => KeyInvoked?.Invoke(this, key);

    private void OnTrigSetChanged()
    {
        OnPropertyChanged(nameof(ShowTrigonometric));
        OnPropertyChanged(nameof(ShowInverseTrigonometric));
        OnPropertyChanged(nameof(ShowHyperbolic));
        OnPropertyChanged(nameof(ShowInverseHyperbolic));
    }
}

/// <summary>Applies a keypad key to equation text with a caret and a selection.</summary>
public static class EquationTextEditor
{
    /// <returns>The new text and caret position.</returns>
    public static (string Text, int Caret) Apply(string text, int caret, int selectionLength, GraphKeypadKey key)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        selectionLength = Math.Clamp(selectionLength, 0, text.Length - caret);

        switch (key.Action)
        {
            case GraphKeypadAction.Insert:
                string replaced = text.Remove(caret, selectionLength).Insert(caret, key.Text);
                return (replaced, caret + key.Text.Length);

            case GraphKeypadAction.Backspace when selectionLength > 0:
                return (text.Remove(caret, selectionLength), caret);

            case GraphKeypadAction.Backspace when caret > 0:
                return (text.Remove(caret - 1, 1), caret - 1);

            case GraphKeypadAction.Clear:
                return (string.Empty, 0);

            default:
                return (text, caret);
        }
    }
}
