using System.CommandLine;
using Lego2STL.Cli.Commands;

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
        var root = new RootCommand(
            "Lego2STL - turn a LEGO parts catalogue into a CSV plus 3D-printable geometry.")
        {
            ExtractCommand.Create(),
            RefreshColorsCommand.Create(),
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
}
