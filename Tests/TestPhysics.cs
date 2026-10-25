using RayTracer.Basics;
using RayTracer.Core.Physics;
using RayTracer.Geometry;
using RayTracer.Graphics;
using RayTracer.ImageIO;
using RayTracer.Options;
using RayTracer.Parser;

namespace Tests;

/// <summary>
/// These tests hold the world's bodies to what physics says of them -- a fall, a bounce, a pendulum's period, the
/// speed the air lets a thing fall at, a cradle passing its strike along -- and cover the scene language that makes
/// bodies and swarms.
/// </summary>
[TestClass]
public class TestPhysics
{
    private static readonly Vector Down = new (0, -9.81, 0);
    private static readonly Vector Still = new (0, 0, 0);

    // The ground: the plane y = 0, met from above, facing up.
    private static (double Distance, Vector Normal)? Ground(Point from, Vector direction, double within)
    {
        if (direction.Y >= 0 || from.Y < 0)
            return null;

        double distance = from.Y / -direction.Y;

        return distance <= within ? (distance, new Vector(0, 1, 0)) : null;
    }

    private static (double Distance, Vector Normal)? Nothing(Point from, Vector direction, double within) => null;

    private static Body Ball(double bounce = 0.5, double drag = 0) => new () { Bounce = bounce, Drag = drag };

    /// <summary>
    /// A body let fall in no air falls as gravity says, half g t squared, and keeps the speed it was given across.
    /// </summary>
    [TestMethod]
    public void TestABodyFallsAsGravitySays()
    {
        World world = new (Down, Still, Nothing);
        Mover ball = world.Add(new Body { Drag = 0, Velocity = new Vector(2, 0, 0) }, new Point(0, 10, 0), 0.1, null);

        world.RunTo(1.2);

        Assert.AreEqual(10 - 0.5 * 9.81 * 1.2 * 1.2, ball.Position.Y, 0.01, "how far it fell");
        Assert.AreEqual(2.4, ball.Position.X, 1e-9, "how far it went across");
        Assert.AreEqual(-9.81 * 1.2, ball.Velocity.Y, 1e-9, "how fast it falls");
    }

    /// <summary>
    /// A ball bouncing on the ground keeps its bounce's share of its speed, so it comes back up to that share
    /// squared of the height it fell from; one that keeps none of it stays down.
    /// </summary>
    [TestMethod]
    public void TestABallBouncesToItsShareSquared()
    {
        World world = new (Down, Still, Ground);
        Mover ball = world.Add(Ball(bounce: 0.6), new Point(0, 2.1, 0), 0.1, null);
        double highest = 0;
        bool landed = false;

        for (double time = 0; time < 1.8; time += 0.002)
        {
            world.RunTo(time);

            if (ball.Velocity.Y > 0)
                landed = true;

            if (landed)
                highest = Math.Max(highest, ball.Position.Y - 0.1);
        }

        Assert.AreEqual(2.0 * 0.6 * 0.6, highest, 0.03, "the height of the first bounce");

        World dead = new (Down, Still, Ground);
        Mover lump = dead.Add(Ball(bounce: 0), new Point(0, 1.1, 0), 0.1, null);

        dead.RunTo(2);

        Assert.AreEqual(0.1, lump.Position.Y, 1e-3, "a ball that keeps nothing lies on the ground");
        Assert.AreEqual(0, lump.Velocity.Y, 0.02, "and is still");
    }

    /// <summary>
    /// A body tied to the very place it starts, its string given a length, falls until the string is taut and
    /// hangs there, rather than turning into nothing at all: it has no way to begin from, and is taken to hang down.
    /// </summary>
    [TestMethod]
    public void TestABodyStartingOnItsPinFallsToTheEndOfItsString()
    {
        World world = new (Down, Still, Nothing);
        Point pin = new (0, 2, 0);
        Mover bob = world.Add(Ball(), pin, 0.02, null);

        bob.Spec.Tethers.Add(new Tether(pin, null, 1));
        world.Tie(new Dictionary<string, Mover>());
        world.RunTo(3);

        Assert.IsTrue(double.IsFinite(bob.Position.Y), $"it should stand somewhere: {bob.Position}");
        Assert.AreEqual(1, (bob.Position - pin).Magnitude, 0.01, "hanging at the end of its string");
        Assert.IsTrue(double.IsFinite(bob.Turn.W), "and turned somehow");
    }

    /// <summary>
    /// A body turned by its ties is not turned by what strikes it, so none of an impulse is spent turning it: a knock
    /// off its middle moves the place struck as much as one at its middle would.  (Counted, an impulse at a lever
    /// was answered as though part went into a turn the ties never let it make.)
    /// </summary>
    [TestMethod]
    public void TestATiedBodySpendsNoImpulseTurning()
    {
        World world = new (Down, Still, Nothing);
        Point pin = new (0, 2, 0);
        Mover bob = world.Add(Ball(), new Point(0, 1, 0), 0.1, null);
        Mover free = world.Add(Ball(), new Point(3, 1, 0), 0.1, null);

        bob.Spec.Tethers.Add(new Tether(pin, null, 1));
        world.Tie(new Dictionary<string, Mover>());

        Vector lever = new (0.1, 0, 0), across = new (0, 0, 1);

        Assert.AreEqual(0, bob.Turning(lever, across), "a tied body turns with its ties alone");
        Assert.IsTrue(free.Turning(lever, across) > 0, "and a free one turns at a lever");
    }

    /// <summary>
    /// A ball on a slope stands on it, its middle its radius off the face, as on level ground: its looks reach the
    /// face wherever it lies, and its depth into it is measured square across the face, the way it is pushed out.
    /// (Its looks reaching its radius and measured along them, a ball on a slope of 30 degrees sank an eighth of its
    /// radius into it.)
    /// </summary>
    [TestMethod]
    public void TestABallOnASlopeStandsOnIt()
    {
        Vector up = new (Math.Sin(Math.PI / 6), Math.Cos(Math.PI / 6), 0);

        (double Distance, Vector Normal)? Slope(Point from, Vector direction, double within)
        {
            double toward = up.Dot(direction);
            double above = up.Dot(from - Point.Zero);

            if (toward >= 0)
                return null;

            double distance = -above / toward;

            return distance >= 0 && distance <= within ? (distance, up) : null;
        }

        World world = new (Down, Still, Slope);
        Mover ball = world.Add(new Body { Drag = 0, Friction = 0.6, Bounce = 0 }, Point.Zero + up * 0.1, 0.1, null);

        foreach (double time in new[] { 0.01, 0.05, 0.1, 0.3 })
        {
            world.RunTo(time);

            Assert.AreEqual(0.1, up.Dot(ball.Position - Point.Zero), 0.0005, $"its middle a radius off the slope at {time}");
        }

        // And held to it by its friction, it rolls rather than slides: a solid ball rolling down a slope gathers
        // speed at five sevenths of what a sliding one would, g sin 30 x 5/7.
        Assert.AreEqual(9.81 * 0.5 * 5 / 7 * 0.3, ball.Velocity.Magnitude, 0.03, "rolling down it");
    }

    /// <summary>
    /// A ball on a string swings as a pendulum does: back to where it began in two pi times the root of its length
    /// over gravity, the string never longer than it is.
    /// </summary>
    [TestMethod]
    public void TestAPendulumSwingsAtItsPeriod()
    {
        World world = new (Down, Still, Nothing);
        double length = 0.8;
        double angle = 5 * Math.PI / 180;
        Point pin = new (0, 2, 0);
        Point start = new (length * Math.Sin(angle), 2 - length * Math.Cos(angle), 0);
        Mover bob = world.Add(Ball(), start, 0.02, null);
        double period = 2 * Math.PI * Math.Sqrt(length / 9.81);
        double longest = 0;

        bob.Spec.Tethers.Add(new Tether(pin, null, null));
        world.Tie(new Dictionary<string, Mover>());

        List<(double Time, double X)> swing = [];

        for (double time = 0; time < period * 1.3; time += 0.001)
        {
            world.RunTo(time);
            swing.Add((time, bob.Position.X));
            longest = Math.Max(longest, (bob.Position - pin).Magnitude);
        }

        // The first time it comes back to the far side of its swing and turns, after half a period.
        double turned = swing.Skip(10).Zip(swing.Skip(11), (a, b) => (a, b)).Zip(swing.Skip(12), (ab, c) => (ab.a, ab.b, c))
            .First(three => three.b.X < three.a.X && three.b.X <= three.c.X).b.Time;

        Assert.AreEqual(period / 2, turned, period * 0.01, "half the period");
        Assert.AreEqual(length, longest, length * 1e-3, "the string holds its length");

        Vector facing = bob.Turn.Rotate((start - pin).Unit);

        Assert.IsTrue((facing - (bob.Position - pin).Unit).Magnitude < 1e-6, "it is turned to face along its string");
    }

    /// <summary>
    /// Falling through the air, a ball comes to the speed at which the air's drag holds its weight -- the root of
    /// two m g over the air's density, its drag coefficient and its area -- and a wind carries it with it.
    /// </summary>
    [TestMethod]
    public void TestTheAirHoldsAFallingBall()
    {
        World world = new (Down, Still, Nothing);
        double radius = 0.02;
        Mover ball = world.Add(new Body(), new Point(0, 1000, 0), radius, null);
        double mass = 4.0 / 3 * Math.PI * radius * radius * radius * Body.Density;
        double terminal = Math.Sqrt(2 * mass * 9.81 / (World.AirDensity * 0.47 * Math.PI * radius * radius));

        world.RunTo(30);

        Assert.AreEqual(-terminal, ball.Velocity.Y, terminal * 0.01, "the ball's terminal speed");

        World windy = new (Down, new Vector(5, 0, 0), Nothing);
        Mover blown = windy.Add(new Body(), new Point(0, 1000, 0), radius, null);

        windy.RunTo(30);

        Assert.AreEqual(5, blown.Velocity.X, 0.05, "carried along at the wind's speed");
    }

    /// <summary>
    /// Three balls hung in a row, the first swung out and let go: it strikes the second, the strike passes through,
    /// and the third swings out while the first stops -- a Newton's cradle, its balls keeping nearly all their
    /// speed in a strike.
    /// </summary>
    [TestMethod]
    public void TestACradlePassesItsStrikeAlong()
    {
        World world = new (Down, Still, Nothing);
        double radius = 0.0125, length = 0.14, gap = 0.0001;
        double angle = 30 * Math.PI / 180;
        List<Mover> balls = [];

        for (int ball = 0; ball < 3; ball++)
        {
            double pinX = (ball - 1) * (2 * radius + gap);
            Point pin = new (pinX, 0.3, 0);
            Point hanging = ball == 0
                ? new Point(pinX - length * Math.Sin(angle), 0.3 - length * Math.Cos(angle), 0)
                : new Point(pinX, 0.3 - length, 0);
            Mover mover = world.Add(new Body { Bounce = 0.98, Drag = 0 }, hanging, radius, null);

            mover.Spec.Tethers.Add(new Tether(pin, null, null));
            balls.Add(mover);
        }

        world.Tie(new Dictionary<string, Mover>());

        double highest = 0, firstAfter = 0;
        double quarter = Math.PI / 2 * Math.Sqrt(length / 9.81);

        for (double time = quarter + 0.01; time < quarter * 2.5; time += 0.001)
        {
            world.RunTo(time);
            highest = Math.Max(highest, balls[2].Position.Y - (0.3 - length));
            firstAfter = Math.Max(firstAfter, balls[0].Position.Y - (0.3 - length));
        }

        double first = length * (1 - Math.Cos(angle));

        Assert.IsTrue(highest > first * 0.85, $"the last ball rose {highest} of the first's {first}");
        Assert.IsTrue(firstAfter < first * 0.1, $"the first ball rose {firstAfter} after the strike");
    }

    /// <summary>
    /// Five balls hung touching in a row, the last let go: the strike passes through the three in the middle within
    /// the moment of the strike, so they hardly stir, and the first swings out -- the strike running against the
    /// order the balls were added in, so that nothing hangs on the order they are worked through.
    /// </summary>
    [TestMethod]
    public void TestACradlesMiddleBallsHardlyStir()
    {
        World world = new (Down, Still, Nothing);
        double radius = 0.0125, length = 0.14;
        double angle = 30 * Math.PI / 180;
        List<Mover> balls = [];

        for (int ball = 0; ball < 5; ball++)
        {
            double pinX = (ball - 2) * 2 * radius;
            Point pin = new (pinX, 0.3, 0);
            // The last let go, so that the strike runs against the order the balls were added in.
            Point hanging = ball == 4
                ? new Point(pinX + length * Math.Sin(angle), 0.3 - length * Math.Cos(angle), 0)
                : new Point(pinX, 0.3 - length, 0);
            Mover mover = world.Add(new Body { Bounce = 0.98, Drag = 0 }, hanging, radius, null);

            mover.Spec.Tethers.Add(new Tether(pin, null, null));
            balls.Add(mover);
        }

        world.Tie(new Dictionary<string, Mover>());

        double middle = 0, last = 0;
        double quarter = Math.PI / 2 * Math.Sqrt(length / 9.81);

        for (double time = quarter + 0.01; time < quarter * 2.5; time += 0.001)
        {
            world.RunTo(time);
            last = Math.Max(last, balls[0].Position.Y - (0.3 - length));

            for (int ball = 1; ball < 4; ball++)
                middle = Math.Max(middle, balls[ball].Position.Y - (0.3 - length));
        }

        double first = length * (1 - Math.Cos(angle));

        Assert.IsTrue(last > first * 0.85, $"the far ball rose {last} of the let-go one's {first}");
        Assert.IsTrue(middle < first * 0.01, $"a middle ball rose {middle} of the let-go one's {first}");
    }

    /// <summary>
    /// A ball shoved along the ground without turning slides, is gripped into turning, and rolls on without slipping:
    /// at five sevenths of the speed it was shoved at, turning at that over its radius.
    /// </summary>
    [TestMethod]
    public void TestAShovedBallComesToRoll()
    {
        World world = new (Down, Still, Ground);
        Mover ball = world.Add(new Body { Drag = 0, Friction = 0.5, Velocity = new Vector(2, 0, 0) }, new Point(0, 0.1, 0),
            0.1, null);

        world.RunTo(1);

        Assert.AreEqual(2 * 5.0 / 7, ball.Velocity.X, 0.02, "its speed rolling");
        Assert.AreEqual(2 * 5.0 / 7 / 0.1, -ball.Spin.Z, 0.2, "how fast it turns, rolling");
    }

    /// <summary>
    /// A box shoved along the ground on a face slides as Coulomb says, stopping after the speed squared over twice the
    /// friction and gravity, its grip no more than the friction times what the ground pushes it by; and one dropped
    /// flat comes back up to its bounce squared of the height, landing on all four lowest corners at once.
    /// </summary>
    [TestMethod]
    public void TestABoxSlidesAndBouncesAsItShould()
    {
        static Vector[] CornersOf(double half)
        {
            Vector[] corners = new Vector[8];

            for (int corner = 0; corner < 8; corner++)
            {
                corners[corner] = new Vector(
                    (corner & 1) == 0 ? -half : half, (corner & 2) == 0 ? -half : half, (corner & 4) == 0 ? -half : half);
            }

            return corners;
        }

        World sliding = new (Down, Still, Ground);
        Mover shoved = sliding.Add(
            new Body { Drag = 0, Friction = 0.3, Bounce = 0, Velocity = new Vector(2, 0, 0) }, new Point(0, 0.1, 0),
            Math.Sqrt(0.03), CornersOf(0.1));

        sliding.RunTo(2);

        Assert.AreEqual(2.0 * 2 / (2 * 0.3 * 9.81), shoved.Position.X, 0.03, "how far it slid");

        World dropping = new (Down, Still, Ground);
        Mover dropped = dropping.Add(
            new Body { Drag = 0, Bounce = 0.6 }, new Point(0, 1.1, 0), Math.Sqrt(0.03), CornersOf(0.1));
        double highest = 0;
        bool landed = false;

        for (double time = 0; time < 1.2; time += 0.002)
        {
            dropping.RunTo(time);
            landed |= dropped.Velocity.Y > 0;

            if (landed)
                highest = Math.Max(highest, dropped.Position.Y - 0.1);
        }

        Assert.AreEqual(0.6 * 0.6, highest, 0.03, "the height of its first bounce");
    }

    /// <summary>
    /// A ball lying asleep on the ground is woken when a string tied to it pulls: another ball, thrown away from it,
    /// draws the string taut.
    /// </summary>
    [TestMethod]
    public void TestASleepingBodyWakesWhenPulled()
    {
        World world = new (Down, Still, Ground);
        Mover lying = world.Add(new Body(), new Point(0, 0.1, 0), 0.1, null);

        world.RunTo(1);

        Assert.IsTrue(lying.Asleep, "lying still a second, it should be asleep");

        Body pulling = new () { Drag = 0, Velocity = new Vector(2, 0, 0) };

        pulling.Tethers.Add(new Tether(null, "lying", 0.5));

        world.Add(pulling, new Point(0.3, 0.1, 0), 0.1, null);
        world.Tie(new Dictionary<string, Mover> { ["lying"] = lying });
        world.RunTo(1.5);

        Assert.IsFalse(lying.Asleep, "pulled, it should wake");
    }

    /// <summary>
    /// A swarm that began before nought is in full flow at nought, exactly as one begun at nought is that much later:
    /// the same copies, in the same places.  One that begins later throws nothing until then.  It is also what found
    /// that a copy's flight stopped short once less than half a step was left: each copy was up to a millisecond
    /// behind, by as much as its birth fell, and the two swarms' differed by millimeters.  (In flight with nothing to
    /// strike: a copy meeting the ground stops for the rest of its step, so where its steps fall decides where it
    /// bounces to, and the two swarms' times differ in their last places.)
    /// </summary>
    [TestMethod]
    public void TestASwarmBegunBeforeNoughtIsAlreadyInFlow()
    {
        static List<Point> Places(Swarm swarm) => swarm.Surfaces.Select(copy => copy.Transform * Point.Zero).ToList();

        static Swarm Throwing(double since) => new ()
        {
            Thing = new Sphere(), Since = since, Spread = 0.3, SpeedFrom = 3, SpeedTo = 4, Rate = 40, LifeFrom = 0.8,
            LifeTo = 1.2, Radius = 0.01
        };

        Swarm early = Throwing(-5), plain = Throwing(0);

        early.Fill(Nothing, Down, Still, 0.3, 0, []);
        plain.Fill(Nothing, Down, Still, 5.3, 0, []);

        List<Point> now = Places(early), later = Places(plain);

        Assert.IsTrue(now.Count > 30, $"it should be in full flow, but holds {now.Count} copies");
        Assert.AreEqual(later.Count, now.Count, "as many copies as the plain swarm five seconds on");

        for (int copy = 0; copy < now.Count; copy++)
            Assert.IsTrue((now[copy] - later[copy]).Magnitude < 1e-9, $"copy {copy} stands {(now[copy] - later[copy]).Magnitude} from the plain one's place");

        // Thrown straight up with nothing to pull them down, a copy's height tells its age: some outlive the
        // shortest life, so the copies looked at reach back as far as the longest.
        Swarm rising = new ()
        {
            Thing = new Sphere(), SpeedFrom = 1, SpeedTo = 1, Rate = 100, LifeFrom = 0.5, LifeTo = 1.5, Drag = 0
        };

        rising.Fill(Nothing, Still, Still, 3, 0, []);

        Assert.IsTrue(Places(rising).Max(place => place.Y) > 1.2, "the oldest copy alive should be past 1.2 seconds");

        Swarm late = Throwing(1);

        late.Fill(Nothing, Down, Still, 0.5, 0, []);

        Assert.AreEqual(0, late.Surfaces.Count, "a swarm begun at one second throws nothing before it");
    }

    /// <summary>
    /// A ball lying still on the ground falls asleep, and wakes when another ball rolls into it: struck, it moves off.
    /// </summary>
    [TestMethod]
    public void TestASleepingBodyWakesWhenStruck()
    {
        World world = new (Down, Still, Ground);
        Mover lying = world.Add(Ball(), new Point(0, 0.1, 0), 0.1, null);

        world.RunTo(1);

        Assert.IsTrue(lying.Asleep, "lying still a second, it should be asleep");

        Mover rolling = world.Add(new Body { Drag = 0, Velocity = new Vector(1, 0, 0) }, new Point(-0.5, 0.1, 0), 0.1, null);

        world.RunTo(1.6);

        Assert.IsFalse(lying.Asleep, "struck, it should wake");
        Assert.IsTrue(lying.Position.X > 0.05, $"struck, it should move off, but it is at {lying.Position.X}");
        Assert.IsTrue(rolling.Position.X < lying.Position.X, "the one that struck it stays behind it");
    }

    /// <summary>
    /// Two balls meeting off their middles grip each other as they slide past, and are set turning: without the
    /// grip, a strike through the middles could turn neither.  Between them they keep the speed they had.
    /// </summary>
    [TestMethod]
    public void TestAGlancingStrikeSetsBallsTurning()
    {
        World world = new (Still, Still, Nothing);
        Mover moving = world.Add(new Body { Drag = 0, Velocity = new Vector(2, 0, 0) }, new Point(0, 0, 0), 0.1, null);
        Mover struck = world.Add(new Body { Drag = 0 }, new Point(0.5, 0.1, 0), 0.1, null);

        world.RunTo(1);

        Assert.IsTrue(moving.Spin.Magnitude > 0.5, $"the striking ball turns at {moving.Spin.Magnitude}");
        Assert.IsTrue(struck.Spin.Magnitude > 0.5, $"the struck ball turns at {struck.Spin.Magnitude}");
        Assert.IsTrue((moving.Velocity + struck.Velocity - new Vector(2, 0, 0)).Magnitude < 1e-9, "they keep their momentum");
    }

    /// <summary>
    /// A box thrown turning onto the ground lands, tumbles and comes to rest lying on a face: its lowest corners on
    /// the ground, itself still and no longer turning, and staying so -- at the size of a crate and the size of a die.
    /// Held by its deepest corner alone, a resting box rocked from corner to corner and crept, which a test asking
    /// only that it be slower than five centimeters a second let through.
    /// </summary>
    [TestMethod]
    public void TestABoxComesToRestOnTheGround()
    {
        foreach (double half in new[] { 0.1, 0.008 })
        {
            World world = new (Down, Still, Ground);
            Vector[] corners = new Vector[8];

            for (int corner = 0; corner < 8; corner++)
            {
                corners[corner] = new Vector(
                    (corner & 1) == 0 ? -half : half, (corner & 2) == 0 ? -half : half, (corner & 4) == 0 ? -half : half);
            }

            Mover box = world.Add(
                new Body
                {
                    Bounce = 0.45, Friction = 0.3, Spin = new Vector(5, 2, -12), Velocity = new Vector(1.1, 0.5, 0.1)
                },
                new Point(0, 0.1 + half, 0), half * Math.Sqrt(3), corners);

            world.RunTo(3);

            Point then = box.Position;
            Vector facing = box.Turn.Rotate(new Vector(1, 0, 0));

            world.RunTo(3.5);

            double lowest = corners.Min(corner => box.Position.Y + box.Turn.Rotate(corner).Y);

            Assert.AreEqual(0, lowest, half * 1e-3, $"its lowest corner lies on the ground ({half})");
            // Lying on an edge, its middle would be forty percent higher.
            Assert.AreEqual(half, box.Position.Y, half * 5e-3, $"it lies on a face ({half})");
            Assert.IsTrue(box.Velocity.Magnitude < 1e-3, $"it is still moving at {box.Velocity.Magnitude} ({half})");
            Assert.IsTrue(box.Spin.Magnitude < 1e-2, $"it is still turning at {box.Spin.Magnitude} ({half})");
            Assert.IsTrue((box.Position - then).Magnitude < half * 1e-3, $"it crept {(box.Position - then).Magnitude} ({half})");
            Assert.IsTrue((box.Turn.Rotate(new Vector(1, 0, 0)) - facing).Magnitude < 1e-3, $"it turned as it lay ({half})");
        }
    }

    /// <summary>
    /// A swarm's copies fly as thrown balls do: thrown straight up, each is where gravity says for as long as it has
    /// flown; only those whose life is not up are there; thrown within a spread, none goes further from the way than
    /// it; and let fall onto the ground, a copy comes back up to its bounce squared of the height.
    /// </summary>
    [TestMethod]
    public void TestASwarmsCopiesFlyAsThrownBallsDo()
    {
        static List<Point> Places(Swarm swarm) => swarm.Surfaces.Select(copy => copy.Transform * Point.Zero).ToList();

        Swarm up = new ()
        {
            Thing = new Sphere(), SpeedFrom = 5, SpeedTo = 5, Rate = 10, LifeFrom = 10, LifeTo = 10, Drag = 0, Radius = 0.01
        };

        up.Fill(Nothing, Down, Still, 0.5, 0, []);

        List<double> heights = Places(up).Select(place => place.Y).Order().ToList();

        Assert.AreEqual(6, heights.Count, "one thrown every tenth of a second from time nought");

        for (int copy = 0; copy < 6; copy++)
        {
            double flown = copy * 0.1;

            Assert.AreEqual(5 * flown - 0.5 * 9.81 * flown * flown, heights[copy], 0.01, $"after {flown} s");
        }

        up.LifeFrom = up.LifeTo = 0.25;
        up.Fill(Nothing, Down, Still, 0.5, 0, []);

        Assert.AreEqual(3, up.Surfaces.Count, "only those thrown in the last quarter second");

        Swarm spread = new ()
        {
            Thing = new Sphere(), Spread = 20 * Math.PI / 180, Rate = 1000, LifeFrom = 10, LifeTo = 10, Drag = 0
        };

        spread.Fill(Nothing, Still, Still, 0.1, 0, []);

        List<double> angles = Places(spread)
            .Select(place => new Vector(place.X, place.Y, place.Z))
            .Where(away => away.Magnitude > 1e-9)
            .Select(away => Math.Acos(away.Y / away.Magnitude) * 180 / Math.PI)
            .ToList();

        Assert.IsTrue(angles.Max() <= 20 + 1e-6, $"one was thrown {angles.Max()} degrees from the way");
        Assert.IsTrue(angles.Max() > 18, "the spread should be filled to its edge");

        Swarm dropped = new ()
        {
            Thing = new Sphere(), From = new Point(0, 1.01, 0), SpeedFrom = 0, SpeedTo = 0, Rate = 0.1,
            LifeFrom = 10, LifeTo = 10, Bounce = 0.5, Drag = 0, Radius = 0.01
        };
        double highest = 0, lowest = 1;
        bool landed = false;

        for (double time = 0; time < 1; time += 0.01)
        {
            dropped.Fill(Ground, Down, Still, time, 0, []);

            double height = Places(dropped)[0].Y - 0.01;

            landed |= height < 0.01;
            lowest = Math.Min(lowest, height);

            if (landed)
                highest = Math.Max(highest, height);
        }

        Assert.AreEqual(0.5 * 0.5, highest, 0.02, "the height of its first bounce");
        Assert.IsTrue(lowest > -1e-9, $"it sank {-lowest} into the ground");

        Swarm falling = new ()
        {
            Thing = new Sphere(), SpeedFrom = 0, SpeedTo = 0, Rate = 0.01, LifeFrom = 100, LifeTo = 100, Radius = 0.01
        };
        double mass = 4.0 / 3 * Math.PI * 1e-6 * Body.Density;
        double terminal = Math.Sqrt(2 * mass * 9.81 / (World.AirDensity * 0.47 * Math.PI * 1e-4));

        falling.Fill(Nothing, Down, Still, 10, 0, []);

        double then = Places(falling)[0].Y;

        falling.Fill(Nothing, Down, Still, 10.1, 0, []);

        Assert.AreEqual(terminal, (then - Places(falling)[0].Y) / 0.1, terminal * 0.01, "a copy's terminal speed");
    }

    /// <summary>
    /// A swarm thrown from within a shape sets each copy off from a place of its own inside it, over the whole of it;
    /// a copy passes what lies inside the shape with it -- here a shelf through the middle of the ball -- and meets
    /// the scene once it has left, coming down onto the ground under it.
    /// </summary>
    [TestMethod]
    public void TestASwarmThrowsFromWithinAShape()
    {
        Sphere room = new () { Transform = Transforms.Translate(0, 5, 0) * Transforms.Scale(2, 2, 2) };
        Swarm held = new ()
        {
            Thing = new Sphere { Transform = Transforms.Scale(0.01, 0.01, 0.01) }, Within = room, SpeedFrom = 0,
            SpeedTo = 0, Rate = 2000, LifeFrom = 10, LifeTo = 10, Drag = 0
        };

        held.Fill(Nothing, Still, Still, 0.5, 0, []);

        List<double> away = held.Surfaces.Select(copy => (copy.Transform * Point.Zero - new Point(0, 5, 0)).Magnitude).ToList();

        Assert.AreEqual(1001, away.Count);
        Assert.IsTrue(away.Max() <= 2, $"one started {away.Max()} from the middle of a ball of two");
        Assert.IsTrue(away.Max() > 1.9 && away.Min() < 0.3, "they should start all through the ball");

        // A shelf at the ball's middle, met from above, and the ground.
        static (double Distance, Vector Normal)? Shelved(Point from, Vector direction, double within)
        {
            if (direction.Y < 0 && from.Y > 5 && (from.Y - 5) / -direction.Y <= within)
                return ((from.Y - 5) / -direction.Y, new Vector(0, 1, 0));

            return Ground(from, direction, within);
        }

        Swarm falling = new ()
        {
            Thing = new Sphere { Transform = Transforms.Scale(0.01, 0.01, 0.01) }, Within = room, SpeedFrom = 0,
            SpeedTo = 0, Rate = 20, LifeFrom = 100, LifeTo = 100, Bounce = 0, Drag = 0
        };

        falling.Fill(Shelved, Down, Still, 3, 0, []);

        List<Point> landed = falling.Surfaces.Take(20).Select(copy => copy.Transform * Point.Zero).ToList();

        Assert.IsTrue(landed.All(place => Math.Abs(place.Y - 0.01) < 1e-6),
            $"every copy should lie on the ground, not the shelf: {string.Join(", ", landed.Select(place => place.Y))}");
    }

    /// <summary>
    /// A copy given a speed it falls at comes down at that speed once it has gathered it, whatever its size; and one
    /// that spins turns at its rate, about a way of its own.
    /// </summary>
    [TestMethod]
    public void TestACopyFallsAndSpinsAsSaid()
    {
        Swarm falling = new ()
        {
            Thing = new Sphere { Transform = Transforms.Scale(0.05, 0.05, 0.05) }, SpeedFrom = 0, SpeedTo = 0,
            Rate = 0.01, LifeFrom = 100, LifeTo = 100, FallsFrom = 1.5
        };

        falling.Fill(Nothing, Down, Still, 10, 0, []);

        double then = (falling.Surfaces[0].Transform * Point.Zero).Y;

        falling.Fill(Nothing, Down, Still, 10.1, 0, []);

        Assert.AreEqual(1.5, (then - (falling.Surfaces[0].Transform * Point.Zero).Y) / 0.1, 1e-3, "its falling speed");

        Swarm spinning = new ()
        {
            Thing = new Cube { Transform = Transforms.Scale(0.04, 0.03, 0.002) }, SpeedFrom = 0, SpeedTo = 0,
            Rate = 0.01, LifeFrom = 100, LifeTo = 100, SpinFrom = 2, SpinTo = 2
        };

        spinning.Fill(Nothing, Still, Still, 1, 0, []);

        Matrix before = spinning.Surfaces[0].Transform;

        spinning.Fill(Nothing, Still, Still, 1.25, 0, []);

        Matrix turned = spinning.Surfaces[0].Transform * before.Invert();
        double trace = Diagonal(turned);

        Assert.AreEqual(2 * 0.25, Math.Acos(Math.Clamp((trace - 1) / 2, -1, 1)), 1e-9, "how far it turned in a quarter second");

        // With the shutter open, it turns on through the frame as well.
        spinning.Fill(Nothing, Still, Still, 1, 0.1, [0, 1]);

        Matrix through = spinning.Surfaces[0].MotionAt(1);

        Assert.AreEqual(2 * 0.1, Math.Acos(Math.Clamp((Diagonal(through) - 1) / 2, -1, 1)), 1e-9, "how far it turns through a frame");
    }

    // The sum of a turn's diagonal, from what it does to the three axes.
    private static double Diagonal(Matrix turn) =>
        (turn * new Vector(1, 0, 0)).X + (turn * new Vector(0, 1, 0)).Y + (turn * new Vector(0, 0, 1)).Z;

    /// <summary>
    /// A copy meets things at its own size as it is placed, its own transforms and all: a ball scaled to a
    /// centimeter comes to rest with its middle a centimeter off the ground, not the unit it is in its own space.
    /// </summary>
    [TestMethod]
    public void TestACopyMeetsThingsAtItsOwnSize()
    {
        Swarm dropped = new ()
        {
            Thing = new Sphere { Transform = Transforms.Scale(0.01, 0.01, 0.01) }, From = new Point(0, 1.5, 0),
            SpeedFrom = 0, SpeedTo = 0, Rate = 0.1, LifeFrom = 10, LifeTo = 10, Bounce = 0, Drag = 0
        };

        dropped.Fill(Ground, Down, Still, 2, 0, []);

        Assert.AreEqual(0.01, (dropped.Surfaces[0].Transform * Point.Zero).Y, 1e-6, "its middle, lying on the ground");
    }

    /// <summary>
    /// A tumbling copy turns as it falls and, once it has come to rest, lies on its broadest side on what it landed
    /// on: a thin card dropped spinning ends flat on the ground, its middle half its thickness up, and stays so.
    /// </summary>
    [TestMethod]
    public void TestATumblingCopyComesToRestLyingFlat()
    {
        Swarm dropped = new ()
        {
            Thing = new Cube { Transform = Transforms.Scale(0.04, 0.03, 0.002) }, From = new Point(0, 1, 0),
            SpeedFrom = 0, SpeedTo = 0, Rate = 0.1, LifeFrom = 100, LifeTo = 100, Bounce = 0, Friction = 0.8,
            SpinFrom = 4, SpinTo = 4, FallsFrom = 1.2
        };
        Matrix placed = null;

        foreach (double time in new[] { 0.3, 3, 3.5 })
        {
            dropped.Fill(Ground, Down, Still, time, 0, []);

            Matrix now = dropped.Surfaces[0].Transform;
            Point middle = now * Point.Zero;
            Vector thin = (now * new Vector(0, 0, 1)).Unit;

            if (time < 1)
            {
                Assert.IsTrue(middle.Y > 0.3, $"still falling at {time} s, at {middle.Y}");
                continue;
            }

            Assert.AreEqual(0.002, middle.Y, 1e-6, $"its middle at {time} s");
            Assert.AreEqual(1, Math.Abs(thin.Y), 1e-9, $"its thin side up at {time} s");

            if (placed is not null)
                Assert.IsTrue(Same(placed, now), "it lies still once at rest");

            placed = now;
        }
    }

    private static bool Same(Matrix a, Matrix b)
    {
        Point[] corners = [Point.Zero, new (1, 0, 0), new (0, 1, 0), new (0, 0, 1)];

        return corners.All(corner => ((a * corner) - (b * corner)).Magnitude < 1e-12);
    }

    /// <summary>
    /// Renders a small scene with the given options and hands back its picture, or what stopped it.
    /// </summary>
    private static (Canvas Picture, string Error) Rendered(string directory, string body, long? frame = null,
        string output = "scene.png", string camera = "camera { location [0, 2, -6]  look at [0, 1, 0] }")
    {
        string scene = Path.Combine(directory, "scene.igl");
        string written = Path.Combine(directory, output);
        StringWriter captured = new ();
        TextWriter was = Console.Out;

        File.WriteAllText(scene,
            "context { angles are degrees  no gamma  animation length 1  rate 10 }\n" +
            camera + "\n" +
            "point light { location [-4, 9, -9] }\n" + body);
        Console.SetOut(captured);

        try
        {
            new LanguageParser(scene).Parse()?.Render(new RenderOptions
            {
                OutputFileName = written, Width = 32, Height = 24, Frame = frame ?? 0
            });
        }
        catch (Exception exception)
        {
            return (null, exception.Message);
        }
        finally
        {
            Console.SetOut(was);
        }

        return captured.ToString().Contains("Error")
            ? (null, captured.ToString())
            : (new ImageFile(written).Load()[0], null);
    }

    private static bool Same(Canvas one, Canvas other) =>
        Enumerable.Range(0, one.Width * one.Height)
            .All(index => one.GetPixel(index % one.Width, index / one.Width)
                .Matches(other.GetPixel(index % one.Width, index / one.Width)));

    /// <summary>
    /// The scene language: a body with every property said, tied to a pin and to another body by name; a box; a
    /// swarm; gravity and a wind -- each frame the same however often it is rendered -- and what cannot be moved
    /// refused.
    /// </summary>
    [TestMethod]
    public void TestTheSceneLanguageMovesBodies()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"physics-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            const string world = """
                context { gravity [0, -9.81, 0]  wind [1, 0, 0] }
                plane { }
                sphere {
                    named 'first'  scale 0.2  translate [-1, 2, 0]
                    body { mass 0.5  velocity [1, 0, 0]  spin [0, 90, 0]  bounce 0.6  friction 0.4  drag 1  radius 0.2 }
                }
                sphere {
                    scale 0.15  translate [0, 1.5, 0]
                    body { tether to [0, 2.5, 0] length 1  tether to 'first' }
                }
                cube { scale 0.1  translate [1, 1, 0]  body { shape box  bounce 0.2 } }
                Spark = sphere { scale 0.02 }
                swarm { of Spark  from [0, 0.1, 0]  toward [0, 1, 0]  spread 20  speed 2 to 3  rate 30  life 0.5 to 1
                        since -1  bounce 0.3  friction 0.5  drag 1  radius 0.02  with seed 3 }
                Leaf = cube { scale [0.03, 0.02, 0.001] }
                Room = sphere { scale 0.5  translate Y 2 }
                swarm { of Leaf  within Room  speed 0  rate 20  life 5  since -2  spin 30 to 90  falls at 1 to 2  with seed 4 }
                """;

            (Canvas later, string error) = Rendered(directory, world, frame: 6);

            Assert.IsNull(error);

            (Canvas again, _) = Rendered(directory, world, frame: 6);
            (Canvas first, _) = Rendered(directory, world, frame: 0);

            Assert.IsTrue(Same(later, again), "a frame should be the same however often it is rendered");
            Assert.IsFalse(Same(later, first), "the bodies should have moved");

            StringAssert.Contains(Rendered(directory, "group { sphere { body { } } }").Error, "top of the scene");
            StringAssert.Contains(Rendered(directory, "union { sphere { body { } } cube { } }").Error, "top of the scene");
            StringAssert.Contains(
                Rendered(directory, "Spark = sphere { }  group { swarm { of Spark } }").Error, "A swarm must stand");
            StringAssert.Contains(Rendered(directory, "sphere { body { tether to 'nobody' } }").Error, "no body has that name");
            StringAssert.Contains(Rendered(directory, "sphere { body { tether to 3 } }").Error, "tied to a place");
            StringAssert.Contains(Rendered(directory, "sphere { body { bounce 2 } }").Error, "nothing to one");
            StringAssert.Contains(Rendered(directory, "sphere { body { mass 0 } }").Error, "more than nothing");
            StringAssert.Contains(Rendered(directory, "sphere { body { tether to [0, 2, 0] length 0 } }").Error,
                "longer than nothing");
            StringAssert.Contains(Rendered(directory, "swarm { rate 10 }").Error, "needs something to throw");
            StringAssert.Contains(Rendered(directory,
                "Drop = sphere { scale 0.01 }  swarm { of Drop  rate 10  motion { translate X 1 } }").Error,
                "must stand still while it throws");
            StringAssert.Contains(Rendered(directory,
                "Ember = sphere { scale 0.01  material { transparency 1  interior { medium { emission 2 } } }  gives light }  " +
                "swarm { of Ember }").Error, "cannot be shared");
            StringAssert.Contains(Rendered(directory,
                "Drop = sphere { scale 0.01 }  primitive Fountain() -> group { return group { cylinder { }  swarm { of Drop } } }  " +
                "object Fountain()").Error, "A swarm must stand");
            StringAssert.Contains(Rendered(directory,
                "primitive Dropped() -> group { return group { sphere { body { } } } }  object Dropped()").Error,
                "A body must stand");
            StringAssert.Contains(Rendered(directory,
                "sphere { named 'twin'  translate X -1  body { } }  sphere { named 'twin'  translate X 1  body { } }").Error,
                "Two bodies are named 'twin'");
            StringAssert.Contains(Rendered(directory,
                "Leaf = cube { scale [0.03, 0.02, 0.001] }  Room = sphere { translate Y 3 }  " +
                "swarm { of Leaf  within Room  from [0, 1, 0] }").Error, "not both");
            StringAssert.Contains(Rendered(directory,
                "Leaf = cube { scale 0.01 }  Room = plane { }  swarm { of Leaf  within Room }").Error, "no inside");
            StringAssert.Contains(Rendered(directory,
                "Leaf = cube { scale 0.01 }  Room = disc { center [0, 3, 0]  normal [0, 1, 0]  radius 2 }  swarm { of Leaf  within Room }").Error,
                "must be a closed surface");
            StringAssert.Contains(Rendered(directory,
                "Leaf = cube { scale 0.01 }  swarm { of Leaf  spin -10 }").Error, "less than nothing");
            StringAssert.Contains(Rendered(directory,
                "Leaf = cube { scale 0.01 }  swarm { of Leaf  falls at 0 }").Error, "more than nothing");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    /// <summary>
    /// What the world works out reaches the picture: a spinning body is turned, a falling one is blurred when the
    /// shutter is open, a light one is carried by the wind, and one dropped on the ground stays on it rather than
    /// falling through; and a swarm's copies are carried by the wind as well.
    /// </summary>
    [TestMethod]
    public void TestTheWorldsWorkReachesThePicture()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"physics-tests-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            const string seen = "camera { location [0, 2, -6]  look at [0, 1, 0] }";

            void Differ(string why, string one, string other, string camera = seen, string otherCamera = seen)
            {
                (Canvas first, string error) = Rendered(directory, one, frame: 5, camera: camera);
                (Canvas second, string otherError) = Rendered(directory, other, frame: 5, camera: otherCamera);

                Assert.IsNull(error ?? otherError);
                Assert.IsFalse(Same(first, second), why);
            }

            const string blown = "context { wind [20, 0, 0] }\n";
            const string light = "sphere { scale 0.3  translate [0, 1.5, 0]  body { mass 0.01 } }";
            const string sparks = "Spark = sphere { scale 0.3 }\n" +
                                  "swarm { of Spark  from [0, 1.5, 0]  speed 0  rate 10  life 10  radius 0.01 }";
            const string falling = "sphere { scale 0.3  translate [0, 1.5, 0]  body { drag 0  velocity [3, 0, 0] } }";
            const string dropped = "plane { }\nsphere { scale 0.3  translate [0, 0.6, 0]  body { } }";

            Differ("a spinning body should be turned",
                "cube { scale 0.4  translate [0, 1.5, 0]  body { drag 0  spin [0, 0, 300] } }",
                "cube { scale 0.4  translate [0, 1.5, 0]  body { drag 0 } }");
            // Against a shutter barely open, so that what differs is the motion and not the sampling.
            Differ("a moving body should be blurred", falling, falling,
                "camera { location [0, 2, -6]  look at [0, 1, 0]  shutter 1  blur samples 8 }",
                "camera { location [0, 2, -6]  look at [0, 1, 0]  shutter 0.001  blur samples 8 }");
            Differ("the wind should carry a light body", blown + light, light);
            Differ("the wind should carry a swarm's copies", blown + sparks, sparks);
            Differ("the ground should hold a body up", dropped, "plane { }");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
