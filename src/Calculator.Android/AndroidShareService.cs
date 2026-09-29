using Android.Content;
using AndroidX.Core.Content;
using Avalonia.Controls;
using Calculator.UI.Platform;

namespace Calculator.Android;

/// <summary>
/// The Android share sheet. The picture is offered to other apps through the file provider of the app (declared in
/// AndroidManifest.xml for the cache folder, where the picture is written).
/// </summary>
public sealed class AndroidShareService : IShareService
{
    public Task<bool> ShareFileAsync(TopLevel owner, string path, string title)
    {
        Context context = global::Android.App.Application.Context;
        global::Android.Net.Uri uri = FileProvider.GetUriForFile(context, context.PackageName + ".files", new Java.IO.File(path))!;

        var send = new Intent(Intent.ActionSend);
        send.SetType("image/png");
        send.PutExtra(Intent.ExtraStream, uri);
        send.AddFlags(ActivityFlags.GrantReadUriPermission);

        // Started from the application context, so the chooser needs a task of its own.
        Intent chooser = Intent.CreateChooser(send, title)!;
        chooser.AddFlags(ActivityFlags.NewTask | ActivityFlags.GrantReadUriPermission);
        context.StartActivity(chooser);
        return Task.FromResult(true);
    }
}
