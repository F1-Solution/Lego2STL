using CommunityToolkit.Mvvm.ComponentModel;

namespace Lego2STL.Gui.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Announces a change from outside the object.
    /// </summary>
    /// <remarks>
    /// Needed when something derived from a shared setting changes without the object itself
    /// changing: switching language alters the wording of a part's warnings, but nothing about
    /// the part has moved, so nothing would otherwise be announced.
    /// </remarks>
    public void OnPropertyChangedPublic(string propertyName) => OnPropertyChanged(propertyName);
}
