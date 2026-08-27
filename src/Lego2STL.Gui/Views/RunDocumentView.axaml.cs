using System.Collections.Specialized;
using Avalonia.Controls;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.Gui.Views;

/// <summary>
/// A run's page, and the one thing a view model cannot do about a log.
/// </summary>
/// <remarks>
/// Following a running log means scrolling to the end as lines arrive, which is a fact about a
/// scroll viewer rather than about the run. It only follows while the view is already at the
/// end, so scrolling back to read something is not undone by the next line.
/// </remarks>
public partial class RunDocumentView : UserControl
{
    public RunDocumentView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Follow();
    }

    private void Follow()
    {
        if (DataContext is not RunDocumentViewModel model)
        {
            return;
        }

        model.Log.CollectionChanged += (_, e) =>
        {
            if (e.Action != NotifyCollectionChangedAction.Add)
            {
                return;
            }

            var scroll = LogScroll;

            if (scroll.Offset.Y >= scroll.Extent.Height - scroll.Viewport.Height - 1)
            {
                scroll.ScrollToEnd();
            }
        };
    }
}
