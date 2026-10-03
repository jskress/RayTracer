using RayTracer.Graphics;

namespace RayTracer.Fonts;

/// <summary>
/// This enumeration defines how text may be aligned along a single line.
/// </summary>
public enum TextAlignment
{
    Left,
    Center,
    Right
}

/// <summary>
/// This enumeration defines how the text will be positioned horizontally, relative to the
/// origin -- or, for text laid along a path, whether it starts at the path's start, is centered on
/// its middle, or ends at its end.
/// </summary>
public enum HorizontalPosition
{
    Left,
    Center,
    Right
}

/// <summary>
/// This enumeration defines how the text will be positioned vertically, relative to the
/// origin -- or, for text laid along a path, where the path runs through it.  <c>Top</c> puts the
/// font's ascender line there and <c>Bottom</c> the last line's descender line, so that every glyph
/// lies wholly to one side; <c>Baseline</c> sets the first line's baseline there, and <c>Center</c>
/// the middle of the block.
/// </summary>
public enum VerticalPosition
{
    Top,
    Baseline,
    Center,
    Bottom
}

/// <summary>
/// This class encapsulates settings to use in laying out text.
/// </summary>
public class TextLayoutSettings
{
    /// <summary>
    /// This property specifies how glyphs in a line are aligned along their line.  It
    /// only has effect when there is more than one line of text.
    /// </summary>
    public TextAlignment TextAlignment { get; set; } = TextAlignment.Left;

    /// <summary>
    /// This property specifies how the overall block of text will be positioned horizontally,
    /// relative to the origin.
    /// </summary>
    public HorizontalPosition HorizontalPosition { get; set; } = HorizontalPosition.Left;

    /// <summary>
    /// This property specifies how the overall block of text will be positioned vertically,
    /// relative to the origin.
    /// </summary>
    public VerticalPosition VerticalPosition { get; set; } = VerticalPosition.Baseline;

    /// <summary>
    /// This property specifies the amount of space, in font "em"s, to put between lines.
    /// </summary>
    public double LineGap { get; set; } = 0.3;

    /// <summary>
    /// This property holds the path the text is laid along, if it is laid along one rather than in
    /// straight lines.  See <see cref="GlyphLayout"/>.
    /// </summary>
    public GeneralPath Guide { get; set; }
}
