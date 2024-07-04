namespace RayTracer.ImageIO.Png;

/// <summary>
/// This enum notes the color types supported by the PNG format.
/// </summary>
public enum PngColorType : byte
{
    Grayscale = 0,
    TrueColor = 2,
    IndexedColor = 3,
    GrayscaleWithAlpha = 4,
    TrueColorWithAlpha = 6
}
