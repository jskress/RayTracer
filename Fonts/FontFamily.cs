namespace RayTracer.Fonts;

/// <summary>
/// This class represents a font family.
/// </summary>
public class FontFamily
{
    /// <summary>
    /// This property holds the name of the font family; spaces are allowed.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// This property holds the current set of known faces in the family.
    /// </summary>
    public List<FontFace> Faces { get; set; }
}
