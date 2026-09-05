using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class renders one segment of a tapered extrusion's outline: the wall that segment sweeps
/// out as the outline is carried up and scaled at the same time.
/// <para>
/// It is the tapered counterpart of <see cref="ExtrusionPathSurface"/>, and solves the same 2D
/// problem in the same way.  Only two things differ.  The distance along the ray comes from the
/// <see cref="TaperedProjection"/> rather than from following the faster of two axes, because the
/// projection here is a central one; and the normal leans out of the horizontal, because the wall
/// does.
/// </para>
/// </summary>
internal class TaperedExtrusionPathSurface
{
    private readonly IPathSegment _segment;
    private readonly double _minimumY;
    private readonly double _maximumY;
    private readonly double _taperRate;

    internal TaperedExtrusionPathSurface(
        IPathSegment segment, double minimumY, double maximumY, double taperRate)
    {
        _segment = segment;
        _minimumY = minimumY;
        _maximumY = maximumY;
        _taperRate = taperRate;
    }

    /// <summary>
    /// This method is used to locate the intersection points, if any, where the given ray meets
    /// this wall.
    /// </summary>
    /// <param name="surface">The extrusion the intersections belong to.</param>
    /// <param name="ray">The 3D ray we started with.</param>
    /// <param name="projection">The ray, carried into the plane of the outline.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    internal void AddIntersections(
        Surface surface, Ray ray, TaperedProjection projection, List<Intersection> intersections)
    {
        foreach (TwoDIntersection intersection in _segment.GetIntersections(projection.Line))
        {
            double distance = projection.DistanceTo(intersection.Point);

            if (double.IsNaN(distance))
                continue;

            double y = ray.Origin.Y + distance * ray.Direction.Y;

            if (y < _minimumY || y > _maximumY)
                continue;

            intersections.Add(new PrecomputedNormalIntersection(
                surface, distance, NormalAt(intersection)));
        }
    }

    /// <summary>
    /// This method works out the normal to the wall at a crossing.
    /// <para>
    /// The wall is ruled: one way along it follows the outline, the other runs from the apex out
    /// through the point, and the normal is across both.  Taking that cross product and dividing
    /// out the scale leaves the outline's own 2D normal lying flat, with a rise added to it of
    /// the taper's rate times how far the outline's normal reaches along the point itself.  So a
    /// wall that draws in as it rises leans its normal upward, one that spreads leans it down,
    /// and a rate of nought leaves it horizontal, which is the untapered case.
    /// </para>
    /// </summary>
    /// <param name="intersection">The crossing, in the plane of the unscaled outline.</param>
    /// <returns>The normal to the wall there.</returns>
    private Vector NormalAt(TwoDIntersection intersection)
    {
        TwoDVector normal = intersection.TwoDNormal;
        TwoDPoint point = intersection.Point;

        return new Vector(
            normal.X,
            -_taperRate * (normal.X * point.X + normal.Y * point.Y),
            normal.Y).Unit;
    }
}
