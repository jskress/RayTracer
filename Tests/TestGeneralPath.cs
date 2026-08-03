using RayTracer.Extensions;
using RayTracer.Fonts;
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

    /// <summary>
    /// This tests extracting subpaths from a path.  Note that this is specific to the Merriweather
    /// font.
    /// </summary>
    [TestMethod]
    public void TestFindSubPaths()
    {
        GeneralPath path = GetGlyphPath(".");
        List<SubPath> subPaths = path.FindAllSubPaths();

        Assert.HasCount(1, subPaths);

        path = GetGlyphPath("A");
        subPaths = path.FindAllSubPaths();

        Assert.HasCount(2, subPaths);
    }

    /// <summary>
    /// This method is used to verify that the Contains method that checks for one path
    /// contained by another works.
    /// </summary>
    [TestMethod]
    public void TestContainsPath()
    {
        // Negative case. A colon will have two paths, neither contained by the other.
        GeneralPath path = GetGlyphPath(":");
        List<SubPath> subPaths = path.FindAllSubPaths();

        Assert.HasCount(2, subPaths);

        Assert.IsFalse(subPaths[0].Path.Contains(subPaths[1].Path));
        Assert.IsFalse(subPaths[1].Path.Contains(subPaths[0].Path));

        // Positive case
        path = GetGlyphPath("A");
        subPaths = path.FindAllSubPaths();

        Assert.HasCount(2, subPaths);

        Assert.IsTrue(subPaths[0].Path.Contains(subPaths[1].Path));
        Assert.IsFalse(subPaths[1].Path.Contains(subPaths[0].Path));
    }

    /// <summary>
    /// This tests that a run of straight lines reports the direction it was drawn in.
    /// </summary>
    [TestMethod]
    public void TestIsCounterClockwise()
    {
        GeneralPath path = new GeneralPath()
            .MoveTo(-1, -1).LineTo(1, -1).LineTo(1, 1).LineTo(-1, 1).ClosePath();

        Assert.IsTrue(path.IsCounterClockwise());

        path = new GeneralPath()
            .MoveTo(-1, -1).LineTo(-1, 1).LineTo(1, 1).LineTo(1, -1).ClosePath();

        Assert.IsFalse(path.IsCounterClockwise());
    }

    /// <summary>
    /// This tests that direction comes from the curves themselves rather than from the polygon
    /// their endpoints make.  A circle drawn as two half-circle curves has all three of its
    /// endpoints on one line, so that polygon encloses nothing and cannot say which way the
    /// circle runs.
    /// </summary>
    [TestMethod]
    public void TestIsCounterClockwiseForCurves()
    {
        const double control = 4.0 / 3.0;

        GeneralPath path = new GeneralPath()
            .MoveTo(1, 0)
            .CubicTo(1, control, -1, control, -1, 0)
            .CubicTo(-1, -control, 1, -control, 1, 0);

        Assert.IsTrue(path.IsCounterClockwise());

        path = new GeneralPath()
            .MoveTo(1, 0)
            .CubicTo(1, -control, -1, -control, -1, 0)
            .CubicTo(-1, control, 1, control, 1, 0);

        Assert.IsFalse(path.IsCounterClockwise());
    }

    /// <summary>
    /// This tests the area a path reports, since that is what its direction is read from.  The
    /// area is exact for curves, so a circle drawn as four Bezier arcs comes out at pi.
    /// </summary>
    [TestMethod]
    public void TestSignedArea()
    {
        GeneralPath path = new GeneralPath()
            .MoveTo(-1, -1).LineTo(1, -1).LineTo(1, 1).LineTo(-1, 1).ClosePath();

        Assert.IsTrue(4.0.Near(path.SignedArea()));

        // The same square, drawn the other way round, encloses the same area, negated.
        path = new GeneralPath()
            .MoveTo(-1, -1).LineTo(-1, 1).LineTo(1, 1).LineTo(1, -1).ClosePath();

        Assert.IsTrue((-4.0).Near(path.SignedArea()));

        // An empty path encloses nothing, and says so rather than failing.
        Assert.IsTrue(0.0.Near(new GeneralPath().SignedArea()));

        // The usual four-arc approximation of the unit circle, to within its own error.
        const double control = 0.5522847498307933;

        path = new GeneralPath()
            .MoveTo(1, 0)
            .CubicTo(1, control, control, 1, 0, 1)
            .CubicTo(-control, 1, -1, control, -1, 0)
            .CubicTo(-1, -control, -control, -1, 0, -1)
            .CubicTo(control, -1, 1, -control, 1, 0);

        Assert.IsTrue(Math.Abs(Math.PI - path.SignedArea()) < 0.002);
    }

    /// <summary>
    /// This tests that runs are told apart when they are left open, so a "move to" ends the
    /// run before it just as closing the run does.
    /// </summary>
    [TestMethod]
    public void TestFindSubPathsForOpenRuns()
    {
        GeneralPath path = new GeneralPath()
            .MoveTo(0, 0).LineTo(1, 0).LineTo(1, 1)
            .MoveTo(4, 4).LineTo(5, 4).LineTo(5, 5);
        List<SubPath> subPaths = path.FindAllSubPaths();

        Assert.HasCount(2, subPaths);
        Assert.HasCount(2, subPaths[0].Path.Segments);
        Assert.HasCount(2, subPaths[1].Path.Segments);
    }

    /// <summary>
    /// This tests that normalizing sets the outermost runs counter-clockwise and each nested
    /// run against its parent, so that every side wall of the solid it becomes faces outward.
    /// Note that this is specific to the Merriweather font.
    /// </summary>
    [TestMethod]
    public void TestNormalizeFor3D()
    {
        // An "A" is an outer run with the triangular hole of the letter nested inside it.
        GeneralPath path = GetGlyphPath("A").NormalizeFor3D();
        List<SubPath> subPaths = path.FindAllSubPaths();

        Assert.HasCount(2, subPaths);

        GeneralPath.ArrangeSubPaths(subPaths);

        Assert.HasCount(1, subPaths);
        Assert.HasCount(1, subPaths[0].ContainedPaths);
        Assert.IsTrue(subPaths[0].Path.IsCounterClockwise());
        Assert.IsFalse(subPaths[0].ContainedPaths[0].Path.IsCounterClockwise());

        // Normalizing a path that is already normalized must leave it exactly as it is.
        List<IPathSegment> was = [..path.Segments];

        path.NormalizeFor3D();

        Assert.AreEqual(was.Count, path.Segments.Count);

        for (int index = 0; index < was.Count; index++)
            Assert.AreSame(was[index], path.Segments[index]);
    }

    /// <summary>
    /// This tests that runs are nested as deeply as they actually sit, and that each level of
    /// nesting comes out running opposite the one above it, whichever way the runs were drawn.
    /// Runs side by side, with neither inside the other, are both outermost.
    /// </summary>
    [TestMethod]
    public void TestNormalizeFor3DForNestedRuns()
    {
        // Three runs, one inside the next, all drawn clockwise, plus a fourth off to the side.
        GeneralPath path = new GeneralPath()
            .MoveTo(-4, -4).LineTo(-4, 4).LineTo(4, 4).LineTo(4, -4).ClosePath()
            .MoveTo(-3, -3).LineTo(-3, 3).LineTo(3, 3).LineTo(3, -3).ClosePath()
            .MoveTo(-2, -2).LineTo(-2, 2).LineTo(2, 2).LineTo(2, -2).ClosePath()
            .MoveTo(10, -1).LineTo(10, 1).LineTo(12, 1).LineTo(12, -1).ClosePath()
            .NormalizeFor3D();
        List<SubPath> subPaths = path.FindAllSubPaths();

        GeneralPath.ArrangeSubPaths(subPaths);

        Assert.HasCount(2, subPaths);

        SubPath outer = subPaths[0];
        SubPath middle = outer.ContainedPaths[0];

        Assert.HasCount(1, outer.ContainedPaths);
        Assert.HasCount(1, middle.ContainedPaths);
        Assert.IsEmpty(middle.ContainedPaths[0].ContainedPaths);

        Assert.IsTrue(outer.Path.IsCounterClockwise());
        Assert.IsFalse(middle.Path.IsCounterClockwise());
        Assert.IsTrue(middle.ContainedPaths[0].Path.IsCounterClockwise());

        // The run off to the side is nobody's child, so it is outermost in its own right.
        Assert.IsEmpty(subPaths[1].ContainedPaths);
        Assert.IsTrue(subPaths[1].Path.IsCounterClockwise());
    }

    /// <summary>
    /// This tests that normalizing a path that has no direction to speak of leaves it alone
    /// rather than failing.  A space in a run of text gives an empty path, and a single
    /// segment on its own encloses nothing.
    /// </summary>
    [TestMethod]
    public void TestNormalizeFor3DForDegeneratePaths()
    {
        GeneralPath path = new GeneralPath().NormalizeFor3D();

        Assert.IsEmpty(path.Segments);

        path = new GeneralPath()
            .MoveTo(0, 0).LineTo(1, 1)
            .NormalizeFor3D();

        Assert.HasCount(1, path.Segments);
        Assert.AreEqual(new TwoDPoint(0, 0), path.Segments[0].Points[0]);
        Assert.AreEqual(new TwoDPoint(1, 1), path.Segments[0].Points[^1]);
    }

    private static GeneralPath GetGlyphPath(string text)
    {
        List<GeneralPath> glyphs = TextOutline.Glyphs(
            "Merriweather", FontWeight.Regular, false, new TextLayoutSettings(), null,
            text);

        Assert.HasCount(1, glyphs);

        return glyphs[0];
    }
}
