using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;

namespace RayTracer.Pigments;

/// <summary>
/// This class provides a pigment that based around an image.
/// </summary>
public class ImagePigment : Pigment
{
    /// <summary>
    /// This property notes the type of map to use in applying the image to a surface.
    /// </summary>
    public ImageMapType? MapType { get; set; }

    /// <summary>
    /// This property holds the file name or URL of the image to use.
    /// </summary>
    public string ImageName { get; set; }

    /// <summary>
    /// This property notes whether repeating the pattern outside the unit cube should be
    /// turned off.
    /// This applies only when the map type is planar or cylindrical.
    /// </summary>
    public bool Once { get; set; }
    
    private Canvas _canvas;

    /// <summary>
    /// This method is used to give the pigment a time to prepare for rendering.
    /// For example, a pigment that uses an image would use this as the opportunity to load
    /// the required image before rendering actually begins.
    /// This will be invoked after the seed has been set.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="surface">The surface that this pigment is set on.</param>
    protected override void PrepareForRendering(RenderContext context, Surface surface)
    {
        // First, try to default the map type if we weren't given one.
        MapType ??= surface switch
        {
            Cube or Plane or Triangle or Parallelogram => ImageMapType.Planar,
            Sphere => ImageMapType.Spherical,
            Cylinder => ImageMapType.Cylindrical,
            Torus => ImageMapType.Toroidal,
            _ => throw new Exception("Cannot determine image map type for surface.")
        };

        // Next, load the image.
        ImageFile imageFile = new ImageFile(ImageName);

        _canvas = imageFile.Load()[0];
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  We return the
    /// color that our wrapped pigment returns, with noise applied.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public override Color GetColorFor(Point point)
    {
        (double u, double v) = MapType.GetImageLocationFor(
            point, _canvas.Width, _canvas.Height, Once);
        
        if (double.IsNaN(u) || double.IsNaN(v))
            return Colors.Transparent;

        int x = Math.Min((int) Math.Round(u), _canvas.Width - 1);
        int y = Math.Min((int) Math.Round(v),  _canvas.Height - 1);

        return _canvas.GetPixel(x, y);
    }

    /// <summary>
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigment other)
    {
        return other is ImagePigment pigmentation &&
               MapType == pigmentation.MapType &&
               ImageName == pigmentation.ImageName &&
               Once == pigmentation.Once;
    }
}
