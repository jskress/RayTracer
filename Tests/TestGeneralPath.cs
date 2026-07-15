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

    /// <summary>
    /// <c>Contains</c> on a plain convex, straight-edged square must accept interior points
    /// and reject points outside its bounds -- the baseline case for the even/odd test.
    /// </summary>
    [TestMethod]
    public void TestContainsOnAConvexSquare()
    {
        GeneralPath square = new GeneralPath()
            .MoveTo(-1, -1).LineTo(1, -1).LineTo(1, 1).LineTo(-1, 1).ClosePath();

        Assert.IsTrue(square.Contains(new TwoDPoint(0, 0)));
        Assert.IsTrue(square.Contains(new TwoDPoint(0.9, 0.9)));
        Assert.IsFalse(square.Contains(new TwoDPoint(1.1, 0)));
        Assert.IsFalse(square.Contains(new TwoDPoint(0, -1.1)));
    }

    /// <summary>
    /// <c>Contains</c> on a concave (L-shaped) profile must reject a point in its notch even
    /// though that point sits well within the shape's overall bounding box -- this is also
    /// the regression case for a real bug found during development: two edges that merely
    /// touch the test line at a shared vertex, without the path actually crossing from one
    /// side to the other there (a flat-topped notch, exactly like this L's inner corner),
    /// were being double-counted as two separate crossings instead of recognized as zero.
    /// </summary>
    [TestMethod]
    public void TestContainsRejectsThePointInAConcaveNotch()
    {
        GeneralPath lShape = new GeneralPath()
            .MoveTo(0, 0).LineTo(2, 0).LineTo(2, 1).LineTo(1, 1).LineTo(1, 2).LineTo(0, 2)
            .ClosePath();

        Assert.IsTrue(lShape.Contains(new TwoDPoint(0.5, 0.5)));
        Assert.IsFalse(lShape.Contains(new TwoDPoint(1.5, 1.5)));

        // The notch's inner corner sits at (1,1); a test point directly out from it, at the
        // same height as the flat-topped edge the notch cuts into, is exactly the
        // regression case described above.
        Assert.IsTrue(lShape.Contains(new TwoDPoint(0.3333333333333333, 1)));
    }

    /// <summary>
    /// <c>Contains</c> must test curved edges exactly (not by approximating them as a
    /// polyline first) -- a point that's only inside because of a curve's bulge past where a
    /// straight edge would otherwise sit must be accepted, and a point just past that bulge
    /// must be rejected.  This is also the regression case for a real bug found during
    /// development: a quadratic segment whose Y coordinate happens to vary linearly in t
    /// (an entirely ordinary curve shape, not a contrived one) made the root-solver's
    /// leading coefficient exactly zero, which its quadratic-formula solve didn't handle.
    /// </summary>
    [TestMethod]
    public void TestContainsTestsCurvedEdgesExactly()
    {
        // A shape whose right edge bulges from (1,-1) out to (1.4,0) and back to (1,1) --
        // the curve's own midpoint (t=0.5) works out to exactly (1.2,0), so a straight edge
        // here would sit at x=1, but the true boundary reaches out to x=1.2.
        GeneralPath bulging = new GeneralPath()
            .MoveTo(-1, -1).LineTo(1, -1).QuadTo(1.4, 0, 1, 1).LineTo(-1, 1).ClosePath();

        Assert.IsTrue(bulging.Contains(new TwoDPoint(1.1, 0)));
        Assert.IsFalse(bulging.Contains(new TwoDPoint(1.5, 0)));
    }
}
