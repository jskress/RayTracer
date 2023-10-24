using RayTracer.Core;
using RayTracer.Geometry;

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
    public static Intersection Hit(this List<Intersection> intersections)
    {
        intersections.Sort();

        return intersections.FirstOrDefault(intersection => intersection.Distance >= 0);
    }

    /// <summary>
    /// This method is used to determine the entrance and exit indices of refraction for
    /// the given hit.
    /// </summary>
    /// <param name="intersections">The list of intersections to work with.</param>
    /// <param name="hit">The current "hit" intersection.</param>
    /// <returns>The entrance and exit indices of refraction.</returns>
    public static (double N1, double N2) FindIndicesOfRefraction(
        this List<Intersection> intersections, Intersection hit)
    {
        List<Surface> containers = new ();
        double n1 = 0;
        double n2 = 0;

        foreach (Intersection intersection in intersections)
        {
            if (intersection == hit)
                n1 = containers.Count == 0 ? 1 : containers.Last().Material.IndexOfRefraction;

            if (containers.Contains(intersection.Surface))
                containers.Remove(intersection.Surface);
            else
                containers.Add(intersection.Surface);

            if (intersection == hit)
            {
                n2 = containers.Count == 0 ? 1 : containers.Last().Material.IndexOfRefraction;

                break;
            }
        }

        return (n1, n2);
    }
}
