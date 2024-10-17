namespace RayTracer.Fonts;

/// <summary>
/// This class represents the kerning information for a particular font face.
/// </summary>
public class Kerning
{
    /// <summary>
    /// This property holds the font face from which this kerning object came.
    /// </summary>
    public FontFace FontFace { get; internal init; }

    /// <summary>
    /// This property exposes the list of kerning pairs we carry.
    /// </summary>
    public List<KerningPair> KerningPairs { get; } = [];

    /// <summary>
    /// This property notes whether any kerning pairs exist.
    /// </summary>
    public bool IsEmpty => KerningPairs.Count == 0;

    /// <summary>
    /// This method is used to add a new kerning entry for our font face.
    /// </summary>
    /// <param name="left">The code point of the left glyph the kerning is for.</param>
    /// <param name="right">The code point of the right glyph the kerning is for.</param>
    /// <param name="kern">The adjustment amount to apply between the two characters.</param>
    public void AddKerning(int left, int right, short kern)
    {
        KerningPair pair = new KerningPair
        {
            Left = left,
            Right = right,
            Kern = kern
        };
        int index = FindEntry(left, right);

        if (index < 0)
            KerningPairs.Add(pair);
        else
            KerningPairs[index] = pair;
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
        
        return index < 0 ? (short) 0 : KerningPairs[index].Kern;
    }

    /// <summary>
    /// This method removes a kerning pair from the kerning.
    /// </summary>
    /// <param name="left">The code point of the left glyph the kerning is for.</param>
    /// <param name="right">The code point of the right glyph the kerning is for.</param>
    /// <returns><c>true</c>, if the pair was found and removed, or <c>false</c>, if not.</returns>
    public bool RemoveKern(int left, int right)
    {
        int index = FindEntry(left, right);

        if (index >= 0)
            KerningPairs.RemoveAt(index);

        return index >= 0;
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
        for (int index = 0; index < KerningPairs.Count; index++)
        {
            if (KerningPairs[index].Left == left && KerningPairs[index].Right == right)
                return index;
        }

        return -1;
    }

    /// <summary>
    /// This method is used to update the owning font face for storage.
    /// </summary>
    internal void UpdateFontFace()
    {
        FontFace.KerningData = KerningPairs
            .Select(pair => pair.ForStorage())
            .ToList();
    }
}
