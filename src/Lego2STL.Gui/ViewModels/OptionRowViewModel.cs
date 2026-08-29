using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;

namespace Lego2STL.Gui.ViewModels;

/// <summary>
/// One option, as a row that can be found, understood and put back.
/// </summary>
/// <remarks>
/// <para>
/// A row holds no value of its own. It reads and writes the one settings object through a pair
/// of delegates, so there is still exactly one property per option and nothing to keep in step:
/// a row that looked set while the run was given something else is the failure this shape
/// makes impossible rather than merely unlikely.
/// </para>
/// <para>
/// The help text comes through <see cref="Loc"/> for the same reason every label does, so
/// changing the language re-words the option list along with the rest of the window.
/// </para>
/// </remarks>
public abstract partial class OptionRowViewModel : ViewModelBase
{
    protected OptionRowViewModel(string flag, TextKey label, TextKey help)
    {
        Flag = flag;
        LabelKey = label;
        HelpKey = help;
    }

    /// <summary>The flag as the command line spells it, which is also what it is searched by.</summary>
    public string Flag { get; }

    protected TextKey LabelKey { get; }

    protected TextKey HelpKey { get; }

    /// <summary>
    /// What the option is called in the reader's own language.
    /// </summary>
    /// <remarks>
    /// The flag is still shown beside it rather than replaced by it: a window whose options
    /// cannot be turned back into a command is no longer the same tool as the terminal, which
    /// is the one promise this whole screen exists to keep.
    /// </remarks>
    public string Label => Loc.Current.Text(LabelKey);

    /// <summary>
    /// What a help text's placeholders are filled with, for the few that have any.
    /// </summary>
    /// <remarks>
    /// The command line already fills these in when it prints its own help; the window used to
    /// show the phrase raw, so the printer row read "...to lay plates out for: {0}."
    /// </remarks>
    internal object?[] HelpValues { get; init; } = [];

    public string Help => HelpValues.Length == 0
        ? Loc.Current.Text(HelpKey)
        : Loc.Current.Format(HelpKey, HelpValues);

    /// <summary>Whether this row is showing. Filtering hides; it never re-materialises the list.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    /// <summary>Whether the option would do anything, given what else has been asked for.</summary>
    public bool IsEnabled => Enabled();

    /// <summary>Whether this option has been moved off what a fresh run would use.</summary>
    public abstract bool IsChanged { get; }

    public abstract void Reset();

    /// <summary>What decides <see cref="IsEnabled"/>; always able, unless given a reason.</summary>
    internal Func<bool> Enabled { get; init; } = () => true;

    /// <summary>
    /// Matches on the flag, on the name, or on what the option does, so every way of looking
    /// for it works - including the flag, for anyone arriving from the terminal.
    /// </summary>
    public bool Matches(string? search) =>
        string.IsNullOrWhiteSpace(search)
        || Flag.Contains(search, StringComparison.OrdinalIgnoreCase)
        || Label.Contains(search, StringComparison.OrdinalIgnoreCase)
        || Help.Contains(search, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Re-reads everything about this row that something outside it may have changed.
    /// </summary>
    /// <remarks>
    /// "Value" is named as a string because each of the five kinds declares its own, of its own
    /// type; there is no one property here to point at.
    /// </remarks>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Label));
        OnPropertyChanged(nameof(Help));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(IsChanged));
        OnPropertyChanged("Value");

        // Named as a string for the same reason "Value" is: only some of the five kinds have one.
        OnPropertyChanged("Placeholder");
    }

    [RelayCommand]
    private void ResetOne()
    {
        Reset();
        Refresh();
    }
}

/// <summary>An option that is either asked for or not.</summary>
public sealed partial class ToggleOptionRow(
    string flag, TextKey label, TextKey help, Func<bool> read, Action<bool> write, bool fresh)
    : OptionRowViewModel(flag, label, help)
{
    public bool Value
    {
        get => read();
        set
        {
            if (read() == value)
            {
                return;
            }

            write(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsChanged));
        }
    }

    public override bool IsChanged => read() != fresh;

    public override void Reset() => write(fresh);
}

/// <summary>An option that takes a measurement.</summary>
public sealed partial class NumberOptionRow(
    string flag, TextKey label, TextKey help, Func<double> read, Action<double> write, double fresh)
    : OptionRowViewModel(flag, label, help)
{
    public double Value
    {
        get => read();
        set
        {
            if (Math.Abs(read() - value) < double.Epsilon)
            {
                return;
            }

            write(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsChanged));
        }
    }

    public double Minimum { get; init; }

    public double Maximum { get; init; } = double.MaxValue;

    public double Increment { get; init; } = 1;

    public string Format { get; init; } = "0.##";

    public override bool IsChanged => Math.Abs(read() - fresh) > double.Epsilon;

    public override void Reset() => write(fresh);
}

/// <summary>An option that takes something typed.</summary>
public partial class TextOptionRow(
    string flag, TextKey label, TextKey help, Func<string?> read, Action<string?> write, string? fresh)
    : OptionRowViewModel(flag, label, help)
{
    public string? Value
    {
        get => read();
        set
        {
            if (read() == value)
            {
                return;
            }

            write(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsChanged));
        }
    }

    /// <summary>
    /// What an empty box shows in grey.
    /// </summary>
    /// <remarks>
    /// Asked for rather than stored, so a placeholder can follow another setting: the bed size
    /// shows the bed of whichever printer is chosen, which is the size a run really would use.
    /// A fixed "220x220" there read as the default while the default was the A1's 256x256.
    /// </remarks>
    internal Func<string?> WhenEmpty { get; init; } = () => null;

    public string? Placeholder => WhenEmpty();

    public override bool IsChanged => !string.Equals(read(), fresh, StringComparison.Ordinal);

    public override void Reset() => write(fresh);
}

/// <summary>An option that names somewhere on the disk, so it can be browsed to.</summary>
public sealed class PathOptionRow(
    string flag, TextKey label, TextKey help, Func<string?> read, Action<string?> write, string? fresh)
    : TextOptionRow(flag, label, help, read, write, fresh)
{
    public bool WantsFolder { get; init; } = true;
}

/// <summary>An option that takes one of a known few.</summary>
public sealed partial class ChoiceOptionRow(
    string flag,
    TextKey label,
    TextKey help,
    Func<string?> read,
    Action<string?> write,
    string? fresh,
    IReadOnlyList<string> choices)
    : OptionRowViewModel(flag, label, help)
{
    public IReadOnlyList<string> Choices { get; } = choices;

    public string? Value
    {
        get => read();
        set
        {
            if (read() == value)
            {
                return;
            }

            write(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsChanged));
        }
    }

    public override bool IsChanged => !string.Equals(read(), fresh, StringComparison.Ordinal);

    public override void Reset() => write(fresh);
}
