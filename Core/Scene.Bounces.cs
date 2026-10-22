using System.Collections.Concurrent;
using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This part of the scene follows light as it bounces from one matte surface to the next: the light
/// off a sunlit wall onto the floor beside it, off a red rug onto the white wall above, off the sand
/// onto the shaded side of everything standing on it.
/// <para>
/// **It is followed from the surface, back the way the light came.**  A matte surface takes light from
/// everywhere it can see, so it looks out in a spread of directions -- more of them where it faces, in
/// proportion to how squarely, exactly as a matte surface weighs what it takes -- and whatever each finds
/// is light arriving.  Picked that way, each direction's light counts at its face value, and their
/// average is the light falling on the surface, over π; the surface's own color and its diffuse take
/// their share of it, as they take their share of a lamp's.  What a direction finds is everything the
/// renderer knows how to work out there: the lamps lighting that place, what it mirrors, what shines
/// through it, and -- if there are bounces left -- the light bounced onto it in turn.
/// </para>
/// <para>
/// **Only the first bounce spreads.**  The surface a ray from the eye first meets looks in many
/// directions; every surface met after that looks in one, and asks each lamp about one of its samples,
/// so that a path of light is followed rather than a tree of them, and the cost of a second bounce is
/// about that of the first rather than its square.  Each is a fair guess at the whole, picked afresh at
/// every place, so the many paths from the first surface average them out.
/// </para>
/// <para>
/// **What the renderer already looks for directly is not counted again.**  A direction that runs
/// straight out to the sky finds the sky, which a sky light has already counted -- and which, with no sky
/// light, is a backdrop rather than a light -- so it brings back nothing.  One that runs straight through
/// a glowing volume finds a glow its light's own samples have already counted, and brings back only what
/// the volume lets through.  Lamps, panels and suns cannot be met by a ray at all.  Anything met another
/// way -- the sky in a mirror, a lamp's glow on a wall -- is light no lamp's sample could have found, and
/// counts.  Nor does a search see what a shadow ray would pass, anywhere along its path, since that stands
/// for a light rather than being lit by one; see <see cref="PassedByBouncedLight"/>.
/// </para>
/// <para>
/// **And bounced light is light from one matte surface to the next.**  A surface met along a path of it
/// shows no highlight: light a shiny surface focuses onto a matte one -- a sun's glint off chrome onto the
/// sand, a lamp's off a glossy panel -- comes from a place a few degrees across as bright as the light
/// itself, which a bounce finds once in thousands, and every time it does puts a bright speck on whatever
/// was looking.  That light is a caustic, and is left to the caustics.
/// </para>
/// </summary>
public partial class Scene
{
    /// <summary>
    /// This property holds how many times light is followed from one matte surface to the next; see
    /// <see cref="RayTracer.General.RenderContext.DiffuseBounces"/>.  None, by default.
    /// </summary>
    public int DiffuseBounces { get; set; }

    /// <summary>
    /// This property holds how many directions the first matte surface along a path looks in for the
    /// light bounced onto it.
    /// </summary>
    public int DiffuseSamples { get; set; } = 32;

    /// <summary>
    /// This property holds how far the bounced light the scene caches may stray, as a share of the light,
    /// or nought for no cache; see <see cref="IrradianceCache{TS}"/>.  With a cache, the first matte
    /// surface along a path from the eye blends the light bounced onto it from records worked out with
    /// <see cref="DiffuseSamples"/> directions apiece, rather than working it out afresh.
    /// </summary>
    public double DiffuseCacheError { get; set; }

    // The cache for each kind of light rendered with: there is but one, unless a scene is rendered both ways.
    private readonly ConcurrentDictionary<Type, object> _caches = new ();

    // A record's reach is held to between these many pixels' width at its place, divided by the cache's
    // error: so that each covers at least a few pixels -- a record tucked into a corner, its directions
    // meeting walls an inch away, would otherwise cover next to none -- and none so many that a far wall
    // is one smear.
    private const double FewestPixels = 3;
    private const double MostPixels = 64;

    // A place the cache covers looks round itself for what is near in this share of the directions a
    // record looks in, and never fewer than the least.
    private const int NearShare = 16;
    private const int FewestNearSamples = 16;

    /// <summary>
    /// This field notes, while the cache is being filled, where the place being looked at falls in the
    /// order the pass's records join in, and how many records it has offered so far.
    /// </summary>
    [ThreadStatic]
    private static long _pretraceKey;

    [ThreadStatic]
    private static int _pretraceOrdinal;

    [ThreadStatic]
    private static bool _pretracing;

    /// <summary>
    /// This field holds how far the search for bounced light now looking went before it met something,
    /// while its distance is being measured; see <see cref="ArrivingAt{TS}"/>.
    /// </summary>
    [ThreadStatic]
    private static double _bounceMet;

    [ThreadStatic]
    private static bool _measuring;

    /// <summary>
    /// This field holds the ray a place the cache covers is looking round itself along, while it is
    /// looking, and how far it looks: whatever the ray meets beyond that, or the sky, is far light, which
    /// the cache gives; see <see cref="BeyondTheNear"/>.
    /// </summary>
    [ThreadStatic]
    private static Ray _nearRay;

    [ThreadStatic]
    private static double _nearLimit;

    /// <summary>
    /// This field holds how much of the ray a place is looking round itself along went beyond the near,
    /// all of it unless strands nearer covered part of it, for the far light to make up.
    /// </summary>
    [ThreadStatic]
    private static double _escaped;

    /// <summary>
    /// This method fills the cache of bounced light, in passes over ever finer grids of the picture's
    /// pixels, each pass reading only what the passes before it set down; see
    /// <see cref="IrradianceCache{TS}"/>.
    /// </summary>
    /// <typeparam name="TS">The kind of light the picture is rendered with.</typeparam>
    /// <param name="rayFor">The ray through the middle of a pixel.</param>
    /// <param name="width">How many pixels across the picture is.</param>
    /// <param name="height">How many down.</param>
    /// <returns>How many records the cache holds.</returns>
    public int FillTheBounceCache<TS>(Func<int, int, Ray> rayFor, int width, int height)
        where TS : struct, ISpectrum<TS>
    {
        if (DiffuseBounces <= 0 || DiffuseCacheError <= 0)
            return 0;

        IrradianceCache<TS> cache = CacheFor<TS>();
        long pass = 0;

        for (int stride = 32; stride >= 1; stride /= 2, pass++)
        {
            int across = (width + stride - 1) / stride;
            int down = (height + stride - 1) / stride;
            long currentPass = pass;

            Parallel.For(0, across * down,
                new ParallelOptions { MaxDegreeOfParallelism = System.Environment.ProcessorCount },
                index =>
                {
                    int x = Math.Min(width - 1, index % across * stride + stride / 2);
                    int y = Math.Min(height - 1, index / across * stride + stride / 2);

                    _pretraceKey = ((currentPass << 32) | ((long) y * width + x)) << 8;
                    _pretraceOrdinal = 0;
                    _pretracing = true;

                    try
                    {
                        GetColorFor<TS>(rayFor(x, y), TraceDepth);
                    }
                    finally
                    {
                        _pretracing = false;
                    }
                });

            cache.EndPass();
        }

        return cache.Count;
    }

    /// <summary>
    /// This method returns the cache of bounced light for a kind of light, or <c>null</c> where the scene
    /// keeps none.
    /// </summary>
    internal IrradianceCache<TS> CacheFor<TS>()
        where TS : struct, ISpectrum<TS>
    {
        if (DiffuseCacheError <= 0)
            return null;

        return (IrradianceCache<TS>) _caches.GetOrAdd(typeof(TS), _ => new IrradianceCache<TS>(DiffuseCacheError));
    }

    /// <summary>
    /// This field holds how many bounces the path being followed has already been through.
    /// </summary>
    [ThreadStatic]
    private static int _bounced;

    /// <summary>
    /// This field holds the straight run a matte surface's search for bounced light is looking along,
    /// while it is looking; see <see cref="SeenAlongABounce"/>.
    /// </summary>
    [ThreadStatic]
    private static BounceRun? _bouncing;

    /// <summary>
    /// This record holds one straight run of a search for bounced light: where it set off, and which way.
    /// </summary>
    private readonly record struct BounceRun(Point Origin, Vector Direction);

    /// <summary>
    /// This method works out the light bounced onto a surface point, as the surface takes it: its own
    /// color and diffuse share of the light arriving from everything it can see.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="intersection">The surface point.</param>
    /// <param name="material">The material it is shaded with.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed, which the light
    /// found along each direction may spend.</param>
    /// <returns>The bounced light the surface sends toward the eye, before its transparency and film
    /// have their say.</returns>
    private TS BouncedOnto<TS>(Intersection intersection, Material material, int remaining)
        where TS : struct, ISpectrum<TS>
    {
        if (_bounced >= DiffuseBounces || material.Diffuse <= 0)
            return TS.Black;

        TS pigment = TS.FromReflectance(material.GetColorFor(
            intersection.Surface, intersection.LitPoint, intersection.Normal, intersection.Footprint,
            intersection.Portals, new Viewing(intersection.Eye, intersection.Normal)));

        if (pigment.IsBlack)
            return TS.Black;

        // The first matte surface along a path from the eye reads the cache, where the scene keeps one; every
        // surface after it, along a bounce, follows its one direction as before.  A wire keeps no records,
        // being too fine to stand for the places round it, and looks round itself as a covered place does
        // for what is near it.
        bool first = _bounced == 0 && !_spreading && TS.Count > 1;

        if (intersection is HairIntersection wire)
        {
            // A wire finer than the ray looks round itself in proportion to its share of it; see RaysOf.
            (int count, double weight) = RaysOf(wire, !first
                ? 1
                : CacheFor<TS>() is not null
                    ? Math.Max(FewestNearSamples, DiffuseSamples / NearShare)
                    : Math.Max(1, DiffuseSamples), 134);

            return count == 0
                ? TS.Black
                : pigment * ArrivingAt<TS>(intersection, remaining, count) * (material.Diffuse * weight);
        }

        TS arriving = first && CacheFor<TS>() is { } cache
            ? CachedArrivingAt(cache, intersection, remaining)
            : ArrivingAt<TS>(intersection, remaining, !first ? 1 : Math.Max(1, DiffuseSamples));

        return pigment * arriving * material.Diffuse;
    }

    /// <summary>
    /// This method returns the light arriving at a surface point with the help of the cache: what is near
    /// found by looking round the point itself, and the far light blended from the records covering it --
    /// or, where none does, the whole worked out afresh, and offered to the cache as a record of its own
    /// while it is being filled.
    /// </summary>
    private TS CachedArrivingAt<TS>(IrradianceCache<TS> cache, Intersection intersection, int remaining)
        where TS : struct, ISpectrum<TS>
    {
        if (cache.TryBlend(intersection.Point, intersection.Normal, out IrradianceCache<TS>.Blend blend))
        {
            // Filling the cache, nothing is wanted of a place covered already but that it is.
            if (_pretracing)
                return blend.Far;

            // Where no record covering the place saw anything far, it looks as far as it likes.
            return ArrivingAt(
                intersection, remaining, Math.Max(FewestNearSamples, DiffuseSamples / NearShare),
                near: blend.HasFar ? blend.Near : double.PositiveInfinity, far: blend.Far);
        }

        int count = Math.Max(1, DiffuseSamples);
        List<(double Met, TS Light)> looks = new (count);
        TS light = ArrivingAt(intersection, remaining, count, looks);

        if (_pretracing)
        {
            // The reach is the harmonic mean of how far the directions went before meeting something, which
            // the nearest things rule, held to a span of pixels at the place, as far as the place has a
            // pixel's width to measure by.
            double nearness = 0;

            foreach ((double met, _) in looks)
                nearness += 1 / met;

            double reach = nearness > 0 ? count / nearness : double.PositiveInfinity;
            double pixel = 2 * intersection.FootprintRadius;

            if (pixel > 0)
                reach = Math.Clamp(reach, FewestPixels * pixel / cache.Error, MostPixels * pixel / cache.Error);
            else if (double.IsPositiveInfinity(reach))
                reach = 1e9;

            // The record keeps only the far light: the directions that went beyond its near distance.
            double near = cache.NearOf(reach);
            TS far = TS.Black;
            int open = 0;

            foreach ((double met, TS seen) in looks)
            {
                if (met > near)
                {
                    far += seen;
                    open++;
                }
            }

            cache.Offer(_pretraceKey + _pretraceOrdinal++, new IrradianceCache<TS>.Record(
                intersection.Point, intersection.Normal, open > 0 ? far * (1.0 / open) : TS.Black,
                (double) open / count, reach));
        }

        return light;
    }

    /// <summary>
    /// This method works out the light arriving at a surface point from everything it can see, over π:
    /// the average of what it finds looking out in directions picked in proportion to how squarely they
    /// meet it.
    /// </summary>
    /// <param name="intersection">The surface point.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <param name="count">How many directions to look in.</param>
    /// <param name="looks">If given, where to note how far each direction went before meeting something,
    /// and the light it found.</param>
    /// <param name="near">How far to look along each direction: a direction meeting nothing nearer finds
    /// <paramref name="far"/> instead of looking further.</param>
    /// <param name="far">The light a direction finds beyond <paramref name="near"/>.</param>
    private TS ArrivingAt<TS>(
        Intersection intersection, int remaining, int count, List<(double Met, TS Light)> looks = null,
        double near = double.PositiveInfinity, TS far = default)
        where TS : struct, ISpectrum<TS>
    {
        Vector normal = intersection.Normal;
        (Vector across, Vector along) = Microfacets.FrameAround(normal);

        // The directions set off from the side the eye is on.  For a surface met from inside that is not
        // the point a lamp's light is worked out from -- which, for anything with an inside, stays just
        // outside it, so that glass met from within takes the light falling on its outer face -- but the
        // point just inside.
        Point origin = intersection.Inside ? intersection.UnderPoint : intersection.LitPoint;

        // The directions are spread as evenly as they can be and turned by an amount that is a fixed
        // function of the place, as a rough surface's reflections are: neighboring places look in
        // different directions, and the same place in the same ones every time.
        double turnOut = ShiftFor(intersection.Point, 109);
        double turnRound = ShiftFor(intersection.Point, 110);
        TS sum = TS.Black;
        bool wasSpreading = _spreading;
        BounceRun? wasBouncing = _bouncing;
        bool wasMeasuring = _measuring;
        double wasMet = _bounceMet;
        Ray wasNearRay = _nearRay;
        double wasNearLimit = _nearLimit;
        double wasEscaped = _escaped;
        bool looksNear = !double.IsPositiveInfinity(near);

        _bounced++;
        _spreading = true;

        try
        {
            for (int index = 0; index < count; index++)
            {
                double first = (index + 0.5) / count + turnOut;
                double second = index * 0.6180339887498949 + turnRound;

                first -= Math.Floor(first);
                second -= Math.Floor(second);

                // A cosine-weighted direction: even over the disc under the hemisphere, lifted onto it.
                double sine = Math.Sqrt(first);
                double cosine = Math.Sqrt(Math.Max(0, 1 - first));
                double around = 2 * Math.PI * second;
                Vector direction = (across * (sine * Math.Cos(around)) + along * (sine * Math.Sin(around)) +
                                    normal * cosine).Unit;
                Ray ray = new (
                    origin, direction, intersection.TimeIndex, intersection.ConeSpread, intersection.ConeTravelled);

                _bouncing = new BounceRun(origin, direction);
                _measuring = looks is not null;
                _bounceMet = double.PositiveInfinity;
                _nearRay = looksNear ? ray : null;
                _nearLimit = near;
                _escaped = 0;

                // A bounce spends none of the mirrors' allowance: the bounces have their own, and a path that
                // mirrors and bounces by turns is held to both.  Spending it, a scene asking for six
                // bounces would quietly have been given as many as the mirrors' allowance left.
                TS light = GetColorFor<TS>(ray, remaining);

                if (_escaped > 0)
                    light += far * _escaped;

                sum += light;
                looks?.Add((_bounceMet, light));
            }
        }
        finally
        {
            _bounced--;
            _spreading = wasSpreading;
            _bouncing = wasBouncing;
            _measuring = wasMeasuring;
            _bounceMet = wasMet;
            _nearRay = wasNearRay;
            _nearLimit = wasNearLimit;
            _escaped = wasEscaped;
        }

        return sum * (1.0 / count);
    }

    /// <summary>
    /// This method reports whether a ray a place is looking round itself along has gone beyond what is
    /// near, to a crossing or -- given none -- to the sky, noting how much of it did; the far light the
    /// cache gives makes that up.
    /// </summary>
    /// <param name="ray">The ray.</param>
    /// <param name="distance">How far along it the crossing is, or infinity for the sky.</param>
    /// <param name="left">How much of the ray is left uncovered by strands nearer.</param>
    /// <returns><c>true</c>, if the ray looks no further.</returns>
    private static bool BeyondTheNear(Ray ray, double distance, double left)
    {
        if (_nearRay is null || !ReferenceEquals(ray, _nearRay) || distance <= _nearLimit)
            return false;

        _escaped = left;

        return true;
    }

    /// <summary>
    /// This method notes how far a search for bounced light went before it met something, when it is being
    /// measured and this is the first thing it met on its straight run.
    /// </summary>
    private static void NoteWhereABounceMet(Ray ray, Intersection hit)
    {
        if (_measuring && double.IsPositiveInfinity(_bounceMet) && SeenAlongABounce(ray))
            _bounceMet = (ray.At(hit.Distance) - _bouncing!.Value.Origin).Magnitude;
    }

    /// <summary>
    /// This method reports whether the sky, or a glowing volume that gives light, met along a ray is light
    /// counted another way, and so to be found black: on a search for bounced light's straight run, which the
    /// light's own samples count; and anywhere along a path of bounced light once a scene has caustics, if
    /// the sky or the glow is a light, whose photons carry everything it gives a matte surface by way of
    /// mirrors and glass.  The sky seen in a mirror from the floor is the photons' then, and found by a
    /// bounce as well, would be counted twice.  A sky with no sky light sends no photons, and is found.
    /// </summary>
    /// <param name="ray">The ray.</param>
    /// <param name="isALight">Whether what the ray meets is a light, and so sends photons.</param>
    /// <returns><c>true</c>, if what the ray meets of such a light counts for nothing here.</returns>
    private bool CountedElsewhere(Ray ray, bool isALight)
    {
        return SeenAlongABounce(ray) || (isALight && _caustics is not null && _bounced > 0);
    }

    /// <summary>
    /// This method reports whether a ray is a search for bounced light still on its straight run, on which
    /// whatever the renderer looks for directly -- the sky, a glowing volume -- has been counted already.
    /// </summary>
    /// <param name="ray">The ray.</param>
    /// <returns><c>true</c>, if the ray is on such a run.</returns>
    private static bool SeenAlongABounce(Ray ray)
    {
        return _bouncing is { } run && OnRun(ray, run.Origin, run.Direction);
    }

    /// <summary>
    /// This method reports whether a path of bounced light goes straight past what it has met, as a shadow
    /// ray would: something that casts no shadow, or the bulb round a light.  It holds all along the path,
    /// off a mirror as much as straight out -- a painted sun seen in chrome from the sand is as much the
    /// sun's light counted twice as one seen straight up.
    /// <para>
    /// **Bounced light is light on its way from one surface to another, as a shadow ray's is**, so it passes
    /// what a shadow ray passes.  Those are just the things that stand for a light rather than being lit:
    /// the glowing bulb round a lamp, whose glow is the lamp's light, already counted; a painted sun's disc,
    /// set out with no shadow so as not to stand in the sun's way, whose glow is the sun's.  Seen, each would
    /// be its light counted a second time -- and, a few texels across and as bright as the light it stands
    /// for, found by one bounce in thousands and scattered over everything near as bright specks.
    /// </para>
    /// </summary>
    /// <param name="hit">What the search met.</param>
    /// <returns><c>true</c>, if the search goes on past it.</returns>
    private bool PassedByBouncedLight(Intersection hit)
    {
        if (hit.Surface.NoShadow || hit.Portals.CastsNoShadow)
            return true;

        foreach (Light light in Lights)
        {
            if (light.Housing is { } housing && housing.Contains((hit.Surface, hit.Portals)))
                return true;
        }

        return false;
    }

    /// <summary>
    /// This method reports whether a medium's glow is one a light already looks at for itself.
    /// </summary>
    private bool GlowsAsALight(Medium medium)
    {
        foreach (Light light in Lights)
        {
            if (light is VolumeLight glow && ReferenceEquals(glow.Medium, medium))
                return true;
        }

        return false;
    }
}
