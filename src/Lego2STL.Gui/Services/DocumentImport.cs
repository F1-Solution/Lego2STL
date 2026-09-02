using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Lego2STL.Gui.Services;

/// <summary>
/// Brings a picked document into application storage, where a run can be written beside it.
/// </summary>
/// <remarks>
/// A document picker hands over a stream, not a path, and on Android and iOS the place it
/// came from is not ours to write to. Copying costs a second copy of the input on the device,
/// which is the honest price of a sandbox and is the user's to delete.
/// </remarks>
public static class DocumentImport
{
    public static async Task<string> CopyInAsync(
        Stream source,
        string fileName,
        string root,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var imports = Path.Combine(Path.GetFullPath(root), "imports");
        Directory.CreateDirectory(imports);

        var destination = Path.Combine(imports, Path.GetFileName(fileName));

        await using var file = File.Create(destination);
        await source.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

        return destination;
    }
}
