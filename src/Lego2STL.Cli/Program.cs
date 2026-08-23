using System.CommandLine;
using Lego2STL.Cli.Commands;
using Lego2STL.Core.Text;

namespace Lego2STL.Cli;

internal static class Program
{
    /// <summary>
    /// Exit codes. 2 is distinct from 1 on purpose: it means the run finished and produced
    /// output, but some rows could not be verified, so downstream stages were refused.
    /// </summary>
    internal const int ExitOk = 0;
    internal const int ExitFailure = 1;
    internal const int ExitUnverified = 2;

    private static async Task<int> Main(string[] args)
    {
        SpeakUtf8();

        // The language is read off the arguments before the commands are declared, because
        // help is written as they are declared and has to be written in some language.
        var words = Strings.For(CommonOptions.PeekLanguage(args));

        var root = new RootCommand(words[TextKey.HelpRoot])
        {
            CommonOptions.Language,
            ExtractCommand.Create(words),
            BuildCommand.Create(words),
            CalibrationCommand.Create(words),
            BricksCommand.Create(words),
            RefreshColorsCommand.Create(words),
        };

        try
        {
            return await root.Parse(args).InvokeAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return ExitFailure;
        }
        catch (Exception ex)
        {
            // Everything the tool throws deliberately carries a message meant for the
            // user, so show that rather than a stack trace.
            Console.Error.WriteLine("Error: " + ex.Message);
            if (Environment.GetEnvironmentVariable("LEGO2STL_DEBUG") == "1")
            {
                Console.Error.WriteLine(ex);
            }

            return ExitFailure;
        }
    }

    /// <summary>
    /// Tells the console the output is UTF-8.
    /// </summary>
    /// <remarks>
    /// A Windows console starts on a code page that has no idea what to do with an accented
    /// letter, so without this every "è" and "à" in the Italian wording arrives as a question
    /// mark. Setting it can fail when output has been redirected somewhere that will not take
    /// it, and that is not worth stopping for: the words are still right, and whatever is
    /// reading them will make of them what it can.
    /// </remarks>
    private static void SpeakUtf8()
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException)
        {
        }
    }
}
