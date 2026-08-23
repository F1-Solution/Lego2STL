using Lego2STL.Core.Text;

namespace Lego2STL.Core.Ocr;

/// <summary>
/// Thrown when text recognition is asked for on a platform that has none.
/// </summary>
/// <remarks>
/// A distinct type rather than a plain message, because it is not a failure so much as a
/// boundary: reading a document needs a recogniser, and everything downstream of the parts
/// list does not. Callers catch this to say so, and to point at the route that still works.
/// </remarks>
public sealed class OcrUnavailableException : Exception
{
    public OcrUnavailableException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Supplies the platform's text recogniser, or explains why there is not one.
/// </summary>
/// <remarks>
/// The pipeline asks for <see cref="IOcrEngine"/> and never names an implementation, so this
/// is the single place that knows which platform is running. On Windows it is the built-in
/// engine; elsewhere there is none yet, and asking says so plainly rather than failing with
/// a missing-type error at load time.
/// </remarks>
public static class OcrEngines
{
    /// <summary>
    /// The target framework that carries the recogniser, named for whoever has to go and
    /// build it. Kept in step with the two listed in Directory.Build.props by hand.
    /// </summary>
    public const string WindowsTargetFramework = "net10.0-windows10.0.19041.0";

    /// <summary>True when this build can recognise text at all.</summary>
    public static bool IsAvailable =>
#if WINDOWS
        true;
#else
        false;
#endif

    /// <summary>
    /// Why there is no recogniser, in the reader's own words.
    /// </summary>
    /// <remarks>
    /// Which of the two reasons it is matters, because the remedies are nothing alike. Running
    /// somewhere that has no recogniser at all is a fact about the machine, and the way round
    /// it is another machine. Running the plain build on Windows is a fact about the build, and
    /// the way round it is to run the other one - so saying "run this on Windows" to someone
    /// who is on Windows, as this once did, sends them looking in the wrong place entirely.
    /// </remarks>
    public static string DescribeUnavailable(Strings? words = null)
    {
        var w = words ?? Strings.English;

        return OperatingSystem.IsWindows()
            ? w.Format(TextKey.ErrOcrWrongBuild, WindowsTargetFramework)
            : w[TextKey.ErrOcrUnavailable];
    }

    /// <summary>
    /// The platform's recogniser.
    /// </summary>
    /// <param name="languageTag">
    /// A specific recogniser language, or null to let the platform choose. The text being
    /// read is digits and Latin letters, so any Latin-script recogniser will do.
    /// </param>
    /// <param name="words">The language to explain an absent recogniser in.</param>
    /// <exception cref="OcrUnavailableException">When this build has no recogniser.</exception>
    public static IOcrEngine Create(string? languageTag = null, Strings? words = null)
    {
#if WINDOWS
        _ = words;
        return WindowsOcrEngine.Create(languageTag);
#else
        _ = languageTag;
        throw new OcrUnavailableException(DescribeUnavailable(words));
#endif
    }
}
