using System.Collections.Concurrent;
using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace RayTracer.Core;

/// <summary>
/// This part of the scene shades strands of hair, which scatter light as fibers rather than as
/// surfaces: see <see cref="Fiber"/>.
/// </summary>
public partial class Scene
{
    // What a material whose color is the same everywhere absorbs, worked out once for each roughness.
    private readonly ConcurrentDictionary<(Material, double), Dye> _steadyDyes = new ();

    /// <summary>
    /// This method works out the light a strand of hair sends toward the eye.
    /// <para>
    /// **A strand is shaded as the fiber it is, never as a surface.**  It has no inside a ray can be
    /// in, no face that turns from the light, and nothing it mirrors or lets through is followed as a
    /// ray of its own: all of that is in how it scatters the light reaching it, which is gathered from
    /// every light here -- from behind it as much as from in front, which is the glow of backlit hair.
    /// </para>
    /// <para>
    /// The light is taken at the strand's middle, as the fiber's scattering assumes, and each shadow ray
    /// sets off a radius toward its light, so that the strand's own share of what passes through it is
    /// counted in its scattering and not again on the way out.
    /// </para>
    /// <para>
    /// **Other strands stop a shadow ray here**, though they let light through to a surface beneath them.
    /// Light that came to this strand by way of other strands was scattered on the way, and it is counted
    /// with all the rest of the light scattered among the strands, gathered ahead of time; see
    /// <see cref="HairLight"/>.  A strand standing alone has no others to scatter light to it.
    /// </para>
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="hit">Where the ray met the strand, already prepared.</param>
    /// <returns>The light leaving it toward the eye.</returns>
    private TS ShadeFiber<TS>(HairIntersection hit)
        where TS : struct, ISpectrum<TS>
    {
        StrandSurface strands = (StrandSurface) hit.Surface;
        Material material = strands.Material ?? Material.Default;
        Dye dye = DyeOf(hit, hit.Point);
        TS absorption = dye.Absorbing<TS>();
        TS own = dye.Showing<TS>();
        Fiber fiber = Fiber.For(dye.Roughness);

        StrandFrame frame = FrameOf(hit);
        Fiber.Seen<TS> seen = fiber.Seeing(frame.SineOf(hit.Eye), Math.PI / 2, frame.H, absorption);
        double ambient = material.EffectiveAmbient;
        TS color = TS.Black;

        for (int number = 0; number < Lights.Count; number++)
        {
            Light light = Lights[number].At(hit.TimeIndex);
            int count = light.SampleCount;
            (int taken, double odds) = TakenOf(count, hit, number);
            TS sum = TS.Black;

            for (int each = 0; each < taken; each++)
            {
                int index = taken == count ? each : (int) ((each + 0.5) * count / taken + hit.Ordinal * 7) % count;
                LightSample sample = light.SampleAround(frame.Center, index);
                TS emitted = light.EmittedToward<TS>(sample);

                // Ambient light stands in for light bounced about the scene, as it does for a surface,
                // and is shown in the color the hair is matched to.
                sum += own * emitted * ambient;

                // A strand that did not draw this light this time takes only its ambient from it.
                double intensity = odds > 0 ? sample.Cone * light.FadingOver(sample.Distance) / odds : 0;

                if (intensity <= 0)
                    continue;

                Vector toward = sample.Direction;
                TS reaching = GetLightReaching<TS>(
                    frame.Center + toward * frame.Radius, toward, sample.Distance - frame.Radius, hit.TimeIndex,
                    null, true, hit.FootprintRadius, light.Housing);

                if (reaching.IsBlack)
                    continue;

                // A light's color is the irradiance it gives a surface square to it, over π.
                TS scattered = fiber.Scattered(seen, frame.SineOf(toward), frame.AngleOf(toward));

                sum += scattered * emitted * reaching * (Math.PI * intensity);
            }

            color += taken == 1 ? sum : sum * (1.0 / taken);
        }

        // The light scattered among the strands arrives from every way, more from some than others, and
        // each lobe sends toward the eye what arrives from where it draws its light; the last lobe draws
        // from every way round alike.
        if (strands.HasNeighbors && HairOf(strands) is { } hair)
        {
            HairLight.Here here = LightAmong(hair, hit.Portals).At(frame.Center);

            for (int lobe = 0; lobe < 3; lobe++)
            {
                (double sinTheta, double phi) = fiber.CenterOf(seen, lobe);

                color += Fiber.Carried(seen, lobe) * here.From<TS>(frame.Toward(sinTheta, phi));
            }

            color += Fiber.Carried(seen, 3) * here.Everywhere<TS>();
        }

        return color;
    }

    /// <summary>
    /// This method returns how many of a light's samples to shade a strand with, and the odds that the
    /// strand looks at the light at all: every sample, surely, unless the strand is counted for only a
    /// share of what the eye sees, when it is that share of them -- and, where that comes to less than one,
    /// one sample by chance, at odds of what it comes to, counted for as much more as it is unlikely.
    /// <para>
    /// **A fine strand is one of a great many along a ray**, each covering a little of it: at a pixel
    /// fourteen millimeters across, the strands of a rug's pile cover a few hundredths of it each, and the
    /// ray goes through a hundred of them before it is covered.  Shading every one of them with every
    /// sample of a fire's light sent thousands of shadow rays into the pile for each pixel.  Each looks at
    /// as many as its share says, picked evenly from among the light's own, and a strand whose share is
    /// less than one sample's worth of a light looked at from many places looks at one now and then, so
    /// that the strands of one ray between them look at about as many as one opaque strand would.  The
    /// chance is drawn from the crossing itself, so a picture comes out the same every time it is made.
    /// </para>
    /// </summary>
    private static (int Taken, double Odds) TakenOf(int count, HairIntersection hit, int light)
    {
        if (hit.Share >= 1)
            return (count, 1);

        double wanted = count * hit.Share;

        if (wanted >= 1 || count < FewestToDrawFrom)
            return (Math.Clamp((int) Math.Ceiling(wanted), 1, count), 1);

        return (1, ChanceAt(hit, light) < wanted ? wanted : 0);
    }

    // How many places a light must be looked at from for a strand to look at it only by chance.  A light
    // looked at from one place -- a lamp -- would light each fine strand or not on a coin's toss, and a
    // pixel's worth of them comes out speckled as brightly as the lamp itself; so every strand looks at a
    // light seen from fewer places, as it always has, and only the strands of a fire, a sky or a broad
    // lamp take their chance, where the speckle is no more than the light's own sampling makes.
    private const int FewestToDrawFrom = 8;

    /// <summary>
    /// This method returns a number from nought to one, the same every time for the same crossing and the
    /// same light, and as good as random from one to the next.
    /// </summary>
    private static double ChanceAt(HairIntersection hit, int light)
    {
        ulong bits = (ulong) BitConverter.DoubleToInt64Bits(hit.Distance) +
                     0x9E3779B97F4A7C15UL * (ulong) (hit.Ordinal * 31L + light + 1);

        bits = (bits ^ (bits >> 30)) * 0xBF58476D1CE4E5B9UL;
        bits = (bits ^ (bits >> 27)) * 0x94D049BB133111EBUL;
        bits ^= bits >> 31;

        return (bits >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>
    /// This method returns the head of hair a strand surface belongs to: itself, or the hair it is a part
    /// of.
    /// </summary>
    private static Hair HairOf(StrandSurface strands) => strands as Hair ?? strands.Parent as Hair;

    /// <summary>
    /// This record holds a strand's frame in the world where a ray met it: its middle there, the way it
    /// runs, the way toward the eye square to it, the way across, where across the strand the ray passed,
    /// and how wide it is.  See <see cref="Fiber"/> for how angles are measured in it.
    /// </summary>
    internal readonly record struct StrandFrame(
        Point Center, Vector Tangent, Vector Face, Vector Side, double H, double Radius)
    {
        /// <summary>
        /// This method returns the sine of a direction's angle out of the plane square to the strand.
        /// </summary>
        public double SineOf(Vector direction) => Math.Clamp(direction.Dot(Tangent), -1, 1);

        /// <summary>
        /// This method returns a direction's angle round the strand.
        /// </summary>
        public double AngleOf(Vector direction) => Math.Atan2(direction.Dot(Face), direction.Dot(Side));

        /// <summary>
        /// This method returns the direction at the given angles.
        /// </summary>
        public Vector Toward(double sinTheta, double phi)
        {
            double cosTheta = Math.Sqrt(Math.Max(0, 1 - sinTheta * sinTheta));

            return (Tangent * sinTheta + (Side * Math.Cos(phi) + Face * Math.Sin(phi)) * cosTheta).Unit;
        }
    }

    /// <summary>
    /// This method works out a strand's frame in the world where a ray met it, already prepared.
    /// </summary>
    internal static StrandFrame FrameOf(HairIntersection hit)
    {
        StrandSurface strands = (StrandSurface) hit.Surface;
        Point center = strands.SurfaceToWorld(hit.Center, hit.TimeIndex, hit.Portals);
        Vector tangent = (strands.SurfaceToWorld(hit.Center + hit.Tangent, hit.TimeIndex, hit.Portals) - center).Unit;
        Vector eye = hit.Eye;
        Vector face = eye - tangent * eye.Dot(tangent);

        face = face.Magnitude > 1e-9 ? face.Unit : Microfacets.FrameAround(tangent).Across;

        Vector side = face.Cross(tangent);
        double h = Math.Abs(hit.Across) * ((hit.Point - center).Dot(side) < 0 ? -1 : 1);

        return new StrandFrame(center, tangent, face, side, h, (hit.OverPoint - center).Magnitude);
    }

    /// <summary>
    /// This method returns how much of the light along a shadow ray a strand it crosses sends onward, per
    /// band.  See <see cref="Fiber.SharesOf{TS}"/>.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="crossed">The crossing.</param>
    /// <param name="ray">The shadow ray.</param>
    /// <returns>The share of each band sent onward.</returns>
    private TS OnwardThroughStrand<TS>(HairIntersection crossed, Ray ray)
        where TS : struct, ISpectrum<TS>
    {
        return OnwardThroughStrand<TS>(crossed, ray.At(crossed.Distance));
    }

    private TS OnwardThroughStrand<TS>(HairIntersection crossed, Point where)
        where TS : struct, ISpectrum<TS>
    {
        Dye dye = DyeOf(crossed, where);

        return Fiber.For(dye.Roughness).SharesOf(crossed.SineAlong, dye.Absorbing<TS>()).Onward;
    }

    /// <summary>
    /// This method returns how much of the light falling on a surface beneath a strand gets past it, in
    /// whichever band the most does: what it sends onward of the share of the ray it covers, and all of
    /// the rest.  See <see cref="StrandTally"/>.
    /// </summary>
    private double MostPassedBy<TS>(HairIntersection strand, Point where)
        where TS : struct, ISpectrum<TS>
    {
        TS onward = OnwardThroughStrand<TS>(strand, where);
        double most = 0;

        for (int band = 0; band < TS.Count; band++)
            most = Math.Max(most, onward[band]);

        return strand.Coverage >= 1 ? most : most * strand.Coverage + 1 - strand.Coverage;
    }

    /// <summary>
    /// This method works out what colors a strand: how much its inside absorbs, the color a head of such
    /// hair comes out, and how rough it is.
    /// <para>
    /// **A strand wearing the material of the surface it grows from takes its color from that surface
    /// where it grows**, read in that surface's own space at the strand's root, so a striped skin grows
    /// striped fur; and it takes none of that surface's finish, which belongs to a skin and not a hair.
    /// A strand given a material of its own is colored where it is met, and takes its roughness from it.
    /// Either way, melanin named in the material colors it in place of the pigment.
    /// </para>
    /// </summary>
    /// <param name="hit">The crossing.</param>
    /// <param name="where">Where it is, in the world.</param>
    /// <returns>What colors the strand.</returns>
    internal Dye DyeOf(HairIntersection hit, Point where)
    {
        StrandSurface strands = (StrandSurface) hit.Surface;
        Material material = strands.Material ?? Material.Default;
        double roughness = !strands.WearsItsSkin && material.Roughness > 0 ? material.Roughness : Fiber.DefaultRoughness;

        if (material.Melanin is { } melanin)
        {
            Color absorption = Fiber.AbsorptionFor(melanin);

            return new Dye(absorption, Fiber.ColorFor(absorption, roughness), roughness, melanin);
        }

        // Most hair is one color all over, and a shadow ray through fur crosses a great many strands, so
        // such a color is turned into absorption once.
        bool steady = material.Pigment is SolidPigment && material.Decals is null;

        if (steady && _steadyDyes.TryGetValue((material, roughness), out Dye known))
            return known;

        Color color = ColorOfStrand(hit, strands, material, where);
        Dye dye = new (Fiber.AbsorptionFor(color, roughness), color, roughness, null);

        if (steady)
            _steadyDyes[(material, roughness)] = dye;

        return dye;
    }

    /// <summary>
    /// This record holds what colors a strand: how much its inside absorbs, in red, green and blue; the
    /// color a head of such hair comes out; its roughness; and its melanin, when that is what colors it.
    /// </summary>
    internal sealed record Dye(Color Absorption, Color Matched, double Roughness, Melanin? Melanin)
    {
        /// <summary>
        /// This method returns how much the strand's inside absorbs in whatever bands the render carries
        /// light in.
        /// </summary>
        /// <typeparam name="TS">The kind of light being carried.</typeparam>
        /// <returns>The absorption, per band.</returns>
        public TS Absorbing<TS>()
            where TS : struct, ISpectrum<TS>
        {
            return TS.FromUnbounded(Absorption);
        }

        /// <summary>
        /// This method returns the color a head of such hair comes out, as a reflectance, in whatever
        /// bands the render carries light in.
        /// <para>
        /// **A spectral render works the color out band by band from the absorption**, rather than
        /// fitting a spectrum to the color itself.  Hair dark enough to absorb nearly everything is matched
        /// to a color very dark and very red, and the spectrum fitted to such a color came out bright at
        /// both ends of the visible range: black hair came out purple.  The absorption's own spectrum has
        /// no such trouble, and the color worked out from it agrees with it by construction.
        /// </para>
        /// </summary>
        /// <typeparam name="TS">The kind of light being carried.</typeparam>
        /// <returns>The color, per band.</returns>
        public TS Showing<TS>()
            where TS : struct, ISpectrum<TS>
        {
            if (!TS.IsSpectral)
                return TS.FromReflectance(Matched);

            TS absorbing = Absorbing<TS>();
            TS showing = TS.Black;

            for (int band = 0; band < TS.Count; band++)
                showing[band] = Fiber.ColorFor(absorbing[band], Roughness);

            return showing;
        }
    }

    /// <summary>
    /// This method returns the color of the material a strand wears: at its root, in the space of what it
    /// grew from, if that is where the material came from, or where it was met otherwise.
    /// </summary>
    private static Color ColorOfStrand(HairIntersection hit, StrandSurface strands, Material material, Point where)
    {
        if (strands.WearsItsSkin && strands.RootOf(hit.Strand) is ({ } part, { } root))
            return material.GetColorFor(part, strands.SurfaceToWorld(root, 0, hit.Portals), portals: hit.Portals);

        return material.GetColorFor(hit.Surface, where, portals: hit.Portals);
    }
}
