using System.Text;

namespace RayTracer.Fonts;

/// <summary>
/// This class holds a single kerning entry.
/// </summary>
public class KerningPair
{
    /// <summary>
    /// This property holds the code point of the left glyph this entry supports.
    /// </summary>
    public int Left { get; set; }

    /// <summary>
    /// This property holds the code point of the right glyph this entry supports.
    /// </summary>
    public int Right { get; set; }

    /// <summary>
    /// This property holds the adjustment amount to apply between the two characters.
    /// </summary>
    public short Kern { get; set; }

    /// <summary>
    /// This method creates the string list to use in storing this kerning pair.
    /// </summary>
    /// <returns>The string list to physically store.</returns>
    public List<string> ForStorage()
    {
        string left = new Rune(Left).ToString();
        string right = new Rune(Right).ToString();

        return [left, right, Kern.ToString()];
    }
}
