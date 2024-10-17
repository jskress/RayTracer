using RayTracer.Basics;
using RayTracer.Graphics;
using Typography.OpenFont;

namespace RayTracer.Fonts;

/// <summary>
/// This class wraps a glyph from a true type font.
/// </summary>
public class TtfGlyph
{
    /// <summary>
    /// This property holds an offset that should be added to the glyph when "rendered".
    /// </summary>
    public TwoDPoint Offset { get; set; } = TwoDPoint.Zero;

    /// <summary>
    /// This property notes the advance of this glyph; i.e., the amount to translate
    /// (typically in the X direction) to get the starting point of the next glyph.
    /// </summary>
    public double Advance => _typeface.GetAdvanceWidth(_codePoint) * _emScale;

    private readonly Typeface _typeface;
    private readonly float _emScale;
    private readonly int _codePoint;
    private readonly Glyph _glyph;
    private readonly GlyphPoint[] _points;
 
    public TtfGlyph(Typeface typeface, int codePoint)
    {
        _typeface = typeface;
        _emScale = 1.0f / typeface.UnitsPerEm;
        _codePoint = codePoint;
        _glyph = typeface.Lookup(codePoint);

        if (_glyph.GlyphClass > GlyphClassKind.Base)
            throw new Exception($"Can't handle glyphs of class {_glyph.GlyphClass}.");

        _points = _glyph.GlyphPoints
            .Select(point => new GlyphPoint(point * _emScale))
            .ToArray();
    }

    /// <summary>
    /// This method is used to determine what the kerning adjustment, if any, there should
    /// be between this glyph and the one given.
    /// </summary>
    /// <param name="kerningOverrides">A collection of kerning pairs that may override everything.</param>
    /// <param name="other">The glyph to follow this one.</param>
    /// <returns>The kerning distance between them.</returns>
    public double GetKerningTo(Kerning kerningOverrides, TtfGlyph other)
    {
        return FontManager.Instance.GetKerningFor(
            _typeface, kerningOverrides, _codePoint, other._codePoint) * _emScale;
    }

    /// <summary>
    /// This method is used to create a representative general path from this glyph's
    /// outline.
    /// </summary>
    /// <returns>The general path representation of this glyph.</returns>
    public GeneralPath ToPath()
    {
        GeneralPath path = new GeneralPath();
        GlyphPoint control = null;
        int closeIndex = 0;
        bool needMoveTo = true;

        for (ushort index = 0; index < _points.Length; index++)
        {
            GlyphPoint point = _points[index];

            if (point.IsOnCurve)
            {
                if (control is null)
                {
                    if (needMoveTo)
                    {
                        MoveTo(path, point);
                        needMoveTo = false;
                    }
                    else
                    {
                        LineTo(path, point);
                    }
                }
                else
                {
                    QuadTo(path, control, point);
                    control = null;
                }
            }
            else if (control is not null)
            {
                double x = (point.X + control.X) / 2;
                double y = (point.Y + control.Y) / 2;

                QuadTo(path, control, new TwoDPoint(x, y));

                control = point;
            }
            else
            {
                if (needMoveTo)
                {
                    MoveTo(path, _points[_glyph.EndPoints![closeIndex]]);
                    needMoveTo = false;
                }
                control = point;
            }

            if (index == _glyph.EndPoints![closeIndex])
            {
                path.ClosePath();

                closeIndex++;
                control = null;
                needMoveTo = true;
            }
        }

        return path;
    }

    /// <summary>
    /// This method adds a "move to" command to the given path, taking into account our
    /// offset.
    /// </summary>
    /// <param name="path">The path to update.</param>
    /// <param name="point">The point to move to.</param>
    private void MoveTo(GeneralPath path, TwoDPoint point)
    {
        path.MoveTo(point + Offset);
    }

    /// <summary>
    /// This method adds a "line to" command to the given path, taking into account our
    /// offset.
    /// </summary>
    /// <param name="path">The path to update.</param>
    /// <param name="point">The end point for the line.</param>
    private void LineTo(GeneralPath path, TwoDPoint point)
    {
        path.LineTo(point + Offset);
    }

    /// <summary>
    /// This method adds a "quad to" command to the given path, taking into account our
    /// offset.
    /// </summary>
    /// <param name="path">The path to update.</param>
    /// <param name="control">The control point for the quadratic curve.</param>
    /// <param name="point">The end point for the quadratic curve.</param>
    private void QuadTo(GeneralPath path, TwoDPoint control, TwoDPoint point)
    {
        path.QuadTo(control + Offset, point + Offset);
    }
}
