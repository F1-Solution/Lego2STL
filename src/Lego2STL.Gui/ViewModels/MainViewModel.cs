using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Plates;
using Lego2STL.Core.Rebrickable;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;
using Lego2STL.Gui.Services;

namespace Lego2STL.Gui.ViewModels;

/// <summary>
/// The window: a rail over the run folder.
/// </summary>
/// <remarks>
/// <para>
/// A shell and nothing else. Which screen is showing is which view model the body is pointed
/// at, rather than an enum kept alongside one - so a screen cannot be selected in the rail
/// while the body shows something else, because there is nowhere for the two to disagree.
/// </para>
/// <para>
/// Starting a run lives on Setup and only there, which is what makes starting one from a screen
/// that cannot describe a run impossible rather than merely discouraged. This listens for that
/// and decides what beginning a run means.
/// </para>
/// </remarks>
public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly UserSettings _saved;
    private CancellationTokenSource? _running;

    public MainViewModel()
    {
        _saved = UserSettings.Load();

        var options = new RunOptionsViewModel
        {
            Language = _saved.DisplayLanguage,
            LDrawDirectory = _saved.LDrawDirectory,
            ElementMap = _saved.ElementMap,
            OutputDirectory = _saved.OutputDirectory,
            Printer = _saved.Printer ?? PrintBeds.Default.Name,

            // Found the same way the command line finds it, so a key already given to the
            // terminal - in the file or in the environment - is one the window starts with
            // rather than one it asks for again.
            ApiKey = ApiKeyOrNothing(),
        };

        Loc.Current.Use(options.Language);

        Runs = new RunsViewModel();

        // Narrowed to what was changed from the second run onward: on a first there is nothing
        // to have changed, and an empty list is a worse introduction than a long one.
        Setup = new SetupViewModel(options, changedOnly: RunIndex.Read().Count > 0);
        Settings = new SettingsViewModel(options, _saved, Runs);

        Setup.Started += (_, _) => _ = BeginAsync(Setup.Options.ToSettings());
        Runs.OpenRequested += (_, folder) => Open(RunDocumentViewModel.Reopened(folder));

        Current = Setup;
        _ = Runs.RefreshAsync();
    }

    /// <summary>
    /// The stored key, or nothing. A file that cannot be read is treated as no key, because
    /// starting without one and being told so beats refusing to start at all.
    /// </summary>
    private static string? ApiKeyOrNothing()
    {
        try
        {
            return RebrickableApiKey.Find();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public RunsViewModel Runs { get; }

    public SetupViewModel Setup { get; }

    public SettingsViewModel Settings { get; }

    /// <summary>The run being watched, or the one last opened. Null until there is one.</summary>
    [ObservableProperty]
    public partial RunDocumentViewModel? OpenRun { get; set; }

    /// <summary>The one settings object, which the footer and the option list both read.</summary>
    public RunOptionsViewModel Options => Setup.Options;

    public Loc Words => Loc.Current;

    // ---- Which screen ------------------------------------------------------------------------

    [ObservableProperty]
    public partial ViewModelBase Current { get; set; }

    public bool ShowingRuns => ReferenceEquals(Current, Runs);

    public bool ShowingSetup => ReferenceEquals(Current, Setup);

    public bool ShowingOpenRun => OpenRun is not null && ReferenceEquals(Current, OpenRun);

    public bool ShowingSettings => ReferenceEquals(Current, Settings);

    public void Show(ViewModelBase screen) => Current = screen;

    partial void OnCurrentChanged(ViewModelBase value)
    {
        OnPropertyChanged(nameof(ShowingRuns));
        OnPropertyChanged(nameof(ShowingSetup));
        OnPropertyChanged(nameof(ShowingOpenRun));
        OnPropertyChanged(nameof(ShowingSettings));
    }

    [RelayCommand]
    private void ShowRuns()
    {
        Show(Runs);
        _ = Runs.RefreshAsync();
    }

    [RelayCommand]
    private void ShowSetup() => Show(Setup);

    [RelayCommand]
    private void ShowOpenRun()
    {
        if (OpenRun is { } run)
        {
            Show(run);
        }
    }

    [RelayCommand]
    private void ShowSettings() => Show(Settings);

    /// <summary>
    /// Back to setting a run up. The run that was open keeps its page.
    /// </summary>
    /// <remarks>
    /// The rail's foot carries this, and Setup carries Start, so a run cannot be started from a
    /// screen that has nothing to say about what it would run.
    /// </remarks>
    [RelayCommand]
    private void NewRun() => Show(Setup);

    /// <summary>The language menu in the header, findable when the rail cannot be read.</summary>
    public LanguageChoice SelectedLanguage
    {
        get => Settings.SelectedLanguage;
        set
        {
            Settings.SelectedLanguage = value;
            OpenRun?.Reword();
            OnPropertyChanged();
        }
    }

    // ---- Running -------------------------------------------------------------------------------

    private async Task BeginAsync(RunSettings settings)
    {
        if (RunLayout.Plan(settings) is not { } layout)
        {
            return;
        }

        Remember();

        Open(RunDocumentViewModel.Live(settings, layout));

        _running?.Dispose();
        _running = new CancellationTokenSource();

        try
        {
            if (OpenRun is { } run)
            {
                await run.RunAsync(_running.Token).ConfigureAwait(true);
            }
        }
        finally
        {
            await Runs.RefreshAsync().ConfigureAwait(true);
        }
    }

    private void Open(RunDocumentViewModel run)
    {
        OpenRun?.Dispose();
        OpenRun = run;

        run.ContinueRequested += (_, settings) => _ = BeginAsync(settings);

        Show(run);
        OnPropertyChanged(nameof(ShowingOpenRun));
    }

    private void Remember()
    {
        _saved.Language = Options.Language.Tag();
        _saved.LDrawDirectory = Options.LDrawDirectory;
        _saved.ElementMap = Options.ElementMap;
        _saved.OutputDirectory = Options.OutputDirectory;
        _saved.Printer = Options.Printer;
        _saved.Save();
    }

    public void Dispose()
    {
        _running?.Dispose();
        _running = null;
        OpenRun?.Dispose();
    }
}
