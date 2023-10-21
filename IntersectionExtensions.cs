using RayTracer.Core;

namespace RayTracer;

/// <summary>
/// This class provides extension methods relating to intersections.
/// </summary>
public static class IntersectionExtensions
{
    /// <summary>
    /// This method returns the intersection of a list that should be considered a hit.
    /// </summary>
    /// <param name="intersections">The list of intersections to examine.</param>
    /// <returns>The "hit" intersection.</returns>
    public static Intersection? Hit(this List<Intersection> intersections)
    {
        intersections.Sort();

        return intersections.FirstOrDefault(intersection => intersection.Distance >= 0);
    }
}
