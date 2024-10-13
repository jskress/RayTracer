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
/// origin.
/// </summary>
public enum HorizontalPosition
{
    Left,
    Center,
    Right
}

/// <summary>
/// This enumeration defines how the text will be positioned vertically, relative to the
/// origin.
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
}
