using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.Gui.Views;

/// <summary>
/// The option list, and the one thing its rows cannot do for themselves.
/// </summary>
/// <remarks>
/// Browsing for a folder needs the window it was asked from, which a row has no business
/// knowing about. The handler is added here for the whole list rather than to each button,
/// because the buttons live inside a template and there is no one of them to name; which row
/// asked travels on the button's tag.
/// </remarks>
public partial class OptionListView : UserControl
{
    public OptionListView()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnClicked, handledEventsToo: false);
    }

    private void OnClicked(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { Tag: PathOptionRow row } button && button.Classes.Contains("pick"))
        {
            _ = BrowseAsync(row);
        }
    }

    private async Task BrowseAsync(PathOptionRow row)
    {
        if (TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var chosen = row.WantsFolder
            ? (await top.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { AllowMultiple = false })).FirstOrDefault()
                as IStorageItem
            : (await top.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions { AllowMultiple = false })).FirstOrDefault();

        if (chosen?.TryGetLocalPath() is { Length: > 0 } path)
        {
            row.Value = path;
        }
    }
}
