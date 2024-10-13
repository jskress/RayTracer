using System.Reflection;
using RayTracer.Basics;
using Typography.OpenFont;

namespace RayTracer.Fonts;

/// <summary>
/// This class represents a glyph point from a true type font.  We have our own
/// version because the Typographic Nuget library does not make the "on curve"
/// property publicly available, but we have to have it.
/// </summary>
public record GlyphPoint : TwoDPoint
{
    private static readonly FieldInfo OnCurveField = typeof(GlyphPointF).GetField(
        "onCurve", BindingFlags.Instance | BindingFlags.NonPublic);
    
    public bool IsOnCurve { get; }

    public GlyphPoint(GlyphPointF point) : base(point.X, point.Y)
    {
        IsOnCurve = (bool) OnCurveField.GetValue(point)!;
    }

    public override string ToString()
    {
        return $"({X}, {Y}), onCurve: {IsOnCurve}";
    }
}
