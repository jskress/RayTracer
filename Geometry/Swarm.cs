using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a swarm: many small copies of one thing thrown from a place, a few at a time, each falling
/// under gravity, dragged by the air and bouncing off the scene until its life is up -- sparks, the water of a
/// fountain, leaves, snow.  It is a group of instances of the one thing, filled when the scene's bodies are moved
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

        if (Thing is null || Rate <= 0)
            return;

        if (!Thing.SharedAndReady)
        {
            Thing.SharedAndReady = true;
            Thing.PrepareForRendering(SampleTimes);
        }

        // Half the longest side of the thing's box, as a body's, so a ball comes to rest on its own surface: the ball
        // round the box would hold a drop up off the ground by most of its radius.
        BoundingBox box = Thing.TrueBoundingBox();
        Vector half = box is null ? new Vector(0.01, 0.01, 0.01) : (box.Maximum - box.Minimum) / 2;
        double radius = Radius ?? Math.Max(half.X, Math.Max(half.Y, half.Z));
        double mass = 4.0 / 3 * Math.PI * radius * radius * radius * Core.Physics.Body.Density;
        double dragging = 0.5 * Core.Physics.World.AirDensity * 0.47 * Math.PI * radius * radius * Drag / mass;
        Matrix toWorld = Transform;
        Matrix fromWorld = Transform.Invert();
        Point from = toWorld * From;
        Vector toward = (toWorld * Toward).Unit;
        int seed = Seed ?? 0;

        if (time < Since)
            return;

        // Only the copies thrown within the longest life of the time can be alive, so a swarm long in flow costs no
        // more than one just begun.
        long last = (long) Math.Floor((time - Since) * Rate);
        long first = Math.Max(0, (long) Math.Floor((time - Math.Max(LifeFrom, LifeTo) - Since) * Rate));

        for (long number = first; number <= last; number++)
        {
            double born = Since + number / Rate;
            double life = LifeFrom + (LifeTo - LifeFrom) * Draw(seed, number, 1);

            if (time - born >= life)
                continue;

            Vector velocity = Thrown(toward, seed, number) * (SpeedFrom + (SpeedTo - SpeedFrom) * Draw(seed, number, 2));
            Point place = from;
            double at = born;

            // All the way to the time, the last step what is left: stopping short once less than half a step was
            // left held each copy up to a millisecond behind, by as much as its birth fell, several millimeters at
            // these speeds and different for every copy.
            void FlyTo(double until)
            {
                while (until - at > 1e-12)
                {
                    double step = Math.Min(Step, until - at);
                    Vector air = velocity - wind;

                    velocity += (gravity - air * (dragging * air.Magnitude)) * step;
                    (place, velocity) = Flown(strike, place, velocity, step, radius);
                    at += step;
                }
            }

            FlyTo(time);

            Point now = place;
            Point[] through = instants.Select(instant =>
            {
                FlyTo(time + instant * interval);

                return place;
            }).ToArray();
            Instance copy = new () { Prototype = Thing, Transform = fromWorld * Transforms.Translate(now.X, now.Y, now.Z) };

            if (through.Length > 0)
            {
                Matrix[] motions = through
                    .Select(there => fromWorld * Transforms.Translate(there.X - now.X, there.Y - now.Y, there.Z - now.Z) * toWorld)
                    .ToArray();

                copy.MotionAt = fraction => motions[NearestOf(instants, fraction)];
            }

            Add(copy);
        }

        base.PrepareSurfaceForRendering();

        BoundingBox = GetDefaultBoundingBox();
        BoundingBox?.Expand();
    }

    // A copy moved on a step: straight, unless what lies along the way stops it, when it stands at the meeting and
    // goes on turned away by what it keeps of its speed, with some lost across.
    private (Point Place, Vector Velocity) Flown(
        Func<Point, Vector, double, (double Distance, Vector Normal)?> strike, Point place, Vector velocity, double step,
        double radius)
    {
        Vector move = velocity * step;
        double distance = move.Magnitude;

        if (distance < 1e-15)
            return (place, velocity);

        Vector way = move / distance;

        if (strike(place, way, distance + radius) is not { } hit)
            return (place + move, velocity);

        Point stopped = place + way * Math.Max(0, hit.Distance - radius);
        double into = velocity.Dot(hit.Normal);

        if (into >= 0)
            return (place + move, velocity);

        Vector across = velocity - hit.Normal * into;
        double keep = into < -RestingSpeed ? Bounce : 0;

        return (stopped, across * (1 - Friction) - hit.Normal * (into * keep));
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
