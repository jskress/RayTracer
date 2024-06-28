using System.Linq.Expressions;

namespace RayTracer.ImageIO;

/// <summary>
/// This class represents a sequence of bytes, which may be symbolized by regex notations
/// for a single character, that, collectively, represent the starting bytes of a particular
/// image file type.
/// </summary>
internal class FileTypeMarker
{
    internal const string Whitespace = @"\s";

    /// <summary>
    /// This property reports how many bytes this marker contains.
    /// </summary>
    internal int Length => _characters.Length;

    private readonly string[] _characters;

    internal FileTypeMarker(params string[] characters)
    {
        if (!characters.All(text => text.Length == 1 || text == Whitespace))
            throw new ArgumentException("Invalid matching instruction found.", nameof(characters));

        _characters = characters;
    }

    /// <summary>
    /// This method is used to see if the given array of bytes contains this marker.
    /// </summary>
    /// <param name="bytes">The data to match on.</param>
    /// <returns><c>true</c>, if the given bytes match this file type marker, or <c>False</c>,
    /// if not.</returns>
    internal bool Matches(byte[] bytes)
    {
        if (bytes == null || bytes.Length < _characters.Length)
            return false;

        for (int index = 0; index < _characters.Length; index++)
        {
            char ch = Convert.ToChar(bytes[index]);

            if (_characters[index] == Whitespace && !char.IsWhiteSpace(ch))
                return false;

            if (ch != _characters[index][0])
                return false;
        }

        return true;
    }
}
