namespace Calculator.UI.Platform;

/// <summary>What the view models need from the system: the clipboard and opening links.</summary>
public interface ISystemServices
{
    Task SetClipboardTextAsync(string text);

    Task<string?> GetClipboardTextAsync();

    Task OpenUrlAsync(Uri url);
}
