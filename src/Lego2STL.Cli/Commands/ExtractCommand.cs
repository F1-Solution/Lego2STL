using System.CommandLine;
using System.Text;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Ocr;
using Lego2STL.Core.Pdf;
using Lego2STL.Core.Run;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli.Commands;

/// <summary>
/// Reads a catalogue out of a document and writes the parts list.
/// </summary>
internal static class ExtractCommand
{
    public static Command Create()
    {
        var input = new Argument<FileInfo>("input")
        {
            Description = "The document to read, or a parts list from an earlier run.",
        };

        var pages = new Argument<string?>("pages")
        {
            Description = "Which pages hold the catalogue, e.g. 2-5 or 2-5,8,11-13. Omit to be shown a guess.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var colorScheme = new Option<ColorScheme>("--color-scheme")
        {
            Description = "Whose colour numbering the document prints.",
            DefaultValueFactory = _ => ColorScheme.BrickLink,
        };

        var outputDirectory = new Option<DirectoryInfo?>("--output-dir")
        {
            Description = "Where to put the run folder. Defaults to beside the input.",
        };

        var delimiter = new Option<string?>("--delimiter")
        {
            Description = "Separator for the parts list. Defaults to a semicolon.",
        };

        var listPages = new Option<bool>("--list-pages")
        {
            Description = "Report what is on each page and stop, without reading anything.",
        };

        var command = new Command("extract", "Read a catalogue from a document and write the parts list.")
        {
            input,
            pages,
            colorScheme,
            outputDirectory,
            delimiter,
            listPages,
        };

        command.SetAction(async (parseResult, cancellationToken) => await RunAsync(
            parseResult.GetRequiredValue(input),
            parseResult.GetValue(pages),
            parseResult.GetValue(colorScheme),
            parseResult.GetValue(outputDirectory),
            parseResult.GetValue(delimiter),
            parseResult.GetValue(listPages),
            parseResult.GetValue(CommonOptions.Language),
            cancellationToken).ConfigureAwait(false));

        return command;
    }

    private static async Task<int> RunAsync(
        FileInfo input,
        string? pageRange,
        ColorScheme colorScheme,
        DirectoryInfo? outputDirectory,
        string? delimiterText,
        bool listPagesOnly,
        DisplayLanguage language,
        CancellationToken cancellationToken)
    {
        var words = Strings.For(language);

        if (!input.Exists)
        {
            Console.Error.WriteLine($"{words[TextKey.MsgError]}: {words.Format(TextKey.MsgNoSuchFile, input.FullName)}");
            return Program.ExitFailure;
        }

        var delimiter = ParseDelimiter(delimiterText);

        using var document = PdfPageImageSource.Open(input.FullName);
        Console.WriteLine(words.Format(TextKey.MsgPagesInDocument, input.Name, document.PageCount));

        if (listPagesOnly)
        {
            await ListPagesAsync(document, words, cancellationToken).ConfigureAwait(false);
            return Program.ExitOk;
        }

        if (string.IsNullOrWhiteSpace(pageRange))
        {
            Console.Error.WriteLine($"{words[TextKey.MsgError]}: {words[TextKey.MsgNoPageRange]}");
            return Program.ExitFailure;
        }

        var pages = PageRange.Parse(pageRange, document.PageCount);
        Console.WriteLine(words.Format(TextKey.MsgReadingPages, PageRange.Format(pages), colorScheme));

        var reader = new CatalogueReader(OcrEngines.Create());
        var read = await reader.ReadAsync(document, pages, cancellationToken).ConfigureAwait(false);

        foreach (var note in read.Notes)
        {
            Console.WriteLine("  " + note);
        }

        var layout = RunLayout.For(input.FullName, outputDirectory?.FullName);
        layout.CreateDirectories();

        var list = PartsListBuilder.Build(read.Entries, ColorReference.Table, colorScheme);
        await PartsListCsv.WriteFileAsync(layout.PartsListPath, list, delimiter, language, cancellationToken)
            .ConfigureAwait(false);

        await WriteReportAsync(layout, input, pages, colorScheme, read, list, words, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine(words.Format(
            TextKey.MsgEntriesSummary,
            list.Entries.Count,
            list.TotalPieces,
            list.DistinctPartNumbers.Count));
        Console.WriteLine(words.Format(TextKey.MsgPartsListWritten, layout.PartsListPath));
        Console.WriteLine(words.Format(TextKey.MsgReportWritten, layout.ReportPath));

        if (read.Unresolved.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine(words.Format(
                read.Unresolved.Count == 1
                    ? TextKey.MsgCouldNotReadEntriesOne
                    : TextKey.MsgCouldNotReadEntriesMany,
                read.Unresolved.Count));
            foreach (var u in read.Unresolved)
            {
                Console.Error.WriteLine($"  page {u.Page} at {u.Bounds}: {u.Reason} (read as \"{Flatten(u.RawText)}\")");
            }

            Console.Error.WriteLine(words[TextKey.MsgWrittenWithoutThem]);
            return Program.ExitUnverified;
        }

        return Program.ExitOk;
    }

    /// <summary>
    /// Classifies every page by how many catalogue entries it holds, so the pages worth
    /// reading can be seen at a glance.
    /// </summary>
    private static async Task ListPagesAsync(
        PdfPageImageSource document,
        Strings words,
        CancellationToken cancellationToken)
    {
        var locator = new Lego2STL.Core.Extraction.LabelLocator();
        var candidates = new List<int>();

        Console.WriteLine();
        for (var pageNumber = 1; pageNumber <= document.PageCount; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var page = document.GetPage(pageNumber);
            var count = locator.Locate(page).Count;

            if (count > 0)
            {
                candidates.Add(pageNumber);
                Console.WriteLine("  " + words.Format(
                    count == 1 ? TextKey.MsgPageIsCatalogueOne : TextKey.MsgPageIsCatalogueMany,
                    pageNumber,
                    count));
            }

            await Task.Yield();
        }

        Console.WriteLine();
        Console.WriteLine(candidates.Count == 0
            ? words[TextKey.MsgNoCataloguePages]
            : words.Format(TextKey.MsgSuggestedRange, PageRange.Format(candidates)));
    }

    private static async Task WriteReportAsync(
        RunLayout layout,
        FileInfo input,
        IReadOnlyList<int> pages,
        ColorScheme colorScheme,
        CatalogueReadResult read,
        PartsList list,
        Strings words,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine(words[TextKey.ReportRunTitle]);
        sb.AppendLine(new string('-', 60));
        sb.AppendLine($"{words[TextKey.ReportInput],-16}: {input.FullName}");
        sb.AppendLine($"{words[TextKey.ReportPages],-16}: {PageRange.Format(pages)}");
        sb.AppendLine($"{words[TextKey.ReportColourNumbering],-16}: {colorScheme}");
        sb.AppendLine();

        sb.AppendLine(words[TextKey.ReportReading]);
        foreach (var note in read.Notes)
        {
            sb.AppendLine("  " + note);
        }

        var fromShapes = read.Entries.Count(e => e.QuantitySource == ReadingSource.LearnedShapes);
        sb.AppendLine("  " + words.Format(TextKey.ReportEntriesRead, read.Entries.Count, fromShapes));
        sb.AppendLine();

        if (list.Notes.Count > 0)
        {
            sb.AppendLine(words[TextKey.ReportPartsList]);
            foreach (var note in list.Notes)
            {
                sb.AppendLine("  " + note);
            }

            sb.AppendLine();
        }

        sb.AppendLine(words[TextKey.ReportTotals]);
        sb.AppendLine("  " + words.Format(TextKey.ReportTotalEntries, list.Entries.Count));
        sb.AppendLine("  " + words.Format(TextKey.ReportTotalPieces, list.TotalPieces));
        sb.AppendLine("  " + words.Format(
            TextKey.ReportTotalDistinctParts, list.DistinctPartNumbers.Count));
        sb.AppendLine();

        if (read.Unresolved.Count > 0)
        {
            sb.AppendLine(words[TextKey.ReportCouldNotBeRead]);
            foreach (var u in read.Unresolved)
            {
                sb.AppendLine($"  {u.Page} @ {u.Bounds}: {u.Reason}");
                sb.AppendLine("    " + words.Format(
                    TextKey.ReportRecogniserReturned, Flatten(u.RawText)));
            }
        }

        await File.WriteAllTextAsync(layout.ReportPath, sb.ToString(), new UTF8Encoding(true), cancellationToken)
            .ConfigureAwait(false);
    }

    private static char ParseDelimiter(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return PartsListCsv.DefaultDelimiter;
        }

        if (text is "\\t" or "tab")
        {
            return '\t';
        }

        return text.Length == 1
            ? text[0]
            : throw new ArgumentException($"--delimiter takes a single character, or \"tab\"; got '{text}'.");
    }

    private static string Flatten(string text) => text.Replace('\n', '/').Replace("\r", "");
}
