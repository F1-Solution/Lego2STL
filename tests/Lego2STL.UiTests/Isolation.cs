using System;
using System.IO;
using System.Runtime.CompilerServices;
using Lego2STL.Gui.Services;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Lego2STL.UiTests;

/// <summary>
/// Keeps the suite out of the account it runs under.
/// </summary>
/// <remarks>
/// The window remembers a few preferences, and without this the tests would write them into
/// whoever's profile is running them - changing their language for real, and leaving the tests
/// depending on each other through a file on disk, so that they passed or failed by order.
/// <para>
/// The directory is one per assembly run, not one per test: every test that reads or writes a
/// preference or a saved tolerance shares it, and only the assembly-level
/// <c>CollectionBehavior(DisablesTestParallelization = true)</c> above stops two of them
/// touching the same file at once. Without it, xUnit runs test classes against each other in
/// parallel, and which one wins a write to that file - or catches another mid-write - stops
/// being something a test result can be trusted to say.
/// </para>
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
