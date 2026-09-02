using System.Collections.Generic;
using FluentAssertions;
using Lego2STL.Gui.Services;
using Xunit;

namespace Lego2STL.UiTests;

/// <summary>
/// The nine buttons that hand a file to the machine, and who they hand it to.
/// </summary>
/// <remarks>
/// A phone has no file manager to reveal a folder in, so revealing has to become sharing.
/// What is checked here is only that the call reaches whatever the platform installed -
/// what Android and iOS then do with it cannot be checked without a device.
/// </remarks>
public sealed class DesktopActionsTests
{
    private sealed class Recorder : IDesktopActions
    {
        public List<string> Opened { get; } = [];
        public List<string> Revealed { get; } = [];

        public void Open(string path) => Opened.Add(path);
        public void Reveal(string path) => Revealed.Add(path);
    }

    [Fact]
    public void Opening_reaches_the_installed_handler()
    {
        var original = Desktop.Handler;
        var recorder = new Recorder();

        try
        {
            Desktop.Handler = recorder;

            Desktop.Open(@"C:\runs\6324712\3mf\black.3mf");
            Desktop.Reveal(@"C:\runs\6324712\stl\3705.stl");

            recorder.Opened.Should().ContainSingle().Which.Should().EndWith("black.3mf");
            recorder.Revealed.Should().ContainSingle().Which.Should().EndWith("3705.stl");
        }
        finally
        {
            Desktop.Handler = original;
        }
    }

    [Fact]
    public void Nothing_is_handed_over_when_there_is_nothing_to_hand()
    {
        var original = Desktop.Handler;
        var recorder = new Recorder();

        try
        {
            Desktop.Handler = recorder;

            Desktop.Open("   ");
            Desktop.Reveal(string.Empty);

            recorder.Opened.Should().BeEmpty();
            recorder.Revealed.Should().BeEmpty();
        }
        finally
        {
            Desktop.Handler = original;
        }
    }
}
