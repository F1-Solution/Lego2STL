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
    protected OptionRowViewModel(string flag, TextKey help)
    {
        Flag = flag;
        HelpKey = help;
    }

    /// <summary>The flag as the command line spells it, which is also what it is searched by.</summary>
    public string Flag { get; }

    protected TextKey HelpKey { get; }

    public string Help => Loc.Current.Text(HelpKey);

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

    /// <summary>Matches on the flag or on what the option does, so either way of looking works.</summary>
    public bool Matches(string? search) =>
        string.IsNullOrWhiteSpace(search)
        || Flag.Contains(search, StringComparison.OrdinalIgnoreCase)
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
        OnPropertyChanged(nameof(Help));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(IsChanged));
        OnPropertyChanged("Value");
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
    string flag, TextKey help, Func<bool> read, Action<bool> write, bool fresh)
    : OptionRowViewModel(flag, help)
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
    string flag, TextKey help, Func<double> read, Action<double> write, double fresh)
    : OptionRowViewModel(flag, help)
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
    string flag, TextKey help, Func<string?> read, Action<string?> write, string? fresh)
    : OptionRowViewModel(flag, help)
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

    public string? Placeholder { get; init; }

    public override bool IsChanged => !string.Equals(read(), fresh, StringComparison.Ordinal);

    public override void Reset() => write(fresh);
}

/// <summary>An option that names somewhere on the disk, so it can be browsed to.</summary>
public sealed class PathOptionRow(
    string flag, TextKey help, Func<string?> read, Action<string?> write, string? fresh)
    : TextOptionRow(flag, help, read, write, fresh)
{
    public bool WantsFolder { get; init; } = true;
}

/// <summary>An option that takes one of a known few.</summary>
public sealed partial class ChoiceOptionRow(
    string flag,
    TextKey help,
    Func<string?> read,
    Action<string?> write,
    string? fresh,
    IReadOnlyList<string> choices)
    : OptionRowViewModel(flag, help)
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
