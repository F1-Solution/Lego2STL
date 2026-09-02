using Avalonia;
using Avalonia.Controls;
using Avalonia.Reactive;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.Gui.Views;

public partial class MainView : UserControl
{
    /// <summary>Below this width the rail cannot sit beside the page, so it folds into a flyout.</summary>
    public const double CompactWidth = 700;

    public MainView()
    {
        InitializeComponent();

        // The view's own width, not the platform: a narrow desktop window folds the same way,
        // which is what lets the behaviour be tested without a device. Wrapped in an
        // AnonymousObserver because IObservable<T>'s own instance Subscribe(IObserver<T>) wins
        // overload resolution over any Subscribe(Action<T>) extension and refuses a plain lambda.
        this.GetObservable(BoundsProperty).Subscribe(new AnonymousObserver<Rect>(bounds =>
        {
            if (DataContext is MainViewModel model)
            {
                model.IsCompact = bounds.Width > 0 && bounds.Width < CompactWidth;
            }
        }));
    }
}
