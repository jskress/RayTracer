using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This class sets the lights standing in groups out in the world, once the scene is built and ready.
/// <para>
/// **A light can stand on something** -- a bulb in a lamp, a beam in a lamp's head -- written in the
/// group it stands in, in that group's own space.  So it goes wherever the group goes and turns and
/// grows with it: set on a table by `atop`, laid along a street by a field, carried by whatever holds
/// the group.  None of the shading knows any of this.  Each light is carried out into the world here
/// and joins the scene as an ordinary light, so a scene with no light standing on anything renders
/// exactly as it did.
/// </para>
/// <para>
/// **Once for every place the group stands.**  A lamp a field lays along a street is built once and
/// stood in every place by an instance, and its one light becomes one light at every lamp, carried out
/// through each lamp's instances in turn.
/// </para>
/// <para>
/// **Once for every instant, if the group moves.**  Where a group moves while the shutter is open, its
/// light is set out where the group stands at each instant the camera looks at, and a ray is lit by it
/// where it stood at the ray's own instant (see <see cref="Light.At"/>), so a moving lamp's light and
/// shadows blur with it.
/// </para>
/// <para>
/// **What closes round a light does not shadow it.**  A bulb drawn as a glowing ball would otherwise
/// put the whole room in its shadow; light leaves a real one through its glass.  So the closed
/// surfaces of the light's own group that surround it -- worked out by looking out from the light in
/// several ways and keeping only what every look crosses an odd number of times -- are left out of
/// its shadows (see <see cref="Light.Housing"/>).  A shade open at its ends is crossed by no look
/// leaving through the opening, so it shadows the light as anything else does: that is the pool of
/// light a lamp throws.
/// </para>
/// </summary>
internal static class LightPlacer
{
    /// <summary>
    /// This field holds the ways looked out from a light to find what closes round it: straight up and
    /// straight down, out through the ends of a shade, and five more spread unevenly between, so that
    /// none of them runs along an edge or a seam of anything ordinary.
    /// </summary>
    private static readonly Vector[] Looks =
    [
        new (0, 1, 0), new (0, -1, 0),
        new Vector(0.267, 0.534, 0.802).Unit, new Vector(-0.873, 0.218, -0.436).Unit,
        new Vector(0.408, -0.816, 0.408).Unit, new Vector(-0.322, -0.115, 0.940).Unit,
        new Vector(0.913, 0.331, -0.238).Unit
    ];

    /// <summary>
    /// This method sets every light standing in a group out into the scene.
    /// </summary>
    /// <param name="scene">The scene, its surfaces prepared, settled and arranged.</param>
    /// <param name="instants">How many instants of the shutter's opening the camera looks at.</param>
    public static void Place(Scene scene, int instants)
    {
        List<(Light Light, Group Group, Portals Portals)> standing = [];

        Find(scene.Surfaces, [], standing, []);

        foreach ((Light light, Group group, Portals portals) in standing)
        {
            Light placed = light.PlacedBy(point => group.SurfaceToWorld(point, 0, portals));

            placed.Housing = HousingOf(scene, (PointLight) placed, group);

            if (Moves(group, portals, instants))
            {
                Light[] atTimes = new Light[instants];

                atTimes[0] = placed;

                for (int instant = 1; instant < instants; instant++)
                {
                    int now = instant;

                    atTimes[instant] = light.PlacedBy(point => group.SurfaceToWorld(point, now, portals));
                    atTimes[instant].Housing = placed.Housing;
                }

                placed.MovesThrough(atTimes);
            }

            scene.Lights.Add(placed);
        }
    }

    /// <summary>
    /// This method finds every light standing in a group, with the group and the instances it stands
    /// behind, walking down through groups, combinations and shared shapes.
    /// <para>
    /// **Only into what holds a light.**  A shared shape is walked again at every place it stands, since
    /// a light in it is a light at each, and a wood of a hundred and forty-five thousand trees, each
    /// holding its leaves by instance, is tens of millions of places -- a tenth again on the render, to
    /// find nothing.  So whether a shape holds a light anywhere inside it is worked out once for each
    /// shape and remembered, and the walk goes only where one is.
    /// </para>
    /// </summary>
    /// <param name="surfaces">The surfaces to look through.</param>
    /// <param name="through">The instances passed through to get here, outermost first.</param>
    /// <param name="standing">Where to put what is found.</param>
    /// <param name="holding">Whether each shape asked about so far holds a light.</param>
    private static void Find(
        IEnumerable<Surface> surfaces, List<Surface> through, List<(Light, Group, Portals)> standing,
        Dictionary<Surface, bool> holding)
    {
        foreach (Surface surface in surfaces)
        {
            if (!HoldsALight(surface, holding))
                continue;

            switch (surface)
            {
                case Group group:
                    if (group.Lights.Count > 0)
                    {
                        Portals portals = PortalsOf(through);

                        foreach (Light light in group.Lights)
                            standing.Add((light, group, portals));
                    }

                    Find(group.Surfaces, through, standing, holding);
                    break;
                case CsgSurface csg:
                    Find([csg.Left, csg.Right], through, standing, holding);
                    break;
                case Instance instance:
                    through.Add(instance);
                    Find([instance.Prototype], through, standing, holding);
                    through.RemoveAt(through.Count - 1);
                    break;
            }
        }
    }

    /// <summary>
    /// This method reports whether a light stands anywhere in a surface, remembering the answer for
    /// every shape it asks about.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <param name="holding">The answers so far.</param>
    /// <returns>Whether a light stands in it.</returns>
    private static bool HoldsALight(Surface surface, Dictionary<Surface, bool> holding)
    {
        if (holding.TryGetValue(surface, out bool holds))
            return holds;

        holds = surface switch
        {
            Group group => group.Lights.Count > 0 || group.Surfaces.Any(child => HoldsALight(child, holding)),
            CsgSurface csg => HoldsALight(csg.Left, holding) || HoldsALight(csg.Right, holding),
            Instance instance => HoldsALight(instance.Prototype, holding),
            _ => false
        };
        holding[surface] = holds;

        return holds;
    }

    /// <summary>
    /// This method returns the instances passed through, as a hit found through them would carry them:
    /// the innermost first.
    /// </summary>
    /// <param name="through">The instances passed through, outermost first.</param>
    /// <returns>The instances, as a hit carries them.</returns>
    private static Portals PortalsOf(List<Surface> through)
    {
        Portals portals = default;

        for (int index = through.Count - 1; index >= 0; index--)
            portals = portals.And(through[index]);

        return portals;
    }

    /// <summary>
    /// This method reports whether a group stands anywhere else at any instant than it does at the
    /// first, by where it carries a point and three directions out from it.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="portals">The instances it stands behind.</param>
    /// <param name="instants">How many instants the camera looks at.</param>
    /// <returns>Whether it moves.</returns>
    private static bool Moves(Group group, Portals portals, int instants)
    {
        Point[] marks = [Point.Zero, new Point(1, 0, 0), new Point(0, 1, 0), new Point(0, 0, 1)];
        Point[] first = marks.Select(mark => group.SurfaceToWorld(mark, 0, portals)).ToArray();

        for (int instant = 1; instant < instants; instant++)
        {
            for (int index = 0; index < marks.Length; index++)
            {
                if (!group.SurfaceToWorld(marks[index], instant, portals).Matches(first[index]))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// This method works out what closes round a light: the closed surfaces of its own group crossed an
    /// odd number of times by every look out from it.  Each is kept with the instances it was found
    /// through, so that the bulb of one lamp in a street is not taken for the bulb of the next.
    /// </summary>
    /// <param name="scene">The scene, arranged for searching.</param>
    /// <param name="light">The light, placed.</param>
    /// <param name="group">The group it stands in.</param>
    /// <returns>What closes round it, or <c>null</c> for nothing.</returns>
    private static IReadOnlySet<(Surface, Portals)> HousingOf(Scene scene, PointLight light, Group group)
    {
        HashSet<Surface> own = new SurfaceIterator(group.Surfaces).Surfaces.ToHashSet();
        HashSet<(Surface, Portals)> housing = null;

        if (own.Count == 0)
            return null;

        foreach (Vector look in Looks)
        {
            Dictionary<(Surface, Portals), int> crossed = [];

            foreach (Intersection crossing in scene.Intersect(new Ray(light.Location, look)))
            {
                if (crossing.Distance <= 0 || crossing is HairIntersection ||
                    crossing.Surface.IsASheet || !own.Contains(crossing.Surface))
                    continue;

                (Surface, Portals) key = (crossing.Surface, crossing.Portals);

                crossed[key] = crossed.GetValueOrDefault(key) + 1;
            }

            HashSet<(Surface, Portals)> odd = crossed
                .Where(pair => pair.Value % 2 == 1)
                .Select(pair => pair.Key)
                .ToHashSet();

            if (housing is null)
                housing = odd;
            else
                housing.IntersectWith(odd);

            if (housing.Count == 0)
                return null;
        }

        return housing;
    }
}
