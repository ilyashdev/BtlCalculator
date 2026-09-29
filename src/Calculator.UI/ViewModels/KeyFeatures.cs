using Calculator.UI.Localization;
using Calculator.Core.Graphing;

namespace Calculator.UI.ViewModels;

/// <summary>A key graph feature: a title and its lines.</summary>
/// <param name="IsMessage">The lines are sentences ("The function does not have …"), not formulas.</param>
public sealed record KeyFeatureViewModel(string Title, IReadOnlyList<string> Lines, bool IsMessage)
{
    public bool IsFormula => !IsMessage;

    public bool HasTitle => Title.Length > 0;

    public static KeyFeatureViewModel Message(string title, string text) => new(title, [text], IsMessage: true);
}

/// <summary>
/// Turns an analysis result into the rows of the key graph features panel, in the order of the original
/// (KeyGraphFeaturesPanel). Features that could not be computed are collected into one "too complex" row at the end.
/// </summary>
public sealed class KeyFeatureListBuilder(AnalysisFormatter format)
{
    private readonly List<KeyFeatureViewModel> _features = [];
    private readonly List<string> _tooComplex = [];

    public IReadOnlyList<KeyFeatureViewModel> Features => _features;

    public void Build(FunctionAnalysisResult result)
    {
        PeriodicFeatures? periodic = result.Periodic;

        if (periodic is null)
        {
            Add("Domain", result.Domain is null ? null : [format.Set("x", result.Domain)], "KGFDomainNone");
        }
        else
        {
            Add("Domain", periodic.DomainExclusions switch
            {
                null => null,
                [] => ["x ∈ ℝ"],
                var exclusions => [format.Families("≠", exclusions)],
            }, "KGFDomainNone");
        }

        Add("Range", result.Range is null ? [] : [format.Set("y", [result.Range])], "KGFRangeNone");

        if (periodic is null)
        {
            Add("XIntercept", result.Zeros is null ? null : result.Zeros.Count == 0 ? [] : [format.Values("x", result.Zeros)], "KGFXInterceptNone");
        }
        else
        {
            Add("XIntercept", periodic.Zeros is null ? null : periodic.Zeros.Count == 0 ? [] : [format.Families("=", periodic.Zeros)], "KGFXInterceptNone");
        }

        Add("YIntercept", result.YIntercept is double y ? [$"y = {format.Number(y)}"] : [], "KGFYInterceptNone");

        if (periodic is null)
        {
            Add("Minima", result.Minima?.Select(format.Point), "KGFMinimaNone");
            Add("Maxima", result.Maxima?.Select(format.Point), "KGFMaximaNone");
            Add("InflectionPoints", result.InflectionPoints?.Select(format.Point), "KGFInflectionPointsNone");
            Add("VerticalAsymptotes", result.VerticalAsymptotes?.Select(x => $"x = {format.Number(x)}"), "KGFVerticalAsymptotesNone");
        }
        else
        {
            Add("Minima", periodic.Minima?.Select(format.Point), "KGFMinimaNone");
            Add("Maxima", periodic.Maxima?.Select(format.Point), "KGFMaximaNone");
            Add("InflectionPoints", periodic.InflectionPoints?.Select(format.Point), "KGFInflectionPointsNone");
            Add("VerticalAsymptotes", periodic.VerticalAsymptotes is null ? null
                : periodic.VerticalAsymptotes.Count == 0 ? [] : [format.Families("=", periodic.VerticalAsymptotes)], "KGFVerticalAsymptotesNone");
        }

        Add("HorizontalAsymptotes", result.HorizontalAsymptotes.Select(value => $"y = {format.Number(value)}"), "KGFHorizontalAsymptotesNone");
        Add("ObliqueAsymptotes", result.ObliqueAsymptotes.Select(line => format.Line(line.Slope, line.Intercept)), "KGFObliqueAsymptotesNone");

        _features.Add(KeyFeatureViewModel.Message(AppStrings.Get("Parity"), AppStrings.Get(result.Parity switch
        {
            FunctionParity.Odd => "KGFParityOdd",
            FunctionParity.Even => "KGFParityEven",
            FunctionParity.Neither => "KGFParityNeither",
            _ => "KGFParityUnknown",
        })));

        _features.Add(result.Period is double period
            ? new KeyFeatureViewModel(AppStrings.Get("Periodicity"), [format.Number(period)], IsMessage: false)
            : KeyFeatureViewModel.Message(AppStrings.Get("Periodicity"), AppStrings.Get("KGFPeriodicityNotPeriodic")));

        if (periodic is null)
        {
            Add("Monotonicity", result.Monotonicity?.Select(row => $"{format.OpenInterval(row.Interval)}  {Direction(row.Direction)}"), "KGFMonotonicityError");
        }
        else
        {
            Add("Monotonicity", periodic.Monotonicity?.Select(piece => $"{format.Interval(piece)}  {Direction(piece.Direction)}"), "KGFMonotonicityError");
        }

        if (_tooComplex.Count > 0)
        {
            _features.Add(KeyFeatureViewModel.Message(string.Empty, AppStrings.Get("KGFTooComplexFeaturesError") + Environment.NewLine + string.Join("; ", _tooComplex)));
        }
    }

    private static string Direction(FunctionMonotonicity direction) => AppStrings.Get(direction switch
    {
        FunctionMonotonicity.Increasing => "KGFMonotonicityIncreasing",
        FunctionMonotonicity.Decreasing => "KGFMonotonicityDecreasing",
        FunctionMonotonicity.Constant => "KGFMonotonicityConstant",
        _ => "KGFMonotonicityUnknown",
    });

    /// <summary>Adds a row: null lines mean "too complex", no lines show the "none" message.</summary>
    private void Add(string titleKey, IEnumerable<string>? lines, string noneKey)
    {
        string title = AppStrings.Get(titleKey);
        if (lines is null)
        {
            _tooComplex.Add(title);
            return;
        }

        List<string> list = lines.ToList();
        _features.Add(list.Count > 0
            ? new KeyFeatureViewModel(title, list, IsMessage: false)
            : KeyFeatureViewModel.Message(title, AppStrings.Get(noneKey)));
    }
}
