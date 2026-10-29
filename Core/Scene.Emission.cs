using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This part of the scene gives off the light of its glowing surfaces: what a surface that glows of its own
/// shows a ray meeting it, and -- in a scene that follows bounced light -- the light those surfaces cast,
/// looked at directly by one light made of them all; see <see cref="EmissionLight"/>.
/// <para>
/// **The two must agree exactly**, since the light stands in for what bounced light found by meeting the
/// surfaces: a sample's glow is worked out by the same reckoning a ray meeting the surface there is shaded
/// with (<see cref="GlowGiven{TS}"/>), and a ray that the light's samples stand in for -- a search for
/// bounced light on its straight run -- finds the surface's glow black.  A rough surface's reflection meeting
/// a glowing surface shares its glow with the light's samples by how likely each was to find it, as it shares
/// a glowing volume's.
/// </para>
/// </summary>
public partial class Scene
{
    /// <summary>
    /// This property holds the light of the scene's glowing surfaces, or <c>null</c> for a scene without one:
    /// one that does not follow bounced light, or has nothing glowing in it.
    /// </summary>
    public EmissionLight GlowingSurfaces { get; private set; }

    /// <summary>
    /// This method makes the scene's glowing surfaces a light, when it follows bounced light and has any.  The
    /// scene must be ready to be searched, and its lights standing on things set out.
    /// </summary>
    /// <param name="samples">How many places the glowing surfaces are looked at from.</param>
    /// <param name="instants">How many instants of the shutter's opening the camera looks at.</param>
    internal void LightTheGlowingSurfaces(int samples, int instants)
    {
        if (DiffuseBounces <= 0 || EmissionLight.Of(this, instants, samples) is not { } light)
            return;

        GlowingSurfaces = light;
        Lights.Add(light);
    }

    /// <summary>
    /// This method works out the light a surface gives off of its own toward the ray that met it, which its
    /// color tints, as it tints what it takes from a lamp -- and which leaves through the skin as everything
    /// else of its own does: its own share of what a Fresnel surface does not mirror, a film's passing share, a
    /// coat and a sheen.  (Charged by the coat alone, a glowing bulb that follows Fresnel showed all its glow at
    /// a graze on top of the mirror image that is all it shows there.)
    /// </summary>
    private static TS GlowGiven<TS>(
        Intersection intersection, Material material, double ownShare, FilmShares<TS>? film, Coat coat,
        double cosEye, Sheen sheen)
        where TS : struct, ISpectrum<TS>
    {
        TS given = TS.FromReflectance(material.GetColorFor(intersection)) * TS.FromIlluminant(material.Emission) *
                   ownShare;

        if (film is { } passing)
            given *= passing.Passed;

        if (coat is not null)
            given *= coat.ThroughOut<TS>(cosEye, intersection.N1);

        if (sheen is not null)
            given *= sheen.Beneath(cosEye);

        return given;
    }

    /// <summary>
    /// This method works out the glow a ray meeting a glowing surface at the given place finds there, its
    /// material settled as the shading settles it and charged for what the surface lets through, exactly as
    /// <see cref="GetHitColor{TS}"/> charges it.  It is what the light of the glowing surfaces carries.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="seen">The place, prepared, its eye looking back along the way that found it.</param>
    /// <returns>The glow.</returns>
    internal TS GlowSeenAt<TS>(Intersection seen)
        where TS : struct, ISpectrum<TS>
    {
        (Scene, int) was = ShadeHere(seen.TimeIndex);

        try
        {
            Material material = SettledFor(seen);

            if (material.Emission is null)
                return TS.Black;

            double transparency = material.PigmentMayTransmit
                ? material.TransparencyFor(material.GetColorFor(seen))
                : material.Transparency;
            bool transmits = material.Transparency > 0 || material.PigmentMayTransmit;
            bool sparesHighlight = (material.IsRough && transmits) || material.Film is not null;
            double cosEye = seen.Eye.Dot(seen.Normal);
            double ownShare = material.FresnelApplies ? 1 - material.ReflectanceAt(cosEye) : 1;

            if (material.BareMetalAt(seen) is { } bare)
                ownShare *= 1 - bare.Share;

            if (sparesHighlight)
                ownShare *= 1 - transparency;

            FilmShares<TS>? film = material.Film?.SharesAt<TS>(seen, material.MetalApplies ? material.Metal : null);
            TS glow = GlowGiven(
                seen, material, ownShare, film, CoatOn(seen, material), cosEye,
                material.SheenApplies ? material.Sheen : null);

            return sparesHighlight ? glow : glow * (1 - transparency);
        }
        finally
        {
            ShadeAgain(was);
        }
    }

    /// <summary>
    /// This field holds a list to find a glowing surface's crossings in, on each thread.
    /// </summary>
    [ThreadStatic]
    private static List<Intersection> _glowCrossings;

    /// <summary>
    /// This method looks for a glowing surface along a ray, alone -- or with the rest of the combination it is
    /// part of, which may hide it -- and returns where the ray first meets it, prepared, or <c>null</c> where
    /// the ray first meets something else of it, or nothing.
    /// </summary>
    /// <param name="whole">The surface to look for, or the combination it is part of.</param>
    /// <param name="surface">The glowing surface.</param>
    /// <param name="portals">The instances it stands behind.</param>
    /// <param name="ray">The ray, from the place being lit.</param>
    /// <returns>The place, or <c>null</c>.</returns>
    internal Intersection FindTheGlow(Surface whole, Surface surface, Portals portals, Ray ray)
    {
        List<Intersection> crossings = _glowCrossings ??= [];
        Intersection first = FirstCrossingOf(whole, ray, portals, crossings);

        if (first is null || !ReferenceEquals(first.Surface, surface))
            return null;

        first.PrepareUsing(ray, crossings, Environment.IndexOfRefraction);

        return first;
    }

    /// <summary>
    /// This method returns the share of a glowing surface's glow a ray meeting it is owed, where the light of
    /// the glowing surfaces looks at the surface too: none, along a search for bounced light's straight run,
    /// which the light's samples stand in for; the reflection's share, along a rough surface's reflection, the
    /// rest being the highlight's (see <see cref="HighlightShareOf"/>); and all of it any other way -- seen
    /// from the eye, in a mirror, round a corner -- where the light's samples could not have found it.
    /// </summary>
    /// <param name="intersection">Where the ray met the glowing surface.</param>
    /// <returns>The share.</returns>
    private double GlowShareOf(Intersection intersection)
    {
        Ray ray = RayThatReached(intersection);

        if (SeenAlongABounce(ray))
            return 0;

        if (_looking is not { } run || !OnRun(ray, run.Origin, run.Direction))
            return 1;

        EmissionLight light = (EmissionLight) GlowingSurfaces.At(intersection.TimeIndex);
        double byLooking = run.Count * run.Odds;
        double byLight = light.SampleCount * light.OddsToward(run.Origin, intersection);

        return byLooking + byLight > 0 ? byLooking / (byLooking + byLight) : 1;
    }
}
