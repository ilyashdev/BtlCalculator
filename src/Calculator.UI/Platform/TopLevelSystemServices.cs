using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace Calculator.UI.Platform;

/// <summary>
/// <see cref="ISystemServices"/> through the top level of the app (the window on desktop, the view on phones), which
/// Avalonia gives the clipboard and the launcher of the platform. The top level is set once the UI exists.
/// </summary>
public sealed class TopLevelSystemServices : ISystemServices
{
    private TopLevel? _topLevel;

    public void Attach(TopLevel topLevel) => _topLevel = topLevel;

    public async Task SetClipboardTextAsync(string text)
    {
        if (_topLevel?.Clipboard is IClipboard clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    public async Task<string?> GetClipboardTextAsync() =>
        _topLevel?.Clipboard is IClipboard clipboard ? await clipboard.TryGetTextAsync() : null;

    public async Task OpenUrlAsync(Uri url)
    {
        if (_topLevel is not null)
        {
            await _topLevel.Launcher.LaunchUriAsync(url);
        }
    }
}
