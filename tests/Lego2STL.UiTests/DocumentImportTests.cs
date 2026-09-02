using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Lego2STL.Gui.Services;
using Xunit;

namespace Lego2STL.UiTests;

/// <summary>
/// What a picked document becomes before a run can use it.
/// </summary>
public sealed class DocumentImportTests
{
    [Fact]
    public async Task A_picked_document_becomes_a_file_under_the_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-import-" + Path.GetRandomFileName());
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("ID;Codice Lego\n"));

        var path = await DocumentImport.CopyInAsync(source, "6324712.csv", root);

        path.Should().Be(Path.Combine(root, "imports", "6324712.csv"));
        File.ReadAllText(path).Should().StartWith("ID;Codice Lego");

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task A_picker_that_hands_over_a_path_cannot_escape_the_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "lego2stl-import-" + Path.GetRandomFileName());
        using var source = new MemoryStream([1, 2, 3]);

        var path = await DocumentImport.CopyInAsync(source, "../../escape.csv", root);

        path.Should().Be(Path.Combine(root, "imports", "escape.csv"));

        Directory.Delete(root, recursive: true);
    }
}
