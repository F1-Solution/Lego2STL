using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lego2STL.Core.Rebrickable;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;
using Lego2STL.Gui.Localization;
using Lego2STL.Gui.Services;

namespace Lego2STL.Gui.ViewModels;

/// <summary>One shop, as the settings let it be edited.</summary>
public sealed partial class ShopRowViewModel : ObservableObject
{
    public ShopRowViewModel(Shop shop, bool isPreferred)
    {
        ArgumentNullException.ThrowIfNull(shop);

        Name = shop.Name;
        Url = shop.Url;
        Search = shop.Search;
        IsPreferred = isPreferred;
    }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Url { get; set; }

    [ObservableProperty]
    public partial string? Search { get; set; }

    /// <summary>Whether this is the shop the catalogue's button opens.</summary>
    [ObservableProperty]
    public partial bool IsPreferred { get; set; }

    public Shop ToShop() => new(Name, Url, Search);
}

/// <summary>
/// What belongs to this machine rather than to this run.
/// </summary>
/// <remarks>
/// Four settings, and each has exactly one home: a key, where the log goes, whether the run
/// says anything on screen, and which language the window is in. Nothing here also appears on
/// the setup screen, which is what removed the second language menu the window used to carry
/// and with it the chance of setting a thing in one place and reading it from another.
/// </remarks>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly UserSettings _saved;
    private readonly RunsViewModel? _runs;

    /// <summary>For the designer and for the view locator, neither of which has a run to hand.</summary>
    public SettingsViewModel()
        : this(new RunOptionsViewModel(), new UserSettings(), null)
    {
    }

    public SettingsViewModel(RunOptionsViewModel options, UserSettings saved, RunsViewModel? runs)
    {
        Options = options;
        _saved = saved;
        _runs = runs;

        options.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RunOptionsViewModel.ApiKey))
            {
                RememberTheKey();
            }
        };

        FillShops();
    }

    public RunOptionsViewModel Options { get; }

    /// <summary>
    /// Keeps the key where the command line looks for it, so it is typed once.
    /// </summary>
    /// <remarks>
    /// The one setting here that is not in the window's own preferences file: the terminal
    /// needs it too, and a key with two homes is a key that is present in one of them and
    /// missing from the other. A file that cannot be written loses a preference, which is not
    /// worth interrupting anyone over.
    /// </remarks>
    private void RememberTheKey()
    {
        try
        {
            RebrickableApiKey.Save(Options.ApiKey);
        }
        catch (InvalidOperationException)
        {
        }
    }

    public static IReadOnlyList<LanguageChoice> Languages => Loc.Choices;

    /// <summary>
    /// The window's language, and its one home.
    /// </summary>
    /// <remarks>
    /// Changing it re-words the window at once rather than at the next start, reaches the
    /// settings a run would be given, and is remembered - all three, because a language setting
    /// that does only one of them is not what anyone means by one.
    /// </remarks>
    public LanguageChoice SelectedLanguage
    {
        get => Loc.Choices.First(choice => choice.Language == Options.Language);
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
        }
    }

    /// <summary>Empties the history. The folders themselves are left exactly where they are.</summary>
    [RelayCommand]
    private void ForgetEveryRun()
    {
        if (_runs is not null)
        {
            _runs.ForgetEverythingCommand.Execute(null);
            return;
        }

        RunIndex.ForgetEverything();
    }

    /// <summary>Where parts can be bought. Starts at the three offered, and can be changed.</summary>
    public ObservableCollection<ShopRowViewModel> ShopRows { get; } = [];

    /// <summary>
    /// Builds the rows, and keeps the file in step with them from then on.
    /// </summary>
    /// <remarks>
    /// Saved as it is edited rather than on leaving the screen, because that is how every other
    /// setting here behaves and a screen with two habits is a screen that loses one of them.
    /// </remarks>
    private void FillShops()
    {
        var shops = _saved.Shops.Count > 0 ? _saved.Shops : Shops.Defaults;

        var preferred = shops.FirstOrDefault(
            s => string.Equals(s.Name, _saved.PreferredShop, StringComparison.Ordinal)) ?? shops[0];

        foreach (var shop in shops)
        {
            Add(new ShopRowViewModel(shop, shop == preferred));
        }

        RememberShops();
    }

    private void Add(ShopRowViewModel row)
    {
        row.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(ShopRowViewModel.IsPreferred) && row.IsPreferred)
            {
                foreach (var other in ShopRows.Where(r => r != row))
                {
                    other.IsPreferred = false;
                }
            }

            RememberShops();
        };

        ShopRows.Add(row);
    }

    private void RememberShops()
    {
        _saved.Shops = [.. ShopRows.Select(r => r.ToShop())];
        _saved.PreferredShop = ShopRows.FirstOrDefault(r => r.IsPreferred)?.Name;
        _saved.Save();
    }

    [RelayCommand]
    private void AddShop() => Add(new ShopRowViewModel(
        new Shop(string.Empty, "https://", null), isPreferred: ShopRows.Count == 0));

    /// <summary>Taking away the preferred shop promotes whichever is left, so one is always chosen.</summary>
    [RelayCommand]
    private void RemoveShop(ShopRowViewModel? row)
    {
        if (row is null || !ShopRows.Remove(row))
        {
            return;
        }

        if (row.IsPreferred && ShopRows.Count > 0)
        {
            ShopRows[0].IsPreferred = true;
        }

        RememberShops();
    }

    /// <summary>The flag, so the same thing can be asked for from a terminal.</summary>
    public static string LanguageFlag => "--lang";
}
