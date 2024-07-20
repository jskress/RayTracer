using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This class represents an intersection between a ray and a piece of geometry.
/// </summary>
public class Intersection : IComparable<Intersection>
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
    /// This holds the point of intersection, slightly below the actual surface.
    /// </summary>
    public Point UnderPoint { get; private set; }

    /// <summary>
    /// This property holds the eye vector at this point of intersection.
    /// </summary>
    public Vector Eye { get; private set; }

    /// <summary>
    /// This property notes whether the intersection information needs to be flipped
    /// inside for outside.
    /// </summary>
    public bool ShouldFlipInsideForOut { get; set; }

    /// <summary>
    /// This property holds the surface normal at this point of intersection.
    /// </summary>
    public Vector Normal { get; private set; }

    /// <summary>
    /// This property holds the reflection vector at the point of intersection.
    /// </summary>
    public Vector Reflect { get; private set; }

    /// <summary>
    /// This property notes whether the hit occurred inside the surface or out.
    /// </summary>
    public bool Inside { get; private set; }

    /// <summary>
    /// This holds the index of refraction of where the ray came from.
    /// </summary>
    public double N1 { get; set; }

    /// <summary>
    /// This holds the index of refraction of the surface being entered.
    /// </summary>
    public double N2 { get; set; }

    /// <summary>
    /// This property exposes the reflectance for this intersection.
    /// </summary>
    public double Reflectance => GetReflectance();

    public Intersection(Surface surface, double distance)
    {
        Surface = surface;
        Distance = distance;
        Point = null!;
        OverPoint = null!;
        UnderPoint = null!;
        Eye = null!;
        Normal = null!;
        Reflect = null!;
        Inside = false;
    }

    /// <summary>
    /// This method is responsible for precomputing things once we know that we are
    /// the intersection of interest.
    /// </summary>
    /// <param name="ray">The ray to use in computing things.</param>
    /// <param name="intersections">The full list of intersections involved.</param>
    public void PrepareUsing(Ray ray, List<Intersection> intersections)
    {
        Point = ray.At(Distance);
        Eye = -ray.Direction;
        Normal = Surface.NormaAt(Point, this);
        Inside = Normal.Dot(Eye) < 0;

        if (ShouldFlipInsideForOut)
        {
            Normal = -Normal;
            Inside = !Inside;
        }

        Vector adjustment = Normal * DoubleExtensions.Epsilon;

        OverPoint = Point + adjustment;
        UnderPoint = Point - adjustment;
        Reflect = ray.Direction.Reflect(Normal);

        (N1, N2) = intersections.FindIndicesOfRefraction(this);

        if (Inside)
            Normal = -Normal;
    }

    /// <summary>
    /// This method returns the reflectance for this intersection.  It assumes that the
    /// <c>PrepareUsing()</c> method has already been called.
    /// </summary>
    /// <returns>The reflectance for the intersection.</returns>
    private double GetReflectance()
    {
        double cos = Eye.Dot(Normal);

        if (N1 > N2)
        {
            double n = N1 / N2;
            double sin2T = n * n * (1 - cos * cos);

            if (sin2T > 1.0)
                return 1;

            cos = Math.Sqrt(1 - sin2T);
        }

        double factor = (N1 - N2) / (N1 + N2);
        double r0 = factor * factor;

        return r0 + (1 - r0) * Math.Pow(1 - cos, 5);
    }

    /// <summary>
    /// This method is used to compare this intersection to another for the
    /// purpose of sorting.
    /// </summary>
    /// <param name="other">The other intersection to compare to.</param>
    /// <returns>The appropriate comparison of the intersections' distances.</returns>
    public int CompareTo(Intersection other)
    {
        if (ReferenceEquals(this, other))
            return 0;

        return ReferenceEquals(null, other)
            ? 1
            : Distance.CompareTo(other.Distance);
    }
}
