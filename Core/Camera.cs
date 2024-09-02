using System.Diagnostics.CodeAnalysis;
using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Graphics;

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
    /// This property provides the view transformation that the camera represents.
    /// </summary>
    public Matrix Transform => GetTransform();

    /// <summary>
    /// This method is used to render the given scene to the specified canvas.
    /// </summary>
    /// <param name="context">The current rendering context.</param>
    /// <param name="scene">The scene to render.</param>
    /// <returns>The image, as a canvas, that resulted from the render.</returns>
    public Canvas Render(RenderContext context, Scene scene)
    {
        Canvas canvas = context.NewCanvas;
        PixelToRayConverter converter = new (context, FieldOfView, GetTransform());

        context.ProgressBar?.SetTotal(canvas.Width * canvas.Height);

        context.Scanner.Scan(canvas.Width, canvas.Height, (x, y) =>
        {
            Ray ray = converter.GetRayForPixel(x, y);

            canvas.SetColor(scene.GetColorFor(ray), x, y);
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
