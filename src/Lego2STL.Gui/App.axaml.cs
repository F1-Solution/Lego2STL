using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Lego2STL.Gui.ViewModels;
using Lego2STL.Gui.Views;

namespace Lego2STL.Gui;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var model = new MainViewModel();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow { DataContext = model };
            desktop.ShutdownRequested += (_, _) => model.Dispose();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            // A phone has no shutdown to hook: the view model lives as long as the process.
            single.MainView = new MainView { DataContext = model };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
