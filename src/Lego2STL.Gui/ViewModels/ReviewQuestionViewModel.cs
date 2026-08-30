using System;
using System.IO;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lego2STL.Core.Run;

namespace Lego2STL.Gui.ViewModels;

/// <summary>
/// One question about a region the reader could not make out.
/// </summary>
/// <remarks>
/// <para>
/// Asked afterwards, over a run that has already finished: a run of several hours left going
/// overnight must never stop for a person, so nothing here is reachable from the pipeline.
/// </para>
/// <para>
/// Three buttons and three different meanings. An answer names a part and corrects the list; a
/// skip leaves the question standing; "not a LEGO code" says the region was never a label, which
/// is recorded so that neither this run nor a second one over the same pages asks again.
/// </para>
/// </remarks>
public sealed partial class ReviewQuestionViewModel : ViewModelBase
{
    private readonly ManifestUnread _entry;
    private readonly string _overridesPath;

    public ReviewQuestionViewModel(ManifestUnread entry, string reviewDirectory, string overridesPath)
    {
        ArgumentNullException.ThrowIfNull(entry);

        _entry = entry;
        _overridesPath = overridesPath;

        PartNumber = entry.PartNumber;
        ColorCode = entry.ColorCode;
        Quantity = entry.Quantity;
        Picture = Crop(reviewDirectory, entry.Picture);
    }

    public int Page => _entry.Page;

    public string Bounds => _entry.Bounds;

    /// <summary>What the recogniser did return, which is often half of the answer.</summary>
    public string RawText => _entry.RawText;

    public string Reason => _entry.Reason;

    /// <summary>The piece of page being asked about, when the run kept one.</summary>
    public Bitmap? Picture { get; }

    public bool HasPicture => Picture is not null;

    [ObservableProperty]
    public partial string? PartNumber { get; set; }

    [ObservableProperty]
    public partial int? ColorCode { get; set; }

    [ObservableProperty]
    public partial int? Quantity { get; set; }

    /// <summary>
    /// True once all three have been given.
    /// </summary>
    /// <remarks>
    /// The same rule the correction applies, checked here so the button can go grey rather than
    /// being pressed and doing nothing.
    /// </remarks>
    public bool CanAccept =>
        PartNumber is { Length: > 0 } && ColorCode is not null && Quantity is > 0;

    partial void OnPartNumberChanged(string? value) => OnPropertyChanged(nameof(CanAccept));

    partial void OnColorCodeChanged(int? value) => OnPropertyChanged(nameof(CanAccept));

    partial void OnQuantityChanged(int? value) => OnPropertyChanged(nameof(CanAccept));

    /// <summary>Raised when this question is settled, with the answer that settled it.</summary>
    public event EventHandler<ReviewAnswer>? Answered;

    /// <summary>Raised when the question is left standing and the next one should be offered.</summary>
    public event EventHandler? Skipped;

    [RelayCommand]
    private void Accept()
    {
        if (!CanAccept)
        {
            return;
        }

        Settle(new ReviewAnswer(Page, Bounds, PartNumber, ColorCode, Quantity, NotAnEntry: false));
    }

    [RelayCommand]
    private void Skip() => Skipped?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void NotAnEntry() =>
        Settle(new ReviewAnswer(Page, Bounds, null, null, null, NotAnEntry: true));

    /// <summary>Writes the answer down first, so a window closed next moment loses nothing.</summary>
    private void Settle(ReviewAnswer answer)
    {
        try
        {
            ReviewAnswers.Append(_overridesPath, answer);
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or ArgumentException
                                       or NotSupportedException)
        {
            // A folder that cannot be written to costs the memory of the answer, not the answer:
            // the list is still corrected, and the question is simply asked again next time.
        }

        Answered?.Invoke(this, answer);
    }

    private static Bitmap? Crop(string directory, string? name)
    {
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        try
        {
            var path = Path.Combine(directory, name);

            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }
}
