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
        (Surface before, Surface after, Portals beforeThrough, Portals afterThrough) =
            intersections.FindContainers(hit);

        return (
            before is null ? null : InteriorOf(before, beforeThrough),
            after is null ? null : InteriorOf(after, afterThrough));
    }

    /// <summary>
    /// This method works out what the ray was inside before the given hit and what it is inside after
    /// it: the innermost thing on each side, or <c>null</c> where it is inside nothing and so in the
    /// surroundings.  Each comes with the instances it was met through, since anything that asks about
    /// its shape -- where a medium filling it ends -- must find it where it stands, and a shared shape
    /// is filled with what the holder it was met through dresses it in.
    /// </summary>
    /// <param name="intersections">The list of intersections to work with.</param>
    /// <param name="hit">The current "hit" intersection.</param>
    /// <returns>The surfaces the ray is inside before and after the hit, and the instances each was met
    /// through.</returns>
    public static (Surface Before, Surface After, Portals BeforeThrough, Portals AfterThrough) FindContainers(
        this List<Intersection> intersections, Intersection hit)
    {
        // **A shape and the place it is standing in**, because a shared shape gives the same surface
        // for every instance of it: a ray entering one glass ball and then another would otherwise
        // read as entering and *leaving* the one ball, and come out the far side unrefracted.
        List<(Surface Surface, Portals Portals)> containers = [];
        Surface before = null;
        Surface after = null;
        Portals beforeThrough = default;
        Portals afterThrough = default;

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

                (after, afterThrough) = (before, beforeThrough);

                break;
            }

            // **A height field's triangles are its skin, not things of their own.**  A closed field is gone into and
            // out of as the one solid it is, whichever of its triangles, walls or underside a ray crosses, and an
            // open one is a sheet, never gone into.  Taken triangle by triangle, a field the ray had crossed behind
            // where it set off -- a look up from over the ground, followed back past the eye -- was gone into and
            // never left, and everything the look met after it was taken to stand inside that one triangle rather
            // than in the haze round both: until 2026-11-05 mountains over a valley came through a haze undimmed.
            Surface surface = intersection.Surface;

            // Its walls are a group of triangles each, a step further down.
            if ((surface.Parent as HeightField ?? surface.Parent?.Parent as HeightField) is { } field)
            {
                if (!field.Closed)
                {
                    if (intersection != hit)
                        continue;

                    (after, afterThrough) = (before, beforeThrough);

                    break;
                }

                surface = field;
            }

            (Surface, Portals) inside = (surface, intersection.Portals);

            if (containers.Contains(inside))
                containers.Remove(inside);
            else
                containers.Add(inside);

            if (intersection == hit)
            {
                if (!containers.IsEmpty())
                    (after, afterThrough) = containers.Last();

                break;
            }
        }

        return (before, after, beforeThrough, afterThrough);
    }

    /// <summary>
    /// This method returns what a surface is filled with where it stands: a shape shared between holders
    /// dressed differently is filled with what the one it was met through gives it.
    /// </summary>
    /// <param name="surface">The surface to look inside.</param>
    /// <param name="portals">The instances the surface was met through.</param>
    internal static Interior InteriorOf(Surface surface, Portals portals)
    {
        return (surface.MaterialThrough(portals) ?? Material.Default).Interior;
    }
}
