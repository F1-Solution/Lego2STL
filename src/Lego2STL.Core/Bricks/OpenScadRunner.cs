using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Lego2STL.Core.Bricks;

/// <summary>Thrown when OpenSCAD, or the brick library, is not where it needs to be.</summary>
public sealed class OpenScadUnavailableException : Exception
{
    public OpenScadUnavailableException(string message)
        : base(message)
    {
    }
}

/// <summary>What a generation produced.</summary>
/// <param name="Spec">The piece asked for.</param>
/// <param name="ScadPath">The description that was written, kept so it can be edited by hand.</param>
/// <param name="ShapePath">The shape, when OpenSCAD produced one.</param>
/// <param name="Output">Anything OpenSCAD said.</param>
public sealed record BrickResult(BrickSpec Spec, string ScadPath, string? ShapePath, string Output);

/// <summary>
/// Generates parametric pieces by writing a description and handing it to OpenSCAD.
/// </summary>
/// <remarks>
/// <para>
/// A separate command, and deliberately so. This is the one part of the tool that produces a
/// piece that was never in a catalogue: a size is asked for and a shape is generated to fit
/// it, rather than a real part being looked up. That is a different thing from everything else
/// here and is kept apart from it.
/// </para>
/// <para>
/// The brick library it drives is MachineBlocks, which is published under a licence that is
/// non-commercial and share-alike. It is therefore neither included nor downloaded: it has to
/// be your own copy, in a place you name, driven by your own OpenSCAD. All this writes is a
/// few lines saying which piece is wanted.
/// </para>
/// </remarks>
public sealed class OpenScadRunner
{
    /// <summary>Generating one small piece takes seconds; far past that means something is wrong.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private readonly string _openScad;
    private readonly string _library;

    private OpenScadRunner(string openScad, string library)
    {
        _openScad = openScad;
        _library = library;
    }

    public string OpenScadPath => _openScad;

    public string LibraryPath => _library;

    /// <summary>
    /// Finds OpenSCAD and the brick library, or explains what is missing and where to get it.
    /// </summary>
    public static OpenScadRunner Create(string? openScadPath = null, string? libraryPath = null)
    {
        var openScad = FindOpenScad(openScadPath)
            ?? throw new OpenScadUnavailableException(
                "OpenSCAD was not found. Install it from openscad.org, then either put it on " +
                "the path or name it with --openscad.");

        var library = FindLibrary(libraryPath)
            ?? throw new OpenScadUnavailableException(
                "The MachineBlocks library was not found. It is not included here, because it " +
                "is published under a non-commercial share-alike licence, so it has to be your " +
                "own copy. Clone it from github.com/pks5/machineblocks and name the " +
                "folder with --machineblocks.");

        return new OpenScadRunner(openScad, library);
    }

    /// <summary>Where OpenSCAD usually is, when it is not on the path.</summary>
    private static IEnumerable<string> LikelyPlaces()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            yield return @"C:\Program Files\OpenSCAD\openscad.exe";
            yield return @"C:\Program Files (x86)\OpenSCAD\openscad.exe";

            // The nightly installs beside the stable one rather than over it, and is what the
            // brick library is developed against, so a machine with only that still works.
            yield return @"C:\Program Files\OpenSCAD (Nightly)\openscad.exe";
            yield return @"C:\Program Files (x86)\OpenSCAD (Nightly)\openscad.exe";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            yield return "/Applications/OpenSCAD.app/Contents/MacOS/OpenSCAD";
            yield return "/opt/homebrew/bin/openscad";
            yield return "/usr/local/bin/openscad";
        }
        else
        {
            yield return "/usr/bin/openscad";
            yield return "/usr/local/bin/openscad";
            yield return "/snap/bin/openscad";
        }
    }

    public static string? FindOpenScad(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return File.Exists(explicitPath) ? explicitPath : null;
        }

        foreach (var candidate in LikelyPlaces())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return OnThePath(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "openscad.exe"
            : "openscad");
    }

    private static string? OnThePath(string program)
    {
        var path = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), program);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // A malformed entry in the path is not worth stopping for.
            }
        }

        return null;
    }

    /// <summary>
    /// The library is recognised by the one file that matters: the block description this
    /// drives. A folder without it is not a copy of the library, whatever it is called.
    /// </summary>
    public static string? FindLibrary(string? explicitPath = null)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            candidates.Add(explicitPath);
        }
        else
        {
            var variable = Environment.GetEnvironmentVariable("MACHINEBLOCKS_DIR");
            if (!string.IsNullOrWhiteSpace(variable))
            {
                candidates.Add(variable);
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidates.Add(Path.Combine(home, "machineblocks"));
            candidates.Add(Path.Combine(home, "src", "machineblocks"));
        }

        foreach (var candidate in candidates)
        {
            if (BlockFileIn(candidate) is not null)
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    /// <summary>The library's block description, wherever in the folder it sits.</summary>
    public static string? BlockFileIn(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        foreach (var relative in new[] { Path.Combine("lib", "block.scad"), "block.scad" })
        {
            var candidate = Path.Combine(directory, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// The few lines that describe one piece.
    /// </summary>
    /// <remarks>
    /// Written to a file rather than passed on the command line, and kept afterwards, because
    /// it is the thing worth having: anyone who wants a piece this command cannot describe can
    /// open it, change a number, and run OpenSCAD themselves.
    /// </remarks>
    public string Describe(BrickSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var block = BlockFileIn(_library)
            ?? throw new OpenScadUnavailableException($"No block.scad under {_library}.");

        var sb = new StringBuilder();

        sb.AppendLine("// Written by lego2stl bricks. Edit freely: this is the input, not the output.");
        sb.AppendLine(CultureInfo.InvariantCulture, $"// {spec}");
        sb.AppendLine("//");
        sb.AppendLine("// The shape comes from MachineBlocks, which is published under");
        sb.AppendLine("// CC BY-NC-SA. That licence covers whatever this produces: it is for");
        sb.AppendLine("// your own use, not for selling, and anything shared has to be shared");
        sb.AppendLine("// on the same terms.");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"use <{Escape(block)}>");
        sb.AppendLine();
        sb.AppendLine("machineblock(");

        // Width, depth and height in one vector, height counted in plates. Everything else the
        // library needs has a default, so nothing here has to repeat its configuration file.
        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"    size = [{spec.Columns}, {spec.Rows}, {spec.PlateCount}],");

        sb.AppendLine(CultureInfo.InvariantCulture, $"    studs = {Boolean(spec.Knobs)},");
        sb.AppendLine(CultureInfo.InvariantCulture, $"    pillars = {Boolean(spec.StudHoles)},");

        // Named rather than left to the default, because the default is a file path relative
        // to the library's own examples folder and these descriptions are not written there.
        sb.AppendLine("    studIcon = \"none\",");
        sb.AppendLine("    surfacePattern = \"none\"");
        sb.AppendLine(");");

        return sb.ToString();
    }

    /// <summary>Writes the description and, if OpenSCAD will, turns it into a shape.</summary>
    public async Task<BrickResult> GenerateAsync(
        BrickSpec spec,
        string directory,
        bool describeOnly = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        Directory.CreateDirectory(directory);

        var scadPath = Path.Combine(directory, spec.FileName + ".scad");
        await File.WriteAllTextAsync(scadPath, Describe(spec), cancellationToken).ConfigureAwait(false);

        if (describeOnly)
        {
            return new BrickResult(spec, scadPath, null, string.Empty);
        }

        var shapePath = Path.Combine(directory, spec.FileName + ".stl");
        var output = await RunAsync(scadPath, shapePath, cancellationToken).ConfigureAwait(false);

        return new BrickResult(spec, scadPath, File.Exists(shapePath) ? shapePath : null, output);
    }

    private async Task<string> RunAsync(string scadPath, string shapePath, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(_openScad)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        start.ArgumentList.Add("-o");
        start.ArgumentList.Add(shapePath);
        start.ArgumentList.Add(scadPath);

        using var process = Process.Start(start)
            ?? throw new OpenScadUnavailableException($"Could not start {_openScad}.");

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(Timeout);

        try
        {
            await process.WaitForExitAsync(limit.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new OpenScadUnavailableException(
                $"OpenSCAD was still going after {Timeout.TotalMinutes:0} minutes and was stopped. " +
                "A piece this size should take seconds; check the description it was given.");
        }

        // OpenSCAD says everything of interest on the error stream, including its progress,
        // so both are kept and shown together.
        var said = string.Join(
            Environment.NewLine,
            new[] { await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false) }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        if (process.ExitCode != 0)
        {
            throw new OpenScadUnavailableException(
                $"OpenSCAD stopped with code {process.ExitCode}." +
                (said.Length > 0 ? Environment.NewLine + said : string.Empty));
        }

        return said;
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            // Already gone.
        }
    }

    private static string Boolean(bool value) => value ? "true" : "false";

    /// <summary>A path OpenSCAD will read: forward slashes, which it accepts everywhere.</summary>
    private static string Escape(string path) => path.Replace('\\', '/');
}
