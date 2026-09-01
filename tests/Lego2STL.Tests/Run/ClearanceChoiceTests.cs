using FluentAssertions;
using Lego2STL.Core.Run;

namespace Lego2STL.Tests.Run;

/// <summary>
/// Which clearance a build uses, and where it came from.
/// </summary>
/// <remarks>
/// One function for both front ends. Resolving this twice - once for the command line, once for
/// the window - is how the two would come to disagree about whether a preferred preset applies,
/// and the disagreement would show up as a plate that printed differently from the same settings.
/// </remarks>
public sealed class ClearanceChoiceTests
{
    private static readonly TolerancePreset Preferred =
        new("black", 0.15, Preferred: true, DateTimeOffset.UnixEpoch);

    private static readonly TolerancePreset Other =
        new("white", 0.20, Preferred: false, DateTimeOffset.UnixEpoch);

    private static readonly IReadOnlyList<TolerancePreset> Both = [Preferred, Other];

    /// <summary>Explicit always beats remembered. That is the whole precedence rule.</summary>
    [Fact]
    public void An_explicit_clearance_beats_every_preset() =>
        ClearanceChoice.Resolve(0.05, "white", Both)
            .Should().Be(new ResolvedClearance(0.05, null));

    /// <summary>
    /// An explicit zero is a real zero, not an unanswered question.
    /// </summary>
    /// <remarks>
    /// The reason the option had to become nullable. While it defaulted to zero there was no way
    /// to say "no clearance, thanks" to a machine that has a preferred preset saved.
    /// </remarks>
    [Fact]
    public void An_explicit_zero_means_no_clearance() =>
        ClearanceChoice.Resolve(0.0, null, Both)
            .Should().Be(new ResolvedClearance(0.0, null));

    [Fact]
    public void A_named_preset_beats_the_preferred_one() =>
        ClearanceChoice.Resolve(null, "white", Both)
            .Should().Be(new ResolvedClearance(0.20, "white"));

    [Fact]
    public void The_preferred_preset_applies_when_nothing_was_asked_for() =>
        ClearanceChoice.Resolve(null, null, Both)
            .Should().Be(new ResolvedClearance(0.15, "black"));

    /// <summary>The refusal to guess survives: no presets, nothing asked, true size.</summary>
    [Fact]
    public void Nothing_asked_and_nothing_preferred_is_true_size() =>
        ClearanceChoice.Resolve(null, null, [Other])
            .Should().Be(new ResolvedClearance(0.0, null));

    [Fact]
    public void Nothing_saved_at_all_is_true_size() =>
        ClearanceChoice.Resolve(null, null, [])
            .Should().Be(new ResolvedClearance(0.0, null));

    /// <summary>
    /// A name nobody saved stops the run rather than quietly meaning zero.
    /// </summary>
    /// <remarks>
    /// A typo that fell back to true size would be discovered after printing, which is the exact
    /// failure this whole feature exists to end.
    /// </remarks>
    [Fact]
    public void A_name_nobody_saved_refuses()
    {
        var act = () => ClearanceChoice.Resolve(null, "blck", Both);

        act.Should().Throw<UnknownTolerancePresetException>()
            .Which.Available.Should().BeEquivalentTo("black", "white");
    }
}
