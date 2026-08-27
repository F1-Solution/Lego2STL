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
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;
using Lego2STL.Gui.Services;

namespace Lego2STL.Gui.ViewModels;

/// <summary>
/// One run, on a page of its own.
/// </summary>
/// <remarks>
/// <para>
/// Wraps a <see cref="RunDocument"/> - the one projection - with the part of a run that only
/// exists while it is happening: the bar, the step, the log, and a way to stop it. A run read
/// back off the disk is the same page with that facet inert, which is why a run watched and the
/// same run reopened cannot come apart: they are the same type over the same record.
/// </para>
/// <para>
/// Nothing here clears the log. A new run is a new document with a page of its own, so the
/// previous run's log survives both on its page and in its folder.
/// </para>
/// </remarks>
public sealed partial class RunDocumentViewModel : ViewModelBase, IDisposable
{
    private readonly ThumbnailCache _thumbnails = new();
    private CancellationTokenSource? _running;
    private RunSettings? _settings;
    private RunLayout? _layout;

    private RunDocumentViewModel(RunDocument document)
    {
        Document = document;
        Fill();
    }

    /// <summary>A page for a document that already exists, however it was arrived at.</summary>
    public static RunDocumentViewModel Of(RunDocument document) => new(document);

    /// <summary>A run about to happen, watched as it does.</summary>
    public static RunDocumentViewModel Live(RunSettings settings, RunLayout layout) =>
        new(RunDocument.From(RunManifest.Starting(settings, DateTimeOffset.UtcNow), layout))
        {
            _settings = settings,
            _layout = layout,
            IsLive = true,
        };

    /// <summary>A run read back off the disk. Its live facet is inert.</summary>
    public static RunDocumentViewModel Reopened(RunFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        var page = new RunDocumentViewModel(folder.ToDocument()) { _layout = folder.Layout };

        foreach (var line in RunLogFile.Read(folder.Layout.LogPath))
        {
            page.Log.Add(line);
        }

        return page;
    }

    /// <summary>The projection. Replaced whole; never edited.</summary>
    public RunDocument Document { get; private set; }

    public string Name => Document.Name;

    public string Folder => Document.Folder;

    public string CommandLine => Document.CommandLine;

    public bool IsLive { get; private init; }

    // ---- How it stands ---------------------------------------------------------------------

    /// <summary>
    /// One of five words, and never "done" for a run that could not be verified.
    /// </summary>
    /// <remarks>
    /// The disagreement this ends: the command line returned exit code 2 for a run this window
    /// was showing as finished, at full, with nothing amiss.
    /// </remarks>
    public string StatusText => Loc.Current.Text(Document.Status switch
    {
        RunStatus.Running => TextKey.UiStatusRunning,
        RunStatus.Complete => TextKey.UiStatusComplete,
        RunStatus.NeedsDecision => TextKey.UiStatusNeedsDecision,
        RunStatus.Failed => TextKey.UiStatusFailed,
        _ => TextKey.UiStatusStopped,
    });

    /// <summary>How far it got while running, and how it ended once it has.</summary>
    public string RailText => Document.IsRunning
        ? Progress.ToString("P0", System.Globalization.CultureInfo.CurrentCulture)
        : StatusText;

    public double Progress => Document.Progress;

    public bool NeedsDecision => Document.NeedsDecision;

    public bool Failing => Document.Failing;

    public bool Busy => IsLive && _running is { IsCancellationRequested: false } && Document.IsRunning;

    [ObservableProperty]
    public partial string StageText { get; set; } = string.Empty;

    /// <summary>Where it stopped, for a run that did not finish.</summary>
    public string? StoppedAt => Document.LastStage is { } stage && !Document.IsRunning
                                && Document.Status != RunStatus.Complete
        ? Loc.Current.Format(TextKey.UiStoppedAt, Describe(stage.Stage), stage.Completed, stage.Total)
        : null;

    // ---- What it has to answer for ---------------------------------------------------------

    public bool HasProblem => Document.Unread.Count > 0
                              || Document.Failed.Count > 0
                              || Document.Error is not null;

    /// <summary>Each cause named with its reason, so the next move is obvious.</summary>
    public string ProblemText
    {
        get
        {
            var lines = new List<string>();

            if (Document.Error is { Length: > 0 } error)
            {
                lines.Add(error);
            }

            lines.AddRange(Document.Unread);
            lines.AddRange(Document.Failed.Select(f => $"{f.Part}: {f.Reason}"));

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>What this build could not know about the run, said rather than guessed at.</summary>
    public string? NoticeText =>
        Document.FromNewerBuild ? Loc.Current.Text(TextKey.UiNewerManifest)
        : !Document.ManifestKnown ? Loc.Current.Text(TextKey.UiNoManifest)
        : null;

    public bool HasNotice => NoticeText is not null;

    /// <summary>Offered where the answer is to run it again rather than to guess at it.</summary>
    public bool CanRunItAgain => !Document.ManifestKnown && File.Exists(Document.PartsListPath);

    public bool CanContinue => NeedsDecision && File.Exists(Document.PartsListPath);

    // ---- The log -----------------------------------------------------------------------------

    public ObservableCollection<string> Log { get; } = [];

    /// <summary>True when --quiet means the log is on the disk rather than on the screen.</summary>
    public bool Quiet => _settings?.Quiet ?? false;

    // ---- The catalogue -----------------------------------------------------------------------

    public ObservableCollection<CataloguePartViewModel> Parts { get; } = [];

    public ObservableCollection<string> Colours { get; } = [];

    [ObservableProperty]
    public partial string? ColourFilter { get; set; }

    [ObservableProperty]
    public partial string? Search { get; set; }

    public IEnumerable<CataloguePartViewModel> VisibleParts =>
        Parts.Where(part => part.Matches(ColourFilter, Search));

    partial void OnColourFilterChanged(string? value) => OnPropertyChanged(nameof(VisibleParts));

    partial void OnSearchChanged(string? value) => OnPropertyChanged(nameof(VisibleParts));

    /// <summary>Raised when the next run should begin, with the settings it should use.</summary>
    public event EventHandler<RunSettings>? ContinueRequested;

    [RelayCommand]
    private void OpenFolder() => Desktop.Open(Document.Folder);

    [RelayCommand]
    private void OpenPartsList() => Desktop.Open(Document.PartsListPath);

    /// <summary>
    /// Carries on from the parts list this run already wrote, in the same folder.
    /// </summary>
    /// <remarks>
    /// The way out of a run that could not be verified: the list is the thing that can be
    /// corrected by hand, and feeding it back in lands on the same folder rather than scattering
    /// a second copy beside it.
    /// </remarks>
    [RelayCommand]
    private void ContinueFromPartsList() => Again();

    [RelayCommand]
    private void RunItAgain() => Again();

    private void Again()
    {
        if (!File.Exists(Document.PartsListPath))
        {
            return;
        }

        var settings = (Document.Settings ?? new RunSettings()) with
        {
            Kind = InputKind.PartsList,
            InputPath = Document.PartsListPath,
            SetNumber = null,
            Pages = null,
        };

        ContinueRequested?.Invoke(this, settings);
    }

    [RelayCommand]
    private void Cancel() => _running?.Cancel();

    /// <summary>
    /// Runs the pipeline and keeps this page in step with it.
    /// </summary>
    /// <remarks>
    /// The document is replaced at the end from the same projection a reopened run uses, so
    /// what is on screen when a run finishes and what is on screen when it is opened again next
    /// week are the same thing by construction.
    /// </remarks>
    public async Task RunAsync(CancellationToken outer = default)
    {
        if (!IsLive || _settings is not { } settings || _layout is not { } layout)
        {
            return;
        }

        _running = CancellationTokenSource.CreateLinkedTokenSource(outer);
        OnPropertyChanged(nameof(Busy));

        using var log = RunLogFile.Open(settings.LogFile);
        var started = DateTimeOffset.UtcNow;

        void Say(string message)
        {
            log?.WriteLine(message);
            Log.Add(message);
        }

        RunIndex.Record(layout);

        try
        {
            var runner = new PipelineRunner(Say, new Watcher(Report));
            var outcome = await runner.RunAsync(settings, _running.Token).ConfigureAwait(true);

            Replace(RunDocument.From(
                RunManifest.From(outcome, started, DateTimeOffset.UtcNow, _last), layout));
        }
        catch (OperationCanceledException)
        {
            StageText = Loc.Current.Text(TextKey.MsgCancelled);
            Say(StageText);

            // The pipeline has already recorded the stop; read back what it wrote rather than
            // inventing a second account of the same run.
            Replace(RunFolder.Read(layout.Root).ToDocument());
        }
        finally
        {
            log?.Flush();
            _running?.Dispose();
            _running = null;
            OnPropertyChanged(nameof(Busy));
        }
    }

    private RunProgress? _last;

    private void Report(RunProgress progress)
    {
        _last = progress;

        StageText = progress.Detail is { Length: > 0 }
            ? $"{Describe(progress.Stage)} - {progress.Detail}"
            : Describe(progress.Stage);

        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(RailText));
    }

    private static string Describe(RunStage stage) => Loc.Current.Text(stage switch
    {
        RunStage.ReadingDocument => TextKey.UiStageReadingDocument,
        RunStage.LookingUpSet => TextKey.UiStageLookingUpSet,
        RunStage.ReadingPartsList => TextKey.UiStageReadingPartsList,
        RunStage.WritingPartsList => TextKey.UiStageWritingPartsList,
        RunStage.GatheringShapes => TextKey.UiStageGatheringShapes,
        RunStage.BuildingShapes => TextKey.UiStageBuildingShapes,
        RunStage.ArrangingPlates => TextKey.UiStageArrangingPlates,
        RunStage.WritingReport => TextKey.UiStageWritingReport,
        RunStage.Finished => TextKey.UiDone,
        _ => TextKey.UiIdle,
    });

    private void Replace(RunDocument document)
    {
        Document = document;
        Fill();

        foreach (var name in new[]
                 {
                     nameof(Document), nameof(StatusText), nameof(RailText), nameof(Progress),
                     nameof(NeedsDecision), nameof(Failing), nameof(HasProblem), nameof(ProblemText),
                     nameof(NoticeText), nameof(HasNotice), nameof(CanRunItAgain), nameof(CanContinue),
                     nameof(StoppedAt), nameof(CommandLine), nameof(Busy), nameof(VisibleParts),
                 })
        {
            OnPropertyChanged(name);
        }
    }

    /// <summary>Builds the catalogue from the document, which is the only source it has.</summary>
    private void Fill()
    {
        Parts.Clear();
        Colours.Clear();

        foreach (var part in Document.Parts)
        {
            var shape = Path.Combine(Document.StlDirectory, part.PartNumber + ".stl");
            var plate = PlateFor(part.ColorName);

            Parts.Add(new CataloguePartViewModel(
                part,
                File.Exists(shape) ? shape : null,
                plate));
        }

        foreach (var colour in Parts.Select(p => p.ColorName)
                     .Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            Colours.Add(colour);
        }

        OnPropertyChanged(nameof(VisibleParts));

        _ = LoadPicturesAsync();
    }

    /// <summary>
    /// The plate a colour's pieces were laid out on, found by looking rather than recorded.
    /// </summary>
    /// <remarks>
    /// A plate's file is named for its colour, so the folder answers this without the manifest
    /// having to carry a second list that could disagree with what is actually there.
    /// </remarks>
    private string? PlateFor(string colourName)
    {
        try
        {
            if (!Directory.Exists(Document.PlateDirectory))
            {
                return null;
            }

            var wanted = colourName.Replace(' ', '-').ToLowerInvariant();

            return Directory.EnumerateFiles(Document.PlateDirectory, "*.3mf")
                .FirstOrDefault(file =>
                    Path.GetFileNameWithoutExtension(file)
                        .StartsWith(wanted, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private async Task LoadPicturesAsync()
    {
        _thumbnails.Offline = Document.Settings?.Offline ?? true;

        foreach (var part in Parts.ToList())
        {
            if (!ColorReference.Table.TryGet(ColorScheme.BrickLink, part.BrickLinkColorCode, out var colour))
            {
                continue;
            }

            part.Picture = await _thumbnails.TryGetAsync(part.PartNumber, colour).ConfigureAwait(true);
        }
    }

    /// <summary>Re-reads everything worded, after the language has been changed.</summary>
    public void Reword()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(RailText));
        OnPropertyChanged(nameof(NoticeText));
        OnPropertyChanged(nameof(StoppedAt));

        foreach (var part in Parts)
        {
            part.OnPropertyChangedPublic(nameof(CataloguePartViewModel.WarningText));
        }
    }

    public void Dispose()
    {
        _running?.Dispose();
        _running = null;
        _thumbnails.Dispose();
    }

    private sealed class Watcher(Action<RunProgress> look) : IProgress<RunProgress>
    {
        public void Report(RunProgress value) => look(value);
    }
}
