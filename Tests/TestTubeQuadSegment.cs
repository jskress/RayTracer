using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestTubeQuadSegment
{
    /// <summary>
    /// A quadratic Bezier whose control point sits exactly at the midpoint of start and end
    /// (with a matching midpoint radius) degenerates to a perfectly straight line -- the
    /// Bernstein-basis algebra collapses to plain linear interpolation.  So this configured
    /// segment must behave identically, hit for hit, to the already-proven
    /// <see cref="TubeSegment"/> built from the same start/end.
    /// </summary>
    [TestMethod]
    public void TestDegenerateControlPointMatchesLinearSegment()
    {
        TubeQuadSegment quad = new ()
        {
            Start = new Point(0, -10, 0), StartRadius = 2,
            Control = new Point(0, 0, 0), ControlRadius = 2,
            End = new Point(0, 10, 0), EndRadius = 2
        };
        TubeSegment linear = new ()
        {
            Start = new Point(0, -10, 0), StartRadius = 2,
            End = new Point(0, 10, 0), EndRadius = 2
        };
        Ray ray = new (new Point(5, 0, 0), Directions.Left);
        List<Intersection> quadIntersections = [];
        List<Intersection> linearIntersections = [];

        quad.PrepareForRendering();
        linear.PrepareForRendering();
        quad.AddIntersections(ray, quadIntersections);
        linear.AddIntersections(ray, linearIntersections);

        List<double> quadDistances = quadIntersections.Select(i => i.Distance).OrderBy(d => d).ToList();
        List<double> linearDistances = linearIntersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, quadDistances.Count);
        Assert.AreEqual(linearDistances.Count, quadDistances.Count);

        for (int index = 0; index < quadDistances.Count; index++)
            Assert.IsTrue(linearDistances[index].Near(quadDistances[index], 0.0001));
    }

    /// <summary>
    /// The same degenerate (straight-line) configuration, fired straight down the shared
    /// axis, must hit only the two end caps -- exactly matching
    /// <see cref="TestTubeSegment.TestEqualRadiiAlongAxisHitsOnlyTheEndCaps"/> -- confirming
    /// the quadratic segment's own cap accept/reject logic agrees with the linear segment's
    /// in the case where they describe the same solid.
    /// </summary>
    [TestMethod]
    public void TestDegenerateControlPointCapsMatchLinearSegment()
    {
        TubeQuadSegment quad = new ()
        {
            Start = new Point(0, 0, 0), StartRadius = 2,
            Control = new Point(0, 2, 0), ControlRadius = 2,
            End = new Point(0, 4, 0), EndRadius = 2
        };
        Ray ray = new (new Point(0, 10, 0), Directions.Down);
        List<Intersection> intersections = [];

        quad.PrepareForRendering();
        quad.AddIntersections(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue(4.0.Near(distances[0]));
        Assert.IsTrue(12.0.Near(distances[1]));
    }

    /// <summary>
    /// A genuinely curved, constant-radius "arch" (start and end level, control point
    /// raised above their midpoint), hit by a ray fired straight down through the peak.
    /// Because the control point sits directly above the start/end midpoint, the ray
    /// happens to pass exactly through the center of the u=0.5 cross-section sphere, so the
    /// expected hit distances reduce to that sphere's own near/far points -- entering the
    /// top of the tube and exiting the bottom.  Expected distances (3 and 5) were derived
    /// independently via a symbolic (sympy) resultant computation, not from this
    /// implementation.
    /// </summary>
    [TestMethod]
    public void TestCurvedArchLateralHitMatchesIndependentDerivation()
    {
        TubeQuadSegment segment = new ()
        {
            Start = new Point(0, 0, 0), StartRadius = 1,
            Control = new Point(2, 2, 0), ControlRadius = 1,
            End = new Point(4, 0, 0), EndRadius = 1
        };
        Ray ray = new (new Point(2, 5, 0), Directions.Down);
        List<Intersection> intersections = [];

        segment.PrepareForRendering();
        segment.AddIntersections(ray, intersections);

        List<double> distances = intersections.Select(i => i.Distance).OrderBy(d => d).ToList();

        Assert.AreEqual(2, distances.Count);
        Assert.IsTrue(3.0.Near(distances[0], 0.0001));
        Assert.IsTrue(5.0.Near(distances[1], 0.0001));
    }

    /// <summary>
    /// At the near (entering) hit on the arch above, the peak's cross-section sphere is
    /// centered at (2, 1, 0) (the quadratic Bezier's value at u=0.5), so the normal at the
    /// entry point (2, 2, 0) must point straight up, away from that center.
    /// </summary>
    [TestMethod]
    public void TestCurvedArchNormalIsRadialFromCrossSectionCenter()
    {
        TubeQuadSegment segment = new ()
        {
            Start = new Point(0, 0, 0), StartRadius = 1,
            Control = new Point(2, 2, 0), ControlRadius = 1,
            End = new Point(4, 0, 0), EndRadius = 1
        };
        Ray ray = new (new Point(2, 5, 0), Directions.Down);
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
        TubeQuadSegment segment = new ()
        {
            Start = new Point(0, 0, 0), StartRadius = 1,
            Control = new Point(2, 2, 0), ControlRadius = 1,
            End = new Point(4, 0, 0), EndRadius = 1
        };
        Ray ray = new (new Point(50, 50, 50), Directions.Up);
        List<Intersection> intersections = [];

        segment.PrepareForRendering();
        segment.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }
}
