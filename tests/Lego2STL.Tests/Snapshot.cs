using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using FluentAssertions;

namespace Lego2STL.Tests;

/// <summary>
/// Compares what the tool produces against a copy kept in the repository.
/// </summary>
/// <remarks>
/// <para>
/// The other tests say what each piece should do. These say what the whole thing currently
/// does, byte for byte, so that a change nobody meant to make shows up as a difference rather
/// than as a surprise months later. They cannot say the output is right - only that it is the
/// same - which is why they sit alongside the tests that check the answers and not instead of
/// them.
/// </para>
/// <para>
/// When a change is deliberate, run with LEGO2STL_UPDATE_SNAPSHOTS=1 and the kept copies are
/// rewritten. What then appears in the diff is exactly what changed, which is the point: it
/// has to be read before it is committed.
/// </para>
/// </remarks>
public static class Snapshot
{
    public const string UpdateVariable = "LEGO2STL_UPDATE_SNAPSHOTS";

    public static bool Updating =>
        Environment.GetEnvironmentVariable(UpdateVariable) is "1" or "true";

    /// <summary>
    /// The folder the kept copies live in, found from this file's own path so that it is the
    /// one in the repository rather than a copy under the build output.
    /// </summary>
    public static string Directory([CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "Snapshots");

    public static string PathFor(string name) => Path.Combine(Directory(), name);

    /// <summary>Compares text, or writes it when the copies are being brought up to date.</summary>
    public static void Matches(string name, string actual)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(actual);

        var path = PathFor(name);

        // Line endings are not what is being tested, and a repository that rewrites them would
        // otherwise fail every one of these on a fresh clone.
        var normalised = Normalise(actual);

        if (Updating || !File.Exists(path))
        {
            Write(path, normalised);

            if (!Updating)
            {
                Assert.Fail(
                    $"There was no kept copy for {name}, so one was written from this run. " +
                    "Read it, and commit it if it is right.");
            }

            return;
        }

        var expected = Normalise(File.ReadAllText(path));

        normalised.Should().Be(
            expected,
            "{0} should match the kept copy. If the change is meant, re-run with {1}=1 and read the diff.",
            name,
            UpdateVariable);
    }

    /// <summary>Compares bytes, for the files whose exact contents matter.</summary>
    public static void Matches(string name, byte[] actual)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(actual);

        var path = PathFor(name);

        if (Updating || !File.Exists(path))
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, actual);

            if (!Updating)
            {
                Assert.Fail(
                    $"There was no kept copy for {name}, so one was written from this run. " +
                    "Read it, and commit it if it is right.");
            }

            return;
        }

        var expected = File.ReadAllBytes(path);

        // Compared by length first, because a mismatch of thousands of bytes reported one at a
        // time is unreadable.
        actual.Length.Should().Be(
            expected.Length,
            "{0} should be the same size as the kept copy. If the change is meant, re-run with {1}=1.",
            name,
            UpdateVariable);

        actual.Should().Equal(expected, "{0} should match the kept copy byte for byte", name);
    }

    /// <summary>
    /// Compares an archive by what is inside it: every entry's name, and its contents once
    /// unpacked.
    /// </summary>
    /// <remarks>
    /// A 3MF is a zip, and how a zip packs its entries belongs to the runtime rather than to
    /// this. Compressing the identical bytes under .NET 10 rather than 8 produces a file four
    /// bytes different, with every entry, checksum and timestamp unchanged. Comparing the
    /// container byte for byte therefore fails on a runtime upgrade while saying nothing about
    /// the writer, so what is compared is the payload: the names, in order, and the bytes each
    /// one unpacks to. That is what the specification pins down and what a reader will see.
    /// </remarks>
    public static void ArchiveMatches(string name, byte[] actual)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(actual);

        var path = PathFor(name);

        if (Updating || !File.Exists(path))
        {
            System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, actual);

            if (!Updating)
            {
                Assert.Fail(
                    $"There was no kept copy for {name}, so one was written from this run. " +
                    "Read it, and commit it if it is right.");
            }

            return;
        }

        var expected = Unpack(File.ReadAllBytes(path));
        var found = Unpack(actual);

        found.Select(e => e.Name).Should().Equal(
            expected.Select(e => e.Name),
            "{0} should hold the same entries, in the same order, as the kept copy. " +
            "If the change is meant, re-run with {1}=1.",
            name,
            UpdateVariable);

        foreach (var (entry, bytes) in expected)
        {
            found.First(e => e.Name == entry).Bytes.Should().Equal(
                bytes, "{0} in {1} should match the kept copy byte for byte", entry, name);
        }
    }

    /// <summary>
    /// Every entry of a zip, unpacked, in the order the archive stores them - a list rather
    /// than a lookup, because the order is part of what is being compared and a lookup does
    /// not promise to keep it.
    /// </summary>
    private static List<(string Name, byte[] Bytes)> Unpack(byte[] archive)
    {
        using var stream = new MemoryStream(archive, writable: false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        var entries = new List<(string, byte[])>();

        foreach (var entry in zip.Entries)
        {
            using var contents = entry.Open();
            using var buffer = new MemoryStream();
            contents.CopyTo(buffer);
            entries.Add((entry.FullName, buffer.ToArray()));
        }

        return entries;
    }

    private static void Write(string path, string content)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private static string Normalise(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n') + "\n";
}
