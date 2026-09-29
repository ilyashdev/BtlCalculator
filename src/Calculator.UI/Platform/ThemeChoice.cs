namespace Calculator.UI.Platform;

/// <summary>The theme chosen in Settings. The values are saved; do not renumber.</summary>
public enum ThemeChoice
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>The theme the app shows (the system setting resolved).</summary>
public enum AppTheme
{
    Light,
    Dark,
}
