using RayTracer.Extensions;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestGeneralPath
{
    /// <summary>
    /// A line's <c>GetPoint</c> must match plain linear interpolation at its endpoints and
    /// midpoint.
    /// </summary>
    [TestMethod]
    public void TestLineGetPoint()
    {
        GeneralPath path = new GeneralPath()
            .MoveTo(0, 0)
            .LineTo(4, 8);
        Line line = (Line) path.Segments[0];

        Assert.IsTrue(0.0.Near(line.GetPoint(0).X) && 0.0.Near(line.GetPoint(0).Y));
        Assert.IsTrue(4.0.Near(line.GetPoint(1).X) && 8.0.Near(line.GetPoint(1).Y));
        Assert.IsTrue(2.0.Near(line.GetPoint(0.5).X) && 4.0.Near(line.GetPoint(0.5).Y));
    }

    /// <summary>
    /// A quad curve's <c>GetPoint</c> must match its own endpoints exactly and the
    /// well-known quadratic Bezier midpoint formula (<c>0.25*P0 + 0.5*P1 + 0.25*P2</c>).
    /// </summary>
    [TestMethod]
    public void TestQuadCurveGetPoint()
    {
        GeneralPath path = new GeneralPath()
            .MoveTo(0, 0)
            .QuadTo(2, 4, 6, 0);
        IPathSegment curve = path.Segments[0];

        Assert.IsTrue(0.0.Near(curve.GetPoint(0).X) && 0.0.Near(curve.GetPoint(0).Y));
        Assert.IsTrue(6.0.Near(curve.GetPoint(1).X) && 0.0.Near(curve.GetPoint(1).Y));

        TwoDPoint mid = curve.GetPoint(0.5);

        Assert.IsTrue((0.25 * 0 + 0.5 * 2 + 0.25 * 6).Near(mid.X));
        Assert.IsTrue((0.25 * 0 + 0.5 * 4 + 0.25 * 0).Near(mid.Y));
    }

    /// <summary>
    /// A cubic curve's <c>GetPoint</c> must match its own endpoints exactly, reusing the
    /// same known-point technique as <see cref="TestCubicCurve.TestFindsKnownPointOnCurve"/>.
    /// </summary>
    [TestMethod]
    public void TestCubicCurveGetPoint()
    {
        GeneralPath path = new GeneralPath()
            .MoveTo(0, 0)
            .CubicTo(0.5, 2, 2.5, -1, 3, 1);
        CubicCurve curve = (CubicCurve) path.Segments[0];

        Assert.IsTrue(0.0.Near(curve.GetPoint(0).X) && 0.0.Near(curve.GetPoint(0).Y));
        Assert.IsTrue(3.0.Near(curve.GetPoint(1).X) && 1.0.Near(curve.GetPoint(1).Y));

        // B(0.3), computed independently via the cubic Bezier formula (matching the
        // TestCubicCurve known-point test's own derivation).
        TwoDPoint atPoint3 = curve.GetPoint(0.3);

        Assert.IsTrue(0.774.Near(atPoint3.X, 0.0001));
        Assert.IsTrue(0.72.Near(atPoint3.Y, 0.0001));
    }

    /// <summary>
    /// Sampling a multi-segment path must produce exactly <c>stepsPerSegment * segmentCount
    /// + 1</c> points (no duplicated points at segment boundaries), starting and ending at
    /// the path's own start and end points.
    /// </summary>
    [TestMethod]
    public void TestSampleProducesContinuousPolylineWithoutDuplicateBoundaryPoints()
    {
        GeneralPath path = new GeneralPath()
            .MoveTo(0, 0)
            .LineTo(2, 0)
            .QuadTo(3, 2, 4, 0)
            .CubicTo(5, 2, 6, -2, 7, 0);

        List<TwoDPoint> points = path.Sample(10);

        Assert.AreEqual(31, points.Count);
        Assert.IsTrue(0.0.Near(points[0].X) && 0.0.Near(points[0].Y));
        Assert.IsTrue(7.0.Near(points[^1].X) && 0.0.Near(points[^1].Y));

        // The shared boundary between the line and the quad curve (at (2, 0)) must appear
        // exactly once, not twice.
        Assert.AreEqual(1, points.Count(p => p.X.Near(2) && p.Y.Near(0)));
    }

    /// <summary>
    /// Sampling a closed path (one that ends with an explicit closing line back to its own
    /// start) must produce a polyline whose first and last points coincide.
    /// </summary>
    [TestMethod]
    public void TestSampleOfClosedPathReturnsToStart()
    {
        GeneralPath path = new GeneralPath()
            .MoveTo(0, 0)
            .LineTo(4, 0)
            .LineTo(4, 4)
            .LineTo(0, 4)
            .ClosePath();

        List<TwoDPoint> points = path.Sample(4);

        Assert.IsTrue(points[0].X.Near(points[^1].X) && points[0].Y.Near(points[^1].Y));
    }
}
