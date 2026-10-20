using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This part of the scene works out the light that comes up from under a surface whose stuff scatters it --
/// wax, marble, jade, skin -- by following it on its wanderings inside.
/// <para>
/// **A walk is the light's path taken backwards.**  From where the eye's ray meets the surface, a walk goes in
/// the way a matte surface would send light out, travels a distance drawn by how often the stuff turns light,
/// is turned every way alike, and goes on so until it crosses back out; where it comes out, the surface is
/// lit as a matte one would be, and that light, less what the turns took on the way, is what the eye sees
/// at the start.  Lit evenly, a deep slab of the stuff then shows exactly its pigment's color, which is what
/// <see cref="Subsurface"/>'s table was worked out to do.  A thin edge lets the walk out of its far side,
/// which is how light comes through a candle's rim or an ear.
/// </para>
/// <para>
/// **Each color travels its own distance.**  So at each step the walk draws its distance by one color, picked
/// by how much each still carries, and weighs what it carries by how likely that distance was over all of
/// them -- the balance heuristic, one step at a time, as Cycles does it.  Picked evenly instead, red carried
/// ten times as far as blue came out a seventh too dark, and blue a tenth: each step grew one color's weight
/// and shrank the other's, until a few walks carried nearly all the light and most walks too little.
/// </para>
/// </summary>
public partial class Scene
{
    /// <summary>
    /// This property holds how many walks a surface whose light comes up from under it takes, the first time
    /// a ray from the eye meets one; one thereafter.
    /// </summary>
    public int SubsurfaceSamples { get; set; } = 64;

    // A walk is given up after this many turns: the light it carries by then is next to nothing for any color
    // a surface may be given (a thousandth of a pale one's), and a walk need not wander for ever to find so.
    private const int MostTurnsUnder = 1024;

    // The matte surface a walk's way out is lit as: all of the light, none mirrored.
    private static readonly Material Beneath = new ()
    {
        Pigment = new Pigments.SolidPigment(Colors.White), Ambient = 0, Diffuse = 1, Specular = 0
    };

    /// <summary>
    /// This method notes whether the light a surface shows at a place comes up from under it.  A surface met
    /// from inside, or that lets light through, is lit where the light lands, as it always was; a sheet has
    /// no inside to walk in.
    /// </summary>
    private static bool ComesUpFromUnder(Intersection intersection, Material material, double transparency)
    {
        return material.Subsurface is not null && material.Diffuse > 0 && transparency <= 0 &&
               !intersection.Inside && !intersection.Surface.IsASheet;
    }

    /// <summary>
    /// This method notes whether the light a sheet shows at a place is the light that wanders through it --
    /// a leaf, a sheet of paper, a lampshade.  A sheet reached through a shape shared in many places is walked
    /// as any other is, since a sheet's walk needs nothing of where it stands.
    /// </summary>
    private static bool PassesThroughTheSheet(Intersection intersection, Material material, double transparency)
    {
        return material.Subsurface is not null && material.Diffuse > 0 && transparency <= 0 &&
               intersection.Surface.IsASheet;
    }

    /// <summary>
    /// This method works out the light that comes up from under a surface at a place, toward the eye: the
    /// average over its walks of the light at each walk's way out, less what the walk's turns took.
    /// </summary>
    /// <param name="intersection">The place.</param>
    /// <param name="material">The surface's material.</param>
    /// <param name="ownShare">The share of the light the surface keeps, as against mirroring.</param>
    /// <param name="film">How a film on the surface shares the light out, if it has one.</param>
    /// <returns>The light that comes up from under it.</returns>
    private TS UnderTheSurface<TS>(
        Intersection intersection, Material material, double ownShare, FilmShares<TS>? film)
        where TS : struct, ISpectrum<TS>
    {
        Subsurface subsurface = material.Subsurface;
        TS color = TS.FromReflectance(material.GetColorFor(intersection)) * material.Diffuse;
        TS albedo = Subsurface.AlbedoOf(color);
        TS extinction = subsurface.ExtinctionOf<TS>();
        Surface container = intersection.Surface;
        int walks = _bounced == 0 && !_spreading ? Math.Max(1, SubsurfaceSamples) : 1;
        Vector inward = -intersection.Normal;
        (Vector across, Vector along) = Microfacets.FrameAround(inward);
        ulong place = (ulong) (ShiftFor(intersection.Point, 131) * ulong.MaxValue);
        List<Intersection> crossings = [];
        TS gathered = TS.Black;

        for (int walk = 0; walk < walks; walk++)
        {
            ulong key = Mix(place + (ulong) walk * 0x9E3779B97F4A7C15);
            Point at = intersection.UnderPoint;
            Vector going = Spread(inward, across, along, Draw(key, 0), Draw(key, 1));
            TS carried = TS.White;

            for (int turn = 0; turn < MostTurnsUnder; turn++)
            {
                int choice = 4 * turn + 2;

                // Each color is picked in proportion to what it still carries, so the colors carrying most are
                // the ones whose distances are drawn most; picked evenly, the weights of colors travelling far
                // and near grew and shrank by turns until a few walks carried nearly all of it.
                TS odds = carried * albedo;
                double total = odds.Sum;

                if (total <= 0)
                    break;

                odds *= 1 / total;

                int band = PickedBy(odds, Draw(key, choice));
                double rate = extinction[band];
                double flight = rate > 0 ? -Math.Log(1 - Draw(key, choice + 1)) / rate : double.PositiveInfinity;
                Intersection crossing = FirstCrossingOf(
                    container, new Ray(at, going, intersection.TimeIndex), intersection.Portals, crossings);

                // A shape that does not close round the walk lets it go; whatever is out that way is lost to it.
                if (crossing is null)
                    break;

                if (flight < crossing.Distance)
                {
                    double likely = 0;

                    for (int each = 0; each < TS.Count; each++)
                        likely += odds[each] * extinction[each] * Math.Exp(-extinction[each] * flight);

                    for (int each = 0; each < TS.Count; each++)
                        carried[each] *= albedo[each] * extinction[each] * Math.Exp(-extinction[each] * flight) / likely;

                    at += going * flight;
                    going = AnyWay(Draw(key, choice + 2), Draw(key, choice + 3));

                    // A walk carrying little is given up by chance, its light given to the walks that go on.
                    double most = 0;

                    for (int each = 0; each < TS.Count; each++)
                        most = Math.Max(most, carried[each]);

                    if (most < 0.25)
                    {
                        if (most <= 0 || Draw(key, choice + 4000) >= most / 0.25)
                            break;

                        carried *= 0.25 / most;
                    }

                    continue;
                }

                // Out: what was carried, weighed by how likely getting this far was over all the colors.
                double leaving = 0;

                for (int each = 0; each < TS.Count; each++)
                    leaving += odds[each] * Math.Exp(-extinction[each] * crossing.Distance);

                for (int each = 0; each < TS.Count; each++)
                    carried[each] *= Math.Exp(-extinction[each] * crossing.Distance) / leaving;

                gathered += carried * LitWhereItLeaves<TS>(crossing, new Ray(at, going, intersection.TimeIndex), crossings, key);

                break;
            }
        }

        TS light = gathered * (1.0 / walks);

        if (ownShare != 1)
            light *= ownShare;

        if (film is { } passing)
            light *= passing.Passed;

        return light;
    }

    /// <summary>
    /// This method works out the light a sheet whose stuff scatters light shows at a place: what falls on its
    /// near side and wanders back out of it, and what falls on its far side and wanders through.
    /// <para>
    /// **A sheet has no inside to walk in, so the walks go through a slab as thick as the sheet is.**  Thinner
    /// than anything is seen from, a walk's way across it is nothing worth following, and only how deep it has
    /// gone matters: a walk that comes back out of the near face is light the sheet gave back, and one that
    /// comes out of the far face light it let through.  How much of each comes of the walks, and the light on
    /// each side of the sheet is asked once.  A leaf with the sun behind it glows; one thick enough to have no
    /// far side to speak of shows its pigment, as a deep slab does.
    /// </para>
    /// </summary>
    /// <param name="intersection">The place.</param>
    /// <param name="material">The sheet's material.</param>
    /// <param name="ownShare">The share of the light the sheet keeps, as against mirroring.</param>
    /// <param name="film">How a film on the sheet shares the light out, if it has one.</param>
    /// <returns>The light the sheet shows.</returns>
    private TS ThroughTheSheet<TS>(
        Intersection intersection, Material material, double ownShare, FilmShares<TS>? film)
        where TS : struct, ISpectrum<TS>
    {
        TS color = TS.FromReflectance(material.GetColorFor(intersection)) * material.Diffuse;
        int walks = _bounced == 0 && !_spreading ? Math.Max(1, SubsurfaceSamples) : 1;
        (TS reflected, TS passed) = SharedOutBy(
            Subsurface.AlbedoOf(color), material.Subsurface.ExtinctionOf<TS>(), material.Subsurface.SheetThickness,
            walks, (ulong) (ShiftFor(intersection.Point, 137) * ulong.MaxValue));
        double step = DoubleExtensions.Epsilon * intersection.Surface.SelfOffsetScale;
        TS light = reflected * LitAsMatte<TS>(intersection, intersection.LitPoint, intersection.Normal);

        if (passed.Sum > 0)
            light += passed * LitAsMatte<TS>(intersection, intersection.Point - intersection.Eye * step, -intersection.Normal);

        if (ownShare != 1)
            light *= ownShare;

        if (film is { } passing)
            light *= passing.Passed;

        return light;
    }

    /// <summary>
    /// This method walks light through a slab of the given thickness and returns the share of each band that
    /// comes back out of the face it went in at, and the share that comes out of the other.
    /// </summary>
    internal static (TS Reflected, TS Passed) SharedOutBy<TS>(
        TS albedo, TS extinction, double thickness, int walks, ulong place)
        where TS : struct, ISpectrum<TS>
    {
        TS reflected = TS.Black, passed = TS.Black;

        for (int walk = 0; walk < walks; walk++)
        {
            ulong key = Mix(place + (ulong) walk * 0x9E3779B97F4A7C15);
            double depth = 0;
            double heading = Math.Sqrt(1 - Draw(key, 0));
            TS carried = TS.White;

            for (int turn = 0; turn < MostTurnsUnder; turn++)
            {
                int choice = 4 * turn + 2;
                TS odds = carried * albedo;
                double total = odds.Sum;

                if (total <= 0)
                    break;

                odds *= 1 / total;

                int band = PickedBy(odds, Draw(key, choice));
                double rate = extinction[band];
                double flight = rate > 0 ? -Math.Log(1 - Draw(key, choice + 1)) / rate : double.PositiveInfinity;
                double edge = heading > 0 ? (thickness - depth) / heading
                    : heading < 0 ? depth / -heading
                    : double.PositiveInfinity;

                if (flight < edge)
                {
                    double likely = 0;

                    for (int each = 0; each < TS.Count; each++)
                        likely += odds[each] * extinction[each] * Math.Exp(-extinction[each] * flight);

                    for (int each = 0; each < TS.Count; each++)
                        carried[each] *= albedo[each] * extinction[each] * Math.Exp(-extinction[each] * flight) / likely;

                    depth += heading * flight;
                    heading = 2 * Draw(key, choice + 2) - 1;

                    double most = 0;

                    for (int each = 0; each < TS.Count; each++)
                        most = Math.Max(most, carried[each]);

                    if (most < 0.25)
                    {
                        if (most <= 0 || Draw(key, choice + 4000) >= most / 0.25)
                            break;

                        carried *= 0.25 / most;
                    }

                    continue;
                }

                double leaving = 0;

                for (int each = 0; each < TS.Count; each++)
                    leaving += odds[each] * Math.Exp(-extinction[each] * edge);

                for (int each = 0; each < TS.Count; each++)
                    carried[each] *= Math.Exp(-extinction[each] * edge) / leaving;

                if (heading > 0)
                    passed += carried;
                else
                    reflected += carried;

                break;
            }
        }

        return (reflected * (1.0 / walks), passed * (1.0 / walks));
    }

    /// <summary>
    /// This method works out the light falling on one side of a sheet at a place, as a matte surface that
    /// keeps all of it would show it, every sample of every light asked as a lit surface asks them.
    /// </summary>
    private TS LitAsMatte<TS>(Intersection intersection, Point point, Vector normal)
        where TS : struct, ISpectrum<TS>
    {
        TS light = TS.Black;

        foreach (Light each in Lights)
        {
            Light placed = each.At(intersection.TimeIndex);
            int count = placed.SampleCount;
            TS sum = TS.Black;

            for (int index = 0; index < count; index++)
            {
                LightSample sample = placed.SampleToward(point, index, normal);

                if (sample.Direction.Dot(normal) <= 0)
                    continue;

                TS reaching = GetLightReaching<TS>(
                    point, sample.Direction, sample.Distance, intersection.TimeIndex, null, false,
                    intersection.FootprintRadius, placed.Housing);

                sum += placed.ApplyPhong(
                    point, normal, normal, intersection.Surface, sample, reaching, material: Beneath, highlights: false);
            }

            light += count == 1 ? sum : sum * (1.0 / count);
        }

        return light;
    }

    /// <summary>
    /// This method works out the light falling on the surface where a walk leaves it, as a matte surface
    /// that keeps all of it would show it: one of each light's samples, picked by the walk.
    /// </summary>
    private TS LitWhereItLeaves<TS>(Intersection crossing, Ray ray, List<Intersection> crossings, ulong key)
        where TS : struct, ISpectrum<TS>
    {
        // Prepared as met from inside, the place's normal is turned in toward the walk and its over point is
        // outside, which is where the light it is lit by is asked from.
        crossing.PrepareUsing(ray, crossings, Environment.IndexOfRefraction, TS.Wavelength);

        Vector outward = -crossing.Normal;
        Point lit = crossing.OverPoint;
        TS light = TS.Black;

        for (int index = 0; index < Lights.Count; index++)
        {
            Light each = Lights[index].At(crossing.TimeIndex);
            int count = each.SampleCount;
            int one = count > 1 ? Math.Min(count - 1, (int) (Draw(key, 8000 + index) * count)) : 0;
            LightSample sample = each.SampleToward(lit, one, outward);

            if (sample.Direction.Dot(outward) <= 0)
                continue;

            TS reaching = GetLightReaching<TS>(
                lit, sample.Direction, sample.Distance, crossing.TimeIndex, null, false, 0, each.Housing);

            light += each.ApplyPhong(
                lit, outward, outward, crossing.Surface, sample, reaching, material: Beneath, highlights: false);
        }

        return light;
    }

    /// <summary>
    /// This method returns the band a number from nought up to one picks, each band taking its share of the
    /// range by the given odds, which add to one.
    /// </summary>
    private static int PickedBy<TS>(TS odds, double number)
        where TS : struct, ISpectrum<TS>
    {
        for (int band = 0; band < TS.Count - 1; band++)
        {
            number -= odds[band];

            if (number < 0)
                return band;
        }

        return TS.Count - 1;
    }

    /// <summary>
    /// This method returns where a ray inside a surface first crosses it, or null if it never does, the ray
    /// and the distance along it being the world's.
    /// <para>
    /// **A surface is crossed in its parent's space.**  It applies its own transform to a ray and no other, so
    /// one standing in a group, or in a shape shared in many places, is handed the ray carried there first --
    /// up through its parents and the instances it was reached through, as its points are.  The way is carried
    /// as a step of one rather than made a unit again, so a distance along it is still a distance in the world.
    /// The crossing is stamped with those instances, so that its normal is carried back out through them.
    /// </para>
    /// </summary>
    private static Intersection FirstCrossingOf(
        Surface container, Ray ray, Portals portals, List<Intersection> crossings)
    {
        crossings.Clear();
        container.Intersect(IntoTheSpaceOf(container, ray, portals), crossings);

        Intersection first = null;

        foreach (Intersection crossing in crossings)
        {
            if (crossing.Distance > 1e-9 && (first is null || crossing.Distance < first.Distance))
                first = crossing;
        }

        if (first is not null)
            first.Portals = portals;

        return first;
    }

    /// <summary>
    /// This method carries a ray from the world into the space a surface's own transform is applied in: its
    /// parent's, reached up through its parents and the instances it was met through.  A walk under a surface
    /// and a medium's edges both ask for it; see <see cref="FirstCrossingOf"/>.
    /// </summary>
    private static Ray IntoTheSpaceOf(Surface surface, Ray ray, Portals portals)
    {
        if (surface.Parent is null && portals.IsEmpty)
            return ray;

        Point Carried(Point point) => surface.Parent is { } parent
            ? parent.WorldToSurface(point, ray.TimeIndex, portals)
            : portals.Through.WorldToSurface(point, ray.TimeIndex, portals.Outward);

        Point from = Carried(ray.Origin);

        return new Ray(from, Carried(ray.Origin + ray.Direction) - from, ray.TimeIndex);
    }

    // A way into the surface, spread as a matte surface spreads the light it gives out: by the cosine.
    private static Vector Spread(Vector normal, Vector across, Vector along, double first, double second)
    {
        double radius = Math.Sqrt(first);
        double angle = 2 * Math.PI * second;

        return (across * (radius * Math.Cos(angle)) + along * (radius * Math.Sin(angle)) +
                normal * Math.Sqrt(Math.Max(0, 1 - first))).Unit;
    }

    // A way picked evenly over every way there is.
    private static Vector AnyWay(double first, double second)
    {
        double z = 2 * first - 1;
        double ring = Math.Sqrt(Math.Max(0, 1 - z * z));
        double angle = 2 * Math.PI * second;

        return new Vector(ring * Math.Cos(angle), ring * Math.Sin(angle), z);
    }

    // The walk's choices: a number from nought up to one for each, a hash of the walk and which choice it is.
    private static double Draw(ulong key, int choice) =>
        (Mix(key ^ ((ulong) choice + 1) * 0xD1B54A32D192ED03) >> 11) * (1.0 / (1UL << 53));

    private static ulong Mix(ulong value)
    {
        value = (value ^ value >> 30) * 0xbf58476d1ce4e5b9;
        value = (value ^ value >> 27) * 0x94d049bb133111eb;

        return value ^ value >> 31;
    }
}
