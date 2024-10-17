using System.Reflection;
using Typography.OpenFont;

namespace RayTracer.Extensions;

/// <summary>
/// This class gives us some extra things for our typography library.
/// </summary>
public static class TypographyExtensions
{
    private static readonly PropertyInfo KernProperty = typeof(Typeface)
        .GetProperty("KernTable", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// This is a helper method for telling us whether the given typeface has a kerning
    /// table.
    /// </summary>
    /// <param name="typeface">The typeface to check.</param>
    /// <returns><c>true</c>, if the typeface has a kerning table, or <c>false</c>, if not.</returns>
    public static bool HasKernTable(this Typeface typeface)
    {
        return KernProperty.GetValue(typeface) != null;
    }

    /// <summary>
    /// This method is used to acquire the full list of code points for all the glyphs
    /// provided by the given typeface that can be accessed by a code point.
    /// Each code point maps to its glyph index.
    /// </summary>
    /// <param name="typeface">The typeface to get the code point list from.</param>
    /// <returns>The mapping of code points to glyph indices.</returns>
    public static Dictionary<int, ushort> GetCodePointsMap(this Typeface typeface)
    {
        Dictionary<int, ushort> codePoints = new ();

        // This is brute force, but the Typography library gives us no other choice.
        // It's either this or a ton of icky reflection, and this is only done when
        // performance doesn't really matter.
        for (int codePoint = 0; codePoint <= 1_114_112; codePoint++)
        {
            Glyph glyph = typeface.Lookup(codePoint);

            if (glyph != null && glyph.GlyphIndex != 0)
                codePoints[codePoint] = glyph.GlyphIndex;
        }

        return codePoints;
    }
}
