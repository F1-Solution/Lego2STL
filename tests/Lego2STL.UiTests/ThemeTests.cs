using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using FluentAssertions;

namespace Lego2STL.UiTests;

/// <summary>
/// The window's own colours, in both variants.
/// </summary>
/// <remarks>
/// A colour named in one variant and forgotten in the other is invisible until someone opens
/// the window on a machine set the other way, and then it is a black label on a black card. The
/// loop over the names is what makes adding a token to one dictionary and not the other a
/// failure here rather than a bug report later.
/// </remarks>
public sealed class ThemeTests
{
    private static readonly string[] Tokens =
    [
        "AppWindowBackground",
        "AppSurface",
        "AppCardBorder",
        "AppText",
        "AppTextSecondary",
        "AppAccent",
        "AppAccentHover",
        "AppTextOnAccent",
        "AppSuccess",
        "AppWarningText",
        "AppWarningBand",
        "AppWarningBorder",
        "AppDanger",
        "AppLogBackground",
        "AppLogForeground",
    ];

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Every_colour_the_window_uses_is_named_in_both_variants(string variantName)
    {
        var variant = variantName == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        foreach (var token in Tokens)
        {
            Application.Current!.TryFindResource(token, variant, out var found)
                .Should().BeTrue("{0} has to be defined for the {1} variant", token, variantName);

            found.Should().BeOfType<SolidColorBrush>(
                "{0} is used as a brush", token);
        }
    }

    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_two_variants_do_not_share_a_single_definition(string variantName)
    {
        var variant = variantName == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        var other = variantName == "Light" ? ThemeVariant.Dark : ThemeVariant.Light;

        Application.Current!.TryFindResource("AppWindowBackground", variant, out var here);
        Application.Current!.TryFindResource("AppWindowBackground", other, out var there);

        ((SolidColorBrush)here!).Color.Should().NotBe(
            ((SolidColorBrush)there!).Color,
            "a ground that is the same colour in both variants is not a theme");
    }

    /// <summary>
    /// The one move that recolours every Fluent checkbox, radio, spinner and focus ring at
    /// once. Without it the window borrows whatever accent the machine happens to be set to,
    /// and the careful pair of accent colours above is contradicted by every tick box.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void The_system_accent_is_the_windows_own_rather_than_the_machines(string variantName)
    {
        var variant = variantName == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;

        Application.Current!.TryFindResource("AppAccent", variant, out var accent)
            .Should().BeTrue();

        Application.Current!.TryFindResource("SystemAccentColor", variant, out var system)
            .Should().BeTrue("the platform accent has to be overridden, not inherited");

        system.Should().BeOfType<Color>()
            .Which.Should().Be(((SolidColorBrush)accent!).Color);

        foreach (var shade in new[]
                 {
                     "SystemAccentColorLight1", "SystemAccentColorLight2", "SystemAccentColorLight3",
                     "SystemAccentColorDark1", "SystemAccentColorDark2", "SystemAccentColorDark3",
                 })
        {
            Application.Current!.TryFindResource(shade, variant, out var found)
                .Should().BeTrue("{0} has to be overridden too", shade);

            found.Should().BeOfType<Color>();
        }
    }

    [AvaloniaFact]
    public void The_window_still_follows_whichever_way_the_machine_is_set()
    {
        Application.Current!.RequestedThemeVariant.Should().Be(
            ThemeVariant.Default, "there is no theme switch in the window; the machine decides");
    }
}
