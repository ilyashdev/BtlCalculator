using Calculator.Core.Numerics;

namespace Calculator.Core.Converters;

/// <summary>
/// Units of every category, in the order of the legacy app (UnitConverterDataLoader). Factors are exact definitions
/// relative to the base unit of the category (noted per category); a few legacy values that were rounded
/// (knot, mile per hour, electronvolt, BTU, mmHg) use the exact definitions instead.
/// </summary>
public static class UnitCatalog
{
    private static readonly Dictionary<UnitCategory, IReadOnlyList<Unit>> Units = new()
    {
        // Base: millilitre
        [UnitCategory.Volume] = Sorted(
            Unit.Factor("Milliliter", 1, "1"),
            Unit.Factor("CubicCentimeter", 2, "1"),
            Unit.Factor("Liter", 3, "1000"),
            Unit.Factor("CubicMeter", 4, "1000000"),
            Unit.Factor("TeaspoonUS", 5, "4.92892159375"),
            Unit.Factor("TablespoonUS", 6, "14.78676478125"),
            Unit.Factor("FluidOunceUS", 7, "29.5735295625"),
            Unit.Factor("CupUS", 8, "236.5882365"),
            Unit.Factor("PintUS", 9, "473.176473"),
            Unit.Factor("QuartUS", 10, "946.352946"),
            Unit.Factor("GallonUS", 11, "3785.411784"),
            Unit.Factor("CubicInch", 12, "16.387064"),
            Unit.Factor("CubicFoot", 13, "28316.846592"),
            Unit.Factor("CubicYard", 14, "764554.857984"),
            Unit.Fraction("TeaspoonUK", 15, "568.26125", "96"),
            Unit.Factor("TablespoonUK", 16, "17.7581640625"),
            Unit.Factor("FluidOunceUK", 17, "28.4130625"),
            Unit.Factor("PintUK", 18, "568.26125"),
            Unit.Factor("QuartUK", 19, "1136.5225"),
            Unit.Factor("GallonUK", 20, "4546.09"),
            Unit.Factor("CoffeeCup", 22, "236.5882", isWhimsical: true),
            Unit.Factor("Bathtub", 23, "378541.2", isWhimsical: true),
            Unit.Factor("SwimmingPool", 24, "3750000000", isWhimsical: true)),

        // Base: metre
        [UnitCategory.Length] = Sorted(
            Unit.Factor("Angstrom", 1, "0.0000000001"),
            Unit.Factor("Nanometer", 2, "0.000000001"),
            Unit.Factor("Micron", 3, "0.000001"),
            Unit.Factor("Millimeter", 4, "0.001"),
            Unit.Factor("Centimeter", 5, "0.01"),
            Unit.Factor("Meter", 6, "1"),
            Unit.Factor("Kilometer", 7, "1000"),
            Unit.Factor("Inch", 8, "0.0254"),
            Unit.Factor("Foot", 9, "0.3048"),
            Unit.Factor("Yard", 10, "0.9144"),
            Unit.Factor("Mile", 11, "1609.344"),
            Unit.Factor("NauticalMile", 12, "1852"),
            Unit.Factor("Paperclip", 13, "0.035052", isWhimsical: true),
            Unit.Factor("Hand", 14, "0.18669", isWhimsical: true),
            Unit.Factor("JumboJet", 15, "76", isWhimsical: true)),

        // Base: kilogram
        [UnitCategory.Weight] = Sorted(
            Unit.Factor("Carat", 1, "0.0002"),
            Unit.Factor("Milligram", 2, "0.000001"),
            Unit.Factor("Centigram", 3, "0.00001"),
            Unit.Factor("Decigram", 4, "0.0001"),
            Unit.Factor("Gram", 5, "0.001"),
            Unit.Factor("Decagram", 6, "0.01"),
            Unit.Factor("Hectogram", 7, "0.1"),
            Unit.Factor("Kilogram", 8, "1"),
            Unit.Factor("Tonne", 9, "1000"),
            Unit.Factor("Ounce", 10, "0.028349523125"),
            Unit.Factor("Pound", 11, "0.45359237"),
            Unit.Factor("Stone", 12, "6.35029318"),
            Unit.Factor("ShortTon", 13, "907.18474"),
            Unit.Factor("LongTon", 14, "1016.0469088"),
            Unit.Factor("Snowflake", 15, "0.000002", isWhimsical: true),
            Unit.Factor("SoccerBall", 16, "0.4325", isWhimsical: true),
            Unit.Factor("Elephant", 17, "4000", isWhimsical: true),
            Unit.Factor("Whale", 18, "90000", isWhimsical: true)),

        // Base: kelvin
        [UnitCategory.Temperature] = Sorted(
            Unit.Affine("DegreesCelsius", 1, "273.15", "1", "1"),
            Unit.Affine("DegreesFahrenheit", 2, "459.67", "5", "9"),
            Unit.Affine("Kelvin", 3, "0", "1", "1")),

        // Base: joule
        [UnitCategory.Energy] = Sorted(
            Unit.Factor("Electron-Volt", 1, "0.0000000000000000001602176634"),
            Unit.Factor("Joule", 2, "1"),
            Unit.Factor("Kilojoule", 3, "1000"),
            Unit.Factor("Calorie", 4, "4.184"),
            Unit.Factor("Kilocalorie", 5, "4184"),
            Unit.Factor("Foot-Pound", 6, "1.3558179483314004"),
            Unit.Factor("BritishThermalUnit", 7, "1055.05585262"),
            Unit.Factor("Battery", 8, "9000", isWhimsical: true),
            Unit.Factor("Banana", 9, "439614", isWhimsical: true),
            Unit.Factor("SliceOfCake", 10, "1046700", isWhimsical: true),
            Unit.Factor("Kilowatthour", 166, "3600000")),

        // Base: square metre
        [UnitCategory.Area] = Sorted(
            Unit.Factor("SquareMillimeter", 1, "0.000001"),
            Unit.Factor("SquareCentimeter", 2, "0.0001"),
            Unit.Factor("SquareMeter", 3, "1"),
            Unit.Factor("Hectare", 4, "10000"),
            Unit.Factor("SquareKilometer", 5, "1000000"),
            Unit.Factor("SquareInch", 6, "0.00064516"),
            Unit.Factor("SquareFoot", 7, "0.09290304"),
            Unit.Factor("SquareYard", 8, "0.83612736"),
            Unit.Factor("Acre", 9, "4046.8564224"),
            Unit.Factor("SquareMile", 10, "2589988.110336"),
            Unit.Factor("Hand", 11, "0.012516104", isWhimsical: true),
            Unit.Factor("Paper", 12, "0.06032246", isWhimsical: true),
            Unit.Factor("SoccerField", 13, "10869.66", isWhimsical: true),
            Unit.Factor("Castle", 14, "100000", isWhimsical: true),
            Unit.Fraction("Pyeong", 15, "400", "121")),

        // Base: metre per second
        [UnitCategory.Speed] = Sorted(
            Unit.Factor("CentimetersPerSecond", 1, "0.01"),
            Unit.Factor("MetersPerSecond", 2, "1"),
            Unit.Fraction("KilometersPerHour", 3, "1000", "3600"),
            Unit.Factor("FeetPerSecond", 4, "0.3048"),
            Unit.Factor("MilesPerHour", 5, "0.44704"),
            Unit.Fraction("Knot", 6, "1852", "3600"),
            Unit.Factor("Mach", 7, "340.3"),
            Unit.Factor("Turtle", 8, "0.0894", isWhimsical: true),
            Unit.Factor("Horse", 9, "20.115", isWhimsical: true),
            Unit.Factor("Jet", 10, "245.85", isWhimsical: true)),

        // Base: second
        [UnitCategory.Time] = Sorted(
            Unit.Factor("Microsecond", 1, "0.000001"),
            Unit.Factor("Millisecond", 2, "0.001"),
            Unit.Factor("Second", 3, "1"),
            Unit.Factor("Minute", 4, "60"),
            Unit.Factor("Hour", 5, "3600"),
            Unit.Factor("Day", 6, "86400"),
            Unit.Factor("Week", 7, "604800"),
            Unit.Factor("Year", 8, "31557600")),

        // Base: watt
        [UnitCategory.Power] = Sorted(
            Unit.Factor("Watt", 1, "1"),
            Unit.Factor("Kilowatt", 2, "1000"),
            Unit.Factor("Horsepower", 3, "745.69987158227022"),
            Unit.Fraction("Foot-PoundPerMinute", 4, "1.3558179483314004", "60"),
            Unit.Fraction("BTUPerMinute", 5, "1055.05585262", "60"),
            Unit.Factor("LightBulb", 6, "60", isWhimsical: true),
            Unit.Factor("Horse", 7, "745.7", isWhimsical: true),
            Unit.Factor("TrainEngine", 8, "2982799.486329081", isWhimsical: true)),

        // Base: byte
        [UnitCategory.Data] = Sorted(
            Unit.Factor("Bit", 1, "0.125"),
            Unit.Factor("Nibble", 2, "0.5"),
            Unit.Factor("Byte", 3, "1"),
            Unit.Factor("Kilobit", 4, "125"),
            Unit.Factor("Kibibits", 5, "128"),
            Unit.Factor("Kilobyte", 6, "1000"),
            Unit.Factor("Kibibytes", 7, "1024"),
            Unit.Factor("Megabit", 8, "125000"),
            Unit.Factor("Mebibits", 9, "131072"),
            Unit.Factor("Megabyte", 10, "1000000"),
            Unit.Factor("Mebibytes", 11, "1048576"),
            Unit.Factor("Gigabit", 12, "125000000"),
            Unit.Factor("Gibibits", 13, "134217728"),
            Unit.Factor("Gigabyte", 14, "1000000000"),
            Unit.Factor("Gibibytes", 15, "1073741824"),
            Unit.Factor("Terabit", 16, "125000000000"),
            Unit.Factor("Tebibits", 17, "137438953472"),
            Unit.Factor("Terabyte", 18, "1000000000000"),
            Unit.Factor("Tebibytes", 19, "1099511627776"),
            Unit.Factor("Petabit", 20, "125000000000000"),
            Unit.Factor("Pebibits", 21, "140737488355328"),
            Unit.Factor("Petabyte", 22, "1000000000000000"),
            Unit.Factor("Pebibytes", 23, "1125899906842624"),
            Unit.Factor("Exabits", 24, "125000000000000000"),
            Unit.Factor("Exbibits", 25, "144115188075855872"),
            Unit.Factor("Exabytes", 26, "1000000000000000000"),
            Unit.Factor("Exbibytes", 27, "1152921504606846976"),
            Unit.Factor("Zetabits", 28, "125000000000000000000"),
            Unit.Factor("Zebibits", 29, "147573952589676412928"),
            Unit.Factor("Zetabytes", 30, "1000000000000000000000"),
            Unit.Factor("Zebibytes", 31, "1180591620717411303424"),
            Unit.Factor("Yottabit", 32, "125000000000000000000000"),
            Unit.Factor("Yobibits", 33, "151115727451828646838272"),
            Unit.Factor("Yottabyte", 34, "1000000000000000000000000"),
            Unit.Factor("Yobibytes", 35, "1208925819614629174706176"),
            Unit.Factor("FloppyDisk", 36, "1474560", isWhimsical: true),
            Unit.Factor("CD", 37, "700000000", isWhimsical: true),
            Unit.Factor("DVD", 38, "4700000000", isWhimsical: true)),

        // Base: pascal
        [UnitCategory.Pressure] = Sorted(
            Unit.Factor("Atmosphere", 1, "101325"),
            Unit.Factor("Bar", 2, "100000"),
            Unit.Factor("KiloPascal", 3, "1000"),
            Unit.Factor("MillimeterOfMercury", 4, "133.322387415"),
            Unit.Factor("Pascal", 5, "1"),
            Unit.Factor("PSI", 6, "6894.757293168361")),

        // Base: degree. The radian factor 180/π is set from BigMath.Pi below.
        [UnitCategory.Angle] = Sorted(
            Unit.Factor("Degree", 1, "1"),
            new Unit("Radian", 2, 180, BigMath.Pi(50), BigDecimal.Zero),
            Unit.Factor("Gradian", 3, "0.9")),
    };

    public static IReadOnlyList<UnitCategory> Categories { get; } = Enum.GetValues<UnitCategory>();

    public static IReadOnlyList<Unit> UnitsOf(UnitCategory category) => Units[category];

    public static Unit Find(UnitCategory category, string key) => Units[category].First(unit => unit.Key == key);

    /// <summary>Temperature, power and angle values can be negative (legacy SupportsNegative).</summary>
    public static bool SupportsNegative(UnitCategory category) =>
        category is UnitCategory.Temperature or UnitCategory.Power or UnitCategory.Angle;

    /// <summary>The units selected when a category is opened, depending on the region (legacy regional defaults).</summary>
    public static (Unit From, Unit To) DefaultUnits(UnitCategory category, string regionCode)
    {
        bool usCustomaryAndFahrenheit = regionCode is "US" or "FM" or "MH" or "PW";
        bool usCustomary = usCustomaryAndFahrenheit || regionCode == "LR";
        bool fahrenheit = usCustomaryAndFahrenheit || regionCode is "BS" or "KY" or "LR";

        (string from, string to) = category switch
        {
            UnitCategory.Volume => usCustomary ? ("Milliliter", "TeaspoonUS") : ("TeaspoonUS", "Milliliter"),
            UnitCategory.Length => usCustomary ? ("Centimeter", "Inch") : ("Inch", "Centimeter"),
            UnitCategory.Weight => usCustomary ? ("Kilogram", "Pound") : ("Pound", "Kilogram"),
            UnitCategory.Temperature => fahrenheit ? ("DegreesCelsius", "DegreesFahrenheit") : ("DegreesFahrenheit", "DegreesCelsius"),
            UnitCategory.Energy => ("Joule", "Kilocalorie"),
            UnitCategory.Area => usCustomary ? ("SquareMeter", "SquareFoot") : ("SquareFoot", "SquareMeter"),
            UnitCategory.Speed => usCustomary ? ("KilometersPerHour", "MilesPerHour") : ("MilesPerHour", "KilometersPerHour"),
            UnitCategory.Time => ("Hour", "Minute"),
            UnitCategory.Power => (regionCode == "GB" ? "Watt" : "Kilowatt", "Horsepower"),
            UnitCategory.Data => ("Gigabyte", "Megabyte"),
            UnitCategory.Pressure => ("Atmosphere", "Bar"),
            UnitCategory.Angle => ("Degree", "Radian"),
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };

        return (Find(category, from), Find(category, to));
    }

    private static List<Unit> Sorted(params Unit[] units) => [.. units.OrderBy(unit => unit.Order)];
}
