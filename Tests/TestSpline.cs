using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestSpline
{
    /// <summary>
    /// A line's endpoints and midpoint must come out exactly as plain linear interpolation
    /// would give, and its tangent must be constant everywhere.
    /// </summary>
    [TestMethod]
    public void TestLineGetPointAndTangent()
    {
        SplineLine line = new () { Start = new Point(0, 0, 0), End = new Point(4, 8, -2) };

        Assert.IsTrue(line.GetPoint(0).Matches(line.Start));
        Assert.IsTrue(line.GetPoint(1).Matches(line.End));
        Assert.IsTrue(line.GetPoint(0.5).Matches(new Point(2, 4, -1)));

        Vector expectedTangent = new (4, 8, -2);

        Assert.IsTrue(line.GetTangent(0).Matches(expectedTangent));
        Assert.IsTrue(line.GetTangent(0.5).Matches(expectedTangent));
        Assert.IsTrue(line.GetTangent(1).Matches(expectedTangent));
    }

    /// <summary>
    /// A quadratic curve's endpoints must match its start/end control points exactly, its
    /// midpoint must match the well-known quadratic Bezier midpoint formula
    /// (<c>0.25*P0 + 0.5*P1 + 0.25*P2</c>), and its boundary tangents must match the
    /// standard <c>B'(0) = 2(P1-P0)</c>, <c>B'(1) = 2(P2-P1)</c> formulas -- all derived
    /// independently of this implementation's own power-basis algebra.
    /// </summary>
    [TestMethod]
    public void TestQuadCurveGetPointAndTangent()
    {
        Point p0 = new (0, 0, 0);
        Point p1 = new (2, 4, 1);
        Point p2 = new (6, 0, 2);
        SplineQuadCurve curve = new () { Start = p0, Control = p1, End = p2 };

        Assert.IsTrue(curve.GetPoint(0).Matches(p0));
        Assert.IsTrue(curve.GetPoint(1).Matches(p2));

        Point expectedMidpoint = new (
            0.25 * p0.X + 0.5 * p1.X + 0.25 * p2.X,
            0.25 * p0.Y + 0.5 * p1.Y + 0.25 * p2.Y,
            0.25 * p0.Z + 0.5 * p1.Z + 0.25 * p2.Z);

        Assert.IsTrue(curve.GetPoint(0.5).Matches(expectedMidpoint));
        Assert.IsTrue(curve.GetTangent(0).Matches(2 * (p1 - p0)));
        Assert.IsTrue(curve.GetTangent(1).Matches(2 * (p2 - p1)));
    }

    /// <summary>
    /// A quadratic curve whose control point sits exactly at the midpoint of start and end
    /// degenerates to a perfectly straight line -- the same identity exploited by
    /// <see cref="TestTubeQuadSegment"/> -- so it must produce the same points and tangent
    /// as an equivalent <see cref="SplineLine"/> everywhere.
    /// </summary>
    [TestMethod]
    public void TestQuadCurveWithMidpointControlMatchesLine()
    {
        Point start = new (1, 2, 3);
        Point end = new (7, -4, 9);
        Point midpoint = new ((start.X + end.X) / 2, (start.Y + end.Y) / 2, (start.Z + end.Z) / 2);
        SplineQuadCurve curve = new () { Start = start, Control = midpoint, End = end };
        SplineLine line = new () { Start = start, End = end };

        foreach (double u in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
        {
            Assert.IsTrue(curve.GetPoint(u).Matches(line.GetPoint(u)));
            Assert.IsTrue(curve.GetTangent(u).Matches(line.GetTangent(u)));
        }
    }

    /// <summary>
    /// A cubic curve's endpoints must match its start/end control points exactly, and its
    /// boundary tangents must match the standard <c>B'(0) = 3(P1-P0)</c>,
    /// <c>B'(1) = 3(P3-P2)</c> formulas.
    /// </summary>
    [TestMethod]
    public void TestCubicCurveGetPointAndTangent()
    {
        Point p0 = new (0, 0, 0);
        Point p1 = new (2, 4, 1);
        Point p2 = new (5, 4, -1);
        Point p3 = new (7, 0, 0);
        SplineCubicCurve curve = new () { Start = p0, Control1 = p1, Control2 = p2, End = p3 };

        Assert.IsTrue(curve.GetPoint(0).Matches(p0));
        Assert.IsTrue(curve.GetPoint(1).Matches(p3));
        Assert.IsTrue(curve.GetTangent(0).Matches(3 * (p1 - p0)));
        Assert.IsTrue(curve.GetTangent(1).Matches(3 * (p3 - p2)));
    }

    /// <summary>
    /// A cubic curve whose two control points sit evenly spaced (at u = 1/3 and 2/3) along
    /// the line from start to end degenerates to a perfectly straight line -- the same
    /// identity exploited by <see cref="TestTubeCubicSegment"/> -- so it must produce the
    /// same points and tangent as an equivalent <see cref="SplineLine"/> everywhere.
    /// </summary>
    [TestMethod]
    public void TestCubicCurveWithEvenlySpacedControlsMatchesLine()
    {
        Point start = new (0, -10, 0);
        Point end = new (0, 10, 0);
        SplineCubicCurve curve = new ()
        {
            Start = start,
            Control1 = new Point(0, -10.0 / 3, 0),
            Control2 = new Point(0, 10.0 / 3, 0),
            End = end
        };
        SplineLine line = new () { Start = start, End = end };

        foreach (double u in new[] { 0.0, 0.25, 0.5, 0.75, 1.0 })
        {
            Assert.IsTrue(curve.GetPoint(u).Matches(line.GetPoint(u)));
            Assert.IsTrue(curve.GetTangent(u).Matches(line.GetTangent(u)));
        }
    }

    /// <summary>
    /// A spline built from a mix of straight, quadratic and cubic segments must build the
    /// matching concrete curve type for each segment, and thread each curve's start from
    /// the previous one's end (or the spline's own start, for the first curve).
    /// </summary>
    [TestMethod]
    public void TestSplineBuildsMatchingCurveTypesAndThreadsPoints()
    {
        Point p0 = new (0, 0, 0);
        Point p1 = new (1, 0, 0);
        Point control = new (2, 1, 0);
        Point p2 = new (3, 0, 0);
        Point control1 = new (4, 1, 0);
        Point control2 = new (5, -1, 0);
        Point p3 = new (6, 0, 0);
        Spline spline = new ()
        {
            Start = p0,
            Segments =
            {
                new SplineSegmentSpec { End = p1 },
                new SplineSegmentSpec { Control1 = control, End = p2 },
                new SplineSegmentSpec { Control1 = control1, Control2 = control2, End = p3 }
            }
        };

        List<ISplineCurve> curves = spline.GetCurves();

        Assert.AreEqual(3, curves.Count);
        Assert.IsInstanceOfType<SplineLine>(curves[0]);
        Assert.IsInstanceOfType<SplineQuadCurve>(curves[1]);
        Assert.IsInstanceOfType<SplineCubicCurve>(curves[2]);

        Assert.IsTrue(curves[0].GetPoint(0).Matches(p0));
        Assert.IsTrue(curves[0].GetPoint(1).Matches(p1));
        Assert.IsTrue(curves[1].GetPoint(0).Matches(p1));
        Assert.IsTrue(curves[1].GetPoint(1).Matches(p2));
        Assert.IsTrue(curves[2].GetPoint(0).Matches(p2));
        Assert.IsTrue(curves[2].GetPoint(1).Matches(p3));
    }
}
