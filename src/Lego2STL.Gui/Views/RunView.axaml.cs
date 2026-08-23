using System.Collections.Specialized;
using Avalonia.Controls;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.Gui.Views;

public partial class RunView : UserControl
{
    public RunView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Follow();
    }

    /// <summary>
    /// Keeps the newest line in view. A log that has to be scrolled by hand to see what is
    /// happening is a log nobody watches.
    /// </summary>
    private void Follow()
    {
        if (DataContext is MainViewModel model)
        {
            model.Log.CollectionChanged += OnLogChanged;
        }
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            LogScroller.ScrollToEnd();
        }
    }
}
