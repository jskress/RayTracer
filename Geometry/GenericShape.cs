using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a generic 2D shape: an arbitrary closed path (straight, quadratic,
/// and cubic segments, exactly like the paths <see cref="Extrusion"/> and <see cref="Lathe"/>
/// use) lying flat in the surface's own local X/Y plane, at Z = 0.  Unlike those two, there's
/// no extrusion or revolution axis to reserve, so the shape is placed and oriented purely
/// through the usual <see cref="Surface.Transform"/> -- the same way <see cref="Sphere"/> or
/// <see cref="Cube"/> are canonically defined in local space and moved into place.
/// </summary>
public class GenericShape : FlatSurface
{
    /// <summary>
    /// This attribute holds the path that represents the outline of the shape in the local
    /// X/Y plane.
    /// </summary>
    public GeneralPath Path { get; set; }

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to
    /// perform any expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        Normal = new Vector(0, 0, 1);
        PlaneConstant = 0;
    }

    /// <summary>
    /// This method is used to produce a default bounding box for this shape.
    /// </summary>
    /// <returns>A default bounding box, if any, for the surface.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        return new BoundingBox()
            .Add(new Point(Path.MinX, Path.MinY, 0))
            .Add(new Point(Path.MaxX, Path.MaxY, 0));
    }

    /// <summary>
    /// This method is used to test whether the given point, already known to lie on the
    /// shape's plane, actually lies within its path.
    /// </summary>
    /// <param name="point">The point, on the shape's plane, to test.</param>
    /// <param name="distance">The distance along the ray where the point lies.</param>
    /// <returns>The appropriate intersection object, or <c>null</c> if the point is outside
    /// the path.</returns>
    protected override Intersection TryCreateIntersection(Point point, double distance)
    {
        TwoDPoint twoDPoint = new (point.X, point.Y);

        return Path.Contains(twoDPoint) ? new Intersection(this, distance) : null;
    }
}
