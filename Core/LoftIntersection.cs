using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This class records a crossing with a <see cref="Loft"/>: which of its patches it was found on, and where in that
/// patch, or which of its flat ends; what its normal is worked out from.
/// </summary>
public class LoftIntersection : Intersection
{
    /// <summary>
    /// This property holds which patch the crossing lies on, or for one of the ends, minus one less the end's place.
    /// </summary>
    public int Patch { get; }

    /// <summary>
    /// This property holds how far along the patch's outline the crossing lies.
    /// </summary>
    public double U { get; }

    /// <summary>
    /// This property holds how far across the patch, from one section to the next.
    /// </summary>
    public double V { get; }

    public LoftIntersection(Surface surface, double distance, int patch, double u, double v)
        : base(surface, distance)
    {
        Patch = patch;
        U = u;
        V = v;
    }
}
