using FluentAssertions;
using Lego2STL.Core.Run;
using Lego2STL.Tests.Run;

namespace Lego2STL.Tests.Pipeline;

/// <summary>
/// The two files that go beside a run's plates.
/// </summary>
/// <remarks>
/// They live with the plates because they are about printing plates. The catalogue's scan of that
/// folder matches only *.3mf, so neighbours there are harmless.
/// </remarks>
public sealed class PrintSettingsTests
{
    [Fact]
    public void The_settings_live_beside_the_plates()
    {
        var layout = RunLayout.At(APretendRun.TempFolder("settings"));

        Path.GetDirectoryName(layout.PresetPath).Should().Be(layout.PlateDirectory);
        Path.GetDirectoryName(layout.PrintNotesPath).Should().Be(layout.PlateDirectory);
        Path.GetExtension(layout.PresetPath).Should().Be(".json");
    }
}
