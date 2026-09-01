using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This class represents a crossing of a parametric surface, which carries whereabouts on the sheet
/// it happened as well as how far along the ray.
/// <para>
/// The sheet's normal is worked out from its two slopes at that place, and the place is not something
/// the point alone can be asked for -- a sheet may pass through one point of space at more than one
/// pair of parameters, a shell being exactly such a thing.  So the narrowing that found the crossing
/// records where it was, rather than leaving it to be searched for again.
/// </para>
/// </summary>
public class ParametricIntersection : Intersection
{
    /// <summary>
    /// This property holds how far along the sheet's first parameter the crossing lies.
    /// </summary>
    public double U { get; }

    /// <summary>
    /// This property holds how far along its second.
    /// </summary>
    public double V { get; }

    public ParametricIntersection(Surface surface, double distance, double u, double v)
        : base(surface, distance)
    {
        U = u;
        V = v;
    }
}
