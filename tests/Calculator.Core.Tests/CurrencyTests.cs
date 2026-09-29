using Calculator.Core.Converters;
using Calculator.Core.Currency;
using Calculator.Core.Numerics;

namespace Calculator.Core.Tests;

public class CurrencyTests
{
    private const string Response = """
        {"result":"success","provider":"https://www.exchangerate-api.com","time_last_update_unix":1759017751,
         "base_code":"USD","rates":{"USD":1,"EUR":0.852,"RUB":82.5,"JPY":149.9,"BAD":"x","ZERO":0}}
        """;

    [Fact]
    public void ParsesRatesExactly()
    {
        ExchangeRates rates = ExchangeRates.Parse(Response);

        Assert.Equal("USD", rates.BaseCode);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1759017751), rates.UpdatedAt);
        Assert.Equal(BigDecimal.Parse("0.852"), rates.Rates["EUR"]);
        Assert.False(rates.Rates.ContainsKey("BAD"));
        Assert.False(rates.Rates.ContainsKey("ZERO"));
    }

    [Theory]
    [InlineData("""{"result":"error","error-type":"unsupported-code"}""")]
    [InlineData("not json")]
    [InlineData("""{"result":"success"}""")]
    public void RejectsBadResponses(string json) => Assert.Throws<FormatException>(() => ExchangeRates.Parse(json));

    [Fact]
    public void ConvertsThroughTheBaseCurrency()
    {
        IReadOnlyList<Unit> units = ExchangeRates.Parse(Response).ToUnits();
        Unit eur = units.Single(unit => unit.Key == "EUR");
        Unit rub = units.Single(unit => unit.Key == "RUB");
        Unit usd = units.Single(unit => unit.Key == "USD");

        Assert.Equal(BigDecimal.Parse("85.2"), UnitConversion.Convert(100, usd, eur));
        Assert.Equal(BigDecimal.Parse("96.83"), CurrencyDefaults.RoundAmount(UnitConversion.Convert(1, eur, rub)));
    }

    [Fact]
    public void NewRatesKeepTheSelectedCurrencies()
    {
        IReadOnlyList<Unit> units = ExchangeRates.Parse(Response).ToUnits();
        var engine = new UnitConverterEngine(units, units.Single(u => u.Key == "USD"), units.Single(u => u.Key == "RUB"), supportsNegative: false);
        engine.EnterDigit(2);

        IReadOnlyList<Unit> updated = ExchangeRates.Parse(Response.Replace("82.5", "80", StringComparison.Ordinal)).ToUnits();
        engine.ReplaceUnits(updated);

        Assert.Equal("RUB", engine.ToUnit.Key);
        Assert.Equal(BigDecimal.Parse("160"), engine.ToValue);
    }

    [Theory]
    [InlineData("USD", "USD", "EUR")]
    [InlineData("EUR", "EUR", "USD")]
    [InlineData("CHF", "EUR", "CHF")]
    [InlineData("RUB", "USD", "RUB")]
    public void DefaultsDependOnTheLocalCurrency(string local, string from, string to) =>
        Assert.Equal((from, to), CurrencyDefaults.ForLocalCurrency(local));

    [Theory]
    [InlineData("12.345", "12.35")]
    [InlineData("0.0001234", "0.000123")]
    [InlineData("0", "0")]
    public void RoundsAmountsToCents(string amount, string expected) =>
        Assert.Equal(BigDecimal.Parse(expected), CurrencyDefaults.RoundAmount(BigDecimal.Parse(amount)));
}
