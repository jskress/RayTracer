using RayTracer.Core;
using RayTracer.Geometry;

namespace RayTracer.Extensions;

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
    /// <param name="environment">The index of refraction of the space outside every object, which is
    /// what a ray is travelling through whenever it is inside none of them.</param>
    /// <returns>The entrance and exit indices of refraction.</returns>
    public static (double N1, double N2) FindIndicesOfRefraction(
        this List<Intersection> intersections, Intersection hit, double environment = 1)
    {
        // **A shape and the place it is standing in**, because a shared shape gives the same surface
        // for every instance of it: a ray entering one glass ball and then another would otherwise
        // read as entering and *leaving* the one ball, and come out the far side unrefracted.
        List<(Surface Surface, Surface Portal)> containers = [];
        double n1 = 0;
        double n2 = 0;

        foreach (Intersection intersection in intersections)
        {
            if (intersection == hit)
                n1 = containers.IsEmpty()
                    ? environment
                    : (containers.Last().Surface.Material ?? Material.Default).Interior.IndexOfRefraction;

            (Surface, Surface) inside = (intersection.Surface, intersection.Portal);

            if (containers.Contains(inside))
                containers.Remove(inside);
            else
                containers.Add(inside);

            if (intersection == hit)
            {
                n2 = containers.IsEmpty()
                    ? environment
                    : (containers.Last().Surface.Material ?? Material.Default).Interior.IndexOfRefraction;

                break;
            }
        }

        return (n1, n2);
    }
}
