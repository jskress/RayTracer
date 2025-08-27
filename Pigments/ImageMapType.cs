using RayTracer.Basics;

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
    /// This method is used to take a 3D point and, using the indicated image map type,
    /// convert it to a normalized x, y coordinate pair, each coordinate in the range of
    /// [0..1).
    /// </summary>
    /// <param name="imageMapType">The type of image map to use.</param>
    /// <param name="point">The point to transform.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of hte image.</param>
    /// <param name="once">A flag indicating whether the image may repeat.</param>
    /// <returns>The X and Y coordinates within the image to use.</returns>
    public static (double, double) GetImageLocationFor(
        this ImageMapType? imageMapType, Point point, double width, double height,
        bool once)
    {
        return imageMapType switch
        {
            ImageMapType.Planar => GetPlanarImageLocationFor(point, width, height, once),
            ImageMapType.Spherical => GetSphericalImageLocationFor(point, width, height),
            ImageMapType.Cylindrical => GetCylindricalImageLocationFor(point, width, height, once),
            ImageMapType.Toroidal => GetToroidalImageLocationFor(point, width, height),
            _ => throw new ArgumentOutOfRangeException(nameof(imageMapType), imageMapType, null)
        };
    }

    /// <summary>
    /// This method is used to take a 3D point and, using a planar map type, convert it
    /// to a normalized x, y coordinate pair, each coordinate in the range of [0..1).
    /// </summary>
    /// <param name="point">The point to transform.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of hte image.</param>
    /// <param name="once">A flag indicating whether the image may repeat.</param>
    /// <returns>The X and Y coordinates within the image to use.</returns>
    private static (double, double) GetPlanarImageLocationFor(
        Point point, double width, double height, bool once)
    {
        if (once && point.X is < 0 or > 1 || point.Z is < 0 or > 1)
            return (double.NaN, double.NaN);

        double x = point.X * width % width;
        double y = point.Z * height % height;

        return (x, y);
    }

    /// <summary>
    /// This method is used to take a 3D point and, using a spherical map type, convert it
    /// to a normalized x, y coordinate pair, each coordinate in the range of [0..1).
    /// </summary>
    /// <param name="point">The point to transform.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of hte image.</param>
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
    /// This method is used to take a 3D point and, using a cylindrical map type, convert it
    /// to a normalized x, y coordinate pair, each coordinate in the range of [0..1).
    /// </summary>
    /// <param name="point">The point to transform.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of hte image.</param>
    /// <param name="once">A flag indicating whether the image may repeat.</param>
    /// <returns>The X and Y coordinates within the image to use.</returns>
    private static (double, double) GetCylindricalImageLocationFor(
        Point point, double width, double height, bool once)
    {
        if (once && point.Y is < 0 or > 1)
            return (double.NaN, double.NaN);

        double y = point.Y * height % height;

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
    /// This method is used to take a 3D point and, using a toroidal map type, convert it
    /// to a normalized x, y coordinate pair, each coordinate in the range of [0..1).
    /// </summary>
    /// <param name="point">The point to transform.</param>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of hte image.</param>
    /// <returns>The X and Y coordinates within the image to use.</returns>
    private static (double, double) GetToroidalImageLocationFor(
        Point point, double width, double height)
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
        length = Math.Sqrt(point.X * point.X + point.Y * point.Y);

        double phi = Math.Acos(-point.X / length);
        
        if (point.Y > 0)
            phi = TwoPi - phi;
        
        theta /= TwoPi;
        phi /= TwoPi;

        return (-theta * width, phi * height);
    }
}
