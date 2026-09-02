using System;
using System.IO;
using Android.Content;
using AndroidX.Core.Content;
using Lego2STL.Gui.Services;
using Application = Android.App.Application;

namespace Lego2STL.Gui.Platforms.Android;

/// <summary>A phone has no file manager to reveal a folder in, so revealing becomes sharing.</summary>
public sealed class AndroidShareActions : IDesktopActions
{
    public void Open(string path)
    {
        try
        {
            if (Uri.IsWellFormedUriString(path, UriKind.Absolute) && !File.Exists(path))
            {
                Start(new Intent(Intent.ActionView, global::Android.Net.Uri.Parse(path)));
                return;
            }

            Share(path);
        }
        catch (Exception ex) when (ex is ActivityNotFoundException or IOException)
        {
            // A convenience button is not worth an error dialog, on any platform.
        }
    }

    public void Reveal(string path) => Open(path);

    private static void Share(string path)
    {
        var context = Application.Context;
        var uri = FileProvider.GetUriForFile(context, context.PackageName + ".fileprovider", new Java.IO.File(path));

        var intent = new Intent(Intent.ActionSend)
            .SetType("application/octet-stream")
            .PutExtra(Intent.ExtraStream, uri)
            .AddFlags(ActivityFlags.GrantReadUriPermission);

        Start(Intent.CreateChooser(intent, "Lego2STL")!);
    }

    private static void Start(Intent intent) =>
        Application.Context.StartActivity(intent.AddFlags(ActivityFlags.NewTask));
}
