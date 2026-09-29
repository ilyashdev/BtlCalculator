using System.Text.Json;
using Calculator.Core.Converters;
using Calculator.Core.Numerics;

namespace Calculator.Core.Currency;

/// <summary>
/// Exchange rates against one base currency: 1 <see cref="BaseCode"/> = <c>Rates[code]</c> units of that currency.
/// </summary>
/// <param name="UpdatedAt">When the provider last updated the rates.</param>
public sealed record ExchangeRates(string BaseCode, DateTimeOffset UpdatedAt, IReadOnlyDictionary<string, BigDecimal> Rates)
{
    /// <summary>
    /// Parses the response of the ExchangeRate-API open endpoint (https://open.er-api.com/v6/latest/{base}):
    /// <c>{"result":"success","time_last_update_unix":…,"base_code":"USD","rates":{"USD":1,"EUR":0.92,…}}</c>.
    /// Rates are read from the JSON text, so they stay exact decimals.
    /// </summary>
    /// <exception cref="FormatException">The text is not a successful response.</exception>
    public static ExchangeRates Parse(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.GetProperty("result").GetString() != "success")
            {
                throw new FormatException("The exchange rate service returned an error.");
            }

            string baseCode = root.GetProperty("base_code").GetString() ?? throw new FormatException("The base currency is missing.");
            DateTimeOffset updatedAt = DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("time_last_update_unix").GetInt64());

            var rates = new Dictionary<string, BigDecimal>(StringComparer.Ordinal);
            foreach (JsonProperty rate in root.GetProperty("rates").EnumerateObject())
            {
                if (BigDecimal.TryParse(rate.Value.GetRawText(), out BigDecimal value) && !value.IsZero && !value.IsNegative)
                {
                    rates[rate.Name] = value;
                }
            }

            if (!rates.ContainsKey(baseCode))
            {
                rates[baseCode] = BigDecimal.One;
            }

            return new ExchangeRates(baseCode, updatedAt, rates);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new FormatException("The exchange rate response has an unexpected format.", exception);
        }
    }

    /// <summary>
    /// The currencies as converter units in code order. The base of the converter is the base currency:
    /// an amount of currency C is worth amount ÷ rate(C) base units.
    /// </summary>
    public IReadOnlyList<Unit> ToUnits() =>
        Rates
            .OrderBy(rate => rate.Key, StringComparer.Ordinal)
            .Select((rate, index) => new Unit(rate.Key, index, BigDecimal.One, rate.Value, BigDecimal.Zero))
            .ToList();
}
