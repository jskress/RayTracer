using RayTracer.Graphics;
using Typography.OpenFont;

namespace RayTracer.Fonts;

/// <summary>
/// This class turns a run of text into the outlines of its glyphs -- laid out, arranged and
/// returned as a sequence of general paths, one per glyph.  Both the text surface (which
/// extrudes each glyph) and the text path source (which folds the glyphs into a single path)
/// build on it, so the two lay text out identically.
/// </summary>
public static class TextOutline
{
    /// <summary>
    /// This method lays the given text out in the given font and returns the outline of each
    /// glyph as its own general path, arranged per the layout settings and kerning overrides.
    /// The outlines come at the font's own scale, where a line of text is about one unit tall.
    /// </summary>
    /// <param name="familyName">The font family to set the text in.</param>
    /// <param name="weight">The weight of the font face to use.</param>
    /// <param name="italic">Whether to use the italic face.</param>
    /// <param name="layoutSettings">How the lines are aligned and positioned.</param>
    /// <param name="kerningOverrides">Kerning pairs that override the font's own, or <c>null</c>.</param>
    /// <param name="text">The text to lay out.</param>
    /// <returns>The outline of each laid-out glyph, one general path apiece.</returns>
    public static List<GeneralPath> Glyphs(
        string familyName, FontWeight weight, bool italic,
        TextLayoutSettings layoutSettings, Kerning kerningOverrides, string text)
    {
        FaceIdentifier id = new FaceIdentifier
        {
            FamilyName = familyName,
            Weight = (int) weight,
            Italic = italic
        };
        Typeface typeface = FontManager.Instance.GetTypeFace(id);
        GlyphLayout layout = new GlyphLayout(typeface, layoutSettings, text);

        layout.Arrange(kerningOverrides);

        return layout.ToList();
    }
}
