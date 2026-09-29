namespace Calculator.UI.Navigation;

/// <summary>Every page reachable from the navigation pane (legacy NavCategory).</summary>
public enum AppMode
{
    Standard,
    Scientific,
    Graphing,
    Programmer,
    DateCalculation,
    Currency,
    Volume,
    Length,
    Weight,
    Temperature,
    Energy,
    Area,
    Speed,
    Time,
    Power,
    Data,
    Pressure,
    Angle,
    Settings,
}

public enum AppModeGroup
{
    Calculator,
    Converter,
    Settings,
}

/// <param name="TitleKey">Resource key of the name shown in the navigation pane and as the page title.</param>
/// <param name="Glyph">Glyph of Fluent System Icons (the IconFont resource).</param>
public sealed record AppModeInfo(AppMode Mode, AppModeGroup Group, string TitleKey, string Glyph)
{
    public static IReadOnlyList<AppModeInfo> All { get; } =
    [
        new(AppMode.Standard, AppModeGroup.Calculator, "StandardModeText", "\uE233"),
        new(AppMode.Scientific, AppModeGroup.Calculator, "ScientificModeText", "\uE7E3"),
        new(AppMode.Graphing, AppModeGroup.Calculator, "GraphingCalculatorModeText", "\uF343"),
        new(AppMode.Programmer, AppModeGroup.Calculator, "ProgrammerModeText", "\uF2F0"),
        new(AppMode.DateCalculation, AppModeGroup.Calculator, "DateCalculationModeText", "\uE24F"),
        new(AppMode.Currency, AppModeGroup.Converter, "CategoryName_CurrencyText", "\uE43F"),
        new(AppMode.Volume, AppModeGroup.Converter, "CategoryName_VolumeText", "\uF1D8"),
        new(AppMode.Length, AppModeGroup.Converter, "CategoryName_LengthText", "\uF67D"),
        new(AppMode.Weight, AppModeGroup.Converter, "CategoryName_WeightText", "\uEA51"),
        new(AppMode.Temperature, AppModeGroup.Converter, "CategoryName_TemperatureText", "\uF790"),
        new(AppMode.Energy, AppModeGroup.Converter, "CategoryName_EnergyText", "\uE619"),
        new(AppMode.Area, AppModeGroup.Converter, "CategoryName_AreaText", "\U000F0A89"),
        new(AppMode.Speed, AppModeGroup.Converter, "CategoryName_SpeedText", "\uF831"),
        new(AppMode.Time, AppModeGroup.Converter, "CategoryName_TimeText", "\uF2DE"),
        new(AppMode.Power, AppModeGroup.Converter, "CategoryName_PowerText", "\uF4D7"),
        new(AppMode.Data, AppModeGroup.Converter, "CategoryName_DataText", "\uE467"),
        new(AppMode.Pressure, AppModeGroup.Converter, "CategoryName_PressureText", "\uF4C8"),
        new(AppMode.Angle, AppModeGroup.Converter, "CategoryName_AngleText", "\U000F03A5"),
        new(AppMode.Settings, AppModeGroup.Settings, "SettingsHeader.Text", "\uF6AA"),
    ];

    public static AppModeInfo Of(AppMode mode) => All.First(info => info.Mode == mode);
}
