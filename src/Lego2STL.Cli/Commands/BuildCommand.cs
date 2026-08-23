using System.CommandLine;
using Lego2STL.Core.Pipeline;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli.Commands;

/// <summary>
/// Turns a parts list, or a set number, into shape files and coloured plates.
/// </summary>
internal static class BuildCommand
{
    public static Command Create()
    {
        var input = new Argument<FileInfo?>("parts-list")
        {
            Description = "A parts list from an earlier run. Leave it out when using --set.",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var set = new Option<string?>("--set")
        {
            Description = "A set number to look up instead of reading a parts list, e.g. 42100-1.",
        };

        var options = new PipelineOptions();

        var command = new Command("build", "Turn a parts list into shape files and coloured plates.")
        {
            input,
            set,
        };

        options.AddTo(command, includeDocumentOptions: false);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var words = Strings.For(parseResult.GetValue(CommonOptions.Language));

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
                    $"{words[TextKey.MsgError]}: name a parts list, or use --set to look one up.");
                return Program.ExitFailure;
            }

            if (!file.Exists)
            {
                Console.Error.WriteLine(
                    $"{words[TextKey.MsgError]}: {words.Format(TextKey.MsgNoSuchPartsList, file.FullName)}");
                return Program.ExitFailure;
            }

            var settings = options.Read(parseResult, InputKind.PartsList, file.FullName, null);
            return await ConsoleRun.ExecuteAsync(settings, cancellationToken).ConfigureAwait(false);
        });

        return command;
    }
}
