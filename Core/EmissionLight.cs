using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class is the light of every surface in a scene that glows of its own -- a lampshade, a lit
/// panel, a television's screen -- looked at as a light, in a scene that follows bounced light.
/// <para>
/// **Such a scene already finds their light**: a matte surface looking about itself for the light bounced
/// onto it meets the lampshade now and then, and takes its glow as it takes the light off a wall.  But a
/// small bright thing is met by one look in hundreds, and every time it is, it lights the place looking as
/// brightly as the rest of the looks together: a room lit by a lampshade came out blotched under its desk
/// however many looks were taken.  So the glowing surfaces are looked at directly, as a lamp is, and a look
/// for bounced light that runs straight into one finds it black -- its light having been counted here --
/// exactly as the sky is, and a glowing volume.  What a place takes from them on average is just what it
/// took before; it is only found the sure way.
/// </para>
/// <para>
/// **One light for all of them, whatever their number.**  A droid with a few dozen lamps on its dome is a
/// few dozen glowing surfaces, and a light apiece would cost a few dozen shadow rays at every place.  So
/// each sample picks one of them -- in proportion to how bright it is and how much of the view, seen from
/// the place being lit, the ball round it fills -- and the cost is the light's samples, however many glow.
/// </para>
/// <para>
/// **A sample is a direction, not a place.**  It is picked evenly over the cone a ball round the surface
/// fills -- the whole sphere, from inside the ball -- and the surface itself is then looked for along it,
/// alone: where the look meets it, its glow toward the place is worked out exactly as a ray meeting it
/// there would find it, and a shadow ray goes out to just short of it.  That holds for any surface,
/// combined, turned or shared however it is, and a direction's odds have no distance in them, so a place
/// right beside a glowing panel is lit as steadily as one across the room.
/// </para>
/// <para>
/// **A long thin surface has a row of balls round it**, one round each of the boxes it lies within (its
/// <see cref="Surface.Casings"/>, out in the world), each box cut along its length into pieces no longer
/// than twice their width.  One ball round a neon tube's stroke is half empty space at best, and a wall a
/// hand's breadth behind the sign stands inside it, looking over the whole sphere for a thread: the light
/// came out as grain however many samples it took.  The balls overlap, and a direction is as likely as the
/// balls round its surface that hold it make it.
/// </para>
/// <para>
/// **What does not count**: anything that casts no shadow, and the bulb round a lamp, which are light
/// counted already -- the painted disc of a sun, the glowing glass of a bulb with a lamp inside -- and which
/// bounced light passes as a shadow ray does.  Strands of hair never glow.
/// </para>
/// </summary>
public class EmissionLight : Light
{
    /// <summary>
    /// This field holds how many places the glowing surfaces are looked at from when a scene says nothing.
    /// </summary>
    public const int DefaultSamples = 16;

    /// <summary>
    /// This property holds how many places the glowing surfaces are looked at from.
    /// </summary>
    public int Samples { get; set; } = DefaultSamples;

    /// <summary>
    /// This property notes how many places this light is looked at from.
    /// </summary>
    public override int SampleCount => Samples;

    /// <summary>
    /// This property notes that a ray can meet this light: a glowing surface shows in a mirror like anything
    /// else does.
    /// </summary>
    public override bool CanBeSeen => true;

    /// <summary>
    /// A smooth surface's highlight takes what each sample brings, as a glowing volume's does: the glowing
    /// surfaces are as many colors and brightnesses as there are of them.
    /// </summary>
    protected internal override bool HighlightsBySample => true;

    /// <summary>
    /// This field holds how many pieces one of a surface's boxes may be cut into along its length.
    /// </summary>
    private const int MostPieces = 256;

    /// <summary>
    /// This record holds one glowing surface, as it stands in one place: the surface, the instances it
    /// stands behind, what a look for it must cross to find it -- itself, or the combination it is part
    /// of, which may hide some of it -- how bright it is, and which of the light's balls are round it.
    /// </summary>
    private sealed record Glowing(
        Surface Surface, Portals Portals, Surface Whole, double Brightness, int FirstBall, int BallCount);

    /// <summary>
    /// This record holds one ball round a part of a glowing surface, its middle and radius at each instant.
    /// </summary>
    private sealed record Ball(Point[] Middles, double[] Radii);

    private readonly Scene _scene;
    private readonly Glowing[] _glowing;
    private readonly int[] _roundWhich;
    private readonly double[] _brightness;
    private readonly Point[][] _middles;
    private readonly double[][] _radii;
    private readonly Dictionary<(Surface, Portals), int> _which;
    private int _instant;

    /// <summary>
    /// This field holds the share of the light each glowing surface was last given from one place, on each
    /// thread: a place asks for every one of its samples in turn, and the shares are the same for them all.
    /// </summary>
    [ThreadStatic]
    private static Weighed _weighed;

    private sealed record Weighed(EmissionLight Light, Point From, double[] Running);

    private EmissionLight(Scene scene, Glowing[] glowing, List<Ball> balls, int instants)
    {
        _scene = scene;
        _glowing = glowing;
        _which = new Dictionary<(Surface, Portals), int>(glowing.Length);
        _roundWhich = new int[balls.Count];
        _brightness = new double[balls.Count];
        _middles = new Point[instants][];
        _radii = new double[instants][];

        for (int index = 0; index < glowing.Length; index++)
        {
            _which[(glowing[index].Surface, glowing[index].Portals)] = index;

            for (int ball = glowing[index].FirstBall; ball < glowing[index].FirstBall + glowing[index].BallCount; ball++)
            {
                _roundWhich[ball] = index;
                _brightness[ball] = glowing[index].Brightness;
            }
        }

        for (int instant = 0; instant < instants; instant++)
        {
            _middles[instant] = balls.Select(ball => ball.Middles[instant]).ToArray();
            _radii[instant] = balls.Select(ball => ball.Radii[instant]).ToArray();
        }
    }

    /// <summary>
    /// This method gathers every surface in a scene that glows of its own into one light, or returns
    /// <c>null</c> where none does.  The scene must be ready to be searched, its lights standing on things
    /// already set out, since the bulbs round them are left out.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="instants">How many instants of the shutter's opening the camera looks at.</param>
    /// <param name="samples">How many places the glowing surfaces are to be looked at from.</param>
    /// <returns>The light, or <c>null</c>.</returns>
    public static EmissionLight Of(Scene scene, int instants, int samples = DefaultSamples)
    {
        List<Glowing> glowing = [];
        List<Ball> balls = [];

        instants = Math.Max(1, instants);
        Find(scene, scene.Surfaces, [], null, glowing, balls, [], instants);

        if (glowing.Count == 0)
            return null;

        EmissionLight light = new (scene, glowing.ToArray(), balls, instants) { Samples = samples };

        if (instants > 1)
        {
            Light[] atTimes = new Light[instants];

            atTimes[0] = light;

            for (int instant = 1; instant < instants; instant++)
            {
                EmissionLight then = (EmissionLight) light.MemberwiseClone();

                then._instant = instant;
                atTimes[instant] = then;
            }

            light.MovesThrough(atTimes);
        }

        return light;
    }

    /// <summary>
    /// This method finds every glowing surface, as it stands in each place, walking down through groups,
    /// combinations and shared shapes -- into a shared shape only where it may hold something glowing,
    /// which is worked out once for each shape and kept.
    /// </summary>
    private static void Find(
        Scene scene, IEnumerable<Surface> surfaces, List<Surface> through, Surface whole, List<Glowing> glowing,
        List<Ball> balls, Dictionary<(Surface, bool), bool> holding, int instants)
    {
        bool dressedToGlow = DressedToGlow(through);

        foreach (Surface surface in surfaces)
        {
            if (!MayGlow(surface, dressedToGlow, holding))
                continue;

            switch (surface)
            {
                case Group group:
                    Find(scene, group.Surfaces, through, whole, glowing, balls, holding, instants);
                    break;
                case CsgSurface csg:
                    Find(scene, [csg.Left, csg.Right], through, whole ?? csg, glowing, balls, holding, instants);
                    break;
                case IAssembled { Root: not null } assembled:
                    Find(scene, [assembled.Root], through, whole ?? surface, glowing, balls, holding, instants);
                    break;
                case Instance instance:
                    // What a shared shape holds is looked for in its own right: a combination outside it
                    // cannot be carried in through the instance.
                    through.Add(instance);
                    Find(scene, [instance.Prototype], through, null, glowing, balls, holding, instants);
                    through.RemoveAt(through.Count - 1);
                    break;
                default:
                    Portals portals = PortalsOf(through);

                    if (Glows(scene, surface, portals) && BallsRound(surface, portals, instants) is { Count: > 0 } round)
                    {
                        Color glow = surface.MaterialThrough(portals).Emission;

                        glowing.Add(new Glowing(
                            surface, portals, whole ?? surface, (glow.Red + glow.Green + glow.Blue) / 3, balls.Count,
                            round.Count));
                        balls.AddRange(round);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// This method reports whether the instances passed through dress what is left the default in a material
    /// that glows.
    /// </summary>
    private static bool DressedToGlow(List<Surface> through)
    {
        for (int index = through.Count - 1; index >= 0; index--)
        {
            if (through[index].Material is { } material && !through[index].WearsTheDefault)
                return Glows(material);
        }

        return false;
    }

    /// <summary>
    /// This method reports whether anything in a surface may glow, dressed as it is, remembering the answer
    /// for every shape it asks about: anything whose own material glows, or anything left the default where
    /// it is dressed in one that does.
    /// </summary>
    private static bool MayGlow(Surface surface, bool dressedToGlow, Dictionary<(Surface, bool), bool> holding)
    {
        if (holding.TryGetValue((surface, dressedToGlow), out bool may))
            return may;

        may = surface switch
        {
            Group group => group.Surfaces.Any(child => MayGlow(child, dressedToGlow, holding)),
            CsgSurface csg => MayGlow(csg.Left, dressedToGlow, holding) || MayGlow(csg.Right, dressedToGlow, holding),
            IAssembled { Root: not null } assembled => MayGlow(assembled.Root, dressedToGlow, holding),
            Instance instance => MayGlow(
                instance.Prototype,
                instance.Material is { } dressing && !instance.WearsTheDefault ? Glows(dressing) : dressedToGlow,
                holding),
            StrandSurface => false,
            _ => surface.WearsTheDefault ? dressedToGlow : Glows(surface.Material)
        };
        holding[(surface, dressedToGlow)] = may;

        return may;
    }

    /// <summary>
    /// This method reports whether a surface, standing behind the given instances, glows as a light: its
    /// material gives off light of its own, and it is not something bounced light passes -- a stand-in that
    /// casts no shadow, or the bulb round a lamp.
    /// </summary>
    private static bool Glows(Scene scene, Surface surface, Portals portals)
    {
        if (surface is StrandSurface || surface.NoShadow || portals.CastsNoShadow ||
            !Glows(surface.MaterialThrough(portals)))
            return false;

        foreach (Light light in scene.Lights)
        {
            if (light.Housing is { } housing && housing.Contains((surface, portals)))
                return false;
        }

        return true;
    }

    /// <summary>
    /// This method reports whether a material gives off light of its own.
    /// </summary>
    private static bool Glows(Material material) =>
        material?.Emission is { } glow && glow.Red + glow.Green + glow.Blue > 0;

    /// <summary>
    /// This method returns the instances passed through, as a hit found through them would carry them: the
    /// innermost first.
    /// </summary>
    private static Portals PortalsOf(List<Surface> through)
    {
        Portals portals = default;

        for (int index = through.Count - 1; index >= 0; index--)
            portals = portals.And(through[index]);

        return portals;
    }

    /// <summary>
    /// This method works out the balls round a surface at each instant -- round the pieces of each box it lies
    /// within, carried out into the world -- or none, for a surface without end.  A box is cut along its
    /// longest edge into pieces no longer than twice the next longest, as it stands at the first instant; a
    /// round box that stands square and even in the world is its ball.
    /// </summary>
    private static List<Ball> BallsRound(Surface surface, Portals portals, int instants)
    {
        List<Ball> balls = [];

        foreach (Casing casing in surface.Casings())
        {
            Point[] corners = new Point[instants];
            Vector[][] edges = new Vector[instants][];

            for (int instant = 0; instant < instants; instant++)
            {
                Point corner = surface.SurfaceToWorld(casing.Corner, instant, portals);

                corners[instant] = corner;
                edges[instant] =
                [
                    surface.SurfaceToWorld(casing.Corner + casing.First, instant, portals) - corner,
                    surface.SurfaceToWorld(casing.Corner + casing.Second, instant, portals) - corner,
                    surface.SurfaceToWorld(casing.Corner + casing.Third, instant, portals) - corner
                ];

                if (!double.IsFinite(corner.X + corner.Y + corner.Z) ||
                    edges[instant].Any(edge => !double.IsFinite(edge.X + edge.Y + edge.Z)))
                    return [];
            }

            double[] lengths = edges[0].Select(edge => edge.Magnitude).ToArray();
            int longest = lengths[0] >= lengths[1] && lengths[0] >= lengths[2] ? 0 : lengths[1] >= lengths[2] ? 1 : 2;
            double next = Math.Max(lengths[(longest + 1) % 3], lengths[(longest + 2) % 3]);
            bool exact = casing.Round && IsEven(edges[0]);
            int pieces = exact
                ? 1
                : Math.Clamp(
                    (int) Math.Ceiling(lengths[longest] / (2 * Math.Max(next, lengths[longest] / MostPieces))), 1,
                    MostPieces);

            for (int piece = 0; piece < pieces; piece++)
            {
                Point[] middles = new Point[instants];
                double[] radii = new double[instants];

                for (int instant = 0; instant < instants; instant++)
                {
                    Vector step = edges[instant][longest] * (1.0 / pieces);
                    Vector one = edges[instant][(longest + 1) % 3];
                    Vector other = edges[instant][(longest + 2) % 3];
                    Point corner = corners[instant] + step * piece;

                    middles[instant] = corner + (step + one + other) * 0.5;

                    // Rounding must not leave a hair of the surface outside its ball.
                    radii[instant] = (1 + 1e-9) * (exact && IsEven(edges[instant])
                        ? step.Magnitude / 2
                        : Math.Max(
                            Math.Max((step + one + other).Magnitude, (step + one - other).Magnitude),
                            Math.Max((step - one + other).Magnitude, (one + other - step).Magnitude)) / 2);
                }

                balls.Add(new Ball(middles, radii));
            }
        }

        return balls;
    }

    /// <summary>
    /// This method reports whether a box's three edges are square to each other and alike in length: whether a
    /// ball touching its faces is a ball.
    /// </summary>
    private static bool IsEven(Vector[] edges)
    {
        double length = edges[0].Magnitude;
        double allowed = 1e-9 * length * length;

        return Math.Abs(edges[1].Magnitude - length) <= 1e-9 * length &&
               Math.Abs(edges[2].Magnitude - length) <= 1e-9 * length &&
               Math.Abs(edges[0].Dot(edges[1])) <= allowed && Math.Abs(edges[0].Dot(edges[2])) <= allowed &&
               Math.Abs(edges[1].Dot(edges[2])) <= allowed;
    }

    /// <summary>
    /// This property holds how many glowing surfaces the light is made of, counting each place a shared one
    /// stands in.
    /// </summary>
    public int Count => _glowing.Length;

    /// <summary>
    /// This method reports whether the given hit is on one of the glowing surfaces this light looks at for
    /// itself, where it stands.
    /// </summary>
    /// <param name="hit">The hit.</param>
    /// <returns><c>true</c>, if it is.</returns>
    public bool Holds(Intersection hit) => _which.ContainsKey((hit.Surface, hit.Portals));

    /// <summary>
    /// This method works out which way the brightest of the glowing surfaces lies from a point, for the one
    /// caller that wants a light as a single place rather than as a spread.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <returns>Which way the middle of that surface lies, and how far off.</returns>
    public override (Vector Direction, double Distance) TowardFrom(Point point)
    {
        Glowing brightest = _glowing.MaxBy(glowing => glowing.Brightness)!;
        Vector toward = new (0, 0, 0);

        for (int ball = brightest.FirstBall; ball < brightest.FirstBall + brightest.BallCount; ball++)
            toward += _middles[_instant][ball] - point;

        toward *= 1.0 / brightest.BallCount;

        return (toward.Unit, toward.Magnitude);
    }

    /// <summary>
    /// This method works out one of the places this light is looked at from: a direction toward one of the
    /// glowing surfaces, and where along it the surface is met.
    /// </summary>
    /// <param name="point">The point being lit.</param>
    /// <param name="index">Which sample this is.</param>
    /// <param name="normal">The surface normal, which the light has no use for: its samples, and their odds,
    /// are the same whichever way the place faces, which is what lets a rough surface's reflection be weighed
    /// against them from wherever it sets off.</param>
    /// <returns>The sample, carrying the glow found along it; a sample that meets nothing carries nothing.</returns>
    public override LightSample SampleToward(Point point, int index, Vector normal = null)
    {
        double[] running = WeighedFrom(point);
        double total = running[^1];

        if (total <= 0)
            return Missed(Directions.Up);

        // The samples are spread evenly over the shares, and the whole spread is turned by a fixed amount at
        // each place: neighboring places look in different directions, and the same place in the same ones
        // every time.  Where a sample falls within its surface's share picks how far out from the middle of
        // the cone it looks, so the samples given one surface are spread evenly over its cone too.
        double first = (index + 0.5) / Samples + Scene.ShiftFor(point, 142);
        double second = index * 0.7548776662466927 + Scene.ShiftFor(point, 143);

        first -= Math.Floor(first);
        second -= Math.Floor(second);

        double wanted = first * total;
        int ball = ShareHolding(running, wanted);
        double before = ball > 0 ? running[ball - 1] : 0;
        double share = running[ball] - before;
        double within = Math.Clamp((wanted - before) / share, 0, 1);
        Glowing glowing = _glowing[_roundWhich[ball]];
        (Vector axis, double outward) = ConeOf(ball, point);

        // Evenly over the cone: the cosine evenly between one and the cone's edge.
        double cosine = 1 - within * outward;
        double sine = Math.Sqrt(Math.Max(0, 1 - cosine * cosine));
        double around = 2 * Math.PI * second;
        (Vector across, Vector along) = Microfacets.FrameAround(axis);
        Vector direction = (axis * cosine + across * (sine * Math.Cos(around)) + along * (sine * Math.Sin(around))).Unit;
        Intersection seen = _scene.FindTheGlow(
            glowing.Whole, glowing.Surface, glowing.Portals, new Ray(point, direction, _instant));

        if (seen is null)
            return Missed(direction);

        double odds = OddsAlong(glowing, point, direction, ball, total);

        // What one sample carries is the light it found over the odds of having picked its way, over π -- the
        // irradiance its share of the glowing surfaces gives a surface square to it, divided by π, as every
        // light's samples carry (see VolumeLight).  The cosine at the place lit is the shading's to apply.
        Color glow = _scene.GlowSeenAt<RgbSpectrum>(seen).ToColor() * (1 / (Math.PI * odds));

        return new LightSample(
            direction, Math.Max(seen.Distance - seen.SelfOffset, 0), 1, glow, odds, seen);
    }

    /// <summary>
    /// This method returns a sample that found no glow along its way, which costs no shadow ray.
    /// </summary>
    private static LightSample Missed(Vector direction) => new (direction, 0, 0, Colors.Black);

    /// <summary>
    /// This method returns the color this light carries along one of its samples: the glow it found.
    /// </summary>
    /// <param name="sample">The sample being asked about.</param>
    /// <returns>The light along it.</returns>
    public override Color ColorFor(LightSample sample)
    {
        return sample.Carried is null ? Color : sample.Carried * Color;
    }

    /// <summary>
    /// This method returns the light this light carries along one of its samples.  Carried band by band, the
    /// glow is worked out again at the place the sample found, as a ray meeting it there would find it: a
    /// glowing surface's color and its glow's are each a spectrum, and their product is not the spectrum of the
    /// product of the two colors.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="sample">The sample being asked about.</param>
    /// <returns>The light along it.</returns>
    public override TS EmittedToward<TS>(LightSample sample)
    {
        if (TS.IsSpectral && sample.Seen is { } seen && sample.Odds > 0)
            return _scene.GlowSeenAt<TS>(seen) * (1 / (Math.PI * sample.Odds));

        return TS.FromIlluminant(ColorFor(sample));
    }

    /// <summary>
    /// This method returns how likely this light is to look along the given way from a point toward the
    /// glowing surface a ray going that way met, per unit of solid angle: that surface's share from the point
    /// over the solid angle of its cone.  Nought, for a surface this light does not look at.  A rough surface's
    /// reflection meeting the surface is weighed against these odds.
    /// </summary>
    /// <param name="from">Where the way sets off.</param>
    /// <param name="hit">What it met.</param>
    /// <returns>The odds.</returns>
    public double OddsToward(Point from, Intersection hit)
    {
        if (!_which.TryGetValue((hit.Surface, hit.Portals), out int which))
            return 0;

        double total = WeighedFrom(from)[^1];
        Vector way = hit.Point - from;
        double length = way.Magnitude;

        return total > 0 && length > 0 ? OddsAlong(_glowing[which], from, way * (1 / length), -1, total) : 0;
    }

    /// <summary>
    /// This method returns how likely the light is to look along a direction from a point toward a glowing
    /// surface, per unit of solid angle: a ball's share over the solid angle of its cone is the surface's
    /// brightness, so the odds are that over the total, for every ball round the surface whose cone holds the
    /// direction -- the one the direction was picked in among them, wherever rounding puts it.
    /// </summary>
    private double OddsAlong(Glowing glowing, Point point, Vector direction, int picked, double total)
    {
        int holding = 0;

        for (int ball = glowing.FirstBall; ball < glowing.FirstBall + glowing.BallCount; ball++)
        {
            if (ball == picked || Holds(ball, point, direction))
                holding++;
        }

        return glowing.Brightness * holding / total;
    }

    /// <summary>
    /// This method reports whether the cone a ball fills, seen from a point, holds a direction: whether a ray
    /// that way meets the ball, or the point is inside it.  It is measured by how far the ray passes from the
    /// ball's middle, which keeps its digits for a far ball's tiny cone.
    /// </summary>
    private bool Holds(int ball, Point point, Vector direction)
    {
        Vector toward = _middles[_instant][ball] - point;
        double radius = _radii[_instant][ball];
        double squared = radius * radius;

        if (toward.Dot(toward) <= squared)
            return true;

        Vector off = toward.Cross(direction);

        return toward.Dot(direction) > 0 && off.Dot(off) <= squared;
    }

    /// <summary>
    /// This method returns the middle of the cone one of the balls round the glowing surfaces fills, seen from
    /// a point, and how far out it reaches: one less the cosine of its edge, which is two -- the whole sphere --
    /// from inside the ball.
    /// </summary>
    private (Vector Axis, double Outward) ConeOf(int ball, Point point)
    {
        Vector toward = _middles[_instant][ball] - point;
        double distance = toward.Magnitude;
        double radius = _radii[_instant][ball];

        if (distance <= radius)
            return (Directions.Up, 2);

        // One less the cosine of the edge, worked out so that a far ball's tiny cone keeps its digits.
        double ratio = radius * radius / (distance * distance);

        return (toward * (1 / distance), ratio / (1 + Math.Sqrt(1 - ratio)));
    }

    /// <summary>
    /// This method returns the running total of the balls' shares from a point: each as bright as the surface
    /// it is round, times the solid angle it fills there.
    /// </summary>
    private double[] WeighedFrom(Point point)
    {
        if (_weighed is { } weighed && ReferenceEquals(weighed.Light, this) &&
            (ReferenceEquals(weighed.From, point) ||
             (weighed.From.X == point.X && weighed.From.Y == point.Y && weighed.From.Z == point.Z)))
            return weighed.Running;

        double[] running = new double[_brightness.Length];
        double total = 0;

        for (int ball = 0; ball < _brightness.Length; ball++)
        {
            total += _brightness[ball] * 2 * Math.PI * ConeOf(ball, point).Outward;
            running[ball] = total;
        }

        _weighed = new Weighed(this, point, running);

        return running;
    }

    /// <summary>
    /// This method finds which share of a running total holds the given amount.
    /// </summary>
    private static int ShareHolding(double[] running, double wanted)
    {
        int low = 0;
        int high = running.Length - 1;

        while (low < high)
        {
            int middle = (low + high) / 2;

            if (running[middle] <= wanted)
                low = middle + 1;
            else
                high = middle;
        }

        return low;
    }
}
