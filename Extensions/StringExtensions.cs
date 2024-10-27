using System.Text;

namespace RayTracer.Extensions;

/// <summary>
/// This class gives us some extra things on strings.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// This method is used to return the string as an array of the runes of which it is
    /// made.  If the string is <c>null</c>, then <c>null</c> will be returned.
    /// If the string is empty, an empty array is returned.
    /// </summary>
    /// <param name="text">The text to return the runes for.</param>
    /// <returns>The array of runes that make up the string, or <c>null</c>.</returns>
    public static Rune[] AsRunes(this string text)
    {
        if (text == null)
            return null;

        if (text.Length == 0)
            return [];

        return text.EnumerateRunes()
            .ToArray();
    }

    /// <summary>
    /// This method is used to convert an array of runes into a representative string.
    /// </summary>
    /// <param name="runes">The runes to convert to a string.</param>
    /// <returns>The resulting string.</returns>
    public static string AsString(this Rune[] runes)
    {
        return string.Join("", runes);
    }

    /// <summary>
    /// This method is used to return the string as a list of the Unicode code
    /// points for the characters in that string.  If the string is <c>null</c>,
    /// then <c>null</c> will be returned.  If the string is empty, an empty list
    /// is returned.
    /// </summary>
    /// <param name="text">The text to return the code points for.</param>
    /// <returns>The list of code points that make up the string, or <c>null</c>.</returns>
    public static List<int> AsCodePoints(this string text)
    {
        if (text == null)
            return null;

        if (text.Length == 0)
            return [];

        return text.EnumerateRunes()
            .Select(rune => rune.Value)
            .ToList();
    }

    /// <summary>
    /// This method is used to return the first character (or pair, in the case of unicode
    /// surrogates) as its code point.
    /// </summary>
    /// <param name="text">The text holding the character(s) to get the code point for.</param>
    /// <returns>The code point for the first logical character in the text.</returns>
    public static int AsCodePoint(this string text)
    {
        return text.AsCodePoints().First();
    }
}
