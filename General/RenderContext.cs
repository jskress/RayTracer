using System.Diagnostics.CodeAnalysis;
using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Options;
using RayTracer.Pixels;
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
    [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
    public IScanner Scanner { get; set; } = new PixelParallelScanner();

    /// <summary>
    /// This property holds whether angles are in radians or degrees.
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public bool AnglesAreRadians { get; set; }

    /// <summary>
    /// This property holds the gamma correction value to use when generating image files.
    /// </summary>
    public double Gamma { get; set; } = 2.2;

    /// <summary>
    /// This property notes whether gamma correction should actually be applied.
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public bool ApplyGamma { get; set; } = true;

    /// <summary>
    /// This property is used to suppress all shadow rendering.
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public bool SuppressAllShadows { get; set; }

    /// <summary>
    /// This property holds the bits per color channel to use when writing image files.
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public int BitsPerChannel { get; set; } = 8;

    /// <summary>
    /// This property provides the largest value a color channel can have.
    /// </summary>
    public int MaxColorChannelValue => (1 << BitsPerChannel) - 1;

    /// <summary>
    /// This property notes whether output images should be in grayscale or full color.
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public bool Grayscale { get; set; }

    /// <summary>
    /// This property holds the antialiasing option for the ray tracer.
    /// </summary>
    public AliasingOption AntiAliasing { get; set; } = new();

    /// <summary>
    /// This property holds how many places along a ray's crossing of a medium the scene's lamps are
    /// asked what they deliver there, for a medium that turns light aside.  It belongs here, beside
    /// the scanner and the anti-aliasing, because it says how hard to work rather than what anything
    /// is made of; a medium may name its own count when one volume in a scene needs more care than
    /// the rest.
    /// </summary>
    public int MediumSamples { get; set; } = 16;

    /// <summary>
    /// What every material's ambient is multiplied by once the scene is whole.
    /// <para>
    /// Ambient stands in for light that has bounced about the scene, which this renderer does not trace.
    /// A scene may want less of that stand-in than the materials in it assume -- a street at night is lit
    /// by its lamps, and every surface glowing at a fixed fraction of its own color is the one thing that
    /// stops it looking like night.  A multiplier is what reaches those materials: the alternative, a
    /// scene-wide ambient *value*, could only settle the ones that said nothing, and a curated library
    /// says something almost everywhere.
    /// </para>
    /// </summary>
    public double AmbientScale { get; set; } = 1;

    /// <summary>
    /// This property holds how many further turns of a light's path through a medium are followed past
    /// the first.  It is nothing by default, so that a scene says when it wants the cost: what a thick
    /// medium does to light it has already turned once is most of what it does, but it is also most of
    /// the work.
    /// </summary>
    public int MediumBounces { get; set; }

    /// <summary>
    /// This property holds the time value (in ticks) for the frame currently being rendered.
    /// </summary>
    public long Ticks { get; set; }

    /// <summary>
    /// This property holds the progress bar we are using.
    /// </summary>
    public IProgressReporter Progress { get; init; }

    /// <summary>
    /// This property holds the statistics collector being used.
    /// </summary>
    public Statistics Statistics { get; set; }

    /// <summary>
    /// This property holds the name of the scene to render, when the command line names one.  It
    /// takes precedence over any scene the file's <c>render</c> command names.
    /// </summary>
    public string SceneName { get; set; }

    /// <summary>
    /// This property holds the name of the camera to render with, when the command line names one.
    /// It takes precedence over any camera the file's <c>render</c> command names.
    /// </summary>
    public string CameraName { get; set; }

    /// <summary>
    /// This method is used to apply any options the user specified on the command line to
    /// the context.
    /// </summary>
    /// <param name="options">The command line options to apply.</param>
    /// <param name="frame">The frame to render.</param>
    public void ApplyOptions(RenderOptions options, long frame)
    {
        long seconds = frame / options.FrameRate;
        double remainder = frame % options.FrameRate;
        long fraction = remainder.Near(0)
            ? 0
            : (long) Math.Round(1_000 / (options.FrameRate / remainder));

        Width = options.Width ?? Width;
        Height = options.Height ?? Height;
        Gamma = options.Gamma ?? Gamma;

        // These are one-directional CLI overrides: passing the flag forces the setting on;
        // not passing it leaves whatever the scene's own `context { }` block configured.
        if (options.NoGamma)
            ApplyGamma = false;

        if (options.NoShadows)
            SuppressAllShadows = true;

        BitsPerChannel = options.BitsPerChannel ?? BitsPerChannel;

        // Grayscale is a switch, so it goes the way `no gamma` and `no shadows` do above: giving it
        // forces it on, and leaving it off says nothing about what the scene asked for.
        if (options.Grayscale)
            Grayscale = true;

        // Antialiasing is the scene's to ask for and the command line's to overrule.  It is left
        // alone unless `-a` was actually given, which is why the option is null until it is: a
        // scene that says it wants antialiasing must not have it taken away by a command line that
        // said nothing on the subject, or a sweep that renders the gallery strips every picture of
        // it without a word.
        AntiAliasing = options.AntiAliasing ?? AntiAliasing;
        Ticks = seconds * 1_000 + fraction;
        SceneName = options.SceneName;
        CameraName = options.CameraName;
    }
}
