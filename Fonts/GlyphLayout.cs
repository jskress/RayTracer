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

    public GlyphLayout(Typeface typeface, TextLayoutSettings settings, string text)
    {
        _glyphLines = text.Split('\n')
            .Select(line => new GlyphLine(typeface, line))
            .ToArray();
        _settings = settings;
    }

    /// <summary>
    /// This method is used to arrange the lines in this layout.
    /// </summary>
    public void Arrange()
    {
        double verticalAdvance = 1 + _settings.LineGap;
        double totalHeight = _glyphLines.Length * verticalAdvance - _settings.LineGap;
        double totalWidth = 0;

        // First, Arrange the glyphs in each line.
        foreach (GlyphLine line in _glyphLines)
        {
            line.Layout();

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
        double top = _settings.VerticalPosition switch
        {
            VerticalPosition.Top => -1,
            VerticalPosition.Baseline => 0,
            VerticalPosition.Center => (totalHeight - 1) / 2 - 0.5,
            VerticalPosition.Bottom => totalHeight - 1,
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
        return _glyphLines
            .SelectMany(line => line)
            .GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
