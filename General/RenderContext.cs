using RayTracer.Basics;
using System.Diagnostics.CodeAnalysis;
using RayTracer.Extensions;
using RayTracer.Graphics;
using RayTracer.Options;
using RayTracer.Pixels;
using RayTracer.Renderer;
using RayTracer.Scanners;

using RayTracer.Geometry;

namespace RayTracer.General;

/// <summary>
/// This class represents the current rendering context.
/// </summary>
public class RenderContext
{
    /// <summary>
    /// This property holds the shapes built once and stood in many places, by what was asked for.
    /// <para>
    /// **It belongs to the render rather than to the primitive**, so that nothing built for one
    /// picture is handed to the next -- a shape carries the state it was got ready with, and a second
    /// render would find it already prepared and never notice.
    /// </para>
    /// </summary>
    internal Dictionary<string, Surface> SharedShapes { get; } = new ();

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
    /// This property holds the gamma to write image files with, when a scene or the command line gives one;
    /// see <see cref="OutputCurve"/>.
    /// </summary>
    public double Gamma
    {
        get;
        set
        {
            field = value;
            GammaIsSaid = true;
        }
    } = 2.2;

    /// <summary>
    /// This property notes whether a scene or the command line has said what gamma to write with.
    /// </summary>
    public bool GammaIsSaid { get; private set; }

    /// <summary>
    /// This property notes whether gamma correction should actually be applied.
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public bool ApplyGamma { get; set; } = true;

    /// <summary>
    /// This property holds how the light a render ends with is developed into a picture; see
    /// <see cref="Graphics.FilmCurve"/>.  It is not asked for a file that holds light, nor with `no gamma`.
    /// </summary>
    public FilmCurve FilmCurve { get; set; } = FilmCurve.Soft;

    /// <summary>
    /// This property holds how many stops to expose every camera by, when the command line says, in place of
    /// what each camera says for itself.
    /// </summary>
    public double? Exposure { get; set; }

    /// <summary>
    /// This method returns what a picture shows of the given light: developed by the film curve when gamma
    /// correction is on, and as it stands, but for being cut off at white, with `no gamma`.
    /// </summary>
    /// <param name="light">The light.</param>
    /// <returns>What the picture shows, at most white, before the file's curve.</returns>
    public Color Shown(Color light)
    {
        return (ApplyGamma ? FilmCurve : FilmCurve.None).Develop(light);
    }

    /// <summary>
    /// This property returns the curve light is written to an image file with.
    /// <para>
    /// **It is the sRGB curve unless something says otherwise**, because that is the curve every viewer
    /// reads a picture with: a PNG that says nothing of how it was written is taken as sRGB.  Until
    /// 2026-10-19 light was written with a plain 2.2 power, which tracks the sRGB curve closely except in
    /// the deepest darks, where a viewer then showed them brighter than they were worked out to be -- a
    /// thousandth of white came out as three and a half thousandths.  A gamma given outright is written as
    /// the plain power it asks for, and the file says so; `no gamma` writes light as it stands.
    /// </para>
    /// </summary>
    public TransferFunction OutputCurve => !ApplyGamma ? TransferFunction.Linear
        : GammaIsSaid ? TransferFunction.Power(1 / Gamma)
        : TransferFunction.Srgb;

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
    /// This property notes whether light is carried wavelength by wavelength, rather than in the three
    /// broad bands of red, green and blue that are the default.
    /// </summary>
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public bool Spectral { get; set; }

    /// <summary>
    /// This property holds the temperature, in kelvin, of the glow the picture is balanced to show as
    /// white, or <c>null</c> to leave white where an even spectrum puts it, at about 5500 K.  See
    /// <see cref="RayTracer.Graphics.WhiteBalance"/>.
    /// </summary>
    public double? WhiteBalance { get; set; }

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
    /// This property holds how many directions a rough surface looks in for what it mirrors, the first
    /// time a ray meets one; see <see cref="Core.Material.Roughness"/>.
    /// </summary>
    public int ReflectionSamples { get; set; } = 16;

    /// <summary>
    /// This property holds how many walks under its surface a material that scatters light inside takes, the
    /// first time a ray from the eye meets one; see <see cref="Core.Subsurface"/>.
    /// </summary>
    public int SubsurfaceSamples { get; set; } = 64;

    /// <summary>
    /// This property holds how many directions the weathering patterns look about in at each place they are
    /// asked about; see <see cref="Core.Weathering"/>.
    /// </summary>
    public int WeatheringSamples { get; set; } = Core.Weathering.DefaultSamples;

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
    /// This property holds how many times light is followed as it bounces from one matte surface to the
    /// next before it reaches the eye: none by default, so that a scene says when it wants the cost, and
    /// <c>ambient</c> stands in for all of it.  One bounce is the light off a wall onto the floor beside
    /// it; two, the light off the floor back onto the wall; each further bounce is fainter by the
    /// surfaces' own color, so a few is nearly all of it.
    /// </summary>
    public int DiffuseBounces { get; set; }

    /// <summary>
    /// This property holds how many directions a matte surface looks in for the light bounced onto it,
    /// the first time a ray from the eye meets one.  Every bounce after that looks in one, so that the
    /// cost is paid once.
    /// </summary>
    public int DiffuseSamples { get; set; } = 32;

    /// <summary>
    /// This property holds how far the cache of bounced light may stray, as a share of the light, or nought
    /// for no cache -- the default, every place working its bounced light out for itself.  Asked for, the
    /// light is worked out at scattered places and blended between them, which is smoother for far fewer
    /// directions; see <see cref="RayTracer.Core.IrradianceCache{TS}"/>.
    /// </summary>
    public double DiffuseCacheError { get; set; }

    /// <summary>
    /// This field holds the error a cache of bounced light is given when a scene asks for one without
    /// saying: a seventh of the light, which is where Ward's cache is usually set.
    /// </summary>
    public const double DefaultDiffuseCacheError = 0.15;

    /// <summary>
    /// This property holds how many photons are sent from the lamps to make the scene's caustics -- the
    /// light mirrors and glass gather onto matte surfaces -- or none, the default, for no caustics.  Asked
    /// for, glass that bends light also stops shadow rays, the light through it being the photons'; see
    /// <see cref="RayTracer.Core.Scene.ShootCausticPhotons"/>.
    /// </summary>
    public int CausticPhotons { get; set; }

    /// <summary>
    /// This field holds how many photons a scene's caustics are made of when it asks for them without
    /// saying how many.
    /// </summary>
    public const int DefaultCausticPhotons = 2_000_000;

    /// <summary>
    /// This property holds how many surfaces a ray from the eye may be mirrored or bent at before it is
    /// followed no further, or <c>null</c> for the four it has always been.  Said, a way worth less than a
    /// 1024th of its pixel is not followed either, which is what keeps a deep trace through glass -- which
    /// sends every ray two ways at every surface -- from costing twice as much for every surface deeper.  A
    /// tumbler of water and a wine glass took 10 minutes 11 seconds twelve deep, and 60 seconds with this
    /// (44 seconds four deep), differing in 3.6% of pixels by at most 4 levels in 255.
    /// </summary>
    public int? TraceDepth { get; set; }

    /// <summary>
    /// This field holds the least share of its pixel a way a ray goes must be worth to be followed, when the
    /// scene has said how deep to trace.
    /// </summary>
    public const double Bailout = 1.0 / 1024;

    /// <summary>
    /// This property holds how gravity pulls the scene's bodies and swarms, in meters a second squared.
    /// </summary>
    public Vector Gravity { get; set; } = new (0, -9.81, 0);

    /// <summary>
    /// This property holds how the air moves, which is what drags the scene's bodies and swarms along with it.
    /// </summary>
    public Vector Wind { get; set; } = new (0, 0, 0);

    /// <summary>
    /// This property holds the animation a scene's context asks for, or null for a still.
    /// </summary>
    public Animation Animation { get; set; }

    /// <summary>
    /// This property holds the time, in seconds, of the frame being rendered: nought for a still.
    /// </summary>
    public double Time { get; set; }

    /// <summary>
    /// This property holds how long a frame lasts, in seconds, when the render is a frame of an animation, and
    /// nought for a still.  A placement that follows the time is blurred over it, as far as the shutter is open.
    /// </summary>
    public double FrameInterval { get; set; }

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
    public void ApplyOptions(RenderOptions options)
    {
        Width = options.Width ?? Width;
        Height = options.Height ?? Height;
        // Only a gamma actually given is assigned, since assigning one is what says it.
        if (options.Gamma is { } gamma)
            Gamma = gamma;

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

        // Spectral is a switch as well.
        if (options.Spectral)
            Spectral = true;

        WhiteBalance = options.WhiteBalance ?? WhiteBalance;
        Exposure = options.Exposure ?? Exposure;

        if (options.FilmCurve is { } curve)
            FilmCurve = FilmCurve.Named(curve);

        // Antialiasing is the scene's to ask for and the command line's to overrule.  It is left
        // alone unless `-a` was actually given, which is why the option is null until it is: a
        // scene that says it wants antialiasing must not have it taken away by a command line that
        // said nothing on the subject, or a sweep that renders the gallery strips every picture of
        // it without a word.
        AntiAliasing = options.AntiAliasing ?? AntiAliasing;
        SceneName = options.SceneName;
        CameraName = options.CameraName;
    }
}
