using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Terms;

/// <summary>
/// This class holds the functions a scene may ask of a spline: how long it is, and where along it a share of that
/// length lies and which way it runs there -- what a camera riding a spline, or watching something that does,
/// needs to know.  A share past the end of a loop begins round it again; past the end of any other holds there.
/// </summary>
public static class SplineFunctions
{
    /// <summary>
    /// This method returns a spline's length.
    /// </summary>
    [Function("length")]
    public static double Length(Spline spline) => spline.Measure.Length;

    /// <summary>
    /// This method returns the place a share of a spline's length along it.
    /// </summary>
    [Function("pointAlong")]
    public static Point PointAlong(Spline spline, double share) => spline.Measure.PointAt(share * spline.Measure.Length);

    /// <summary>
    /// This method returns the way a spline runs a share of its length along it, of unit length.
    /// </summary>
    [Function("headingAlong")]
    public static Vector HeadingAlong(Spline spline, double share) =>
        spline.Measure.HeadingAt(share * spline.Measure.Length);
}
