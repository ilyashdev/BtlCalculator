namespace Calculator.UI;

/// <summary>
/// The name of the app. It is a brand, not text to translate, so it lives here and not in the string resources.
/// The project names (Calculator.UI, Calculator.Core) are internal and are not shown anywhere.
/// </summary>
public static class Branding
{
    /// <summary>
    /// The name of the installed app: the window title, the Android launcher, the Start menu. The same in
    /// Calculator.Android (AndroidManifest.xml, MainActivity, ApplicationTitle) and Calculator.Desktop/Package/AppxManifest.xml.
    /// </summary>
    public const string AppName = "Calculator";

    /// <summary>The brand, shown in "About" and when sharing.</summary>
    public const string ShortName = "BTL Calculator";

    /// <summary>Used in stores and on the project page.</summary>
    public const string FullName = "Better than Legacy Calculator";

    /// <summary>The project page, linked from "About".</summary>
    public const string ProjectUrl = "https://github.com/ilyashdev/BtlCalculator";

    /// <summary>The project page as it is shown: without the scheme.</summary>
    public const string ProjectUrlText = "github.com/ilyashdev/BtlCalculator";
}
