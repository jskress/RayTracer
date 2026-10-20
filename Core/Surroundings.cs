using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This record holds where a surface is being shaded, for the patterns that look about the place rather than
/// only at it: how closed in it is, how far it stands out, which way it faces.  See <see cref="Weathering"/>.
/// <para>
/// **A pattern is asked about a point and nothing else**, and that has always been enough: wood's grain
/// and marble's veins are where they are whatever stands round them.  Dirt is not -- it gathers where the
/// surface is closed in, and wear where it stands out -- so the patterns that place them must see the
/// scene round the point.  It is set where a material's pigment is asked for its color, and only for a
/// pigment that wants it, so every other is asked exactly as it always was.
/// </para>
/// </summary>
/// <param name="Scene">The scene the surface is in.</param>
/// <param name="Surface">The surface being shaded.</param>
/// <param name="Point">The place on it, in the world.</param>
/// <param name="Normal">The way it faces there, in the world, of unit length.</param>
/// <param name="Portals">The instances it was met through.</param>
/// <param name="TimeIndex">Which instant of the shutter's opening it was met at.</param>
public sealed record Surroundings(
    Scene Scene, Surface Surface, Point Point, Vector Normal, Portals Portals, int TimeIndex = 0)
{
    /// <summary>
    /// This property holds where the surface being shaded on this thread is, while its pigment is asked for
    /// its color; <c>null</c> otherwise.
    /// </summary>
    [field: ThreadStatic]
    public static Surroundings Current { get; set; }
}
