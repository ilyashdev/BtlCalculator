#if WINDOWS
using System.Runtime.Versioning;
using Avalonia.Controls;
using Calculator.UI.Platform;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinRT.Interop;

namespace Calculator.Desktop;

/// <summary>The Windows share sheet, opened for the app window.</summary>
[SupportedOSPlatform("windows10.0.17763.0")]
public sealed class WindowsShareService : IShareService
{
    public async Task<bool> ShareFileAsync(TopLevel owner, string path, string title)
    {
        if (owner.TryGetPlatformHandle()?.Handle is not nint window || window == 0)
        {
            return false;
        }

        StorageFile file = await StorageFile.GetFileFromPathAsync(path);
        DataTransferManager manager = DataTransferManagerInterop.GetForWindow(window);

        // The sheet asks for the data once, right after it opens.
        void OnDataRequested(DataTransferManager sender, DataRequestedEventArgs e)
        {
            manager.DataRequested -= OnDataRequested;
            e.Request.Data.Properties.Title = title;
            e.Request.Data.SetStorageItems([file]);
        }

        manager.DataRequested += OnDataRequested;
        DataTransferManagerInterop.ShowShareUIForWindow(window);
        return true;
    }
}
#endif
