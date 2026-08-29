using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        Log.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowLog));
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
        ? Loc.Current.Format(TextKey.UiStoppedAt, RunStageWords.For(stage.Stage), stage.Completed, stage.Total)
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

    /// <summary>
    /// Whether to give the log its panel at all.
    /// </summary>
    /// <remarks>
    /// A run still going keeps it even while empty, because lines are about to arrive and a
    /// panel appearing under the cursor is worse than an empty one. A run that is over and said
    /// nothing gets no empty black strip along the foot of its page.
    /// </remarks>
    public bool ShowLog => !Quiet && (Busy || Log.Count > 0);

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
        OnPropertyChanged(nameof(ShowLog));

        using var log = RunLogFile.Open(settings.LogFile);
        var started = DateTimeOffset.UtcNow;

        void Say(string message)
        {
            log?.WriteLine(message);
            OnTheWindowsThread(() => Log.Add(message));
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
        OnPropertyChanged(nameof(ShowLog));
        }
    }

    private RunProgress? _last;

    private void Report(RunProgress progress)
    {
        // Kept current here rather than in the update, because the manifest reads it as soon as
        // the run ends and what it wants is the last step reached, not the last one drawn.
        _last = progress;

        OnTheWindowsThread(() =>
        {
            StageText = progress.Detail is { Length: > 0 }
                ? $"{RunStageWords.For(progress.Stage)} - {progress.Detail}"
                : RunStageWords.For(progress.Stage);

            OnPropertyChanged(nameof(Progress));
            OnPropertyChanged(nameof(RailText));
        });
    }

    /// <summary>
    /// Does something to this page on the thread the window is on.
    /// </summary>
    /// <remarks>
    /// The pipeline hands back its log lines and its progress on whatever thread it is on, and
    /// it awaits without capturing a context - so from the first fetch onward that is a pool
    /// thread, and adding to the log or naming the stage from there is what "the calling thread
    /// cannot access this object because a different thread owns it" means. Inline when already
    /// on the window's thread, so what a run says stays in the order it said it.
    /// </remarks>
    private static void OnTheWindowsThread(Action update)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            update();
        }
        else
        {
            Dispatcher.UIThread.Post(update);
        }
    }

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

        foreach (var part in RunCatalogue.Build(Document))
        {
            Parts.Add(part);
        }

        foreach (var colour in RunCatalogue.ColoursIn(Parts))
        {
            Colours.Add(colour);
        }

        OnPropertyChanged(nameof(VisibleParts));

        _ = RunCatalogue.LoadPicturesAsync(
            _thumbnails, [.. Parts], Document.Settings?.Offline ?? true);
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
