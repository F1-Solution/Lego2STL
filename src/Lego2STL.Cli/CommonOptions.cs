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
        Description =
            "Language for messages, the report and the parts list's column names: " +
            "en or it. Defaults to the machine's own.",
        HelpName = "en|it",
        Recursive = true,
        CustomParser = Parse,
        DefaultValueFactory = _ => DisplayLanguages.FromEnvironment(),
    };

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
