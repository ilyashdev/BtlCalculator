using Avalonia.Media;
using Calculator.UI.Platform;

namespace Calculator.UI.Graphing;

/// <summary>Equation colors of the legacy app (EquationBrush1 … 14), one set per theme.</summary>
public static class EquationPalette
{
    private static readonly Color[] Dark =
    [
        Color.Parse("#4D92C8"), Color.Parse("#4DCDD5"), Color.Parse("#A366E0"), Color.Parse("#58A358"),
        Color.Parse("#4DDB97"), Color.Parse("#4DA688"), Color.Parse("#8A8B8C"), Color.Parse("#EF5865"),
        Color.Parse("#EB4DAF"), Color.Parse("#CA5B93"), Color.Parse("#FFCE4D"), Color.Parse("#F99255"),
        Color.Parse("#B0896D"), Color.Parse("#FFFFFF"),
    ];

    private static readonly Color[] Light =
    [
        Color.Parse("#0063B1"), Color.Parse("#00B7C3"), Color.Parse("#6600CC"), Color.Parse("#107C10"),
        Color.Parse("#00CC6A"), Color.Parse("#008055"), Color.Parse("#58595B"), Color.Parse("#E81123"),
        Color.Parse("#E3008C"), Color.Parse("#B31564"), Color.Parse("#FFB900"), Color.Parse("#F7630C"),
        Color.Parse("#8E562E"), Color.Parse("#000000"),
    ];

    public static int Count => Dark.Length;

    public static Color Get(int index, AppTheme theme) =>
        (theme == AppTheme.Dark ? Dark : Light)[((index % Count) + Count) % Count];
}
