using Avalonia.Controls;

namespace Calculator.UI.Platform;

/// <summary>The share sheet of the system (Windows, Android, iOS). The app heads give their implementation.</summary>
public interface IShareService
{
    /// <summary>Opens the share sheet for a file.</summary>
    /// <param name="owner">The window (or phone view) the sheet belongs to.</param>
    /// <returns>False when the system has no share sheet; the caller then offers to save the file.</returns>
    Task<bool> ShareFileAsync(TopLevel owner, string path, string title);
}

/// <summary>Where the system has no share sheet (Linux has no common one; macOS is not done yet).</summary>
public sealed class NoShareService : IShareService
{
    public Task<bool> ShareFileAsync(TopLevel owner, string path, string title) => Task.FromResult(false);
}
