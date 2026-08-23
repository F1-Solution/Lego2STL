using System.CommandLine;
using System.Text;
using Lego2STL.Core.Catalogue;
using Lego2STL.Core.Colors;
using Lego2STL.Core.Ocr;
using Lego2STL.Core.Pdf;
using Lego2STL.Core.Run;

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
        CancellationToken cancellationToken)
    {
        if (!input.Exists)
        {
            Console.Error.WriteLine($"Error: no such file: {input.FullName}");
            return Program.ExitFailure;
        }

        var delimiter = ParseDelimiter(delimiterText);

        using var document = PdfPageImageSource.Open(input.FullName);
        Console.WriteLine($"{input.Name}: {document.PageCount} pages.");

        if (listPagesOnly)
        {
            await ListPagesAsync(document, cancellationToken).ConfigureAwait(false);
            return Program.ExitOk;
        }

        if (string.IsNullOrWhiteSpace(pageRange))
        {
            Console.Error.WriteLine(
                "Error: no page range given. Pass one, e.g. \"2-5\", or use --list-pages to see " +
                "what is on each page.");
            return Program.ExitFailure;
        }

        var pages = PageRange.Parse(pageRange, document.PageCount);
        Console.WriteLine($"Reading pages {PageRange.Format(pages)} using {colorScheme} colour numbering.");

        var reader = new CatalogueReader(OcrEngines.Create());
        var read = await reader.ReadAsync(document, pages, cancellationToken).ConfigureAwait(false);

        foreach (var note in read.Notes)
        {
            Console.WriteLine("  " + note);
        }

        var layout = RunLayout.For(input.FullName, outputDirectory?.FullName);
        layout.CreateDirectories();

        var list = PartsListBuilder.Build(read.Entries, ColorReference.Table, colorScheme);
        await PartsListCsv.WriteFileAsync(layout.PartsListPath, list, delimiter, cancellationToken)
            .ConfigureAwait(false);

        await WriteReportAsync(layout, input, pages, colorScheme, read, list, cancellationToken)
            .ConfigureAwait(false);

        Console.WriteLine();
        Console.WriteLine($"{list.Entries.Count} entries, {list.TotalPieces} pieces, " +
                          $"{list.DistinctPartNumbers.Count} distinct parts.");
        Console.WriteLine($"Parts list: {layout.PartsListPath}");
        Console.WriteLine($"Report:     {layout.ReportPath}");

        if (read.Unresolved.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"{read.Unresolved.Count} entr{(read.Unresolved.Count == 1 ? "y" : "ies")} could not be read:");
            foreach (var u in read.Unresolved)
            {
                Console.Error.WriteLine($"  page {u.Page} at {u.Bounds}: {u.Reason} (read as \"{Flatten(u.RawText)}\")");
            }

            Console.Error.WriteLine("The parts list was written without them. Later stages are refused until they are settled.");
            return Program.ExitUnverified;
        }

        return Program.ExitOk;
    }

    /// <summary>
    /// Classifies every page by how many catalogue entries it holds, so the pages worth
    /// reading can be seen at a glance.
    /// </summary>
    private static async Task ListPagesAsync(PdfPageImageSource document, CancellationToken cancellationToken)
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
                Console.WriteLine($"  page {pageNumber,4}  catalogue, {count} entr{(count == 1 ? "y" : "ies")}");
            }

            await Task.Yield();
        }

        Console.WriteLine();
        Console.WriteLine(candidates.Count == 0
            ? "No catalogue pages found."
            : $"Suggested range: {PageRange.Format(candidates)}");
    }

    private static async Task WriteReportAsync(
        RunLayout layout,
        FileInfo input,
        IReadOnlyList<int> pages,
        ColorScheme colorScheme,
        CatalogueReadResult read,
        PartsList list,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Lego2STL run report");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine($"Input          : {input.FullName}");
        sb.AppendLine($"Pages          : {PageRange.Format(pages)}");
        sb.AppendLine($"Colour numbers : {colorScheme}");
        sb.AppendLine();

        sb.AppendLine("Reading");
        foreach (var note in read.Notes)
        {
            sb.AppendLine("  " + note);
        }

        var fromShapes = read.Entries.Count(e => e.QuantitySource == ReadingSource.LearnedShapes);
        sb.AppendLine($"  {read.Entries.Count} entries read; {fromShapes} quantity line(s) came from the learned lettering.");
        sb.AppendLine();

        if (list.Notes.Count > 0)
        {
            sb.AppendLine("Parts list");
            foreach (var note in list.Notes)
            {
                sb.AppendLine("  " + note);
            }

            sb.AppendLine();
        }

        sb.AppendLine("Totals");
        sb.AppendLine($"  {list.Entries.Count} entries");
        sb.AppendLine($"  {list.TotalPieces} pieces");
        sb.AppendLine($"  {list.DistinctPartNumbers.Count} distinct part numbers (one shape each)");
        sb.AppendLine();

        if (read.Unresolved.Count > 0)
        {
            sb.AppendLine("Could not be read");
            foreach (var u in read.Unresolved)
            {
                sb.AppendLine($"  page {u.Page} at {u.Bounds}: {u.Reason}");
                sb.AppendLine($"    recogniser returned: \"{Flatten(u.RawText)}\"");
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
