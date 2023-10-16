
using RayTracer.Core;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents an image, either loaded or rendered.  An image's extent
/// is immutable, though it's pixel buffer is not.
/// </summary>
public class Image
{
    public int Width => _pixels[0].Length;
    public int Height => _pixels.Length;

    private readonly Color[][] _pixels;

    public Image(int width, int height)
    {
        _pixels = new Color[height][];

        for (int line = 0; line < height; line++)
            _pixels[line] = new Color[width];
    }

    /// <summary>
    /// This method returns the color of the specified pixel.
    /// </summary>
    /// <param name="x">The x coordinate of the pixel to get the color for.</param>
    /// <param name="y">The y coordinate of the pixel to get the color for.</param>
    /// <returns>The color of the indicated pixel.</returns>
    public Color GetPixel(int x, int y)
    {
        return _pixels[y][x];
    }

    /// <summary>
    /// This method is used to set the color of a particular pixel.
    /// </summary>
    /// <param name="color">The color to set in the given pixel.</param>
    /// <param name="pixel">The pixel to set the color for.</param>
    public void SetColor(Color color, Pixel pixel)
    {
        _pixels[pixel.Y][pixel.X] = color;
    }
}
