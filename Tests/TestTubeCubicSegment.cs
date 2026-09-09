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
        Vector normal = segment.SurfaceNormalAt(point, hit);

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

    /// <summary>
    /// Builds a cubic segment of one constant radius, scaled bodily by the given factor.
    /// </summary>
    private static TubeCubicSegment Segment(
        Point start, Point control1, Point control2, Point end, double radius, double scale = 1)
    {
        Point Scaled(Point point) => new (point.X * scale, point.Y * scale, point.Z * scale);

        TubeCubicSegment segment = new ()
        {
            Start = Scaled(start), StartRadius = radius * scale,
            Control1 = Scaled(control1), Control1Radius = radius * scale,
            Control2 = Scaled(control2), Control2Radius = radius * scale,
            End = Scaled(end), EndRadius = radius * scale
        };

        segment.PrepareForRendering();

        return segment;
    }

    /// <summary>
    /// Returns where the segment's own curve is at the given curve parameter.
    /// </summary>
    private static Point CurveAt(TubeCubicSegment segment, double u)
    {
        double v = 1 - u;

        return new Point(
            v * v * v * segment.Start.X + 3 * v * v * u * segment.Control1.X +
            3 * v * u * u * segment.Control2.X + u * u * u * segment.End.X,
            v * v * v * segment.Start.Y + 3 * v * v * u * segment.Control1.Y +
            3 * v * u * u * segment.Control2.Y + u * u * u * segment.End.Y,
            v * v * v * segment.Start.Z + 3 * v * v * u * segment.Control1.Z +
            3 * v * u * u * segment.Control2.Z + u * u * u * segment.End.Z);
    }

    /// <summary>
    /// Fires a ray clean through the middle of the segment, square to the plane its curve
    /// lies in, and returns what it hit.  Aiming at the middle matters: a segment that has
    /// lost its curved body still has both of its end spheres, so "did anything at all
    /// render" would pass on the very failure being tested for.
    /// </summary>
    private static List<Intersection> AcrossTheMiddle(TubeCubicSegment segment, double scale = 1)
    {
        Point middle = CurveAt(segment, 0.5);
        Ray ray = new (new Point(middle.X, middle.Y, middle.Z - 50 * scale), Directions.In);
        List<Intersection> intersections = [];

        segment.AddIntersections(ray, intersections);

        return intersections;
    }

    /// <summary>
    /// The curved cubic every test above uses is, as it happens, a degenerate one: its
    /// control points make the u^3 term vanish exactly, so the "cubic" is really a parabola.
    /// That mattered a great deal.  The resultant this solve reconstructs is the determinant
    /// of an eleven-by-eleven Sylvester matrix, so its magnitude runs like the eleventh power
    /// of the segment's own coefficients -- and an entirely ordinary cubic produced one around
    /// 1e-11, with its roots sitting perfectly well within it, only for an absolute
    /// "is this nought?" test to discard the whole polynomial as noise.  The segment then drew
    /// as its two end spheres and nothing in between, silently.  Only curves whose numbers
    /// happened to come out large enough, this file's parabola among them, ever worked.
    /// </summary>
    [TestMethod]
    public void TestAGenuineCubicHasABody()
    {
        (string Name, TubeCubicSegment Segment)[] cases =
        [
            ("an arch with a real cubic term", Segment(
                new Point(0, 0, 0), new Point(1, 2, 0),
                new Point(4, 2, 0), new Point(6, 0, 0), 1)),
            ("an s-wiggle", Segment(
                new Point(0, 0, 0), new Point(1.0 / 3, 0.42, 0),
                new Point(2.0 / 3, 0.11, 0), new Point(1, 0, 0), 0.15)),
            ("a shallow arc, off the exact thirds", Segment(
                new Point(0, 0, 0), new Point(0.2, 0.33, 0),
                new Point(0.2, 0.67, 0), new Point(0, 1, 0), 0.1)),
            ("a curve that leaves its plane", Segment(
                new Point(0, 0, 0), new Point(0.41, 0.7, 0.22),
                new Point(-0.3, 1.4, -0.51), new Point(0.2, 2.1, 0.13), 0.12))
        ];

        foreach ((string name, TubeCubicSegment segment) in cases)
        {
            Assert.AreEqual(2, AcrossTheMiddle(segment).Count,
                $"{name} rendered no body between its end spheres");
        }
    }

    /// <summary>
    /// The same curve at sizes three orders of magnitude apart.  Nothing about a segment's
    /// bodily size should decide whether it renders, but an absolute threshold on a quantity
    /// that scales like an eleventh power is only ever right for one size of thing.
    /// </summary>
    [TestMethod]
    public void TestACubicHasABodyAtEverySize()
    {
        foreach (double scale in new[] { 0.001, 0.01, 0.1, 1, 10, 100, 1000 })
        {
            TubeCubicSegment segment = Segment(
                new Point(0, 0, 0), new Point(1.0 / 3, 0.42, 0),
                new Point(2.0 / 3, 0.11, 0), new Point(1, 0, 0), 0.15, scale);

            Assert.AreEqual(2, AcrossTheMiddle(segment, scale).Count,
                $"an s-wiggle scaled by {scale} rendered no body");
        }
    }

    /// <summary>
    /// A body has to be the right body, not merely present.  Every point the solve reports on
    /// the lateral surface must lie exactly one radius from the curve itself, which this
    /// checks against a dense sampling of the Bezier rather than against the solver.
    /// </summary>
    [TestMethod]
    public void TestACubicLateralHitLiesOneRadiusFromTheCurve()
    {
        const double radius = 0.15;

        TubeCubicSegment segment = Segment(
            new Point(0, 0, 0), new Point(1.0 / 3, 0.42, 0),
            new Point(2.0 / 3, 0.11, 0), new Point(1, 0, 0), radius);
        Point middle = CurveAt(segment, 0.5);
        Ray ray = new (new Point(middle.X, middle.Y, middle.Z - 50), Directions.In);
        List<Intersection> intersections = [];

        segment.AddIntersections(ray, intersections);

        Assert.AreEqual(2, intersections.Count);

        foreach (Intersection intersection in intersections)
        {
            Point point = ray.At(intersection.Distance);
            double nearest = Enumerable.Range(0, 20001)
                .Select(step => (point - CurveAt(segment, step / 20000.0)).Magnitude)
                .Min();

            Assert.IsTrue(radius.Near(nearest, 0.0001),
                $"a reported surface point sits {nearest} from the curve, not {radius}");
        }
    }
}
