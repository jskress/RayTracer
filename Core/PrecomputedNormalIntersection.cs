using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This class represents an intersection between a ray and a piece of geometry where the
/// normal will have been precomputed, either once, ahead of time, or, perhaps, as a side
/// effect of the ray/intersection test.
/// </summary>
public class PrecomputedNormalIntersection : Intersection
{
    /// <summary>
    /// This holds the normal that was precalculated.
    /// </summary>
    public Vector PrecomputedNormal { get; }

    public PrecomputedNormalIntersection(Surface surface, double distance, Vector normal)
        : base(surface, distance)
    {
        PrecomputedNormal = normal;
    }
}
