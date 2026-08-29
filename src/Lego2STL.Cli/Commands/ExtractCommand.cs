using System.CommandLine;
using Lego2STL.Core.Pdf;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli.Commands;

/// <summary>
/// Reads a catalogue out of a document and takes it as far as asked.
/// </summary>
internal static class ExtractCommand
{
    public static Command Create(Strings words)
    {
        var input = new Argument<FileInfo>("input")
        {
            Description = words[TextKey.HelpArgDocument],
        };

        var pages = new Argument<string?>("pages")
        {
            Description = words[TextKey.HelpArgPages],
            Arity = ArgumentArity.ZeroOrOne,
        };

        var listPages = new Option<bool>("--list-pages")
        {
            Description = words[TextKey.HelpOptListPages],
        };

        var options = new PipelineOptions(words);

        var command = new Command("extract", words[TextKey.HelpExtract])
        {
            input,
            pages,
            listPages,
        };

        options.AddTo(command, includeDocumentOptions: true);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var file = parseResult.GetRequiredValue(input);

            // Whatever was actually parsed wins over what help was written in: the two agree
            // unless --lang came after a mistyped value, and then the parsed one is right.
            var spoken = Strings.For(parseResult.GetValue(CommonOptions.Language));

            if (!file.Exists)
            {
                Console.Error.WriteLine(
                    $"{spoken[TextKey.MsgError]}: {spoken.Format(TextKey.MsgNoSuchFile, file.FullName)}");
                return Program.ExitFailure;
            }

            if (parseResult.GetValue(listPages))
            {
                return ListPages(file, spoken, cancellationToken);
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

        // The same search a run makes, so this listing describes what will really happen
        // rather than a second search of its own.
        var search = CataloguePages.Find(document, cancellationToken: cancellationToken);

        foreach (var page in search.Pages)
        {
            Console.WriteLine("  " + words.Format(
                page.EntryCount == 1 ? TextKey.MsgPageIsCatalogueOne : TextKey.MsgPageIsCatalogueMany,
                page.Number,
                page.EntryCount));
        }

        Console.WriteLine();
        Console.WriteLine(search.Pages.Count > 0
            ? words.Format(TextKey.MsgSuggestedRange, PageRange.Format(search.Numbers))
            : words[search.Typeset ? TextKey.MsgNoCatalogueInThisBook : TextKey.MsgNoCataloguePages]);

        return Program.ExitOk;
    }
}
