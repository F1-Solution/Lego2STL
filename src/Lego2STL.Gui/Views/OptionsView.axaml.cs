using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.Gui.Views;

public partial class OptionsView : UserControl
{
    public OptionsView()
    {
        InitializeComponent();

        PickOutput.Click += async (_, _) => await ChooseFolderAsync(p => Model!.Options.OutputDirectory = p);
        PickLDraw.Click += async (_, _) => await ChooseFolderAsync(p => Model!.Options.LDrawDirectory = p);
        PickCache.Click += async (_, _) => await ChooseFolderAsync(p => Model!.Options.LDrawCache = p);
        PickLog.Click += async (_, _) => await ChooseLogAsync();
    }

    private MainViewModel? Model => DataContext as MainViewModel;

    private async Task ChooseFolderAsync(Action<string> apply)
    {
        if (Model is null || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var folders = await top.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { AllowMultiple = false });

        if (folders.FirstOrDefault()?.TryGetLocalPath() is { Length: > 0 } path)
        {
            apply(path);
        }
    }

    private async Task ChooseLogAsync()
    {
        if (Model is null || TopLevel.GetTopLevel(this) is not { } top)
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
            Model.Options.LogFile = path;
        }
    }
}
