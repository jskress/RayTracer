using System.Collections;
using RayTracer.Basics;
using RayTracer.Graphics;
using Typography.OpenFont;

namespace RayTracer.Fonts;

/// <summary>
/// This class represents a single line of glyphs.
/// </summary>
public class GlyphLine : IEnumerable<GeneralPath>
{
    /// <summary>
    /// This property holds an offset that should be added to the glyph line when "rendered".
    /// </summary>
    public TwoDPoint Offset { get; set; } = TwoDPoint.Zero;

    /// <summary>
    /// This property notes the total advance of this glyph line.
    /// </summary>
    public double Advance { get; private set; }

    private readonly TtfGlyph[] _glyphs;

    private TwoDPoint _offset = TwoDPoint.Zero;

    public GlyphLine(Typeface typeface, string text)
    {
        _glyphs = text.EnumerateRunes()
            .Select(rune => new TtfGlyph(typeface, rune.Value))
            .ToArray();
    }

    /// <summary>
    /// This method is used to arrange this line's glyphs along a horizontal line,
    /// determining the total advance along the way.  The layout makes use of any applicable
    /// kerning between glyphs.
    /// </summary>
    internal void Layout()
    {
        double advance = 0;
        TtfGlyph previous = null;

        foreach (TtfGlyph glyph in _glyphs)
        {
            if (previous is not null)
                advance += previous.GetKerningTo(glyph);

            glyph.Offset = new TwoDPoint(advance, 0);

            advance += glyph.Advance;
            previous = glyph;
        }

        Advance = advance;
    }

    /// <summary>
    /// This method is used to apply the line's offset once it has been settled.
    /// </summary>
    internal void ApplyOffset()
    {
        foreach (TtfGlyph glyph in _glyphs)
            glyph.Offset += Offset;
    }

    /// <summary>
    /// This method produces an enumerator over our glyphs, transforming them into general
    /// paths.
    /// </summary>
    /// <returns>An enumeration over our glyphs.</returns>
    public IEnumerator<GeneralPath> GetEnumerator()
    {
        return _glyphs
            .Select(glyph => glyph.ToPath())
            .GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
