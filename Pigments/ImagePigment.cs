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
    /// This property holds the information for the image to use.
    /// </summary>
    public ImageReference ImageReference { get; set; }

    /// <summary>
    /// This property notes the type of map to use in applying the image to a surface.
    /// </summary>
    public ImageMapType? MapType { get; set; }

    /// <summary>
    /// This property notes whether repeating the pattern outside the unit cube should be
    /// turned off.
    /// This applies only when the map type is planar or cylindrical.
    /// </summary>
    public bool Once { get; set; }

    private Texture _texture;
    private TextureWrap _wrapAcross;
    private TextureWrap _wrapDown;

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
            // No surface at all means we are a scene's background, and the sky a background is asked
            // about is a sphere of radius one, infinitely far off.
            null => ImageMapType.Spherical,
            _ => throw new Exception("Cannot determine image map type for surface.")
        };

        // Next, load the image and settle what lies past its edges.  The image is read as the light it
        // stands for when the render turns its light back into a picture's values at the end, and as
        // the values it holds when the scene writes no gamma -- in such a scene a color is the value
        // that reaches the file, and an image's colors must mean what the scene's own do, or a texture
        // under white light would not come back as itself.
        _texture = ImageReference.TextureFor(context.ApplyGamma);
        (_wrapAcross, _wrapDown) = MapType.WrapFor(Once);
    }

    /// <summary>
    /// An image may carry an alpha channel of its own, and finding out would mean reading it, so this answers yes and pays for the sample.
    /// </summary>
    public override bool MayTransmit => true;

    /// <summary>
    /// This method produces the image's color at a point, read between the four texels nearest it.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    public override Color GetColorFor(Point point)
    {
        (double u, double v) = MapType.GetTexelLocationFor(point, _texture.Width, _texture.Height);

        return double.IsNaN(u) || double.IsNaN(v)
            ? Colors.Transparent
            : _texture.Bilinear(u, v, _wrapAcross, _wrapDown);
    }

    /// <summary>
    /// This method produces the image's color averaged over the patch of it a footprint covers.  Each
    /// edge of the footprint is carried into the image by placing both its ends, and the two edges so
    /// carried are the axes of the ellipse the texture averages over.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <param name="footprint">The patch of surface the ray is answerable for.</param>
    /// <returns>The image's color over the patch.</returns>
    public override Color GetColorFor(Point point, Footprint footprint)
    {
        if (footprint is null || footprint.IsEmpty)
            return GetColorFor(point);

        int width = _texture.Width, height = _texture.Height;
        (double u, double v) = MapType.GetTexelLocationFor(point, width, height);

        if (double.IsNaN(u) || double.IsNaN(v))
            return Colors.Transparent;

        (double du0, double dv0) = MapType.AxesFor(point, footprint.Across, width, height);
        (double du1, double dv1) = MapType.AxesFor(point, footprint.Along, width, height);

        return double.IsNaN(du0 + dv0 + du1 + dv1)
            ? _texture.Bilinear(u, v, _wrapAcross, _wrapDown)
            : _texture.Filtered(u, v, du0, dv0, du1, dv1, _wrapAcross, _wrapDown);
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
               ImageReference.Matches(pigmentation.ImageReference) &&
               Once == pigmentation.Once;
    }
}
