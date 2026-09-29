using System.ComponentModel;
using System.Globalization;
using Calculator.UI.Localization;
using Calculator.UI.Platform;
using Calculator.UI.Services;
using Calculator.Core.Converters;
using Calculator.Core.Currency;
using Calculator.Core.Formatting;
using Calculator.Core.Numerics;

namespace Calculator.UI.ViewModels;

/// <summary>
/// The currency converter: a <see cref="ConverterViewModel"/> over downloaded rates, plus the rate status under it
/// (last update time, "1 USD = 0.85 EUR", the update action and the rate provider).
/// </summary>
public sealed class CurrencyViewModel : ObservableObject
{
    private const int RatioDecimals = 4;

    private readonly ExchangeRateService _service;
    private ExchangeRates? _rates;
    private ConverterViewModel? _converter;
    private bool _isUpdating;
    private string _statusText = string.Empty;

    public CurrencyViewModel(ExchangeRateService service, ISystemServices system)
    {
        _service = service;
        InitializeCommand = new AsyncDelegateCommand(InitializeAsync);
        UpdateCommand = new AsyncDelegateCommand(UpdateAsync);
        OpenProviderCommand = new AsyncDelegateCommand(() => system.OpenUrlAsync(new Uri(ExchangeRateService.AttributionUrl)));
    }

    /// <summary>Raised when the converter is created, after the first rates are available.</summary>
    public event EventHandler? ConverterCreated;

    /// <summary>Null until rates are available (from the cache or the first download).</summary>
    public ConverterViewModel? Converter => _converter;

    public bool HasRates => _rates is not null;

    public bool IsUpdating
    {
        get => _isUpdating;
        private set => SetProperty(ref _isUpdating, value);
    }

    /// <summary>"Updating rates…", "Couldn't get new rates…" or the offline message; empty when all is well.</summary>
    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (SetProperty(ref _statusText, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => StatusText.Length > 0;

    /// <summary>"Updated 28.09.2026 11:02".</summary>
    public string TimestampText
    {
        get
        {
            if (_rates is null)
            {
                return string.Empty;
            }

            DateTime local = _rates.UpdatedAt.ToLocalTime().DateTime;
            CultureInfo culture = CultureInfo.CurrentCulture;
            return AppStrings.Format("CurrencyTimestampFormat", local.ToString("d", culture), local.ToString("t", culture));
        }
    }

    /// <summary>"1 USD = 0.8520 EUR".</summary>
    public string RatioText
    {
        get
        {
            if (_converter is null)
            {
                return string.Empty;
            }

            Unit from = _converter.SelectedFromUnit.Unit;
            Unit to = _converter.SelectedToUnit.Unit;
            BigDecimal ratio = UnitConversion.Convert(BigDecimal.One, from, to);
            BigDecimal rounded = ratio.RoundToDecimalPlaces(RatioDecimals);
            if (rounded.IsZero)
            {
                rounded = ratio.RoundToSignificantDigits(RatioDecimals);
            }

            var options = NumberFormatOptions.FromCulture(CultureInfo.CurrentCulture, UnitConverterEngine.MaxInputDigits);
            return AppStrings.Format("CurrencyFromToRatioFormat", NumberFormatter.Format(BigDecimal.One, options), from.Key,
                NumberFormatter.Format(rounded, options), to.Key);
        }
    }

    public string ProviderText => ExchangeRateService.AttributionText;

    /// <summary>Executed once when the mode is first shown.</summary>
    public AsyncDelegateCommand InitializeCommand { get; }

    public AsyncDelegateCommand UpdateCommand { get; }

    public AsyncDelegateCommand OpenProviderCommand { get; }

    /// <summary>Shows the saved rates at once, then downloads fresh ones.</summary>
    private async Task InitializeAsync()
    {
        ExchangeRates? cached = await _service.LoadCachedAsync();
        if (cached is not null)
        {
            ApplyRates(cached);
        }

        await UpdateAsync();
    }

    private async Task UpdateAsync()
    {
        IsUpdating = true;
        StatusText = AppStrings.Get("UpdatingCurrencyRates");

        ExchangeRates? downloaded = await _service.DownloadAsync();
        if (downloaded is not null)
        {
            ApplyRates(downloaded);
            StatusText = string.Empty;
        }
        else
        {
            // With rates from an earlier run the converter still works; without any rates the user has to go online.
            StatusText = HasRates
                ? AppStrings.Get("CurrencyRatesUpdateFailed")
                : AppStrings.Get("OfflineStatusHyperlinkText").Replace("%HL%", string.Empty, StringComparison.Ordinal);
        }

        IsUpdating = false;
    }

    private void ApplyRates(ExchangeRates rates)
    {
        _rates = rates;
        IReadOnlyList<Unit> units = rates.ToUnits();

        if (_converter is null)
        {
            _converter = new ConverterViewModel(CreateEngine(units), new ConverterStyle(DescribeCurrency, CurrencyDefaults.RoundAmount, ShowsSupplementaryResults: false, SortsByName: true));
            _converter.PropertyChanged += OnConverterPropertyChanged;
            OnPropertyChanged(nameof(Converter));
            ConverterCreated?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _converter.ReplaceUnits(units);
        }

        OnPropertyChanged(nameof(HasRates));
        OnPropertyChanged(nameof(TimestampText));
        OnPropertyChanged(nameof(RatioText));
    }

    private static UnitConverterEngine CreateEngine(IReadOnlyList<Unit> units)
    {
        (string fromCode, string toCode) = CurrencyDefaults.ForLocalCurrency(RegionInfo.CurrentRegion.ISOCurrencySymbol);
        Unit from = units.FirstOrDefault(unit => unit.Key == fromCode) ?? units[0];
        Unit to = units.FirstOrDefault(unit => unit.Key == toCode) ?? units[0];
        return new UnitConverterEngine(units, from, to, supportsNegative: false);
    }

    private static UnitItemViewModel DescribeCurrency(Unit unit) =>
        new(unit, $"{CurrencyNames.GetName(unit.Key)} ({unit.Key})", unit.Key, CurrencyNames.GetSymbol(unit.Key));

    private void OnConverterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ConverterViewModel.SelectedFromUnit) or nameof(ConverterViewModel.SelectedToUnit))
        {
            OnPropertyChanged(nameof(RatioText));
        }
    }
}
