using System.Text;

namespace Lego2STL.Core.Run;

/// <summary>
/// The log a run keeps of what it said.
/// </summary>
/// <remarks>
/// One definition for both front ends and for reading a finished run back, so a log opened by
/// the terminal and a log opened by the window are the same file in the same encoding - which
/// is what lets either of them read the other's.
/// </remarks>
public static class RunLogFile
{
    /// <summary>
    /// Opens a log for writing, or nothing when no log was asked for.
    /// </summary>
    /// <remarks>
    /// With the byte-order mark, so an accented line reads correctly in a text editor that
    /// guesses at encodings. Not appending: a run's log is that run's, and a folder reused by a
    /// second pass should say what the second pass did.
    /// </remarks>
    public static StreamWriter? Open(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        return new StreamWriter(path, append: false, new UTF8Encoding(true));
    }

    /// <summary>
    /// Reads a log back, line by line.
    /// </summary>
    /// <remarks>
    /// Empty when there is nothing to read, including while a run is still writing it: a log is
    /// the least important thing on a run's page, and no reason to refuse to open one.
    /// </remarks>
    public static IReadOnlyList<string> Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            var lines = new List<string>();

            while (reader.ReadLine() is { } line)
            {
                lines.Add(line);
            }

            return lines;
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or NotSupportedException)
        {
            return [];
        }
    }
}
