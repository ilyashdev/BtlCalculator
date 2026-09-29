using Avalonia.Controls;
using Calculator.UI.Platform;

namespace Calculator.UI.Navigation;

/// <summary>
/// A page content shown by <see cref="Views.MainPage"/>. The main page owns the top bar and the keyboard;
/// the mode view tells it whether the history button is needed and receives the keys.
/// </summary>
public interface IModeView
{
    /// <summary>True when the top bar should show the history button (the history pane is not docked).</summary>
    bool ShowsHistoryButton { get; }

    /// <summary>True when the mode supports the "keep on top" compact window (standard mode only).</summary>
    bool SupportsKeepOnTop { get; }

    /// <summary>An element shown on the right of the top bar (the graph / equations switch of the graphing mode), or null.</summary>
    Control? TopBarAccessory { get; }

    event EventHandler? ShowsHistoryButtonChanged;

    void ToggleHistory();

    /// <summary>"Keep on top" shows only the display and the keypad.</summary>
    void SetCompactMode(bool compact);

    void HandleKeyboardInput(KeyboardInput input);

    /// <summary>
    /// Closes the topmost panel of the mode (history over the keypad, a flyout, the key graph features); used by the
    /// Android back button, which leaves the app only when nothing is open. False when no panel was open.
    /// </summary>
    bool CloseOverlay() => false;
}
