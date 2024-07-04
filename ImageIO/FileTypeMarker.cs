using System.Globalization;
using System.Text.RegularExpressions;

namespace RayTracer.ImageIO;

/// <summary>
/// This class represents a sequence of bytes, which may be symbolized by regex notations
/// for a single character, that, collectively, represent the starting bytes of a particular
/// image file type.
/// </summary>
internal class FileTypeMarker
{
    internal const string Whitespace = @"\s";

    private static readonly Regex HexPattern = new ("^0x[0-9a-fA-F][0-9a-fA-F]$");

    /// <summary>
    /// This property reports how many bytes this marker contains.
    /// </summary>
    internal int Length => _matchers.Length;

    private readonly Func<byte, bool>[] _matchers;

    internal FileTypeMarker(params string[] matcherSource)
    {
        _matchers = ToFunctions(matcherSource);
    }

    /// <summary>
    /// This is a helper method that will convert an array of matching "instructions" into
    /// an appropriate list of evaluation functions.
    /// </summary>
    /// <param name="matcherSource">The matching instructions to convert.</param>
    /// <returns>The matching instructions converted to appropriate evaluation functions.</returns>
    private static Func<byte, bool>[] ToFunctions(string[] matcherSource)
    {
        List<Func<byte, bool>> matchers = [];

        foreach (string matcher in matcherSource)
        {
            if (HexPattern.IsMatch(matcher))
            {
                byte expected = (byte) int.Parse(matcher[2..], NumberStyles.AllowHexSpecifier);
                
                matchers.Add(data => Matches(data, expected));
            }
            else if (matcher == Whitespace)
                matchers.Add(IsWhitespace);
            else if (matcher.Length == 1 && matcher[0] < 256)
                matchers.Add(data => Matches(data, (byte) matcher[0]));
            else
                throw new ArgumentException($"Invalid matching instruction, '{matcher}', found.", nameof(matcherSource));
        }

        return matchers.ToArray();
    }

    /// <summary>
    /// This method is used to see if the given array of bytes contains this marker.
    /// </summary>
    /// <param name="bytes">The data to match on.</param>
    /// <returns><c>true</c>, if the given bytes match this file type marker, or <c>False</c>,
    /// if not.</returns>
    internal bool Matches(byte[] bytes)
    {
        if (bytes == null || bytes.Length < _matchers.Length)
            return false;

        return !_matchers
            .Where((match, index) => !match(bytes[index]))
            .Any();
    }

    /// <summary>
    /// This is a method used to test whether a character is whitespace or not.
    /// </summary>
    /// <param name="data">The raw data as a byte.</param>
    /// <param name="expected">The byte to compare against.</param>
    /// <returns><c>true</c>, if the data is whitespace, or <c>false</c>, if not.</returns>
    private static bool Matches(byte data, byte expected)
    {
        return expected == data;
    }

    /// <summary>
    /// This is a method used to test whether a character is whitespace or not.
    /// </summary>
    /// <param name="data">The raw data as a byte.</param>
    /// <returns><c>true</c>, if the data is whitespace, or <c>false</c>, if not.</returns>
    private static bool IsWhitespace(byte data)
    {
        return char.IsWhiteSpace((char) data);
    }
}
