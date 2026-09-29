namespace Calculator.UI.Platform;

/// <summary>Keys the calculator reacts to, translated from the key events of the window (see KeyboardTranslator).</summary>
/// <param name="Character">The typed character (layout dependent), or null for non-character keys.</param>
public sealed record KeyboardInput(char? Character, KeyboardKey Key, bool Control, bool Shift);

public enum KeyboardKey
{
    Other,
    Enter,
    Escape,
    Delete,
    Backspace,
    Left,
    Right,
    Up,
    Down,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F12,
}
