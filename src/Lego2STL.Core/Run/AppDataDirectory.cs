namespace Lego2STL.Core.Run;

/// <summary>
/// Where the few things kept between one use and the next live.
/// </summary>
/// <remarks>
/// The history of runs and the window's preferences are both files in this one folder, so the
/// rule for finding it is written once. Two copies would eventually disagree, and the way that
/// shows up is a copy carried on a stick writing half its state into the account it happened to
/// be run under. The variable is what lets a test, or that copy on a stick, keep its own.
/// </remarks>
public static class AppDataDirectory
{
    public const string Variable = "LEGO2STL_SETTINGS_DIR";

    /// <summary>The folder. Not created: whoever writes into it creates it.</summary>
    public static string Path =>
        Environment.GetEnvironmentVariable(Variable) is { Length: > 0 } chosen
            ? chosen
            : System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Lego2STL");

    public static string File(string name) => System.IO.Path.Combine(Path, name);
}
