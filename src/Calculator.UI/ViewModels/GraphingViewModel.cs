using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using Calculator.UI.Localization;
using Calculator.UI.Platform;
using Calculator.Core.Graphing;

namespace Calculator.UI.ViewModels;

/// <summary>The "Graph theme" option: the graph stays light (the default of the original) or follows the app theme.</summary>
public enum GraphThemeMode
{
    AlwaysLight,
    MatchAppTheme,
}

/// <summary>
/// The graphing mode: the equation list, parameters, the visible range and the key graph features.
/// Drawing is done by <see cref="Graphing.GraphDrawable"/> from the state exposed here.
/// </summary>
public sealed class GraphingViewModel : ObservableObject
{
    private const double ZoomFactor = 1.5;
    private const string GraphThemePreferenceKey = "GraphTheme";

    private readonly ISettingsStore _settings;
    private readonly Func<AppTheme> _appTheme;

    private GraphViewport _viewport = GraphViewport.Default(0, 0);
    private bool _hasCustomView;
    private TrigonometricUnit _trigonometricUnit = TrigonometricUnit.Radians;
    private double _lineThickness = 2;
    private TracePoint? _trace;
    private GraphEquationViewModel? _analyzedEquation;
    private bool _isAnalysisVisible;
    private int _nextColorIndex;
    private GraphThemeMode _graphThemeMode;
    private bool _isTracingActive;
    private bool _isAnalyzing;
    private bool _areGraphOptionsOpen;

    /// <param name="appTheme">The theme the app shows now (the graph can follow it).</param>
    public GraphingViewModel(ISettingsStore settings, Func<AppTheme> appTheme)
    {
        _settings = settings;
        _appTheme = appTheme;
        _graphThemeMode = (GraphThemeMode)settings.GetInt(GraphThemePreferenceKey, (int)GraphThemeMode.AlwaysLight);
        AddEmptyEquation();

        RemoveEquationCommand = new DelegateCommand<GraphEquationViewModel>(RemoveEquation);
        ToggleVisibilityCommand = new DelegateCommand<GraphEquationViewModel>(equation => equation.IsVisible = !equation.IsVisible);
        CycleColorCommand = new DelegateCommand<GraphEquationViewModel>(equation => equation.ColorIndex++);
        CycleLineStyleCommand = new DelegateCommand<GraphEquationViewModel>(equation =>
            equation.LineStyle = equation.LineStyle switch
            {
                GraphLineStyle.Solid => GraphLineStyle.Dash,
                GraphLineStyle.Dash => GraphLineStyle.Dot,
                _ => GraphLineStyle.Solid,
            });
        AnalyzeCommand = new AsyncDelegateCommand<GraphEquationViewModel>(AnalyzeAsync);
        CloseAnalysisCommand = new DelegateCommand(() => IsAnalysisVisible = false);
        ZoomInCommand = new DelegateCommand(() => ZoomAtCenter(1 / ZoomFactor));
        ZoomOutCommand = new DelegateCommand(() => ZoomAtCenter(ZoomFactor));
        ResetViewCommand = new DelegateCommand(ResetView);
        SelectUnitCommand = new DelegateCommand<TrigonometricUnit>(unit => TrigonometricUnit = unit);
        ToggleTracingCommand = new DelegateCommand(() => IsTracingActive = !IsTracingActive);
    }

    /// <summary>Raised whenever the graph has to be redrawn.</summary>
    public event EventHandler? RedrawRequested;

    /// <summary>
    /// Grows whenever the functions change (equations, parameters, units). Panning and zooming do not change it (the
    /// curve cache follows the view); redraws for tracing, colors or line thickness keep it too, so the drawable
    /// reuses the curves it computed before.
    /// </summary>
    public int GeometryVersion { get; private set; }

    public ObservableCollection<GraphEquationViewModel> Equations { get; } = [];

    public ObservableCollection<GraphVariableViewModel> Variables { get; } = [];

    public bool HasVariables => Variables.Count > 0;

    /// <summary>The sliders are hidden while the key graph features cover the pane.</summary>
    public bool ShowVariables => HasVariables && !IsAnalysisVisible;

    public GraphViewport Viewport => _viewport;

    public TrigonometricUnit TrigonometricUnit
    {
        get => _trigonometricUnit;
        set
        {
            if (SetProperty(ref _trigonometricUnit, value))
            {
                RebuildGeometry();
            }
        }
    }

    public GraphThemeMode GraphThemeMode
    {
        get => _graphThemeMode;
        set
        {
            if (SetProperty(ref _graphThemeMode, value))
            {
                _settings.Set(GraphThemePreferenceKey, (int)value);
                OnPropertyChanged(nameof(IsGraphAlwaysLight));
                OnPropertyChanged(nameof(IsGraphMatchingAppTheme));
                RefreshThemeColors();
            }
        }
    }

    // Two-way targets for the radio buttons of the graph options.
    public bool IsGraphAlwaysLight
    {
        get => GraphThemeMode == GraphThemeMode.AlwaysLight;
        set
        {
            if (value)
            {
                GraphThemeMode = GraphThemeMode.AlwaysLight;
            }
        }
    }

    public bool IsGraphMatchingAppTheme
    {
        get => GraphThemeMode == GraphThemeMode.MatchAppTheme;
        set
        {
            if (value)
            {
                GraphThemeMode = GraphThemeMode.MatchAppTheme;
            }
        }
    }

    /// <summary>The theme the graph surface is drawn in.</summary>
    public AppTheme GraphTheme => GraphThemeMode == GraphThemeMode.AlwaysLight
        ? AppTheme.Light
        : _appTheme();

    /// <summary>Keyboard tracing: a cursor moved with the arrow keys traces the nearest curve point.</summary>
    public bool IsTracingActive
    {
        get => _isTracingActive;
        set
        {
            if (SetProperty(ref _isTracingActive, value))
            {
                Trace = null;
                RequestRedraw();
            }
        }
    }

    public double LineThickness
    {
        get => _lineThickness;
        set
        {
            if (SetProperty(ref _lineThickness, value))
            {
                RequestRedraw();
            }
        }
    }

    /// <summary>The traced point under the pointer, or null.</summary>
    public TracePoint? Trace
    {
        get => _trace;
        private set => SetProperty(ref _trace, value);
    }

    /// <summary>The equation whose key features are shown (its "f" swatch and text form the panel header).</summary>
    public GraphEquationViewModel? AnalyzedEquation
    {
        get => _analyzedEquation;
        private set
        {
            if (SetProperty(ref _analyzedEquation, value))
            {
                OnPropertyChanged(nameof(AnalyzedEquationColor));
                OnPropertyChanged(nameof(AnalyzedEquationNumber));
                OnPropertyChanged(nameof(AnalyzedEquationText));
            }
        }
    }

    // The header of the key graph features: the analyzed equation, or neutral values before one is chosen.
    public Color AnalyzedEquationColor => _analyzedEquation?.Color ?? Colors.Gray;

    public string AnalyzedEquationNumber => _analyzedEquation?.NumberText ?? string.Empty;

    public string AnalyzedEquationText => _analyzedEquation?.Text ?? string.Empty;

    public ObservableCollection<KeyFeatureViewModel> AnalysisFeatures { get; } = [];

    public bool IsAnalysisVisible
    {
        get => _isAnalysisVisible;
        set
        {
            if (SetProperty(ref _isAnalysisVisible, value))
            {
                OnPropertyChanged(nameof(IsEquationListVisible));
                OnPropertyChanged(nameof(ShowVariables));
            }
        }
    }

    public bool IsEquationListVisible => !IsAnalysisVisible;

    /// <summary>True while the key graph features are being computed.</summary>
    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        private set => SetProperty(ref _isAnalyzing, value);
    }

    /// <summary>
    /// Set by the view while the graph options are shown. The range texts are updated only then: otherwise every
    /// step of a pan would update four hidden text boxes.
    /// </summary>
    public bool AreGraphOptionsOpen
    {
        get => _areGraphOptionsOpen;
        set
        {
            if (SetProperty(ref _areGraphOptionsOpen, value) && value)
            {
                NotifyRangeTexts();
            }
        }
    }

    // The range shown in the graph options, editable as text.
    public string XMinText
    {
        get => FormatRangeValue(_viewport.XMin);
        set => SetRange(value, (viewport, number) => viewport with { XMin = number });
    }

    public string XMaxText
    {
        get => FormatRangeValue(_viewport.XMax);
        set => SetRange(value, (viewport, number) => viewport with { XMax = number });
    }

    public string YMinText
    {
        get => FormatRangeValue(_viewport.YMin);
        set => SetRange(value, (viewport, number) => viewport with { YMin = number });
    }

    public string YMaxText
    {
        get => FormatRangeValue(_viewport.YMax);
        set => SetRange(value, (viewport, number) => viewport with { YMax = number });
    }

    public DelegateCommand<GraphEquationViewModel> RemoveEquationCommand { get; }

    public DelegateCommand<GraphEquationViewModel> ToggleVisibilityCommand { get; }

    public DelegateCommand<GraphEquationViewModel> CycleColorCommand { get; }

    public DelegateCommand<GraphEquationViewModel> CycleLineStyleCommand { get; }

    public AsyncDelegateCommand<GraphEquationViewModel> AnalyzeCommand { get; }

    public DelegateCommand CloseAnalysisCommand { get; }

    public DelegateCommand ZoomInCommand { get; }

    public DelegateCommand ZoomOutCommand { get; }

    public DelegateCommand ResetViewCommand { get; }

    public DelegateCommand<TrigonometricUnit> SelectUnitCommand { get; }

    public DelegateCommand ToggleTracingCommand { get; }

    /// <summary>Equations to draw, in list order (the index is also the tracing index).</summary>
    public IReadOnlyList<GraphEquationViewModel> DrawableEquations =>
        Equations.Where(equation => equation.IsVisible && equation.Equation is not null).ToList();

    /// <summary>A new evaluation context with the current parameter values and unit.</summary>
    public GraphEvaluationContext CreateContext() => new()
    {
        TrigonometricUnit = TrigonometricUnit,
        Parameters = Variables.ToDictionary(variable => variable.Name, variable => variable.Value),
    };

    public void SetGraphSize(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _viewport = _viewport.Width <= 0 || !_hasCustomView
            ? GraphViewport.Default(width, height).MakeProportional()
            : _viewport.Resize(width, height);
        OnRangeChanged();
    }

    public void Pan(double deltaScreenX, double deltaScreenY)
    {
        _viewport = _viewport.Pan(deltaScreenX, deltaScreenY);
        _hasCustomView = true;
        Trace = null;
        OnRangeChanged();
    }

    public void Zoom(double screenX, double screenY, double factor)
    {
        _viewport = _viewport.Zoom(screenX, screenY, factor);
        _hasCustomView = true;
        OnRangeChanged();
    }

    public void UpdateTrace(TracePoint? trace)
    {
        // Pointer moves away from any curve keep reporting "nothing": no redraw for them.
        if (trace is null && Trace is null)
        {
            return;
        }

        Trace = trace;
        RequestRedraw();
    }

    /// <summary>Called by the view after the user finished typing in the last (empty) line: a new empty line is added.</summary>
    public void EnsureTrailingEmptyEquation()
    {
        if (Equations.Count == 0 || !Equations[^1].IsEmpty)
        {
            AddEmptyEquation();
        }
    }

    public void RefreshThemeColors()
    {
        foreach (GraphEquationViewModel equation in Equations)
        {
            equation.RefreshColor();
        }

        RequestRedraw();
    }

    private void AddEmptyEquation()
    {
        var equation = new GraphEquationViewModel(_nextColorIndex++, () => GraphTheme);
        equation.Changed += OnEquationChanged;
        Equations.Add(equation);
        Renumber();
    }

    /// <summary>The "f" index of every line is its position in the list.</summary>
    private void Renumber()
    {
        for (int i = 0; i < Equations.Count; i++)
        {
            Equations[i].Number = i + 1;
        }
    }

    private void RemoveEquation(GraphEquationViewModel equation)
    {
        equation.Changed -= OnEquationChanged;
        Equations.Remove(equation);
        EnsureTrailingEmptyEquation();
        Renumber();
        UpdateVariables();
        RebuildGeometry();
    }

    private void OnEquationChanged(object? sender, EventArgs e)
    {
        // As soon as the last line gets text, a new empty line appears for the next equation (like the original).
        EnsureTrailingEmptyEquation();
        UpdateVariables();
        RebuildGeometry();
    }

    /// <summary>Keeps one slider per parameter used by the equations; values are kept for parameters that stay.</summary>
    private void UpdateVariables()
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (GraphEquationViewModel equation in Equations)
        {
            if (equation.Equation is GraphEquation parsed)
            {
                names.UnionWith(parsed.Parameters);
            }
        }

        foreach (GraphVariableViewModel variable in Variables.Where(variable => !names.Contains(variable.Name)).ToList())
        {
            variable.ValueChanged -= OnVariableChanged;
            Variables.Remove(variable);
        }

        foreach (string name in names.Where(name => Variables.All(variable => variable.Name != name)))
        {
            var variable = new GraphVariableViewModel(name);
            variable.ValueChanged += OnVariableChanged;
            Variables.Add(variable);
        }

        OnPropertyChanged(nameof(HasVariables));
        OnPropertyChanged(nameof(ShowVariables));
    }

    private void OnVariableChanged(object? sender, EventArgs e) => RebuildGeometry();

    private void ZoomAtCenter(double factor) => Zoom(_viewport.Width / 2, _viewport.Height / 2, factor);

    private void ResetView()
    {
        _hasCustomView = false;
        _viewport = GraphViewport.Default(_viewport.Width, _viewport.Height).MakeProportional();
        OnRangeChanged();
    }

    private void SetRange(string text, Func<GraphViewport, double, GraphViewport> apply)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double number))
        {
            return;
        }

        GraphViewport candidate = apply(_viewport, number);
        // The same limits as zooming: a range too small or too far away for double precision is not accepted.
        if (candidate.IsUsable)
        {
            _viewport = candidate;
            _hasCustomView = true;
            OnRangeChanged();
        }
    }

    private static string FormatRangeValue(double value) => Math.Round(value, 4).ToString(CultureInfo.CurrentCulture);

    private void OnRangeChanged()
    {
        if (AreGraphOptionsOpen)
        {
            NotifyRangeTexts();
        }

        // A new view is not new geometry: the curve cache computes beyond the screen and follows the view itself.
        RequestRedraw();
    }

    private void NotifyRangeTexts()
    {
        OnPropertyChanged(nameof(XMinText));
        OnPropertyChanged(nameof(XMaxText));
        OnPropertyChanged(nameof(YMinText));
        OnPropertyChanged(nameof(YMaxText));
    }

    private void RequestRedraw() => RedrawRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>The curves themselves changed (equations, parameters, units), not only their look or the view.</summary>
    private void RebuildGeometry()
    {
        GeometryVersion++;
        RequestRedraw();
    }

    /// <summary>
    /// The analysis samples the function hundreds of thousands of times (x·sin(100x) takes about 0.4 s), so it runs
    /// off the UI thread; the panel opens at once with a progress ring.
    /// </summary>
    private async Task AnalyzeAsync(GraphEquationViewModel item)
    {
        AnalysisFeatures.Clear();
        AnalyzedEquation = item;

        if (item.Equation is not GraphEquation equation)
        {
            return;
        }

        if (equation.Kind != GraphEquationKind.FunctionOfX)
        {
            string key = equation.Kind == GraphEquationKind.FunctionOfY ? "KGFVariableIsNotX" : "KGFAnalysisNotSupported";
            AnalysisFeatures.Add(KeyFeatureViewModel.Message(string.Empty, AppStrings.Get(key)));
            IsAnalysisVisible = true;
            return;
        }

        IsAnalysisVisible = true;
        IsAnalyzing = true;
        GraphEvaluationContext context = CreateContext();
        FunctionAnalysisResult result = await Task.Run(() => FunctionAnalyzer.Analyze(equation.Expression, context));
        IsAnalyzing = false;

        // Another equation may have been chosen meanwhile; its analysis shows instead.
        if (AnalyzedEquation != item)
        {
            return;
        }

        var features = new KeyFeatureListBuilder(new AnalysisFormatter(CultureInfo.CurrentCulture));
        features.Build(result);
        foreach (KeyFeatureViewModel feature in features.Features)
        {
            AnalysisFeatures.Add(feature);
        }

        IsAnalysisVisible = true;
    }
}
