using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Renderer;
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
    /// This property holds whether angles are in radians or degrees.
    /// </summary>
    public bool AnglesAreRadians { get; set; }

    /// <summary>
    /// This property holds the gamma correction value to use when generating image files.
    /// </summary>
    public double Gamma { get; set; } = 2.2;

    /// <summary>
    /// This property notes whether gamma correction should actually be applied.
    /// </summary>
    public bool ApplyGamma { get; set; } = true;

    /// <summary>
    /// This property notes whether gamma correction information should be noted in the
    /// output image, if the image format supports this.
    /// </summary>
    public bool ReportGamma { get; set; }

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

    /// <summary>
    /// This property holds the time value (in ticks) for the frame currently being rendered.
    /// </summary>
    public long Ticks { get; set; }

    /// <summary>
    /// This property holds the statistics collector being used.
    /// </summary>
    public Statistics Statistics { get; set; }
    
    /// <summary>
    /// This method is used to apply any options the user specified on the command line to
    /// the context.
    /// </summary>
    /// <param name="options">The command line options to apply.</param>
    /// <param name="frame">The frame to render.</param>
    public void ApplyOptions(ProgramOptions options, long frame)
    {
        long seconds = frame / options.FrameRate;
        double remainder = frame % options.FrameRate;
        long fraction = remainder.Near(0)
            ? 0
            : (long) Math.Round(1_000 / (options.FrameRate / remainder));

        Width = options.Width ?? Width;
        Height = options.Height ?? Height;
        Gamma = options.Gamma ?? Gamma;
        ApplyGamma = !options.NoGamma ?? ApplyGamma;
        ReportGamma = options.ReportGamma ?? ReportGamma;
        BitsPerChannel = options.BitsPerChannel;
        Grayscale = options.Grayscale;
        Ticks = seconds * 1_000 + fraction;
    }
}
