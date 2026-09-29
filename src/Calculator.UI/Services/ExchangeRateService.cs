using Calculator.Core.Currency;
using Calculator.UI.Platform;

namespace Calculator.UI.Services;

/// <summary>
/// Downloads exchange rates from the free ExchangeRate-API open endpoint (no key; the terms ask for the attribution
/// "Rates By Exchange Rate API" next to the rates) and keeps the last response on disk for offline use.
/// </summary>
public sealed class ExchangeRateService
{
    public const string AttributionText = "Rates By Exchange Rate API";
    public const string AttributionUrl = "https://www.exchangerate-api.com";

    private const string RatesUrl = "https://open.er-api.com/v6/latest/USD";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly HttpClient _http;
    private readonly string _cachePath;

    public ExchangeRateService()
        : this(new HttpClient { Timeout = RequestTimeout }, Path.Combine(AppFolders.Data, "exchange-rates.json"))
    {
    }

    /// <param name="http">The client that downloads the rates (tests give one that answers without the network).</param>
    /// <param name="cachePath">The file the last rates are kept in.</param>
    public ExchangeRateService(HttpClient http, string cachePath)
    {
        _http = http;
        _cachePath = cachePath;
    }

    /// <summary>The rates saved by the last successful download, or null.</summary>
    public async Task<ExchangeRates?> LoadCachedAsync()
    {
        if (!File.Exists(_cachePath))
        {
            return null;
        }

        try
        {
            return ExchangeRates.Parse(await File.ReadAllTextAsync(_cachePath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            // A damaged cache is treated as no cache; the next download replaces it.
            return null;
        }
    }

    /// <summary>Downloads the current rates and saves them; returns null when offline or the service fails.</summary>
    public async Task<ExchangeRates?> DownloadAsync()
    {
        string json;
        ExchangeRates rates;
        try
        {
            json = await _http.GetStringAsync(RatesUrl);
            rates = ExchangeRates.Parse(json);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or FormatException)
        {
            return null;
        }

        await SaveCacheAsync(json);
        return rates;
    }

    private async Task SaveCacheAsync(string json)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            await File.WriteAllTextAsync(_cachePath, json);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The rates are still used in this session; only offline start-up loses them.
        }
    }
}
