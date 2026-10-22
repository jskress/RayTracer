using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class answers whether a place is inside a closed surface, for the things that fill a surface with
/// something of their own: the points a grown tree grows toward, the seeds a fracture breaks a solid around.
/// </summary>
internal static class Containment
{
    // The three ways looked out from a place to tell whether it is inside a surface, the place inside if most of
    // them say so: none of them along an axis, so that none runs along the edge or the seam of anything ordinary.
    private static readonly Vector[] Looks =
    [
        new Vector(0.267, 0.534, 0.802).Unit, new Vector(-0.873, 0.218, -0.436).Unit,
        new Vector(0.408, -0.816, 0.408).Unit
    ];

    /// <summary>
    /// This method reports whether a place, in the space a surface stands in, is inside it: inside any of a group's
    /// surfaces, inside what an instance shares, and otherwise inside by most of three looks out crossing it an odd
    /// number of times.
    /// <para>
    /// A group is asked part by part, since its parts may overlap -- a crown of three spheres -- and a look out
    /// through two of them crosses an even number of times from a place inside both.
    /// </para>
    /// </summary>
    internal static bool Holds(Surface surface, Point place)
    {
        switch (surface)
        {
            case Group group:
            {
                Point inside = group.InverseTransformAt(0) * place;

                return group.Surfaces.Any(child => Holds(child, inside));
            }
            case Instance instance:
                return Holds(instance.Prototype, instance.InverseTransformAt(0) * place);
        }

        int odd = 0;

        foreach (Vector look in Looks)
        {
            List<Intersection> crossings = [];

            surface.Intersect(new Ray(place, look), crossings);

            if (crossings.Count(crossing => crossing.Distance > 0) % 2 == 1)
                odd++;
        }

        return odd >= 2;
    }
}
