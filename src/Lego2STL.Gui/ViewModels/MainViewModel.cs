using CommunityToolkit.Mvvm.ComponentModel;

namespace Lego2STL.Gui.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Greeting { get; set; } = "Welcome to Avalonia!";
}
