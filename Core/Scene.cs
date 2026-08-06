using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.Pigments;

namespace RayTracer.Core;

/// <summary>
/// This class represents a world of things to render.
/// </summary>
public class Scene : NamedThing, IDisposable
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
    /// This method determines the color for the given ray.
    /// </summary>
    /// <param name="ray">The ray to determine the color for.</param>
    /// <param name="remaining">The remaining number of reflective recursions allowed.</param>
    /// <returns>The color for the ray.</returns>
    public Color GetColorFor(Ray ray, int remaining = 4)
    {
        List<Intersection> hits = Intersect(ray);
        Intersection hit = hits.Hit();

        // Now that the sky costs a pattern lookup to ask about, only rays that saw nothing ask it.
        // A ray that saw nothing also crossed the surroundings forever, which is a span an endless
        // medium is entitled to have the whole of its say over.
        if (hit == null)
        {
            return ThroughTheSurroundings(
                Background.GetTransformedColorFor(HeadingOf(ray)), ray, double.PositiveInfinity);
        }

        hit.PrepareUsing(ray, hits, Environment.IndexOfRefraction);

        Color color = GetHitColor(hit, remaining);

        // What the ray crossed to get here was the surroundings, unless it was on its way out of
        // something, in which case it crossed that thing's insides and has been charged for it
        // already.  This is the same division the fade through a substance has always drawn.
        return hit.Inside ? color : ThroughTheSurroundings(color, ray, hit.Distance);
    }

    /// <summary>
    /// This method hands the given color to whatever fills the space between this scene's objects,
    /// to be dimmed and added to over the span the ray crossed to bring it.
    /// </summary>
    /// <param name="color">The color the ray carried.</param>
    /// <param name="ray">The ray that carried it.</param>
    /// <param name="distance">How far the ray crossed the surroundings.</param>
    /// <returns>The color once the surroundings have had their say.</returns>
    private Color ThroughTheSurroundings(Color color, Ray ray, double distance)
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
    /// <returns>The color once the medium has had its say.</returns>
    private Color ThroughMedium(
        Medium medium, Color color, Ray ray, double distance, Surface container = null)
    {
        // A medium with a shape has no answer to be written down and must be walked along instead.
        // Only a medium filling a surface may have one, which is why the container is wanted here: it
        // says both what space the shape is described in and where the shape ends.
        if (medium.HasShape && container is not null)
            return MarchedThrough(medium, color, ray, distance, container);

        Color through = medium.ApplyOver(color, distance);

        // Everything above was written down rather than worked out.  What follows is the one term
        // that has to be gone and looked for, and only a medium that turns light aside has it -- so a
        // scene whose fog merely swallows light costs exactly what it did before any of this existed.
        if (!medium.Scatters || Lights.Count == 0)
            return through;

        Color gathered = GatheredFromTheLights(medium, ray, distance, container);

        // The gathered light adds to what came through without covering the pixel any further: how
        // much of the pixel the medium stands in front of was settled by what it took out, and
        // turning light aside is one of the ways it took it.
        return new Color(
            through.Red + gathered.Red,
            through.Green + gathered.Green,
            through.Blue + gathered.Blue,
            through.Alpha);
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
    private Color GatheredFromTheLights(
        Medium medium, Ray ray, double distance, Surface container)
    {
        int count = medium.Samples;
        double extinction = medium.MeanExtinction;

        // A medium that turns light aside stops it by doing so, so this cannot be nothing here; the
        // guard is for the arithmetic's sake rather than for any scene's.
        if (extinction <= 0 || distance <= 0)
            return Colors.Black;

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
        Color gathered = Colors.Black;
        List<Intersection> crossings = container is null ? null : [];

        for (int index = 0; index < count; index++)
        {
            double fraction = (index + JitterFor(index, shift)) / count;
            double along = -Math.Log(1 - fraction * reach) / extinction;
            double chance = extinction * Math.Exp(-extinction * along) / reach;
            Point where = ray.Origin + heading * along;
            Color arriving = GatheredAt(
                medium, container, where, heading, ray.TimeIndex, index, crossings);

            if (arriving.Matches(Colors.Black))
                continue;

            // What this place turns aside, of what reached it, dimmed by the trip back to the eye and
            // divided by how likely the place was to have been asked at all.
            Color scattered = arriving * medium.Scattering * (medium.Density / (chance * count));

            gathered += scattered * medium.GetTransmittanceOver(along);
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
    /// <returns>The color once the medium has had its say.</returns>
    private Color MarchedThrough(
        Medium medium, Color behind, Ray ray, double distance, Surface container)
    {
        int count = medium.Samples;
        double step = distance / count;
        Vector heading = ray.Direction.Unit;
        double shift = ShiftFor(ray);
        bool gathers = medium.Scatters && Lights.Count > 0;
        double surviving = 1;
        Color transmittance = Colors.White;
        Color added = Colors.Black;
        List<Intersection> crossings = gathers ? [] : null;

        for (int index = 0; index < count; index++)
        {
            double along = (index + JitterFor(index, shift)) * step;
            Point where = ray.Origin + heading * along;
            double density = medium.DensityAt(container.WorldToSurface(where, ray.TimeIndex));

            if (density <= 0)
                continue;

            Color source = medium.Emission * density;

            if (gathers)
            {
                source += medium.Scattering * density * GatheredAt(
                    medium, container, where, heading, ray.TimeIndex, index, crossings);
            }

            // What this step's worth gives up to the eye, dimmed by everything nearer than it, and then
            // the step's own share of the dimming added on for whatever lies further.
            added += source * transmittance * step;
            transmittance *= Transmitted(medium.ExtinctionAt(density), step);
            surviving = (transmittance.Red + transmittance.Green + transmittance.Blue) / 3;

            // Nothing beyond this can show, so there is no sense in walking further.
            if (surviving < 0.001)
                break;
        }

        return new Color(
            behind.Red * transmittance.Red + added.Red,
            behind.Green * transmittance.Green + added.Green,
            behind.Blue * transmittance.Blue + added.Blue,
            behind.Alpha + (1 - behind.Alpha) * (1 - surviving));
    }

    /// <summary>
    /// This method returns what fraction of each color survives the given rate of stopping over the
    /// given length.
    /// </summary>
    /// <param name="extinction">The rate at which each color stops coming this way.</param>
    /// <param name="distance">The length crossed at that rate.</param>
    /// <returns>The fraction of each color that gets through.</returns>
    private static Color Transmitted(Color extinction, double distance)
    {
        return new Color(
            Math.Exp(-extinction.Red * distance),
            Math.Exp(-extinction.Green * distance),
            Math.Exp(-extinction.Blue * distance));
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
    /// <param name="where">The place being asked about.</param>
    /// <param name="heading">The way the ray was travelling.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to look at.</param>
    /// <param name="crossings">A list to find the shape's far side with, reused between places so that
    /// a walk does not allocate one per step.</param>
    /// <returns>The light the place sends along the ray.</returns>
    private Color GatheredAt(
        Medium medium, Surface container, Point where, Vector heading, int timeIndex, int index,
        List<Intersection> crossings)
    {
        Color arriving = GatheredDirectlyAt(
            medium, container, where, heading, timeIndex, index, crossings);

        // Everything above is light that reached this place straight from a lamp.  What follows is
        // light that got here by being turned somewhere else first, which in anything thick is most of
        // it -- and all of which was simply missing until now.
        if (medium.Bounces > 0)
        {
            arriving += CarriedOnFrom(
                medium, container, where, heading, medium.Bounces, ShiftFor(where, index), timeIndex,
                crossings);
        }

        return arriving;
    }

    /// <summary>
    /// This method works out how much light one place is sent straight from the scene's lamps, and how
    /// much of that the medium would turn toward the way the ray is going.
    /// </summary>
    /// <param name="medium">The medium being crossed.</param>
    /// <param name="container">The surface it fills, or <c>null</c> for the surroundings.</param>
    /// <param name="where">The place being asked about.</param>
    /// <param name="heading">The way the ray was travelling.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to look at.</param>
    /// <param name="index">Which sample along the crossing this is.</param>
    /// <param name="crossings">A list to find the shape's far side with.</param>
    /// <returns>The light the lamps send this way.</returns>
    private Color GatheredDirectlyAt(
        Medium medium, Surface container, Point where, Vector heading, int timeIndex, int index,
        List<Intersection> crossings)
    {
        Color arriving = Colors.Black;

        foreach (Light light in Lights)
        {
            // One place on the lamp per sample, walking around it as the samples go.  An area light's
            // softness is then spread across the crossing for nothing, rather than each sample paying
            // for the whole face of it.
            LightSample sample = light.SampleToward(where, index % light.SampleCount);
            Color reaching = GetLightReaching(where, sample.Direction, sample.Distance, timeIndex);

            if (reaching.Matches(Colors.Black))
                continue;

            // A medium filling a surface stands in its own light's way, and nothing else accounts for
            // that: the walk above looks at surfaces and at the surroundings, neither of which this is.
            // A medium filling the surroundings needs no such thing, having been charged for its trip
            // there already.
            if (container is not null)
                reaching *= SurvivingTheMedium(medium, container, where, sample, timeIndex, crossings);

            if (reaching.Matches(Colors.Black))
                continue;

            // The angle between the way the light was travelling and the way it leaves toward the eye.
            // Both are read as the directions light moves in, and negating both leaves their dot
            // product alone, so this is simply the one direction against the other.
            double phase = medium.PhaseFor(sample.Direction.Dot(heading));

            arriving += light.ColorFor(sample) * reaching * (sample.Cone * phase);
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
    /// cancels out of the weighing.  A path that wanders out of the medium is done, since in this
    /// renderer nothing but a lamp gives off light -- which for a cloud lit by a sky rather than by
    /// lamps is the honest limit of what this can show.
    /// </para>
    /// </summary>
    /// <param name="medium">The medium the light is wandering in.</param>
    /// <param name="container">The surface it fills, or <c>null</c> for the surroundings.</param>
    /// <param name="from">The place the path is being followed back from.</param>
    /// <param name="heading">The way the light being followed is travelling.</param>
    /// <param name="left">How many more turns may be followed.</param>
    /// <param name="seed">Where in the run of numbers this path's choices are drawn from.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to look at.</param>
    /// <param name="crossings">A list to find the shape's edge with.</param>
    /// <returns>The light that arrived here by way of somewhere else.</returns>
    private Color CarriedOnFrom(
        Medium medium, Surface container, Point from, Vector heading, int left, double seed,
        int timeIndex, List<Intersection> crossings)
    {
        Color carried = Colors.Black;
        Color throughput = Colors.White;
        Vector going = heading;
        Point at = from;

        for (int turn = 0; turn < left; turn++)
        {
            Vector came = medium.SampleDirectionAround(
                going, Fraction(seed, turn * 3), Fraction(seed, turn * 3 + 1));
            double howFar = TurnedAgainAfter(
                medium, container, at, came, Fraction(seed, turn * 3 + 2), crossings);

            // The path left the medium without being turned again, and there is nothing out there to
            // have sent it on its way.
            if (double.IsNaN(howFar))
                break;

            at += came * howFar;
            going = came;
            throughput *= medium.Albedo;

            if (throughput.Red + throughput.Green + throughput.Blue < 0.003)
                break;

            carried += throughput * GatheredDirectlyAt(
                medium, container, at, going, timeIndex, turn, crossings);
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
    /// <param name="from">Where the path starts.</param>
    /// <param name="going">Which way it goes.</param>
    /// <param name="fraction">A number from nothing up to one, which picks the distance.</param>
    /// <param name="crossings">A list to find the shape's edge with.</param>
    /// <returns>How far it goes before being turned, or <c>NaN</c> if it gets out.</returns>
    private static double TurnedAgainAfter(
        Medium medium, Surface container, Point from, Vector going, double fraction,
        List<Intersection> crossings)
    {
        double edge = EdgeOfTheMedium(container, new Ray(from, going, 0), crossings);
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
            double density = medium.DensityAt(container.WorldToSurface(from + going * along));

            passed += medium.MeanExtinction / medium.Density * density * step;

            if (passed >= wanted)
                return along;
        }

        return double.NaN;
    }

    /// <summary>
    /// This method returns how far off the near edge of a medium's surface lies along a ray, or
    /// infinity where the medium fills the surroundings and so has no edge.
    /// </summary>
    /// <param name="container">The surface the medium fills, or <c>null</c>.</param>
    /// <param name="ray">The ray to look along.</param>
    /// <param name="crossings">A list to find the crossings with.</param>
    /// <returns>How far off the edge is.</returns>
    private static double EdgeOfTheMedium(
        Surface container, Ray ray, List<Intersection> crossings)
    {
        if (container is null)
            return double.PositiveInfinity;

        crossings.Clear();
        container.Intersect(ray, crossings);

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
    private static double ShiftFor(Point where, int index)
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
    /// shape it is walked, and coarsely -- a quarter of the steps the eye's walk takes -- because all
    /// that is wanted of it is how much stuff stands in the way, not where any of it is.  Either way it
    /// stops at the surface's own edge, since past that the medium is not there to shade anything.
    /// </para>
    /// </summary>
    /// <param name="medium">The medium doing the shading.</param>
    /// <param name="container">The surface it fills.</param>
    /// <param name="where">The place the light is heading for.</param>
    /// <param name="sample">Which way the light lies, and how far off.</param>
    /// <param name="timeIndex">Which instant of the shutter's opening to look at.</param>
    /// <param name="crossings">A list to find the shape's far side with.</param>
    /// <returns>The fraction of each color that arrives.</returns>
    private static Color SurvivingTheMedium(
        Medium medium, Surface container, Point where, LightSample sample, int timeIndex,
        List<Intersection> crossings)
    {
        Ray toward = new (where, sample.Direction, timeIndex);

        crossings.Clear();
        container.Intersect(toward, crossings);

        double edge = double.PositiveInfinity;

        foreach (Intersection crossing in crossings)
        {
            // The nearest crossing ahead of us is where the shape ends.  Ones behind belong to the
            // stuff already passed through, and are none of this walk's business.
            if (crossing.Distance > 0 && crossing.Distance < edge)
                edge = crossing.Distance;
        }

        if (double.IsPositiveInfinity(edge))
            return Colors.White;

        edge = Math.Min(edge, sample.Distance);

        // An even density needs no walking: how much stands in the way is the rate times the length,
        // which is the very thing the closed form is for.
        if (!medium.HasShape)
            return Transmitted(medium.ExtinctionAt(medium.Density), edge);

        // A fixed number of steps rather than a share of the crossing's own.  How finely this one
        // shadow is answered is its own question, and tying it to how many places the eye's ray asks
        // about made the work grow as the square of that count -- twice the quality for four times the
        // render, which is not a knob anyone can use.  It also never settled: this is a bias rather
        // than a noise, so more places along the eye's ray never averaged it away.
        const int count = 16;

        double step = edge / count;
        Color depth = Colors.Black;

        for (int index = 0; index < count; index++)
        {
            Point at = where + sample.Direction * ((index + 0.5) * step);
            double density = medium.DensityAt(container.WorldToSurface(at, timeIndex));

            if (density > 0)
                depth += medium.ExtinctionAt(density) * step;
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
    /// This method finds the point of intersection, if any, of the given ray with the
    /// things in the world.
    /// ray intersects the geometry and, if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    public List<Intersection> Intersect(Ray ray)
    {
        List<Intersection> intersections = [];

        foreach (Surface surface in Surfaces)
            surface.Intersect(ray, intersections);

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
        Color surfaceColor = Lights.Aggregate(Colors.Black, (color, light) =>
            color + Illuminate(light, intersection));
        Color reflectedColor = GetReflectionColor(intersection, remaining);
        Color refractedColor = GetRefractedColor(intersection, remaining);
        Material material = intersection.Surface.Material ?? Material.Default;
        Color refColor;

        // What a surface lets past, it cannot also show.  The pigment may say so color by color,
        // which is what lets one pattern be a window in some places and a wall in others, so the
        // question is asked at this point rather than of the material as a whole.
        double transparency = material.PigmentMayTransmit
            ? material.TransparencyFor(
                material.Pigment.GetColorFor(intersection.Surface, intersection.Point))
            : material.Transparency;

        if (material.Reflective > 0 && (material.Transparency > 0 || material.PigmentMayTransmit))
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
        Color color = surfaceColor * (1 - transparency) + refColor;

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
                    intersection.Distance, intersection.Surface);
            }
        }

        return color;
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
    /// <returns>The color the light lends the point.</returns>
    private Color Illuminate(Light light, Intersection intersection)
    {
        int count = light.SampleCount;

        if (count == 1)
        {
            LightSample only = light.SampleToward(intersection.OverPoint, 0, intersection.Normal);

            return light.ApplyPhong(
                intersection.OverPoint, intersection.Eye, intersection.Normal, intersection.Surface,
                only, GetLightReaching(
                    intersection.OverPoint, only.Direction, only.Distance, intersection.TimeIndex));
        }

        Color sum = Colors.Black;

        for (int index = 0; index < count; index++)
        {
            LightSample sample = light.SampleToward(
                intersection.OverPoint, index, intersection.Normal);

            sum += light.ApplyPhong(
                intersection.OverPoint, intersection.Eye, intersection.Normal, intersection.Surface,
                sample, GetLightReaching(
                    intersection.OverPoint, sample.Direction, sample.Distance, intersection.TimeIndex));
        }

        return sum * (1.0 / count);
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
    /// forward from the light rather than backward from the surface, which is a different
    /// algorithm entirely -- POV-Ray needs its photon mapping for the same reason.
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
        Ray ray = new (point, direction, timeIndex);
        Color reaching = Colors.White;

        foreach (Intersection intersection in Intersect(ray))
        {
            // Only things that actually stand between the point and the light can shade it;
            // anything behind the point, or beyond the light, is irrelevant.  Zero counts as being
            // in the way, which is what Hit() has always taken it to mean -- rays start at a point
            // nudged off the surface precisely so that a grazing hit at no distance at all is a
            // real occluder rather than the surface shading itself.
            if (intersection.Distance < 0 || intersection.Distance >= distance ||
                intersection.Surface.NoShadow)
                continue;

            Material material = intersection.Surface.Material ?? Material.Default;
            Interior interior = material.Interior;

            // Shadow rays never have their intersections prepared, since that work would be wasted
            // on all but the nearest hit, so what is needed of the crossing is worked out here --
            // and only when something actually asks for it.  The pigment is sampled where the light
            // crossed, so that patterned glass shadows as a pattern, and once sampled it serves for
            // both how much light gets past and what color it comes out.
            bool needsPigment = interior.Filter > 0 || material.PigmentMayTransmit;
            Point where = needsPigment || interior.Refracts
                ? ray.At(intersection.Distance)
                : null;
            Color surfaceColor = needsPigment
                ? material.Pigment.GetColorFor(intersection.Surface, where)
                : null;
            double transparency = surfaceColor is null
                ? material.Transparency
                : material.TransparencyFor(surfaceColor);

            if (transparency <= 0)
                return Colors.Black;

            reaching *= transparency;

            if (interior.Filter > 0 || interior.Refracts)
            {
                if (interior.Filter > 0)
                    reaching *= interior.GetFilterTint(surfaceColor);

                // Some of the light never gets in at all, being mirrored off the surface instead,
                // and how much depends on how glancing its approach is.  This is what keeps clear
                // glass from being invisible to its own shadow: struck head-on it lets nearly
                // everything through, but around the rim, where the light only grazes it, most is
                // turned away.  A glass ball therefore casts a faint shadow gathered into a dark
                // ring, which is close to what one really does -- what is still missing is the
                // bright spot in the middle, where refraction gathers the light it bent aside.
                // That is a caustic, and finding it means tracing forward from the light.
                if (interior.Refracts)
                {
                    Vector normal = intersection.Surface.NormaAt(where, intersection);

                    reaching *= 1 - interior.GetReflectanceAt(
                        ray.Direction.Dot(normal), Environment.IndexOfRefraction);
                }
            }
        }

        // Fog dims a lamp as surely as it dims a hillside, so the light is charged for its own trip
        // through the surroundings.  Only what the medium takes out counts here; what it gives off
        // is its own light and not this lamp's.  Note what this means for a distant light, whose
        // trip is endless: an endless absorbing medium extinguishes it utterly, which is the honest
        // answer for a fog described as having no far side.  A fog with one is a bounded surface.
        return Environment.Medium is null
            ? reaching
            : reaching * Environment.Medium.GetTransmittanceOver(distance);
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

        return GetLightReaching(point, sample.Direction, sample.Distance).Matches(Colors.Black);
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
        Material material = intersection.Surface.Material ?? Material.Default;
        double reflective = material.Reflective;

        if (remaining < 1 || reflective == 0)
            return Colors.Black;

        Ray reflectedRay = new (
            intersection.OverPoint, intersection.Reflect, intersection.TimeIndex);
        Color color = GetColorFor(reflectedRay, remaining - 1) * reflective;

        // A metal colors what it mirrors, not just its highlight -- it is what makes a gold
        // surface throw back a gold scene rather than a plain one.  The angle here is the eye
        // against the normal, the true angle of incidence, rather than the approximation the
        // highlight has to settle for.
        if (material.Metallic != 0)
        {
            Color pigmentColor = material.Pigment.GetColorFor(intersection.Surface, intersection.Point);

            color *= material.GetMetallicTint(pigmentColor, intersection.Eye.Dot(intersection.Normal));
        }

        return color;
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
        Material material = intersection.Surface.Material ?? Material.Default;

        // The pigment may say, color by color, how much light gets past it, so where it might,
        // it is sampled and has its say.  Sampled once here and reused for the filter below, since
        // both want the surface's color at the very same point.
        Color pigmentColor = material.PigmentMayTransmit || material.Interior.Filter > 0
            ? material.Pigment.GetColorFor(intersection.Surface, intersection.Point)
            : null;
        double transparency = pigmentColor is null
            ? material.Transparency
            : material.TransparencyFor(pigmentColor);

        if (remaining < 1 || transparency == 0)
            return Colors.Black;

        double ratio = intersection.N1 / intersection.N2;
        double cosI = intersection.Eye.Dot(intersection.Normal);
        double sin2T = ratio * ratio * (1 - cosI * cosI);

        if (sin2T > 1)
            return Colors.Black;

        double cosT = Math.Sqrt(1 - sin2T);
        Vector direction = intersection.Normal * (ratio * cosI - cosT) -
                           intersection.Eye * ratio;
        Point point = intersection.Inside ? intersection.OverPoint : intersection.UnderPoint;

        Ray refractedRay = new (point, direction, intersection.TimeIndex);
        Color color = GetColorFor(refractedRay, remaining - 1) * transparency;

        // Transparency says how much light gets through; the filter says what color it comes out.
        // Tinting toward the pigment's color at this very point, rather than toward some single
        // color for the whole surface, is what lets patterned glass filter as a pattern.  Like
        // the transparency it follows, this is charged once per surface crossed, so a solid tints
        // what passes through it twice -- going in, and coming back out.
        if (material.Interior.Filter > 0)
            color *= material.Interior.GetFilterTint(pigmentColor);

        return color;
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
