using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.Gui.Views;

/// <summary>
/// Setting a run up, and the two things the screen cannot do for itself.
/// </summary>
/// <remarks>
/// Choosing a file needs the window it was asked from, which a view model has no business
/// knowing about, so the two pickers live here and set the property they were asked for.
/// </remarks>
public partial class SetupView : UserControl
{
    public SetupView()
    {
        InitializeComponent();

        PickDocument.Click += async (_, _) =>
            await ChooseFileAsync("pdf", path => Model!.Options.DocumentPath = path);

        PickPartsList.Click += async (_, _) =>
            await ChooseFileAsync("csv", path => Model!.Options.PartsListPath = path);
    }

    private SetupViewModel? Model => DataContext as SetupViewModel;

    private async Task ChooseFileAsync(string extension, Action<string> apply)
    {
        if (Model is null || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(extension) { Patterns = ["*." + extension] }],
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { Length: > 0 } path)
        {
            apply(path);
        }
    }
}
