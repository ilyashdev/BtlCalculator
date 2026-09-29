using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Calculator.UI.Localization;

/// <summary>
/// Currency names in the UI language and currency symbols. The names come from Resources/Currency/CurrencyNames.json
/// (Unicode CLDR, created by tools/Import-CurrencyNames.ps1); currencies missing there use the English name .NET
/// knows, then the code. Symbols come from the regions .NET knows ("$", "€", "₽").
/// </summary>
public static class CurrencyNames
{
    private static readonly Lazy<Dictionary<string, Dictionary<string, string>>> Localized = new(LoadLocalizedNames);
    private static readonly Lazy<Dictionary<string, RegionInfo>> Regions = new(BuildRegions);

    /// <summary>"Доллар США", "Dollar des États-Unis", "US Dollar".</summary>
    public static string GetName(string code)
    {
        // The UI culture and its parents: es-MX, es, then the English neutral resources.
        for (CultureInfo culture = CultureInfo.CurrentUICulture; ; culture = culture.Parent)
        {
            string key = culture.Name.Length == 0 ? "en" : culture.Name;
            if (Localized.Value.TryGetValue(key, out Dictionary<string, string>? names) && names.TryGetValue(code, out string? name))
            {
                return Capitalize(name, CultureInfo.CurrentUICulture);
            }

            if (culture.Name.Length == 0)
            {
                break;
            }
        }

        return Regions.Value.TryGetValue(code, out RegionInfo? region) ? region.CurrencyEnglishName : code;
    }

    public static string GetSymbol(string code) => Regions.Value.TryGetValue(code, out RegionInfo? region) ? region.CurrencySymbol : code;

    /// <summary>CLDR names are lower case in many languages ("dollar des États-Unis"); list items start with a capital.</summary>
    private static string Capitalize(string name, CultureInfo culture) =>
        name.Length == 0 ? name : culture.TextInfo.ToUpper(name[0]) + name[1..];

    private static Dictionary<string, Dictionary<string, string>> LoadLocalizedNames()
    {
        using Stream? stream = typeof(CurrencyNames).Assembly.GetManifestResourceStream("CurrencyNames.json");
        if (stream is null)
        {
            return [];
        }

        return JsonSerializer.Deserialize(stream, CurrencyNamesJsonContext.Default.DictionaryStringDictionaryStringString) ?? [];
    }

    private static Dictionary<string, RegionInfo> BuildRegions()
    {
        var regions = new Dictionary<string, RegionInfo>(StringComparer.Ordinal);
        foreach (CultureInfo culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            RegionInfo region;
            try
            {
                region = new RegionInfo(culture.Name);
            }
            catch (ArgumentException)
            {
                // Some specific cultures have no region (e.g. invariant-like ones); they carry no currency either.
                continue;
            }

            // ISO 4217 codes start with the country code (USD → US), so the issuing country wins over others using it.
            string code = region.ISOCurrencySymbol;
            if (!regions.ContainsKey(code) || code.StartsWith(region.TwoLetterISORegionName, StringComparison.Ordinal))
            {
                regions[code] = region;
            }
        }

        return regions;
    }
}

/// <summary>Source-generated JSON reader for CurrencyNames.json, so reading it needs no reflection (trimming, Native AOT).</summary>
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
internal sealed partial class CurrencyNamesJsonContext : JsonSerializerContext;
