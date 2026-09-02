using FluentAssertions;
using Lego2STL.Core.LDraw;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Text;
using Xunit;

namespace Lego2STL.Tests.LDraw;

/// <summary>
/// The escalation, stopped one step short.
/// </summary>
/// <remarks>
/// A phone must not start an 80 MB download over somebody's mobile connection, so the last
/// step of the escalation is switchable. What is checked here is that switching it off
/// changes nothing else: a part that a local directory can answer is still answered, and a
/// part nobody can answer is still reported by name rather than silently dropped.
/// </remarks>
public sealed class CappedAcquisitionTests
{
    [Fact]
    public void The_whole_library_is_allowed_unless_somebody_says_otherwise()
    {
        new LDrawSourceOptions().AllowFullArchive.Should().BeTrue();
        new RunSettings().AllowFullArchive.Should().BeTrue();
        new RunSettings().LDrawOptions.AllowFullArchive.Should().BeTrue();
    }

    [Fact]
    public void Refusing_it_reaches_the_options_a_run_uses()
    {
        new RunSettings { AllowFullArchive = false }.LDrawOptions.AllowFullArchive.Should().BeFalse();
    }

    [Fact]
    public async Task A_capped_library_still_answers_from_a_local_directory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lego2stl-ldraw-" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(directory, "parts"));
        File.WriteAllText(Path.Combine(directory, "parts", "cube.dat"), "0 Cube\n0 BFC CERTIFY CCW\n");

        using var library = new EscalatingLDrawLibrary(
            new LDrawSourceOptions { LocalDirectory = directory, AllowFullArchive = false },
            _ => { },
            Strings.For(DisplayLanguages.Fallback));

        (await library.TryReadAsync("cube.dat")).Should().Contain("0 Cube");
        library.Missing.Should().BeEmpty();

        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task A_capped_offline_library_reports_what_it_cannot_answer()
    {
        var directory = Path.Combine(Path.GetTempPath(), "lego2stl-ldraw-" + Path.GetRandomFileName());
        Directory.CreateDirectory(directory);

        using var library = new EscalatingLDrawLibrary(
            new LDrawSourceOptions { LocalDirectory = directory, Offline = true, AllowFullArchive = false },
            _ => { },
            Strings.For(DisplayLanguages.Fallback));

        (await library.TryReadAsync("3705.dat")).Should().BeNull();
        library.Missing.Should().Contain("3705.dat");

        Directory.Delete(directory, recursive: true);
    }
}
