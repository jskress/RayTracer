namespace RayTracer.Extensions;

/// <summary>
/// This class gives us some extra things on strings.
/// </summary>
public static class StringExtensions
{
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
