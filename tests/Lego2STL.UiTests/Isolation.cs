using System;
using System.IO;
using System.Runtime.CompilerServices;
using Lego2STL.Gui.Services;

namespace Lego2STL.UiTests;

/// <summary>
/// Keeps the suite out of the account it runs under.
/// </summary>
/// <remarks>
/// The window remembers a few preferences, and without this the tests would write them into
/// whoever's profile is running them - changing their language for real, and leaving the tests
/// depending on each other through a file on disk, so that they passed or failed by order.
/// </remarks>
internal static class Isolation
{
    [ModuleInitializer]
    internal static void UseATemporaryProfile()
    {
        var directory = Path.Combine(
            Path.GetTempPath(), "lego2stl-uitests-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);
        Environment.SetEnvironmentVariable(UserSettings.DirectoryVariable, directory);
    }
}
