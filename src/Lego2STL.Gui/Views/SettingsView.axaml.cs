using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.Gui.Views;

/// <summary>
/// What belongs to this machine, and the one thing the screen cannot do for itself.
/// </summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        PickLog.Click += async (_, _) => await ChooseLogAsync();
    }

    private async Task ChooseLogAsync()
    {
        if (DataContext is not SettingsViewModel model || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = "lego2stl.log",
            DefaultExtension = "log",
        });

        if (file?.TryGetLocalPath() is { Length: > 0 } path)
        {
            model.Options.LogFile = path;
        }
    }
}
