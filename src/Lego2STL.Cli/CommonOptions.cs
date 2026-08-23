using System.CommandLine;
using System.CommandLine.Parsing;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli;

/// <summary>
/// Options every command shares.
/// </summary>
/// <remarks>
/// Declared once and marked recursive, so they can be written before or after the command
/// name and mean the same thing either way. Someone who has just typed a long command line
/// should not have to move the cursor to change the language.
/// </remarks>
internal static class CommonOptions
{
    /// <summary>
    /// Which language to speak. Defaults to the machine's own, so an Italian Windows gets
    /// Italian without being asked; naming one explicitly is what makes output reproducible
    /// regardless of the machine, which is why the tests always do.
    /// </summary>
    public static Option<DisplayLanguage> Language { get; } = new("--lang")
    {
        Description = Strings.For(PeekLanguage(Environment.GetCommandLineArgs()))[TextKey.HelpOptLang],
        HelpName = "en|it",
        Recursive = true,
        CustomParser = Parse,
        DefaultValueFactory = _ => DisplayLanguages.FromEnvironment(),
    };

    /// <summary>
    /// The language, read straight out of the arguments before anything is parsed.
    /// </summary>
    /// <remarks>
    /// Help has to be written in a language before it can be shown, and it is built as the
    /// commands are declared, which is before any parsing has happened. So the arguments are
    /// glanced at first for this one option. Anything unrecognised falls back to the machine's
    /// own language, exactly as leaving the option out would.
    /// </remarks>
    public static DisplayLanguage PeekLanguage(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var argument = args[i];

            if (argument.StartsWith("--lang=", StringComparison.Ordinal)
                && DisplayLanguages.TryParse(argument["--lang=".Length..], out var inline))
            {
                return inline;
            }

            if (argument == "--lang"
                && i + 1 < args.Count
                && DisplayLanguages.TryParse(args[i + 1], out var next))
            {
                return next;
            }
        }

        return DisplayLanguages.FromEnvironment();
    }

    /// <summary>The words for whichever language this run settled on.</summary>
    public static Strings Words(ParseResult parseResult) =>
        Strings.For(parseResult.GetValue(Language));

    private static DisplayLanguage Parse(ArgumentResult result)
    {
        if (result.Tokens.Count == 0)
        {
            return DisplayLanguages.FromEnvironment();
        }

        var tag = result.Tokens[0].Value;

        if (DisplayLanguages.TryParse(tag, out var language))
        {
            return language;
        }

        result.AddError(
            $"'{tag}' is not a language this speaks. Available: " +
            string.Join(", ", DisplayLanguages.All.Select(l => $"{l.Tag()} ({l.NativeName()})")) + ".");

        return DisplayLanguages.Fallback;
    }
}
