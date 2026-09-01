using CommunityToolkit.Mvvm.ComponentModel;
using Lego2STL.Core.Run;

namespace Lego2STL.Gui.ViewModels;

/// <summary>One measured clearance, as a row that can be edited.</summary>
public sealed partial class ToleranceRowViewModel : ObservableObject
{
    public ToleranceRowViewModel(TolerancePreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        Name = preset.Name;
        Millimetres = preset.Millimetres;
        IsPreferred = preset.Preferred;
    }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial double Millimetres { get; set; }

    /// <summary>Whether a build with nothing else to go on takes its clearance from this.</summary>
    [ObservableProperty]
    public partial bool IsPreferred { get; set; }

    public TolerancePreset ToPreset() =>
        new(Name.Trim(), Millimetres, IsPreferred, DateTimeOffset.UtcNow);
}
