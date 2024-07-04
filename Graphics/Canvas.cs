namespace RayTracer.Graphics;

/// <summary>
/// This class represents a canvas upon which rendering may occur.  New canvases are
/// created with transparent pixels.
/// </summary>
public class Canvas
{
    /// <summary>
    /// This property provides the width of the canvas.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// This property provides the height of the canvas.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// This property reports whether this image makes any use of the alpha channel.
    /// </summary>
    public bool NeedsAlphaChannel => HasAlphaValues();

    private readonly Color[][] _pixels;

    public Canvas(int width, int height)
    {
        Width = width;
        Height = height;

        _pixels = new Color[height][];

        for (int y = 0; y < height; y++)
        {
            _pixels[y] = new Color[width];

            Array.Fill(_pixels[y], Colors.Transparent);
        }
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
    /// <param name="x">The x coordinate of the pixel to set the color for.</param>
    /// <param name="y">The x coordinate of the pixel to set the color for.</param>
    public void SetColor(Color color, int x, int y)
    {
        _pixels[y][x] = color;
    }

    /// <summary>
    /// This method scans our pixels to see if there are any that make use of the alpha
    /// channel.
    /// </summary>
    /// <returns><c>true</c>, if the alpha channel is used, or <c>false</c>, if not.</returns>
    private bool HasAlphaValues()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (_pixels[y][x].Alpha < 1.0d)
                    return true;
            }
        }

        return false;
    }
}
