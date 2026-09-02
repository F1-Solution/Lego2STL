using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Lego2STL.Gui.Services;

/// <summary>
/// Hands a file or a folder to whatever the machine opens such things with.
/// </summary>
/// <remarks>
/// Each platform has its own way of being asked. Windows will open a path directly; macOS has
/// "open"; the freedesktop convention is "xdg-open". Android and iOS have no equivalent, so
/// they install a share-sheet handler instead. Getting this wrong is not worth an error dialog
/// - the buttons that use it are conveniences, and the result is always named on screen too.
/// </remarks>
public static class Desktop
{
    public static IDesktopActions Handler { get; set; } = new ProcessStart();

    public static void Open(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            Handler.Open(path);
        }
    }

    public static void Reveal(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            Handler.Reveal(path);
        }
    }

    private sealed class ProcessStart : IDesktopActions
    {
        public void Open(string path)
        {
            try
            {
                Start(path);
            }
            catch (Exception ex) when (ex is InvalidOperationException
                                           or System.ComponentModel.Win32Exception
                                           or PlatformNotSupportedException)
            {
                // Nothing to be done, and nothing worth interrupting for.
            }
        }

        /// <summary>Opens the folder a file is in, rather than the file itself.</summary>
        public void Reveal(string path)
        {
            var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(folder))
            {
                Open(folder);
            }
        }

        private static void Start(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
                return;
            }

            var opener = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open";
            Process.Start(new ProcessStartInfo(opener, [path]) { UseShellExecute = false })?.Dispose();
        }
    }
}
