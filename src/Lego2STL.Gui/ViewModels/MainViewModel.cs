using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Geometry;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;
using Lego2STL.Gui.Services;

namespace Lego2STL.Gui.ViewModels;

/// <summary>Which of the four screens is showing.</summary>
public enum Screen
{
    Input,
    Options,
    Run,
    Catalogue,
}

/// <summary>
/// The window: four screens over one run.
/// </summary>
/// <remarks>
/// Nothing about the pipeline is decided here. This gathers what has been chosen, hands it to
/// the same runner the terminal uses, and shows what comes back. The only thing it adds is
/// that the run happens off the interface thread and can be stopped part-way, which a terminal
/// gets from the shell for nothing.
/// </remarks>
public sealed partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly UserSettings _saved;
    private readonly ThumbnailCache _thumbnails = new();
    private CancellationTokenSource? _running;

    public MainViewModel()
    {
        _saved = UserSettings.Load();

        Options = new RunOptionsViewModel
        {
            Language = _saved.DisplayLanguage,
            LDrawDirectory = _saved.LDrawDirectory,
            OutputDirectory = _saved.OutputDirectory,
            Printer = _saved.Printer ?? Core.Plates.PrintBeds.Default.Name,
        };

        Loc.Current.Use(Options.Language);
        Options.PropertyChanged += OnOptionChanged;
    }

    public RunOptionsViewModel Options { get; }

    public Loc Words => Loc.Current;

    // ---- Which screen ---------------------------------------------------------------------

    [ObservableProperty]
    public partial Screen Screen { get; set; } = Screen.Input;

    public bool OnInput => Screen == Screen.Input;

    public bool OnOptions => Screen == Screen.Options;

    public bool OnRun => Screen == Screen.Run;

    public bool OnCatalogue => Screen == Screen.Catalogue;

    partial void OnScreenChanged(Screen value)
    {
        OnPropertyChanged(nameof(OnInput));
        OnPropertyChanged(nameof(OnOptions));
        OnPropertyChanged(nameof(OnRun));
        OnPropertyChanged(nameof(OnCatalogue));
    }

    [RelayCommand]
    private void Show(string screen)
    {
        if (Enum.TryParse<Screen>(screen, out var wanted))
        {
            Screen = wanted;
        }
    }

    // ---- Language -------------------------------------------------------------------------

    public static IReadOnlyList<LanguageChoice> Languages => Loc.Choices;

    public LanguageChoice SelectedLanguage
    {
        get => Loc.Choices.First(c => c.Language == Options.Language);
        set
        {
            if (value is null || value.Language == Options.Language)
            {
                return;
            }

            Options.Language = value.Language;
            Loc.Current.Use(value.Language);

            _saved.Language = value.Language.Tag();
            _saved.Save();

            OnPropertyChanged();
            RefreshCatalogueText();
        }
    }

    // ---- Finding the catalogue pages -------------------------------------------------------

    [ObservableProperty]
    public partial bool Scanning { get; set; }

    [RelayCommand]
    private async Task ScanPagesAsync()
    {
        if (string.IsNullOrWhiteSpace(Options.DocumentPath) || !File.Exists(Options.DocumentPath))
        {
            return;
        }

        Scanning = true;
        try
        {
            var found = await Task.Run(() =>
            {
                using var document = Core.Pdf.PdfPageImageSource.Open(Options.DocumentPath!);
                var locator = new Core.Extraction.LabelLocator();
                var pages = new List<int>();

                for (var page = 1; page <= document.PageCount; page++)
                {
                    using var image = document.GetPage(page);
                    if (locator.Locate(image).Count > 0)
                    {
                        pages.Add(page);
                    }
                }

                return pages;
            }).ConfigureAwait(true);

            Options.Pages = found.Count > 0 ? Core.Pdf.PageRange.Format(found) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException)
        {
            Append(Words.Text(TextKey.MsgError) + ": " + ex.Message);
        }
        finally
        {
            Scanning = false;
        }
    }

    // ---- The run ----------------------------------------------------------------------------

    public ObservableCollection<string> Log { get; } = [];

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string StageText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool Busy { get; set; }

    [ObservableProperty]
    public partial string? Failure { get; set; }

    [ObservableProperty]
    public partial RunOutcome? Outcome { get; set; }

    public bool CanStart => !Busy && Options.CanRun;

    [RelayCommand]
    private async Task StartAsync()
    {
        if (Busy)
        {
            return;
        }

        var settings = Options.ToSettings();

        if (settings.Problems() is { Count: > 0 } problems)
        {
            Failure = string.Join(" ", problems);
            return;
        }

        Remember();

        Busy = true;
        Failure = null;
        Progress = 0;
        Log.Clear();
        Screen = Screen.Run;
        OnPropertyChanged(nameof(CanStart));

        _running?.Dispose();
        _running = new CancellationTokenSource();

        var progress = new Progress<RunProgress>(Report);

        try
        {
            var runner = new PipelineRunner(Append, progress);
            var outcome = await Task.Run(
                () => runner.RunAsync(settings, _running.Token), _running.Token).ConfigureAwait(true);

            Outcome = outcome;
            Failure = outcome.Error;

            StageText = outcome.Result switch
            {
                RunResult.Complete => Words.Text(TextKey.UiDone),
                RunResult.Unverified => Words.Text(TextKey.UiDone),
                _ => Words.Text(TextKey.UiFailed),
            };

            Progress = outcome.Result == RunResult.Failed ? 0 : 1;

            ShowCatalogue(outcome);

            if (outcome.Result != RunResult.Failed)
            {
                Screen = Screen.Catalogue;
            }
        }
        catch (OperationCanceledException)
        {
            StageText = Words.Text(TextKey.MsgCancelled);
            Append(Words.Text(TextKey.MsgCancelled));
        }
        finally
        {
            Busy = false;
            OnPropertyChanged(nameof(CanStart));
        }
    }

    [RelayCommand]
    private void Cancel() => _running?.Cancel();

    [RelayCommand]
    private void OpenRunFolder()
    {
        if (Outcome?.Layout is { } layout)
        {
            Desktop.Open(layout.Root);
        }
    }

    private void Report(RunProgress progress)
    {
        Progress = progress.Fraction;
        StageText = progress.Detail is { Length: > 0 }
            ? $"{Describe(progress.Stage)} - {progress.Detail}"
            : Describe(progress.Stage);
    }

    private string Describe(RunStage stage) => stage switch
    {
        RunStage.ReadingDocument => Words.Text(TextKey.UiScanning),
        RunStage.LookingUpSet => Words.Text(TextKey.UiSetNumber),
        RunStage.ReadingPartsList => Words.Text(TextKey.UiChoosePartsList),
        RunStage.WritingPartsList => Words.Text(TextKey.UiChoosePartsList),
        RunStage.GatheringShapes => Words.Text(TextKey.UiGroupLibrary),
        RunStage.BuildingShapes => Words.Text(TextKey.UiGroupGeometry),
        RunStage.ArrangingPlates => Words.Text(TextKey.UiGroupPlates),
        RunStage.WritingReport => Words.Text(TextKey.UiLog),
        RunStage.Finished => Words.Text(TextKey.UiDone),
        _ => Words.Text(TextKey.UiIdle),
    };

    private void Append(string message)
    {
        // The runner reports from a worker; the collection is bound, so it is added to on the
        // thread the interface owns.
        Avalonia.Threading.Dispatcher.UIThread.Post(() => Log.Add(message));
    }

    // ---- The catalogue -----------------------------------------------------------------------

    public ObservableCollection<CataloguePartViewModel> Parts { get; } = [];

    public ObservableCollection<string> Colours { get; } = [];

    [ObservableProperty]
    public partial string? ColourFilter { get; set; }

    [ObservableProperty]
    public partial string? Search { get; set; }

    public IEnumerable<CataloguePartViewModel> VisibleParts =>
        Parts.Where(p => p.Matches(ColourFilter, Search));

    partial void OnColourFilterChanged(string? value) => OnPropertyChanged(nameof(VisibleParts));

    partial void OnSearchChanged(string? value) => OnPropertyChanged(nameof(VisibleParts));

    /// <summary>
    /// Fills the catalogue from what a run produced. Public so it can be shown a made-up run
    /// and drawn, which is how the card layout is checked without printing anything.
    /// </summary>
    /// <remarks>
    /// Goes through the same record and the same projection a reopened run does, rather than
    /// reading the shapes it happens to still be holding: one route to a catalogue, so a run
    /// just finished and the same run opened next week cannot show different things.
    /// </remarks>
    public void ShowCatalogue(RunOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        Parts.Clear();
        Colours.Clear();

        if (outcome.Layout is not { } layout)
        {
            OnPropertyChanged(nameof(VisibleParts));
            return;
        }

        var document = RunDocument.From(
            RunManifest.From(outcome, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null), layout);

        var plates = outcome.Plates?.Plates.ToDictionary(
            p => p.ColorName, p => p.FileName, StringComparer.Ordinal) ?? [];

        foreach (var part in document.Parts)
        {
            var shapePath = outcome.ShapesByPart.ContainsKey(part.PartNumber)
                ? Path.Combine(layout.StlDirectory, part.PartNumber + ".stl")
                : null;

            var platePath = plates.TryGetValue(part.ColorName, out var plateName)
                ? Path.Combine(layout.PlateDirectory, plateName)
                : null;

            Parts.Add(new CataloguePartViewModel(part, shapePath, platePath));
        }

        foreach (var colour in Parts.Select(p => p.ColorName)
                     .Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            Colours.Add(colour);
        }

        OnPropertyChanged(nameof(VisibleParts));

        _ = LoadPicturesAsync(outcome.Settings.Offline);
    }

    /// <summary>
    /// Fetches the pictures after the list is already on screen, so the catalogue appears at
    /// once and fills in rather than waiting on a network that may not answer.
    /// </summary>
    private async Task LoadPicturesAsync(bool offline)
    {
        _thumbnails.Offline = offline;

        foreach (var part in Parts.ToList())
        {
            if (!ColorReference.Table.TryGet(ColorScheme.BrickLink, part.BrickLinkColorCode, out var colour))
            {
                continue;
            }

            part.Picture = await _thumbnails.TryGetAsync(part.PartNumber, colour).ConfigureAwait(true);
        }
    }

    private void RefreshCatalogueText()
    {
        foreach (var part in Parts)
        {
            part.OnPropertyChangedPublic(nameof(CataloguePartViewModel.WarningText));
        }
    }

    // ---- Odds and ends ------------------------------------------------------------------------

    private void OnOptionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RunOptionsViewModel.CanRun) or nameof(RunOptionsViewModel.Problem))
        {
            OnPropertyChanged(nameof(CanStart));
        }
    }

    private void Remember()
    {
        _saved.Language = Options.Language.Tag();
        _saved.LDrawDirectory = Options.LDrawDirectory;
        _saved.OutputDirectory = Options.OutputDirectory;
        _saved.Printer = Options.Printer;
        _saved.Save();
    }

    public void Dispose()
    {
        _running?.Dispose();
        _thumbnails.Dispose();
    }
}
