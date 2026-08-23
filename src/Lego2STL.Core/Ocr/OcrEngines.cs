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
    /// <summary>True when this build can recognise text at all.</summary>
    public static bool IsAvailable =>
#if WINDOWS
        true;
#else
        false;
#endif

    /// <summary>
    /// The platform's recogniser.
    /// </summary>
    /// <param name="languageTag">
    /// A specific recogniser language, or null to let the platform choose. The text being
    /// read is digits and Latin letters, so any Latin-script recogniser will do.
    /// </param>
    /// <exception cref="OcrUnavailableException">When this platform has no recogniser.</exception>
    public static IOcrEngine Create(string? languageTag = null)
    {
#if WINDOWS
        return WindowsOcrEngine.Create(languageTag);
#else
        _ = languageTag;
        throw new OcrUnavailableException(
            "Reading a document needs text recognition, which this build does not have: the " +
            "recogniser it uses is part of Windows. Everything after the parts list works here, " +
            "so run 'extract' on Windows once and bring the parts list over, or start from a set " +
            "number instead.");
#endif
    }
}
