using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lego2STL.Core.Extraction;
using Lego2STL.Core.Pdf;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;

namespace Lego2STL.Gui.ViewModels;

/// <summary>
/// Where a run is set up, and the only place one can be started.
/// </summary>
/// <remarks>
/// <para>
/// Start living here and nowhere else is what makes starting from the wrong screen impossible
/// rather than merely discouraged: there is no button to press anywhere a run cannot be
/// described. Pressing it raises an event; the shell decides what beginning a run means.
/// </para>
/// <para>
/// The log follows the input into the folder the run will use, so that everything a run
/// produced is in one place, the log survives the window closing unexpectedly, and the command
/// line shown here writes the same file when it is pasted into a terminal.
/// </para>
/// </remarks>
public sealed partial class SetupViewModel : ViewModelBase
{
    /// <summary>The log path last offered, so a path chosen by hand can be told apart from it.</summary>
    private string? _offered;

    public SetupViewModel(RunOptionsViewModel options, bool changedOnly = false)
    {
        Options = options;
        Rows = new OptionRowsViewModel(options, changedOnly);

        options.PropertyChanged += WhateverChanged;
        FollowTheInput();
    }

    public RunOptionsViewModel Options { get; }

    public OptionRowsViewModel Rows { get; }

    /// <summary>
    /// Why this run cannot start, or null when it can.
    /// </summary>
    /// <remarks>
    /// A document that could not be looked through is said here in preference, because it is
    /// the more recent and the more surprising of the two things that can be wrong.
    /// </remarks>
    public string? Problem => _scanProblem ?? Options.Problem;

    private string? _scanProblem;

    public bool CanStart => Options.CanRun;

    public string CommandLine => Options.CommandLine;

    /// <summary>How many options were moved off their defaults, for the rail to show.</summary>
    public int ChangedCount => Rows.Rows.Count(row => row.IsChanged);

    /// <summary>The folder this run will use, worked out before it starts.</summary>
    public string? PlannedFolder => RunLayout.Plan(Options.ToSettings())?.Root;

    [ObservableProperty]
    public partial bool Scanning { get; set; }

    /// <summary>Raised when a run should begin. The shell decides what that means.</summary>
    public event EventHandler? Started;

    /// <summary>
    /// Puts the run's own log into the folder the run will use, while nobody has said otherwise.
    /// </summary>
    /// <remarks>
    /// Only ever replaces the path it last offered, which is the whole rule. Before the first
    /// offer that path is nothing, so an untouched setting is filled in; afterwards, a path
    /// someone typed is theirs and a cleared one stays cleared, because neither is what this
    /// last put there.
    /// </remarks>
    public void FollowTheInput()
    {
        var planned = RunLayout.Plan(Options.ToSettings())?.LogPath;

        if (planned is null || planned == Options.LogFile || Options.LogFile != _offered)
        {
            return;
        }

        Options.LogFile = planned;
        _offered = planned;
    }

    [RelayCommand]
    private void Start()
    {
        if (!CanStart)
        {
            return;
        }

        Started?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Which pages hold a catalogue, so a page range does not have to be found by eye.
    /// </summary>
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
                using var document = PdfPageImageSource.Open(Options.DocumentPath!);
                var locator = new LabelLocator();
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

            Options.Pages = found.Count > 0 ? PageRange.Format(found) : string.Empty;
        }
        catch (Exception ex) when (ex is IOException
                                       or InvalidDataException
                                       or InvalidOperationException)
        {
            _scanProblem = Loc.Current.Text(TextKey.MsgError) + ": " + ex.Message;
            OnPropertyChanged(nameof(Problem));
        }
        finally
        {
            Scanning = false;
        }
    }

    private void WhateverChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RunOptionsViewModel.Kind)
            or nameof(RunOptionsViewModel.DocumentPath)
            or nameof(RunOptionsViewModel.PartsListPath)
            or nameof(RunOptionsViewModel.SetNumber)
            or nameof(RunOptionsViewModel.OutputDirectory))
        {
            _scanProblem = null;
            FollowTheInput();
        }

        OnPropertyChanged(nameof(Problem));
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CommandLine));
        OnPropertyChanged(nameof(ChangedCount));
        OnPropertyChanged(nameof(PlannedFolder));
        StartCommand.NotifyCanExecuteChanged();
    }
}
