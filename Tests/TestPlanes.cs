using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace Tests;

[TestClass]
public class TestPlanes
{
    [TestMethod]
    public void TestIntersection()
    {
        Plane plane = new ();
        Point origin = new (0, 10, 0);
        Vector direction = new (0, 0, 1);
        Ray ray = new (origin, direction);
        List<Intersection> intersections = new ();

        plane.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);

        ray = new Ray(Point.Zero, direction);

        plane.AddIntersections(ray, intersections);

        Assert.AreEqual(0, intersections.Count);

        origin = new Point(0, 1, 0);
        direction = new Vector(0, -1, 0);
        ray = new Ray(origin, direction);

        plane.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
        Assert.AreEqual(1, intersections[0].Distance);
        Assert.AreSame(plane, intersections[0].Surface);

        intersections.Clear();

        origin = new Point(0, -1, 0);
        direction = new Vector(0, 1, 0);
        ray = new Ray(origin, direction);

        plane.AddIntersections(ray, intersections);

        Assert.AreEqual(1, intersections.Count);
        Assert.AreEqual(1, intersections[0].Distance);
        Assert.AreSame(plane, intersections[0].Surface);
    }

    [TestMethod]
    public void TestNormal()
    {
        Plane plane = new ();
        Vector n1 = plane.SurfaceNormaAt(Point.Zero, null);
        Vector n2 = plane.SurfaceNormaAt(new Point(10, 0, -10), null);
        Vector n3 = plane.SurfaceNormaAt(new Point(-5, 0, 150), null);
        Vector expected = new Vector(0, 1, 0);

        Assert.IsTrue(expected.Matches(n1));
        Assert.IsTrue(expected.Matches(n2));
        Assert.IsTrue(expected.Matches(n3));
    }
}
