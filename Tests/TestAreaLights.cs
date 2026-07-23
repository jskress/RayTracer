using RayTracer.Basics;
using RayTracer.Core;

namespace Tests;

/// <summary>
/// These tests cover the area light's geometry: where across its face it is looked at from, and
/// that those places span the face as they should.  What the sampling does to a shadow -- the soft
/// edge that is the point of it -- is covered by rendering in <see cref="TestLightClauses"/>.
/// </summary>
[TestClass]
public class TestAreaLights
{
    /// <summary>
    /// An area light two units wide each way, centred at the origin, with the jitter turned off so
    /// the samples land on an exact grid the arithmetic can be checked against.
    /// </summary>
    private static AreaLight PlainGrid(int uSteps, int vSteps) => new ()
    {
        Location = Point.Zero,
        Axis1 = new Vector(2, 0, 0),
        Axis2 = new Vector(0, 0, 2),
        USteps = uSteps,
        VSteps = vSteps,
        Jitter = false
    };

    [TestMethod]
    public void TestTheGridHasASampleForEverySquare()
    {
        Assert.AreEqual(1, PlainGrid(1, 1).SampleCount);
        Assert.AreEqual(16, PlainGrid(4, 4).SampleCount);
        Assert.AreEqual(15, PlainGrid(5, 3).SampleCount);
    }

    [TestMethod]
    public void TestASingleSampleSitsAtTheCentre()
    {
        // A one-by-one area light is a point light at its centre, so from anywhere it is looked at
        // just as a lamp there would be.
        AreaLight light = PlainGrid(1, 1);
        Point from = new (0, -10, 0);

        LightSample sample = light.SampleToward(from, 0);

        Assert.IsTrue(sample.Direction.Matches(new Vector(0, 1, 0)), "toward the centre from below is up");
        Assert.AreEqual(10, sample.Distance, 1e-9);
        Assert.AreEqual(1, sample.Cone, 1e-9);
    }

    [TestMethod]
    public void TestTheSamplesSpanTheWholeFace()
    {
        // A two-wide grid along each axis puts its samples at the four corners of the face, half a
        // full axis either way of the centre.  The face is two units across each way, so the
        // corners sit at ±1.
        AreaLight light = PlainGrid(2, 2);
        List<Point> corners = [];

        // Read each sample's position back out by walking a unit along its direction from a known
        // far-off point -- simplest is to place the shaded point at the origin's far side and use
        // the distance, but cleaner here is to rebuild the point from direction and distance.
        Point from = new (0, -100, 0);

        for (int index = 0; index < light.SampleCount; index++)
        {
            LightSample sample = light.SampleToward(from, index);

            corners.Add(from + sample.Direction * sample.Distance);
        }

        // The four corners, in the grid's order.
        Assert.IsTrue(corners[0].Matches(new Point(-1, 0, -1)));
        Assert.IsTrue(corners[1].Matches(new Point(1, 0, -1)));
        Assert.IsTrue(corners[2].Matches(new Point(-1, 0, 1)));
        Assert.IsTrue(corners[3].Matches(new Point(1, 0, 1)));
    }

    [TestMethod]
    public void TestJitterMovesTheSamplesButKeepsThemNearTheirSquares()
    {
        // With jitter on, the samples are nudged off the grid, so they no longer land on the exact
        // corners -- but only within a grid square of them, never wildly.
        AreaLight jittered = new ()
        {
            Location = Point.Zero,
            Axis1 = new Vector(2, 0, 0),
            Axis2 = new Vector(0, 0, 2),
            USteps = 4,
            VSteps = 4,
            Jitter = true
        };
        AreaLight plain = PlainGrid(4, 4);
        Point from = new (0, -100, 0);
        bool anyMoved = false;

        for (int index = 0; index < 16; index++)
        {
            Point jitteredPoint = from + Sample(jittered, index);
            Point plainPoint = from + Sample(plain, index);
            double moved = (jitteredPoint - plainPoint).Magnitude;

            // A grid square here is 2/3 of a unit across; the nudge is at most half of one, so a
            // sample never strays even a full square from where the plain grid put it.
            Assert.IsTrue(moved < 2.0 / 3, $"sample {index} strayed {moved:F3}, further than a square");

            if (moved > 1e-6)
                anyMoved = true;
        }

        Assert.IsTrue(anyMoved, "jitter should actually move the samples");
    }

    [TestMethod]
    public void TestTheJitterIsTheSameEveryTime()
    {
        // The scatter is fixed once and reused, so a scene renders the same from one run to the
        // next -- which is why two lights built the same way sample the same places.
        AreaLight first = new ()
        {
            Location = Point.Zero, Axis1 = new Vector(2, 0, 0), Axis2 = new Vector(0, 0, 2),
            USteps = 4, VSteps = 4, Jitter = true, Seed = 3
        };
        AreaLight second = new ()
        {
            Location = Point.Zero, Axis1 = new Vector(2, 0, 0), Axis2 = new Vector(0, 0, 2),
            USteps = 4, VSteps = 4, Jitter = true, Seed = 3
        };
        Point from = new (0, -100, 0);

        for (int index = 0; index < 16; index++)
            Assert.IsTrue((Sample(first, index) - Sample(second, index)).Magnitude < 1e-12);
    }

    /// <summary>
    /// Rebuilds a sample's position, as an offset from the shaded point, from its direction and
    /// distance.
    /// </summary>
    private static Vector Sample(AreaLight light, int index)
    {
        LightSample sample = light.SampleToward(new Point(0, -100, 0), index);

        return sample.Direction * sample.Distance;
    }
}
