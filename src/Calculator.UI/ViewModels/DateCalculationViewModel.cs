using System.Globalization;
using Calculator.UI.Localization;
using Calculator.Core.Dates;

namespace Calculator.UI.ViewModels;

/// <summary>Date calculation mode: difference between dates, or adding/subtracting years, months and days.</summary>
public sealed class DateCalculationViewModel : ObservableObject
{
    private bool _isDifferenceMode = true;
    private DateTime _fromDate = DateTime.Today;
    private DateTime _toDate = DateTime.Today;
    private bool _isAddMode = true;
    private int _years;
    private int _months;
    private int _days;

    public DateCalculationViewModel()
    {
        Modes = [AppStrings.Get("Date_DifferenceOption.Content"), AppStrings.Get("Date_AddSubtractOption.Content")];
    }

    public IReadOnlyList<string> Modes { get; }

    /// <summary>Numbers offered by the Years, Months and Days pickers (0 … 999, like the original).</summary>
    public IReadOnlyList<int> Amounts { get; } = Enumerable.Range(0, 1000).ToList();

    public int SelectedModeIndex
    {
        get => IsDifferenceMode ? 0 : 1;
        set => IsDifferenceMode = value == 0;
    }

    public bool IsDifferenceMode
    {
        get => _isDifferenceMode;
        set
        {
            if (SetProperty(ref _isDifferenceMode, value))
            {
                OnPropertyChanged(nameof(IsAddSubtractMode));
                OnPropertyChanged(nameof(SelectedModeIndex));
            }
        }
    }

    public bool IsAddSubtractMode => !IsDifferenceMode;

    public DateTime FromDate
    {
        get => _fromDate;
        set => SetAndRecalculate(ref _fromDate, value);
    }

    public DateTime ToDate
    {
        get => _toDate;
        set => SetAndRecalculate(ref _toDate, value);
    }

    public bool IsAddMode
    {
        get => _isAddMode;
        set => SetAndRecalculate(ref _isAddMode, value);
    }

    public int Years
    {
        get => _years;
        set => SetAndRecalculate(ref _years, value);
    }

    public int Months
    {
        get => _months;
        set => SetAndRecalculate(ref _months, value);
    }

    public int Days
    {
        get => _days;
        set => SetAndRecalculate(ref _days, value);
    }

    /// <summary>"1 year, 2 months, 3 weeks, 4 days", or "Same dates".</summary>
    public string DifferenceText
    {
        get
        {
            DateDifference difference = CurrentDifference;
            if (difference.IsZero)
            {
                return AppStrings.Get("Date_SameDates");
            }

            var parts = new List<string>();
            AddPart(parts, difference.Years, "Date_Year");
            AddPart(parts, difference.Months, "Date_Month");
            AddPart(parts, difference.Weeks, "Date_Week");
            AddPart(parts, difference.Days, "Date_Day");
            return string.Join(", ", parts);
        }
    }

    /// <summary>The total number of days, shown under the difference when it has larger units.</summary>
    public string TotalDaysText
    {
        get
        {
            DateDifference difference = CurrentDifference;
            bool onlyDays = difference.Years == 0 && difference.Months == 0 && difference.Weeks == 0;
            return difference.IsZero || onlyDays ? string.Empty : FormatCount(difference.TotalDays, "Date_Day");
        }
    }

    public string ResultDateText
    {
        get
        {
            DateOnly from = DateOnly.FromDateTime(FromDate);
            DateOnly? result = IsAddMode
                ? DateCalculator.Add(from, Years, Months, Days)
                : DateCalculator.Subtract(from, Years, Months, Days);

            return result is DateOnly date
                ? date.ToDateTime(TimeOnly.MinValue).ToString("D", CultureInfo.CurrentCulture)
                : AppStrings.Get("Date_OutOfBoundMessage");
        }
    }

    private DateDifference CurrentDifference => DateCalculator.Difference(DateOnly.FromDateTime(FromDate), DateOnly.FromDateTime(ToDate));

    private static void AddPart(List<string> parts, int count, string unitKey)
    {
        if (count != 0)
        {
            parts.Add(FormatCount(count, unitKey));
        }
    }

    /// <summary>"2 дня", "5 дней", "3 أيام": the unit in the plural form of the count (see AppStrings.Plural).</summary>
    private static string FormatCount(int count, string unitKey) =>
        count.ToString("N0", CultureInfo.CurrentCulture) + " " + AppStrings.Plural(unitKey, count);

    private void SetAndRecalculate<T>(ref T field, T value)
    {
        if (SetProperty(ref field, value))
        {
            OnPropertyChanged(nameof(DifferenceText));
            OnPropertyChanged(nameof(TotalDaysText));
            OnPropertyChanged(nameof(ResultDateText));
        }
    }
}
