namespace Calculator.UI.Platform;

/// <summary>Where the app keeps its files.</summary>
public static class AppFolders
{
    /// <summary>
    /// The app's data folder: %APPDATA%\BtlCalculator on Windows, ~/.config/BtlCalculator on Linux, the application
    /// support folder on macOS, the app's own files folder on Android and iOS.
    /// </summary>
    public static string Data { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), "BtlCalculator");

    /// <summary>Temporary files that can be deleted any time (the picture of a shared graph).</summary>
    public static string Cache { get; } = Path.Combine(Path.GetTempPath(), "BtlCalculator");
}
