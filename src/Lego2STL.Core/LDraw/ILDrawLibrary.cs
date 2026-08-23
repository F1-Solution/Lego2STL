namespace Lego2STL.Core.LDraw;

/// <summary>
/// Supplies the text of an LDraw file by name.
/// </summary>
/// <remarks>
/// <para>
/// A part file is never self-sufficient: it refers to sub-parts and primitives that live in
/// other folders, and those refer to more. One panel in the reference set pulls in 37 files.
/// So the converter needs a way to ask for any file by the name written inside another,
/// which is what this is.
/// </para>
/// <para>
/// Names arrive in the form they are written in the files: a bare file name, or one prefixed
/// with a folder using a backslash. Implementations are responsible for the search order and
/// for treating names case-insensitively, because the files are referenced inconsistently.
/// </para>
/// </remarks>
public interface ILDrawLibrary
{
    /// <summary>A short description of where files are coming from, for the report.</summary>
    string Description { get; }

    /// <summary>
    /// Returns the text of a referenced file, or null when this source does not have it.
    /// </summary>
    /// <param name="reference">
    /// The name as written in a file, e.g. "3001.dat", "s\3001s01.dat" or "48\4-4cyli.dat".
    /// </param>
    Task<string?> TryReadAsync(string reference, CancellationToken cancellationToken = default);
}
