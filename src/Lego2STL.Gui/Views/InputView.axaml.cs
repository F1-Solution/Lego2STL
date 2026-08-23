using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.Gui.Views;

public partial class InputView : UserControl
{
    public InputView()
    {
        InitializeComponent();

        PickDocument.Click += async (_, _) => await ChooseAsync(
            "PDF", ["*.pdf"], path => Model!.Options.DocumentPath = path);

        PickPartsList.Click += async (_, _) => await ChooseAsync(
            "CSV", ["*.csv", "*.txt"], path => Model!.Options.PartsListPath = path);
    }

    private MainViewModel? Model => DataContext as MainViewModel;

    private async System.Threading.Tasks.Task ChooseAsync(
        string what, string[] patterns, System.Action<string> apply)
    {
        if (Model is null || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(what) { Patterns = patterns }],
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { Length: > 0 } path)
        {
            apply(path);
        }
    }
}
