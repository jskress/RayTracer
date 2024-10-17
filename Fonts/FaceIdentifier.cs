namespace RayTracer.Fonts;

public class FaceIdentifier
{
    /// <summary>
    /// The name of the font family that the font face belongs to.
    /// </summary>
    public string FamilyName { get; init; }

    /// <summary>
    /// This property specifies the weight of the font.
    /// </summary>
    public int Weight { get; init; }

    /// <summary>
    /// This property specifies whether the font is italic.
    /// </summary>
    public bool Italic { get; init; }

    /// <summary>
    /// This method returns whether the face this identifier identifies belongs to the
    /// given font family.
    /// </summary>
    /// <param name="fontFamily">The font family to test.</param>
    /// <returns><c>true</c>, if our identified font face belongs to the given font family,
    /// or <c>false</c>, if not.</returns>
    public bool BelongsTo(FontFamily fontFamily)
    {
        return FamilyName == fontFamily.Name;
    }

    /// <summary>
    /// This method returns whether this identifier matches the given font face.
    /// </summary>
    /// <param name="face">The font face to test.</param>
    /// <returns><c>true</c>, if this identifier identifies the given font face, or
    /// <c>false</c>, if not.</returns>
    public bool MatchesFace(FontFace face)
    {
        return Weight == face.Weight && Italic == face.Italic;
    }

    /// <summary>
    /// This method formats this font face identifier as a representative file name.
    /// </summary>
    /// <param name="extension">The file extension to use.</param>
    /// <returns>This ID as a file name.</returns>
    public string AsFileName(string extension)
    {
        string weightText = Enum.GetName(typeof(FontWeight), Weight)!.ToLowerInvariant();
        string name = FamilyName.ToLowerInvariant().Replace(" ", "-");

        if (Italic)
            name += "-italic";
        
        return $"{name}-{weightText}{extension}";
        
    }

    /// <summary>
    /// This method provides a string representation of this font face identifier that is
    /// meant to be read by people.
    /// </summary>
    /// <returns>A string representation of this ID for display.</returns>
    public string ForDisplay()
    {
        return FamilyName + (Italic ? "-Italic" : "") + ", " + Enum.GetName(typeof(FontWeight), Weight);
    }
}
