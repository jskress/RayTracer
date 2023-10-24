using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Scanners;

namespace RayTracer.Parser;

/// <summary>
/// This class represents the data for rendering that we can parse from a file.
/// </summary>
public class RenderData
{
    /// <summary>
    /// This property holds the desired width of the rendered image.
    /// </summary>
    public int Width { get; set; } = 800;

    /// <summary>
    /// This property holds the desired height of the rendered image.
    /// </summary>
    public int Height { get; set; } = 600;

    /// <summary>
    /// This property holds the fully qualified file to write the target image to.
    /// </summary>
    public ImageFile OutputFile { get; init; }

    /// <summary>
    /// This holds the camera that should be used to render the image.
    /// </summary>
    public Camera Camera { get; init; }

    /// <summary>
    /// This property holds the scene (lights and surfaces) that should be rendered.
    /// </summary>
    public Scene Scene { get; init; }

    /// <summary>
    /// This property holds the scanner that should be used to render the image.
    /// </summary>
    public IScanner Scanner { get; set; } = new PixelParallelScanner();

    /// <summary>
    /// This property produces a new canvas to render on.
    /// </summary>
    public Canvas Canvas => new (Width, Height);
}
