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

    /// <summary>
    /// This property holds how bright the image is: what its colors are multiplied by.  A photograph of
    /// the sky holds light in whatever units the camera that took it left it in, so bringing it to the
    /// scene's is the one thing it needs said of it -- a single knob, like a physical sky's, so that its
    /// sun and its sky go up or down together.
    /// </summary>
    public double Brightness { get; set; } = 1;

    private Texture _texture;

    // How the image is laid on the surface it was readied for, and the radius of the ring a toroidal map's tube runs
    // round there: the answer for anything that does not say otherwise (see Laying).
    private Laying _readied = new (ImageMapType.Spherical, 1, TextureWrap.Repeat, TextureWrap.Repeat);

    /// <summary>
    /// This property holds how many texels the image has across, once it is loaded, and nought before.
    /// </summary>
    public int TexelsAcross => _texture?.Width ?? 0;

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
        // First, check that the image can be laid on the surface if no way was given, which a shape with no map of
        // its own cannot say.  No surface at all means we are a scene's background; a group, a combination or an
        // instance is never read itself, its parts are, so it says nothing of how the image is laid.
        bool holder = surface is Group or CsgSurface or Instance;

        if (MapType is null && surface is not null && !holder && DefaultMapFor(surface) is null)
            throw new Exception("Cannot determine image map type for surface.");

        // Next, load the image.  The image is read as the light it stands for when the render turns its light back
        // into a picture's values at the end, and as the values it holds when the scene writes no gamma -- in such
        // a scene a color is the value that reaches the file, and an image's colors must mean what the scene's own
        // do, or a texture under white light would not come back as itself.
        _texture = ImageReference.TextureFor(context.ApplyGamma);

        if (!holder)
            _readied = LayingFor(MapType ?? DefaultMapFor(surface) ?? ImageMapType.Spherical, surface);
    }

    /// <summary>
    /// This record holds how the image is laid on one surface: the map, the radius of the ring a toroidal map's tube
    /// runs round, and what lies past the image's edges each way.
    /// </summary>
    private readonly record struct Laying(ImageMapType Map, double Ring, TextureWrap WrapAcross, TextureWrap WrapDown);

    /// <summary>
    /// This method returns the map an image given none is laid on a surface by, from the shape it is, or
    /// <c>null</c> for a shape that has none: a background, standing for the sky -- a sphere of radius one,
    /// infinitely far off -- is spherical.
    /// </summary>
    private static ImageMapType? DefaultMapFor(Surface surface) => surface switch
    {
        Cube or Plane or Triangle or Parallelogram => ImageMapType.Planar,
        Sphere => ImageMapType.Spherical,
        Cylinder => ImageMapType.Cylindrical,
        Torus => ImageMapType.Toroidal,
        null => ImageMapType.Spherical,
        _ => null
    };

    /// <summary>
    /// This method returns how the image is laid by the given map on the given surface: round the surface's own ring,
    /// for a toroidal map on a torus.
    /// </summary>
    private Laying LayingFor(ImageMapType map, Surface surface)
    {
        (TextureWrap across, TextureWrap down) = ((ImageMapType?) map).WrapFor(Once);

        return new Laying(map, surface is Torus torus ? torus.MajorRadius : 1, across, down);
    }

    /// <summary>
    /// This method returns how the image is laid on the surface being read (see <see cref="Pigment.Wearer"/>): by the
    /// map it was given, or the one the surface's own shape implies, worked out for the surface being read rather
    /// than once for the pigment -- a pigment worn by a cube and a sphere lays the image flat on the one and round the
    /// other.  Read for no surface, or for one whose shape implies nothing, it is laid as it was readied.
    /// </summary>
    private Laying Laid()
    {
        Surface wearer = Wearer;

        if (wearer is null || (MapType ?? DefaultMapFor(wearer)) is not { } map)
            return _readied;

        double ring = wearer is Torus torus ? torus.MajorRadius : 1;

        return map == _readied.Map && ring == _readied.Ring ? _readied : LayingFor(map, wearer);
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
        Laying laid = Laid();
        ImageMapType? map = laid.Map;
        (double u, double v) = map.GetTexelLocationFor(point, _texture.Width, _texture.Height, laid.Ring);

        return double.IsNaN(u) || double.IsNaN(v)
            ? Colors.Transparent
            : Brightened(_texture.Bilinear(u, v, laid.WrapAcross, laid.WrapDown));
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

        Laying laid = Laid();
        ImageMapType? map = laid.Map;
        int width = _texture.Width, height = _texture.Height;
        (double u, double v) = map.GetTexelLocationFor(point, width, height, laid.Ring);

        if (double.IsNaN(u) || double.IsNaN(v))
            return Colors.Transparent;

        (double du0, double dv0) = map.AxesFor(point, footprint.Across, width, height, laid.Ring);
        (double du1, double dv1) = map.AxesFor(point, footprint.Along, width, height, laid.Ring);

        return Brightened(double.IsNaN(du0 + dv0 + du1 + dv1)
            ? _texture.Bilinear(u, v, laid.WrapAcross, laid.WrapDown)
            : _texture.Filtered(u, v, du0, dv0, du1, dv1, laid.WrapAcross, laid.WrapDown));
    }

    /// <summary>
    /// This method brightens a color read from the image, leaving how opaque it is alone.
    /// </summary>
    private Color Brightened(Color color)
    {
        return Brightness == 1
            ? color
            : new Color(color.Red * Brightness, color.Green * Brightness, color.Blue * Brightness, color.Alpha);
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
               Once == pigmentation.Once &&
               Brightness == pigmentation.Brightness;
    }
}
