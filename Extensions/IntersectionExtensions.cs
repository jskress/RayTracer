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
        (Surface before, Surface after, _) = intersections.FindContainers(hit);

        return (before is null ? null : InteriorOf(before), after is null ? null : InteriorOf(after));
    }

    /// <summary>
    /// This method works out what the ray was inside before the given hit and what it is inside after
    /// it: the innermost thing on each side, or <c>null</c> where it is inside nothing and so in the
    /// surroundings.  The one before comes with the instances it was met through, since anything that
    /// asks about its shape -- where a medium filling it ends -- must find it where it stands.
    /// </summary>
    /// <param name="intersections">The list of intersections to work with.</param>
    /// <param name="hit">The current "hit" intersection.</param>
    /// <returns>The surfaces the ray is inside before and after the hit, and the instances the one
    /// before was met through.</returns>
    public static (Surface Before, Surface After, Portals BeforeThrough) FindContainers(
        this List<Intersection> intersections, Intersection hit)
    {
        // **A shape and the place it is standing in**, because a shared shape gives the same surface
        // for every instance of it: a ray entering one glass ball and then another would otherwise
        // read as entering and *leaving* the one ball, and come out the far side unrefracted.
        List<(Surface Surface, Portals Portals)> containers = [];
        Surface before = null;
        Surface after = null;
        Portals beforeThrough = default;

        foreach (Intersection intersection in intersections)
        {
            if (intersection == hit && !containers.IsEmpty())
                (before, beforeThrough) = containers.Last();

            // A strand is crossed once, as a ribbon, so it is never gone into: a ray passing one is in
            // whatever it was in before, and stays there.
            if (intersection.Surface is StrandSurface)
            {
                if (intersection != hit)
                    continue;

                after = before;

                break;
            }

            (Surface, Portals) inside = (intersection.Surface, intersection.Portals);

            if (containers.Contains(inside))
                containers.Remove(inside);
            else
                containers.Add(inside);

            if (intersection == hit)
            {
                after = containers.IsEmpty() ? null : containers.Last().Surface;

                break;
            }
        }

        return (before, after, beforeThrough);
    }

    /// <summary>
    /// This method returns what a surface is filled with.
    /// </summary>
    internal static Interior InteriorOf(Surface surface)
    {
        return (surface.Material ?? Material.Default).Interior;
    }
}
