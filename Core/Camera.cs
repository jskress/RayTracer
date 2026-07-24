using System.Diagnostics.CodeAnalysis;
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
    /// This property reports the field of view (in degrees) for the camera.
    /// </summary>
    [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
    public double FieldOfView { get; set; } = 90.0.ToRadians();

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
    /// This property holds how many places across the lens each ray is taken from.  It costs a ray
    /// apiece, and too few show as grain in the blurred parts rather than as smooth softness.
    /// </summary>
    public int BlurSamples { get; set; } = 16;

    /// <summary>
    /// This property holds the seed the lens's scatter is drawn from, or null for the default.
    /// Fixing it keeps a render the same from one run to the next.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// This property provides the view transformation that the camera represents.
    /// </summary>
    public Matrix Transform => GetTransform();

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
        PixelToRayConverter converter = new (
            context, FieldOfView, GetTransform(), new Lens(Aperture, GetFocalDistance(),
                BlurSamples, Seed));
        PixelRenderer renderer = context.AntiAliasing.GetRenderer(converter);

        context.ProgressBar?.SetTotal(canvas.Width * canvas.Height);

        context.Scanner.Scan(canvas.Width, canvas.Height, (x, y) =>
        {
            Color color = renderer.Render(scene, x, y);

            canvas.SetColor(color, x, y);
            context.ProgressBar?.Bump();
        });

        context.ProgressBar?.Done();

        return canvas;
    }

    /// <summary>
    /// This method generates the view transform for the camera.
    /// </summary>
    /// <returns>The view transform the camera represents.</returns>
    private Matrix GetTransform()
    {
        Vector forward = (LookAt - Location).Unit;
        Vector left = forward.Cross(Up.Unit);
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
