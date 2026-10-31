using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

using RayTracer.Renderer;

namespace RayTracer.Core;

/// <summary>
/// This class represents a world of things to render.
/// </summary>
public partial class Scene : NamedThing, IDisposable
{
    /// <summary>
    /// This list holds the collection of cameras in the scene.
    /// </summary>
    public List<Camera> Cameras { get; } = [];

    /// <summary>
    /// This list holds the collection of lights in the scene.
    /// </summary>
    public List<Light> Lights { get; } = [];

    /// <summary>
    /// This list holds the collection of surfaces (shapes) in the scene.
    /// </summary>
    public List<Surface> Surfaces { get; } = [];

    /// <summary>
    /// The surfaces standing at the top of the scene, sorted into a tree of boxes so that a ray need
    /// not be shown every last one of them.
    /// <para>
    /// **The things at the top of a scene are as much in need of this as a group's children are**,
    /// and for a long time only a group had it.  A scene holding a dozen things did not care; one
    /// holding thousands paid for every one of them on every ray.  Measured on four thousand grass
    /// blades written at the top of a scene: 43 seconds walked, 0.7 seconds arranged.
    /// </para>
    /// </summary>
    private readonly ArrangedSurfaces _arranged = new ();

    /// <summary>
    /// This method sorts the scene's own surfaces for searching.  It is called once everything has
    /// been prepared and placed, since a surface cannot say where it is until it knows its shape, and
    /// one put in terms of its neighbours does not stand where it will until those are settled.
    /// </summary>
    public void ArrangeSurfaces()
    {
        _arranged.Arrange(Surfaces);
        _hasStrands = new SurfaceIterator(Surfaces).Surfaces.Any(surface => surface is StrandSurface || surface.Hair is not null);
    }

    // Whether anything in the scene is made of strands of hair, which are all that make a shadow ray's
    // width matter; a scene without any sends its shadow rays as lines, as it always has.
    private bool _hasStrands;

    // How little of a ray may be left to see past strands that cover only part of it before the rest is
    // let go: a thousandth, which no picture can show.
    private const double MostlyCovered = 1e-3;

    /// <summary>
    /// This property holds the pigment to use for a pixel when rays do not intersect with
    /// anything in the scene.
    /// <para>
    /// A ray that strikes nothing is asked of the background by the way it was <i>heading</i>, not by
    /// where it started: the pigment is sampled on a sphere of radius one, which is to say a sky
    /// infinitely far off.  That is what makes a patterned background mean anything.  Asked
    /// by position, as it once was, every ray from a camera would ask about the one point the camera
    /// stands at and the whole sky would come back a single flat color, while reflected rays -- which
    /// start all over the place -- would show a pattern the sky itself did not have.
    /// </para>
    /// <para>
    /// Being a function of direction alone is also what makes a mirror agree with the sky above it, and
    /// it means a pattern's own scale is in units of that unit sphere rather than of the world: the
    /// coordinates it is handed never leave the range -1 to 1, so a sky wanting finer detail scales the
    /// pigment down rather than up.
    /// </para>
    /// </summary>
    public Pigment Background { get; set; } = new SolidPigment(Colors.Transparent);

    /// <summary>
    /// This property holds what is true of the space between this scene's objects, which for now is
    /// what its index of refraction is.
    /// </summary>
    public SceneEnvironment Environment { get; set; } = new ();

    /// <summary>
    /// This property holds where to put the counts, or null when nobody asked for any.  It is set
    /// once before the render starts rather than passed down through every call, since the scene is
    /// asked about rays from every thread and from a good many depths of recursion.
    /// </summary>
    internal Statistics Statistics { get; set; }

    /// <summary>
    /// This method determines the color for the given ray.
    /// </summary>
    /// <param name="ray">The ray to determine the color for.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <returns>The color for the ray.</returns>
    public Color GetColorFor(Ray ray, int remaining = 4)
    {
        return GetColorFor<RgbSpectrum>(ray, remaining).ToColor();
    }

    /// <summary>
    /// This method determines the light arriving along the given ray, in whatever bands the render is
    /// carrying light in.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="ray">The ray to determine the light for.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <returns>The light arriving along the ray.</returns>
    public TS GetColorFor<TS>(Ray ray, int remaining = 4)
        where TS : struct, ISpectrum<TS>
    {
        // A pixel the camera's lens does not reach shows the background alone.
        if (ray.Outside)
            return Background.GetTransformedLightFor<TS>(HeadingOf(ray));

        List<Intersection> hits = Intersect(ray);

        // Sorted again, as finding the nearest has always sorted it, so that crossings tied in distance
        // fall in exactly the order they always have.
        hits.Sort();

        // **A strand finer than the ray is wide covers only part of it** -- see StrandSet -- so it is seen
        // in that share, and the rest of the ray goes on to whatever lies beyond: further strands, each
        // seen in its share of what is left, until something covers the whole of it.  Nothing else covers
        // only part of a ray, so for everything else this finds the nearest crossing and shades it, as it
        // always has.
        TS seen = TS.Black;
        double left = 1;
        int strands = 0;
        bool bouncing = _bounced > 0;

        foreach (Intersection hit in hits)
        {
            if (hit.Distance < 0)
                continue;

            // Bounced light passes what a shadow ray passes, all along its path; see Scene.Bounces.
            if (bouncing && PassedByBouncedLight(hit))
                continue;

            NoteWhereABounceMet(ray, hit);

            // A place the cache covers looks round itself only for what is near; see Scene.Bounces.
            if (BeyondTheNear(ray, hit.Distance, left))
                return seen;

            hit.PrepareUsing(ray, hits, Environment.IndexOfRefraction, TS.Wavelength);

            if (hit is HairIntersection { Coverage: < 1 } fine)
            {
                fine.Share = left * fine.Coverage;
                fine.Ordinal = strands++;
            }

            TS color = GetHitColor<TS>(hit, remaining);

            // What the ray crossed to get here was the surroundings, unless it was on its way out of
            // something, in which case it crossed that thing's insides and has been charged for it
            // already.  This is the same division the fade through a substance has always drawn.  And
            // unless it met something standing inside a thing that fades light or is filled with a
            // medium, when it crossed that thing's insides on the way -- a log inside a flame is seen
            // through the flame in front of it, and a flower set in tinted glass through the glass.
            color = hit.Inside ? color
                : HasInsides(hit.InteriorBefore) ? ThroughTheInsideOf(hit.ContainerBefore, color, hit)
                : ThroughTheSurroundings(color, ray, hit.Distance);

            if (hit is not HairIntersection { Coverage: < 1 } strand)
                return left == 1 ? color : (seen + color * left).WithAlpha(1);

            seen += color * (left * strand.Coverage);
            left *= 1 - strand.Coverage;

            if (left < MostlyCovered)
                return seen.WithAlpha(1);
        }

        // Now that the sky costs a pattern lookup to ask about, only rays that saw nothing ask it.
        // A ray that saw nothing also crossed the surroundings forever, which is a span an endless
        // medium is entitled to have the whole of its say over.
        // A search for bounced light that runs straight out to the sky finds what a sky light has counted
        // already, or a backdrop that lights nothing; see Scene.Bounces.
        if (BeyondTheNear(ray, double.PositiveInfinity, left))
            return seen;

        TS sky = CountedElsewhere(ray, _skyIsALight)
            ? TS.Black
            : Background.GetTransformedLightFor<TS>(HeadingOf(ray), SkyFootprintOf(ray));

        // A rough surface's reflection may be owed only part of a picture's sky; see SkyShareOf.  The
        // whole is left untouched rather than multiplied by one, which would make a clear sky opaque.
        if (SkyShareOf(ray) is var share and < 1)
            sky *= share;

        sky = ThroughTheSurroundings(sky, ray, double.PositiveInfinity);

        return left == 1 ? sky : (seen + sky * left).WithAlpha(1 - left * (1 - sky.Alpha));
    }

    /// <summary>
    /// This method reports whether what something stands inside has anything to say about light crossing
    /// it: a fade or a medium.  Plain glass, or nothing, has not.
    /// </summary>
    private static bool HasInsides(Interior interior)
    {
        return interior is not null &&
               (interior.Medium is not null || !double.IsPositiveInfinity(interior.Clarity));
    }

    /// <summary>
    /// This method charges the light from a crossing for the insides of the surface it stands within,
    /// over the ray's whole run to it: the ray set off inside the surface, or it would have met the
    /// surface first.  It is what the far side of the surface charges a ray leaving it; see the end of
    /// <see cref="GetHitColor{TS}"/>.
    /// </summary>
    private TS ThroughTheInsideOf<TS>(Surface container, TS color, Intersection hit)
        where TS : struct, ISpectrum<TS>
    {
        Interior interior = hit.InteriorBefore;

        color *= interior.GetFadeOver(hit.Distance);

        return interior.Medium is null
            ? color
            : ThroughMedium(
                interior.Medium, color, RayThatReached(hit), hit.Distance, container, hit.ContainerPortals);
    }

    /// <summary>
    /// This method hands the given color to whatever fills the space between this scene's objects,
    /// to be dimmed and added to over the span the ray crossed to bring it.
    /// </summary>
    /// <param name="color">The color the ray carried.</param>
    /// <param name="ray">The ray that carried it.</param>
    /// <param name="distance">How far the ray crossed the surroundings.</param>
    /// <returns>The color once the surroundings have had their say.</returns>
    private TS ThroughTheSurroundings<TS>(TS color, Ray ray, double distance)
        where TS : struct, ISpectrum<TS>
    {
        return Environment.Medium is null
            ? color
            : ThroughMedium(Environment.Medium, color, ray, distance);
    }

    /// <summary>
    /// This method hands the given color to a medium the ray crossed, for everything the medium does
    /// to it: what it takes out, what it gives off, and what it gathers in from the scene's lamps.
    /// </summary>
    /// <param name="medium">The medium that was crossed.</param>
    /// <param name="color">The color arriving from beyond it.</param>
    /// <param name="ray">The ray that crossed it.</param>
    /// <param name="distance">How far the ray crossed it; may be endless.</param>
    /// <param name="container">The surface it fills, or <c>null</c> for the surroundings.</param>
    /// <param name="portals">The instances the surface was met through, if it was.</param>
    /// <returns>The color once the medium has had its say.</returns>
    private TS ThroughMedium<TS>(
        Medium medium, TS color, Ray ray, double distance, Surface container = null,
        Portals portals = default)
        where TS : struct, ISpectrum<TS>
    {
        // A medium with a shape has no answer to be written down and must be walked along instead.
        // Only a medium filling a surface may have one, which is why the container is wanted here: it
        // says both what space the shape is described in and where the shape ends.  A glowing volume
        // met along a rough surface's reflection is walked whatever its shape, since its glow is
        // shared place by place (see HighlightShareOf).
        VolumeLight mixing = container is null ? null : MixingLightFor(medium, ray);

        // A glowing volume crossed straight on a search for bounced light lends none of its glow, which its
        // light's own samples count, though it still dims what lies beyond; see Scene.Bounces.  It is
        // walked to leave the glow out, whatever its shape.
        bool withoutGlow = mixing is null && container is not null && CountedElsewhere(ray, true) &&
                           GlowsAsALight(medium);

        if (withoutGlow || mixing is not null || (medium.HasShape && container is not null))
            return MarchedThrough(medium, color, ray, distance, container, portals, mixing, withoutGlow);

        TS through = medium.ApplyOver(color, distance);

        // Everything above was written down rather than worked out.  What follows is the one term
        // that has to be gone and looked for, and only a medium that turns light aside has it -- so a
        // scene whose fog merely swallows light costs exactly what it did before any of this existed.
        if (!medium.Scatters || Lights.Count == 0)
            return through;

        TS gathered = GatheredFromTheLights<TS>(medium, ray, distance, container, portals);

        // The gathered light adds to what came through without covering the pixel any further: how
        // much of the pixel the medium stands in front of was settled by what it took out, and
        // turning light aside is one of the ways it took it.
        return (through + gathered).WithAlpha(through.Alpha);
    }

    /// <summary>
    /// This method works out how much of the scene's light the medium turns toward the eye along a
    /// ray's crossing of it -- the shaft of light through a window, the cone below a spotlight, the
    /// halo around a lamp in fog.
    /// <para>
    /// The crossing is cut into equal lengths and asked once within each, at a place nudged off the
    /// middle so that a bank of samples does not lay its own pattern over the picture.  The nudge is
    /// the same for every ray, being a plain function of which sample it is, so two renders of one
    /// scene agree to the last bit.  What each place is asked is what every lamp delivers to it,
    /// dimmed by whatever stands in the way, which is the very question a surface asks when it is lit.
    /// </para>
    /// </summary>
    /// <param name="medium">The medium being crossed.</param>
    /// <param name="ray">The ray crossing it.</param>
    /// <param name="distance">How far the ray crosses it; may be endless.</param>
    /// <returns>The light the medium sends along the ray.</returns>
    private TS GatheredFromTheLights<TS>(
        Medium medium, Ray ray, double distance, Surface container, Portals portals)
        where TS : struct, ISpectrum<TS>
    {
        int count = medium.Samples;
        double extinction = medium.MeanExtinction;

        // A medium that turns light aside stops it by doing so, so this cannot be nothing here; the
        // guard is for the arithmetic's sake rather than for any scene's.
        if (extinction <= 0 || distance <= 0)
            return TS.Black;

        // Places along the crossing are not spread evenly over it.  They are spread by how much of
        // what is there could still reach the eye, which falls away exponentially -- so most of them
        // land near this end, where the light that scatters actually shows, and hardly any land far
        // off, where almost none of it would survive the trip back.  Two things follow.  The
        // transmittance the weighing would apply is very nearly the transmittance the choosing already
        // applied, so it cancels and what is left carries no great spread of size; and a crossing with
        // no end needs no arbitrary stopping point, since the exponential ends it.  Sampled evenly
        // instead, a ray that saw nothing marched a hundred units in the same handful of steps as its
        // neighbour that struck the floor at ten, and one long step landing in a lamp's cone showed up
        // as a bright speck.  Those specks lay in a line along the horizon, which is where that
        // difference between neighbours falls.
        double reach = Medium.FractionStopped(extinction * distance);
        Vector heading = ray.Direction.Unit;
        double shift = ShiftFor(ray);
        TS scattering = TS.FromUnbounded(medium.Scattering);
        TS gathered = TS.Black;
        List<Intersection> crossings = container is null ? null : [];

        for (int index = 0; index < count; index++)
        {
            double fraction = (index + JitterFor(index, shift)) / count;
            double along = -Math.Log(1 - fraction * reach) / extinction;
            double chance = extinction * Math.Exp(-extinction * along) / reach;
            Point where = ray.Origin + heading * along;
            TS arriving = GatheredAt<TS>(
                medium, container, portals, where, heading, ray.TimeIndex, index, crossings);

            if (arriving.IsBlack)
                continue;

            // What this place turns aside, of what reached it, dimmed by the trip back to the eye and
            // divided by how likely the place was to have been asked at all.
            TS scattered = arriving * scattering * (medium.Density / (chance * count));

            gathered += scattered * medium.GetTransmittanceOver<TS>(along);
        }

        return gathered;
    }

    /// <summary>
    /// This method walks a ray along its crossing of a medium whose density varies from place to
    /// place, gathering as it goes what the medium takes out of the ray, what it gives off, and what it
    /// turns toward the eye.
    /// <para>
    /// All three have to be gathered together rather than one after another, because each depends on
    /// how much medium lies between it and the eye -- and with the density varying, that is only known
    /// by having walked the part already passed.  So the walk carries a running transmittance: at each
    /// step the medium's own light and the light it turns aside are weighed by what has accumulated so
    /// far, and then the step's own share is added to it.
    /// </para>
    /// <para>
    /// The walk is first order in the step, so its answer approaches the truth as the steps grow finer
    /// rather than being exact at any count -- which is the price of a density that varies at all.  The
    /// steps are nudged off their even spacing as the sampling elsewhere is, and by the same
    /// ray-dependent shift, so neither a banded picture nor a shifting one comes of it.
    /// </para>
    /// </summary>
    /// <param name="medium">The medium being crossed.</param>
    /// <param name="behind">The color arriving from beyond it.</param>
    /// <param name="ray">The ray crossing it.</param>
    /// <param name="distance">How far the ray crosses it.</param>
    /// <param name="container">The surface the medium fills.</param>
    /// <param name="portals">The instances the surface was met through, if it was.</param>
    /// <returns>The color once the medium has had its say.</returns>
    private TS MarchedThrough<TS>(
        Medium medium, TS behind, Ray ray, double distance, Surface container, Portals portals,
        VolumeLight mixing = null, bool withoutGlow = false)
        where TS : struct, ISpectrum<TS>
    {
        int count = medium.Samples;
        double step = distance / count;
        Vector heading = ray.Direction.Unit;
        double shift = ShiftFor(ray);
        bool gathers = medium.Scatters && Lights.Count > 0;
        double surviving = 1;
        TS scattering = TS.FromUnbounded(medium.Scattering);
        TS transmittance = TS.White;
        TS added = TS.Black;
        List<Intersection> crossings = gathers ? [] : null;

        for (int index = 0; index < count; index++)
        {
            double along = (index + JitterFor(index, shift)) * step;
            Point where = ray.Origin + heading * along;
            Point local = container.WorldToSurface(where, ray.TimeIndex, portals);
            double density = medium.DensityAt(local);

            if (density <= 0)
                continue;

            // Asked at the same point the density was, so that a medium whose color varies and a
            // medium whose amount varies are describing the same place.
            TS source = withoutGlow ? TS.Black : medium.EmissionAt<TS>(local, density) * density;

            if (mixing is not null)
                source *= ReflectionShareAt(mixing, where, ray.TimeIndex);

            if (gathers)
            {
                source += scattering * density * GatheredAt<TS>(
                    medium, container, portals, where, heading, ray.TimeIndex, index, crossings);
            }

            // What this step's worth gives up to the eye, dimmed by everything nearer than it, and then
            // the step's own share of the dimming added on for whatever lies further.
            added += source * transmittance * step;
            transmittance *= Transmitted(medium.ExtinctionAt<TS>(density), step);
            surviving = transmittance.Average;

            // Nothing beyond this can show, so there is no sense in walking further.
            if (surviving < 0.001)
                break;
        }

        return (behind * transmittance + added).WithAlpha(
            behind.Alpha + (1 - behind.Alpha) * (1 - surviving));
    }

    /// <summary>
    /// This method returns what fraction of each band survives the given rate of stopping over the
    /// given length.
    /// </summary>
    /// <param name="extinction">The rate at which each band stops coming this way.</param>
    /// <param name="distance">The length crossed at that rate.</param>
    /// <returns>The fraction of each band that gets through.</returns>
    private static TS Transmitted<TS>(TS extinction, double distance)
        where TS : struct, ISpectrum<TS>
    {
        TS surviving = TS.White;

        for (int band = 0; band < TS.Count; band++)
            surviving[band] = Math.Exp(-extinction[band] * distance);

        return surviving;
    }

    /// <summary>
    /// This method works out how much of the scene's light one place inside a shaped medium turns
    /// toward the eye.  It is the same question a place in an even medium is asked, with one thing
    /// added: the light has to get through the rest of the shape to arrive at all.
    /// <para>
    /// That last part is what makes a cloud look like a cloud rather than a fog in a ball.  A cloud's
    /// far side is dark because its near side stood in the way, and nothing but the shape shadowing
    /// itself produces that.
    /// </para>
    /// </summary>
    /// <param name="medium">The medium being crossed.</param>
    /// <param name="container">The surface it fills.</param>
    /// <param name="portals">The instances the surface was met through, if it was.</param>
    /// <param name="where">The place being asked about.</param>
    /// <param name="heading">The way the ray was travelling.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to look at.</param>
    /// <param name="crossings">A list to find the shape's far side with, reused between places so that
    /// a walk does not allocate one per step.</param>
    /// <returns>The light the place sends along the ray.</returns>
    private TS GatheredAt<TS>(
        Medium medium, Surface container, Portals portals, Point where, Vector heading, int timeIndex,
        int index, List<Intersection> crossings)
        where TS : struct, ISpectrum<TS>
    {
        TS arriving = GatheredDirectlyAt<TS>(
            medium, container, portals, where, heading, timeIndex, index, crossings);

        // Everything above is light that reached this place straight from a lamp.  What follows is
        // light that got here by being turned somewhere else first, which in anything thick is most of
        // it -- and all of which was simply missing until now.
        if (medium.Bounces > 0)
        {
            arriving += CarriedOnFrom<TS>(
                medium, container, portals, where, heading, medium.Bounces, ShiftFor(where, index),
                timeIndex, crossings);
        }

        return arriving;
    }

    /// <summary>
    /// This method works out how much light one place is sent straight from the scene's lamps, and how
    /// much of that the medium would turn toward the way the ray is going.
    /// </summary>
    /// <param name="medium">The medium being crossed.</param>
    /// <param name="container">The surface it fills, or <c>null</c> for the surroundings.</param>
    /// <param name="portals">The instances the surface was met through, if it was.</param>
    /// <param name="where">The place being asked about.</param>
    /// <param name="heading">The way the ray was travelling.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to look at.</param>
    /// <param name="index">Which sample along the crossing this is.</param>
    /// <param name="crossings">A list to find the shape's far side with.</param>
    /// <returns>The light the lamps send this way.</returns>
    private TS GatheredDirectlyAt<TS>(
        Medium medium, Surface container, Portals portals, Point where, Vector heading, int timeIndex,
        int index, List<Intersection> crossings)
        where TS : struct, ISpectrum<TS>
    {
        TS arriving = TS.Black;

        foreach (Light each in Lights)
        {
            Light light = each.At(timeIndex);

            // One place on the lamp per sample, walking around it as the samples go.  An area light's
            // softness is then spread across the crossing for nothing, rather than each sample paying
            // for the whole face of it.  The walk starts somewhere of the place's own: started at the lamp's
            // first sample on every ray, a crossing of sixteen steps only ever asked a sky of thirty-two about
            // its first sixteen -- its upper half, each counted as the whole sphere -- the same in every pixel,
            // so it never averaged away.
            int count = light.SampleCount;
            int pick = count == 1 ? 0 : (index + (int) (ShiftFor(where, 141) * count)) % count;
            LightSample sample = light.SampleToward(where, pick);
            TS reaching = GetLightReaching<TS>(
                where, sample.Direction, sample.Distance, timeIndex, null, false, 0, light.Housing);

            if (reaching.IsBlack)
                continue;

            // A medium filling a surface stands in its own light's way, and nothing else accounts for
            // that: the walk above looks at surfaces and at the surroundings, neither of which this is.
            // A medium filling the surroundings needs no such thing, having been charged for its trip
            // there already.
            if (container is not null)
            {
                reaching *= SurvivingTheMedium<TS>(
                    medium, container, portals, where, sample, timeIndex, crossings);
            }

            if (reaching.IsBlack)
                continue;

            // The angle between the way the light was travelling and the way it leaves toward the eye.
            // Both are read as the directions light moves in, and negating both leaves their dot
            // product alone, so this is simply the one direction against the other.
            double phase = medium.PhaseFor(sample.Direction.Dot(heading));

            arriving += light.EmittedToward<TS>(sample) * reaching *
                (sample.Cone * phase * light.FadingOver(sample.Distance));
        }

        return arriving;
    }

    /// <summary>
    /// This method follows one path of light back through the medium: where did the light turned
    /// toward the eye here come from, if it did not come straight from a lamp?
    /// <para>
    /// One path rather than many.  A place could be asked about every direction at once, but each of
    /// those would have to ask again, and the work would multiply by itself at every turn.  Following
    /// a single direction, picked in proportion to how much the medium favors it, costs one more path
    /// per turn rather than a tree of them -- and since every place along the ray is doing it, the
    /// picture as a whole still averages over a great many directions.
    /// </para>
    /// <para>
    /// Each turn is worth the share of stopped light that carried on rather than being swallowed, and
    /// nothing else: the direction having been picked in proportion to the shape, the shape itself
    /// cancels out of the weighing.  A path that wanders out of the medium is done, and that is not a
    /// gap: every turn asks the scene's lights, a sky light among them, so whatever the path would
    /// have met out there has already been counted where it last turned.
    /// </para>
    /// </summary>
    /// <param name="medium">The medium the light is wandering in.</param>
    /// <param name="container">The surface it fills, or <c>null</c> for the surroundings.</param>
    /// <param name="portals">The instances the surface was met through, if it was.</param>
    /// <param name="from">The place the path is being followed back from.</param>
    /// <param name="heading">The way the light being followed is travelling.</param>
    /// <param name="left">How many more turns may be followed.</param>
    /// <param name="seed">Where in the run of numbers this path's choices are drawn from.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to look at.</param>
    /// <param name="crossings">A list to find the shape's edge with.</param>
    /// <returns>The light that arrived here by way of somewhere else.</returns>
    private TS CarriedOnFrom<TS>(
        Medium medium, Surface container, Portals portals, Point from, Vector heading, int left,
        double seed, int timeIndex, List<Intersection> crossings)
        where TS : struct, ISpectrum<TS>
    {
        TS albedo = medium.AlbedoOf<TS>();
        TS carried = TS.Black;
        TS throughput = TS.White;
        Vector going = heading;
        Point at = from;

        for (int turn = 0; turn < left; turn++)
        {
            Vector came = medium.SampleDirectionAround(
                going, Fraction(seed, turn * 3), Fraction(seed, turn * 3 + 1));
            double howFar = TurnedAgainAfter(
                medium, container, portals, at, came, Fraction(seed, turn * 3 + 2), timeIndex, crossings);

            // The path left the medium without being turned again.  Whatever lies out that way was
            // counted at the last turn, by asking the lights there; following it out as well would
            // count the same light a second time.
            if (double.IsNaN(howFar))
                break;

            at += came * howFar;
            going = came;
            throughput *= albedo;

            if (throughput.Sum < 0.001 * TS.Count)
                break;

            carried += throughput * GatheredDirectlyAt<TS>(
                medium, container, portals, at, going, timeIndex, turn, crossings);
        }

        return carried;
    }

    /// <summary>
    /// This method returns how far along a direction the medium turns a path again, or <c>NaN</c> if
    /// the path leaves the medium first.  The distance is drawn in proportion to how much light would
    /// still be travelling at each point along the way, which is what makes a single distance stand
    /// for all of them.
    /// </summary>
    /// <param name="medium">The medium the path is in.</param>
    /// <param name="container">The surface it fills, or <c>null</c> for the surroundings.</param>
    /// <param name="portals">The instances the surface was met through, if it was.</param>
    /// <param name="from">Where the path starts.</param>
    /// <param name="going">Which way it goes.</param>
    /// <param name="fraction">A number from nothing up to one, which picks the distance.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to look at.</param>
    /// <param name="crossings">A list to find the shape's edge with.</param>
    /// <returns>How far it goes before being turned, or <c>NaN</c> if it gets out.</returns>
    private static double TurnedAgainAfter(
        Medium medium, Surface container, Portals portals, Point from, Vector going, double fraction,
        int timeIndex, List<Intersection> crossings)
    {
        double edge = EdgeOfTheMedium(container, portals, new Ray(from, going, timeIndex), crossings);
        double wanted = -Math.Log(1 - fraction);

        if (!medium.HasShape)
        {
            double extinction = medium.MeanExtinction;
            double howFar = extinction > 0 ? wanted / extinction : double.PositiveInfinity;

            return howFar < edge ? howFar : double.NaN;
        }

        // With the density varying, how much stuff has been passed through is only known by walking,
        // so the walk goes on until it has passed through as much as was asked for.
        //
        // A fixed number of steps rather than a share of the crossing's own: this is one path's own
        // question, and how finely it is answered has nothing to do with how many paths there are.
        // Tying the two together would have the work grow as the square of the sample count, which
        // makes a quality knob that quadruples the render for twice the quality.
        const int count = 24;
        double step = (double.IsPositiveInfinity(edge) ? 1 : edge) / count;
        double passed = 0;

        for (int index = 0; index < count; index++)
        {
            double along = (index + 0.5) * step;
            double density = medium.DensityAt(
                container.WorldToSurface(from + going * along, timeIndex, portals));

            passed += medium.MeanExtinction / medium.Density * density * step;

            if (passed >= wanted)
                return along;
        }

        return double.NaN;
    }

    /// <summary>
    /// This method returns how far off the near edge of a medium's surface lies along a ray, or
    /// infinity where the medium fills the surroundings and so has no edge.  The surface is crossed where
    /// it stands, the ray carried into its parent's space first (see <see cref="IntoTheSpaceOf"/>): until
    /// 2026-10-19 a medium in a group that was moved or scaled found its edge where the shape would be
    /// had the group never been moved.
    /// </summary>
    /// <param name="container">The surface the medium fills, or <c>null</c>.</param>
    /// <param name="portals">The instances the surface was met through, if it was.</param>
    /// <param name="ray">The ray to look along.</param>
    /// <param name="crossings">A list to find the crossings with.</param>
    /// <returns>How far off the edge is.</returns>
    private static double EdgeOfTheMedium(
        Surface container, Portals portals, Ray ray, List<Intersection> crossings)
    {
        if (container is null)
            return double.PositiveInfinity;

        crossings.Clear();
        container.Intersect(IntoTheSpaceOf(container, ray, portals), crossings);

        double edge = double.PositiveInfinity;

        foreach (Intersection crossing in crossings)
        {
            if (crossing.Distance > 1e-9 && crossing.Distance < edge)
                edge = crossing.Distance;
        }

        return edge;
    }

    /// <summary>
    /// This method returns a number from nothing up to one, drawn from a run belonging to one path.
    /// It is a plain function of where the path started and which choice is being made, so the same
    /// picture comes out of every render however the work was divided between threads.
    /// </summary>
    /// <param name="seed">Where in the run this path's choices are drawn from.</param>
    /// <param name="index">Which choice along the path is being made.</param>
    /// <returns>The number picked.</returns>
    private static double Fraction(double seed, int index)
    {
        // Two irrationals, so that walking one choice to the next never falls into step with itself.
        double walked = seed + (index + 1) * 0.7548776662466927 + index * index * 0.5698402909980532;

        return walked - Math.Floor(walked);
    }

    /// <summary>
    /// This method returns a starting place in the run of numbers for a path leaving one sample, so
    /// that two samples do not follow the same wandering.
    /// </summary>
    /// <param name="where">The place the path leaves from.</param>
    /// <param name="index">Which sample along the crossing it leaves from.</param>
    /// <returns>The starting place.</returns>
    internal static double ShiftFor(Point where, int index)
    {
        ulong bits = (ulong) (
            BitConverter.DoubleToInt64Bits(where.X) * 73_856_093 ^
            BitConverter.DoubleToInt64Bits(where.Y) * 19_349_663 ^
            BitConverter.DoubleToInt64Bits(where.Z) * 83_492_791 ^
            (long) index * 26_699_363);

        bits = (bits ^ bits >> 30) * 0xbf58476d1ce4e5b9;
        bits = (bits ^ bits >> 27) * 0x94d049bb133111eb;

        return (bits >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>
    /// This method returns how much of a lamp's light survives the rest of the shape on its way to one
    /// place inside it.
    /// <para>
    /// Where the density is even this is a closed form, being a rate times a length.  Where it has a
    /// shape it is walked, and coarsely -- in a fixed sixteen steps, whatever the eye's walk takes --
    /// because all that is wanted of it is how much stuff stands in the way, not where any of it is.  Either way it
    /// stops at the surface's own edge, since past that the medium is not there to shade anything.
    /// </para>
    /// </summary>
    /// <param name="medium">The medium doing the shading.</param>
    /// <param name="container">The surface it fills.</param>
    /// <param name="portals">The instances the surface was met through, if it was.</param>
    /// <param name="where">The place the light is heading for.</param>
    /// <param name="sample">Which way the light lies, and how far off.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to look at.</param>
    /// <param name="crossings">A list to find the shape's far side with.</param>
    /// <returns>The fraction of each color that arrives.</returns>
    private static TS SurvivingTheMedium<TS>(
        Medium medium, Surface container, Portals portals, Point where, LightSample sample, int timeIndex,
        List<Intersection> crossings)
        where TS : struct, ISpectrum<TS>
    {
        Ray toward = new (where, sample.Direction, timeIndex);

        // Crossed where it stands, as the edge of a path's wandering is; see EdgeOfTheMedium.
        crossings.Clear();
        container.Intersect(IntoTheSpaceOf(container, toward, portals), crossings);

        double edge = double.PositiveInfinity;

        foreach (Intersection crossing in crossings)
        {
            // The nearest crossing ahead of us is where the shape ends.  Ones behind belong to the
            // stuff already passed through, and are none of this walk's business.
            if (crossing.Distance > 0 && crossing.Distance < edge)
                edge = crossing.Distance;
        }

        if (double.IsPositiveInfinity(edge))
            return TS.White;

        edge = Math.Min(edge, sample.Distance);

        // An even density needs no walking: how much stands in the way is the rate times the length,
        // which is the very thing the closed form is for.
        if (!medium.HasShape)
            return Transmitted(medium.ExtinctionAt<TS>(medium.Density), edge);

        // A fixed number of steps rather than a share of the crossing's own.  How finely this one
        // shadow is answered is its own question, and tying it to how many places the eye's ray asks
        // about made the work grow as the square of that count -- twice the quality for four times the
        // render, which is not a knob anyone can use.  It also never settled: this is a bias rather
        // than a noise, so more places along the eye's ray never averaged it away.
        const int count = 16;

        double step = edge / count;
        TS depth = TS.Black;

        for (int index = 0; index < count; index++)
        {
            Point at = where + sample.Direction * ((index + 0.5) * step);
            double density = medium.DensityAt(container.WorldToSurface(at, timeIndex, portals));

            if (density > 0)
                depth += medium.ExtinctionAt<TS>(density) * step;
        }

        return Transmitted(depth, 1);
    }

    /// <summary>
    /// This method rebuilds the ray that arrived at the given crossing, which is what a medium filling
    /// a surface needs before it can be asked what light reaches along the way.  A crossing keeps no
    /// ray, but it keeps everything needed of one: the eye vector is the ray's own direction turned
    /// about, and the distance is how far along it the crossing lies -- so for a ray leaving a solid,
    /// this is the ray that entered it, and the distance is its whole passage through.
    /// </summary>
    /// <param name="intersection">The crossing to rebuild the ray for.</param>
    /// <returns>The ray that reached it.</returns>
    private static Ray RayThatReached(Intersection intersection)
    {
        Vector direction = -intersection.Eye;

        return new Ray(
            intersection.Point - direction * intersection.Distance, direction,
            intersection.TimeIndex);
    }

    /// <summary>
    /// This method returns where within its own length a sample sits, as a fraction.  Samples laid at
    /// dead centre make a bank of even steps that the eye reads as banding, so each is nudged off it.
    /// <para>
    /// The nudge walks by the golden ratio, which spreads any number of samples about as evenly as
    /// they can be spread and needs nothing remembered between them: no table, no seed, and the same
    /// answer on every thread and in every run.  The whole walk is then shifted by an amount belonging
    /// to the ray, because a nudge that is the same for every ray is no help at all -- every ray then
    /// samples in the same places, its error is the error of the ray beside it, and the picture shows
    /// the pattern of the sampling rather than the pattern of the light.
    /// </para>
    /// </summary>
    /// <param name="index">Which sample along the crossing this is.</param>
    /// <param name="shift">The whole walk's shift, belonging to the ray being sampled.</param>
    /// <returns>Where in its own length it sits, from nothing up to one.</returns>
    private static double JitterFor(int index, double shift)
    {
        const double golden = 0.618033988749895;

        double walked = shift + index * golden;

        return walked - Math.Floor(walked);
    }

    /// <summary>
    /// This method returns the shift to give one ray's sampling, so that neighboring rays do not all
    /// sample in the same places.  It is drawn from the ray's own direction, which makes it scattered
    /// from one ray to the next and yet fixed for any given ray -- so two renders of a scene agree to
    /// the last bit, however the work was divided between threads.
    /// </summary>
    /// <param name="ray">The ray to shift the sampling of.</param>
    /// <returns>The shift, from nothing up to one.</returns>
    private static double ShiftFor(Ray ray)
    {
        ulong bits = (ulong) (
            BitConverter.DoubleToInt64Bits(ray.Direction.X) * 73_856_093 ^
            BitConverter.DoubleToInt64Bits(ray.Direction.Y) * 19_349_663 ^
            BitConverter.DoubleToInt64Bits(ray.Direction.Z) * 83_492_791);

        // Two rounds of mixing, so that directions a hair apart come out nowhere near each other.
        bits = (bits ^ bits >> 30) * 0xbf58476d1ce4e5b9;
        bits = (bits ^ bits >> 27) * 0x94d049bb133111eb;

        return (bits >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>
    /// This method returns the point on the unit sphere the given ray is heading toward, which is the
    /// point the background is asked about.
    /// </summary>
    /// <param name="ray">The ray whose heading is wanted.</param>
    /// <returns>The point it is heading toward.</returns>
    private static Point HeadingOf(Ray ray)
    {
        Vector heading = ray.Direction.Unit;

        return new Point(heading.X, heading.Y, heading.Z);
    }

    /// <summary>
    /// This method works out the patch of the sky a ray that saw nothing is answerable for.  The sky is
    /// read as though painted on a sphere of radius one, so the patch is the ray's cone where it crosses
    /// that sphere: as wide as the angle the cone spreads by, square on to the ray.
    /// <para>
    /// Only a sky that is a picture is read over a patch.  A pattern sky is read at a point, as it always
    /// was, because the usual one is a gradient up from the horizon that repeats below it -- where the
    /// ground hides it, until a patch straddling the horizon reaches down and averages the top of the sky
    /// into the row of pixels along it.  A picture of the sky has no such hidden half, and is the sky
    /// fine enough to beat into moiré.
    /// </para>
    /// </summary>
    /// <param name="ray">The ray that saw nothing.</param>
    /// <returns>The patch of sky, or none for a ray that does not spread or a sky that is not a
    /// picture.</returns>
    private Footprint SkyFootprintOf(Ray ray)
    {
        if (ray.Spread <= 0 || Background is not ImagePigment)
            return Footprint.None;

        Vector heading = ray.Direction.Unit;

        return Footprint.For(ray.Spread * 0.5, heading, -heading);
    }

    /// <summary>
    /// This property holds the sky light that carries the sky the scene shows, when that sky is shared
    /// between a rough surface's highlight and its reflection -- a picture, or any sky looked at through
    /// portals; see <see cref="HighlightShareOf"/>.  Found the first time it is asked for, once the lights
    /// are all in place.
    /// </summary>
    private SkyLight SharedSky
    {
        get
        {
            if (!_sharedSkyFound)
            {
                _sharedSky = Lights.OfType<SkyLight>()
                    .FirstOrDefault(sky => ReferenceEquals(sky.Pigment, Background) &&
                                           (sky.IsAPicture || sky.Openings is { Count: > 0 }) &&
                                           sky.Color.Matches(Colors.White));
                _sharedSkyFound = true;
            }

            return _sharedSky;
        }
    }

    private SkyLight _sharedSky;
    private bool _sharedSkyFound;

    /// <summary>
    /// This method returns the share of the sky that belongs to the ray reading it: all of it, unless
    /// the ray is one of a rough surface's reflections, still on its straight run, and the sky is a
    /// picture whose light that surface's highlight shares -- when it is what is left once the sky's
    /// own samples have taken theirs.
    /// </summary>
    /// <param name="ray">The ray that saw nothing.</param>
    /// <returns>The ray's share of the sky.</returns>
    private double SkyShareOf(Ray ray)
    {
        if (_looking is not { Mirror: { } mirror } run || SharedSky is not { } sky ||
            !OnRun(ray, run.Origin, run.Direction))
            return 1;

        double byReflecting = run.Count * run.Odds;
        double byTheSky = sky.OddsToward(run.Origin, run.Direction, mirror);

        return byReflecting + byTheSky > 0 ? byReflecting / (byReflecting + byTheSky) : 1;
    }

    /// <summary>
    /// This method finds the point of intersection, if any, of the given ray with the
    /// things in the world.
    /// ray intersects the geometry and, if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    public List<Intersection> Intersect(Ray ray)
    {
        Statistics?.CountSceneRay();

        List<Intersection> intersections = [];

        _arranged.Intersect(ray, intersections, Surfaces);

        intersections.Sort();

        return intersections;
    }

    /// <summary>
    /// This method is used to determine the color for the given intersection point.
    /// </summary>
    /// <param name="intersection">The intersection point to derive the color for.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <returns>The color to use.</returns>
    public Color GetHitColor(Intersection intersection, int remaining)
    {
        return GetHitColor<RgbSpectrum>(intersection, remaining).ToColor();
    }

    /// <summary>
    /// This method is used to determine the light leaving the given intersection point toward the
    /// eye, in whatever bands the render is carrying light in.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="intersection">The intersection point to derive the light for.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <returns>The light to use.</returns>
    public TS GetHitColor<TS>(Intersection intersection, int remaining)
        where TS : struct, ISpectrum<TS>
    {
        // Along light gathered by a strand of hair, a place within the hair's reach gives only what came to it from
        // beyond -- the lamps' light it takes there is the hair light's to carry -- and beyond the reach everything
        // counts, from there on.  See ShadeFiber.
        if (_hairDomain is null)
            return ShadedAt<TS>(intersection, remaining, false);

        if (WithinTheHair(intersection))
            return ShadedAt<TS>(intersection, remaining, true);

        HairLight domain = _hairDomain;

        _hairDomain = null;

        try
        {
            return ShadedAt<TS>(intersection, remaining, false);
        }
        finally
        {
            _hairDomain = domain;
        }
    }

    /// <summary>
    /// This method works out the light leaving the given intersection point toward the eye; see
    /// <see cref="GetHitColor{TS}"/>.
    /// </summary>
    /// <param name="intersection">The intersection point to derive the light for.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <param name="fromBeyond">Whether the point gives only the light that came to it from beyond the reach of a
    /// hair, its lamps' light left out.</param>
    private TS ShadedAt<TS>(Intersection intersection, int remaining, bool fromBeyond)
        where TS : struct, ISpectrum<TS>
    {
        // A strand of hair is a fiber, and is shaded as one; a wire finer than the ray is shaded round its
        // whole round at once, and one wide enough to be seen place by place as the metal it is, below.
        if (intersection is HairIntersection strand && !strand.IsAWire)
            return ShadeFiber<TS>(strand, remaining, fromBeyond);

        if (intersection is HairIntersection { IsAFineWire: true } wire)
            return ShadeWire<TS>(wire, remaining);

        // For the patterns that look about the place being shaded; see Surroundings.
        ShadeHere(intersection.TimeIndex);

        Material material = RegularizedFor(intersection);

        // A blend between a smooth material and a rough one is shaded as each and the two mixed, rather than as one
        // material between them: a mirror's highlight is Phong's and a rough surface's is its facets', and a blend
        // of the numbers gave the edge of a smooth paint going rough no highlight at all -- the smallest roughness is
        // facets too narrow to show -- a hard seam in the gloss along the very edge of the blend.
        if (material.Split is { } split)
        {
            // The light reaching the place from each lamp is the same whichever half is shading it, and is found
            // once (see ReachingFrom): shaded twice over, a weathered droid's shadow rays all but doubled a scene.
            Reached<TS>.Sharing++;

            try
            {
                intersection.Material = split.From;

                TS from = HitColorOf<TS>(intersection, Regularized(intersection, split.From), remaining, fromBeyond);

                intersection.Material = split.To;

                TS to = HitColorOf<TS>(intersection, Regularized(intersection, split.To), remaining, fromBeyond);

                intersection.Material = material;

                return from * (1 - split.Share) + to * split.Share;
            }
            finally
            {
                if (--Reached<TS>.Sharing == 0)
                    Reached<TS>.Memo?.Clear();
            }
        }

        return HitColorOf<TS>(intersection, material, remaining, fromBeyond);
    }

    /// <summary>
    /// This method works out the light leaving a point on a surface toward the ray that found it, the material it
    /// wears there settled: see <see cref="GetHitColor{TS}"/>.
    /// </summary>
    /// <param name="intersection">The intersection to shade.</param>
    /// <param name="material">The material settled there.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <param name="fromBeyond">Whether the point gives only the light that came to it from beyond the reach of a
    /// hair, its lamps' light left out; see <see cref="GetHitColor{TS}"/>.</param>
    /// <returns>The light to use.</returns>
    private TS HitColorOf<TS>(Intersection intersection, Material material, int remaining, bool fromBeyond)
        where TS : struct, ISpectrum<TS>
    {
        TS surfaceColor = TS.Black;

        // What a surface lets past, it cannot also show.  The pigment may say so color by color,
        // which is what lets one pattern be a window in some places and a wall in others, so the
        // question is asked at this point rather than of the material as a whole.
        double transparency = material.PigmentMayTransmit
            ? material.TransparencyFor(
                material.GetColorFor(intersection))
            : material.Transparency;
        bool transmits = material.Transparency > 0 || material.PigmentMayTransmit;
        bool roughGlass = material.IsRough && transmits;

        // What a surface mirrors never reaches its pigment, so one whose mirroring follows Fresnel
        // shows only the rest of the light as its own -- next to none of it, at a graze.
        double ownShare = material.FresnelApplies
            ? 1 - material.ReflectanceAt(intersection.Eye.Dot(intersection.Normal))
            : 1;

        // And bare metal shows nothing of its own: what gets into it, it swallows.
        BareMetal? metal = material.BareMetalAt(intersection);

        if (metal is { } bare)
            ownShare *= 1 - bare.Share;

        // Rough glass shows only what it stops of its own color, as any glass does, but keeps its
        // highlight whole: that is what its facets mirror, and letting light through takes none of it
        // away.  So its own share is cut here, where the highlight is spared, rather than below.  A
        // film's highlight is what it mirrors too, and is spared in the same way: without it a soap
        // bubble, which stops nothing, would show no highlight at all.
        bool sparesHighlight = roughGlass || material.Film is not null;

        if (sparesHighlight)
            ownShare *= 1 - transparency;

        // How many directions this point's own reflection will look in, which a glowing volume's
        // samples are weighed against.  None, where it will not look at all.
        int looks = material.IsRough && remaining >= 1 && (material.Reflective > 0 || metal is not null)
            ? _spreading || TS.Count == 1 ? 1 : material.ReflectionSamples
            : 0;
        (double, double)? glass = roughGlass ? (intersection.N1, intersection.N2) : null;

        // And how many directions its glass will look through, which a glowing volume beyond it is
        // weighed against in the same way.
        int looksThrough = roughGlass && remaining >= 1 && transparency > 0
            ? _spreading || TS.Count == 1 || (TS.IsSpectral && intersection.Disperses) ? 1 : material.ReflectionSamples
            : 0;

        // A film shares the light out color by color, in place of Fresnel's one share for every color -- over a
        // metal, by the metal's own index beneath it.
        FilmShares<TS>? film = material.Film?.SharesAt<TS>(intersection, material.MetalApplies ? material.Metal : null);

        // A brushed surface's facets lean further across its grooves than along them; see Facets.
        Facets facets = Facets.At(
            material, intersection.Surface, intersection.Point, intersection.Normal, intersection.TimeIndex,
            intersection.Portals, intersection as HairIntersection);

        // A clear coat mirrors by its own index, and everything the surface beneath it gives back crosses it
        // twice; see Coat.  A rough one looks in as many directions as a rough surface does.
        Coat coat = CoatOn(intersection, material);
        double cosEye = intersection.Eye.Dot(intersection.Normal);

        // A cloth's sheen gives back its share of light from all round, which the weave beneath never gets.
        Sheen sheen = material.SheenApplies ? material.Sheen : null;
        (Coat, double, int)? coated = coat is null
            ? null
            : (coat, intersection.N1, coat.IsRough && remaining >= 1
                ? _spreading || TS.Count == 1 ? 1 : material.ReflectionSamples
                : 0);

        // A surface whose light comes up from under it takes none of the lamps' as a matte surface would:
        // that light comes up from where its walks under the surface lead, and is added apart, while the
        // rest of its shading -- its highlight, its ambient -- is as ever; see Scene.Subsurface.
        bool underneath = ComesUpFromUnder(intersection, material, transparency);
        bool through = !underneath && PassesThroughTheSheet(intersection, material, transparency);

        if (underneath || through)
            intersection.Material = material.WithoutDiffuse();

        // Within the reach of a hair whose strand is gathering light, what the lamps give here, under the surface as
        // well as on it, is the hair light's to carry; see GetHitColor.
        if (!fromBeyond)
        {
            // Under a film, the film colors a metal's highlight as it colors what the metal mirrors.
            foreach (Light light in Lights)
                surfaceColor += Illuminate<TS>(
                    light, intersection, ownShare, looks, glass, looksThrough, film, coated, facets,
                    film is null ? metal : null);
        }

        if ((underneath || through) && fromBeyond)
            intersection.Material = material;
        else if (underneath || through)
        {
            intersection.Material = material;

            TS walked = underneath
                ? UnderTheSurface<TS>(intersection, material, ownShare, film)
                : ThroughTheSheet<TS>(intersection, material, ownShare, film);

            // The walks' light reached the surface from all round and leaves it through the coat.
            if (coat is not null)
                walked *= coat.ThroughFromAround<TS>(cosEye, intersection.N1);

            if (sheen is not null)
                walked *= sheen.Beneath(cosEye);

            surfaceColor += walked;
        }

        // The light it gives off of its own (see GlowGiven), of which a glowing surface the scene looks at as a light
        // gives a ray only what the light's samples leave it.
        if (material.Emission is not null)
        {
            TS given = GlowGiven(intersection, material, ownShare, film, coat, cosEye, sheen);

            if (GlowingSurfaces is not null && GlowingSurfaces.Holds(intersection) &&
                GlowShareOf(intersection) is var share and < 1)
                given *= share;

            surfaceColor += given;
        }

        // The light mirrors and glass gather onto it from the lamps, when the scene asks for caustics, which
        // it takes as it takes a lamp's; see Scene.Caustics.
        if (_caustics is not null && material.Diffuse > 0 && transparency < 1 && (metal is null || ownShare > 0))
        {
            TS gathered = CausticsAt<TS>(intersection, material, ownShare, film);

            if (coat is not null)
                gathered *= coat.ThroughFromAround<TS>(cosEye, intersection.N1);

            if (sheen is not null)
                gathered *= sheen.Beneath(cosEye);

            surfaceColor += gathered;
        }

        // And the light bounced onto it off everything else, which a surface takes as it takes a lamp's --
        // its own share of it, and a film's passing share -- when the scene asks for it; see Scene.Bounces.  Bare
        // metal has no share of it to take.
        if (DiffuseBounces > 0 && (metal is null || ownShare > 0))
        {
            TS bounced = BouncedOnto<TS>(intersection, material, remaining);

            if (!bounced.IsBlack)
            {
                bounced *= ownShare;

                if (film is { } passing)
                    bounced *= passing.Passed;

                if (coat is not null)
                    bounced *= coat.ThroughFromAround<TS>(cosEye, intersection.N1);

                if (sheen is not null)
                    bounced *= sheen.Beneath(cosEye);

                surfaceColor += bounced;
            }
        }

        // How much of the light the path brings back each way will be worth, so that a way worth next to
        // nothing is not followed, when the scene says to stop there; see Bailout.
        (double mirrors, double passes) = Bailout > 0
            ? SharesOf(intersection, material, transparency, film, metal)
            : (1, 1);
        TS reflectedColor = Worth(mirrors) ? FollowedFor<TS>(mirrors, intersection, remaining, true) : TS.Black;
        TS refractedColor = Worth(passes) ? FollowedFor<TS>(passes, intersection, remaining, false) : TS.Black;
        TS refColor;

        // A film mirrors its share of each color and lets the rest on, through a surface that lets
        // light through; what reaches an opaque surface's own color has been charged already.
        if (film is { } shares)
            refColor = reflectedColor * shares.Mirrored + refractedColor * shares.Passed;
        // Smooth glass shares its light between what it mirrors and what it lets through by one
        // Fresnel share for the whole surface.  Rough glass has already shared it facet by facet.
        else if (material.Reflective > 0 && transmits && !material.IsRough)
        {
            double reflectance = intersection.Reflectance;

            refColor = reflectedColor * reflectance +
                       refractedColor * (1 - reflectance);
        }
        else
            refColor = reflectedColor + refractedColor;

        // A surface shows only as much of itself as it stops.  Perfectly clear glass therefore
        // contributes nothing of its own and is seen entirely through, which is what makes a
        // transmitting pigment a window rather than merely a dimmer wall.
        TS color = sparesHighlight ? surfaceColor + refColor : surfaceColor * (1 - transparency) + refColor;

        // If this hit is on the far side of a surface, the ray reached it by travelling through
        // whatever the surface is made of, and a substance that fades light charges for the trip.
        // Everything gathered here made that crossing -- what came from beyond, and what the inner
        // wall itself reflected -- so the fade is applied to all of it at once.  The distance is
        // the ray's own, which is exactly the path through the substance because the ray began
        // where the light entered it.
        if (intersection.Inside)
        {
            color *= material.Interior.GetFadeOver(intersection.Distance);

            // And whatever fills the surface has its say over the same span, for the same reason.
            if (material.Interior.Medium is not null)
            {
                color = ThroughMedium(
                    material.Interior.Medium, color, RayThatReached(intersection),
                    intersection.Distance, intersection.Surface, intersection.Portals);
            }
        }

        return color;
    }

    /// <summary>
    /// This property holds how many surfaces a ray from the eye may be mirrored or bent at before it is
    /// followed no further; see <see cref="RayTracer.General.RenderContext.TraceDepth"/>.
    /// </summary>
    public int TraceDepth { get; set; } = 4;

    /// <summary>
    /// This property holds the least share of its pixel a way a ray is mirrored or bent must be worth to be
    /// followed, or nought to follow every way the depth allows.
    /// <para>
    /// **Glass sends every ray both ways**, mirrored and let through, so the ways a ray goes double at every
    /// surface: a tumbler of water, looked through, crosses eight, and asked to follow them all twelve deep a
    /// render took fourteen times as long as at four.  Nearly all those ways are worth nothing -- a twenty-fifth of
    /// a twenty-fifth of what is left after the first mirroring -- and a way worth less than the smallest step
    /// a picture can show is not followed (POV-Ray's adaptive depth control does the same).
    /// </para>
    /// </summary>
    public double Bailout { get; set; }

    // How much of its pixel the way being followed has fallen short of, so that nought is the whole of it.
    [ThreadStatic]
    private static double _fallenShort;

    /// <summary>
    /// This method reports whether a way a ray goes, worth the given share of what reaches the surface, is
    /// worth following.
    /// </summary>
    private bool Worth(double share)
    {
        return share > 0 && (Bailout <= 0 || (1 - _fallenShort) * share >= Bailout);
    }

    /// <summary>
    /// This method follows a ray mirrored or bent at a surface, carrying how much of its pixel it is worth.
    /// </summary>
    private TS FollowedFor<TS>(double share, Intersection intersection, int remaining, bool mirrored)
        where TS : struct, ISpectrum<TS>
    {
        if (Bailout <= 0)
        {
            return mirrored
                ? GetReflectionColor<TS>(intersection, remaining)
                : GetRefractedColor<TS>(intersection, remaining);
        }

        double was = _fallenShort;

        _fallenShort = 1 - (1 - was) * share;

        try
        {
            return mirrored
                ? GetReflectionColor<TS>(intersection, remaining)
                : GetRefractedColor<TS>(intersection, remaining);
        }
        finally
        {
            _fallenShort = was;
        }
    }

    /// <summary>
    /// This method works out roughly how much of what reaches a surface it mirrors and how much it lets
    /// through, as GetHitColor will weigh them: enough to say whether either is worth following.
    /// </summary>
    private static (double Mirrors, double Passes) SharesOf<TS>(
        Intersection intersection, Material material, double transparency, FilmShares<TS>? film, BareMetal? metal)
        where TS : struct, ISpectrum<TS>
    {
        bool transmits = material.Transparency > 0 || material.PigmentMayTransmit;
        double mirrors = material.Reflective > 0 || film is not null
            ? material.ReflectanceAt(intersection.Eye.Dot(intersection.Normal))
            : 0;
        double passes = transparency;

        if (film is { } shares)
            return (shares.Mirrored.Sum / TS.Count, shares.Passed.Sum / TS.Count * transparency);

        if (metal is { } bare)
            mirrors = bare.Average(intersection.Eye.Dot(intersection.Normal), mirrors);

        if (material.Reflective > 0 && transmits && !material.IsRough)
        {
            double reflectance = intersection.Reflectance;

            mirrors *= reflectance;
            passes *= 1 - reflectance;
        }

        if (material.SheenApplies)
            mirrors *= material.Sheen.Beneath(intersection.Eye.Dot(intersection.Normal));

        // A coat mirrors its own share, and what the surface beneath mirrors crosses it twice.
        if (CoatOn(intersection, material) is { } coat)
        {
            double cos = intersection.Eye.Dot(intersection.Normal);

            mirrors = mirrors * coat.Through<TS>(cos, cos, intersection.N1).Sum / TS.Count +
                      coat.ReflectanceAt(cos, intersection.N1);
        }

        return (mirrors, passes);
    }

    /// <summary>
    /// This method works out the color one light lends the given surface point, shading it once
    /// from each place the light is looked at and averaging.
    /// <para>
    /// Every light but an area light is looked at from a single place, and that case is kept apart
    /// and left exactly as it always was: no averaging, no extra arithmetic, so a scene lit by
    /// lamps and suns comes out bit for bit as it did before area lights existed.  An area light is
    /// looked at from several places across its face; where some are blocked and some are not, the
    /// average is a gray rather than a black or a white, which is the soft edge of its shadow.
    /// </para>
    /// </summary>
    /// <param name="light">The light lending its color.</param>
    /// <param name="intersection">The surface point being lit.</param>
    /// <param name="ownShare">How much of the light reaching the point the surface keeps to show as
    /// its own color rather than mirroring; see <see cref="Material.Fresnel"/>.</param>
    /// <param name="film">How the surface's film shares out the light, for a surface that has one.</param>
    /// <param name="metal">What is bare metal there, whose highlight takes the metal's color, or <c>null</c>.</param>
    /// <returns>The color the light lends the point.</returns>
    private TS Illuminate<TS>(
        Light light, Intersection intersection, double ownShare, int looks, (double, double)? glass,
        int looksThrough, FilmShares<TS>? film, (Coat Coat, double Outside, int Looks)? coated = null,
        Facets facets = null, BareMetal? metal = null)
        where TS : struct, ISpectrum<TS>
    {
        // A light standing on something moving is where that stood at this ray's instant.
        light = light.At(intersection.TimeIndex);

        int count = light.SampleCount;

        // A place met along a path of bounced light asks each lamp about one of its samples, picked by a
        // fixed function of the place, rather than all of them: a fair guess at the whole, which the
        // many paths from the first surface average out, and the difference between a second bounce
        // costing what the first did and costing its square.  See Scene.Bounces.
        int one = count > 1 && _bounced > 0
            ? Math.Min(count - 1, (int) (ShiftFor(intersection.Point, 111) * count))
            : 0;

        if (count == 1 || _bounced > 0)
        {
            LightSample only = light.SampleToward(intersection.LitPoint, one, intersection.Normal);

            return light.ApplyPhong(
                intersection.LitPoint, intersection.Eye, intersection.Normal, intersection.Surface,
                only, ReachingFrom<TS>(intersection, only, glass, light, one),
                intersection.Footprint, intersection.Portals, ownShare,
                ShareOf(light, only, one, intersection, looks, glass, looksThrough, facets), glass,
                intersection.Material, film, highlights: _bounced == 0,
                coat: CoatShareOf(light, only, intersection, coated), facets: facets, metal: metal);
        }

        TS sum = TS.Black;

        for (int index = 0; index < count; index++)
        {
            LightSample sample = light.SampleToward(
                intersection.LitPoint, index, intersection.Normal);

            sum += light.ApplyPhong(
                intersection.LitPoint, intersection.Eye, intersection.Normal, intersection.Surface,
                sample, ReachingFrom<TS>(intersection, sample, glass, light, index),
                intersection.Footprint, intersection.Portals, ownShare,
                ShareOf(light, sample, index, intersection, looks, glass, looksThrough, facets), glass,
                intersection.Material, film, coat: CoatShareOf(light, sample, intersection, coated),
                facets: facets, metal: metal);
        }

        return sum * (1.0 / count);
    }

    /// <summary>
    /// This method returns how much of a light's sample reaches the point being lit.  A shadow ray sets
    /// off from the lit point, on the eye's side -- except toward a lamp behind rough glass, whose glow
    /// comes through the surface.
    /// <para>
    /// **That glow is given at the last of the glass between the lamp and the eye, and only there.**
    /// Light through a pane is bent going in and again coming out, each time off a rough facet; the
    /// rays the glass spreads at its near side take care of the first of those, and the lamp is
    /// found exactly at the far side, where the light has only the one surface left to cross.  Given at
    /// the near side as well, the lamp would be counted twice -- and the near side's count is the
    /// wrong one, treating the lamp as if it stood inside the glass.  So a shadow ray from the near
    /// side that meets more of the same glass on its way finds nothing, and the glow is left to that
    /// glass's far side.  It sets off from just beyond the surface, the surface it is coming through
    /// being already accounted for.
    /// </para>
    /// <para>
    /// The same holds for a lamp in front of a surface of the glass seen from inside it.  Its light
    /// crossed the glass to get there, and is given where it crossed: inside a pane, the near side
    /// glows with it, seen from within.  A shadow ray straight back through the glass would count it a
    /// second time, and unbent, at angles no light that came in through the glass could take -- the far
    /// side mirroring it entirely, where light that has come in through a pane can never be turned back
    /// that way.
    /// </para>
    /// </summary>
    private TS ReachingFrom<TS>(
        Intersection intersection, LightSample sample, (double, double)? glass, Light light, int index = -1)
        where TS : struct, ISpectrum<TS>
    {
        // A sample worth nothing -- a spotlight's, past the edge of its cone; the glowing surfaces', where it found
        // none of them -- is worth nothing however much of it would reach, and is spared its shadow ray.
        if (sample.Cone == 0)
            return TS.Black;

        // While a blend's two halves shade one place, each lamp's sample is looked for once; glass, whose shadow
        // rays depend on the glass, is looked for each time.
        if (Reached<TS>.Sharing > 0 && glass is null && index >= 0)
        {
            Reached<TS>.Memo ??= [];

            if (Reached<TS>.Memo.TryGetValue((intersection, light, index), out TS known))
                return known;

            return Reached<TS>.Memo[(intersection, light, index)] = ReachingFrom<TS>(intersection, sample, null, light);
        }

        if (glass is null || sample.Direction.Dot(intersection.Normal) >= 0)
        {
            TS reaching = GetLightReaching<TS>(
                intersection.LitPoint, sample.Direction, sample.Distance, intersection.TimeIndex,
                glass is not null && intersection.Inside ? intersection.SurfaceMaterial : null,
                false, intersection.FootprintRadius, light.Housing);

            // Something standing inside a medium is lit through it, as the medium's own stuff is: the
            // medium stands in the light's way as far as the edge of the surface it fills.  Unless that surface
            // casts no shadow: then its medium stands in the way of no light reaching anything outside it, and so of
            // none reaching anything inside it either -- charged inside and not out, ground running under a glow's
            // shell came out darker inside it than out, with an edge where the shell met the ground.
            if (!intersection.Inside && !reaching.IsBlack &&
                intersection.ContainerBefore is { NoShadow: false } container &&
                !intersection.ContainerPortals.CastsNoShadow &&
                intersection.InteriorBefore.Medium is { } medium)
            {
                reaching *= SurvivingTheMedium<TS>(
                    medium, container, intersection.ContainerPortals, intersection.LitPoint, sample,
                    intersection.TimeIndex, []);
            }

            return reaching;
        }

        Point beyond = intersection.Inside ? intersection.OverPoint : intersection.UnderPoint;

        return GetLightReaching<TS>(
            beyond, sample.Direction, sample.Distance, intersection.TimeIndex,
            intersection.SurfaceMaterial, false, intersection.FootprintRadius,
            light.Housing);
    }

    /// <summary>
    /// This class holds, for each kind of light carried, the light found reaching a place from each lamp's samples
    /// while the two halves of a blend between a smooth material and a rough one shade it (see
    /// <see cref="GetHitColor{TS}"/>), keyed by the place, the lamp and the sample: what reaches a place does not
    /// depend on what it is made of.  It is emptied once the outermost such shading is done.
    /// </summary>
    private static class Reached<TS>
        where TS : struct, ISpectrum<TS>
    {
        [ThreadStatic]
        internal static int Sharing;

        [ThreadStatic]
        internal static Dictionary<(Intersection, Light, int), TS> Memo;
    }

    /// <summary>
    /// This method works out how much of a rough surface's highlight -- or, beyond rough glass, its glow
    /// -- one sample of a light gives.  A glowing volume beyond the glass is shared with the glass's
    /// rays place by place, exactly as one in front of it is shared with the surface's reflection (see
    /// <see cref="HighlightShareOf"/>), with the odds of the glass bending its rays that way in place
    /// of the odds of mirroring them; the glow being given only at the last of the glass, what lies
    /// between is one straight run, as a reflection's is.
    /// </summary>
    private double? ShareOf(
        Light light, LightSample sample, int index, Intersection intersection, int looks,
        (double, double)? glass, int looksThrough, Facets facets = null)
    {
        if (glass is null || sample.Direction.Dot(intersection.Normal) >= 0)
            return HighlightShareOf(light, sample, intersection, looks, facets);

        bool glowing = light is EmissionLight && sample.Odds > 0;

        if (!glowing && (light is not VolumeLight glow || !Mixes(glow) || sample.Odds <= 0))
            return light.CanBeSeen ? 0 : LookingShareOf(light, index, intersection);

        if (looksThrough == 0)
            return 1;

        Material material = intersection.Material;
        double byLight = glowing
            ? light.SampleCount * sample.Odds
            : ((VolumeLight) light).WeightOf(sample.Odds, sample.Distance);
        double byLooking = looksThrough * Microfacets.RefractionOdds(
            intersection.Normal, intersection.Eye, sample.Direction, material.Roughness * material.Roughness,
            intersection.N1, intersection.N2);

        return byLight / (byLight + byLooking);
    }

    /// <summary>
    /// This method works out how much of a rough surface's highlight one sample of a light gives.
    /// <para>
    /// **A glowing volume reaches a rough surface two ways**: as a highlight, from the places the light
    /// picks inside itself, and in what the surface mirrors, when one of the directions it looks in
    /// passes through the glow.  Counting both in full would count it twice; giving it to one alone
    /// fails one way or the other.  The highlight alone shows a lantern on a near-mirror as the cluster
    /// of its own sample points; the reflection alone finds a small bright lantern so rarely that its
    /// streak across a wet road comes out as glitter.
    /// </para>
    /// <para>
    /// **So each place in the glow is shared** between the two ways, by how likely each was to find it
    /// (Veach's balance heuristic).  The light picks a place with the odds its sample carries, which,
    /// seen from the surface, crowd together with the square of the distance; the reflection picks it
    /// with the odds of looking that way, times how many ways it looks.  Where the reflection is
    /// nearly a mirror its odds are enormous along the one direction it looks and it takes nearly all;
    /// where it is broad the light's own samples take nearly all.  What one way is given here, the other
    /// is given in <see cref="MixingLightFor"/>, and the two always come to the whole.
    /// </para>
    /// <para>
    /// A smooth sky reaches a rough surface through its reflection only, being large and smooth enough
    /// for that to settle well, and so does a glowing volume inside a shell that bends light -- the
    /// reflection's rays bend going in, and the light's own samples do not, so the two would not be
    /// finding the same places.
    /// </para>
    /// <para>
    /// **A sky that is a picture is shared, direction by direction**, since its sun is as small and as
    /// bright as any lantern and a reflection looking for it at random scatters it into glitter.  The
    /// sky picks a direction with the odds of all its samples together (see
    /// <see cref="SkyLight.OddsToward"/>), and the reflection with the odds of looking that way times how
    /// many ways it looks -- both per unit of solid angle, so no distance comes into it.  What the
    /// highlight is given here, the reflection is given in <see cref="SkyShareOf"/>.  Only the sky the
    /// scene shows is shared, and only at the light it shows: a sky light carrying a picture of its own,
    /// or tinting or dimming the scene's, lights the surface with a sky its reflection never sees.
    /// </para>
    /// </summary>
    /// <param name="light">The light.</param>
    /// <param name="sample">The place the light is being looked at from.</param>
    /// <param name="intersection">The point being lit.</param>
    /// <param name="looks">How many directions the point's own reflection will look in.</param>
    /// <returns>The share, or <c>null</c> to leave it to the light, as for a surface that is not
    /// rough.</returns>
    private double? HighlightShareOf(
        Light light, LightSample sample, Intersection intersection, int looks, Facets facets = null)
    {
        Material material = intersection.Material;

        if (!material.IsRough)
            return null;

        return LobeShareOf(light, sample, intersection, looks, material.Roughness * material.Roughness, facets);
    }

    /// <summary>
    /// This method works out how much of a clear coat's highlight one sample of a light gives: a smooth coat's
    /// mirror finds the lights a ray can meet and leaves the rest to its highlight, and a rough one shares
    /// them between the two as a rough surface does.  See <see cref="HighlightShareOf"/>.
    /// </summary>
    /// <returns>The coat, the index outside it and the share, or <c>null</c> for a surface with no coat.</returns>
    private (Coat Coat, double Outside, double Share)? CoatShareOf(
        Light light, LightSample sample, Intersection intersection, (Coat Coat, double Outside, int Looks)? coated)
    {
        if (coated is not { } over)
            return null;

        double share = over.Coat.IsRough
            ? LobeShareOf(light, sample, intersection, over.Looks, over.Coat.Alpha)
            : light.CanBeSeen ? 0 : 1;

        return (over.Coat, over.Outside, share);
    }

    /// <summary>
    /// This method works out how much of one rough lobe's highlight -- a rough surface's, or a rough coat's,
    /// its facets leaning as far as the given alpha says -- one sample of a light gives, against what the
    /// lobe's own reflection finds in the given number of looks.  See <see cref="HighlightShareOf"/>.
    /// </summary>
    private double LobeShareOf(
        Light light, LightSample sample, Intersection intersection, int looks, double alpha, Facets facets = null)
    {
        if (light is SkyLight sky && ReferenceEquals(sky, SharedSky))
        {
            if (looks == 0)
                return 1;

            double byTheSky = sky.OddsToward(intersection.LitPoint, sample.Direction, intersection.Normal);
            double byReflecting = looks * (facets?.DirectionOdds(intersection.Eye, sample.Direction) ??
                                           Microfacets.DirectionOdds(
                                               intersection.Normal, intersection.Eye, sample.Direction, alpha));

            return byTheSky + byReflecting > 0 ? byTheSky / (byTheSky + byReflecting) : 1;
        }

        // The glowing surfaces' light picks its ways as the reflection does, per unit of solid angle, so the two
        // are weighed with no distance between them; what the highlight is given here, the reflection is given
        // in GlowShareOf.
        if (light is EmissionLight && sample.Odds > 0)
        {
            if (looks == 0)
                return 1;

            double byGlowing = light.SampleCount * sample.Odds;
            double byLookingAt = looks * (facets?.DirectionOdds(intersection.Eye, sample.Direction) ??
                                          Microfacets.DirectionOdds(
                                              intersection.Normal, intersection.Eye, sample.Direction, alpha));

            return byGlowing / (byGlowing + byLookingAt);
        }

        if (light is not VolumeLight glow || !Mixes(glow) || sample.Odds <= 0)
            return light.CanBeSeen ? 0 : 1;

        if (looks == 0)
            return 1;

        double byLight = glow.WeightOf(sample.Odds, sample.Distance);
        double byLooking = looks * (facets?.DirectionOdds(intersection.Eye, sample.Direction) ??
                                    Microfacets.DirectionOdds(
                                        intersection.Normal, intersection.Eye, sample.Direction, alpha));

        return byLight / (byLight + byLooking);
    }

    /// <summary>
    /// This method reports whether a glowing volume can be shared between a rough surface's highlight
    /// and its reflection: whether a ray goes into it without bending, the shell having the index of
    /// the space around it.
    /// </summary>
    private bool Mixes(VolumeLight glow)
    {
        double index = glow.Surface.Material?.Interior.IndexOfRefraction ?? Environment.IndexOfRefraction;

        return Math.Abs(index - Environment.IndexOfRefraction) < 1e-12;
    }

    /// <summary>
    /// This method returns the glowing volume a medium belongs to, when a ray is crossing it along a
    /// rough surface's reflection, so that its glow is shared with the volume's own samples.  The ray
    /// must still be on the reflection's straight run, having gone nowhere but on through anything
    /// that does not bend it; one that reaches the glow any other way keeps all of it.
    /// </summary>
    /// <param name="medium">The medium being crossed.</param>
    /// <param name="ray">The ray crossing it.</param>
    /// <returns>The volume light, or <c>null</c> if the glow is not to be shared.</returns>
    private VolumeLight MixingLightFor(Medium medium, Ray ray)
    {
        if (_looking is not { } looking)
            return null;

        VolumeLight glow = null;

        foreach (Light light in Lights)
        {
            if (light is VolumeLight volume && ReferenceEquals(volume.Medium, medium))
            {
                glow = volume;
                break;
            }
        }

        if (glow is null || !Mixes(glow))
            return null;

        return OnRun(ray, looking.Origin, looking.Direction) ? glow : null;
    }

    /// <summary>
    /// This method reports whether a ray is still on a straight run: heading the same way, from a point
    /// on the line the run set off along.
    /// </summary>
    private static bool OnRun(Ray ray, Point origin, Vector direction)
    {
        if (ray.Direction.Unit.Dot(direction) < 1 - 1e-9)
            return false;

        Vector offset = ray.Origin - origin;
        double along = offset.Dot(direction);

        return (offset - direction * along).Magnitude <= 1e-6 * (1 + Math.Abs(along));
    }

    /// <summary>
    /// This method returns the share of a glowing volume's glow, at one place in it, that belongs to the
    /// rough surface's reflection now looking through it -- what is left once the volume's own samples
    /// have taken theirs; see <see cref="HighlightShareOf"/>.
    /// </summary>
    /// <param name="glow">The volume light.</param>
    /// <param name="where">The place in the glow.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening the place is asked about at.</param>
    /// <returns>The reflection's share.</returns>
    private static double ReflectionShareAt(VolumeLight glow, Point where, int timeIndex)
    {
        LookingRun run = _looking!.Value;
        double distance = (where - run.Origin).Magnitude;
        double byLooking = run.Count * run.Odds;
        double byLight = glow.WeightOf(glow.OddsAt(where, timeIndex), distance);

        return byLooking + byLight > 0 ? byLooking / (byLooking + byLight) : 1;
    }

    /// <summary>
    /// This method returns how much of the given light reaches the given point: white, if the
    /// light is in full view of it, black, if something opaque stands in the way, and something
    /// in between if what stands in the way lets light through.
    /// <para>
    /// Every surface between the point and the light is charged, rather than only the nearest one,
    /// since light has to survive all of them to arrive.  A surface that lets light through also
    /// gets to color it, which is what casts a green shadow under green glass.  What this
    /// deliberately does not do is bend the shadow ray on its way through: light really is
    /// refracted into the bright and dark bands of a caustic, but finding them means tracing
    /// forward from the light rather than backward from the surface.  That is what the caustics'
    /// photon map does when a scene asks for it (see <c>Scene.Caustics</c>), and then glass that bends
    /// light stops the shadow ray instead, its light being the photons'.
    /// </para>
    /// </summary>
    /// <param name="light">The light source in question, looked at from where it lies.</param>
    /// <param name="point">The point to test.</param>
    /// <returns>The fraction of the light's color that arrives at the point.</returns>
    public Color GetLightReaching(Light light, Point point)
    {
        LightSample sample = light.SampleToward(point, 0);

        return GetLightReaching(point, sample.Direction, sample.Distance);
    }

    /// <summary>
    /// This method returns how much light arrives at the given point from the given direction, up
    /// to the given distance.  It is the same reckoning as the light-wide form, told the way in
    /// terms of a bare direction so that an area light may ask it once per sample across its face.
    /// </summary>
    /// <param name="point">The point to test.</param>
    /// <param name="direction">The unit direction from the point toward the light or sample.</param>
    /// <param name="distance">How far off the light or sample is, past which nothing can shade
    /// the point; infinite for a distant light.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to look for blockers at, so
    /// that a moving thing casts its shadow from where it stands at that instant.</param>
    /// <returns>The fraction of the light's color that arrives at the point.</returns>
    public Color GetLightReaching(
        Point point, Vector direction, double distance, int timeIndex = 0)
    {
        return GetLightReaching<RgbSpectrum>(point, direction, distance, timeIndex).ToColor();
    }

    /// <summary>
    /// This method returns how much light arrives at the given point from the given direction, up
    /// to the given distance, in whatever bands the render is carrying light in.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="point">The point to test.</param>
    /// <param name="direction">The unit direction from the point toward the light or sample.</param>
    /// <param name="distance">How far off the light or sample is.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to look for blockers at.</param>
    /// <param name="glass">For a lamp glowing through rough glass, the glass: meeting any more of it on
    /// the way means the lamp's glow is given at the glass's far side instead, so none arrives here.
    /// </param>
    /// <returns>The fraction of each band of the light that arrives at the point.</returns>
    public TS GetLightReaching<TS>(
        Point point, Vector direction, double distance, int timeIndex = 0, Material glass = null)
        where TS : struct, ISpectrum<TS>
    {
        return GetLightReaching<TS>(point, direction, distance, timeIndex, glass, false, 0);
    }

    /// <summary>
    /// This method returns how much light arrives at the given point from the given direction, up to the
    /// given distance.
    /// <para>
    /// **Light through hair arrives by way of the hair.**  Each strand it crosses sends most of it onward,
    /// and that is what reaches a surface beneath it -- unless what is being lit is itself a strand, for
    /// which light scattered by other strands is gathered ahead of time; see <see cref="HairLight"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="point">The point to test.</param>
    /// <param name="direction">The unit direction from the point toward the light or sample.</param>
    /// <param name="distance">How far off the light or sample is.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to look for blockers at.</param>
    /// <param name="glass">See <see cref="GetLightReaching{TS}(Point, Vector, double, int, Material)"/>.</param>
    /// <param name="strandsStop">Whether a strand of hair stops the light, as it does for another strand.</param>
    /// <param name="footprint">How wide, as a radius, the ray that found the point had grown there: a strand
    /// of hair finer than that in the way blocks only its share of the light.  Nought for a line.</param>
    /// <param name="housing">What closes round the light, which does not shadow it; see
    /// <see cref="Light.Housing"/>.</param>
    /// <returns>The fraction of each band of the light that arrives at the point.</returns>
    internal TS GetLightReaching<TS>(
        Point point, Vector direction, double distance, int timeIndex, Material glass, bool strandsStop,
        double footprint, IReadOnlySet<(Surface, Portals)> housing = null)
        where TS : struct, ISpectrum<TS>
    {
        StrandTally tally = !_hasStrands ? null
            : strandsStop ? new StrandTally()
            : new StrandTally(strand => MostPassedBy<TS>(strand, point + direction * strand.Distance));
        Ray ray = new (point, direction, timeIndex, baseRadius: _hasStrands ? footprint : 0)
        {
            Tally = tally,
            NarrowsAt = _hasStrands && footprint > 0 ? distance : double.PositiveInfinity
        };
        TS reaching = TS.White;
        List<Intersection> crossings = IntersectWithin(ray, distance);

        // Strands in the way that let next to nothing past stopped the search for more of them.
        if (ray.Tally is { Stopped: true })
            return TS.Black;

        foreach (Intersection intersection in crossings)
        {
            // Only things that actually stand between the point and the light can shade it;
            // anything behind the point, or beyond the light, is irrelevant.  Zero counts as being
            // in the way, which is what Hit() has always taken it to mean -- rays start at a point
            // nudged off the surface precisely so that a grazing hit at no distance at all is a
            // real occluder rather than the surface shading itself.
            //
            // A shared shape is one copy behind every instance of it, so whether it casts a shadow
            // is the instance's to say as much as its own.
            if (intersection.Distance < 0 || intersection.Distance >= distance ||
                intersection.Surface.NoShadow || intersection.Portals.CastsNoShadow)
                continue;

            // Nor does the bulb round a light standing in a lamp: the light leaves it through its glass.
            if (housing is not null && housing.Contains((intersection.Surface, intersection.Portals)))
                continue;

            // A strand of hair sends most of the light onward; a wire sends none of it.
            if (intersection is HairIntersection strand)
            {
                // Only the share of the ray the strand covers is stopped or sent onward by it.
                if (strandsStop || IsAWire(strand))
                {
                    if (strand.Coverage >= 1)
                        return TS.Black;

                    reaching *= 1 - strand.Coverage;

                    continue;
                }

                reaching *= strand.Coverage >= 1
                    ? OnwardThroughStrand<TS>(strand, ray)
                    : OnwardThroughStrand<TS>(strand, ray) * strand.Coverage + TS.White * (1 - strand.Coverage);

                if (reaching.IsBlack)
                    return TS.Black;

                continue;
            }

            Material material = intersection.SurfaceMaterial;

            if (glass is not null && ReferenceEquals(material, glass))
                return TS.Black;

            Interior interior = material.Interior;

            // Shadow rays never have their intersections prepared, since that work would be wasted
            // on all but the nearest hit, so what is needed of the crossing is worked out here --
            // and only when something actually asks for it.  The pigment is sampled where the light
            // crossed, so that patterned glass shadows as a pattern, and once sampled it serves for
            // both how much light gets past and what color it comes out.
            bool needsPigment = interior.Filter > 0 || material.PigmentMayTransmit;
            ThinFilm film = material.Film;
            Point where = needsPigment || interior.Refracts || film is not null
                ? ray.At(intersection.Distance)
                : null;

            // A decal fades where the surface turns away from it, and a flop paint shifts with the
            // angle it is seen at, so either needs the surface's normal too -- which a shadow ray has not
            // worked out, and nothing else here wants, so it is worked out only then.  A flop paint seen
            // along a shadow ray has no light in particular: it is taken as lit from the eye, which is
            // where the ray came from.
            Vector facing = needsPigment && (material.Decals is not null || material.PigmentSeesTheViewer)
                ? intersection.Surface.NormalAt(where, intersection)
                : null;
            Color surfaceColor = needsPigment
                ? material.GetColorFor(
                    intersection.Surface, where, facing, portals: intersection.Portals,
                    viewing: facing is null ? default : new Viewing(-ray.Direction.Unit, facing))
                : null;
            double transparency = surfaceColor is null
                ? material.Transparency
                : material.TransparencyFor(surfaceColor);

            if (transparency <= 0)
                return TS.Black;

            // With caustics asked for, the light through glass that bends it is the photons'; see
            // Scene.Caustics.
            if (StopsTheShadowRay(intersection, material))
                return TS.Black;

            reaching *= transparency;

            if (interior.Filter > 0 || interior.Refracts)
            {
                if (interior.Filter > 0)
                    reaching *= interior.GetFilterTint(TS.FromReflectance(surfaceColor));

                // Some of the light never gets in at all, being mirrored off the surface instead,
                // and how much depends on how glancing its approach is.  This is what keeps clear
                // glass from being invisible to its own shadow: struck head-on it lets nearly
                // everything through, but around the rim, where the light only grazes it, most is
                // turned away.  A glass ball therefore casts a faint shadow gathered into a dark
                // ring, which is close to what one really does -- what this cannot give is the
                // bright spot in the middle, where refraction gathers the light it bent aside.
                // That is a caustic, and finding it means tracing forward from the light, which a
                // scene asking for caustics does; such glass has already stopped this ray above.
                if (interior.Refracts && film is null)
                {
                    Vector normal = intersection.Surface.NormalAt(where, intersection);

                    reaching *= 1 - interior.GetReflectanceAt(
                        ray.Direction.Dot(normal), Environment.IndexOfRefraction);
                }
            }

            // A film turns its share of each color away -- so a bubble's shadow is faintly colored, the
            // complement of what it mirrors.  The light is taken to arrive from the surroundings at
            // either face, as for bare glass above: a shadow ray is not bent where it goes in, so on its
            // way out it would meet the far face at a slant the bent light never does, and find it
            // mirroring everything.
            if (film is not null)
            {
                Vector normal = intersection.Surface.NormalAt(where, intersection);
                double thickness = film.ThicknessAt(film.Pattern is null
                    ? null
                    : intersection.Surface.WorldToSurface(where, intersection.TimeIndex, intersection.Portals));

                reaching *= film.SharesAt<TS>(
                    thickness, Math.Abs(ray.Direction.Unit.Dot(normal)), Environment.IndexOfRefraction,
                    interior.IndexOfRefraction).Passed;
            }
        }

        // Fog dims a lamp as surely as it dims a hillside, so the light is charged for its own trip
        // through the surroundings.  Only what the medium takes out counts here; what it gives off
        // is its own light and not this lamp's.  Note what this means for a distant light, whose
        // trip is endless: an endless absorbing medium extinguishes it utterly, which is the honest
        // answer for a fog described as having no far side.  A fog with one is a bounded surface.
        return Environment.Medium is null
            ? reaching
            : reaching * Environment.Medium.GetTransmittanceOver<TS>(distance);
    }

    /// <summary>
    /// This method finds where a ray crosses the things in the world, over a stretch of that ray
    /// rather than the whole of it.  It is for asking what stands between a point and a light, which
    /// is the great majority of what a render asks: on the teapot scene, 98.9% of every ray fired is
    /// a shadow ray.
    /// <para>
    /// Such a query throws away every crossing behind the point and every crossing past the light --
    /// on that same scene, four crossings in five -- so anything whose box lies wholly in either
    /// stretch need never be looked at.  What is <em>not</em> pruned is anything beyond the nearest
    /// crossing found so far: see <see cref="BoundingVolumeHierarchy"/> for why that would be wrong
    /// here even though it is the usual trick.
    /// </para>
    /// <para>
    /// The list comes back unsorted, deliberately.  How much light survives the trip is the product
    /// of what each surface in the way lets through, and a product does not care what order it is
    /// taken in.
    /// </para>
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="maxDistance">How far along the ray to care about.</param>
    private List<Intersection> IntersectWithin(Ray ray, double maxDistance)
    {
        Statistics?.CountSceneRay();

        List<Intersection> intersections = [];

        _arranged.IntersectWithin(ray, intersections, maxDistance, Surfaces);

        // The ray's instant, which a crossing otherwise learns only when it is prepared -- and these are not --
        // so that what is asked of one (its normal, for glass's Fresnel, a film or a decal's facing) is asked
        // where the surface stands at that instant rather than where it stood as the shutter opened.
        if (ray.TimeIndex != 0)
        {
            foreach (Intersection crossing in intersections)
                crossing.TimeIndex = ray.TimeIndex;
        }

        return intersections;
    }

    /// <summary>
    /// This method returns whether the given point is in shadow with respect to the given
    /// light source, meaning that no light from it arrives at all.
    /// </summary>
    /// <param name="light">The light source in question.</param>
    /// <param name="point">The point to test.</param>
    /// <returns><c>true</c>, if the point is in shadow, or <c>false</c>, if not.</returns>
    public bool IsInShadow(Light light, Point point)
    {
        // Asked of the light as a whole, this looks at it from its first sample -- its one place
        // for a lamp or a sun, the center of its face for an area light.  A point in the soft half
        // of an area light's shadow is not wholly in shadow, so this reports the plain fact of
        // whether any light reaches at all.
        LightSample sample = light.SampleToward(point, 0);

        return GetLightReaching<RgbSpectrum>(point, sample.Direction, sample.Distance).IsBlack;
    }

    /// <summary>
    /// This method is used to determine the color of the reflected ray from the given
    /// intersection.
    /// </summary>
    /// <param name="intersection">The intersection to start with.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <returns>The reflected color.</returns>
    public Color GetReflectionColor(Intersection intersection, int remaining)
    {
        return GetReflectionColor<RgbSpectrum>(intersection, remaining).ToColor();
    }

    /// <summary>
    /// This method is used to determine the light along the reflected ray from the given
    /// intersection, in whatever bands the render is carrying light in.
    /// <para>
    /// For a surface with a film, it is all the light along that ray: what share of it the film mirrors
    /// is charged by <see cref="GetHitColor{TS}"/>, beside the share it lets through.
    /// </para>
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="intersection">The intersection to start with.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <returns>The reflected light.</returns>
    public TS GetReflectionColor<TS>(Intersection intersection, int remaining)
        where TS : struct, ISpectrum<TS>
    {
        Material material = intersection.Material;

        // What is bare metal mirrors by the metal's own share of each band; under a film, the film's shares
        // say it all.
        BareMetal? metal = material.Film is null ? material.BareMetalAt(intersection) : null;

        // A film mirrors something whatever its reflective says.  What it mirrors is returned whole, and
        // the film's share of each color charged where the hit is shaded, beside what it lets through.
        if (remaining < 1 ||
            (material.Reflective == 0 && material.Film is null && !material.CoatApplies && metal is null))
            return TS.Black;

        if (CoatOn(intersection, material) is { } coat)
            return GetCoatedReflection<TS>(intersection, material, coat, remaining, metal);

        if (material.Reflective == 0 && material.Film is null && metal is null)
            return TS.Black;

        TS color;

        if (material.IsRough)
            color = GetRoughReflection<TS>(intersection, material, remaining, metal);
        else
        {
            double cos = intersection.Eye.Dot(intersection.Normal);

            // How much is mirrored depends on the angle it is seen at, for a surface that says so.
            double reflective = material.Film is null
                ? material.ReflectanceAt(cos)
                : 1;

            // The cone goes on widening past the mirror, so what is seen in a reflection is filtered
            // for how far the light really travelled rather than for how far it is from the glass.  A
            // curved mirror also focuses or spreads the cone, which this does not attempt -- that wants
            // full ray differentials, and a flat mirror is the case that turns up.
            Ray reflectedRay = new (
                intersection.MirrorPoint, intersection.Reflect, intersection.TimeIndex,
                intersection.ConeSpread, intersection.ConeTravelled);

            color = metal is { } bare
                ? GetColorFor<TS>(reflectedRay, remaining - 1) * bare.Mirrors<TS>(cos, reflective)
                : GetColorFor<TS>(reflectedRay, remaining - 1) * reflective;
        }

        // What a cloth's sheen gives back of what it faces, the weave beneath never mirrors.
        if (material.SheenApplies)
            color *= material.Sheen.Beneath(intersection.Eye.Dot(intersection.Normal));

        // A metal colors what it mirrors, not just its highlight -- it is what makes a gold
        // surface throw back a gold scene rather than a plain one.  The angle here is the eye
        // against the normal, the true angle of incidence, rather than the approximation the
        // highlight has to settle for.  A material that is a metal has colored it already, by the metal's own.
        if (material.Metallic != 0 && material.Metal is null)
        {
            TS pigmentColor = TS.FromReflectance(material.GetColorFor(intersection));

            color *= material.GetMetallicTint(pigmentColor, intersection.Eye.Dot(intersection.Normal));
        }

        return color;
    }

    /// <summary>
    /// This method works out what a surface under a clear coat mirrors toward the eye: what the coat mirrors
    /// by its own index, and what the surface beneath it mirrors, charged for crossing the coat both ways.
    /// <para>
    /// **A smooth coat over a smooth mirror looks once**, the two mirroring in the same direction, and
    /// whatever that one ray brings back is shared by them both.  Anything rough looks for itself, as a
    /// rough surface does (see <see cref="GetRoughReflection{TS}"/>): a rough coat by its own facets, and
    /// metal flakes under a smooth coat by theirs.
    /// </para>
    /// </summary>
    private TS GetCoatedReflection<TS>(
        Intersection intersection, Material material, Coat coat, int remaining, BareMetal? metal)
        where TS : struct, ISpectrum<TS>
    {
        double outside = intersection.N1;
        double cos = intersection.Eye.Dot(intersection.Normal);
        TS color = TS.Black;
        TS mirrorWeight = TS.Black;

        if (material.Reflective > 0 || metal is not null)
        {
            // What lies beneath mirrors as it would bare, then crosses the coat in and out again -- at the
            // eye's angle both ways, the mirrored direction making the same angle with the normal.  A metal
            // beneath mirrors against the coat's own index, at the angle bent to inside it.
            TS beneath = coat.Through<TS>(cos, cos, outside);

            if (material.Metallic != 0 && material.Metal is null)
            {
                TS pigmentColor = TS.FromReflectance(material.GetColorFor(intersection));

                beneath *= material.GetMetallicTint(pigmentColor, cos);
            }

            if (material.IsRough)
                color += GetRoughReflection<TS>(intersection, material, remaining, metal) * beneath;
            else if (metal is { } bare)
                mirrorWeight += beneath * bare.Mirrors<TS>(cos, material.ReflectanceAt(cos));
            else
                mirrorWeight += beneath * material.ReflectanceAt(cos);
        }

        if (coat.IsRough)
        {
            color += LookedAtRoughly<TS>(
                intersection, coat.Roughness, value => coat.ReflectanceAt(value, outside),
                _spreading || TS.Count == 1 ? 1 : material.ReflectionSamples, remaining);
        }
        else
            mirrorWeight += TS.White * coat.ReflectanceAt(cos, outside);

        if (!mirrorWeight.IsBlack)
        {
            Ray reflectedRay = new (
                intersection.MirrorPoint, intersection.Reflect, intersection.TimeIndex,
                intersection.ConeSpread, intersection.ConeTravelled);

            color += GetColorFor<TS>(reflectedRay, remaining - 1) * mirrorWeight;
        }

        return color;
    }

    /// <summary>
    /// This method returns the clear coat a hit is seen through, or <c>null</c> where there is none: the
    /// material must have one that applies, and the hit must be on the outside of the surface, where the
    /// coat is.
    /// </summary>
    private static Coat CoatOn(Intersection intersection, Material material) =>
        !intersection.Inside && material.CoatApplies ? material.Coat : null;

    /// <summary>
    /// This field notes that a rough surface has already spread a ray out into many, somewhere back
    /// along the path being followed, so that any rough surface met after it looks in one direction
    /// only.  Without it two rough surfaces facing each other would multiply their samples at every
    /// bounce; with it the cost is paid once per path, as it is for a glass that spreads colors.
    /// </summary>
    [ThreadStatic]
    private static bool _spreading;

    /// <summary>
    /// This field holds how rough the roughest surface that has spread the path being followed is, so
    /// that a smooth surface met further along can be shaded as that rough; see
    /// <see cref="RegularizedFor"/>.  Nought while nothing has.
    /// </summary>
    [ThreadStatic]
    private static double _spreadRoughness;

    /// <summary>
    /// This method returns the material to shade a hit with: the surface's own -- unless the ray that
    /// found it had already been spread by a rough surface and the surface is a smooth mirror or clear
    /// glass, in which case it is that material made as rough as the roughest surface that spread the
    /// path.  The material chosen is left on the hit, for everything that shades it after.
    /// <para>
    /// **A mirror met after a rough bounce glitters if it stays a mirror.**  A glowing volume seen in a
    /// rough surface is shared between its own samples and the surface's rays, but only along the
    /// straight run a ray sets off on (see <see cref="HighlightShareOf"/>).  A ray that a wet road
    /// spread, mirrored on by a car's paint into a lantern, has left that run, and the lantern's own
    /// samples cannot be found through a mirror at all -- so only chance finds the lantern there, and
    /// the road under the car came out sprinkled with specks that no number of samples smoothed away.
    /// </para>
    /// <para>
    /// **So the mirror is made rough for that bounce**, as rough as what spread the ray -- path
    /// regularization, after Kaplanyan and Dachsbacher.  A rough surface shades a glowing volume both
    /// ways and shares it, so the lantern in the paint comes out a smooth blur rather than chance hits.
    /// What it costs is a little more blur in a mirror seen in a rough surface, which already blurs
    /// everything it shows by as much: the blur grows by about the square root of two, which is hard to
    /// see.  The camera's own view of a mirror is untouched, and so is any scene with nothing rough in it.
    /// </para>
    /// <para>
    /// Clear glass is frosted the same way.  But a transparent surface at the surroundings' own index is
    /// left alone: it bends nothing, so there is nothing to blur, and a lantern's shell is one.  Made
    /// rough, its rays would set off on a run of their own along which the lantern's share could not be
    /// reckoned, and the lantern would lose the rays' share of its light.
    /// </para>
    /// </summary>
    /// <para>
    /// A blend of whole materials by a pattern is settled here first, where the ray met it; see
    /// <see cref="MaterialBlend"/>.  A mixture of a smooth material and a rough one is left as it is: it is
    /// shaded as each of them, and each is made rough there (see <see cref="GetHitColor{TS}"/>).
    /// </para>
    /// <param name="intersection">The hit about to be shaded.</param>
    /// <returns>The material to shade it with.</returns>
    private static Material RegularizedFor(Intersection intersection)
    {
        Material material = SettledFor(intersection);

        return material.Split is null ? Regularized(intersection, material) : material;
    }

    /// <summary>
    /// This method returns the material a hit wears where the ray met it, its weather and any blend settled
    /// there, before anything is made rough for a spread ray; see <see cref="RegularizedFor"/>.
    /// </summary>
    /// <param name="intersection">The hit.</param>
    /// <returns>The material it wears there.</returns>
    private static Material SettledFor(Intersection intersection)
    {
        Material material = intersection.SurfaceMaterial;

        // What weather the surface has been out in makes its material a blend; see Weather.
        if (WeatherSettler.At(intersection) is { } weather && weather.Weathered(material) is var weathered &&
            !ReferenceEquals(weathered, material))
        {
            material = weathered;
            intersection.Material = material;
        }

        // A blend of materials is settled where the ray met the surface, and the hit keeps what it settled on.
        if (material.Blend is { } blend)
        {
            material = blend.At(intersection, Shading);
            intersection.Material = material;
        }

        return material;
    }

    /// <summary>
    /// This method returns the given material as rough as the surface that spread the ray, if it is a smooth mirror,
    /// clear glass or a smooth coat met by a spread ray; see <see cref="RegularizedFor"/>.
    /// </summary>
    /// <param name="intersection">The hit about to be shaded.</param>
    /// <param name="material">The material it wears there.</param>
    /// <returns>The material to shade it with.</returns>
    private static Material Regularized(Intersection intersection, Material material)
    {
        // A film is never made rough: what it mirrors depends on its being smooth.
        if (!_spreading || _spreadRoughness <= 0 || material.Film is not null)
            return material;

        bool transmits = material.Transparency > 0 || material.PigmentMayTransmit;
        bool beneath = !material.IsRough &&
                       (transmits ? intersection.N1 != intersection.N2 : material.Reflective > 0);

        // A smooth coat mirrors as a mirror does, whatever lies beneath it.
        bool coat = material.CoatApplies && !material.Coat.IsRough;

        if (!beneath && !coat)
            return material;

        material = material.RoughAs(_spreadRoughness, beneath);
        intersection.Material = material;

        return material;
    }

    /// <summary>
    /// This field holds the straight run a rough surface's reflection is looking along, while it is
    /// looking: where it set off, which way, how likely that way was to be picked and among how many.
    /// A glowing volume met along it shares its glow with the volume's own samples by those odds; one
    /// met any other way -- round a corner, through a mirror -- was not something the volume's samples
    /// could have found from that surface, and keeps all of it.
    /// </summary>
    [ThreadStatic]
    private static LookingRun? _looking;

    /// <summary>
    /// This record holds one straight run of a rough surface's reflection; see <see cref="_looking"/>.
    /// <see cref="Mirror"/> is the normal of the surface the run is a reflection off, and is left out
    /// for a run through glass.
    /// </summary>
    private readonly record struct LookingRun(
        Point Origin, Vector Direction, double Odds, int Count, NearSide Side = null, Vector Mirror = null);

    /// <summary>
    /// This record holds the near side of a pane of rough glass that a run set off through, for the
    /// lamps beyond it that the pane also aimed at; see <see cref="GlowsAimedThrough{TS}"/>.
    /// </summary>
    /// <param name="Normal">The surface normal on the eye's side.</param>
    /// <param name="Glass">The glass's material, which its far side must share.</param>
    /// <param name="Alpha">How far the glass's facets lean.</param>
    /// <param name="Outside">The index of refraction on the eye's side, taken for the lamp's too.</param>
    /// <param name="Inside">The glass's index of refraction.</param>
    private sealed record NearSide(Vector Normal, Material Glass, double Alpha, double Outside, double Inside);

    /// <summary>
    /// This method works out what a rough surface mirrors toward the eye: the blur of everything its
    /// facets show, looked for in several directions and averaged.
    /// <para>
    /// **Each direction is a facet the eye can see**, picked in proportion to how much of the view it
    /// fills (see <see cref="Microfacets.SampleVisibleNormal"/>), and the eye's ray mirrored in it.
    /// Picked that way, what each brings back needs weighing only by how much its facet mirrors at
    /// the angle it is seen at, and by how much of the surface the mirrored direction can see past the
    /// facets in front -- so a rough surface, seen across, both shines and darkens toward a graze, as a
    /// real one does.  A facet whose mirror image would point into the surface brings back nothing.
    /// </para>
    /// <para>
    /// **The directions are spread as evenly as they can be**, by the same two numbers walked against
    /// each other that the volume lights use, and the whole pattern is turned by an amount that is a
    /// fixed function of the point.  So neighboring points use different directions and the samples
    /// within a pixel average the steps away, while the same point gets the same directions every
    /// time: a render comes out the same twice over, and a still surface does not crawl when the
    /// camera moves.
    /// </para>
    /// <para>
    /// **Only the first rough surface along a path spreads.**  A ray already one of a spread, or one
    /// carrying a single band of a split color, looks in one direction, so that the cost is paid once.
    /// </para>
    /// <para>
    /// **Each ray carries only the cone it arrived with**, not one widened by the facets' lean, though
    /// what it sees is a blur.  The many directions already average whatever patterns they meet, and a
    /// widened cone made every pattern seen in a rough surface filter itself at its most expensive: on
    /// a wet street lit by lanterns, 60% more time for a picture that was the same to within a level.
    /// </para>
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="intersection">Where the ray met the rough surface.</param>
    /// <param name="material">The surface's material.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <returns>The light the surface mirrors toward the eye.</returns>
    private TS GetRoughReflection<TS>(
        Intersection intersection, Material material, int remaining, BareMetal? metal = null)
        where TS : struct, ISpectrum<TS>
    {
        int count = _spreading || TS.Count == 1 ? 1 : material.ReflectionSamples;

        // Rough glass mirrors by its own index, facet by facet, as it lets through the rest.
        Func<double, double> reflectance = material.Transparency > 0 || material.PigmentMayTransmit
            ? cos => material.Reflective *
                     Microfacets.GlassReflectance(cos, intersection.N1, intersection.N2)
            : material.ReflectanceAt;
        Facets facets = Facets.At(
            material, intersection.Surface, intersection.Point, intersection.Normal, intersection.TimeIndex,
            intersection.Portals, intersection as HairIntersection);

        // Bare metal mirrors each band by its own share, facet by facet.
        if (metal is { } bare)
        {
            return LookedAtRoughly<TS>(
                intersection, material.Roughness, _ => 1, count, remaining, facets,
                cos => bare.Mirrors<TS>(cos, material.ReflectanceAt(cos)));
        }

        return LookedAtRoughly<TS>(intersection, material.Roughness, reflectance, count, remaining, facets);
    }

    /// <summary>
    /// This method looks in the given number of directions off facets as rough as the given roughness, each
    /// mirroring by the given share at the angle it is seen at, and averages what they bring back.  It is
    /// <see cref="GetRoughReflection{TS}"/>'s looking, for any rough lobe: a rough surface's, or a coat's.  A
    /// metal's facets mirror each band by its own share, which <paramref name="tinted"/> gives, beside the share
    /// for every band.
    /// </summary>
    private TS LookedAtRoughly<TS>(
        Intersection intersection, double roughness, Func<double, double> reflectance, int count, int remaining,
        Facets facets = null, Func<double, TS> tinted = null)
        where TS : struct, ISpectrum<TS>
    {
        Vector normal = intersection.Normal;
        (Vector across, Vector along) = facets is null
            ? Microfacets.FrameAround(normal)
            : (facets.Along, facets.Across);
        Vector eye = new (intersection.Eye.Dot(across), intersection.Eye.Dot(along), intersection.Eye.Dot(normal));

        if (eye.Z <= 0)
            return TS.Black;

        double alpha = roughness * roughness;
        double turnOut = ShiftFor(intersection.Point, 101);
        double turnRound = ShiftFor(intersection.Point, 102);
        TS sum = TS.Black;
        bool was = _spreading;
        double wasRoughness = _spreadRoughness;
        LookingRun? wasLooking = _looking;

        _spreading = true;
        _spreadRoughness = Math.Max(wasRoughness, roughness);

        try
        {
            for (int index = 0; index < count; index++)
            {
                double first = (index + 0.5) / count + turnOut;
                double second = index * 0.6180339887498949 + turnRound;
                Vector facet = facets?.SampleVisibleNormal(eye, first - Math.Floor(first), second - Math.Floor(second)) ??
                               Microfacets.SampleVisibleNormal(
                                   eye, alpha, first - Math.Floor(first), second - Math.Floor(second));
                double cosFacet = eye.Dot(facet);
                Vector mirrored = facet * (2 * cosFacet) - eye;

                if (mirrored.Z <= 0)
                    continue;

                double weight = reflectance(cosFacet) *
                                (facets?.Masking(mirrored) ?? Microfacets.Masking(mirrored.Z, alpha));
                Vector direction = (across * mirrored.X + along * mirrored.Y + normal * mirrored.Z).Unit;
                // From the lit point still, which for rough glass met from inside is just outside it: how a
                // lamp's glow through rough glass is shared between its own light and these rays was worked
                // out with them setting off there, and setting off inside, as a smooth mirror's do (see
                // Intersection.MirrorPoint), made a lamp behind a frosted pane glitter.
                Ray ray = new (
                    intersection.LitPoint, direction, intersection.TimeIndex, intersection.ConeSpread,
                    intersection.ConeTravelled);

                _looking = new LookingRun(
                    intersection.LitPoint, direction,
                    facets is null
                        ? Microfacets.Masking(eye.Z, alpha) * Microfacets.Distribution(facet.Z, alpha) / (4 * eye.Z)
                        : facets.Masking(eye) * facets.Distribution(facet) / (4 * eye.Z),
                    count, Mirror: normal);

                sum += tinted is null
                    ? GetColorFor<TS>(ray, remaining - 1) * weight
                    : GetColorFor<TS>(ray, remaining - 1) * tinted(cosFacet) * weight;
            }
        }
        finally
        {
            _spreading = was;
            _spreadRoughness = wasRoughness;
            _looking = wasLooking;
        }

        return sum * (1.0 / count);
    }

    /// <summary>
    /// This method is used to determine the color of the refracted ray from the given
    /// intersection.
    /// </summary>
    /// <param name="intersection">The intersection to start with.</param>
    /// <param name="remaining">The remaining number of refraction recursions allowed.</param>
    /// <returns>The reflected color.</returns>
    public Color GetRefractedColor(Intersection intersection, int remaining)
    {
        return GetRefractedColor<RgbSpectrum>(intersection, remaining).ToColor();
    }

    /// <summary>
    /// This method is used to determine the light along the refracted ray from the given
    /// intersection, in whatever bands the render is carrying light in.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="intersection">The intersection to start with.</param>
    /// <param name="remaining">The remaining number of refraction recursions allowed.</param>
    /// <returns>The refracted light.</returns>
    public TS GetRefractedColor<TS>(Intersection intersection, int remaining)
        where TS : struct, ISpectrum<TS>
    {
        Material material = intersection.Material;

        // The pigment may say, color by color, how much light gets past it, so where it might,
        // it is sampled and has its say.  Sampled once here and reused for the filter below, since
        // both want the surface's color at the very same point.
        Color pigmentColor = material.PigmentMayTransmit || material.Interior.Filter > 0
            ? material.GetColorFor(intersection)
            : null;
        double transparency = pigmentColor is null
            ? material.Transparency
            : material.TransparencyFor(pigmentColor);

        if (remaining < 1 || transparency == 0)
            return TS.Black;

        TS color;

        // A ray carrying every band, crossing into or out of something that bends each band by its
        // own amount, goes as many ways as it has bands.
        if (TS.IsSpectral && TS.Count > 1 && intersection.Disperses)
            color = SplitThrough<TS>(intersection, remaining) * transparency;
        else if (material.IsRough)
            color = GetRoughRefraction<TS>(intersection, material, remaining) * transparency;
        else
        {
            Vector direction = Bent(intersection, intersection.N1, intersection.N2);

            if (direction is null)
                return TS.Black;

            Ray refracted = RefractedRay(intersection, direction);
            BounceRun? wasBouncing = _bouncing;

            // A search for bounced light goes on with its straight run through glass, bent or not, that a shadow ray
            // passes straight -- every glass, when the scene has no caustics -- since the lights' own samples have
            // already counted the sky, the glows and the glowing surfaces beyond it.  Bent off the run by a pane's
            // index, the search found the sky a second time.
            if (wasBouncing is { } run && !StopsTheShadowRay(intersection, material) &&
                OnRun(new Ray(intersection.Point, -intersection.Eye), run.Origin, run.Direction))
            {
                _bouncing = new BounceRun(refracted.Origin, refracted.Direction.Unit);
            }

            try
            {
                color = GetColorFor<TS>(refracted, remaining - 1) * transparency;
            }
            finally
            {
                _bouncing = wasBouncing;
            }
        }

        // Transparency says how much light gets through; the filter says what color it comes out.
        // Tinting toward the pigment's color at this very point, rather than toward some single
        // color for the whole surface, is what lets patterned glass filter as a pattern.  Like
        // the transparency it follows, this is charged once per surface crossed, so a solid tints
        // what passes through it twice -- going in, and coming back out.
        if (material.Interior.Filter > 0)
            color *= material.Interior.GetFilterTint(TS.FromReflectance(pigmentColor));

        return color;
    }

    /// <summary>
    /// This method works out which way a ray goes on through a crossing, by Snell's law.
    /// </summary>
    /// <param name="intersection">The crossing.</param>
    /// <param name="n1">The index of refraction the ray is leaving.</param>
    /// <param name="n2">The index of refraction it is entering.</param>
    /// <returns>The direction it goes on in, or <c>null</c> where it is reflected entirely instead.</returns>
    private static Vector Bent(Intersection intersection, double n1, double n2)
    {
        double ratio = n1 / n2;
        double cosI = intersection.Eye.Dot(intersection.Normal);
        double sin2T = ratio * ratio * (1 - cosI * cosI);

        if (sin2T > 1)
            return null;

        double cosT = Math.Sqrt(1 - sin2T);

        return intersection.Normal * (ratio * cosI - cosT) - intersection.Eye * ratio;
    }

    /// <summary>
    /// This method returns the ray that goes on through a crossing in the given direction.
    /// </summary>
    private static Ray RefractedRay(Intersection intersection, Vector direction)
    {
        Point point = intersection.Inside ? intersection.OverPoint : intersection.UnderPoint;

        return new Ray(
            point, direction, intersection.TimeIndex, intersection.ConeSpread, intersection.ConeTravelled);
    }


    /// <summary>
    /// This method works out what rough glass lets through toward the eye: the blur of everything
    /// beyond it, looked for through several of its facets and averaged -- what makes glass frosted.
    /// <para>
    /// It is <see cref="GetRoughReflection{TS}"/> turned to the far side: each direction is a facet the
    /// eye can see, the eye's ray bent through it rather than mirrored in it, and weighed by how much
    /// the facet lets through at the angle it is seen at and how much of the surface the bent ray can
    /// see past the facets around it.  A facet that would turn the light back entirely lets through
    /// nothing, its share being in what the glass mirrors.  Only the first rough surface along a path
    /// spreads its ray, and the directions are turned by a fixed amount per point, both as for what it
    /// mirrors.  A glowing volume a ray meets on its straight run out of the glass is shared with the
    /// volume's own glow through the glass (see <see cref="ShareOf"/>); and going into a pane, the pane
    /// aims at the lamps beyond it as well (see <see cref="GlowsAimedThrough{TS}"/>).
    /// </para>
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="intersection">Where the ray met the rough glass.</param>
    /// <param name="material">The glass's material.</param>
    /// <param name="remaining">The remaining number of recursions allowed.</param>
    /// <returns>The light the glass lets through toward the eye, before its transparency and filter.</returns>
    private TS GetRoughRefraction<TS>(Intersection intersection, Material material, int remaining)
        where TS : struct, ISpectrum<TS>
    {
        int count = _spreading || TS.Count == 1 ? 1 : material.ReflectionSamples;
        double turnOut = ShiftFor(intersection.Point, 103);
        double turnRound = ShiftFor(intersection.Point, 104);
        TS sum = TS.Black;
        TS aimed = TS.Black;
        bool was = _spreading;
        double wasRoughness = _spreadRoughness;
        LookingRun? wasLooking = _looking;
        double alpha = material.Roughness * material.Roughness;

        // Going into a pane, these rays find a lamp beyond it only by chance, so the pane aims at the
        // lamp as well.  Not through glass with a medium in it, whose fog would give its own glow to
        // the aimed rays as well as dimming the lamp's; there the rays find the lamp alone, as before.
        NearSide side = intersection.Inside || material.Interior.Medium is not null
            ? null
            : new NearSide(
                intersection.Normal, intersection.SurfaceMaterial, alpha,
                intersection.N1, intersection.N2);

        _spreading = true;
        _spreadRoughness = Math.Max(wasRoughness, material.Roughness);

        try
        {
            for (int index = 0; index < count; index++)
            {
                (Vector direction, double weight) = ThroughAFacet(
                    intersection, material, intersection.N1, intersection.N2,
                    (index + 0.5) / count + turnOut, index * 0.6180339887498949 + turnRound);

                if (direction is null)
                    continue;

                Ray ray = RefractedRay(intersection, direction);

                // Each sets off on a straight run, along which a glowing volume is shared with that
                // volume's own glow through the glass, as a reflection's run shares it with a highlight
                // -- and a lamp's glow at the pane's far side with the pane's aim at it.
                _looking = new LookingRun(
                    ray.Origin, direction, Microfacets.RefractionOdds(
                        intersection.Normal, intersection.Eye, direction, alpha, intersection.N1, intersection.N2),
                    count, side);

                sum += GetColorFor<TS>(ray, remaining - 1) * weight;
            }

            if (side is not null)
            {
                _looking = null;
                aimed = GlowsAimedThrough<TS>(intersection, side, count);
            }
        }
        finally
        {
            _spreading = was;
            _spreadRoughness = wasRoughness;
            _looking = wasLooking;
        }

        return side is null ? sum * (1.0 / count) : sum * (1.0 / count) + aimed;
    }

    /// <summary>
    /// This method works out the glow of the lamps beyond a pane of rough glass at the pane's far side,
    /// found by aiming at them from the near side.
    /// <para>
    /// **A lamp seen through both sides of a pane glitters if only the near side's rays look for it.**
    /// Through rough glass a lamp's glow is a narrow, enormously bright lobe -- hundreds of times white
    /// at the middle, through lightly frosted glass -- given at the far side (see
    /// <see cref="ReachingFrom{TS}"/>).  The near side spreads its rays without knowing where the lamp
    /// is, so around the glow, where the light is faint, one ray in a great many lands on that lobe and
    /// whitens its pixel by itself.  More rays only mean more chances to land there: the specks
    /// multiply rather than fade.
    /// </para>
    /// <para>
    /// **So the near side also aims at the lamp**: it picks directions through the glass from the far
    /// side's facets, as the lamp sees them -- the directions the lamp's light comes in along -- and
    /// asks the far side for the glow wherever each comes out.  Those are good just where the near side's
    /// own rays are poor, and poor where they are good, so each direction is shared between the two
    /// ways by how likely each was to pick it (Veach's balance heuristic), exactly as a glowing volume
    /// is shared with a rough surface's reflection; what the aim is given here, the rays are given in
    /// <see cref="LookingShareOf"/>, and the two always come to the whole.
    /// </para>
    /// <para>
    /// The far side's facets are taken to face the near side's way, which is exactly so for a pane and
    /// only roughly so for a ball; the aim is then less good, but the sharing still comes to the whole.
    /// It covers the one straight run across the glass: a lamp found any other way -- after the light
    /// has turned back inside, or through a second pane -- is left to the rays.  An area light is aimed
    /// at one of its places per direction, in turn.
    /// </para>
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="intersection">Where the eye's ray met the pane's near side.</param>
    /// <param name="side">The near side.</param>
    /// <param name="count">How many directions to aim along, the same as the near side's rays.</param>
    /// <returns>The aimed share of the lamps' glow, before the near side's transparency.</returns>
    private TS GlowsAimedThrough<TS>(Intersection intersection, NearSide side, int count)
        where TS : struct, ISpectrum<TS>
    {
        double turnOut = ShiftFor(intersection.Point, 105);
        double turnRound = ShiftFor(intersection.Point, 106);
        Point origin = intersection.UnderPoint;
        TS sum = TS.Black;

        foreach (Light each in Lights)
        {
            Light light = each.At(intersection.TimeIndex);

            if (light.CanBeSeen)
                continue;

            int samples = light.SampleCount;

            for (int index = 0; index < count; index++)
            {
                int which = index % samples;
                LightSample there = light.SampleToward(origin, which, side.Normal);

                if (!Aims(side, there))
                    continue;

                (Vector bent, _) = Bend(
                    -side.Normal, there.Direction, side.Alpha, side.Outside, side.Inside,
                    (index + 0.5) / count + turnOut, index * 0.6180339887498949 + turnRound);

                if (bent is null)
                    continue;

                Vector direction = -bent;
                double through = Microfacets.TransmittedHighlight(
                    side.Normal, intersection.Eye, direction, side.Alpha, side.Outside, side.Inside) / Math.PI;

                if (through <= 0)
                    continue;

                Ray ray = RefractedRay(intersection, direction);
                List<Intersection> hits = Intersect(ray);
                Intersection exit = hits.Hit();

                if (exit is null || !ReferenceEquals(exit.SurfaceMaterial, side.Glass))
                    continue;

                exit.PrepareUsing(ray, hits, Environment.IndexOfRefraction, TS.Wavelength);

                if (!exit.Inside)
                    continue;

                (double, double) sides = (exit.N1, exit.N2);
                LightSample sample = light.SampleToward(exit.LitPoint, which, exit.Normal);
                TS glow = light.GlowThrough(
                    exit.LitPoint, exit.Eye, exit.Normal, exit.Surface, sample,
                    ReachingFrom<TS>(exit, sample, sides, light), sides, exit.Footprint, exit.Portals,
                    RegularizedFor(exit));

                if (glow.IsBlack)
                    continue;

                double byAiming = AimedAt(which, count, samples) * AimOdds(side, there, direction);
                double byLooking = count * Microfacets.RefractionOdds(
                    side.Normal, intersection.Eye, direction, side.Alpha, side.Outside, side.Inside);

                sum += glow * (side.Glass.Interior.GetFadeOver(exit.Distance) * through /
                               (samples * (byLooking + byAiming)));
            }
        }

        return sum;
    }

    /// <summary>
    /// This method returns the share of a lamp's glow at the far side of a pane that belongs to the
    /// near side's ray now arriving there -- what is left once the near side's aim at the lamp has taken
    /// its own; see <see cref="GlowsAimedThrough{TS}"/>.  All of it, for a glow found any way the aim
    /// could not have found it.
    /// </summary>
    /// <param name="light">The lamp.</param>
    /// <param name="index">Which of the lamp's places the glow is from.</param>
    /// <param name="intersection">The point on the far side being lit.</param>
    /// <returns>The ray's share.</returns>
    private static double LookingShareOf(Light light, int index, Intersection intersection)
    {
        if (_looking is not { Side: { } side } run || !intersection.Inside ||
            !ReferenceEquals(intersection.SurfaceMaterial, side.Glass) ||
            !OnRun(RayThatReached(intersection), run.Origin, run.Direction))
            return 1;

        int samples = light.SampleCount;
        LightSample there = light.SampleToward(run.Origin, index, side.Normal);

        if (!Aims(side, there))
            return 1;

        double byAiming = AimedAt(index, run.Count, samples) * AimOdds(side, there, run.Direction);
        double byLooking = run.Count * run.Odds;

        return byLooking + byAiming > 0 ? byLooking / (byLooking + byAiming) : 1;
    }

    /// <summary>
    /// This method reports whether a pane's near side aims at a light's place: one beyond the pane, and
    /// pointed its way.  Asked the same way from both ends, so that the aim and the rays agree on it.
    /// </summary>
    private static bool Aims(NearSide side, LightSample there)
    {
        return there.Direction.Dot(side.Normal) < 0 && there.Cone > 0;
    }

    /// <summary>
    /// This method returns how many of a pane's aimed directions go to one of a light's places, the
    /// directions being handed to its places in turn.
    /// </summary>
    private static int AimedAt(int index, int count, int samples)
    {
        return (count - index + samples - 1) / samples;
    }

    /// <summary>
    /// This method returns how likely a pane's aim at a light's place is to pick a direction through
    /// the glass, per unit of solid angle: the far side's facets bending the light's own direction into
    /// it, the far side being taken to face the near side's way.
    /// </summary>
    private static double AimOdds(NearSide side, LightSample there, Vector direction)
    {
        return Microfacets.RefractionOdds(
            -side.Normal, there.Direction, -direction, side.Alpha, side.Outside, side.Inside);
    }

    /// <summary>
    /// This method picks one facet of rough glass the eye can see and bends the eye's ray through it.
    /// </summary>
    /// <param name="intersection">Where the ray met the glass.</param>
    /// <param name="material">The glass's material.</param>
    /// <param name="n1">The index of refraction on the eye's side.</param>
    /// <param name="n2">The index of refraction on the far side.</param>
    /// <param name="first">A number choosing how far the facet leans; only its fraction counts.</param>
    /// <param name="second">A number choosing which way it leans; only its fraction counts.</param>
    /// <returns>The direction the ray goes on in and what it brings back is worth, or no direction
    /// where the facet lets nothing through.</returns>
    private static (Vector Direction, double Weight) ThroughAFacet(
        Intersection intersection, Material material, double n1, double n2, double first, double second)
    {
        return Bend(
            intersection.Normal, intersection.Eye, material.Roughness * material.Roughness, n1, n2,
            first, second);
    }

    /// <summary>
    /// This method picks one facet of a rough surface that can be seen from a given direction and bends
    /// that direction through it.
    /// </summary>
    /// <param name="normal">The surface normal, of unit length, on the seeing side.</param>
    /// <param name="toward">The direction the facet is seen from, of unit length.</param>
    /// <param name="alpha">How far the facets lean.</param>
    /// <param name="n1">The index of refraction on the seeing side.</param>
    /// <param name="n2">The index of refraction on the far side.</param>
    /// <param name="first">A number choosing how far the facet leans; only its fraction counts.</param>
    /// <param name="second">A number choosing which way it leans; only its fraction counts.</param>
    /// <returns>The bent direction and what it brings back is worth, or no direction where the facet
    /// lets nothing through.</returns>
    private static (Vector Direction, double Weight) Bend(
        Vector normal, Vector toward, double alpha, double n1, double n2, double first, double second)
    {
        (Vector across, Vector along) = Microfacets.FrameAround(normal);
        Vector eye = new (toward.Dot(across), toward.Dot(along), toward.Dot(normal));

        if (eye.Z <= 0)
            return (null, 0);

        Vector facet = Microfacets.SampleVisibleNormal(
            eye, alpha, first - Math.Floor(first), second - Math.Floor(second));
        double cosFacet = eye.Dot(facet);
        double through = 1 - Microfacets.GlassReflectance(cosFacet, n1, n2);
        Vector bent = through > 0 ? Microfacets.Refract(eye, facet, n1 / n2) : null;

        if (bent is null || bent.Z >= 0)
            return (null, 0);

        return ((across * bent.X + along * bent.Y + normal * bent.Z).Unit,
            through * Microfacets.Masking(-bent.Z, alpha));
    }

    /// <summary>
    /// This method follows light of every band through a crossing that bends each band by its own
    /// amount: one ray per band, each going its own way and carrying only its band from there on.
    /// This is where a prism's rainbow and a diamond's fire come from.
    /// <para>
    /// A band's ray never splits again, having only the one band to send anywhere, so the cost is paid
    /// once per path rather than compounding at every crossing; and each is carried as a
    /// <see cref="SingleBand"/>, at the price of one number rather than a whole spectrum.  A band bent
    /// back entirely, where the others got through, simply contributes nothing.
    /// </para>
    /// <para>
    /// Each band is bent at a wavelength somewhere within it rather than at its middle.  Bent at the
    /// middle, a glass that spreads colors widely laid its thirty-two bands down as thirty-two separate
    /// stripes of color.  Where within the band is a plain function of where the crossing is, the same
    /// shift for every band, so neighboring rays bend a little differently, the samples within a pixel
    /// average the steps into a smooth rainbow, and two renders of a scene still agree to the last bit.
    /// </para>
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried, which must be held in
    /// <see cref="BandSpectrum"/>'s bands.</typeparam>
    /// <param name="intersection">The crossing.</param>
    /// <param name="remaining">The remaining number of recursions allowed.</param>
    /// <returns>The light arriving through the crossing, band by band.</returns>
    private TS SplitThrough<TS>(Intersection intersection, int remaining)
        where TS : struct, ISpectrum<TS>
    {
        TS light = TS.Black;
        double width = (SpectralColor.LongestWavelength - SpectralColor.ShortestWavelength) / TS.Count;
        double shift = ShiftFor(intersection.Point, 0) - 0.5;
        Material material = intersection.Material;
        double turnOut = ShiftFor(intersection.Point, 103);
        double turnRound = ShiftFor(intersection.Point, 104);
        (int Band, double Wavelength) was = SingleBand.Carry(0, double.NaN);
        LookingRun? wasLooking = _looking;
        bool wasSpreading = _spreading;
        double wasRoughness = _spreadRoughness;

        // Bands bent through rough glass have been spread by it, each through a facet of its own, and
        // a mirror they meet is shaded as rough as the glass, as after any rough surface.
        if (material.IsRough)
        {
            _spreading = true;
            _spreadRoughness = Math.Max(wasRoughness, material.Roughness);
        }

        try
        {
            for (int band = 0; band < TS.Count; band++)
            {
                double wavelength = SpectralColor.WavelengthOf(band) + shift * width;
                (double n1, double n2) = intersection.IndicesAt(wavelength);

                // Rough glass that spreads colors bends each band through a facet of its own, so
                // the bands between them spread over the blur as well as over the rainbow, and
                // the cost stays one ray a band.
                (Vector direction, double weight) = material.IsRough
                    ? ThroughAFacet(
                        intersection, material, n1, n2,
                        (band + 0.5) / TS.Count + turnOut, band * 0.6180339887498949 + turnRound)
                    : (Bent(intersection, n1, n2), 1);

                if (direction is null)
                    continue;

                // A band bent through a facet sets off on a straight run of its own, sharing a glowing
                // volume it meets with the volume's glow through the glass as any rough glass's rays do.
                Ray ray = RefractedRay(intersection, direction);

                if (material.IsRough)
                {
                    _looking = new LookingRun(
                        ray.Origin, direction, Microfacets.RefractionOdds(
                            intersection.Normal, intersection.Eye, direction,
                            material.Roughness * material.Roughness, n1, n2),
                        1);
                }

                SingleBand.Carry(band, wavelength);
                light[band] = GetColorFor<SingleBand>(ray, remaining - 1).Amount * weight;
            }
        }
        finally
        {
            SingleBand.Carry(was.Band, was.Wavelength);
            _looking = wasLooking;
            _spreading = wasSpreading;
            _spreadRoughness = wasRoughness;
        }

        return light;
    }

    /// <summary>
    /// This method is used to properly clean up our resources.
    /// </summary>
    public void Dispose()
    {
        CleanUpDisposables(Surfaces);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// This method is used to chase the given list of surfaces, disposing of the ones that
    /// need it.  It will recurse as necessary.
    /// </summary>
    /// <param name="surfaces">The list of surfaces to chase.</param>
    private static void CleanUpDisposables(List<Surface> surfaces)
    {
        foreach (Surface surface in surfaces)
        {
            if (surface is IDisposable disposable)
                disposable.Dispose();

            switch (surface)
            {
                case Group group:
                {
                    CleanUpDisposables(group.Surfaces);
                    break;
                }
                case CsgSurface csgSurface:
                    CleanUpDisposables([csgSurface.Left, csgSurface.Right]);
                    break;
            }
        }
    }
}
