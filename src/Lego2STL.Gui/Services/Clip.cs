using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

namespace Lego2STL.Gui.Services;

/// <summary>
/// Copying to the clipboard, for the buttons that offer it.
/// </summary>
/// <remarks>
/// The clipboard belongs to the window rather than to a run, and the buttons that use it live
/// inside templates where there is no one of them to name. So the handler goes on the control
/// that holds them and what to copy travels on the button's tag - the same arrangement the
/// option list already uses for browsing.
/// </remarks>
public static class Clip
{
    /// <summary>Class a button carries to say that clicking it copies its tag.</summary>
    public const string ButtonClass = "copy";

    /// <summary>Makes every "copy" button beneath this control copy the text it carries.</summary>
    public static void CopiesWhatItsButtonsCarry(this InputElement control)
    {
        ArgumentNullException.ThrowIfNull(control);

        control.AddHandler(Button.ClickEvent, (_, e) =>
        {
            if (e.Source is Button { Tag: string text } button && button.Classes.Contains(ButtonClass))
            {
                _ = CopyAsync(button, text);
            }
        });
    }

    /// <summary>
    /// Puts text on the clipboard, from wherever in the window the asking was done.
    /// </summary>
    /// <remarks>
    /// A clipboard that will not take it is not worth interrupting anyone over: the command is
    /// on screen beside the button, so it can still be selected and copied by hand.
    /// </remarks>
    public static async Task CopyAsync(Visual from, string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || TopLevel.GetTopLevel(from)?.Clipboard is not { } clipboard)
        {
            return;
        }

        try
        {
            await clipboard.SetTextAsync(text).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            // Nothing to be done, and the command is still on screen to be copied by hand.
        }
    }
}
