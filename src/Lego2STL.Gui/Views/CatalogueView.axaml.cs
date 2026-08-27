using Avalonia.Controls;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.Gui.Views;

public partial class CatalogueView : UserControl
{
    public CatalogueView()
    {
        InitializeComponent();

        ClearFilter.Click += (_, _) =>
        {
            if (DataContext is RunDocumentViewModel model)
            {
                model.ColourFilter = null;
                model.Search = null;
            }
        };
    }
}
