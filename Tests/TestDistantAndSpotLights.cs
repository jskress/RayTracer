using RayTracer.Basics;
using RayTracer.Core;

namespace Tests;

/// <summary>
/// These tests cover the two lights that are not simply a lamp at a place: the distant light,
/// whose rays are parallel, and the spotlight, which shines only within a cone.
/// </summary>
[TestClass]
public class TestDistantAndSpotLights
{
    [TestMethod]
    public void TestADistantLightPointsTheSameWayEverywhere()
    {
        // The whole point of it: a lamp's direction swings as you move, the sun's does not.
        DistantLight light = new () { Direction = new Vector(0, -1, 0) };

        (Vector here, double hereDistance) = light.TowardFrom(new Point(0, 0, 0));
        (Vector there, double thereDistance) = light.TowardFrom(new Point(100, 5, -30));

        Assert.IsTrue(here.Matches(new Vector(0, 1, 0)), "toward a downward sun should be straight up");
        Assert.IsTrue(here.Matches(there), "a distant light should point the same way everywhere");
        Assert.IsTrue(double.IsPositiveInfinity(hereDistance), "a distant light has no reachable distance");
        Assert.IsTrue(double.IsPositiveInfinity(thereDistance));
    }

    [TestMethod]
    public void TestADistantLightIsFullStrengthEverywhere()
    {
        DistantLight light = new () { Direction = new Vector(1, -1, 0) };

        Assert.AreEqual(1, light.IntensityToward(new Point(3, 4, 5)), 1e-9);
    }

    /// <summary>
    /// A spotlight standing above the origin, aimed straight down, with a small flat core and a
    /// soft rim between fifteen and twenty-five degrees off the axis.
    /// </summary>
    private static Spotlight OverheadSpot() => new ()
    {
        Location = new Point(0, 10, 0),
        PointAt = new Point(0, 0, 0),
        Radius = 15,
        Falloff = 25
    };

    [TestMethod]
    public void TestASpotIsFullOnItsAxis()
    {
        // Straight below the light is straight down the axis: full strength.
        Assert.AreEqual(1, OverheadSpot().IntensityToward(new Point(0, 0, 0)), 1e-9);
    }

    [TestMethod]
    public void TestASpotIsFullWithinItsInnerCone()
    {
        // Ten degrees off the axis is inside the fifteen-degree core, so still full.
        double tenDegrees = 10 * Math.PI / 180;
        Point point = new (10 * Math.Tan(tenDegrees), 0, 0);

        Assert.AreEqual(1, OverheadSpot().IntensityToward(point), 1e-9);
    }

    [TestMethod]
    public void TestASpotIsDarkBeyondItsFalloff()
    {
        // Thirty degrees off the axis is past the twenty-five-degree falloff: nothing.
        double thirtyDegrees = 30 * Math.PI / 180;
        Point point = new (10 * Math.Tan(thirtyDegrees), 0, 0);

        Assert.AreEqual(0, OverheadSpot().IntensityToward(point), 1e-9);
    }

    [TestMethod]
    public void TestASpotEasesAcrossItsRim()
    {
        // Twenty degrees, halfway between the fifteen-degree core and the twenty-five-degree edge,
        // should be part-lit -- and the cubic ease puts it below the halfway strength, since the
        // curve is flat where it meets full and steep in the middle.
        double twentyDegrees = 20 * Math.PI / 180;
        Point point = new (10 * Math.Tan(twentyDegrees), 0, 0);
        double intensity = OverheadSpot().IntensityToward(point);

        Assert.IsTrue(intensity is > 0 and < 1, $"the rim should be part-lit, was {intensity}");
    }

    [TestMethod]
    public void TestASpotIsDarkBehindItself()
    {
        // A point above the light is behind the aim entirely.
        Assert.AreEqual(0, OverheadSpot().IntensityToward(new Point(0, 20, 0)), 1e-9);
    }

    [TestMethod]
    public void TestTightnessGathersTheLightTowardTheAxis()
    {
        // With tightness, a point inside the core is no longer at full strength; it is pulled down
        // by how far off the axis it lies, which is what concentrates the beam.
        double tenDegrees = 10 * Math.PI / 180;
        Point point = new (10 * Math.Tan(tenDegrees), 0, 0);
        Spotlight tight = new ()
        {
            Location = new Point(0, 10, 0),
            PointAt = new Point(0, 0, 0),
            Radius = 15,
            Falloff = 25,
            Tightness = 20
        };

        double intensity = tight.IntensityToward(point);

        Assert.IsTrue(intensity is > 0 and < 1,
            $"tightness should dim a point off the axis even within the core, was {intensity}");
    }

    [TestMethod]
    public void TestASpotIsStillAPointLightForDirectionAndDistance()
    {
        // It inherits where it stands, so it lies where a plain lamp there would.
        Spotlight spot = OverheadSpot();
        (Vector direction, double distance) = spot.TowardFrom(new Point(0, 0, 0));

        Assert.IsTrue(direction.Matches(new Vector(0, 1, 0)), "toward an overhead spot is straight up");
        Assert.AreEqual(10, distance, 1e-9);
    }
}
