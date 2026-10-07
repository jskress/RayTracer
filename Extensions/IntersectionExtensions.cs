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
        (Interior before, Interior after) = intersections.FindInteriors(hit);

        return (before?.IndexOfRefraction ?? environment, after?.IndexOfRefraction ?? environment);
    }

    /// <summary>
    /// This method works out what the ray was travelling through before the given hit and what it
    /// travels through after it: the interior of the innermost thing it is inside on each side, or
    /// <c>null</c> where it is inside nothing and so in the surroundings.
    /// </summary>
    /// <param name="intersections">The list of intersections to work with.</param>
    /// <param name="hit">The current "hit" intersection.</param>
    /// <returns>The interiors before and after the hit.</returns>
    public static (Interior Before, Interior After) FindInteriors(
        this List<Intersection> intersections, Intersection hit)
    {
        // **A shape and the place it is standing in**, because a shared shape gives the same surface
        // for every instance of it: a ray entering one glass ball and then another would otherwise
        // read as entering and *leaving* the one ball, and come out the far side unrefracted.
        List<(Surface Surface, Surface Portal)> containers = [];
        Interior before = null;
        Interior after = null;

        foreach (Intersection intersection in intersections)
        {
            if (intersection == hit)
                before = containers.IsEmpty() ? null : InteriorOf(containers.Last().Surface);

            (Surface, Surface) inside = (intersection.Surface, intersection.Portal);

            if (containers.Contains(inside))
                containers.Remove(inside);
            else
                containers.Add(inside);

            if (intersection == hit)
            {
                after = containers.IsEmpty() ? null : InteriorOf(containers.Last().Surface);

                break;
            }
        }

        return (before, after);
    }

    /// <summary>
    /// This method returns what a surface is filled with.
    /// </summary>
    private static Interior InteriorOf(Surface surface)
    {
        return (surface.Material ?? Material.Default).Interior;
    }
}
