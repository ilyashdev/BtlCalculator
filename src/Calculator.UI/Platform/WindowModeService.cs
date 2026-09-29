using Avalonia;
using Avalonia.Controls;

namespace Calculator.UI.Platform;

/// <summary>
/// "Keep on top": a small window that stays above other windows, like the compact overlay of the original. Works where
/// the app has a window (desktop); on phones the app is the whole screen and the button is hidden.
/// </summary>
public sealed class WindowModeService
{
    // Size of the legacy compact overlay window.
    private const double CompactWidth = 320;
    private const double CompactHeight = 394;

    private Window? _window;
    private Size _normalSize;
    private Size _normalMinimumSize;

    public bool IsKeepOnTopSupported => _window is not null;

    public bool IsKeepOnTop { get; private set; }

    /// <summary>True when the window has a size the user chose: not maximized, minimized or compact.</summary>
    public bool HasNormalSize => _window is { WindowState: WindowState.Normal } && !IsKeepOnTop;

    public void Attach(Window window) => _window = window;

    public void SetKeepOnTop(bool enabled)
    {
        if (_window is null || enabled == IsKeepOnTop)
        {
            return;
        }

        IsKeepOnTop = enabled;
        if (enabled)
        {
            _normalSize = new Size(_window.Width, _window.Height);
            _normalMinimumSize = new Size(_window.MinWidth, _window.MinHeight);
            _window.MinWidth = 0;
            _window.MinHeight = 0;
            _window.WindowState = WindowState.Normal;
            _window.Width = CompactWidth;
            _window.Height = CompactHeight;
            _window.Topmost = true;
        }
        else
        {
            _window.Topmost = false;
            _window.MinWidth = _normalMinimumSize.Width;
            _window.MinHeight = _normalMinimumSize.Height;
            _window.Width = _normalSize.Width;
            _window.Height = _normalSize.Height;
        }
    }
}
