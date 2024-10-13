namespace RayTracer.Fonts;

/// <summary>
/// This record holds a single kerning entry.
/// </summary>
/// <param name="Left">The code point of the left glyph this entry supports.</param>
/// <param name="Right">The code point of the right glyph this entry supports.</param>
/// <param name="Kern">The adjustment amount to apply between the two characters.</param>
public record KerningEntry(int Left, int Right, short Kern);
