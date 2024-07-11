using RayTracer.Core;
using RayTracer.Graphics;
using RayTracer.Scanners;

namespace RayTracer.General;

/// <summary>
/// This class represents the current rendering context.
/// </summary>
public class RenderContext
{
    /// <summary>
    /// This property reports the width of the target image.
    /// </summary>
    public int Width { get; set; } = 800;

    /// <summary>
    /// This property reports the height of the target image.
    /// </summary>
    public int Height { get; set; } = 600;

    /// <summary>
    /// This property produces a new canvas to render on.
    /// </summary>
    public Canvas NewCanvas => new (Width, Height);

    /// <summary>
    /// This holds any information that should be stored with images we generate.
    /// </summary>
    public ImageInformation ImageInformation { get; set; }

    /// <summary>
    /// This property holds the scanner that should be used to render the image.
    /// </summary>
    public IScanner Scanner { get; set; } = new PixelParallelScanner();

    /// <summary>
    /// This property holds the gamma correction value to use when generating image files.
    /// </summary>
    public double Gamma { get; set; } = 2.2;

    /// <summary>
    /// This property holds the bits per color channel to use when writing image files.
    /// </summary>
    public int BitsPerChannel { get; set; } = 8;

    /// <summary>
    /// This property provides the largest value a color channel can have.
    /// </summary>
    public int MaxColorChannelValue => (1 << BitsPerChannel) - 1;

    /// <summary>
    /// This property notes whether output images should be in grayscale or full color.
    /// </summary>
    public bool Grayscale { get; set; }
    
    public Variables Variables { get; private set; }

    /// <summary>
    /// This method is used to apply any options the user specified on the command line to
    /// the context.
    /// </summary>
    /// <param name="options">The command line options to apply.</param>
    public void ApplyOptions(ProgramOptions options)
    {
        Width = options.Width ?? Width;
        Height = options.Height ?? Height;
        Gamma = options.Gamma ?? Gamma;
        BitsPerChannel = options.BitsPerChannel;
        Grayscale = options.Grayscale;
    }

    /// <summary>
    /// This method is used to create and populate our base variable pool.
    /// </summary>
    public void SetInitialVariables()
    {
        Variables = new Variables();

        Colors.AddToVariables(Variables);
        IndicesOfRefraction.AddToVariables(Variables);
        Directions.AddToVariables(Variables);
    }
}
