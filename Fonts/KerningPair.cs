using System.Text;

namespace RayTracer.Fonts;

/// <summary>
/// This record holds a single kerning entry.
/// </summary>
/// <param name="Left">The code point of the left glyph this entry supports.</param>
/// <param name="Right">The code point of the right glyph this entry supports.</param>
/// <param name="Kern">The adjustment amount to apply between the two characters.</param>
public record KerningPair(int Left, int Right, short Kern)
{
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
