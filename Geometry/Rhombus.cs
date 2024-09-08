using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a rhombus.  It is defined by one point, and two side vectors
/// intiating from that point.
/// </summary>
public class Rhombus : Surface
{
    /// <summary>
    /// This property provides the "anchor" point of the rhombus.
    /// </summary>
    public Point Point
    {
        get => _point;
        set
        {
            _point = value;

            DefinitionChanged(value, _side1, _side2);
        }
    }

    /// <summary>
    /// This property provides the first side vector of the rhombus.
    /// </summary>
    public Vector Side1
    {
        get => _side1;
        set
        {
            _side1 = value;

            DefinitionChanged(_point, value, _side2);
        }
    }

    /// <summary>
    /// This property provides the second side vector of the rhombus.
    /// </summary>
    public Vector Side2
    {
        get => _side2;
        set
        {
            _side2 = value;

            DefinitionChanged(_point, _side1, value);
        }
    }

    private Point _point;
    private Vector _side1;
    private Vector _side2;
    private Vector _normal;
    private double _constantD;
    private Vector _constantW;

    /// <summary>
    /// This method is used to reset our control information when our point, or either of
    /// our side vectors change.  If any of the information is <c>null</c> (as will be
    /// during initial creation), we silently no-op.
    /// </summary>
    /// <param name="point">The anchor point of the rhombus.</param>
    /// <param name="side1">The first side of the rhombus.</param>
    /// <param name="side2">The second side of the rhombus.</param>
    private void DefinitionChanged(Point point, Vector side1, Vector side2)
    {
        if (point is not null && side1 is not null && side2 is not null)
        {
            Vector cross = side1.Cross(side2);

            _normal = cross.Unit;
            _constantD = _normal.Dot(point);
            _constantW = cross / cross.Dot(cross);
        }
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the triangle and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        double denominator = _normal.Dot(ray.Direction);
        
        if (denominator == 0)
            return;
        
        double t = (_constantD - _normal.Dot(ray.Origin)) / denominator;

        if (t >= 0)
        {
            Point p = ray.At(t);
            Vector vector = p - _point;
            double alpha = _constantW.Dot(vector.Cross(_side1));
            double beta = _constantW.Dot(_side2.Cross(vector));

            if (alpha is >= 0 and <= 1 && beta is >= 0 and <= 1)
                intersections.Add(new Intersection(this, t));
        }
    }

    /// <summary>
    /// This method returns the normal for the triangle.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will
    /// also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        return _normal;
    }
}
