namespace Lego2STL.Core.Extraction;

/// <summary>
/// The measured constants that decide what counts as a label.
/// </summary>
/// <remarks>
/// Every default here was arrived at by measuring the reference document rather than by
/// taste, and the values that matter sit on a plateau rather than at a knife edge. Sweeping
/// the dilation radii over the four catalogue pages, every combination of
/// <see cref="DilateY"/> in 16-20 and <see cref="DilateX"/> in 16-20 finds exactly the 53
/// labels that are there; at 26 horizontally, neighbouring labels start merging, and at 14
/// or less vertically the two lines of one label start separating.
/// </remarks>
public sealed record LabelLocatorOptions
{
    /// <summary>
    /// Luminance below which a pixel counts as ink. Ink and paper are separated by a wide
    /// margin on these pages: text and renders below 90, paper above 235.
    /// </summary>
    public byte InkThreshold { get; init; } = 128;

    /// <summary>
    /// Glyph height band. Measured: digits are 18 px tall, "1" is 17, "x" is 13 and a comma
    /// is 7. Anything taller is a part render or a heading; anything shorter is a speck.
    /// </summary>
    public int MinGlyphHeight { get; init; } = 6;

    /// <inheritdoc cref="MinGlyphHeight"/>
    public int MaxGlyphHeight { get; init; } = 22;

    /// <summary>Glyph width band. Measured: digits 11 px, "1" 7 px, a comma 3 px.</summary>
    public int MinGlyphWidth { get; init; } = 1;

    /// <inheritdoc cref="MinGlyphWidth"/>
    public int MaxGlyphWidth { get; init; } = 16;

    /// <summary>Ink pixels below which a component is too faint to be a glyph.</summary>
    public int MinGlyphPixels { get; init; } = 6;

    /// <summary>Vertical dilation radius used to join a label's two lines. See the remarks on this type.</summary>
    public int DilateY { get; init; } = 18;

    /// <summary>Horizontal dilation radius used to join the glyphs of a line.</summary>
    public int DilateX { get; init; } = 18;

    /// <summary>
    /// Glyphs a blob must hold before it is worth examining. The smallest real label on the
    /// reference pages has 7; keeping this lower is harmless because the row rules below do
    /// the real filtering, and it leaves room for shorter labels in other documents.
    /// </summary>
    public int MinGlyphsPerBlob { get; init; } = 4;

    /// <summary>A text line must be at least this tall, which excludes stray single rows of pixels.</summary>
    public int MinRowHeight { get; init; } = 6;

    /// <summary>
    /// The largest vertical gap between two lines of the same label. Measured: the gap
    /// between a label's quantity line and its part line is consistently 7 px, while the
    /// gap to unrelated marks above is 19 px or more.
    /// </summary>
    public int MaxLineGap { get; init; } = 12;

    /// <summary>
    /// Mean ink per glyph below which a line is rejected as render detail rather than text.
    /// </summary>
    /// <remarks>
    /// This is what stops small marks inside a part's line art from being read as a label.
    /// Measured on the reference pages: real text lines average 73-96 ink pixels per glyph,
    /// while the speck clusters found inside renders average 13-28.
    /// </remarks>
    public double MinMeanGlyphInk { get; init; } = 40.0;

    /// <summary>
    /// Lines per label. A catalogue entry is a quantity line above a part-and-colour line.
    /// </summary>
    public int RowsPerLabel { get; init; } = 2;
}
