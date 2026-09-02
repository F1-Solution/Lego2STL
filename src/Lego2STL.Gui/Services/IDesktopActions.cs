namespace Lego2STL.Gui.Services;

/// <summary>How this platform hands a file or an address to whoever handles such things.</summary>
public interface IDesktopActions
{
    void Open(string path);

    /// <summary>Shows the file where it lives - or, where there is no such place, shares it.</summary>
    void Reveal(string path);
}
