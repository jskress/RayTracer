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
}
