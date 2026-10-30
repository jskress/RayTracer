using RayTracer.Basics;
using RayTracer.Core.Physics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a swarm: many small copies of one thing thrown from a place, a few at a time or all in one
/// burst, each falling under gravity, dragged by the air and bouncing off the scene until its life is up -- sparks,
/// the water of a fountain, leaves, snow, a firework.  It is a group of instances of the one thing, filled when the scene's bodies are moved
/// (<see cref="PhysicsSettler"/>), each copy moved on its own from the moment it was thrown: copies do not strike
/// one another or the scene's bodies, which is what lets there be thousands.
/// <para>
/// Every copy's throw -- which way, how fast, how long it lives -- is drawn from its number and the swarm's seed, so
/// the copy thrown fortieth is the same copy in every frame and nothing it does depends on how the frames are asked
/// for.
/// </para>
/// </summary>
public class Swarm : Group
{
    // How long one step of a copy's flight is: coarser than a body's, since a copy only flies and bounces.
    private const double Step = 1.0 / 500;

    // Slower than this into what it meets, a copy keeps none of its speed into it.
    private const double RestingSpeed = 0.05;

    /// <summary>
    /// This property holds the thing every copy is.
    /// </summary>
    public Surface Thing { get; set; }

    /// <summary>
    /// This property holds where copies are thrown from, in the swarm's own space.
    /// </summary>
    public Point From { get; set; } = Point.Zero;

    /// <summary>
    /// This property holds the way copies are thrown, in the swarm's own space.
    /// </summary>
    public Vector Toward { get; set; } = new (0, 1, 0);

    /// <summary>
    /// This property holds how far from that way a copy may be thrown, in radians.
    /// </summary>
    public double Spread { get; set; }

    /// <summary>
    /// This property holds the least speed a copy is thrown at.
    /// </summary>
    public double SpeedFrom { get; set; } = 1;

    /// <summary>
    /// This property holds the greatest speed a copy is thrown at.
    /// </summary>
    public double SpeedTo { get; set; } = 1;

    /// <summary>
    /// This property holds how many copies are thrown a second.
    /// </summary>
    public double Rate { get; set; } = 50;

    /// <summary>
    /// This property holds when the swarm began throwing: nought unless said, and before nought for one already in
    /// full flow when the animation begins -- a fountain running, a fire long alight.
    /// </summary>
    public double Since { get; set; }

    /// <summary>
    /// This property holds how many copies the swarm throws in one burst, or null for a swarm that throws a steady
    /// flow, <see cref="Rate"/> a second from <see cref="Since"/> on.
    /// </summary>
    public long? Burst { get; set; }

    /// <summary>
    /// This property holds when a burst is thrown: nought unless said.
    /// </summary>
    public double BurstAt { get; set; }

    /// <summary>
    /// This property holds how long a burst takes, its copies born evenly over it: nought unless said, every copy at
    /// the one instant.
    /// </summary>
    public double BurstOver { get; set; }

    /// <summary>
    /// This property holds the least time a copy lives.
    /// </summary>
    public double LifeFrom { get; set; } = 2;

    /// <summary>
    /// This property holds the greatest time a copy lives.
    /// </summary>
    public double LifeTo { get; set; } = 2;

    /// <summary>
    /// This property holds how much of its speed into a thing a copy keeps coming away.
    /// </summary>
    public double Bounce { get; set; } = 0.3;

    /// <summary>
    /// This property holds how much of its speed across a thing a copy loses meeting it.
    /// </summary>
    public double Friction { get; set; } = 0.5;

    /// <summary>
    /// This property holds how much of the air's drag on a thing its size a copy feels.
    /// </summary>
    public double Drag { get; set; } = 1;

    /// <summary>
    /// This property holds the radius a copy meets things at, or null for one from its size.
    /// </summary>
    public double? Radius { get; set; }

    /// <summary>
    /// This property holds the shape copies are thrown from within, in the swarm's own space -- each copy from a
    /// place of its own inside it -- or null to throw them all from <see cref="From"/>.
    /// </summary>
    public Surface Within { get; set; }

    /// <summary>
    /// This property holds the least a copy spins as it flies, in radians a second.
    /// </summary>
    public double SpinFrom { get; set; }

    /// <summary>
    /// This property holds the most a copy spins as it flies, in radians a second.
    /// </summary>
    public double SpinTo { get; set; }

    /// <summary>
    /// This property holds the least speed a copy falls at in still air, or null for the fall its drag gives it.
    /// </summary>
    public double? FallsFrom { get; set; }

    /// <summary>
    /// This property holds the greatest speed a copy falls at in still air.
    /// </summary>
    public double? FallsTo { get; set; }

    /// <summary>
    /// This property notes whether copies tumble as they fly, which is also what has them come to rest and lie
    /// there once they land.
    /// </summary>
    public bool Spins => SpinFrom > 0 || SpinTo > 0;

    // How long a landed copy takes to settle onto its broadest side.
    private const double Settling = 0.15;

    // How many places in a shape's box are tried for each copy before it is not thrown at all.
    private const int Tries = 256;

    /// <summary>
    /// This method fills the swarm with the copies alive at a time, each where its flight has taken it and, if the
    /// shutter is open, moving as it goes on through the frame.
    /// </summary>
    /// <param name="strike">How the scene is found along a ray, as the bodies find it.</param>
    /// <param name="gravity">How gravity pulls.</param>
    /// <param name="wind">How the air moves.</param>
    /// <param name="time">The frame's time.</param>
    /// <param name="interval">How long a frame lasts, nought for a still.</param>
    /// <param name="instants">How far through the frame the camera looks, sorted; empty for a still.</param>
    public void Fill(
        Func<Point, Vector, double, (double Distance, Vector Normal)?> strike, Vector gravity, Vector wind,
        double time, double interval, double[] instants)
    {
        Surfaces.Clear();

        // Every copy is worked out as though thrown from where the swarm stands at the frame's time, so a swarm that
        // moves -- a motion of its own, or a placement that follows the time -- would carry its whole plume along as
        // one piece rather than leave a trail: refused, rather than drawn wrong.
        if (MotionAt is not null)
        {
            throw new InvalidOperationException(
                "A swarm throws its copies from where it stands, and one that moves would carry every copy in flight " +
                "along with it rather than leave them behind; a swarm must stand still while it throws.");
        }

        if (Thing is null || Rate <= 0)
            return;

        // Every copy is the one thing in another place, so the thing must be one that can stand in many: a glow is
        // lit once, where the thing was made, a medium found where it was drawn, a mark named in all the places at
        // once -- and so on, for whatever an instance refuses.
        if (!Instance.MayBeShared(Thing))
        {
            throw new InvalidOperationException(
                "A swarm's copies are one thing in many places, and this thing cannot be shared between places: " +
                "it glows, holds a medium, moves of itself, or holds a joint that aims or reaches, a mark or a span.");
        }

        if (!Thing.SharedAndReady)
        {
            Thing.SharedAndReady = true;
            Thing.PrepareForRendering(SampleTimes);
        }

        if (Within is not null && !Within.SharedAndReady)
        {
            Within.SharedAndReady = true;
            Within.PrepareForRendering(SampleTimes);
        }

        // Half the longest side of the thing's box, as a body's, so a ball comes to rest on its own surface: the ball
        // round the box would hold a drop up off the ground by most of its radius.  The box is the one it stands in
        // as placed, its own transforms and all: in its own space a drop scaled to seven millimeters was two units
        // across, so it met the water a meter early and the air hardly at all.
        BoundingBox box = BoxAround(Thing, true);
        Vector half = box is null ? new Vector(0.01, 0.01, 0.01) : (box.Maximum - box.Minimum) / 2;
        double radius = Radius ?? Math.Max(half.X, Math.Max(half.Y, half.Z));
        double mass = 4.0 / 3 * Math.PI * radius * radius * radius * Core.Physics.Body.Density;
        double dragging = 0.5 * Core.Physics.World.AirDensity * 0.47 * Math.PI * radius * radius * Drag / mass;
        Matrix toWorld = Transform;
        Matrix fromWorld = Transform.Invert();
        Point from = toWorld * From;
        Vector toward = (toWorld * Toward).Unit;
        int seed = Seed ?? 0;
        BoundingBox room = Within is null ? null : RoomWithin(seed);
        (Vector Axis, double Half)? broadest = Spins ? BroadestSideOf(half) : null;
        double pull = gravity.Magnitude > 1e-12 ? gravity.Magnitude : 9.81;

        // A burst is a flow of its copies over how long it takes, from its time, stopping at its last copy; one that
        // takes no time throws them all at once.
        double since = Burst is null ? Since : BurstAt;
        double rate = Burst is not { } count ? Rate : BurstOver > 0 ? count / BurstOver : double.PositiveInfinity;
        long final = Burst is { } thrown ? thrown - 1 : long.MaxValue;
        bool atOnce = double.IsPositiveInfinity(rate);

        if (time < since)
            return;

        // Only the copies thrown within the longest life of the time can be alive, so a swarm long in flow costs no
        // more than one just begun.
        long last = atOnce ? final : Math.Min(final, (long) Math.Floor((time - since) * rate));
        long first = atOnce ? 0 : Math.Max(0, (long) Math.Floor((time - Math.Max(LifeFrom, LifeTo) - since) * rate));

        for (long number = first; number <= last; number++)
        {
            double born = atOnce ? since : since + number / rate;
            double life = LifeFrom + (LifeTo - LifeFrom) * Draw(seed, number, 1);

            if (time - born >= life)
                continue;

            Point place;

            if (room is null)
                place = from;
            else if (PlaceWithin(room, seed, number) is { } inside)
                place = toWorld * inside;
            else
                continue;

            Vector velocity = Thrown(toward, seed, number) * (SpeedFrom + (SpeedTo - SpeedFrom) * Draw(seed, number, 2));
            double at = born;
            double drag = dragging;

            // Falling at a speed in still air is falling against as much drag as gravity pulls at that speed.
            if (FallsFrom is { } slowest)
            {
                double falls = slowest + ((FallsTo ?? slowest) - slowest) * Draw(seed, number, 5);

                drag = pull / (falls * falls);
            }

            Turn turned = Turn.None;
            Vector spinning = null;
            double spin = 0;

            if (Spins)
            {
                turned = AnyTurn(seed, number);
                spinning = AnyWay(seed, number);
                spin = SpinFrom + (SpinTo - SpinFrom) * Draw(seed, number, 9);
            }

            // Whether a copy thrown from within a shape is in it yet: it passes whatever is in there -- the leaves of
            // the crown it falls from -- and meets the scene only once it has left.
            bool among = room is not null;

            // When a tumbling copy came to rest, and on what way up a thing.
            double? rested = null;
            Vector restedOn = null;
            (Turn Lying, Point Lowered)? settled = null;

            // All the way to the time, the last step what is left: stopping short once less than half a step was
            // left held each copy up to a millisecond behind, by as much as its birth fell, several millimeters at
            // these speeds and different for every copy.  A tumbling copy that has come to rest stays where it is.
            void FlyTo(double until)
            {
                while (rested is null && until - at > 1e-12)
                {
                    double step = Math.Min(Step, until - at);
                    Vector air = velocity - wind;
                    Vector met;

                    velocity += (gravity - air * (drag * air.Magnitude)) * step;

                    if (among && !Containment.Holds(Within, fromWorld * place))
                        among = false;

                    (place, velocity, met) = Flown(among ? Passing : strike, place, velocity, step, radius);
                    at += step;

                    if (Spins && met is not null && velocity.Magnitude < RestingSpeed)
                        (rested, restedOn) = (at, met);
                }
            }

            // Where a tumbling copy stands in the world at a time: turned by its spin until it came to rest, and then
            // settling over a moment onto its broadest side, down onto what it lies on.
            Matrix PlacedAt(double when)
            {
                FlyTo(when);

                Turn turn = Turn.About(spinning, spin * (Math.Min(when, rested ?? when) - born)) * turned;
                Point where = place;

                if (rested is { } landed && when > landed)
                {
                    settled ??= Settled(strike, turn, place, restedOn, broadest, radius);

                    double share = Math.Min(1, (when - landed) / Settling);

                    share = share * share * (3 - 2 * share);
                    turn = turn.Toward(settled.Value.Lying, share);
                    where = place + (settled.Value.Lowered - place) * share;
                }

                return Transforms.Translate(where.X, where.Y, where.Z) * turn.ToMatrix();
            }

            Instance copy;

            if (Spins)
            {
                Matrix here = PlacedAt(time);
                Matrix undo = here.Invert();
                Matrix[] motions = instants
                    .Select(instant => fromWorld * PlacedAt(time + instant * interval) * undo * toWorld)
                    .ToArray();

                copy = new Instance { Prototype = Thing, Transform = fromWorld * here };

                if (motions.Length > 0)
                    copy.MotionAt = fraction => motions[NearestOf(instants, fraction)];
            }
            else
            {
                FlyTo(time);

                Point now = place;
                Point[] through = instants.Select(instant =>
                {
                    FlyTo(time + instant * interval);

                    return place;
                }).ToArray();

                copy = new Instance { Prototype = Thing, Transform = fromWorld * Transforms.Translate(now.X, now.Y, now.Z) };

                if (through.Length > 0)
                {
                    Matrix[] motions = through
                        .Select(there => fromWorld * Transforms.Translate(there.X - now.X, there.Y - now.Y, there.Z - now.Z) * toWorld)
                        .ToArray();

                    copy.MotionAt = fraction => motions[NearestOf(instants, fraction)];
                }
            }

            Add(copy);
        }

        base.PrepareSurfaceForRendering();

        BoundingBox = GetDefaultBoundingBox();
        BoundingBox?.Expand();
    }

    // A copy moved on a step: straight, unless what lies along the way stops it, when it stands at the meeting and
    // goes on turned away by what it keeps of its speed, with some lost across -- and which way what it met faced.
    private (Point Place, Vector Velocity, Vector Met) Flown(
        Func<Point, Vector, double, (double Distance, Vector Normal)?> strike, Point place, Vector velocity, double step,
        double radius)
    {
        Vector move = velocity * step;
        double distance = move.Magnitude;

        if (distance < 1e-15)
            return (place, velocity, null);

        Vector way = move / distance;

        if (strike(place, way, distance + radius) is not { } hit)
            return (place + move, velocity, null);

        Point stopped = place + way * Math.Max(0, hit.Distance - radius);
        double into = velocity.Dot(hit.Normal);

        if (into >= 0)
            return (place + move, velocity, null);

        Vector across = velocity - hit.Normal * into;
        double keep = into < -RestingSpeed ? Bounce : 0;

        return (stopped, across * (1 - Friction) - hit.Normal * (into * keep), hit.Normal);
    }

    // What a copy meets while it is still among what it was thrown from within: nothing.
    private static (double Distance, Vector Normal)? Passing(Point from, Vector way, double within) => null;

    // How a copy come to rest lies at last: turned by the least turn that lays its broadest side on what it rests
    // on, and down onto it from the ball it met it as.  A thing with no side broader than the rest lies as it landed.
    private static (Turn Lying, Point Lowered) Settled(
        Func<Point, Vector, double, (double Distance, Vector Normal)?> strike, Turn turn, Point place, Vector on,
        (Vector Axis, double Half)? broadest, double radius)
    {
        if (broadest is not { } side)
            return (turn, place);

        Vector facing = turn.Rotate(side.Axis);

        if (facing.Dot(on) < 0)
            facing = -facing;

        Turn lying = Turn.Between(facing, on) * turn;
        Point lowered = place;

        // Half its thickness up -- or, for a thing with none, as a flat leaf has, a hundredth of its size, since one
        // lying exactly in the face it rests on would come and go with it pixel by pixel.
        double up = Math.Max(side.Half, radius * 0.01);

        if (strike(place, -on, 2 * radius + up) is { } below)
            lowered = place - on * Math.Max(0, below.Distance - up);

        return (lying, lowered);
    }

    // The thing's broadest side, by its thinnest axis and half its thickness along it -- if it is clearly thinner
    // one way than the others, as a leaf or a flake is and a ball or a die is not.
    private static (Vector Axis, double Half)? BroadestSideOf(Vector half)
    {
        double[] sides = [half.X, half.Y, half.Z];
        int thinnest = Array.IndexOf(sides, sides.Min());
        double middle = sides.Order().ElementAt(1);

        if (sides[thinnest] > 0.6 * middle)
            return null;

        Vector axis = thinnest switch
        {
            0 => new Vector(1, 0, 0),
            1 => new Vector(0, 1, 0),
            _ => new Vector(0, 0, 1)
        };

        return (axis, sides[thinnest]);
    }

    // The box round the shape copies are thrown from within, once it is known to have an inside.
    private BoundingBox RoomWithin(int seed)
    {
        BoundingBox box = BoxAround(Within, true);

        if (box is null || box.IsEmpty || !double.IsFinite(box.Minimum.X + box.Minimum.Y + box.Minimum.Z) ||
            !double.IsFinite(box.Maximum.X + box.Maximum.Y + box.Maximum.Z))
            throw new Exception("A swarm's shape must end somewhere: a plane or an open-ended shape has no inside to throw copies from.");

        for (int probe = 0; probe < 1024; probe++)
        {
            if (Containment.Holds(Within, Drawn(box, seed, -1, probe)))
                return box;
        }

        throw new Exception("A swarm's shape has no inside to throw copies from; it must be a closed surface.");
    }

    // A place of a copy's own inside the shape: the first of its draws in the shape's box that falls inside, or
    // none if all of them miss.
    private Point PlaceWithin(BoundingBox box, int seed, long number)
    {
        for (int attempt = 0; attempt < Tries; attempt++)
        {
            Point place = Drawn(box, seed, number, attempt);

            if (Containment.Holds(Within, place))
                return place;
        }

        return null;
    }

    // A place drawn in a box, for a copy and its attempt.
    private static Point Drawn(BoundingBox box, int seed, long number, int attempt) => new (
        box.Minimum.X + (box.Maximum.X - box.Minimum.X) * Draw(seed, number, 100 + 3 * attempt),
        box.Minimum.Y + (box.Maximum.Y - box.Minimum.Y) * Draw(seed, number, 101 + 3 * attempt),
        box.Minimum.Z + (box.Maximum.Z - box.Minimum.Z) * Draw(seed, number, 102 + 3 * attempt));

    // A turn of a copy's own, evenly over every turn there is (Shoemake's).
    private static Turn AnyTurn(int seed, long number)
    {
        double u = Draw(seed, number, 6), a = 2 * Math.PI * Draw(seed, number, 7), b = 2 * Math.PI * Draw(seed, number, 8);
        double s = Math.Sqrt(1 - u), t = Math.Sqrt(u);

        return new Turn(t * Math.Cos(b), s * Math.Sin(a), s * Math.Cos(a), t * Math.Sin(b));
    }

    // A way of a copy's own, evenly over every way there is.
    private static Vector AnyWay(int seed, long number)
    {
        double z = 2 * Draw(seed, number, 10) - 1, around = 2 * Math.PI * Draw(seed, number, 11);
        double r = Math.Sqrt(Math.Max(0, 1 - z * z));

        return new Vector(r * Math.Cos(around), r * Math.Sin(around), z);
    }

    // A way within the spread of the way copies are thrown, evenly over the cap of directions it allows.
    private Vector Thrown(Vector toward, int seed, long number)
    {
        double cosine = 1 - Draw(seed, number, 3) * (1 - Math.Cos(Spread));
        double sine = Math.Sqrt(Math.Max(0, 1 - cosine * cosine));
        double around = 2 * Math.PI * Draw(seed, number, 4);
        Vector across = Math.Abs(toward.X) < 0.9 ? new Vector(1, 0, 0) : new Vector(0, 0, 1);
        Vector u = toward.Cross(across).Unit;
        Vector v = toward.Cross(u);

        return (toward * cosine + u * (sine * Math.Cos(around)) + v * (sine * Math.Sin(around))).Unit;
    }

    // A draw from nought to one for a copy, the same for the same seed, copy and purpose.
    private static double Draw(int seed, long number, int purpose)
    {
        ulong hash = (ulong) seed * 0x9E3779B97F4A7C15UL ^ (ulong) number * 0xBF58476D1CE4E5B9UL ^
                     (ulong) purpose * 0x94D049BB133111EBUL;

        hash ^= hash >> 30;
        hash *= 0xBF58476D1CE4E5B9UL;
        hash ^= hash >> 27;
        hash *= 0x94D049BB133111EBUL;
        hash ^= hash >> 31;

        return (hash >> 11) * (1.0 / (1UL << 53));
    }

    private static int NearestOf(double[] instants, double fraction)
    {
        int best = 0;

        for (int index = 1; index < instants.Length; index++)
        {
            if (Math.Abs(instants[index] - fraction) < Math.Abs(instants[best] - fraction))
                best = index;
        }

        return best;
    }

    /// <summary>
    /// This method prepares the swarm, which holds nothing until it is filled.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        Surfaces.Clear();
        base.PrepareSurfaceForRendering();
    }
}
