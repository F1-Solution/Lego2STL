using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;

namespace Lego2STL.Gui.ViewModels;

/// <summary>
/// The runs that have happened.
/// </summary>
/// <remarks>
/// <para>
/// The history is a convenience and the folder is the truth: this reads a list of paths and
/// then asks each folder what it holds, so nothing here can disagree with a run's own record.
/// The folders are read off the interface thread and their rows appear as they arrive, so a
/// long history does not hold the window still.
/// </para>
/// <para>
/// A folder can also be taken in by hand, which is how one copied from another machine, or made
/// before there was a history at all, gets a row. One holding no sign of a run is refused with a
/// reason rather than added as an empty row that would explain nothing.
/// </para>
/// </remarks>
public sealed partial class RunsViewModel : ViewModelBase
{
    public ObservableCollection<RunRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial bool Loading { get; set; }

    public int Count => Rows.Count;

    public bool HasAny => Rows.Count > 0;

    /// <summary>Why the last folder offered was refused, or null.</summary>
    [ObservableProperty]
    public partial string? AdoptProblem { get; set; }

    /// <summary>Raised when a run should be opened. The shell decides what that means.</summary>
    public event EventHandler<RunFolder>? OpenRequested;

    public async Task RefreshAsync()
    {
        Loading = true;
        Rows.Clear();
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasAny));

        try
        {
            foreach (var path in RunIndex.Read())
            {
                var folder = await Task.Run(() => RunFolder.Read(path)).ConfigureAwait(true);
                Add(folder);
            }
        }
        finally
        {
            Loading = false;
        }
    }

    [RelayCommand]
    private void ForgetEverything()
    {
        RunIndex.ForgetEverything();
        Rows.Clear();
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasAny));
    }

    /// <summary>
    /// Takes a folder into the history, if it holds a run at all.
    /// </summary>
    /// <remarks>
    /// A record or a parts list is enough: either is something this window can show. Neither
    /// means the folder is some other folder, and saying so is more use than a row with nothing
    /// in it.
    /// </remarks>
    [RelayCommand]
    private async Task AdoptFolderAsync(string? folder)
    {
        AdoptProblem = null;

        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        var read = await Task.Run(() => RunFolder.Read(folder)).ConfigureAwait(true);

        if (!read.Exists || (read.State == ManifestState.Missing && !read.HasPartsList))
        {
            AdoptProblem = Loc.Current.Format(
                TextKey.ErrNoFileAt, Path.Combine(folder, "run.json"));
            return;
        }

        RunIndex.Record(read.Layout);

        if (Find(read.Layout.Root) is { } already)
        {
            Rows.Move(Rows.IndexOf(already), 0);
        }
        else
        {
            Add(read, atTop: true);
        }

        OpenRequested?.Invoke(this, read);
    }

    private RunRowViewModel? Find(string folder)
    {
        foreach (var row in Rows)
        {
            if (string.Equals(row.Folder, folder, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }

    private void Add(RunFolder folder, bool atTop = false)
    {
        var row = new RunRowViewModel(folder);

        row.OpenRequested += (_, opened) => OpenRequested?.Invoke(this, opened);
        row.ForgetRequested += (_, forgotten) => Forget(forgotten);

        if (atTop)
        {
            Rows.Insert(0, row);
        }
        else
        {
            Rows.Add(row);
        }

        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasAny));
    }

    private void Forget(RunRowViewModel row)
    {
        RunIndex.Forget(row.Folder);
        Rows.Remove(row);
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(HasAny));
    }
}
