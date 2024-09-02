using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestTriangles
{
    [TestMethod]
    public void TestTriangleNormal()
    {
        Triangle triangle = new ()
        {
            Point1 = new Point(0, 1, 0),
            Point2 = new Point(-1, 0, 0),
            Point3 = new Point(1, 0, 0)
        };
        Vector n1 = triangle.SurfaceNormaAt(new Point(0, 0.5, 0), null);
        Vector n2 = triangle.SurfaceNormaAt(new Point(-0.5, 0.75, 0), null);
        Vector n3 = triangle.SurfaceNormaAt(new Point(0.5, 0.25, 0), null);

        Assert.IsTrue(n1.Matches(n2));
        Assert.IsTrue(n1.Matches(n3));
        Assert.IsTrue(n2.Matches(n3));
    }

    [TestMethod]
    public void TestParallelRayMisses()
    {
        Triangle triangle = new ()
        {
            Point1 = new Point(0, 1, 0),
            Point2 = new Point(-1, 0, 0),
            Point3 = new Point(1, 0, 0)
        };
        Ray ray = new Ray(new Point(0, -1, -2), Directions.Up);
        List<Intersection> intersections = new ();

        triangle.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayMissesOverP1P3()
    {
        Triangle triangle = new ()
        {
            Point1 = new Point(0, 1, 0),
            Point2 = new Point(-1, 0, 0),
            Point3 = new Point(1, 0, 0)
        };
        Ray ray = new Ray(new Point(1, 1, -2), Directions.In);
        List<Intersection> intersections = new ();

        triangle.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayMissesOverP1P2()
    {
        Triangle triangle = new ()
        {
            Point1 = new Point(0, 1, 0),
            Point2 = new Point(-1, 0, 0),
            Point3 = new Point(1, 0, 0)
        };
        Ray ray = new Ray(new Point(-1, 1, -2), Directions.In);
        List<Intersection> intersections = new ();

        triangle.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayMissesOverP2P3()
    {
        Triangle triangle = new ()
        {
            Point1 = new Point(0, 1, 0),
            Point2 = new Point(-1, 0, 0),
            Point3 = new Point(1, 0, 0)
        };
        Ray ray = new Ray(new Point(0, -1, -2), Directions.In);
        List<Intersection> intersections = new ();

        triangle.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);
    }

    [TestMethod]
    public void TestRayHits()
    {
        Triangle triangle = new ()
        {
            Point1 = new Point(0, 1, 0),
            Point2 = new Point(-1, 0, 0),
            Point3 = new Point(1, 0, 0)
        };
        Ray ray = new Ray(new Point(0, 0.5, -2), Directions.In);
        List<Intersection> intersections = new ();

        triangle.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
        Assert.AreEqual(2, intersections[0].Distance);
    }
}
