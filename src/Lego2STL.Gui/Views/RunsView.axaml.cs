using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.Gui.Views;

/// <summary>
/// The list of runs, and the one thing it cannot do for itself.
/// </summary>
/// <remarks>
/// Taking in a folder means asking for one, which needs the window it was asked from. Which
/// folder was chosen is all the view model wants to know.
/// </remarks>
public partial class RunsView : UserControl
{
    public RunsView()
    {
        InitializeComponent();
        Adopt.Click += async (_, _) => await ChooseAsync();
    }

    private async Task ChooseAsync()
    {
        if (DataContext is not RunsViewModel model || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var folders = await top.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { AllowMultiple = false });

        if (folders.FirstOrDefault()?.TryGetLocalPath() is { Length: > 0 } path)
        {
            await model.AdoptFolderCommand.ExecuteAsync(path);
        }
    }
}
