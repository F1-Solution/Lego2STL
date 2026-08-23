namespace Lego2STL.Tests;

/// <summary>
/// Locates the reference instruction PDF used by the end-to-end tests.
/// </summary>
/// <remarks>
/// The document is a third party's copyrighted building instructions and is 10.4 MB, so it
/// is deliberately not committed. Tests that need it skip with a clear message when it is
/// absent, rather than silently passing or failing for the wrong reason.
/// </remarks>
public static class ReferenceDocument
{
    public const string FileName = "PistolaLego.pdf";

    /// <summary>Pages 2-5 hold the parts catalogue; page 6 is the "Building Instructions" divider.</summary>
    public const string PartsListRange = "2-5";

    public static int ExpectedPageCount => 126;

    /// <summary>Labels counted by hand on pages 2, 3, 4 and 5 respectively.</summary>
    public static IReadOnlyDictionary<int, int> ExpectedLabelsPerPage { get; } =
        new Dictionary<int, int> { [2] = 22, [3] = 5, [4] = 17, [5] = 9 };

    public static int ExpectedLabelTotal => ExpectedLabelsPerPage.Values.Sum();

    /// <summary>The full path, or null when the document is not available.</summary>
    public static string? TryFind()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// The full path. Call only from a test marked <see cref="DocumentFactAttribute"/> or
    /// <see cref="DocumentTheoryAttribute"/>, which is what decides to skip when the
    /// document is missing.
    /// </summary>
    public static string Require() =>
        TryFind() ?? throw new InvalidOperationException(
            $"{FileName} was not found. Mark the test [DocumentFact] or [DocumentTheory] so it skips instead.");
}
