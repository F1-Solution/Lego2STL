using Xunit;

namespace Lego2STL.Tests;

/// <summary>
/// Locates the official LEGO building instructions used by the text-reading tests.
/// </summary>
/// <remarks>
/// <para>
/// Two books of one set, 42100. Both are a third party's copyrighted instructions and between
/// them run to 268 MB, so like <see cref="ReferenceDocument"/> they are deliberately not
/// committed and the tests that need them skip with a reason.
/// </para>
/// <para>
/// They are the pair worth having because they differ in the one way that matters: the second
/// book prints the parts catalogue and the first prints none at all, so between them they
/// cover both "find it" and "say honestly that it is not here".
/// </para>
/// </remarks>
public static class OfficialInstructions
{
    /// <summary>The book that carries the catalogue, on its last two pages.</summary>
    public const string WithCatalogue = "6324712.pdf";

    /// <summary>The companion book, which carries none.</summary>
    public const string WithoutCatalogue = "6324096.pdf";

    public static int ExpectedPageCount => 372;

    /// <summary>The catalogue pages, and how many entries each prints.</summary>
    public static IReadOnlyDictionary<int, int> ExpectedEntriesPerPage { get; } =
        new Dictionary<int, int> { [370] = 114, [371] = 109 };

    public static int ExpectedEntryTotal => ExpectedEntriesPerPage.Values.Sum();

    /// <summary>
    /// The set's piece count as the catalogue itself adds up.
    /// </summary>
    /// <remarks>
    /// Two short of the 4108 on the box, which is what the catalogue prints: it is the yardstick
    /// for reading the pages, not for the set.
    /// </remarks>
    public static int ExpectedPieceTotal => 4106;

    /// <summary>
    /// Entries the document draws twice, one impression exactly over the other.
    /// </summary>
    /// <remarks>
    /// Named because they are the ones a reader loses when it takes the text layer at face
    /// value: their characters arrive doubled, "6218209" as "66221188220099".
    /// </remarks>
    public static IReadOnlyList<string> OverprintedElements { get; } =
        ["6218209", "6263168", "4542578", "6142536", "4211651"];

    /// <summary>The full path to one of the books, or null when it is not available.</summary>
    public static string? TryFind(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    public static string Require(string fileName) =>
        TryFind(fileName) ?? throw new InvalidOperationException(
            $"{fileName} was not found. Mark the test [OfficialInstructionsFact] so it skips instead.");
}

/// <summary>
/// A fact that needs both books, and reports itself as skipped with a reason when either is
/// missing. See <see cref="DocumentFactAttribute"/> for why the skip is decided at discovery.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class OfficialInstructionsFactAttribute : FactAttribute
{
    public OfficialInstructionsFactAttribute()
    {
        var missing = new[] { OfficialInstructions.WithCatalogue, OfficialInstructions.WithoutCatalogue }
            .Where(name => OfficialInstructions.TryFind(name) is null)
            .ToList();

        if (missing.Count > 0)
        {
            Skip = $"{string.Join(" and ", missing)} not present next to the repository.";
        }
    }
}
