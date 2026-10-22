using RayTracer.Basics;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace RayTracer.Core;

/// <summary>
/// This class represents light arriving from every direction at once, as the sky does.
/// <para>
/// Every other light in this renderer is somewhere: a lamp at a point, a panel with a face, a sun in
/// one direction.  This one is nowhere in particular and everywhere at once, and that is what makes it
/// the light of the outdoors.  What it is worth from any given direction is whatever the sky is that
/// way, so it takes a pigment and reads it exactly as the background does -- painted on a sphere of
/// radius one, infinitely far off.  Left to itself it borrows the scene's own background, so that the
/// sky you look at is the sky that lights you.
/// </para>
/// <para>
/// It is also the honest form of the thing <c>ambient</c> has been standing in for.  Ambient is a flat
/// fudge added everywhere, shadow or no shadow, because the light bouncing about a scene was never
/// traced.  This is not that: it arrives from real directions, so a point that can only see a sliver of
/// sky gets a sliver of light, and a niche is darker than an open field for the reason a real one is.
/// That is why a scene with a sky light has no ambient by default.
/// </para>
/// <para>
/// Being spread over directions rather than gathered in a place, it is looked at from many samples like
/// an area light -- and, like an area light, it costs one shadow ray apiece.  Unlike the cloud-lighting
/// of multiple scattering, though, this is the kind case for sampling: the sky is large and smooth, so
/// there is no needle to find and tens of samples settle it rather than thousands.
/// </para>
/// </summary>
public class SkyLight : Light
{
    /// <summary>
    /// This property holds the sky this light carries, read by direction just as a background is.  It
    /// is the scene's own background when the scene names none of its own, which is the ordinary case
    /// and the one worth having: a sky that lights the scene differently from the sky it shows is a
    /// thing to reach for deliberately rather than by accident.
    /// </summary>
    public Pigment Pigment { get; set; }

    /// <summary>
    /// This property holds how many directions the sky is looked at from when a surface is shaded.
    /// </summary>
    public int Samples { get; set; } = 32;

    /// <summary>
    /// This property notes how many places this light is looked at from, which for the sky is however
    /// many directions it was asked to be sampled in.
    /// </summary>
    public override int SampleCount => Samples;

    /// <summary>
    /// This property reports whether the sky is a picture -- a photograph of a real sky, as a rule, sun
    /// and all -- and so is looked at mostly where its light is.  A sky made of patterns is smooth enough
    /// to be looked at evenly, and is, exactly as it always was.
    /// </summary>
    public bool IsAPicture => Pigment is ImagePigment && Map is { IsDark: false };

    /// <summary>
    /// A sky lends a smooth surface the highlight of each piece of itself a sample looks at, as bright as
    /// that piece is and in its color: a photograph's sun, its clouds, its blue; a physical sky's blue
    /// overhead and pale band at the horizon.  Taken from the light's color as a whole, as it once was,
    /// every sample of a blue sky put a white highlight on a shiny surface -- a white sheen the sky did
    /// not have, as bright from the dim blue overhead as from the bright horizon.
    /// </summary>
    protected internal override bool HighlightsBySample => true;

    // The sky's light laid out by direction, made the first time it is wanted, once the picture it is
    // made from is loaded, and kept with the pigment it was made from so that a sky given another is
    // mapped anew.  The two are kept together so that a thread reading them without the lock never sees
    // one without the other.
    private sealed record Mapped(Pigment Pigment, SkyMap Map);

    private Mapped _mapped;
    private readonly Lock _mapping = new ();

    /// <summary>
    /// This property notes that a ray can meet this light -- the sky, which shows in every mirror that faces it.
    /// </summary>
    public override bool CanBeSeen => true;

    /// <summary>
    /// This method works out which way the sky lies from a point, which has no one answer: it lies
    /// every way at once.  Straight up stands for it where a single direction must be given, which is
    /// only in the convenience form of shading that takes no sample of its own.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <returns>Straight up, from infinitely far off.</returns>
    public override (Vector Direction, double Distance) TowardFrom(Point point)
    {
        return (Directions.Up, double.PositiveInfinity);
    }

    /// <summary>
    /// This method picks one of the directions the sky is looked at from.
    /// <para>
    /// Where there is a surface, the directions are spread over the half of the sky it faces, since the
    /// other half is behind it and would be thrown away after being paid for.  They are spread evenly
    /// over that half rather than gathered toward the normal: the shading already weighs each direction
    /// by how squarely it meets the surface, and drawing them toward the normal as well would count
    /// that twice.
    /// </para>
    /// <para>
    /// Where there is no surface -- a place in the middle of a medium, which faces no way at all -- the
    /// directions are spread over the whole sphere instead.
    /// </para>
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <param name="index">Which sample this is.</param>
    /// <param name="normal">The surface normal, or <c>null</c> in the middle of a medium.</param>
    /// <returns>The sample: which way the sky lies, how far off, and all of the light.</returns>
    public override LightSample SampleToward(Point point, int index, Vector normal = null)
    {
        if (IsAPicture)
            return PictureSample(point, index, normal, normal is null ? 4 : 1);

        // Two irrationals walked against each other, which spreads any number of samples about as
        // evenly over a surface as they can be spread, and needs nothing remembered between them.  The
        // same pattern is used for every point, which is what keeps a render the same twice over; that
        // it is the same pattern everywhere shows as no artifact, the shading averaging all of it.
        double alongOne = (index + 0.5) / Samples;
        double alongTwo = index * 0.7548776662466927 % 1;

        // What a sample is worth differs between the two cases, and it is the cosine that makes the
        // difference.  A surface weighs every direction by how squarely it meets it, and over the half
        // of the sky it faces that weighing averages a half -- so each of those samples must count
        // double, or a white surface under a white sky would come back half white instead of white.  A
        // place in a medium weighs by the scattering shape instead, which already averages to one over
        // the sphere it was spread across, so those samples count once.
        return normal is null
            ? new LightSample(OverTheWholeSphere(alongOne, alongTwo), double.PositiveInfinity, 1)
            : new LightSample(
                OverTheHalfFacing(normal, alongOne, alongTwo), double.PositiveInfinity, 2);
    }

    /// <summary>
    /// This method works out one of the directions the sky is looked at in from a strand of hair: over the
    /// whole sphere, since a strand takes light from behind it as well as before.
    /// <para>
    /// **Each counts four times.**  A strand sends on, toward the eye, its share of the light arriving
    /// from every direction, and samples spread evenly over the sphere each stand for 4π of solid angle
    /// shared among them -- the averaging does the sharing.  The sky's light is radiance and a light's
    /// color is irradiance over π, so what a sample carries is the sky's light times 4π over π: four.
    /// </para>
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <param name="index">Which sample.</param>
    /// <returns>The sample.</returns>
    public override LightSample SampleAround(Point point, int index)
    {
        if (IsAPicture)
            return PictureSample(point, index, null, 1);

        double alongOne = (index + 0.5) / Samples;
        double alongTwo = index * 0.7548776662466927 % 1;

        return new LightSample(OverTheWholeSphere(alongOne, alongTwo), double.PositiveInfinity, 4);
    }

    /// <summary>
    /// This method picks a direction anywhere over the sky for a photon to come from: evenly over the whole
    /// sphere for a smooth sky, worth four as each of its samples is; and for a picture, by its light or
    /// evenly in the shares its samples are, worth what such a direction is worth among them.
    /// </summary>
    /// <param name="point">Where the sky is looked at from, which does not matter.</param>
    /// <param name="first">The first number, between nought and one.</param>
    /// <param name="second">The second.</param>
    /// <param name="third">Which way to pick, for a picture.</param>
    /// <returns>The sample.</returns>
    public override LightSample PhotonSample(Point point, double first, double second, double third)
    {
        if (!IsAPicture)
            return new LightSample(OverTheWholeSphere(first, second), double.PositiveInfinity, 4);

        int byLight = Samples - Samples / 2;
        Vector direction = third * Samples < byLight
            ? Map.Pick(first, second)
            : OverTheWholeSphere(first, second);
        double odds = OddsToward(direction, null);

        return new LightSample(direction, double.PositiveInfinity, odds > 0 ? Samples / (Math.PI * odds) : 0);
    }

    /// <summary>
    /// This method picks one of the directions a sky that is a picture is looked at from.
    /// <para>
    /// **Half the directions are picked by the sky's light and half evenly**, as a smooth sky's are, and
    /// every one of them is counted by how likely either way was to pick it (Veach's balance heuristic).
    /// Picked by its light alone, the sky's dim parts would be left to a few samples apiece, though a
    /// surface facing away from the sun is lit by nothing else; picked evenly alone, the sun is almost
    /// never found.  Counted by both, each part of the sky is found by the way that finds it best.
    /// </para>
    /// <para>
    /// **What a sample is worth** is the share of the sky it stands for, which is one over the odds of
    /// it having been picked: over the half of the sky a surface faces, picked evenly, the odds are one
    /// over 2π, and a light's color being its irradiance over π, that is the two every even sample of a
    /// smooth sky is worth.  A direction picked by the sky's light may lie behind the surface, and is
    /// then worth nothing, the shading giving it no light.
    /// </para>
    /// <para>
    /// **And the whole pattern is turned by an amount that is a fixed function of the point**, as a
    /// rough surface's reflection's is.  A smooth sky can be looked at in the same directions from
    /// everywhere, but a photographed one is clouds against blue, and directions fixed to each point's
    /// normal swept across them as the normal turned -- which laid the clouds over a white ball as
    /// blotches.  Turned point by point, the same error is fine grain instead, which the samples
    /// within a pixel average away and more samples settle; measured against a render of a thousand
    /// samples, the blotches' share of the error fell from 2.2 levels to 0.8, the error as a whole
    /// staying at 2.5.  A point gets the same directions every time, so a render comes out the same
    /// twice over.
    /// </para>
    /// </summary>
    /// <param name="point">The point being lit, which turns the pattern.</param>
    /// <param name="index">Which sample this is.</param>
    /// <param name="normal">The surface normal, or <c>null</c> where the sky is looked at over the
    /// whole sphere.</param>
    /// <param name="scale">What a sample is worth for each π of the share of the sky it stands for: one
    /// for a surface or a strand, which take a light's color as irradiance over π, and four in a medium,
    /// whose scattering shape already averages to one over the whole sphere.</param>
    /// <returns>The sample.</returns>
    private LightSample PictureSample(Point point, int index, Vector normal, double scale)
    {
        SkyMap map = Map;
        int byLight = Samples - Samples / 2;
        double turnOne = Scene.ShiftFor(point, 107);
        double turnTwo = Scene.ShiftFor(point, 108);
        Vector direction;

        if (index < byLight)
        {
            direction = map.Pick(
                ((index + 0.5) / byLight + turnOne) % 1, (index * 0.7548776662466927 + turnTwo) % 1);
        }
        else
        {
            int even = index - byLight;
            double alongOne = ((even + 0.5) / (Samples / 2) + turnOne) % 1;
            double alongTwo = (even * 0.7548776662466927 + turnTwo) % 1;

            direction = normal is null
                ? OverTheWholeSphere(alongOne, alongTwo)
                : OverTheHalfFacing(normal, alongOne, alongTwo);
        }

        double odds = OddsToward(direction, normal);

        return new LightSample(
            direction, double.PositiveInfinity, odds > 0 ? Samples / (Math.PI * odds) / scale : 0);
    }

    /// <summary>
    /// This method returns how likely the sky's samples, all of them together, are to look in the given
    /// direction from a surface facing the given way: the sum, over both ways of picking, of how many
    /// samples are picked that way times the odds of each picking that direction, per unit of solid
    /// angle.  It is what a sample's worth is reckoned from, and what a rough surface's reflection weighs
    /// itself against when it finds the same piece of sky.
    /// </summary>
    /// <param name="direction">The direction, which must be a unit vector.</param>
    /// <param name="normal">The way the surface faces, or <c>null</c> where the even samples are spread
    /// over the whole sphere.</param>
    /// <returns>The sky's odds of looking that way.</returns>
    public double OddsToward(Vector direction, Vector normal)
    {
        int even = Samples / 2;
        int byLight = Samples - even;
        double evenOdds = normal is null
            ? 1 / (4 * Math.PI)
            : direction.Dot(normal) > 0 ? 1 / (2 * Math.PI) : 0;

        return even * evenOdds + byLight * Map.OddsOf(direction);
    }

    /// <summary>
    /// This property holds the sky's light laid out by direction, made the first time it is asked for.
    /// <para>
    /// **The grid is twice as fine as the picture**, up to four thousand cells round the sky, and each
    /// cell is weighed by the sky at its middle, read as every sample of the sky is read.  A sun is a
    /// few texels across and the light between texels is a ramp, so on a grid only as fine as the
    /// picture a cell at the sun's edge is picked as though it were lit evenly when its light falls
    /// away across it.  Measured against a sun of known light, looked at in the same directions from
    /// every point, that left the sun's estimate out by up to a tenth between forty-eight samples and a
    /// hundred and twenty-eight, where the finer grid kept it within three hundredths.  Turned point by
    /// point, as the samples now are, the estimate is right on average over a floor to within a percent
    /// at any count, and strays from point to point by a twentieth at thirty-two samples, a fortieth at
    /// sixty-four and a seventieth at a hundred and twenty-eight.
    /// </para>
    /// <para>
    /// A picture too large for that has cells coarser than its texels, and each is weighed by the
    /// picture averaged over it instead -- read at its middle alone, a sun smaller than a cell could fall
    /// between the middles, and then only the even samples could find it.
    /// </para>
    /// </summary>
    private SkyMap Map
    {
        get
        {
            Pigment pigment = Pigment;
            Mapped mapped = Volatile.Read(ref _mapped);

            if (mapped is not null && ReferenceEquals(mapped.Pigment, pigment))
                return mapped.Map;

            lock (_mapping)
            {
                mapped = _mapped;

                if (mapped is not null && ReferenceEquals(mapped.Pigment, pigment))
                    return mapped.Map;

                int texels = pigment is ImagePigment picture ? picture.TexelsAcross : 0;
                int across = Math.Clamp(2 * texels, 256, 4096);
                bool finer = across >= texels;
                SkyMap map = SkyMap.Of((middle, side, down) =>
                {
                    Point place = new (middle.X, middle.Y, middle.Z);
                    Color color = (finer
                        ? pigment.GetTransformedColorFor(place)
                        : pigment.GetTransformedColorFor(place, new Footprint(side, down))) * Color;

                    return 0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue;
                }, across);

                Volatile.Write(ref _mapped, new Mapped(pigment, map));

                return map;
            }
        }
    }

    /// <summary>
    /// This method returns the color the sky is in the direction of the given sample.
    /// <para>
    /// The light's own color multiplies it rather than replacing it, so that a sky may be dimmed or
    /// tinted as a whole without giving up what it is a picture of.  Left alone it is white, and the
    /// sky is exactly what the pigment says.
    /// </para>
    /// </summary>
    /// <param name="sample">The sample being asked about.</param>
    /// <returns>The color of that piece of sky.</returns>
    public override Color ColorFor(LightSample sample)
    {
        if (Pigment is null)
            return Color;

        Vector heading = sample.Direction;

        return Pigment.GetTransformedColorFor(new Point(heading.X, heading.Y, heading.Z)) * Color;
    }

    /// <summary>
    /// This method returns the sky's light in the direction of the given sample, in whatever bands the
    /// render is carrying light in.  A sky worked out wavelength by wavelength is handed to a spectral
    /// render that way, and then tinted by the light's own color, as <see cref="ColorFor"/> tints it.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="sample">The sample being asked about.</param>
    /// <returns>The light of that piece of sky.</returns>
    public override TS EmittedToward<TS>(LightSample sample)
    {
        if (!TS.IsSpectral || Pigment is null || !Pigment.IsSpectral)
            return base.EmittedToward<TS>(sample);

        Vector heading = sample.Direction;

        return Pigment.GetTransformedLightFor<TS>(new Point(heading.X, heading.Y, heading.Z)) *
               Emitted<TS>();
    }

    /// <summary>
    /// This method spreads a sample evenly over the whole sphere of directions.
    /// </summary>
    /// <param name="alongOne">Where the sample falls, from nothing up to one.</param>
    /// <param name="alongTwo">The way round it falls, from nothing up to one.</param>
    /// <returns>The direction picked.</returns>
    private static Vector OverTheWholeSphere(double alongOne, double alongTwo)
    {
        // Even steps in the cosine are even steps in solid angle, which is what makes this an even
        // spread rather than one crowded at the poles.
        double cosine = 1 - 2 * alongOne;
        double sine = Math.Sqrt(Math.Max(0, 1 - cosine * cosine));
        double around = 2 * Math.PI * alongTwo;

        return new Vector(sine * Math.Cos(around), cosine, sine * Math.Sin(around));
    }

    /// <summary>
    /// This method spreads a sample evenly over the half of the sky a surface faces.
    /// </summary>
    /// <param name="normal">The way the surface faces.</param>
    /// <param name="alongOne">Where the sample falls, from nothing up to one.</param>
    /// <param name="alongTwo">The way round it falls, from nothing up to one.</param>
    /// <returns>The direction picked.</returns>
    private static Vector OverTheHalfFacing(Vector normal, double alongOne, double alongTwo)
    {
        double cosine = alongOne;
        double sine = Math.Sqrt(Math.Max(0, 1 - cosine * cosine));
        double around = 2 * Math.PI * alongTwo;

        // A pair of directions square to the normal and to each other, to swing the sample about it.
        // The one to build from must not be nearly the normal itself, or squaring it against it would
        // be all rounding error.
        Vector aside = Math.Abs(normal.X) < 0.9
            ? new Vector(1, 0, 0)
            : new Vector(0, 1, 0);
        Vector first = normal.Cross(aside).Unit;
        Vector second = normal.Cross(first);

        return (first * (sine * Math.Cos(around)) +
                second * (sine * Math.Sin(around)) +
                normal * cosine).Unit;
    }
}
