using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This enumeration notes the supported image map types.
/// </summary>
public enum ImageMapType
{
    /// <summary>
    /// This entry notes a planar image map.
    /// </summary>
    Planar,

    /// <summary>
    /// This entry notes a spherical image map.
    /// </summary>
    Spherical,

    /// <summary>
    /// This entry notes a cylindrical image map.
    /// </summary>
    Cylindrical,

    /// <summary>
    /// This entry notes a toroidal image map.
    /// </summary>
    Toroidal
}

/// <summary>
/// This class provides some useful functionality on our image map type enum.
/// </summary>
public static class ImageMapTypeExtensions
{
    private const double TwoPi = Math.PI * 2;

    /// <summary>
    /// This method is used to take a 3D point and, using the indicated image map type, find where in
    /// the image it lies, in texels: a texel's middle is half a texel in from its corner, and the image
    /// spans nought to its width across and nought to its height down.
    /// <para>
    /// The place is left unwrapped.  What lies past an edge -- the image again, the edge itself, or
    /// nothing -- is the texture's question, answered by <see cref="WrapFor"/>; and a footprint is
    /// measured as the difference between two places, which wrapping would break wherever it crossed an
    /// edge.  The places for the angles round an axis are the exception, since an angle comes back to
    /// itself; see <see cref="AxesFor"/>.
    /// </para>
    /// </summary>
    /// <param name="imageMapType">The type of image map to use.</param>
    /// <param name="point">The point to transform.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <returns>The X and Y coordinates within the image to use, or NaN for a point the map cannot
    /// place.</returns>
    public static (double, double) GetTexelLocationFor(
        this ImageMapType? imageMapType, Point point, double width, double height, double ring = 1)
    {
        return imageMapType switch
        {
            ImageMapType.Planar => (point.X * width, point.Z * height),
            ImageMapType.Spherical => GetSphericalImageLocationFor(point, width, height),
            ImageMapType.Cylindrical => GetCylindricalImageLocationFor(point, width, height),
            ImageMapType.Toroidal => GetToroidalImageLocationFor(point, width, height, ring),
            _ => throw new ArgumentOutOfRangeException(nameof(imageMapType), imageMapType, null)
        };
    }

    /// <summary>
    /// This method reports what lies past the image's edges under the indicated image map type.
    /// <para>
    /// An image drawn `once` has nothing round it, so past its edges a surface shows clear -- and, now
    /// that the image is averaged over a pixel's patch, its edge fades out over that patch rather than
    /// stopping on a texel.  Otherwise a planar map tiles both ways and a cylindrical one up its axis;
    /// a sphere's top and bottom rows are its poles and go on as themselves; and every angle round an
    /// axis comes back to where it began.
    /// </para>
    /// </summary>
    /// <param name="imageMapType">The type of image map to use.</param>
    /// <param name="once">A flag indicating whether the image may repeat.</param>
    /// <returns>What lies past the image's sides, and past its top and bottom.</returns>
    public static (TextureWrap, TextureWrap) WrapFor(this ImageMapType? imageMapType, bool once)
    {
        TextureWrap tiled = once ? TextureWrap.Border : TextureWrap.Repeat;

        return imageMapType switch
        {
            ImageMapType.Planar => (tiled, tiled),
            ImageMapType.Spherical => (TextureWrap.Repeat, TextureWrap.Clamp),
            ImageMapType.Cylindrical => (TextureWrap.Repeat, tiled),
            ImageMapType.Toroidal => (TextureWrap.Repeat, TextureWrap.Repeat),
            _ => throw new ArgumentOutOfRangeException(nameof(imageMapType), imageMapType, null)
        };
    }

    /// <summary>
    /// This method works out how far across and down the image one edge of a footprint reaches, by
    /// placing both ends of the edge and taking the difference.
    /// <para>
    /// A place measured as an angle runs from nought to the image's width and starts again, so the two
    /// ends of an edge that crosses the seam lie nearly a whole image apart when they are really a texel
    /// or two; such a difference is taken the short way round.  Places along a straight line need no
    /// such care, and get none -- a footprint at the horizon of a tiled floor really does span many
    /// tiles, and must be allowed to say so.
    /// </para>
    /// </summary>
    /// <param name="imageMapType">The type of image map to use.</param>
    /// <param name="point">The middle of the footprint.</param>
    /// <param name="edge">One edge of the footprint, in the same space as the point.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <returns>How far across and down the image the edge reaches, or NaN where either end cannot be
    /// placed.</returns>
    public static (double, double) AxesFor(
        this ImageMapType? imageMapType, Point point, Vector edge, double width, double height, double ring = 1)
    {
        (double u0, double v0) = imageMapType.GetTexelLocationFor(point - edge * 0.5, width, height, ring);
        (double u1, double v1) = imageMapType.GetTexelLocationFor(point + edge * 0.5, width, height, ring);
        double du = u1 - u0;
        double dv = v1 - v0;

        if (imageMapType is ImageMapType.Spherical or ImageMapType.Cylindrical or ImageMapType.Toroidal)
            du -= width * Math.Round(du / width);

        if (imageMapType is ImageMapType.Toroidal)
            dv -= height * Math.Round(dv / height);

        return (du, dv);
    }

    /// <summary>
    /// This method is used to take a 3D point and, using a spherical map type, find where in the
    /// image it lies, in texels.
    /// </summary>
    /// <param name="point">The point to transform.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <returns>The X and Y coordinates within the image to use.</returns>
    private static (double, double) GetSphericalImageLocationFor(
        Point point, double width, double height)
    {
        Vector vector = new Vector(point);
        double magnitude = vector.Magnitude;
        
        if (magnitude == 0)
            return (double.NaN, double.NaN);

        vector = vector.Unit;
        
        // Determine its angle from the x-z plane.
        double phi = 0.5 + Math.Asin(vector.Y) / Math.PI; // This will be from 0 to 1

        // Determine its angle from the point (1, 0, 0) in the x-z plane.
        double length = Math.Sqrt(vector.X * vector.X + vector.Z * vector.Z);
        double theta;

        if (length == 0)
            theta = 0;
        else
        {
            if (vector.Z == 0.0)
                theta = vector.X > 0 ? 0.0 : Math.PI;
            else
            {
                theta = Math.Acos(vector.X / length);

                if (vector.Z < 0.0)
                    theta = TwoPi - theta;
            }

            theta /= TwoPi;  // This will be from 0 to 1
        }

        // Note: we need to flip the Y value; otherwise the image is upside down.
        return (theta * width, height - phi * height);
    }

    /// <summary>
    /// This method is used to take a 3D point and, using a cylindrical map type, find where in the
    /// image it lies, in texels.
    /// </summary>
    /// <param name="point">The point to transform.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <returns>The X and Y coordinates within the image to use.</returns>
    private static (double, double) GetCylindricalImageLocationFor(
        Point point, double width, double height)
    {
        // Left unwrapped, as every place is; taken as a remainder, as it once was, a point below the
        // axis's nought gave a negative row and took the render down with it.  Counted down from the top,
        // as a picture's rows are and as the spherical map counts them: counted up, the picture stood on
        // its head.
        double y = height - point.Y * height;

        Vector vector = new Vector(point);
        double magnitude = vector.Magnitude;
        
        if (magnitude == 0)
            return (double.NaN, double.NaN);

        vector = vector.Unit;

        // Determine its angle from the point (1, 0, 0) in the x-z plane.
        double length = Math.Sqrt(vector.X * vector.X + vector.Z * vector.Z);
        double theta;

        if (length == 0)
            return (double.NaN, double.NaN);

        if (vector.Z == 0.0)
            theta = vector.X > 0 ? 0.0 : Math.PI;
        else
        {
            theta = Math.Acos(vector.X / length);

            if (vector.Z < 0.0)
                theta = TwoPi - theta;
        }

        theta /= TwoPi;  // This will be from 0 to 1

        return (theta * width, y);
    }

    /// <summary>
    /// This method is used to take a 3D point and, using a toroidal map type, find where in the
    /// image it lies, in texels.
    /// </summary>
    /// <param name="point">The point to transform.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <returns>The X and Y coordinates within the image to use.</returns>
    private static (double, double) GetToroidalImageLocationFor(
        Point point, double width, double height, double ring)
    {
        double length = Math.Sqrt(point.X * point.X + point.Z * point.Z);
        double theta;

        if (length == 0)
            return (double.NaN, double.NaN);

        if (point.Z == 0.0)
            theta = point.X > 0 ? 0.0 : Math.PI;
        else
        {
            theta = Math.Acos(point.X / length);

            if (point.Z < 0.0)
                theta = TwoPi - theta;
        }

        theta = 0 - theta;

        // The angle round the tube, about the middle of the tube where this point is, not about the torus's own
        // middle: measured from the axis, as it was, it pinched the picture to a band at one side of the ring and
        // a single row at the other.
        double outward = length - ring;

        length = Math.Sqrt(outward * outward + point.Y * point.Y);

        if (length == 0)
            return (double.NaN, double.NaN);

        double phi = Math.Acos(-outward / length);
        
        if (point.Y > 0)
            phi = TwoPi - phi;
        
        theta /= TwoPi;
        phi /= TwoPi;

        return (-theta * width, phi * height);
    }
}
