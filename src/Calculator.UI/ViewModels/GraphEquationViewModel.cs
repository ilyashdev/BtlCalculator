using System.Globalization;
using Avalonia.Collections;
using Avalonia.Media;
using Calculator.UI.Graphing;
using Calculator.UI.Localization;
using Calculator.UI.Platform;
using Calculator.Core.Graphing;

namespace Calculator.UI.ViewModels;

public enum GraphLineStyle
{
    Solid,
    Dot,
    Dash,
}

/// <summary>One line of the equation list: the typed text, its parsed equation or error, color and style.</summary>
public sealed class GraphEquationViewModel : ObservableObject
{
    private string _text = string.Empty;
    private GraphEquation? _equation;
    private string _errorText = string.Empty;
    private bool _isVisible = true;
    private readonly Func<AppTheme> _graphTheme;
    private int _colorIndex;
    private GraphLineStyle _lineStyle;
    private int _number;
    private bool _isPointerOver;
    private bool _hasFocus;

    /// <param name="graphTheme">The theme of the graph surface; equation colors follow it, not the app theme.</param>
    public GraphEquationViewModel(int colorIndex, Func<AppTheme> graphTheme)
    {
        _colorIndex = colorIndex;
        _graphTheme = graphTheme;
    }

    /// <summary>Raised when the graph must be redrawn (text, visibility or style changed).</summary>
    public event EventHandler? Changed;

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value))
            {
                Parse();
                OnPropertyChanged(nameof(IsEmpty));
                OnSwatchChanged();
                OnPropertyChanged(nameof(NumberText));
                OnPropertyChanged(nameof(ShowActions));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>The parsed equation; null while the text is empty or invalid.</summary>
    public GraphEquation? Equation
    {
        get => _equation;
        private set => SetProperty(ref _equation, value);
    }

    public string ErrorText
    {
        get => _errorText;
        private set
        {
            if (SetProperty(ref _errorText, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => ErrorText.Length > 0;

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                OnSwatchChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public int ColorIndex
    {
        get => _colorIndex;
        set
        {
            if (SetProperty(ref _colorIndex, value))
            {
                OnPropertyChanged(nameof(Color));
                OnSwatchChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public Color Color => EquationPalette.Get(ColorIndex, _graphTheme());

    /// <summary>The "f" square: the equation color, gray for the empty "Enter an expression" line.</summary>
    public Color SwatchColor => IsEmpty ? Color.Parse("#8A8A8A") : Color;

    // A hidden equation shows an outlined swatch instead of a filled one.
    public Color SwatchFill => IsVisible ? SwatchColor : Colors.Transparent;

    public Color SwatchTextColor => IsVisible ? Colors.White : SwatchColor;

    /// <summary>1-based position among the non-empty equations, shown as the index of "f".</summary>
    public int Number
    {
        get => _number;
        set
        {
            if (SetProperty(ref _number, value))
            {
                OnPropertyChanged(nameof(NumberText));
            }
        }
    }

    public string NumberText => IsEmpty ? string.Empty : Number.ToString(CultureInfo.CurrentCulture);

    public bool IsPointerOver
    {
        get => _isPointerOver;
        set
        {
            if (SetProperty(ref _isPointerOver, value))
            {
                OnPropertyChanged(nameof(ShowActions));
            }
        }
    }

    public bool HasFocus
    {
        get => _hasFocus;
        set
        {
            if (SetProperty(ref _hasFocus, value))
            {
                OnPropertyChanged(nameof(ShowActions));
            }
        }
    }

    /// <summary>The action buttons are shown while the line is hovered or edited, like the original.</summary>
    public bool ShowActions => (IsPointerOver || HasFocus) && !IsEmpty;

    public GraphLineStyle LineStyle
    {
        get => _lineStyle;
        set
        {
            if (SetProperty(ref _lineStyle, value))
            {
                OnPropertyChanged(nameof(LineStyleDashes));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>The dashes of the line style (in line widths) for the sample on the line style button; empty = solid.</summary>
    public AvaloniaList<double> LineStyleDashes => LineStyle switch
    {
        GraphLineStyle.Dash => [3, 2],
        GraphLineStyle.Dot => [1, 1.5],
        _ => [],
    };

    public bool IsEmpty => Text.Trim().Length == 0;

    /// <summary>Inequalities are drawn dashed, like the legacy GetRequest.</summary>
    public GraphLineStyle EffectiveLineStyle => Equation?.IsInequality == true && LineStyle == GraphLineStyle.Solid ? GraphLineStyle.Dash : LineStyle;

    /// <summary>Called when the graph theme changes so that the color swatch is updated.</summary>
    public void RefreshColor()
    {
        OnPropertyChanged(nameof(Color));
        OnSwatchChanged();
    }

    private void OnSwatchChanged()
    {
        OnPropertyChanged(nameof(SwatchColor));
        OnPropertyChanged(nameof(SwatchFill));
        OnPropertyChanged(nameof(SwatchTextColor));
    }

    private void Parse()
    {
        if (IsEmpty)
        {
            Equation = null;
            ErrorText = string.Empty;
            return;
        }

        char decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == "," ? ',' : '.';
        try
        {
            Equation = GraphEquation.Parse(Text, decimalSeparator);
            ErrorText = string.Empty;
        }
        catch (GraphInputException exception)
        {
            Equation = null;
            ErrorText = AppStrings.Get(exception.Error.ToString());
        }
    }
}

/// <summary>A parameter of the equations (a in y = ax²) with its slider range.</summary>
public sealed class GraphVariableViewModel(string name) : ObservableObject
{
    private double _value = 1;
    private double _minimum = -10;
    private double _maximum = 10;
    private double _step = 0.1;

    public event EventHandler? ValueChanged;

    public string Name { get; } = name;

    /// <summary>The value, snapped to multiples of <see cref="Step"/> from <see cref="Minimum"/> (slider positions are continuous).</summary>
    public double Value
    {
        get => _value;
        set
        {
            double snapped = Step > 0 ? Minimum + (Math.Round((value - Minimum) / Step) * Step) : value;
            snapped = Math.Round(snapped, 10);
            if (SetProperty(ref _value, snapped))
            {
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public double Minimum
    {
        get => _minimum;
        set => SetProperty(ref _minimum, value);
    }

    public double Maximum
    {
        get => _maximum;
        set => SetProperty(ref _maximum, value);
    }

    public double Step
    {
        get => _step;
        set => SetProperty(ref _step, value);
    }
}
