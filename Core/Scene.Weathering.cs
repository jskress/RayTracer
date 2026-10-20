using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This part of the scene answers the weathering patterns' looking about: how far a short ray from a place on a
/// surface goes before it meets anything.  See <see cref="Weathering"/>.
/// </summary>
public partial class Scene
{
    /// <summary>
    /// This property holds how many directions a weathering pattern looks about in at each place.
    /// </summary>
    public int WeatheringSamples { get; set; } = Weathering.DefaultSamples;

    /// <summary>
    /// This property holds the scene being shaded on this thread, for the patterns that look about the place
    /// being shaded; see <see cref="Surroundings"/>.
    /// </summary>
    [field: ThreadStatic]
    internal static Scene Shading { get; private set; }

    /// <summary>
    /// This property holds the instant of the shutter's opening that is being shaded on this thread, which
    /// the weathering patterns look about at: a moving model's dirt is where the model is then.
    /// </summary>
    [field: ThreadStatic]
    internal static int ShadingTime { get; private set; }

    /// <summary>
    /// This property reports whether what is being shaded on this thread is the first thing a ray from the eye
    /// met, rather than something seen along a rough surface's spread or a bounce of light -- where the
    /// weathering patterns look about in fewer directions, the many paths there averaging what each lacks.
    /// See <see cref="Weathering"/>.
    /// </summary>
    internal static bool ShadingFirstHit => !_spreading && _bounced == 0;

    /// <summary>
    /// This method notes that this scene is being shaded on this thread, at the given instant, and returns
    /// what was being shaded before, for <see cref="ShadeAgain"/> to put back.
    /// </summary>
    /// <param name="timeIndex">Which instant of the shutter's opening is being shaded.</param>
    /// <returns>What was being shaded before.</returns>
    internal (Scene Scene, int TimeIndex) ShadeHere(int timeIndex)
    {
        (Scene, int) was = (Shading, ShadingTime);

        Shading = this;
        ShadingTime = timeIndex;

        return was;
    }

    /// <summary>
    /// This method puts back what was being shaded on this thread before <see cref="ShadeHere"/>.
    /// </summary>
    /// <param name="was">What was being shaded.</param>
    internal static void ShadeAgain((Scene Scene, int TimeIndex) was) => (Shading, ShadingTime) = was;

    /// <summary>
    /// This method returns how far along a ray the nearest thing it meets lies, if it meets anything within
    /// the given reach, or the reach if it meets nothing.  What casts no shadow, and hair, do not count: a
    /// stand-in is not there, and a strand is no wall for dirt to gather against.
    /// </summary>
    /// <param name="ray">The ray, its direction of unit length.</param>
    /// <param name="reach">How far to look.</param>
    /// <returns>How far off the nearest thing is, or the reach.</returns>
    internal double NearestWithin(Ray ray, double reach) => NearestCrossingWithin(ray, reach)?.Distance ?? reach;

    /// <summary>
    /// This method returns the nearest crossing of anything a ray meets within the given reach, by the same
    /// rules as <see cref="NearestWithin"/>, or <c>null</c> if it meets nothing.
    /// </summary>
    /// <param name="ray">The ray, its direction of unit length.</param>
    /// <param name="reach">How far to look.</param>
    /// <returns>The nearest crossing, or <c>null</c>.</returns>
    internal Intersection NearestCrossingWithin(Ray ray, double reach)
    {
        Intersection nearest = null;

        foreach (Intersection crossing in IntersectWithin(ray, reach))
        {
            if (crossing.Distance > 0 && crossing.Distance < (nearest?.Distance ?? reach) &&
                crossing.Surface is not StrandSurface && !crossing.Surface.NoShadow && !crossing.Portals.CastsNoShadow)
                nearest = crossing;
        }

        return nearest;
    }
}
