using Lego2STL.Core.Extraction;

namespace Lego2STL.Core.Ocr;

/// <summary>How a glyph was classified against the learned templates.</summary>
/// <param name="Character">The best-matching character.</param>
/// <param name="Distance">How far the glyph is from that template. Lower is better.</param>
/// <param name="Margin">
/// How much worse the runner-up is. A large margin means the answer was not close, which is
/// what makes it trustworthy.
/// </param>
public sealed record GlyphMatch(char Character, double Distance, double Margin);

/// <summary>
/// A character recogniser that teaches itself from the document it is reading.
/// </summary>
/// <remarks>
/// <para>
/// This exists to solve a specific, measured problem. The system text recogniser reads a
/// catalogue's part lines perfectly - all 53 of them on the reference document - but
/// silently returns nothing for some of the very short quantity lines, because "2x" is too
/// little to work with. Fighting that with bigger margins or scaling made it worse.
/// </para>
/// <para>
/// The way out is that the two lines are printed in the same font at the same size. So the
/// part lines, which are read reliably, also say what each character shape looks like: pair
/// the recognised text with the character shapes found in that line and every digit becomes
/// a labelled example. Those examples then read the quantity lines.
/// </para>
/// <para>
/// Measured result: quantity lines went from 45 of 53 to 53 of 53, with the runner-up
/// template never closer than about half the typical gap. Nothing is shipped with the tool
/// and nothing is assumed about the font, so a document set in a different typeface teaches
/// the recogniser its own shapes.
/// </para>
/// </remarks>
public sealed class GlyphTemplateSet
{
    private readonly Dictionary<char, float[]> _sums = [];
    private readonly Dictionary<char, int> _counts = [];

    /// <summary>Characters that have at least one example.</summary>
    public IReadOnlyCollection<char> KnownCharacters => _counts.Keys;

    /// <summary>How many examples a character has.</summary>
    public int ExampleCount(char c) => _counts.GetValueOrDefault(c);

    public int TotalExamples => _counts.Values.Sum();

    /// <summary>Adds one labelled example.</summary>
    public void Learn(char character, float[] patch)
    {
        ArgumentNullException.ThrowIfNull(patch);

        if (patch.Length != GlyphPatch.Length)
        {
            throw new ArgumentException(
                $"Expected a {GlyphPatch.Length}-element patch, got {patch.Length}.", nameof(patch));
        }

        if (!_sums.TryGetValue(character, out var sum))
        {
            sum = new float[GlyphPatch.Length];
            _sums[character] = sum;
        }

        for (var i = 0; i < patch.Length; i++)
        {
            sum[i] += patch[i];
        }

        _counts[character] = _counts.GetValueOrDefault(character) + 1;
    }

    /// <summary>
    /// Learns from a line whose text is known, by pairing each character with the
    /// corresponding glyph.
    /// </summary>
    /// <remarks>
    /// Only used when the counts line up exactly. A mismatch means the glyph segmentation
    /// and the recognised text disagree about how many characters there are, and pairing
    /// them anyway would teach the recogniser wrong shapes - far worse than learning nothing
    /// from that line.
    /// </remarks>
    /// <returns>True when the line was used.</returns>
    public bool LearnFromLine(IReadOnlyList<float[]> patches, string text)
    {
        ArgumentNullException.ThrowIfNull(patches);
        ArgumentNullException.ThrowIfNull(text);

        if (patches.Count != text.Length)
        {
            return false;
        }

        for (var i = 0; i < patches.Count; i++)
        {
            Learn(text[i], patches[i]);
        }

        return true;
    }

    /// <summary>The averaged template for a character, or null when it has no examples.</summary>
    public float[]? Template(char character)
    {
        if (!_sums.TryGetValue(character, out var sum))
        {
            return null;
        }

        var count = _counts[character];
        var template = new float[sum.Length];
        for (var i = 0; i < sum.Length; i++)
        {
            template[i] = sum[i] / count;
        }

        return template;
    }

    /// <summary>
    /// Classifies a glyph against the templates, restricted to the characters the caller
    /// will accept.
    /// </summary>
    /// <param name="patch">The glyph, already sampled by <see cref="GlyphPatch.Sample"/>.</param>
    /// <param name="allowed">
    /// Characters that are possible in this position. Restricting the alphabet is most of
    /// what makes this reliable: a quantity line can only hold digits, so no letter can win.
    /// </param>
    public GlyphMatch? Classify(float[] patch, IEnumerable<char> allowed)
    {
        ArgumentNullException.ThrowIfNull(patch);
        ArgumentNullException.ThrowIfNull(allowed);

        var best = double.MaxValue;
        var second = double.MaxValue;
        char? bestChar = null;

        foreach (var candidate in allowed)
        {
            var template = Template(candidate);
            if (template is null)
            {
                continue;
            }

            var distance = GlyphPatch.Distance(patch, template);

            if (distance < best)
            {
                second = best;
                best = distance;
                bestChar = candidate;
            }
            else if (distance < second)
            {
                second = distance;
            }
        }

        return bestChar is null
            ? null
            : new GlyphMatch(bestChar.Value, best, double.IsPositiveInfinity(second) || second == double.MaxValue ? double.MaxValue : second - best);
    }

    /// <summary>
    /// Reads a whole line by classifying each glyph in turn.
    /// </summary>
    /// <returns>The text, or null when any glyph could not be classified.</returns>
    public string? ReadLine(IReadOnlyList<float[]> patches, IEnumerable<char> allowed)
    {
        ArgumentNullException.ThrowIfNull(patches);

        var alphabet = allowed.ToList();
        var chars = new char[patches.Count];

        for (var i = 0; i < patches.Count; i++)
        {
            var match = Classify(patches[i], alphabet);
            if (match is null)
            {
                return null;
            }

            chars[i] = match.Character;
        }

        return new string(chars);
    }
}
