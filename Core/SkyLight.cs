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
        if (SharesAt(point) is { Openings: > 0 } shares)
            return MixedSample(point, index, normal, normal is null ? 4 : 1, shares);

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
        if (SharesAt(point) is { Openings: > 0 } shares)
            return MixedSample(point, index, null, 1, shares);

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
        double odds = OddsWithoutOpenings(direction, null);

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

        double odds = OddsWithoutOpenings(direction, normal);

        return new LightSample(
            direction, double.PositiveInfinity, odds > 0 ? Samples / (Math.PI * odds) / scale : 0);
    }

    /// <summary>
    /// This method picks one of the directions the sky is looked at from, at a point that sees it through a
    /// portal: most of them through the openings, by the sky's light through each, and the rest as the sky is
    /// looked at anywhere else.
    /// <para>
    /// **Every one is counted by how likely any of the ways was to pick it** (Veach's balance heuristic), as a
    /// picture's two ways are.  So nothing hangs on the openings being all the sky there is: a door left
    /// unmarked, a gap under the eaves, a point outside the room on the wrong side of a wall, are all still
    /// found by the rest, and only found less well.  The directions through the openings are turned by an amount
    /// that is a fixed function of the point, as a picture's are, so a room is lit with fine grain rather than
    /// with a few windows' shapes thrown across it.
    /// </para>
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <param name="index">Which sample this is.</param>
    /// <param name="normal">The surface normal, or <c>null</c> where the sky is looked at over the whole
    /// sphere.</param>
    /// <param name="scale">What a sample is worth for each π of the share of the sky it stands for; see
    /// <see cref="PictureSample"/>.</param>
    /// <param name="shares">How many of the samples are picked each way at this point.</param>
    /// <returns>The sample.</returns>
    private LightSample MixedSample(Point point, int index, Vector normal, double scale, Shares shares)
    {
        Vector direction;

        if (index < shares.Openings)
        {
            direction = ThroughTheOpenings.Pick(
                point, ((index + 0.5) / shares.Openings + Scene.ShiftFor(point, 151)) % 1,
                (index * 0.7548776662466927 + Scene.ShiftFor(point, 152)) % 1);

            if (direction is null)
                return new LightSample(Directions.Up, double.PositiveInfinity, 0);
        }
        else if (index < shares.Openings + shares.ByLight)
        {
            int byLight = index - shares.Openings;

            direction = Map.Pick(
                ((byLight + 0.5) / shares.ByLight + Scene.ShiftFor(point, 107)) % 1,
                (byLight * 0.7548776662466927 + Scene.ShiftFor(point, 108)) % 1);
        }
        else
        {
            int even = index - shares.Openings - shares.ByLight;
            double alongOne = (even + 0.5) / shares.Even;
            double alongTwo = even * 0.7548776662466927 % 1;

            // Turned point by point here, for a smooth sky too: the same few everywhere, the rare one of them
            // through an opening threw its shape on the floor, a step of a percent or so -- faint, but a step.
            alongOne = (alongOne + Scene.ShiftFor(point, 107)) % 1;
            alongTwo = (alongTwo + Scene.ShiftFor(point, 108)) % 1;

            direction = normal is null
                ? OverTheWholeSphere(alongOne, alongTwo)
                : OverTheHalfFacing(normal, alongOne, alongTwo);
        }

        double odds = MixedOdds(point, direction, normal, shares);

        return new LightSample(
            direction, double.PositiveInfinity, odds > 0 ? Samples / (Math.PI * odds) / scale : 0);
    }

    /// <summary>
    /// This record holds how many of the sky's samples are picked each way at a point: through the openings, by
    /// a picture's light, and evenly.
    /// </summary>
    private readonly record struct Shares(int Openings, int ByLight, int Even);

    /// <summary>
    /// This method works out how many of the sky's samples are picked each way at a point.
    /// <para>
    /// **Three in four go through the openings, where a point sees the sky through any**, and the rest are
    /// looked at as they are anywhere else -- half by a picture's light and half evenly, or all evenly for a smooth
    /// sky.  A point that sees nothing through them, or a sky with none, is looked at exactly as it always was.
    /// A sky of a single sample gives it to the openings where it may: one alone cannot be shared.
    /// </para>
    /// </summary>
    private Shares SharesAt(Point point)
    {
        int openings = Openings is { Count: > 0 } && ThroughTheOpenings is { } through && through.Faces(point)
            ? Samples == 1 ? 1 : Samples - Math.Max(1, Samples / 4)
            : 0;
        int rest = Samples - openings;
        int byLight = IsAPicture ? rest - rest / 2 : 0;

        return new Shares(openings, byLight, rest - byLight);
    }

    /// <summary>
    /// This method returns how likely the sky's samples, all of them together, are to look in a direction from a
    /// point that sees the sky through the openings.  See <see cref="OddsToward"/>.
    /// </summary>
    private double MixedOdds(Point point, Vector direction, Vector normal, Shares shares)
    {
        double evenOdds = normal is null
            ? 1 / (4 * Math.PI)
            : direction.Dot(normal) > 0 ? 1 / (2 * Math.PI) : 0;
        double odds = shares.Even * evenOdds + shares.Openings * ThroughTheOpenings.OddsOf(point, direction);

        return shares.ByLight > 0 ? odds + shares.ByLight * Map.OddsOf(direction) : odds;
    }

    /// <summary>
    /// This method returns how likely the sky's samples, all of them together, are to look in the given
    /// direction from a surface facing the given way: the sum, over both ways of picking, of how many
    /// samples are picked that way times the odds of each picking that direction, per unit of solid
    /// angle.  It is what a sample's worth is reckoned from, and what a rough surface's reflection weighs
    /// itself against when it finds the same piece of sky.
    /// </summary>
    /// <param name="point">The point the sky is looked at from.</param>
    /// <param name="direction">The direction, which must be a unit vector.</param>
    /// <param name="normal">The way the surface faces, or <c>null</c> where the even samples are spread
    /// over the whole sphere.</param>
    /// <returns>The sky's odds of looking that way.</returns>
    public double OddsToward(Point point, Vector direction, Vector normal)
    {
        Shares shares = SharesAt(point);

        return shares.Openings > 0
            ? MixedOdds(point, direction, normal, shares)
            : OddsWithoutOpenings(direction, normal);
    }

    /// <summary>
    /// This method returns how likely the sky's samples are to look in a direction where it is not looked at
    /// through any openings: evenly for a smooth sky, and for a picture half by its light and half evenly.
    /// </summary>
    private double OddsWithoutOpenings(Vector direction, Vector normal)
    {
        double evenOdds = normal is null
            ? 1 / (4 * Math.PI)
            : direction.Dot(normal) > 0 ? 1 / (2 * Math.PI) : 0;

        if (!IsAPicture)
            return Samples * evenOdds;

        int even = Samples / 2;
        int byLight = Samples - even;

        return even * evenOdds + byLight * Map.OddsOf(direction);
    }

    /// <summary>
    /// This property holds the openings the sky comes in through, when the scene marks any as portals; see
    /// <see cref="SkyPortals"/>.  Set by the scene once it is settled.
    /// </summary>
    public IReadOnlyList<SkyPortal> Openings
    {
        get => _openings;
        set
        {
            _openings = value;
            _opened = null;
        }
    }

    private IReadOnlyList<SkyPortal> _openings;

    // The sky laid out as seen through the openings, made the first time it is wanted, as the map is, and kept
    // with the pigment it was made from.
    private sealed record Opened(Pigment Pigment, SkyPortals Portals);

    private Opened _opened;

    /// <summary>
    /// This property holds the sky as seen through the openings, laid out the first time it is asked for.
    /// <para>
    /// **Each wall's map is about as fine as the sky's own map**, a picture's twice as fine as the picture up to
    /// a thousand cells across the half of the sky beyond the wall, and a smooth sky's a hundred and twenty-eight,
    /// each cell weighed by the sky at its middle.
    /// </para>
    /// </summary>
    private SkyPortals ThroughTheOpenings
    {
        get
        {
            Pigment pigment = Pigment;
            Opened opened = Volatile.Read(ref _opened);

            if (opened is not null && ReferenceEquals(opened.Pigment, pigment))
                return opened.Portals;

            lock (_mapping)
            {
                opened = _opened;

                if (opened is not null && ReferenceEquals(opened.Pigment, pigment))
                    return opened.Portals;

                int texels = pigment is ImagePigment picture ? picture.TexelsAcross : 0;
                int size = texels > 0 ? Math.Clamp(texels, 128, 1024) : 128;
                bool finer = 2 * size >= texels;
                SkyPortals portals = SkyPortals.Of(_openings, (middle, side, down) =>
                {
                    Color color = pigment switch
                    {
                        null => Color,
                        ImagePigment when finer => pigment.GetTransformedColorFor(PlaceOf(middle)) * Color,
                        ImagePigment => pigment.GetTransformedColorFor(PlaceOf(middle), new Footprint(side, down)) * Color,
                        _ => AveragedOver(pigment, middle, side, down) * Color
                    };

                    return 0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue;
                }, size);

                Volatile.Write(ref _opened, new Opened(pigment, portals));

                return portals;
            }
        }
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
        if (!TS.IsSpectral || Pigment is null)
            return base.EmittedToward<TS>(sample);

        // A sky of plain colors -- a gradient, a photograph -- under a light with a spectrum of its own (a
        // `temperature`) is the pigment's light times that spectrum, as an RGB render takes pigment times color;
        // handed to the base method it was the blackbody alone, the same from every direction, the sky dropped.
        if (!Pigment.IsSpectral && (Spectrum is null || sample.Carried is not null))
            return base.EmittedToward<TS>(sample);

        Vector heading = sample.Direction;

        return Pigment.GetTransformedLightFor<TS>(new Point(heading.X, heading.Y, heading.Z)) *
               Emitted<TS>();
    }

    /// <summary>
    /// This method returns where on the sphere a direction reads a sky.
    /// </summary>
    private static Point PlaceOf(Vector direction) => new (direction.X, direction.Y, direction.Z);

    /// <summary>
    /// This method returns a sky of patterns averaged over a patch of it, read at sixteen places spread evenly
    /// across it: a patch of a smooth sky may hold the edge of something small and bright -- the glow round a
    /// sun -- that its middle alone would miss or take for the whole.
    /// </summary>
    /// <param name="pigment">The sky.</param>
    /// <param name="middle">The direction of the patch's middle.</param>
    /// <param name="side">The patch's edge across.</param>
    /// <param name="down">Its edge down.</param>
    /// <returns>The sky's color averaged over the patch.</returns>
    private static Color AveragedOver(Pigment pigment, Vector middle, Vector side, Vector down)
    {
        double red = 0, green = 0, blue = 0;

        for (int across = 0; across < 4; across++)
        {
            for (int along = 0; along < 4; along++)
            {
                Vector there = (middle + side * ((across + 0.5) / 4 - 0.5) + down * ((along + 0.5) / 4 - 0.5)).Unit;
                Color color = pigment.GetTransformedColorFor(PlaceOf(there));

                red += color.Red;
                green += color.Green;
                blue += color.Blue;
            }
        }

        return new Color(red / 16, green / 16, blue / 16);
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
