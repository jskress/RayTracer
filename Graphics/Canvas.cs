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
    /// This property reports whether any pixel of this image is less than opaque -- as every pixel of a new
    /// canvas is until it is set.  It looks at every pixel each time it is read.
    /// </summary>
    public bool NeedsAlphaChannel => HasAlphaValues();

    /// <summary>
    /// This property notes the curve this canvas's colors were written with.  A canvas the renderer
    /// draws holds light, and so does one read from a file that holds light; one read from anything
    /// else holds the file's values as they stand, and this says how to turn them back into light.
    /// </summary>
    public TransferFunction Encoding { get; set; } = TransferFunction.Linear;

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
    /// <param name="y">The y coordinate of the pixel to set the color for.</param>
    public void SetColor(Color color, int x, int y)
    {
        _pixels[y][x] = color;
    }

    /// <summary>
    /// This method is used to modify this image in place to a gray scale image.
    /// </summary>
    /// <returns>This image, for fluency.</returns>
    public Canvas ToGrayScale()
    {
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
                _pixels[y][x] = _pixels[y][x].AsGray();
        }

        return this;
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
