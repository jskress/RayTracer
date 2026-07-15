using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestTubeCubicSegment
{
    /// <summary>
    /// A cubic Bezier whose two control points sit evenly spaced (at u = 1/3 and 2/3) along
    /// the line from start to end degenerates to a perfectly straight line -- the
    /// Bernstein-basis algebra collapses to plain linear interpolation, the same identity
    /// that makes a quadratic Bezier degenerate when its one control point sits at the
    /// midpoint.  So this configured segment must behave identically, hit for hit, to the
    /// already-proven <see cref="TubeSegment"/> built from the same start/end.
    /// </summary>
    [TestMethod]
    public void TestDegenerateControlPointsMatchLinearSegment()
    {
        TubeCubicSegment cubic = new ()
        {
            Start = new Point(0, -10, 0), StartRadius = 2,
            Control1 = new Point(0, -10.0 / 3, 0), Control1Radius = 2,
            Control2 = new Point(0, 10.0 / 3, 0), Control2Radius = 2,
            End = new Point(0, 10, 0), EndRadius = 2
        };
        TubeSegment linear = new ()
        {
            Start = new Point(0, -10, 0), StartRadius = 2,
            End = new Point(0, 10, 0), EndRadius = 2
        };
        Ray ray = new (new Point(5, 0, 0), Directions.Left);
        List<Intersection> cubicIntersections = [];
        List<Intersection> linearIntersections = [];

        cubic.PrepareForRendering();
        linear.PrepareForRendering();
        cubic.AddIntersections(ray, cubicIntersections);
        linear.AddIntersections(ray, linearIntersections);

        List<double> cubicDistances = cubicIntersections.Select(i => i.Distance).OrderBy(d => d).ToList();
        List<double> linearDistances = linearIntersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, cubicDistances.Count);
        Assert.AreEqual(linearDistances.Count, cubicDistances.Count);

        for (int index = 0; index < cubicDistances.Count; index++)
            Assert.IsTrue(linearDistances[index].Near(cubicDistances[index], 0.0001));
    }

    /// <summary>
    /// The same degenerate (straight-line) configuration, fired straight down the shared
    /// axis, must hit only the two end caps -- exactly matching
    /// <see cref="TestTubeSegment.TestEqualRadiiAlongAxisHitsOnlyTheEndCaps"/> -- confirming
    /// the cubic segment's own cap accept/reject logic agrees with the linear segment's in
    /// the case where they describe the same solid.
    /// </summary>
    [TestMethod]
    public void TestDegenerateControlPointsCapsMatchLinearSegment()
    {
        TubeCubicSegment cubic = new ()
        {
            Start = new Point(0, 0, 0), StartRadius = 2,
            Control1 = new Point(0, 4.0 / 3, 0), Control1Radius = 2,
            Control2 = new Point(0, 8.0 / 3, 0), Control2Radius = 2,
            End = new Point(0, 4, 0), EndRadius = 2
        };
        Ray ray = new (new Point(0, 10, 0), Directions.Down);
        List<Intersection> intersections = [];

        cubic.PrepareForRendering();
        cubic.AddIntersections(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue(4.0.Near(distances[0]));
        Assert.IsTrue(12.0.Near(distances[1]));
    }

    /// <summary>
    /// A genuinely curved, constant-radius symmetric "hump" (start and end level, both
    /// control points raised above and spread between them), hit by a ray fired straight
    /// down through the peak's x-center.  Because of the left/right symmetry, the ray
    /// happens to pass exactly through the center of the u=0.5 cross-section sphere, so the
    /// expected hit distances reduce to that sphere's own near/far points.  Expected
    /// distances (6.75 and 8.75) were derived independently via a symbolic (sympy)
    /// resultant computation, not from this implementation.
    /// </summary>
    [TestMethod]
    public void TestCurvedHumpLateralHitMatchesIndependentDerivation()
    {
        TubeCubicSegment segment = new ()
        {
            Start = new Point(0, 0, 0), StartRadius = 1,
            Control1 = new Point(2, 3, 0), Control1Radius = 1,
            Control2 = new Point(4, 3, 0), Control2Radius = 1,
            End = new Point(6, 0, 0), EndRadius = 1
        };
        Ray ray = new (new Point(3, 10, 0), Directions.Down);
        List<Intersection> intersections = [];

        segment.PrepareForRendering();
        segment.AddIntersections(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue(6.75.Near(distances[0], 0.0001));
        Assert.IsTrue(8.75.Near(distances[1], 0.0001));
    }

    /// <summary>
    /// At the near (entering) hit on the hump above, the peak's cross-section sphere is
    /// centered at (3, 2.25, 0) (the cubic Bezier's value at u=0.5), so the normal at the
    /// entry point (3, 3.25, 0) must point straight up, away from that center.
    /// </summary>
    [TestMethod]
    public void TestCurvedHumpNormalIsRadialFromCrossSectionCenter()
    {
        TubeCubicSegment segment = new ()
        {
            Start = new Point(0, 0, 0), StartRadius = 1,
            Control1 = new Point(2, 3, 0), Control1Radius = 1,
            Control2 = new Point(4, 3, 0), Control2Radius = 1,
            End = new Point(6, 0, 0), EndRadius = 1
        };
        Ray ray = new (new Point(3, 10, 0), Directions.Down);
        List<Intersection> intersections = [];

        segment.PrepareForRendering();
        segment.AddIntersections(ray, intersections);

        Intersection hit = intersections.OrderBy(i => i.Distance).First();
        Point point = ray.At(hit.Distance);
        Vector normal = segment.SurfaceNormaAt(point, hit);

        Assert.IsTrue(Directions.Up.Matches(normal.Unit));
    }

    /// <summary>
    /// A ray aimed entirely away from the segment must report no intersections.
    /// </summary>
    [TestMethod]
    public void TestMissesEverything()
    {
        TubeCubicSegment segment = new ()
        {
            Start = new Point(0, 0, 0), StartRadius = 1,
            Control1 = new Point(2, 3, 0), Control1Radius = 1,
            Control2 = new Point(4, 3, 0), Control2Radius = 1,
            End = new Point(6, 0, 0), EndRadius = 1
        };
        Ray ray = new (new Point(50, 50, 50), Directions.Up);
        List<Intersection> intersections = [];

        segment.PrepareForRendering();
        segment.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }
}
