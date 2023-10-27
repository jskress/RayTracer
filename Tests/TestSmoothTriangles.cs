using RayTracer;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestSmoothTriangles
{
    private static readonly Point Point1 = new (0, 1, 0);
    private static readonly Point Point2 = new (-1, 0, 0);
    private static readonly Point Point3 = new (1, 0, 0);

    private SmoothTriangle _smoothTriangle = new (
        Point1, Point2, Point3,
        Directions.Up, Directions.Left, Directions.Right);

    [TestMethod]
    public void TestConstruction()
    {
        Assert.AreSame(Point1, _smoothTriangle.Point1);
        Assert.AreSame(Point2, _smoothTriangle.Point2);
        Assert.AreSame(Point3, _smoothTriangle.Point3);
        Assert.AreSame(Directions.Up, _smoothTriangle.Normal1);
        Assert.AreSame(Directions.Left, _smoothTriangle.Normal2);
        Assert.AreSame(Directions.Right, _smoothTriangle.Normal3);
    }

    [TestMethod]
    public void TestUvIntersectionCreation()
    {
        SmoothTriangleIntersection intersection = new (
            _smoothTriangle, 3.5, 0.2, 0.4);

        Assert.AreSame(_smoothTriangle, intersection.Surface);
        Assert.AreEqual(3.5, intersection.Distance);
        Assert.AreEqual(0.2, intersection.U);
        Assert.AreEqual(0.4, intersection.V);
    }

    [TestMethod]
    public void TestRayIntersectionProducesUv()
    {
        Ray ray = new (new Point(-0.2, 0.3, -2), Directions.In);
        List<Intersection> intersections = new ();

        _smoothTriangle.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
        Assert.IsTrue(intersections[0] is SmoothTriangleIntersection);

        SmoothTriangleIntersection intersection = (SmoothTriangleIntersection) intersections[0];

        Assert.IsTrue(0.45.Near(intersection.U));
        Assert.IsTrue(0.25.Near(intersection.V));
    }

    [TestMethod]
    public void TestNormalInterpolation()
    {
        SmoothTriangleIntersection intersection = new (
            _smoothTriangle, 1, 0.45, 0.25);
        Vector normal = _smoothTriangle.NormaAt(Point.Zero, intersection);
        Vector expected = new (-0.5547, 0.83205, 0);

        Assert.IsTrue(expected.Matches(normal));
    }

    [TestMethod]
    public void TestNormalAt()
    {
        SmoothTriangleIntersection intersection = new (
            _smoothTriangle, 1, 0.45, 0.25);
        Ray ray = new Ray(new Point(-0.2, 0.3, -2), Directions.In);
        List<Intersection> intersections = new () { intersection };
        Vector expected = new (-0.5547, 0.83205, 0);

        intersection.PrepareUsing(ray, intersections);

        Assert.IsTrue(expected.Matches(intersection.Normal));
    }
}
