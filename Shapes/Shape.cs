using RayTracer.Core;
using RayTracer.Graphics;

namespace RayTracer.Shapes;

/// <summary>
/// This is the base class for all shapes.
/// </summary>
public abstract class Shape : Entity
{
    protected Shape(Point location) : base(location) {}

    /// <summary>
    /// This method must be provided by subclasses to determine whether the given
    /// ray intersects the shape.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="interval">The interval of acceptable values for distance.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public abstract Intersection? FindHit(Ray ray, Interval interval);
}
