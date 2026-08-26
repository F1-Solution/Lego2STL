using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Lego2STL.Gui.ViewModels;

namespace Lego2STL.Gui;

/// <summary>
/// Given a view model, returns the corresponding view.
/// </summary>
/// <remarks>
/// <para>
/// One view per view-model instance, kept. A screen the rail has moved away from is the same
/// control when it comes back, so its scroll position and its place in the logical tree both
/// survive; building a fresh one each time would lose them.
/// </para>
/// <para>
/// Views live in their own namespace, so the segment is mapped as well as the type name. Only
/// mapping the suffix pointed at a namespace holding no views at all, which is why nothing has
/// ever resolved through here.
/// </para>
/// </remarks>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    // Weak, so a view model the shell has let go does not keep its view - and its subtree - alive.
    private readonly ConditionalWeakTable<object, Control> _built = [];

    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        if (_built.TryGetValue(param, out var existing))
        {
            return existing;
        }

        var control = Create(param);
        _built.Add(param, control);
        return control;
    }

    public bool Match(object? data) => data is ViewModelBase;

    private static Control Create(object param)
    {
        var name = param.GetType().FullName!
            .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal)
            .Replace("ViewModel", "View", StringComparison.Ordinal);

        return Type.GetType(name) is { } type
            ? (Control)Activator.CreateInstance(type)!
            : new TextBlock { Text = "Not Found: " + name };
    }
}
