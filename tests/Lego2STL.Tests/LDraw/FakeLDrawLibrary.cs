using Lego2STL.Core.LDraw;

namespace Lego2STL.Tests.LDraw;

/// <summary>
/// An in-memory library, so the walk over references can be tested against files small
/// enough to work out the right answer by hand.
/// </summary>
public sealed class FakeLDrawLibrary : ILDrawLibrary
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

    public string Description => "in-memory test library";

    public List<string> Requested { get; } = [];

    public FakeLDrawLibrary Add(string name, string content)
    {
        _files[Normalise(name)] = content;
        return this;
    }

    public Task<string?> TryReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        Requested.Add(reference);
        return Task.FromResult(_files.GetValueOrDefault(Normalise(reference)));
    }

    private static string Normalise(string name) => name.Replace('\\', '/').ToLowerInvariant();
}
