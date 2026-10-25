using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This class works out what the weathering patterns tell of a place on a surface: how closed in it is, which
/// is where dirt and grime gather, and how far it stands out, which is where paint wears through.
/// <para>
/// **Both are found by looking about**, a few dozen short rays from the place, spread as a matte surface
/// spreads light, so that the rays nearly square to it count most.  Looking out from the surface, a ray that
/// meets something within reach says the place is closed in that way, and counts more the nearer it is:
/// at the foot of a groove nearly every ray meets a wall close by, on an open floor none do, and where a
/// floor meets a wall about a third.  Looking in, from just under the surface into whatever it is the skin
/// of, a ray that comes out again within reach says the place stands out that way: on a flat face of a
/// thick solid none do, and right at a corner a good share come out through the face round it.  One that
/// comes out through a face turned back the way the place faces has found the far side of a sheet, not a
/// face round a corner, and counts less the further that face is turned, to nothing for one facing straight
/// back: so a panel of sheet metal is flat across its face and stands out only along its rim, and a ridge
/// sharper than a right angle stands out by how sharp it is.
/// </para>
/// <para>
/// **The same directions every time**, spread evenly by the golden angle and turned by an amount that is a
/// fixed function of the place, so a render is the same twice over and neighboring places look different
/// ways, which the samples within a pixel then average.  What a place is worth is worked out once and kept,
/// since a pigment is asked for its color again for every light.
/// </para>
/// <para>
/// **The reach is in the units of the space the surface stands in**: the scene's, for a shape standing in the
/// scene, and a model's own for a part of one, so a droid built in inches says how far its dirt reaches in
/// inches however the droid is then sized.  Not the surface's own: a shape is nearly always a unit cube or
/// ball scaled to size, and a reach that stretched with it would mean something different on every one.
/// </para>
/// <para>
/// **At the instant the place was met**, so a moving model's dirt is where the model is then.
/// </para>
/// </summary>
public static class Weathering
{
    /// <summary>
    /// This is how many directions a place is looked about in when the scene says nothing.
    /// </summary>
    public const int DefaultSamples = 64;

    // The golden angle, which spreads any number of directions round a circle about as evenly as can be.
    private const double GoldenAngle = 2.399963229728653;

    // What each thread has worked out lately: a pigment asks once for each light at the same place.  Kept
    // against the scene and surface too, since the same pattern may be asked about the same point in two.
    [ThreadStatic]
    private static (object Pattern, Scene Scene, Surface Surface, int TimeIndex, Point Point, double Value)[] _kept;

    [ThreadStatic]
    private static int _next;

    /// <summary>
    /// This method returns how closed in the place being shaded is, from nought, open, to one, closed off
    /// close by on every side; nought where nothing is being shaded.
    /// </summary>
    /// <param name="pattern">The pattern asking, which the answer is kept against.</param>
    /// <param name="reach">How far to look, in the surface's own units.</param>
    /// <returns>How closed in it is.</returns>
    public static double Enclosure(object pattern, double reach) => Looked(pattern, reach, true);

    /// <summary>
    /// This method returns how far the place being shaded stands out, from nought, a flat face of a solid,
    /// to one; nought where nothing is being shaded.
    /// </summary>
    /// <param name="pattern">The pattern asking, which the answer is kept against.</param>
    /// <param name="reach">How far to look, in the surface's own units.</param>
    /// <returns>How far it stands out.</returns>
    public static double Exposure(object pattern, double reach) => Looked(pattern, reach, false);

    /// <summary>
    /// This method returns which way the place being shaded faces, against a direction in the world: one
    /// facing it, a half square to it, nought facing away; a half where nothing is being shaded.
    /// </summary>
    /// <param name="toward">The direction, in the world.</param>
    /// <returns>How far it faces that way.</returns>
    public static double Facing(Vector toward)
    {
        Surroundings here = Surroundings.Current;

        return here is null ? 0.5 : Math.Clamp((1 + here.Normal.Dot(toward.Unit)) / 2, 0, 1);
    }

    // Looks out from the place, or in, and keeps the answer.
    private static double Looked(object pattern, double reach, bool outward)
    {
        Surroundings here = Surroundings.Current;

        if (here is null || here.Scene is null || reach <= 0)
            return 0;

        _kept ??= new (object, Scene, Surface, int, Point, double)[4];

        foreach ((object kept, Scene scene, Surface surface, int timeIndex, Point at, double value) in _kept)
        {
            if (ReferenceEquals(kept, pattern) && ReferenceEquals(scene, here.Scene) &&
                ReferenceEquals(surface, here.Surface) && timeIndex == here.TimeIndex && at is not null &&
                at.Matches(here.Point))
                return value;
        }

        double found = LookAbout(here, reach * ScaleOf(here.Surface, here.Portals, here.TimeIndex), outward);

        _kept[_next] = (pattern, here.Scene, here.Surface, here.TimeIndex, here.Point, found);
        _next = (_next + 1) % _kept.Length;

        return found;
    }

    // The rays themselves.
    private static double LookAbout(Surroundings here, double reach, bool outward)
    {
        Scene scene = here.Scene;
        Vector normal = outward ? here.Normal : -here.Normal;
        (Vector across, Vector along) = Microfacets.FrameAround(normal);
        double offset = Math.Max(here.Surface.SelfOffsetAt(here.TimeIndex, here.Portals), reach * 1e-4);
        Point origin = here.Point + normal * offset;
        // All of them where the eye looks first; elsewhere -- in a rough surface's blur, along bounced light -- an
        // eighth, since there the many paths average what each one lacks.  Without that a droid's rough aluminum
        // mirrored his weathered paint sixteen ways at every point, each looking about in sixty-four directions:
        // twenty-seven times the render.
        int count = Scene.ShadingFirstHit
            ? Math.Max(1, scene.WeatheringSamples)
            : Math.Max(1, scene.WeatheringSamples / 8);
        double turn = Scene.ShiftFor(here.Point, outward ? 121 : 122) * 2 * Math.PI;
        double total = 0;

        for (int index = 0; index < count; index++)
        {
            double share = (index + 0.5) / count;
            double radius = Math.Sqrt(share);
            double angle = index * GoldenAngle + turn;
            Vector direction = (across * (radius * Math.Cos(angle)) + along * (radius * Math.Sin(angle)) +
                                normal * Math.Sqrt(1 - share)).Unit;
            Ray ray = new (origin, direction, here.TimeIndex);
            Intersection crossing = scene.NearestCrossingWithin(ray, reach);

            if (crossing is not null)
                total += (1 - crossing.Distance / reach) * (outward ? 1 : OutBy(crossing, ray, here.Normal));
        }

        return total / count;
    }

    // How far a ray looking in came out round the place, by the face it came out through: whole for one square
    // to the place's own or turned toward it -- round a corner -- less as it turns back the place's way, and
    // nothing for one facing straight back, the far side of a sheet.
    private static double OutBy(Intersection crossing, Ray ray, Vector normal)
    {
        crossing.TimeIndex = ray.TimeIndex;

        Vector face = crossing.Surface.NormalAt(ray.At(crossing.Distance), crossing).Unit;

        // Which way a face's normal points is not always which way out of the solid it is (a hollow's, cut
        // out of something, points in), but the ray is on its way out.
        if (face.Dot(ray.Direction) < 0)
            face = -face;

        return Math.Clamp(1 + face.Dot(normal), 0, 1);
    }

    // How much the space the surface stands in is scaled on its way into the world -- its parent's, or the
    // instance's it was met through, but not its own transform -- as the cube root of how much it stretches a
    // volume, which for an even scale is the scale itself.
    private static double ScaleOf(Surface surface, Portals portals, int timeIndex)
    {
        Func<Vector, Vector> carried = surface.Parent is { } parent
            ? direction => parent.DirectionToWorld(direction, timeIndex, portals)
            : portals.IsEmpty
                ? null
                : direction => portals.Through.DirectionToWorld(direction, timeIndex, portals.Outward);

        if (carried is null)
            return 1;

        Vector x = carried(new Vector(1, 0, 0));
        Vector y = carried(new Vector(0, 1, 0));
        Vector z = carried(new Vector(0, 0, 1));

        return Math.Cbrt(Math.Abs(x.Dot(y.Cross(z))));
    }
}
