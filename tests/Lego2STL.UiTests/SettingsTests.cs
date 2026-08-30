using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using FluentAssertions;
using Lego2STL.Core.Rebrickable;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;
using Lego2STL.Gui.Services;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;
using Xunit;

namespace Lego2STL.UiTests;

/// <summary>
/// The four settings that belong to this machine rather than to this run.
/// </summary>
/// <remarks>
/// Nothing here appears on the setup screen as well. One home per setting is what removed the
/// second language menu the window used to carry, and what stops a value being changed in one
/// place and read from another.
/// </remarks>
[Collection("the history")]
public sealed class SettingsTests
{
    [AvaloniaFact]
    public void A_key_typed_here_is_the_key_the_run_is_given()
    {
        var settings = ASettingsScreen(out var options);

        settings.Options.ApiKey = "secret-value";

        options.ToSettings().ApiKey.Should().Be("secret-value");
    }

    /// <summary>
    /// The key is named in the shown command and never printed into it, so a command copied
    /// out of the window and pasted into a chat does not carry someone's key with it.
    /// </summary>
    [AvaloniaFact]
    public void A_key_is_never_written_into_the_command_the_window_shows()
    {
        var settings = ASettingsScreen(out var options);

        settings.Options.ApiKey = "secret-value";

        options.CommandLine.Should().NotContain("secret-value").And.Contain("--api-key");
    }

    /// <summary>
    /// The defect this ends: the key had a box on this screen and no home on the disk, so it was
    /// typed, used once, and gone again at the next start.
    /// </summary>
    [AvaloniaFact]
    public void A_key_typed_here_is_still_here_at_the_next_start()
    {
        var settings = ASettingsScreen(out _);

        settings.Options.ApiKey = "secret-value";

        // The file the command line already reads, so one key serves both halves of the tool.
        File.Exists(RebrickableApiKey.ConfigFilePath).Should().BeTrue();
        File.ReadAllText(RebrickableApiKey.ConfigFilePath)
            .Should().Contain("rebrickableApiKey").And.Contain("secret-value");
    }

    [AvaloniaFact]
    public void Clearing_the_key_takes_it_off_the_disk_rather_than_leaving_it_behind()
    {
        var settings = ASettingsScreen(out _);

        settings.Options.ApiKey = "secret-value";
        settings.Options.ApiKey = null;

        File.ReadAllText(RebrickableApiKey.ConfigFilePath).Should().NotContain("secret-value");
    }

    [AvaloniaFact]
    public void Asking_for_quiet_reaches_the_run()
    {
        var settings = ASettingsScreen(out var options);

        settings.Options.Quiet = true;

        options.ToSettings().Quiet.Should().BeTrue();
    }

    [AvaloniaFact]
    public void A_log_named_here_reaches_the_run()
    {
        var settings = ASettingsScreen(out var options);
        var path = Path.Combine(Path.GetTempPath(), "somewhere.log");

        settings.Options.LogFile = path;

        options.ToSettings().LogFile.Should().Be(path);
    }

    [AvaloniaFact]
    public void Choosing_a_language_changes_the_window_and_is_remembered()
    {
        var settings = ASettingsScreen(out var options, out var saved);

        try
        {
            settings.SelectedLanguage = SettingsViewModel.Languages.First(c => c.Language == DisplayLanguage.Italian);

            Loc.Current.Language.Should().Be(DisplayLanguage.Italian);
            options.Language.Should().Be(DisplayLanguage.Italian);
            options.ToSettings().Language.Should().Be(DisplayLanguage.Italian);
            saved.Language.Should().Be(DisplayLanguage.Italian.Tag());

            UserSettings.Load().DisplayLanguage.Should().Be(DisplayLanguage.Italian);
        }
        finally
        {
            settings.SelectedLanguage = SettingsViewModel.Languages.First(c => c.Language == DisplayLanguage.English);
        }
    }

    [AvaloniaFact]
    public async Task Forgetting_every_run_empties_the_history()
    {
        RunIndex.ForgetEverything();

        var layout = RunLayout.At(AFolder("pistola"));
        Directory.CreateDirectory(layout.Root);
        await File.WriteAllTextAsync(layout.PartsListPath, "id;part;colour\n");
        RunIndex.Record(layout);

        var runs = new RunsViewModel();
        await runs.RefreshAsync();
        runs.Rows.Should().ContainSingle();

        var settings = new SettingsViewModel(new RunOptionsViewModel(), new UserSettings(), runs);
        settings.ForgetEveryRunCommand.Execute(null);

        RunIndex.Read().Should().BeEmpty();
        runs.Rows.Should().BeEmpty("the screen showing them has to empty with the history");
    }

    /// <summary>
    /// Each is named by its flag, because the flag is how the same thing is asked for from a
    /// terminal, and naming it is what makes the two halves obviously the same tool.
    /// </summary>
    [AvaloniaFact]
    public void The_four_are_named_by_the_flags_they_are()
    {
        var window = new Window
        {
            Width = 900,
            Height = 600,
            Content = new SettingsView { DataContext = ASettingsScreen(out _) },
        };

        window.Show();
        window.CaptureRenderedFrame();

        var shown = window.GetLogicalDescendants().OfType<Control>()
            .Select(control => control switch
            {
                TextBlock block => block.Text,
                ContentControl content => content.Content as string,
                _ => null,
            })
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        shown.Should().Contain("--api-key");
        shown.Should().Contain("--log");
        shown.Should().Contain("--quiet");
        shown.Should().Contain("--lang");

        Enum.GetNames<TextKey>().Should().NotIntersectWith(shown!,
            "a label showing a key rather than a phrase means a wording is missing");
    }

    private static SettingsViewModel ASettingsScreen(out RunOptionsViewModel options) =>
        ASettingsScreen(out options, out _);

    private static SettingsViewModel ASettingsScreen(
        out RunOptionsViewModel options, out UserSettings saved)
    {
        options = new RunOptionsViewModel();
        saved = new UserSettings();

        return new SettingsViewModel(options, saved, new RunsViewModel());
    }

    private static string AFolder(string name) => Path.Combine(
        Path.GetTempPath(), "lego2stl-settings-" + Guid.NewGuid().ToString("N"), name);

    private static SettingsViewModel ASettingsScreen(UserSettings saved) =>
        new(new RunOptionsViewModel(), saved, null);

    /// <summary>The list starts at the three shops rather than empty.</summary>
    [AvaloniaFact]
    public void The_shops_start_at_the_three_offered()
    {
        var settings = ASettingsScreen(new UserSettings());

        settings.ShopRows.Should().HaveCount(3);
        settings.ShopRows.Should().ContainSingle(row => row.IsPreferred);
    }

    /// <summary>A list already chosen is the one shown, not the offered three.</summary>
    [AvaloniaFact]
    public void A_list_already_saved_is_the_one_shown()
    {
        var saved = new UserSettings
        {
            Shops = [new Shop("Mine", "https://mine/{part}", null)],
            PreferredShop = "Mine",
        };

        var settings = ASettingsScreen(saved);

        settings.ShopRows.Should().ContainSingle().Which.Name.Should().Be("Mine");
        settings.ShopRows[0].IsPreferred.Should().BeTrue();
    }

    [AvaloniaFact]
    public void A_shop_can_be_added_and_taken_away_again()
    {
        var settings = ASettingsScreen(new UserSettings());
        var before = settings.ShopRows.Count;

        settings.AddShopCommand.Execute(null);
        settings.ShopRows.Should().HaveCount(before + 1);

        settings.RemoveShopCommand.Execute(settings.ShopRows[^1]);
        settings.ShopRows.Should().HaveCount(before);
    }

    /// <summary>Taking away the preferred shop leaves a preference that still means something.</summary>
    [AvaloniaFact]
    public void Taking_away_the_preferred_shop_promotes_another()
    {
        var settings = ASettingsScreen(new UserSettings());

        settings.RemoveShopCommand.Execute(settings.ShopRows.First(row => row.IsPreferred));

        settings.ShopRows.Should().ContainSingle(row => row.IsPreferred);
    }

    /// <summary>Only ever one preferred shop, however many are asked for.</summary>
    [AvaloniaFact]
    public void Choosing_one_shop_unchooses_the_others()
    {
        var settings = ASettingsScreen(new UserSettings());

        settings.ShopRows[2].IsPreferred = true;

        settings.ShopRows.Should().ContainSingle(row => row.IsPreferred);
        settings.ShopRows[2].IsPreferred.Should().BeTrue();
    }

    /// <summary>An edit is kept the moment it is made, as every other setting here is.</summary>
    [AvaloniaFact]
    public void An_edited_shop_is_written_down_at_once()
    {
        var saved = new UserSettings();
        var settings = ASettingsScreen(saved);

        settings.ShopRows[0].Name = "My shop";

        saved.Shops[0].Name.Should().Be("My shop");
        saved.PreferredShop.Should().Be(settings.ShopRows.First(row => row.IsPreferred).Name);
    }
}
