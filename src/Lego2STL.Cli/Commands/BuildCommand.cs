using System.CommandLine;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli.Commands;

/// <summary>
/// Turns a parts list, or a set number, into shape files and coloured plates.
/// </summary>
internal static class BuildCommand
{
    public static Command Create(Strings words)
    {
        var input = new Argument<FileInfo?>("parts-list")
        {
            Description = words[TextKey.HelpArgPartsList],
            Arity = ArgumentArity.ZeroOrOne,
        };

        var set = new Option<string?>("--set")
        {
            Description = words[TextKey.HelpOptSet],
        };

        var options = new PipelineOptions(words);

        var command = new Command("build", words[TextKey.HelpBuild])
        {
            input,
            set,
        };

        options.AddTo(command, includeDocumentOptions: false);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var spoken = Strings.For(parseResult.GetValue(CommonOptions.Language));

            var file = parseResult.GetValue(input);
            var setNumber = parseResult.GetValue(set);

            if (!string.IsNullOrWhiteSpace(setNumber))
            {
                var fromSet = options.Read(parseResult, InputKind.SetNumber, setNumber, null);
                return await ConsoleRun.ExecuteAsync(fromSet, cancellationToken).ConfigureAwait(false);
            }

            if (file is null)
            {
                Console.Error.WriteLine(
                    $"{spoken[TextKey.MsgError]}: name a parts list, or use --set to look one up.");
                return Program.ExitFailure;
            }

            if (!file.Exists)
            {
                Console.Error.WriteLine(
                    $"{spoken[TextKey.MsgError]}: {spoken.Format(TextKey.MsgNoSuchPartsList, file.FullName)}");
                return Program.ExitFailure;
            }

            var settings = options.Read(parseResult, InputKind.PartsList, file.FullName, null);
            return await ConsoleRun.ExecuteAsync(settings, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }
}
