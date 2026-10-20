using System.Diagnostics.CodeAnalysis;
using RayTracer;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Pixels;

namespace RayTracer.Core;

/// <summary>
/// This class represents a camera in a scene.
/// </summary>
[SuppressMessage("ReSharper", "PropertyCanBeMadeInitOnly.Global")]
public class Camera : NamedThing
{
    /// <summary>
    /// This property holds the location of the camera.
    /// </summary>
    public Point Location { get; set; } = new (0, 0, -5);

    /// <summary>
    /// This property holds the point at which the camera is aimed.
    /// </summary>
    public Point LookAt { get; set; } = Point.Zero;

    /// <summary>
    /// This property holds a vector that indicates the direction of "up" for the
    /// camera.
    /// </summary>
    public Vector Up { get; set; } = Directions.Up;

    /// <summary>
    /// This property holds the sort of projection the camera uses.  It is perspective -- the
    /// ordinary sort -- unless a scene names another with a word before <c>camera</c>.
    /// </summary>
    public CameraProjectionType ProjectionType { get; set; } = CameraProjectionType.Perspective;

    /// <summary>
    /// This property reports the field of view (in degrees) for the camera.
    /// </summary>
    [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
    public double FieldOfView { get; set; } = 50.0.ToRadians();

    /// <summary>
    /// This property holds the radius of the camera's lens, in the scene's own units.  It is zero
    /// by default, which is a pinhole: every ray leaves the one point, so everything comes out in
    /// focus at every distance, which is what a ray tracer does unless told otherwise.  Give it a
    /// width and the camera gathers light across a disc instead, so only what sits at the focal
    /// distance stays sharp and everything nearer or further blurs -- the depth of field a real
    /// lens has.  The wider the lens, the shallower that field.
    /// </summary>
    public double Aperture { get; set; }

    /// <summary>
    /// This property holds how far ahead of the camera the plane of sharp focus lies, or null if
    /// it is to be worked out from <see cref="FocalPoint"/> instead.
    /// </summary>
    public double? FocalDistance { get; set; }

    /// <summary>
    /// This property holds a point in the scene to bring into focus, or null.  It is the same
    /// thing said the other way round from <see cref="FocalDistance"/>, and is usually the easier
    /// of the two: a scene knows where it put the thing it wants sharp.
    /// </summary>
    public Point FocalPoint { get; set; }

    /// <summary>
    /// This property holds how long the shutter stays open, against the motions the scene gives its
    /// surfaces: at one, a moving thing runs the whole of its motion while the shutter is open, and
    /// at a half it gets half way.  It is zero by default, a shutter that does not linger, so
    /// nothing smears however it is set moving.
    /// </summary>
    public double Shutter { get; set; }

    /// <summary>
    /// This property holds how many samples the camera takes.  It costs a ray apiece, and too few
    /// show as grain in the blurred parts rather than as smooth softness.  The lens and the shutter
    /// share the one set of them, so asking for both costs no more rays than asking for either.
    /// </summary>
    public int BlurSamples { get; set; } = 16;

    /// <summary>
    /// This property holds the seed the lens's scatter is drawn from, or null for the default.
    /// Fixing it keeps a render the same from one run to the next.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// This property holds how many stops brighter or darker the camera takes the light than it is: each
    /// stop doubles it.  It is nought by default, the light as the scene gives it.  A scene lit by a candle,
    /// or one whose sun is many times brighter than white, can be exposed for the way a photographer would,
    /// rather than by relighting it.
    /// </summary>
    public double Exposure { get; set; }

    /// <summary>
    /// This property holds how many blades the camera's opening is made of, or nought for a round one.  Blades
    /// make the opening a polygon, which shapes both what is out of focus -- each blurred point of light
    /// takes the opening's shape -- and the rays a bright point throws out when the camera has
    /// <see cref="Glare"/>.
    /// </summary>
    public int Blades { get; set; }

    /// <summary>
    /// This property holds how far the blades are turned, in radians.
    /// </summary>
    public double BladeTurn { get; set; }

    /// <summary>
    /// This property holds the share of the light the camera's opening spreads into its pattern -- the glare
    /// round a lamp -- from nought, the default, to one; see <see cref="Diffraction"/>.
    /// </summary>
    public double Glare { get; set; }

    /// <summary>
    /// This property provides the view transformation that the camera represents.
    /// </summary>
    public Matrix Transform => GetTransform();

    /// <summary>
    /// This property provides the places this camera looks from and the instants it looks at.  It
    /// is made once and shared, since the surfaces are told where to stand at each of those
    /// instants before the render begins, and must be told about the very same ones the rays will
    /// go on to ask for.
    /// </summary>
    public CameraSampler Sampler => _sampler ??= new CameraSampler(
        Aperture, GetFocalDistance(), Shutter, BlurSamples, Seed, Blades, BladeTurn);

    /// <summary>
    /// This property provides how far through the shutter's opening each sample looks.
    /// </summary>
    public double[] SampleTimes => Enumerable
        .Range(0, Sampler.SampleCount)
        .Select(Sampler.TimeFractionFor)
        .ToArray();

    private CameraSampler _sampler;

    /// <summary>
    /// This method works out how far ahead of the camera the plane of sharp focus lies.  A scene
    /// may say so outright, or name a point to bring into focus, in which case what matters is how
    /// far along the way the camera looks that point lies, not how far off it is -- a point off to
    /// one side is in focus with everything else the same distance ahead.  Said neither way, the
    /// camera focuses on what it was aimed at, which is nearly always what was meant.
    /// </summary>
    /// <returns>The distance ahead of the camera at which things are in sharp focus.</returns>
    public double GetFocalDistance()
    {
        if (FocalDistance.HasValue)
            return FocalDistance.Value;

        Point focus = FocalPoint ?? LookAt;

        return (focus - Location).Dot((LookAt - Location).Unit);
    }

    /// <summary>
    /// This method is used to render the given scene to the specified canvas.
    /// </summary>
    /// <param name="context">The current rendering context.</param>
    /// <param name="scene">The scene to render.</param>
    /// <returns>The image, as a canvas, that resulted from the render.</returns>
    public Canvas Render(RenderContext context, Scene scene)
    {
        Canvas canvas = context.NewCanvas;
        PixelToRayConverter converter = CreateConverter(context);
        PixelRenderer renderer = context.AntiAliasing.GetRenderer(converter);

        renderer.Statistics = context.Statistics;
        renderer.Spectral = context.Spectral;
        scene.Statistics = context.Statistics;

        // Turning a color into a spectrum wants a table that is built the first time it is asked for;
        // it is asked for here, before the first pixel, rather than by whichever pixel gets there first
        // while every other thread waits on it.
        if (context.Spectral)
            SpectralBasis.Shared.Prepare();

        WhiteBalance balance = context.WhiteBalance is { } kelvin ? new WhiteBalance(kelvin) : null;
        double gain = Math.Pow(2, context.Exposure ?? Exposure);

        // The sampler tells samples apart as the picture will show them -- exposed, and developed by the film
        // curve -- unless the picture shows the light just as it is.
        if (gain != 1 || (context.ApplyGamma && context.FilmCurve != FilmCurve.None))
            renderer.Seen = light => context.Shown(Exposed(light, gain));

        context.Progress?.SetTotal(canvas.Width * canvas.Height);

        // The progress bar takes the cursor away while it has the line, so it must be given the
        // chance to hand it back however the render ends.  A scene that fails part way through would
        // otherwise leave the terminal with no cursor at all.  The tool reporter has no cursor to
        // give back but still wants its closing line, which is why this is asked of the interface
        // rather than of the bar.
        try
        {
            context.Scanner.Scan(canvas.Width, canvas.Height, (x, y) =>
            {
                Color color = renderer.Render(scene, x, y);

                if (balance is not null)
                    color = balance.Apply(color);

                if (gain != 1)
                    color = Exposed(color, gain);

                canvas.SetColor(color, x, y);
                context.Statistics?.CountPixel();
                context.Progress?.Bump();
            });
        }
        finally
        {
            context.Progress?.Done();
        }

        // The opening spreads the light as the picture forms, so the whole picture is spread at once; the
        // film curve that develops it comes after, when it is written.
        new Diffraction(Glare, Blades, BladeTurn).Spread(canvas);

        return canvas;
    }

    // Light taken so many times brighter, keeping how much of the pixel it covers.
    private static Color Exposed(Color light, double gain) =>
        new (light.Red * gain, light.Green * gain, light.Blue * gain, light.Alpha);

    /// <summary>
    /// This method fills the scene's cache of bounced light, if it keeps one, by looking at the scene through
    /// this camera in passes over ever finer grids of the picture's pixels; see
    /// <see cref="Scene.FillTheBounceCache{TS}"/>.  It looks through each pixel's middle, at the first
    /// instant and from the middle of the lens, which is where every sample of the pixel will look near.
    /// </summary>
    /// <param name="context">The current rendering context.</param>
    /// <param name="scene">The scene to fill the cache of.</param>
    public void FillTheBounceCache(RenderContext context, Scene scene)
    {
        if (scene.DiffuseBounces <= 0 || scene.DiffuseCacheError <= 0)
            return;

        PixelToRayConverter converter = CreateConverter(context);

        if (context.Spectral)
        {
            SpectralBasis.Shared.Prepare();
            scene.FillTheBounceCache<BandSpectrum>(
                (x, y) => converter.GetRayForPixel(x, y), context.Width, context.Height);
        }
        else
        {
            scene.FillTheBounceCache<RgbSpectrum>(
                (x, y) => converter.GetRayForPixel(x, y), context.Width, context.Height);
        }
    }

    /// <summary>
    /// This method builds the pixel-to-ray converter for the camera's projection.
    /// </summary>
    /// <param name="context">The current rendering context.</param>
    /// <returns>The converter that turns each pixel into a ray.</returns>
    private PixelToRayConverter CreateConverter(RenderContext context)
    {
        Matrix transform = GetTransform();

        return ProjectionType switch
        {
            CameraProjectionType.Perspective => new PerspectiveRayConverter(
                context, FieldOfView, transform, Sampler),
            CameraProjectionType.Orthographic => new OrthographicRayConverter(
                context, FieldOfView, transform, Sampler),
            CameraProjectionType.Fisheye => Curved(new FisheyeRayConverter(
                context, FieldOfView, transform, Sampler)),
            CameraProjectionType.UltraWide => Curved(new UltraWideRayConverter(
                context, FieldOfView, transform, Sampler)),
            CameraProjectionType.Panoramic => Curved(new PanoramicRayConverter(
                context, FieldOfView, transform, Sampler)),
            CameraProjectionType.Spherical => Curved(new SphericalRayConverter(
                context, transform, Sampler)),
            _ => throw new NotImplementedException(
                $"The {ProjectionType} camera projection is not yet implemented.")
        };
    }

    /// <summary>
    /// This method notes the one thing the curved projections share: they have no flat lens, so a
    /// scene that gave one an aperture is warned that it does nothing here.  The shutter is left
    /// alone, since motion blur works whatever the projection.
    /// </summary>
    /// <param name="converter">The converter to hand back.</param>
    /// <returns>The converter it was given.</returns>
    private PixelToRayConverter Curved(PixelToRayConverter converter)
    {
        if (Aperture > 0)
        {
            Terminal.Out(
                $"Warning: a {ProjectionType.ToString().ToLowerInvariant()} camera has no lens, " +
                "so its aperture is ignored and the render is done as a pinhole.");
        }

        return converter;
    }

    /// <summary>
    /// This method generates the view transform for the camera.
    /// <para>
    /// **The sideways axis is made of unit length, which the book's transform does not do.**  Crossing
    /// the way the camera looks with "up" gives a vector as long as the sine of the angle between them --
    /// one only for a camera looking level -- and left as it came, it shrank the picture's sideways and
    /// upward axes alike by the cosine of the camera's tilt.  Every tilted camera saw wider than its field
    /// of view said: 1.5% at ten degrees down, 41% at forty-five, and looking straight down, everything at
    /// a point.
    /// </para>
    /// </summary>
    /// <returns>The view transform the camera represents.</returns>
    private Matrix GetTransform()
    {
        Vector forward = (LookAt - Location).Unit;
        Vector left = forward.Cross(Up.Unit);

        // Looking straight along "up" leaves no way to say which way is sideways.
        if (left.Magnitude < 1e-12)
        {
            throw new Exception(
                "A camera cannot look straight along its up direction; give it an \"up\" that is not " +
                "the way it looks.");
        }

        left = left.Unit;

        Vector trueUp = left.Cross(forward);

        return new Matrix(
        [
             left.X,     left.Y,     left.Z,    0,
             trueUp.X,   trueUp.Y,   trueUp.Z,  0,
            -forward.X, -forward.Y, -forward.Z, 0,
             0,          0,          0,         1
        ]) * Transforms.Translate(-Location.X, -Location.Y, -Location.Z);
    }
}
