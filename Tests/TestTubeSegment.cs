using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestTubeSegment
{
    /// <summary>
    /// A segment with equal radii at both ends is just a plain cylinder.  A ray fired
    /// straight at the axis, perpendicular to it and far from either cap, should cross the
    /// lateral surface at exactly the radius on either side of the axis.
    /// </summary>
    [TestMethod]
    public void TestEqualRadiiPerpendicularHitMatchesCylinder()
    {
        TubeSegment segment = new ()
        {
            Start = new Point(0, -10, 0), StartRadius = 2,
            End = new Point(0, 10, 0), EndRadius = 2
        };
        Ray ray = new (new Point(5, 0, 0), Directions.Left);
        List<Intersection> intersections = [];

        segment.PrepareForRendering();
        segment.AddIntersections(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue(3.0.Near(distances[0]));
        Assert.IsTrue(7.0.Near(distances[1]));
    }

    /// <summary>
    /// At a lateral hit on an equal-radii (cylindrical) segment, the normal must point
    /// straight away from the axis, exactly as a plain cylinder's would.
    /// </summary>
    [TestMethod]
    public void TestNormalForEqualRadiiLateralHitIsRadialFromAxis()
    {
        TubeSegment segment = new ()
        {
            Start = new Point(0, -10, 0), StartRadius = 2,
            End = new Point(0, 10, 0), EndRadius = 2
        };
        Ray ray = new (new Point(5, 0, 0), Directions.Left);
        List<Intersection> intersections = [];

        segment.PrepareForRendering();
        segment.AddIntersections(ray, intersections);

        Intersection hit = intersections.OrderBy(i => i.Distance).First();
        Point point = ray.At(hit.Distance);
        Vector normal = segment.SurfaceNormaAt(point, hit);

        Assert.IsTrue(new Vector(1, 0, 0).Matches(normal.Unit));
    }

    /// <summary>
    /// A ray fired straight down an equal-radii segment's own axis should pass through the
    /// body untouched and cross the surface exactly twice -- once through each end's
    /// hemispherical cap -- rather than registering spurious extra crossings at the
    /// body/cap boundary.
    /// </summary>
    [TestMethod]
    public void TestEqualRadiiAlongAxisHitsOnlyTheEndCaps()
    {
        TubeSegment segment = new ()
        {
            Start = new Point(0, 0, 0), StartRadius = 2,
            End = new Point(0, 4, 0), EndRadius = 2
        };
        Ray ray = new (new Point(0, 10, 0), Directions.Down);
        List<Intersection> intersections = [];

        segment.PrepareForRendering();
        segment.AddIntersections(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue(4.0.Near(distances[0]));
        Assert.IsTrue(12.0.Near(distances[1]));
    }

    /// <summary>
    /// A segment tapering from a zero radius at its start to a nonzero radius at its end is
    /// a genuine cone -- but not the naive one where the cross-sectional radius grows
    /// linearly with height.  Because each tangent point is pulled forward along the axis
    /// (toward the growing end) as well as outward, the envelope cone's half-angle theta
    /// satisfies sin(theta) = deltaRadius / axisLength, not tan(theta).  This test derives
    /// the expected cross-sectional radius independently from that relationship (rather than
    /// from the intersection code) and checks the ray crosses the lateral surface there.
    /// </summary>
    [TestMethod]
    public void TestTaperedConeLateralHitMatchesEnvelopeConeEquation()
    {
        const double axisLength = 10;
        const double deltaRadius = 2;
        const double y = 5;

        TubeSegment segment = new ()
        {
            Start = new Point(0, 0, 0), StartRadius = 0,
            End = new Point(0, axisLength, 0), EndRadius = deltaRadius
        };
        Ray ray = new (new Point(5, y, 0), Directions.Left);
        List<Intersection> intersections = [];

        segment.PrepareForRendering();
        segment.AddIntersections(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        // tan(theta)^2 = sin(theta)^2 / (1 - sin(theta)^2), and with
        // sin(theta) = deltaRadius / axisLength this simplifies to
        // deltaRadius^2 / (axisLength^2 - deltaRadius^2) -- exactly the K2 relationship the
        // implementation itself is built on, but derived here from the geometry directly.
        double tanSquaredTheta = deltaRadius * deltaRadius /
                                  (axisLength * axisLength - deltaRadius * deltaRadius);
        double expectedRadius = Math.Sqrt(y * y * tanSquaredTheta);

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue((5 - expectedRadius).Near(distances[0]));
        Assert.IsTrue((5 + expectedRadius).Near(distances[1]));
    }

    /// <summary>
    /// The cone's lateral surface, considered as an infinite double-napped cone, extends
    /// beyond the apex into a mirrored nappe on the other side.  A ray that would cross that
    /// mirrored nappe -- outside the segment's own [0, 1] parameter range -- must be
    /// rejected, since it isn't actually part of this segment's solid.
    /// </summary>
    [TestMethod]
    public void TestTaperedConeRejectsMirroredNappeBeyondApex()
    {
        TubeSegment segment = new ()
        {
            Start = new Point(0, 0, 0), StartRadius = 0,
            End = new Point(0, 10, 0), EndRadius = 2
        };
        Ray ray = new (new Point(5, -5, 0), Directions.Left);
        List<Intersection> intersections = [];

        segment.PrepareForRendering();
        segment.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    /// <summary>
    /// A ray aimed entirely away from the segment must report no intersections.
    /// </summary>
    [TestMethod]
    public void TestMissesEverything()
    {
        TubeSegment segment = new ()
        {
            Start = new Point(0, -2, 0), StartRadius = 1,
            End = new Point(0, 2, 0), EndRadius = 1
        };
        Ray ray = new (new Point(50, 50, 50), Directions.Up);
        List<Intersection> intersections = [];

        segment.PrepareForRendering();
        segment.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }
}
