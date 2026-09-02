using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Lego2STL.MobileSmokeTest;
using Xunit;

namespace Lego2STL.Tests.Pipeline;

/// <summary>
/// The fixture the phones run, exercised where it can be debugged.
/// </summary>
/// <remarks>
/// Everything here is written by the fixture itself: a two-line parts list and a cube in
/// LDraw's own text format, with no subfile references. That is deliberate - it needs no
/// network, no recogniser and no committed document, so a failure on a device is a fact
/// about the device and not about what the device could reach.
/// </remarks>
public sealed class SmokeFixtureTests
{
    [Fact]
    public async Task The_fixture_takes_a_parts_list_all_the_way_to_a_plate()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-smoke-" + Path.GetRandomFileName());

        var result = await PipelineFixture.RunAsync(root);

        result.Passed.Should().BeTrue(result.Detail);
        result.Detail.Should().Contain("cube");

        Directory.Delete(root, recursive: true);
    }
}
