using Avalonia.Input;

namespace Calculator.UI.Platform;

/// <summary>Translates the key events of Avalonia into the <see cref="KeyboardInput"/> the calculator understands.</summary>
public static class KeyboardTranslator
{
    public static KeyboardInput? Translate(KeyEventArgs e)
    {
        KeyboardKey key = e.Key switch
        {
            Key.Enter => KeyboardKey.Enter,
            Key.Escape => KeyboardKey.Escape,
            Key.Back => KeyboardKey.Backspace,
            Key.Delete => KeyboardKey.Delete,
            Key.Left => KeyboardKey.Left,
            Key.Right => KeyboardKey.Right,
            Key.Up => KeyboardKey.Up,
            Key.Down => KeyboardKey.Down,
            Key.F2 => KeyboardKey.F2,
            Key.F3 => KeyboardKey.F3,
            Key.F4 => KeyboardKey.F4,
            Key.F5 => KeyboardKey.F5,
            Key.F6 => KeyboardKey.F6,
            Key.F7 => KeyboardKey.F7,
            Key.F8 => KeyboardKey.F8,
            Key.F9 => KeyboardKey.F9,
            Key.F12 => KeyboardKey.F12,
            _ => KeyboardKey.Other,
        };

        bool control = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        // The character the key types in the current layout ("+" for Shift+=, "7" for the numpad 7).
        char? character = e.KeySymbol is { Length: 1 } symbol && !char.IsControl(symbol[0]) ? symbol[0] : null;
        if (control && character is char letter)
        {
            character = char.ToLowerInvariant(letter);
        }

        if (key == KeyboardKey.Other && character is null)
        {
            return null;
        }

        return new KeyboardInput(key == KeyboardKey.Other ? character : null, key, control, shift);
    }
}
