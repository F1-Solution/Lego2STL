using System;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;

namespace Lego2STL.Gui.ViewModels;

/// <summary>
/// One run in the list, read from its own folder.
/// </summary>
/// <remarks>
/// Every word here comes from the folder this row names, never from anything stored beside the
/// path in the history. That is what makes a run deleted by hand grey out on its own, and what
/// makes it impossible for a row to claim a run finished while the folder it points at says it
/// stopped.
/// </remarks>
public sealed partial class RunRowViewModel : ViewModelBase
{
    private readonly RunFolder _folder;

    public RunRowViewModel(RunFolder folder)
    {
        ArgumentNullException.ThrowIfNull(folder);

        _folder = folder;
        Document = folder.ToDocument();
    }

    public RunDocument Document { get; }

    public string Folder => Document.Folder;

    public string Name => Document.Name;

    /// <summary>The command that made this run, kept so it can be repeated in a terminal.</summary>
    public string CommandLine => Document.CommandLine;

    /// <summary>The folder has gone. The row stays, so it can be forgotten deliberately.</summary>
    public bool Missing => !_folder.Exists;

    /// <summary>Faded rather than hidden, so a run that was deleted can still be tidied away.</summary>
    public double Dimmed => Missing ? 0.55 : 1;

    public string StatusText => Missing
        ? Loc.Current.Text(TextKey.UiFolderMissing)
        : Loc.Current.Text(Document.Status switch
        {
            RunStatus.Running => TextKey.UiStatusRunning,
            RunStatus.Complete => TextKey.UiStatusComplete,
            RunStatus.NeedsDecision => TextKey.UiStatusNeedsDecision,
            RunStatus.Failed => TextKey.UiStatusFailed,
            _ => TextKey.UiStatusStopped,
        });

    /// <summary>What it produced, or where it got to if it did not finish.</summary>
    public string Summary
    {
        get
        {
            if (Missing)
            {
                return Document.Folder;
            }

            if (Document.LastStage is { } stage && Document.Status is RunStatus.Stopped or RunStatus.Failed)
            {
                return Loc.Current.Format(
                    TextKey.UiStoppedAt, RunStageWords.For(stage.Stage), stage.Completed, stage.Total);
            }

            return Loc.Current.Format(
                TextKey.MsgEntriesSummary,
                Document.EntryCount,
                Document.TotalPieces,
                Document.DistinctPartCount);
        }
    }

    public string When => (Document.FinishedAt ?? Document.StartedAt) is { } at
        ? at.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
        : string.Empty;

    /// <summary>What the run started from, so two runs of one set can be told apart.</summary>
    public string Source => Document.Settings switch
    {
        { Kind: InputKind.SetNumber, SetNumber: { Length: > 0 } set } => set,
        { InputPath: { Length: > 0 } path } => System.IO.Path.GetFileName(path),
        _ => string.Empty,
    };

    /// <summary>Raised when this row should be opened, or dropped from the history.</summary>
    public event EventHandler<RunFolder>? OpenRequested;

    public event EventHandler<RunRowViewModel>? ForgetRequested;

    private bool CanOpen => !Missing;

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private void Open() => OpenRequested?.Invoke(this, _folder);

    [RelayCommand]
    private void Forget() => ForgetRequested?.Invoke(this, this);

}
