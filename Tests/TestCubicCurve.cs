using RayTracer.Extensions;
using RayTracer.Graphics;

namespace Tests;

[TestClass]
public class TestCubicCurve
{
    /// <summary>
    /// This test verifies the cubic-curve intersection math (now backed by
    /// MathNet.Numerics.Polynomial.Roots() instead of a hand-rolled Cardano/trig solver)
    /// still finds a genuine root correctly.  Rather than hand-solving a cubic, this picks
    /// an arbitrary curve parameter (t = 0.3), computes the exact point on the curve there
    /// using the Bezier formula directly, and constructs a ray whose origin *is* that
    /// point -- guaranteeing a true intersection at t = 0.3 (ray distance 0) without
    /// needing to invert the cubic by hand.  Control points are deliberately asymmetric so
    /// neither x(t) nor y(t) degenerates below a genuine cubic.
    /// </summary>
    [TestMethod]
    public void TestFindsKnownPointOnCurve()
    {
        GeneralPath path = new GeneralPath()
            .MoveTo(0, 0)
            .CubicTo(0.5, 2, 2.5, -1, 3, 1);
        CubicCurve curve = (CubicCurve) path.Segments[0];

        // B(0.3) computed directly via the cubic Bezier formula:
        // (1-t)^3 P0 + 3(1-t)^2 t P1 + 3(1-t) t^2 P2 + t^3 P3
        const double expectedX = 0.774;
        const double expectedY = 0.72;
        TwoDRay ray = new () { Origin = new TwoDPoint(expectedX, expectedY), Direction = new TwoDVector(1, 0.5) };

        TwoDIntersection[] intersections = curve.GetIntersections(ray).ToArray();

        Assert.IsTrue(intersections.Length > 0);
        Assert.IsTrue(intersections.Any(intersection =>
            0.3.Near(intersection.Distance, 0.0001) &&
            expectedX.Near(intersection.Point.X, 0.0001) &&
            expectedY.Near(intersection.Point.Y, 0.0001)));
    }

    /// <summary>
    /// A ray that passes nowhere near the curve should report no intersections.
    /// </summary>
    [TestMethod]
    public void TestMissesCurve()
    {
        GeneralPath path = new GeneralPath()
            .MoveTo(0, 0)
            .CubicTo(0.5, 2, 2.5, -1, 3, 1);
        CubicCurve curve = (CubicCurve) path.Segments[0];
        TwoDRay ray = new () { Origin = new TwoDPoint(0, 100), Direction = new TwoDVector(1, 0) };

        TwoDIntersection[] intersections = curve.GetIntersections(ray).ToArray();

        Assert.AreEqual(0, intersections.Length);
    }

    /// <summary>
    /// Regression test for a real bug found during development: a cubic curve whose Y
    /// coordinate happens to vary quadratically (not cubically) in t -- an entirely
    /// ordinary curve shape, symmetric start/end control points being one easy way to get
    /// one, not a contrived edge case -- makes the true t^3 coefficient of the intersection
    /// polynomial exactly zero mathematically, but floating-point cancellation while
    /// building it (subtracting near-equal quantities) leaves a tiny nonzero residual
    /// (~1e-16) instead.  A general cubic solver, not knowing that residual is really zero,
    /// solves the wrong (numerically near-singular) cubic and silently returns the wrong
    /// roots instead of the true quadratic's two real ones.
    /// </summary>
    [TestMethod]
    public void TestFindsBothRootsWhenTheCubicTermIsNegligible()
    {
        GeneralPath path = new GeneralPath()
            .MoveTo(-1, 0.6)
            .CubicTo(-0.55, 1.3, 0.55, 1.3, 1, 0.6);
        CubicCurve curve = (CubicCurve) path.Segments[0];
        TwoDRay ray = new () { Origin = new TwoDPoint(0, 0.9), Direction = new TwoDVector(1, 0) };

        TwoDIntersection[] intersections = curve.GetIntersections(ray).ToArray();

        // The curve's Y coordinate, as a function of t, is the quadratic
        // -2.1t^2 + 2.1t + 0.6, which crosses Y = 0.9 at t ~= 0.1727 and t ~= 0.8273 -- both
        // within [0, 1], so both are genuine points on the curve.  Only the second (whose X
        // is positive) lies ahead of this rightward ray from the origin.
        Assert.AreEqual(1, intersections.Length);
        Assert.IsTrue(0.8273.Near(intersections[0].Distance, 0.0001));
        Assert.IsTrue(0.9.Near(intersections[0].Point.Y, 0.0001));
        Assert.IsTrue(intersections[0].Point.X > 0);
    }
}
