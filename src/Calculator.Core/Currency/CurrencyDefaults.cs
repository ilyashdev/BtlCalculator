using Calculator.Core.Numerics;

namespace Calculator.Core.Currency;

public static class CurrencyDefaults
{
    /// <summary>
    /// Local currencies that the original shows first, converted to US dollars (legacy DefaultFromToCurrency.json:
    /// de-DE EUR → USD, en-GB GBP → USD, sv-SE SEK → USD, ...).
    /// </summary>
    private static readonly HashSet<string> LocalFirst = ["EUR", "GBP", "AUD", "CAD", "DKK", "NOK", "SEK"];

    /// <summary>
    /// The currencies selected when the converter opens, from the currency of the user's region:
    /// US dollar → euro; the currencies above → US dollar; Swiss franc ← euro; any other local currency ← US dollar.
    /// </summary>
    public static (string From, string To) ForLocalCurrency(string localCode) => localCode switch
    {
        "USD" => ("USD", "EUR"),
        "CHF" => ("EUR", "CHF"),
        _ when LocalFirst.Contains(localCode) => (localCode, "USD"),
        _ => ("USD", localCode),
    };

    /// <summary>
    /// Money amounts are shown with two decimals (cents). Amounts too small for that keep three significant digits,
    /// so they do not turn into 0.
    /// </summary>
    public static BigDecimal RoundAmount(BigDecimal amount)
    {
        BigDecimal cents = amount.RoundToDecimalPlaces(2);
        return cents.IsZero && !amount.IsZero ? amount.RoundToSignificantDigits(3) : cents;
    }
}
