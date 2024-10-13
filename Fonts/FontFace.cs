using RayTracer.Extensions;

namespace RayTracer.Fonts;

/// <summary>
/// This class represents the particular face from a font family.
/// </summary>
public class FontFace
{
    /// <summary>
    /// This property specifies the weight of the font.
    /// </summary>
    public int Weight { get; set; }

    /// <summary>
    /// This property specifies whether the font is italic.
    /// </summary>
    public bool Italic { get; set; }

    /// <summary>
    /// This property holds the name of the face's true-type font file in our
    /// font cache directory.
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// This property holds the raw kerning information.  Each entry is expected to be a
    /// list with exactly three strings.  The first two are the code points (as the strings
    /// that represent them) between which the kern value applies.  The third string is the
    /// kern amount, as a string.
    /// </summary>
    public List<List<string>> KerningData { get; set; }

    /// <summary>
    /// This method returns a kerning object made up of the raw kerning data we carry.
    /// </summary>
    /// <returns>Our kerning data in a more useful form.</returns>
    internal Kerning GetKerning()
    {
        Kerning kerning = new ();

        if (KerningData != null)
        {
            foreach (List<string> data in KerningData)
            {
                int left = data[0].AsCodePoint();
                int right = data[1].AsCodePoint();
                short kern = short.Parse(data[2]);

                kerning.AddKerning(left, right, kern);
            }
        }

        return kerning;
    }
}
