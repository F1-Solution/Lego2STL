using System.CommandLine;
using Lego2STL.Core.Extraction;
using Lego2STL.Core.Pdf;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli.Commands;

/// <summary>
/// Reads a catalogue out of a document and takes it as far as asked.
/// </summary>
internal static class ExtractCommand
{
    public static Command Create()
    {
        var input = new Argument<FileInfo>("input")
        {
            Description = "The document to read.",
        };

        var pages = new Argument<string?>("pages")
        {
            Description = "Which pages hold the catalogue, e.g. 2-5 or 2-5,8,11-13. Omit to search for them.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var listPages = new Option<bool>("--list-pages")
        {
            Description = "Report what is on each page and stop, without reading anything.",
        };

        var options = new PipelineOptions();

        var command = new Command("extract", "Read a catalogue from a document and take it onward.")
        {
            input,
            pages,
            listPages,
        };

        options.AddTo(command, includeDocumentOptions: true);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetRequiredValue(input);
            var language = parseResult.GetValue(CommonOptions.Language);
            var words = Strings.For(language);

            if (!file.Exists)
            {
                Console.Error.WriteLine(
                    $"{words[TextKey.MsgError]}: {words.Format(TextKey.MsgNoSuchFile, file.FullName)}");
                return Program.ExitFailure;
            }

            if (parseResult.GetValue(listPages))
            {
                return ListPages(file, words, cancellationToken);
            }

            var settings = options.Read(
                parseResult, InputKind.Document, file.FullName, parseResult.GetValue(pages));

            return await ConsoleRun.ExecuteAsync(settings, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }

    /// <summary>
    /// Classifies every page by how many catalogue entries it holds, so the pages worth
    /// reading can be seen at a glance before committing to reading them.
    /// </summary>
    private static int ListPages(FileInfo file, Strings words, CancellationToken cancellationToken)
    {
        using var document = PdfPageImageSource.Open(file.FullName);
        Console.WriteLine(words.Format(TextKey.MsgPagesInDocument, file.Name, document.PageCount));
        Console.WriteLine();

        var locator = new LabelLocator();
        var candidates = new List<int>();

        for (var pageNumber = 1; pageNumber <= document.PageCount; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var page = document.GetPage(pageNumber);
            var count = locator.Locate(page).Count;

            if (count == 0)
            {
                continue;
            }

            candidates.Add(pageNumber);
            Console.WriteLine("  " + words.Format(
                count == 1 ? TextKey.MsgPageIsCatalogueOne : TextKey.MsgPageIsCatalogueMany,
                pageNumber,
                count));
        }

        Console.WriteLine();
        Console.WriteLine(candidates.Count == 0
            ? words[TextKey.MsgNoCataloguePages]
            : words.Format(TextKey.MsgSuggestedRange, PageRange.Format(candidates)));

        return Program.ExitOk;
    }
}
