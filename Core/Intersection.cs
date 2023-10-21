using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This class represents an intersection between a ray and a piece of geometry.
/// </summary>
public sealed class Intersection : IComparable<Intersection>
{
    /// <summary>
    /// This holds the geometry that was intersected.
    /// </summary>
    public Surface Surface { get; }

    /// <summary>
    /// This holds the distance along the ray where the intersection happened.
    /// </summary>
    public double Distance { get; }

    /// <summary>
    /// This holds the point of intersection.
    /// </summary>
    public Point Point { get; private set; }

    /// <summary>
    /// This holds the point of intersection, slightly above the actual surface.
    /// </summary>
    public Point OverPoint { get; private set; }

    /// <summary>
    /// This property holds the eye vector at this point of intersection.
    /// </summary>
    public Vector Eye { get; private set; }

    /// <summary>
    /// This property holds the surface normal at this point of intersection.
    /// </summary>
    public Vector Normal { get; private set; }

    /// <summary>
    /// This property notes whether the hit occurred inside the surface or out.
    /// </summary>
    public bool Inside { get; private set; }

    public Intersection(Surface surface, double distance)
    {
        Surface = surface;
        Distance = distance;
        Point = null!;
        Eye = null!;
        Normal = null!;
        Inside = false;
    }

    /// <summary>
    /// This method is responsible for precomputing things once we know that we are
    /// the intersection of interest.
    /// </summary>
    /// <param name="ray">The ray to use in computing things.</param>
    public void PrepareUsing(Ray ray)
    {
        Point = ray.At(Distance);
        Eye = -ray.Direction;
        Normal = Surface.NormaAt(Point);
        Inside = Normal.Dot(Eye) < 0;
        OverPoint = Point + Normal * DoubleExtensions.Epsilon;

        if (Inside)
            Normal = -Normal;
    }

    /// <summary>
    /// This method is used to compare this intersection to another for the
    /// purpose of sorting.
    /// </summary>
    /// <param name="other">The other intersection to compare to.</param>
    /// <returns>The appropriate comparison of the intersections' distances.</returns>
    public int CompareTo(Intersection? other)
    {
        if (ReferenceEquals(this, other))
            return 0;

        return ReferenceEquals(null, other)
            ? 1
            : Distance.CompareTo(other.Distance);
    }
}
