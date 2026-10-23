using RayTracer.Basics;

namespace RayTracer.Core.Physics;

/// <summary>
/// This class moves bodies through time: gravity and the air push on them, they strike the scene and each other,
/// and what they are tied to holds them.  It steps a thousandth of a second at a time from time nought, the same
/// way every time, so a frame agrees with the one before it however the frames are asked for.
/// <para>
/// Each step pushes every body and moves it, and then, a few rounds over, pulls each tied body back onto its
/// string, parts bodies that overlap, and pushes bodies out of the scene, each meeting turned into an impulse --
/// sequential impulses, which is how the strike of a Newton's cradle passes from ball to ball.  Bodies collide with
/// one another as balls; with the scene as balls or, if they are boxes, by their corners, the scene found by rays.
/// What it does not do is hold a pile of things resting on one another: there is no stacking here.
/// </para>
/// </summary>
public sealed class World
{
    /// <summary>
    /// This is how long one step is, in seconds.
    /// </summary>
    public const double Step = 1.0 / 1000;

    /// <summary>
    /// This is how dense the air is, in kilograms a cubic meter, which with a body's size is what drags on it.
    /// </summary>
    public const double AirDensity = 1.2;

    // How many times a step goes round its strings and its meetings, so that what one settles is passed on.
    private const int Rounds = 10;

    // A body meeting something slower than this keeps none of its speed into it: it comes to rest rather than
    // trembling in a string of ever smaller bounces.
    private const double RestingSpeed = 0.05;

    // How near the scene a box's corner may lie, as a share of its reach from the middle, and still touch it.
    private const double Touching = 0.005;

    // A body whose fastest point has gone slower than this for as long as `Drowsy` falls asleep: it stands still,
    // untouched by gravity or the scene, until another body strikes it or a string pulls it.  Gravity sinks a
    // resting corner a hundredth of a millimeter into the scene every step, whatever the body's size, and to a die
    // that is a great deal: put back out each step, it was never quite still, turning a degree or two a second where
    // a crate lay dead.  Nothing falling, swinging or rolling down more than a fraction of a degree of slope goes
    // this slowly for this long.  Gravity and the wind do not change, so a body still under them stays still.
    private const double Sleepy = 0.005;
    private const double Drowsy = 0.2;

    // The directions a ball looks out along for the scene it overlaps: the axes and the corners of a cube.
    private static readonly Vector[] Looks = BuildLooks();

    private readonly Vector _gravity;
    private readonly Vector _wind;
    private readonly Func<Point, Vector, double, (double Distance, Vector Normal)?> _strike;
    private readonly List<Mover> _movers = [];

    /// <summary>
    /// This property holds how far through time the world has been moved.
    /// </summary>
    public double Time { get; private set; }

    /// <summary>
    /// This property holds the bodies the world moves.
    /// </summary>
    public IReadOnlyList<Mover> Movers => _movers;

    /// <param name="gravity">How gravity pulls, in meters a second squared.</param>
    /// <param name="wind">How the air moves.</param>
    /// <param name="strike">How the scene is found: the nearest crossing along a ray from a place, within a
    /// distance, and the scene's normal there facing back along the ray; null for none.</param>
    public World(Vector gravity, Vector wind, Func<Point, Vector, double, (double Distance, Vector Normal)?> strike)
    {
        _gravity = gravity;
        _wind = wind;
        _strike = strike;
    }

    /// <summary>
    /// This method adds a body to the world, as it stands at time nought.
    /// </summary>
    /// <param name="spec">What the scene says of it.</param>
    /// <param name="center">Where its middle stands.</param>
    /// <param name="radius">The radius of the ball it is, or of the ball round it for a box.</param>
    /// <param name="corners">A box's corners, from its middle; null for a ball.</param>
    public Mover Add(Body spec, Point center, double radius, Vector[] corners)
    {
        Mover mover = new (spec, center, radius, corners);

        _movers.Add(mover);

        return mover;
    }

    /// <summary>
    /// This method ties the bodies to what they are tied to, once all of them are in the world: to their pins,
    /// and by name to one another.
    /// </summary>
    /// <param name="named">The bodies the scene names, by their names.</param>
    public void Tie(IReadOnlyDictionary<string, Mover> named)
    {
        foreach (Mover mover in _movers)
        {
            foreach (Tether tether in mover.Spec.Tethers)
            {
                Mover other = null;

                if (tether.Name is not null && !named.TryGetValue(tether.Name, out other))
                    throw new InvalidOperationException($"A body is tied to '{tether.Name}', and no body has that name.");

                Point to = other?.Position ?? tether.Pin;
                double length = tether.Length ?? (mover.Position - to).Magnitude;

                mover.Ties.Add(new Tie(tether.Pin, other, length, (mover.Position - to).Unit));
            }
        }
    }

    /// <summary>
    /// This method moves the world on to a time, a step at a time.
    /// </summary>
    public void RunTo(double time)
    {
        while (Time + Step / 2 < time)
        {
            Advance(Math.Min(Step, time - Time));
        }
    }

    private void Advance(double step)
    {
        foreach (Mover mover in _movers)
        {
            mover.Push(_gravity, _wind, step);
            mover.Unpress();
        }

        for (int round = 0; round < Rounds; round++)
        {
            foreach (Mover mover in _movers)
            {
                foreach (Tie tie in mover.Ties)
                    Hold(mover, tie);
            }

            for (int first = 0; first < _movers.Count; first++)
            {
                for (int second = first + 1; second < _movers.Count; second++)
                    Part(_movers[first], _movers[second]);
            }

            foreach (Mover mover in _movers)
                Strike(mover);
        }

        foreach (Mover mover in _movers)
        {
            mover.TurnWithItsTies();
            mover.Drowse(step, Sleepy, Drowsy);
        }

        Time += step;
    }

    // A string pulls a body back to its length and takes away any speed away from what it is tied to.  A string
    // between two bodies shares the pull between them by their masses.
    private static void Hold(Mover mover, Tie tie)
    {
        Point to = tie.Other?.Position ?? tie.Pin;
        Vector away = mover.Position - to;
        double stretch = away.Magnitude - tie.Length;

        if (stretch <= 0 || away.Magnitude < 1e-12)
            return;

        Vector n = away / away.Magnitude;
        Mover other = tie.Other;

        mover.Wake();
        other?.Wake();
        double total = mover.InverseMass + (other?.InverseMass ?? 0);

        if (total <= 0)
            return;

        mover.Position -= n * (stretch * mover.InverseMass / total);

        if (other is not null)
            other.Position += n * (stretch * other.InverseMass / total);

        double parting = (mover.Velocity - (other?.Velocity ?? new Vector(0, 0, 0))).Dot(n);

        if (parting <= 0)
            return;

        Vector impulse = n * (parting / total);

        mover.Velocity -= impulse * mover.InverseMass;

        if (other is not null)
            other.Velocity += impulse * other.InverseMass;
    }

    // Two bodies that overlap, as balls, are parted, and if they are coming together each takes the impulse that
    // turns them apart by what they keep of their speed, with friction across.
    private static void Part(Mover first, Mover second)
    {
        Vector between = second.Position - first.Position;
        double distance = between.Magnitude;
        double reach = first.Radius + second.Radius;

        if (distance >= reach || distance < 1e-12)
            return;

        first.Wake();
        second.Wake();

        Vector n = between / distance;
        double total = first.InverseMass + second.InverseMass;

        if (total <= 0)
            return;

        double overlap = reach - distance;

        first.Position -= n * (overlap * first.InverseMass / total);
        second.Position += n * (overlap * second.InverseMass / total);

        Vector atFirst = n * first.Radius, atSecond = n * -second.Radius;
        Vector closing = second.VelocityAt(atSecond) - first.VelocityAt(atFirst);
        double into = closing.Dot(n);

        if (into >= 0)
            return;

        double keep = into < -RestingSpeed ? (first.Spec.Bounce + second.Spec.Bounce) / 2 : 0;
        double normal = -(1 + keep) * into / (total + first.Turning(atFirst, n) + second.Turning(atSecond, n));

        first.Apply(n * -normal, atFirst);
        second.Apply(n * normal, atSecond);

        Vector sliding = closing - n * into;

        if (sliding.Magnitude < 1e-12)
            return;

        Vector t = sliding.Unit;
        double grip = Math.Sqrt(first.Spec.Friction * second.Spec.Friction);
        double across = Math.Min(sliding.Magnitude / (total + first.Turning(atFirst, t) + second.Turning(atSecond, t)),
            grip * normal);

        first.Apply(t * across, atFirst);
        second.Apply(t * -across, atSecond);
    }

    // A body into the scene is pushed back out along the scene's normal, and takes the impulse that turns it
    // away by what it keeps of its speed, with friction across -- at the place it meets, so that a box landing on
    // a corner is turned, and a ball sliding is set rolling.
    private void Strike(Mover mover)
    {
        if (mover.InverseMass <= 0 || mover.Asleep)
            return;

        if (mover.Corners is null)
        {
            (Vector Normal, double Depth, Vector Lever)? deepest = null;

            foreach (Vector look in Looks)
            {
                if (_strike(mover.Position, look, mover.Radius) is { } hit && hit.Distance < mover.Radius &&
                    (deepest is null || mover.Radius - hit.Distance > deepest.Value.Depth))
                {
                    deepest = (hit.Normal, mover.Radius - hit.Distance, look * hit.Distance);
                }
            }

            if (deepest is { } meeting)
            {
                mover.Position += meeting.Normal * meeting.Depth;
                Meet(mover, meeting.Normal, meeting.Lever);
            }

            return;
        }

        // A box meets the scene at every corner that touches it, not only the deepest: pushed out once, by the
        // deepest, it then takes each corner's impulse in turn, and the rounds bring them all to agree.  Held by its
        // deepest corner alone, a box lying on a face was pushed up at one corner a step, rocked onto the next and
        // never lay still -- a die on felt kept turning about its corners for as long as it was watched.  A corner
        // touches when it lies within a little of the scene as well as past it, since one the push has just set on
        // the scene lies exactly on it.
        int count = 0;
        double deepestDepth = 0;
        Vector outward = null;

        for (int corner = 0; corner < mover.Corners.Length; corner++)
        {
            Vector toCorner = mover.Turn.Rotate(mover.Corners[corner]);
            double reach = toCorner.Magnitude;
            double touching = reach * Touching;
            Vector look = toCorner / reach;

            if (_strike(mover.Position, look, reach + touching) is not { } hit)
                continue;

            // How far the corner stands beyond the scene's face, along its normal: the face's normal faces back
            // toward the middle, and the corner lies past the face the other way.
            double depth = (look * (hit.Distance - reach)).Dot(hit.Normal);

            if (depth <= -touching)
                continue;

            mover.Contacts[count++] = (corner, hit.Normal, toCorner);

            if (depth > deepestDepth)
            {
                deepestDepth = depth;
                outward = hit.Normal;
            }
        }

        if (outward is not null)
            mover.Position += outward * deepestDepth;

        // How fast each corner newly met is to come away is settled before any of them is pushed: settled as each was
        // met, the first corner's push had already slowed the rest, so a box landing flat came away from its later
        // corners slower than its first and tipped -- turning at six radians a second off a flat drop.
        for (int contact = 0; contact < count; contact++)
        {
            (int corner, Vector normal, Vector lever) = mover.Contacts[contact];

            if (mover.Met[corner])
                continue;

            double into = mover.VelocityAt(lever).Dot(normal);

            mover.Met[corner] = true;
            mover.Rebound[corner] = into < -RestingSpeed ? -mover.Spec.Bounce * into : 0;
        }

        for (int contact = 0; contact < count; contact++)
        {
            (int corner, Vector normal, Vector lever) = mover.Contacts[contact];

            Press(mover, corner, normal, lever);
        }
    }

    // A box's corner on the scene, met again each round of a step.  What it has been pushed by over the step so far
    // is kept, so a later round may take some of it back -- never so much that the scene pulls -- and friction is
    // held to what it has been pushed by over the whole step, not in the one round: met afresh each round, the first
    // corner took all of gravity's push and the rest none, so only it could grip and the box pivoted about it,
    // creeping four millimeters a second across the felt for ever.  How fast it is to come away was settled when it
    // was first met in the step, so the rounds after do not undo the bounce.
    private static void Press(Mover mover, int corner, Vector n, Vector at)
    {
        Vector moving = mover.VelocityAt(at);
        double into = moving.Dot(n);
        double was = mover.Pressed[corner];

        mover.Pressed[corner] = Math.Max(
            was + (mover.Rebound[corner] - into) / (mover.InverseMass + mover.Turning(at, n)), 0);
        mover.Apply(n * (mover.Pressed[corner] - was), at);

        moving = mover.VelocityAt(at);

        Vector sliding = moving - n * moving.Dot(n);
        double speed = sliding.Magnitude;

        if (speed < 1e-12)
            return;

        Vector t = sliding / speed;
        Vector held = mover.Gripped[corner] - t * (speed / (mover.InverseMass + mover.Turning(at, t)));
        double most = mover.Spec.Friction * mover.Pressed[corner];

        if (held.Magnitude > most)
            held = held.Magnitude > 0 ? held * (most / held.Magnitude) : held;

        mover.Apply(held - mover.Gripped[corner], at);
        mover.Gripped[corner] = held;
    }

    // A body meeting the scene at a lever from its middle, the scene's face turned toward it: if the place is going
    // into the face, it takes the impulse that turns it away by what it keeps of its speed, and friction across.
    private static void Meet(Mover mover, Vector n, Vector at)
    {
        Vector moving = mover.VelocityAt(at);
        double into = moving.Dot(n);

        if (into >= 0)
            return;

        double keep = into < -RestingSpeed ? mover.Spec.Bounce : 0;
        double normal = -(1 + keep) * into / (mover.InverseMass + mover.Turning(at, n));

        mover.Apply(n * normal, at);

        Vector sliding = moving - n * into;

        if (sliding.Magnitude < 1e-12)
            return;

        Vector t = sliding.Unit;
        double across = Math.Min(sliding.Magnitude / (mover.InverseMass + mover.Turning(at, t)), mover.Spec.Friction * normal);

        mover.Apply(t * -across, at);
    }

    private static Vector[] BuildLooks()
    {
        List<Vector> looks = [];

        for (int axis = 0; axis < 3; axis++)
        {
            foreach (double sign in new[] { -1.0, 1.0 })
                looks.Add(new Vector(axis == 0 ? sign : 0, axis == 1 ? sign : 0, axis == 2 ? sign : 0));
        }

        foreach (double x in new[] { -1.0, 1.0 })
        foreach (double y in new[] { -1.0, 1.0 })
        foreach (double z in new[] { -1.0, 1.0 })
            looks.Add(new Vector(x, y, z).Unit);

        return looks.ToArray();
    }
}

/// <summary>
/// This record holds a string a body is tied by: to a pin or to another body, how long it is, and the way it ran
/// from what it is tied to at time nought, by which a body held by it is turned as it swings.
/// </summary>
public sealed record Tie(Point Pin, Mover Other, double Length, Vector StartingWay);

/// <summary>
/// This class holds a body as the world moves it: what it is, and where it is, how fast and which way it is going,
/// how it is turned and how fast it is turning.
/// </summary>
public sealed class Mover
{
    /// <summary>
    /// This property holds what the scene says of the body.
    /// </summary>
    public Body Spec { get; }

    /// <summary>
    /// This property holds where its middle stood at time nought.
    /// </summary>
    public Point Start { get; }

    /// <summary>
    /// This property holds the radius of the ball it is, or of the ball round it for a box.
    /// </summary>
    public double Radius { get; }

    /// <summary>
    /// This property holds a box's corners, from its middle as it stood at time nought; null for a ball.
    /// </summary>
    public Vector[] Corners { get; }

    /// <summary>
    /// This property holds one over its mass.
    /// </summary>
    public double InverseMass { get; }

    /// <summary>
    /// This property holds the strings it is tied by.
    /// </summary>
    public List<Tie> Ties { get; } = [];

    // Where a box touches the scene in a round: which corner, the scene's face there and the corner from its middle.
    internal readonly (int Corner, Vector Normal, Vector Lever)[] Contacts = new (int, Vector, Vector)[8];

    // For each corner over a step: whether it has met the scene, how fast it is to come away, and the push and grip
    // the scene has given it so far.
    internal readonly bool[] Met = new bool[8];
    internal readonly double[] Rebound = new double[8];
    internal readonly double[] Pressed = new double[8];
    internal readonly Vector[] Gripped = [Still, Still, Still, Still, Still, Still, Still, Still];

    private static readonly Vector Still = new (0, 0, 0);

    public Point Position { get; set; }
    public Vector Velocity { get; set; }
    public Turn Turn { get; private set; } = Turn.None;
    public Vector Spin { get; private set; }

    // One over how hard it is to turn, about axes through its middle, as it stood at time nought.
    private readonly Matrix _turnless;
    private readonly double _dragging;

    internal Mover(Body spec, Point center, double radius, Vector[] corners)
    {
        Spec = spec;
        Start = center;
        Position = center;
        Radius = radius;
        Corners = corners;
        Velocity = spec.Velocity;
        Spin = spec.Spin;

        double volume;
        double area;
        double coefficient;

        if (corners is null)
        {
            volume = 4.0 / 3 * Math.PI * radius * radius * radius;
            area = Math.PI * radius * radius;
            coefficient = 0.47;
        }
        else
        {
            // The box's three edges, from one corner: the corners are in the order a box's are, so the first one
            // differs from the next three in one axis each.
            Vector a = corners[1] - corners[0], b = corners[2] - corners[0], c = corners[4] - corners[0];

            volume = Math.Abs(a.Dot(b.Cross(c)));
            area = (a.Cross(b).Magnitude + b.Cross(c).Magnitude + a.Cross(c).Magnitude) / 3;
            coefficient = 1.05;
        }

        double mass = spec.Mass ?? volume * Body.Density;

        InverseMass = mass > 0 ? 1 / mass : 0;
        _dragging = 0.5 * World.AirDensity * coefficient * area * spec.Drag;

        if (corners is null)
        {
            double inertia = 0.4 * mass * radius * radius;

            _turnless = Transforms.Scale(inertia > 0 ? 1 / inertia : 0);
        }
        else
        {
            Vector a = corners[1] - corners[0], b = corners[2] - corners[0], c = corners[4] - corners[0];
            double la = a.Magnitude, lb = b.Magnitude, lc = c.Magnitude;
            Matrix axes = new ([
                a.X / la, b.X / lb, c.X / lc, 0,
                a.Y / la, b.Y / lb, c.Y / lc, 0,
                a.Z / la, b.Z / lb, c.Z / lc, 0,
                0, 0, 0, 1
            ]);
            Matrix principal = Transforms.Scale(
                12 / (mass * (lb * lb + lc * lc)), 12 / (mass * (la * la + lc * lc)), 12 / (mass * (la * la + lb * lb)));

            _turnless = axes * principal * axes.Transpose();
        }
    }

    // How hard it is to turn now, its axes turned with it.
    private Vector TurnedBy(Vector torque)
    {
        if (Corners is null)
            return _turnless * torque;

        Turn back = new (Turn.W, -Turn.X, -Turn.Y, -Turn.Z);

        return Turn.Rotate(_turnless * back.Rotate(torque));
    }

    /// <summary>
    /// This method returns how fast the place at a lever from its middle is moving.
    /// </summary>
    public Vector VelocityAt(Vector lever) => Velocity + Spin.Cross(lever);

    /// <summary>
    /// This method returns how much an impulse along a direction at a lever is spent turning it rather than moving
    /// it -- what is added to one over its mass to find how much the impulse moves the place.
    /// </summary>
    public double Turning(Vector lever, Vector direction) =>
        direction.Dot(TurnedBy(lever.Cross(direction)).Cross(lever));

    /// <summary>
    /// This method gives it an impulse at a lever from its middle.
    /// </summary>
    public void Apply(Vector impulse, Vector lever)
    {
        Velocity += impulse * InverseMass;

        if (Ties.Count == 0)
            Spin += TurnedBy(lever.Cross(impulse));
    }

    /// <summary>
    /// This property notes whether it has fallen asleep: still, until something strikes it or pulls it.
    /// </summary>
    public bool Asleep { get; private set; }

    // How long it has been nearly still.
    private double _still;

    // Falls asleep once its fastest point has gone slower than a speed for a time.
    internal void Drowse(double step, double speed, double time)
    {
        if (Asleep || InverseMass <= 0)
            return;

        if (Velocity.Magnitude + Spin.Magnitude * Radius >= speed)
        {
            _still = 0;

            return;
        }

        _still += step;

        if (_still < time)
            return;

        Asleep = true;
        Velocity = Still;
        Spin = Still;
    }

    // Wakes it, when something strikes it or pulls it.
    internal void Wake()
    {
        Asleep = false;
        _still = 0;
    }

    // Forgets what the scene did to its corners in the step before.
    internal void Unpress()
    {
        if (Corners is null)
            return;

        Array.Clear(Met);
        Array.Clear(Pressed);
        Array.Fill(Gripped, Still);
    }

    /// <summary>
    /// This method pushes it for a step -- gravity, and the air's drag on it, against the wind -- and moves it.
    /// </summary>
    internal void Push(Vector gravity, Vector wind, double step)
    {
        if (InverseMass <= 0 || Asleep)
            return;

        Vector air = Velocity - wind;
        Vector drag = air * (-_dragging * air.Magnitude);

        Velocity += (gravity + drag * InverseMass) * step;
        Position += Velocity * step;

        if (Ties.Count == 0)
            Turn = Turn.Stepped(Spin, step);
    }

    /// <summary>
    /// This method turns a body held by strings with them, so that what hangs from a string swings with it: the
    /// least turn that carries the first string's way at time nought onto its way now, and, for a body held by
    /// two, the second's too.
    /// </summary>
    internal void TurnWithItsTies()
    {
        if (Ties.Count == 0)
            return;

        Vector WayOf(Tie tie) => (Position - (tie.Other?.Position ?? tie.Pin)).Unit;

        Tie first = Ties[0];
        Turn turn = Turn.Between(first.StartingWay, WayOf(first));

        if (Ties.Count > 1)
        {
            // Then about the first string's way, to bring the second string's round to where it is.
            Vector axis = WayOf(first);
            Vector was = turn.Rotate(Ties[1].StartingWay);
            Vector now = WayOf(Ties[1]);
            Vector wasAcross = was - axis * was.Dot(axis), nowAcross = now - axis * now.Dot(axis);

            if (wasAcross.Magnitude > 1e-9 && nowAcross.Magnitude > 1e-9)
                turn = Turn.Between(wasAcross, nowAcross) * turn;
        }

        Turn = turn;
    }
}
