using RayTracer.Fonts;
using RayTracer.Graphics;
using Typography.OpenFont;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a text primitive.
/// </summary>
public class TextSolid : Group
{
    /// <summary>
    /// This property holds the text that we are to represent.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// This property holds the name of the font family to use.
    /// </summary>
    public string FontFamilyName { get; set; }

    /// <summary>
    /// This property holds the font weight to use.
    /// </summary>
    public FontWeight FontWeight { get; set; } = FontWeight.Regular;

    /// <summary>
    /// This property holds whether the font face to use is italic.
    /// </summary>
    public bool IsItalic { get; set; } = false;

    /// <summary>
    /// This property holds the layout settings to apply to the text when converting to
    /// character extrusions.
    /// </summary>
    public TextLayoutSettings LayoutSettings { get; set; } = new ();

    /// <summary>
    /// This property notes whether the text has surface caps.
    /// </summary>
    public bool Closed { get; set; } = true;

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        Typeface typeface = FontManager.Instance
            .GetGoogleFont(FontFamilyName, (int) FontWeight, IsItalic);
        GlyphLayout layout = new GlyphLayout(typeface, LayoutSettings, Text);

        layout.Arrange();

        foreach (GeneralPath path in layout)
        {
            Add(new Extrusion
            {
                Path = path.Reverse(),
                MinimumY = 0,
                MaximumY = 0.1,
                Closed = Closed,
                Material = null // <-- This is important.
            });
        }

        base.PrepareSurfaceForRendering();
    }
}
