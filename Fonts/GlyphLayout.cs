using System.Collections;
using RayTracer.Basics;
using RayTracer.Graphics;
using Typography.OpenFont;

namespace RayTracer.Fonts;

/// <summary>
/// This method represents the layout of (possibly) multi-line text and its conversion to
/// general paths.
/// </summary>
public class GlyphLayout : IEnumerable<GeneralPath>
{
    private readonly GlyphLine[] _glyphLines;
    private readonly TextLayoutSettings _settings;

    // How far the font's tallest and deepest glyphs reach from the baseline, in ems; the descender
    // is below the baseline, so it is negative.
    private readonly double _ascender;
    private readonly double _descender;

    // The path the text is laid along, if there is one, and how far along it the text's own zero
    // falls.
    private PathGuide _guide;
    private double _along;

    public GlyphLayout(Typeface typeface, TextLayoutSettings settings, string text)
    {
        _glyphLines = text.Split('\n')
            .Select(line => new GlyphLine(typeface, line))
            .ToArray();
        _settings = settings;
        _ascender = typeface.Ascender / (double) typeface.UnitsPerEm;
        _descender = typeface.Descender / (double) typeface.UnitsPerEm;
    }

    /// <summary>
    /// This method is used to arrange the lines in this layout.
    /// </summary>
    /// <param name="kerningOverrides">A collection of kerning pairs that may override everything.</param>
    public void Arrange(Kerning kerningOverrides)
    {
        double verticalAdvance = 1 + _settings.LineGap;
        double totalHeight = _glyphLines.Length * verticalAdvance - _settings.LineGap;
        double totalWidth = 0;

        // First, Arrange the glyphs in each line.
        foreach (GlyphLine line in _glyphLines)
        {
            line.Layout(kerningOverrides);

            totalWidth = Math.Max(totalWidth, line.Advance);
        }

        // Next, let's figure out where our starting coordinates should be.
        (double left, double y) = GetTopLeft(totalWidth, totalHeight);

        // Finally, place each line.
        foreach (GlyphLine line in _glyphLines)
        {
            double x = left + _settings.TextAlignment switch
            {
                TextAlignment.Left => 0,
                TextAlignment.Center => (totalWidth - line.Advance) / 2,
                TextAlignment.Right => totalWidth - line.Advance,
                _ => throw new NotSupportedException(
                    $"Unknown text alignment setting: {_settings.TextAlignment}")
            };

            line.Offset = new TwoDPoint(x, y);
            line.ApplyOffset();

            y -= verticalAdvance;
        }

        // Laid along a path, the text is first laid out exactly as it would be along a straight line,
        // and the line is then bent to the path a glyph at a time.  Where the horizontal position put
        // the text's zero at the left, middle or right of the line, along a path it falls at the
        // path's start, middle or end.
        if (_settings.Guide is not null)
        {
            _guide = new PathGuide(_settings.Guide);
            _along = _settings.HorizontalPosition switch
            {
                HorizontalPosition.Center => _guide.Length / 2,
                HorizontalPosition.Right => _guide.Length,
                _ => 0
            };
        }
    }

    /// <summary>
    /// This method uses the given extents of a glyph layout and determines where, based
    /// on the given settings, we should start positioning text.
    /// </summary>
    /// <param name="totalWidth">The total width of the layout we are working with.</param>
    /// <param name="totalHeight">The total height of the layout we are working with.</param>
    /// <returns>The position where placement should begin.</returns>
    private TwoDPoint GetTopLeft(double totalWidth, double totalHeight)
    {
        double left = _settings.HorizontalPosition switch
        {
            HorizontalPosition.Left => 0,
            HorizontalPosition.Center => -totalWidth / 2,
            HorizontalPosition.Right => -totalWidth,
            _ => throw new NotSupportedException(
                $"Unknown horizontal position setting: {_settings.HorizontalPosition}")
        };
        // Top and bottom are the font's own: the line its tallest glyphs reach up to, and the line
        // its deepest reach down to, so that every glyph lies to one side of the origin.  That
        // matters most for text laid along a path, where it says which side of the path the text is
        // on -- and a bottom at the baseline, as it once was, left every descender across it.
        double top = _settings.VerticalPosition switch
        {
            VerticalPosition.Top => -_ascender,
            VerticalPosition.Baseline => 0,
            VerticalPosition.Center => (totalHeight - 1) / 2 - 0.5,
            VerticalPosition.Bottom => totalHeight - 1 - _descender,
            _ => throw new NotSupportedException(
                $"Unknown vertical position setting: {_settings.VerticalPosition}")
        };

        return new TwoDPoint(left, top);
    }

    /// <summary>
    /// This method produces an enumerator over all the glyphs in all our lines, transforming
    /// each one into a general path.
    /// </summary>
    /// <returns>An enumeration over all our glyphs.</returns>
    public IEnumerator<GeneralPath> GetEnumerator()
    {
        if (_guide is null)
        {
            return _glyphLines
                .SelectMany(line => line)
                .GetEnumerator();
        }

        // Each glyph is laid on the path whole, by its middle: the point halfway along its advance
        // lands on the path that far along it, and the glyph turns to run with the path there.  It is
        // not bent to follow the path, which would warp its letterforms; on a curve tight enough for
        // that to show, the type would be too big for the curve anyway.
        return _glyphLines
            .SelectMany(line => line.Glyphs)
            .Select(glyph =>
            {
                double middle = glyph.Offset.X + glyph.Advance / 2;

                return glyph.ToPath().Transform(_guide.FrameAt(_along + middle, middle));
            })
            .GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
