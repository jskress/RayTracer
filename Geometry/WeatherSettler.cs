using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class hands the weather a group or object says it has been out in down to every part of it, before
/// rendering: each part takes its own, or else the nearest round it.  A shared shape is settled once, by what
/// it says within itself; what is said of an instance of it is the instance's, and is found through the
/// instance at the hit, so one shape may stand clean in one place and weathered in another.
/// </summary>
internal static class WeatherSettler
{
    /// <summary>
    /// This method settles the weather of every surface given and everything inside them.
    /// </summary>
    /// <param name="surfaces">The scene's surfaces.</param>
    /// <returns>Every weather found, with the first surface it was said on, so that what each bares can be
    /// readied as any material is.</returns>
    internal static Dictionary<Weather, Surface> Settle(IEnumerable<Surface> surfaces)
    {
        HashSet<Surface> shared = [];
        Dictionary<Weather, Surface> found = [];

        foreach (Surface surface in surfaces)
            Settle(surface, null, shared, found);

        return found;
    }

    private static void Settle(Surface surface, Weather around, HashSet<Surface> shared, Dictionary<Weather, Surface> found)
    {
        Weather weather = surface.Weather ?? around;

        if (surface.Weather is not null)
        {
            found.TryAdd(surface.Weather, surface);
            surface.Weather.Owner ??= surface;
        }

        surface.SettledWeather = weather;

        switch (surface)
        {
            case Group group:
                foreach (Surface child in group.Surfaces)
                    Settle(child, weather, shared, found);
                break;
            case CsgSurface csgSurface:
                Settle(csgSurface.Left, weather, shared, found);
                Settle(csgSurface.Right, weather, shared, found);
                break;
            case IAssembled { Root: not null } assembled:
                Settle(assembled.Root, weather, shared, found);
                break;
            case Instance instance when shared.Add(instance.Prototype):
                Settle(instance.Prototype, null, shared, found);
                break;
        }
    }

    /// <summary>
    /// This method returns the weather that reaches a hit: its surface's, or else the nearest of the
    /// instances it was met through.
    /// </summary>
    /// <param name="intersection">The hit.</param>
    /// <returns>The weather, or <c>null</c>.</returns>
    internal static Weather At(Intersection intersection)
    {
        if (intersection.Surface.SettledWeather is { } weather)
            return weather;

        for (Portals portals = intersection.Portals; !portals.IsEmpty; portals = portals.Outward)
        {
            if (portals.Through.SettledWeather is { } through)
                return through;
        }

        return null;
    }
}
