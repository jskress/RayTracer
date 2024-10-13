namespace RayTracer.Fonts;

/// <summary>
/// This class represents the kerning information for a particular font face.
/// </summary>
public class Kerning
{
    private readonly List<KerningEntry> _entries = [];

    /// <summary>
    /// This method is used to add a new kerning entry for our font face.
    /// </summary>
    /// <param name="left">The code point of the left glyph the kerning is for.</param>
    /// <param name="right">The code point of the right glyph the kerning is for.</param>
    /// <param name="kern">The adjustment amount to apply between the two characters.</param>
    public void AddKerning(int left, int right, short kern)
    {
        int index = FindEntry(left, right);

        if (index < 0)
            _entries.Add(new KerningEntry(left, right, kern));
        else
            _entries[index] = _entries[index] with { Kern = kern };
    }

    /// <summary>
    /// This method returns the kerning that should be used between the two given code
    /// points.  If we don't recognize the code point pair, <c>0</c> will be returned.
    /// </summary>
    /// <param name="left">The code point of the left glyph the kerning is for.</param>
    /// <param name="right">The code point of the right glyph the kerning is for.</param>
    /// <returns>The amount of kerning to apply between the pair of code points.</returns>
    public short GetKern(int left, int right)
    {
        int index = FindEntry(left, right);
        
        return index < 0 ? (short) 0 : _entries[index].Kern;
    }

    /// <summary>
    /// This method is used to find an entry for the given pair of code points.  If no
    /// such pair exists, then <c>-1</c> is returned.
    /// </summary>
    /// <param name="left">The code point of the left glyph for the kerning to find.</param>
    /// <param name="right">The code point of the right glyph for the kerning to find.</param>
    /// <returns></returns>
    private int FindEntry(int left, int right)
    {
        for (int index = 0; index < _entries.Count; index++)
        {
            if (_entries[index].Left == left && _entries[index].Right == right)
                return index;
        }

        return -1;
    }
}
