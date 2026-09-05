using RayTracer.Basics;
using RayTracer.Fonts;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a text primitive.
/// </summary>
public class TextSolid : Group
{
    /// <summary>
    /// This property holds the text that we are to represent.
    /// </summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public string Text { get; set; }

    /// <summary>
    /// This property holds the name of the font family to use.
    /// </summary>
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
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
    /// This property allows for overriding character kerning for this specific text solid.
    /// </summary>
    public Kerning KerningOverrides { get; set; }

    /// <summary>
    /// This property notes whether the text has surface caps.
    /// </summary>
    public bool Closed { get; set; } = true;

    /// <summary>
    /// This property holds how much each glyph is scaled by at the far face of the text.  It is 1
    /// for ordinary text, whose letters are the same all the way through; less than 1 draws each
    /// letter in as it goes back, which is what makes it read as cut into a surface rather than
    /// stood on one, and 0 brings each to a ridge.
    /// </summary>
    public double Taper { get; set; } = 1;

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        List<GeneralPath> glyphs = TextOutline.Glyphs(
            FontFamilyName, FontWeight, IsItalic, LayoutSettings, KerningOverrides, Text);

        foreach (GeneralPath path in glyphs)
        {
            Extrusion glyph = new Extrusion
            {
                Path = path,
                MinimumY = 0,
                MaximumY = 0.1,
                Closed = Closed
            };

            // A taper draws an outline towards the Y axis, and a line of text is laid out along X,
            // so every letter but the one at the origin sits well off that axis.  Left alone, they
            // would all be drawn towards the same point and the text would come out as a starburst
            // rather than as letters.  Each glyph is therefore brought to the axis, tapered about
            // its own middle, and put back where the layout placed it.
            if (Taper != 1)
            {
                double centerX = (path.MinX + path.MaxX) / 2;
                double centerY = (path.MinY + path.MaxY) / 2;

                // The outline lies in X/Y and is given depth along Y, so its second coordinate is
                // the world's Z -- which is why the way back is not simply the way out reversed.
                path.Transform(Transforms.Translate(-centerX, -centerY, 0));

                glyph.Taper = Taper;
                glyph.Transform = Transforms.Translate(centerX, 0, centerY);
            }

            Add(glyph);
        }

        base.PrepareSurfaceForRendering();
    }
}
